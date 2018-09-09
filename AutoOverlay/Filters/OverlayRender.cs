using System;
using System.Drawing;
using AvsFilterNet;

namespace AutoOverlay
{
    public abstract class OverlayRender : OverlayFilter
    {
        public abstract Clip Source { get; protected set; }
        public abstract Clip Overlay { get; protected set; }
        public abstract Clip SourceMask { get; protected set; }
        public abstract Clip OverlayMask { get; protected set; }
        public abstract bool LumaOnly { get; protected set; }
        public abstract int Width { get; protected set; }
        public abstract int Height { get; protected set; }
        public abstract int Gradient { get; protected set; }
        public abstract int Noise { get; protected set; }
        public abstract bool DynamicNoise { get; protected set; }
        public abstract OverlayMode Mode { get; protected set; }
        public abstract double Opacity { get; protected set; }
        public abstract string Upsize { get; protected set; }
        public abstract string Downsize { get; protected set; }
        public abstract string Rotate { get; protected set; }
        public abstract ColorAdjustMode ColorAdjust { get; protected set; }
        public abstract string Matrix { get; protected set; }

        private Size srcSize, overSize;

        protected override void Initialize(AVSValue args)
        {
            base.Initialize(args);
            srcSize = new Size(Source.GetVideoInfo().width, Source.GetVideoInfo().height);
            overSize = new Size(Overlay.GetVideoInfo().width, Overlay.GetVideoInfo().height);
            var vi = Source.GetVideoInfo();
            if (Width > 0)
                vi.width = Width;
            if (Height > 0)
                vi.height = Height;
            vi.pixel_type = Source.GetVideoInfo().pixel_type;
            vi.num_frames = Child.GetVideoInfo().num_frames;
            SetVideoInfo(ref vi);
        }

        protected dynamic RenderFrame(OverlayInfo info)
        {
            var src = Source.Dynamic();
            var crop = info.GetCrop();
            var over = Overlay.Dynamic();
            if (info.GetCrop() != RectangleF.Empty || info.Width != overSize.Width || info.Height != overSize.Height)
            {
                over = over.Invoke(
                    info.Width > Overlay.GetVideoInfo().width ? Upsize : Downsize,
                    info.Width, info.Height, crop.Left, crop.Top, -crop.Right, -crop.Bottom);
            }
            var overMask = OverlayMask?.Dynamic().BicubicResize(info.Width, info.Height);

            var mergedWidth = srcSize.Width + Math.Max(-info.X, 0) + Math.Max(info.Width + info.X - srcSize.Width, 0);
            var mergedHeight = srcSize.Height + Math.Max(-info.Y, 0) + Math.Max(info.Height + info.Y - srcSize.Height, 0);
            var mergedAr = mergedWidth / (double)mergedHeight;
            var outAr = GetVideoInfo().width / (double) GetVideoInfo().height;
            var wider = mergedAr > outAr;
            if (Mode == OverlayMode.FitBorders)
                wider = !wider;
            var finalWidth = wider ? GetVideoInfo().width : (int)Math.Round(GetVideoInfo().width * (mergedAr / outAr));
            var finalHeight = wider ? (int)Math.Round(GetVideoInfo().height * (outAr / mergedAr)) : GetVideoInfo().height;
            var finalX = wider ? 0 : (GetVideoInfo().width - finalWidth) / 2;
            var finalY = wider ? (GetVideoInfo().height - finalHeight) / 2 : 0;

            if (ColorAdjust != ColorAdjustMode.None && ColorAdjust != ColorAdjustMode.Average)
            {
                var clip2Adjust = ColorAdjust == ColorAdjustMode.AsOverlay ? src : over;
                var srcTest = src.Crop(Math.Max(0, info.X), Math.Max(0, info.Y),
                    -Math.Max(0, srcSize.Width - info.X - info.Width),
                    -Math.Max(0, srcSize.Height - info.Y - info.Height));
                var overTest = over.Crop(Math.Max(0, -info.X), Math.Max(0, -info.Y),
                    -Math.Max(0, -(srcSize.Width - info.X - info.Width)),
                    -Math.Max(0, -(srcSize.Height - info.Y - info.Height)));
                var maskTest = SourceMask?.Dynamic().Crop(Math.Max(0, info.X), Math.Max(0, info.Y),
                    -Math.Max(0, srcSize.Width - info.X - info.Width),
                    -Math.Max(0, srcSize.Height - info.Y - info.Height)); ;
                if (overMask != null)
                    maskTest = (maskTest ?? GetBlankClip(Source, true).Dynamic())
                        .Overlay(overMask, info.X, info.Y, mode: "darken")
                        .Crop(Math.Max(0, -info.X), Math.Max(0, -info.Y),
                            -Math.Max(0, -(srcSize.Width - info.X - info.Width)),
                            -Math.Max(0, -(srcSize.Height - info.Y - info.Height)));
                if (!GetVideoInfo().IsRGB() && !string.IsNullOrEmpty(Matrix))
                {
                    srcTest = srcTest.ConvertToRgb24(matrix: Matrix);
                    overTest = overTest.ConvertToRgb24(matrix: Matrix);
                    maskTest = maskTest?.ConvertToRgb24(matrix: Matrix);
                    clip2Adjust = clip2Adjust.ConvertToRgb24(matrix: Matrix);
                }
                var sample = ColorAdjust == ColorAdjustMode.AsOverlay ? srcTest : overTest;
                var reference = ColorAdjust == ColorAdjustMode.AsOverlay ? overTest : srcTest;
                var adjusted = clip2Adjust.ColorAdjust(sample, reference, maskTest, maskTest, channels: LumaOnly ? "y" : "yuv");
                if (!GetVideoInfo().IsRGB() && !string.IsNullOrEmpty(Matrix))
                    adjusted = adjusted.ConvertToYV24(matrix: Matrix);
                if (ColorAdjust == ColorAdjustMode.AsOverlay)
                    src = adjusted;
                else over = adjusted;
            }

            if (ColorAdjust == ColorAdjustMode.Average)
            {
                var srcTest = src.Crop(Math.Max(0, info.X), Math.Max(0, info.Y),
                    -Math.Max(0, srcSize.Width - info.X - info.Width),
                    -Math.Max(0, srcSize.Height - info.Y - info.Height));
                var overTest = over.Crop(Math.Max(0, -info.X), Math.Max(0, -info.Y),
                    -Math.Max(0, -(srcSize.Width - info.X - info.Width)),
                    -Math.Max(0, -(srcSize.Height - info.Y - info.Height)));
                src = src.ColorAdjust(srcTest, overTest).Merge(src, weight: 0.5);
                over = over.Merge(over.ColorAdjust(overTest, srcTest), weight: 0.501);
            }

            dynamic GetOverMask(int length, bool gradientMask, bool noiseMask)
            {
                return over.OverlayMask(
                    left: info.X > 0 ? length : 0,
                    top: info.Y > 0 ? length : 0,
                    right: srcSize.Width - info.X - info.Width > 0 ? length : 0,
                    bottom: srcSize.Height - info.Y - info.Height > 0 ? length : 0,
                    gradient: gradientMask, noise: noiseMask, seed: DynamicNoise ? info.FrameNumber : 0);
            }

            dynamic GetSourceMask(int length, bool gradientMask, bool noiseMask)
            {
                return src.OverlayMask(
                    left: info.X < 0 ? length : 0,
                    top: info.Y < 0 ? length : 0,
                    right: srcSize.Width - info.X - info.Width < 0 ? length : 0,
                    bottom: srcSize.Height - info.Y - info.Height < 0 ? length : 0,
                    gradient: gradientMask, noise: noiseMask, seed: DynamicNoise ? info.FrameNumber : 0);
            }

            dynamic GetMask(Func<int,bool,bool,dynamic> func)
            {
                if (Gradient > 0 && Gradient == Noise)
                    return func(Gradient, true, true);
                dynamic mask = null;
                if (Gradient > 0)
                    mask = func(Gradient, true, false);
                if (Noise > 0)
                {
                    var noiseMask = func(Noise, false, true);
                    mask = mask == null ? noiseMask : mask.Overlay(noiseMask, mode: "darken");
                }
                return mask;
            }

            dynamic Rotate(dynamic clip, bool invert) => clip == null
                ? null
                : (info.Angle == 0 ? clip : clip.Invoke(this.Rotate, (invert ? -info.Angle : info.Angle) / 100.0));

            switch (Mode)
            {
                case OverlayMode.Fit:
                case OverlayMode.Difference:
                {
                    var mode = LumaOnly ? "luma" : "blend";
                    if (Mode == OverlayMode.Difference)
                        mode = "difference";
                    var mask = GetMask(GetOverMask);
                    if (overMask != null && mask != null)
                        mask = mask.Overlay(overMask, mode: "darken");
                    if (SourceMask != null && mask != null)
                        mask = mask.Overlay(Rotate(SourceMask.Dynamic().Invert(), true), -info.X, -info.Y, mode: "lighten");
                    if (mask == null && info.Angle != 0)
                        mask = ((Clip)GetBlankClip(over, true)).Dynamic();
                    var hybrid = Opacity < double.Epsilon ? src : src.Overlay(Rotate(over, false), info.X, info.Y, mask: Rotate(mask, false), opacity: Opacity, mode: mode);
                    if (GetVideoInfo().width == srcSize.Width && GetVideoInfo().height == srcSize.Height)
                        return hybrid;
                    return hybrid.Invoke(Downsize, GetVideoInfo().width, GetVideoInfo().height);
                }
                case OverlayMode.Fill:
                {
                    var maskOver = GetMask(GetOverMask);
                    var maskSrc = GetMask(GetSourceMask);
                    if (overMask != null && maskOver != null)
                        maskOver = maskOver.Overlay(overMask, mode: "darken");
                    if (SourceMask != null && maskOver != null)
                        maskOver = maskOver.Overlay(SourceMask.Dynamic().Invert().Invoke(this.Rotate, -info.Angle / 100.0), -info.X, -info.Y, mode: "lighten");
                    dynamic hybrid = src.BlankClip(width: mergedWidth, height: mergedHeight);
                    if (Opacity - 1 <= -double.Epsilon)
                        hybrid = hybrid.Overlay(src, Math.Max(0, -info.X), Math.Max(0, -info.Y));
                    else maskSrc = null;
                    if (maskOver != null || Opacity - 1 < double.Epsilon)
                        hybrid = hybrid.Overlay(over.Invoke(this.Rotate, info.Angle / 100.0), Math.Max(0, info.X), Math.Max(0, info.Y));
                    if (maskOver == null && info.Angle != 0)
                        maskOver = GetBlankClip(over, true).Dynamic();
                    return hybrid.Overlay(src, Math.Max(0, -info.X), Math.Max(0, -info.Y), mask: maskSrc)
                            .Overlay(over.Invoke(this.Rotate, info.Angle / 100.0), Math.Max(0, info.X), Math.Max(0, info.Y),
                            opacity: Opacity, mask: maskOver?.Invoke(this.Rotate, info.Angle / 100.0))
                            .Invoke(Downsize, finalWidth, finalHeight)
                            .AddBorders(finalX, finalY, GetVideoInfo().width - finalX - finalWidth, GetVideoInfo().height - finalY - finalHeight);
                }
                case OverlayMode.FillRectangle:
                case OverlayMode.FitBorders:
                {
                    var maskOver = GetMask(GetOverMask);
                    var maskSrc = GetMask(GetSourceMask);
                    if (overMask != null && maskOver != null)
                        maskOver = maskOver.Overlay(overMask, mode: "darken");
                    if (SourceMask != null && maskOver != null)
                        maskOver = maskOver.Overlay(SourceMask.Dynamic().Invert().Invoke(this.Rotate, -info.Angle / 100.0), -info.X, -info.Y, mode: "lighten");
                    var background = src.BilinearResize(mergedWidth / 3, mergedHeight / 3).Overlay(over.BilinearResize(mergedWidth / 3, mergedHeight / 3),
                        opacity: 0.5, mask: overMask?.BilinearResize(mergedWidth / 3, mergedHeight / 3)); //TODO !!!!!!!!!!
                    for (var i = 0; i < 15; i++)
                        background = background.Blur(1.5);
                    background = background.GaussResize(mergedWidth, mergedHeight, p: 3);
                    //var background = src.BlankClip(width: mergedWidth, height: mergedHeight)
                    //        .Overlay(src, Math.Max(0, -info.X), Math.Max(0, -info.Y))
                    //        .Overlay(over.Invoke(rotateFunc, info.Angle / 100.0), Math.Max(0, info.X), Math.Max(0, info.Y));
                    if (Opacity - 1 <= -double.Epsilon)
                        background = background.Overlay(over.Invoke(this.Rotate, info.Angle / 100.0), Math.Max(0, info.X), Math.Max(0, info.Y), mask: maskOver?.Invoke(this.Rotate, info.Angle / 100.0));
                    var hybrid = background.Overlay(src, Math.Max(0, -info.X), Math.Max(0, -info.Y), mask: maskSrc)
                        .Overlay(over.Invoke(this.Rotate, info.Angle / 100.0), Math.Max(0, info.X), Math.Max(0, info.Y), mask: maskOver?.Invoke(this.Rotate, info.Angle / 100.0), opacity:Opacity)
                        .Invoke(Downsize, finalWidth, finalHeight);
                    if (Mode == OverlayMode.FillRectangle)
                        return hybrid.AddBorders(finalX, finalY, GetVideoInfo().width - finalX - finalWidth, GetVideoInfo().height - finalY - finalHeight);
                    var srcRect = new Rectangle(0, 0, srcSize.Width, srcSize.Height);
                    var overRect = new Rectangle(info.X, info.Y, info.Width, info.Height);
                    var union = Rectangle.Union(srcRect, overRect);
                    var intersect = Rectangle.Intersect(srcRect, overRect);
                    var cropLeft =  intersect.Left - union.Left;
                    var cropTop = intersect.Top - union.Top;
                    var cropRight = union.Right - intersect.Right;
                    var cropBottom = union.Bottom - intersect.Bottom;
                    var cropWidthCoef = cropRight == 0 ? 1 : ((double) cropLeft / cropRight) / ((double) cropLeft / cropRight + 1);
                    var cropHeightCoef = cropBottom == 0 ? 1 : ((double)cropTop / cropBottom) / ((double)cropTop / cropBottom + 1);
                    cropLeft = (int) ((finalWidth - GetVideoInfo().width) * cropWidthCoef);
                    cropTop = (int)((finalHeight - GetVideoInfo().height) * cropHeightCoef);
                    cropRight = finalWidth - GetVideoInfo().width - cropLeft;
                    cropBottom = finalHeight - GetVideoInfo().height - cropTop;
                    return hybrid.Crop(cropLeft, cropTop, -cropRight, -cropBottom);
                }
                case OverlayMode.FillFull:
                {
                    finalWidth = wider ? mergedWidth : (int) Math.Round(mergedHeight * outAr);
                    finalHeight = wider ? (int) Math.Round(mergedWidth / outAr) : mergedHeight;
                    finalX = (finalWidth - mergedWidth) / 2;
                    finalY = (finalHeight - mergedHeight) / 2;
                    var maskOver = GetMask(GetOverMask);
                    var maskSrc = GetMask(GetSourceMask);
                    if (maskSrc != null && maskOver != null)
                        maskOver = maskOver.Overlay(maskSrc.Invert().Invoke(this.Rotate, -info.Angle / 100.0), -info.X, -info.Y, mode: "lighten");
                    if (overMask != null && maskOver != null)
                        maskOver = maskOver.Overlay(overMask, mode: "darken");
                    if (SourceMask != null && maskOver != null)
                        maskOver = maskOver.Overlay(SourceMask.Dynamic().Invert().Invoke(this.Rotate, -info.Angle / 100.0), -info.X, -info.Y, mode: "lighten");
                    var background = over.BilinearResize(finalWidth / 4, finalHeight / 4);
                    for (var i = 0; i < 10; i++)
                        background = background.Blur(1.5);
                    var hybrid = background.GaussResize(finalWidth, finalHeight, p: 3);
                    if (maskOver != null)
                        hybrid = hybrid.Overlay(over.Invoke(this.Rotate, info.Angle / 100.0), finalX + Math.Max(0, info.X), finalY + Math.Max(0, info.Y));
                    if (maskOver == null && info.Angle != 0)
                        maskOver = GetBlankClip(over, true).Dynamic();
                    return hybrid.Overlay(src, finalX + Math.Max(0, -info.X), finalY + Math.Max(0, -info.Y))
                            .Overlay(over.Invoke(this.Rotate, info.Angle / 100.0), finalX + Math.Max(0, info.X), finalY + Math.Max(0, info.Y), mask: maskOver?.Invoke(this.Rotate, info.Angle / 100.0))
                            .Invoke(Downsize, GetVideoInfo().width, GetVideoInfo().height);
                }
                case OverlayMode.Mask:
                {
                    src = GetBlankClip((Clip) src, true).Dynamic();
                    if (SourceMask != null)
                        src = src.Overlay(SourceMask, mode: "darken");
                    over = GetBlankClip((Clip) over, true).Dynamic();
                    return src.BlankClip(width: mergedWidth, height: mergedHeight)
                        .Overlay(src, Math.Max(0, -info.X), Math.Max(0, -info.Y))
                        .Overlay(over.Invoke(this.Rotate, info.Angle / 100.0), Math.Max(0, info.X), Math.Max(0, info.Y))
                        .Invoke(Downsize, finalWidth, finalHeight)
                        .AddBorders(finalX, finalY, GetVideoInfo().width - finalX - finalWidth, GetVideoInfo().height - finalY - finalHeight);
                }
                default:
                    throw new AvisynthException();
            }
        }
    }
}
