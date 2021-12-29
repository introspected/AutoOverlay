using System;
using System.Drawing;
using AutoOverlay.AviSynth;
using AutoOverlay.Overlay;

namespace AutoOverlay.Filters
{
    public class FrameContext
    {
        private readonly OverlayContext ctx;
        public OverlayInfo Info { get; }
        public dynamic Source { get; set; }
        public dynamic Overlay { get; set; }
        public dynamic OverlayMask { get; set; }
        public dynamic SourceTest { get; set; }
        public dynamic OverlayTest { get; set; }
        public dynamic MaskTest { get; set; }
        public Size SourceSize { get; set; }
        public Size OverlaySize { get; set; }

        public FrameContext(OverlayContext ctx, OverlayInfo info)
        {
            this.ctx = ctx;
            Info = info;
            Source = ctx.Source;
            Overlay = ctx.Overlay;
            SourceSize = ctx.SourceInfo.Size;
            OverlaySize = ctx.OverlayInfo.Size;
            Info = Info.Resize(SourceSize, OverlaySize);
            if (ctx.Render.Mode == FramingMode.Fit && SourceSize != ctx.TargetInfo.Size)
            {
                Info = Info.Resize(ctx.TargetInfo.Size, OverlaySize);
                Source = Source.Invoke(SourceSize.GetArea() < ctx.TargetInfo.Size.GetArea() ? ctx.Render.Upsize : ctx.Render.Downsize,
                    ctx.TargetInfo.Width, ctx.TargetInfo.Height);
                SourceSize = ctx.TargetInfo.Size;
            }
            if (ctx.Render.Mode == FramingMode.Fit)
            {
                Info = Info.Shrink(SourceSize, OverlaySize);
            }
            var resizeFunc = Info.Width > OverlaySize.Width ? ctx.Render.Upsize : ctx.Render.Downsize;
            var crop = Info.GetCrop();
            Overlay = ctx.Render.ResizeRotate(Overlay, resizeFunc, null, Info.Width, Info.Height, 0, crop, Info.Warp);
            OverlayMask = ctx.Render.ResizeRotate(ctx.OverlayMask, "BicubicResize", null, Info.Width, Info.Height, 0, crop, Info.Warp);
            InitColorAdjust();
        }

        private void InitColorAdjust()
        {
            if (ctx.Render.BitDepth > 0)
            {
                Source = Source.ConvertBits(ctx.Render.BitDepth);
                Overlay = Overlay.ConvertBits(ctx.Render.BitDepth);
            }
            SourceTest = AdjCrop(Source, false);
            OverlayTest = AdjCrop(Overlay, true);
            var srcMaskTest = AdjCrop(ctx.SourceMask, false);
            var overMaskTest = AdjCrop(OverlayMask, true);
            MaskTest = (srcMaskTest, overMaskTest) switch
            {
                ({}, {}) => srcMaskTest.Overlay(overMaskTest, mode: "darken"),
                _ => srcMaskTest ?? overMaskTest
            };
        }

        private dynamic AdjCrop(dynamic clp, bool invert)
        {
            var sign = invert ? -1 : 1;
            return clp?.Crop(
                Math.Max(ctx.Render.SrcColorBorderOffset.Left, sign * (Info.X + ctx.Render.OverColorBorderOffset.Left)),
                Math.Max(ctx.Render.SrcColorBorderOffset.Top, sign * (Info.Y + ctx.Render.OverColorBorderOffset.Top)),
                -Math.Max(ctx.Render.SrcColorBorderOffset.Right, sign * (SourceSize.Width - Info.X - Info.Width + ctx.Render.OverColorBorderOffset.Right)),
                -Math.Max(ctx.Render.SrcColorBorderOffset.Bottom, sign * (SourceSize.Height - Info.Y - Info.Height + ctx.Render.OverColorBorderOffset.Bottom)));
        }
    }
}
