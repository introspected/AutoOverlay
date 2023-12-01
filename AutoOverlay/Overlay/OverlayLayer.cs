using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using AvsFilterNet;

namespace AutoOverlay.Overlay
{
    public record OverlayLayer
    {
        public dynamic Clip { get; set; }
        public dynamic Mask { get; }
        public double Opacity { get; }
        public Rectangle Rectangle { get; }
        public Rectangle ExtraBorders { get; }
        public bool Source { get; }
        public List<OverlayInfo> History { get; }
        public int Index { get; private set; }
        public Rectangle Union { get; }
        public OverlayData Data { get; }

        public static List<OverlayLayer> GetLayers(OverlayContext ctx, List<OverlayInfo> history, List<OverlayInfo>[] extra)
        {
            var input = new OverlayInput
            {
                SourceSize = ctx.SourceInfo.Size,
                OverlaySize = ctx.OverlayInfo.Size,
                TargetSize = ctx.TargetInfo.Size,
                InnerBounds = ctx.Render.InnerBounds,
                OuterBounds = ctx.Render.OuterBounds,
                OverlayBalance = ctx.Render.OverlayBalance,
                FixedSource = ctx.Render.FixedSource,
                ExtraClips = ctx.ExtraClips
                    .Select((p, i) => Tuple.Create(p.GetVideoInfo().GetSize(), extra[i].First()))
                    .ToList()
            };
            var data = history.First().GetOverlayData(input);
            OverlayData lumaData = null;
            if (ctx.Plane.IsChroma())
            {
                var subsample = new Size(ctx.Render.ColorSpace.GetWidthSubsample(), ctx.Render.ColorSpace.GetHeightSubsample());
                lumaData = history.First().GetOverlayData(input.Scale(subsample));
            }
            var layers = new List<OverlayLayer>(2 + ctx.ExtraClips.Count)
            {
                new(data, true, ctx.Source, ctx.SourceMask, 1, ctx.Render, lumaData, history)
            };
            var extraLayers = ctx.ExtraClips
                .Select((clip, i) => new OverlayLayer(data.ExtraClips[i], false, clip, ctx.ExtraMasks[i], ctx.ExtraOpacity[i], ctx.Render, lumaData?.ExtraClips[i], extra[i]));
            layers.AddRange(extraLayers);
            layers.Insert(ctx.Render.OverlayOrder + 1, new(data, false, ctx.Overlay, ctx.OverlayMask, ctx.Render.Opacity, ctx.Render, lumaData, history));
            for (var i = 0; i < layers.Count; i++)
                layers[i].Index = i;
            return layers;
        }

        private OverlayLayer(OverlayData data, bool source, Clip clip, Clip mask, double opacity,
            OverlayRender render, OverlayData lumaData, List<OverlayInfo> history)
        {
            Data = data;
            Source = source;
            Opacity = opacity;
            History = history;
            Union = data.Union;
            Rectangle opposite;
            RectangleF crop;
            float angle;
            Warp warp;
            if (Source)
            {
                Rectangle = data.Source;
                opposite = data.Overlay;
                crop = data.SourceCrop;
                angle = 0;
                warp = data.SourceWarp;
            }
            else
            {
                Rectangle = data.Overlay;
                opposite = data.Source;
                crop = data.OverlayCrop;
                angle = data.OverlayAngle;
                warp = data.OverlayWarp;
            }
            var vi = clip.GetVideoInfo();
            var bitDepth = vi.pixel_type.GetBitDepth();
            var size = vi.GetSize();

            var resizeFunc = Rectangle.Width > size.Width ? render.Upsize : render.Downsize;
            Clip = render.ResizeRotate(clip, resizeFunc, render.Rotate, Rectangle.Width, Rectangle.Height, angle, crop, warp);
            Mask = render.ResizeRotate(mask, resizeFunc, render.Rotate, Rectangle.Width, Rectangle.Height, angle, crop, warp);

            if (render.ColorAdjust < double.Epsilon && bitDepth != render.BitDepth)
            {
                Clip = Clip.ConvertBits(render.BitDepth);
            }

            if (lumaData != null)
            {
                var luma = Source ? lumaData.Source : lumaData.Overlay;
                ExtraBorders = Rectangle.FromLTRB(luma.X % 2, luma.Y % 2, luma.Right % 2, luma.Bottom % 2);
            }
            else
            {
                ExtraBorders = Rectangle.Empty;
            }
        }

        private dynamic Crop(dynamic clip, Rectangle src, Rectangle over) => clip?.Crop(
            Math.Max(0, over.Left - src.Left),
            Math.Max(0, over.Top - src.Top),
            -Math.Max(0, src.Right - over.Right),
            -Math.Max(0, src.Bottom - over.Bottom)
        );
    }
}
