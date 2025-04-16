using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using AvsFilterNet;

namespace AutoOverlay.Overlay
{
    public record OverlayLayer
    {
        public dynamic Clip { get; set; }
        public dynamic Mask { get; }
        public dynamic WhiteMask { get; }
        public double Opacity { get; }
        public Rectangle Rectangle { get; } // after rotation and canvas intersection
        public Rectangle ExtraBorders { get; }
        public bool Source { get; }
        public List<OverlayInfo> History { get; }
        public int Index { get; private set; }
        public Rectangle ActiveArea { get; }
        public OverlayData Data { get; }

        public bool Rotation => !Source && Data.OverlayAngle != 0;

        public static List<OverlayLayer> GetLayers(int frameNumber, OverlayContext ctx, List<OverlayInfo> history, List<OverlayInfo>[] extra)
        {
            List<OverlayInfo> CorrectChromaLocation(List<OverlayInfo> stats, Func<ChromaLocation?> layer)
            {
                if (!ctx.Plane.IsChroma())
                    return stats;
                var src = ctx.Render.SrcChromaLocation() ?? ctx.Render.OverChromaLocation();
                if (!src.HasValue)
                    return stats;
                var over = layer();
                if (!over.HasValue)
                    return stats;
                var shift = over.Value.GetShift(src.Value);
                if (shift.IsNearZero())
                    return stats;
                return stats.Select(p => p.Clone().Also(p => p.Placement = p.Placement.Add(shift))).ToList();
            }

            var mainHistory = CorrectChromaLocation(history, ctx.Render.OverChromaLocation);
            var mainExtra = extra.Select((p, i) => CorrectChromaLocation(p, ctx.Render.ExtraChromaLocation[i])).ToArray();

            var data = OverlayMapper.For(frameNumber, ctx.Input, mainHistory, ctx.Render.Stabilization, mainExtra).GetOverlayData();
            OverlayData lumaData = null;
            if (ctx.Plane.IsChroma())
            {
                lumaData = OverlayMapper.For(frameNumber, ctx.Input.Scale(ctx.SubSample), history, ctx.Render.Stabilization, extra).GetOverlayData();
            }
            var layers = new List<OverlayLayer>(2 + ctx.ExtraClips.Count)
            {
                new(data, true, ctx.Source, ctx.SourceMask, 1, ctx.Render, ctx, lumaData, mainHistory, ctx.SubSample)
            };
            var extraLayers = ctx.ExtraClips
                .Select((tuple, i) => new OverlayLayer(data.ExtraClips[i], false, tuple.Clip, tuple.Mask, tuple.Opacity, ctx.Render, ctx, lumaData?.ExtraClips[i], mainExtra[i], ctx.SubSample));
            layers.AddRange(extraLayers);
            layers.Insert(ctx.Render.OverlayOrder + 1, new(data, false, ctx.Overlay, ctx.OverlayMask, ctx.Render.Opacity, ctx.Render, ctx, lumaData, mainHistory, ctx.SubSample));
            for (var i = 0; i < layers.Count; i++)
                layers[i].Index = i;
            return layers;
        }

        private OverlayLayer(OverlayData data, bool source, Clip clip, Clip mask, double opacity,
            OverlayRender render, OverlayContext ctx, OverlayData lumaData, List<OverlayInfo> history, Size subSample)
        {
            Data = data;
            Source = source;
            Opacity = opacity;
            History = history;
            ActiveArea = data.ActiveArea;
            RectangleD crop;
            float angle;
            Warp warp;
            Rectangle unrotated;
            if (Source)
            {
                unrotated = data.Source;
                crop = data.SourceCrop;
                angle = 0;
                warp = data.SourceWarp;
            }
            else
            {
                unrotated = data.Overlay;
                crop = data.OverlayCrop;
                angle = data.OverlayAngle;
                warp = data.OverlayWarp;
                if (mask == null && angle != 0)
                    mask = AvsUtils.InitClip(clip.Dynamic(), unrotated.Size, ctx.Plane.IsRgb() ? 0xFFFFFF : 0xFF8080);
            }
            var vi = clip.GetVideoInfo();
            var bitDepth = vi.pixel_type.GetBitDepth();
            var size = vi.GetSize();

            var canvas = new Rectangle(Point.Empty, ctx.Input.TargetSize);
            var rotated = new Rectangle(unrotated.Location, BilinearRotate.CalculateSize(unrotated.Size, angle).Floor());
            Rectangle = Rectangle.Intersect(canvas, rotated);
            var roi = Rectangle with
            {
                X = Math.Max(0, -rotated.X), 
                Y = Math.Max(0, -rotated.Y)
            };

            var resizeFunc = Rectangle.Width > size.Width ? render.Upsize : render.Downsize;
            if (ctx.Plane.IsChroma() && render.ChromaResize != null)
                resizeFunc = render.ChromaResize;

            dynamic Prepare(Clip clp, Warp warp) => render.ResizeRotate(clp, resizeFunc, render.Rotate, unrotated.Width, unrotated.Height, angle, crop, warp)?.ROI(roi);

            Clip = Prepare(clip, warp);
            Mask = render.MaskMode ? null : Prepare(mask, warp);
            if (Rotation)
            {
                Mask ??= WhiteMask = Prepare(AvsUtils.InitClip(clip.Dynamic(), unrotated.Size, ctx.Plane.IsRgb() ? 0xFFFFFF : 0xFF8080), Warp.Empty);
            }
            else
            {
                WhiteMask = AvsUtils.GetBlankClip(Clip, true);
            }

            if (render.ColorAdjust < double.Epsilon && bitDepth != render.BitDepth)
            {
                Clip = Clip.ConvertBits(render.BitDepth);
            }

            if (lumaData == null)
            {
                ExtraBorders = Rectangle.Empty;
            }
            else
            {
                var luma = Source ? lumaData.Source : lumaData.Overlay;
                var chroma = Rectangle.FromLTRB(
                    Rectangle.Left * subSample.Width, 
                    Rectangle.Top * subSample.Height,
                    Rectangle.Right * subSample.Width,
                    Rectangle.Bottom * subSample.Height);
                ExtraBorders = Rectangle.FromLTRB(
                    Math.Max(0, Math.Sign(chroma.Left - luma.Left)),
                    Math.Max(0, Math.Sign(chroma.Top - luma.Top)),
                    Math.Max(0, Math.Sign(luma.Right - chroma.Right)),
                    Math.Max(0, Math.Sign(luma.Bottom - chroma.Bottom)));
            }
        }
    }
}
