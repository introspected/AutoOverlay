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
        public string ResizeFunc { get; }

        public bool Rotation => !Source && Data.OverlayAngle != 0;

        public static List<OverlayLayer> GetLayers(int frameNumber, OverlayContext ctx, List<OverlayInfo> history, List<OverlayInfo>[] extra)
        {
            var data = OverlayMapper.For(frameNumber, ctx.Input, history, ctx.Render.Stabilization, extra).GetOverlayData();
            OverlayData lumaData = null;
            if (ctx.Plane.IsChroma())
            {
                lumaData = OverlayMapper.For(frameNumber, ctx.Input.Scale(ctx.SubSample), history, ctx.Render.Stabilization, extra).GetOverlayData();
            }
            var layers = new List<OverlayLayer>(2 + ctx.ExtraClips.Count)
            {
                new(data, true, ctx.Source, ctx.SourceMask, 1, ctx.Render, ctx, lumaData, history, ctx.SubSample)
            };
            var extraLayers = ctx.ExtraClips
                .Select((tuple, i) => new OverlayLayer(data.ExtraClips[i], false, tuple.Clip, tuple.Mask, tuple.Opacity, ctx.Render, ctx, lumaData?.ExtraClips[i], extra[i], ctx.SubSample));
            layers.AddRange(extraLayers);
            layers.Insert(ctx.Render.OverlayOrder + 1, new(data, false, ctx.Overlay, ctx.OverlayMask, ctx.Render.Opacity, ctx.Render, ctx, lumaData, history, ctx.SubSample));
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
            RectangleF crop;
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

            ResizeFunc = Rectangle.Width > size.Width ? render.Upsize : render.Downsize;

            dynamic Prepare(Clip clp, Warp warp) => render.ResizeRotate(clp, ResizeFunc, render.Rotate, unrotated.Width, unrotated.Height, angle, crop, warp)?.ROI(roi);

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
                ExtraBorders = Rectangle.FromLTRB(
                    Math.Sign(luma.X % subSample.Width),
                    Math.Sign(luma.Y % subSample.Height),
                    Math.Sign(luma.Right % subSample.Width),
                    Math.Sign(luma.Bottom % subSample.Height));
            }
        }
    }
}
