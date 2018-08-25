using System;
using System.Drawing;
using AvsFilterNet;

namespace AutoOverlay
{
    public abstract class OverlayRender : OverlayFilter
    {
        protected Clip srcClip;
        protected Clip overClip;
        protected Clip srcMaskClip;
        protected Clip overMaskClip;
        protected Size srcSize, overSize;
        protected bool lumaOnly = false;
        protected int gradient = 0;
        protected int noise = 0;
        protected bool dynamicNoise = true;
        protected OverlayMode overlayMode = OverlayMode.Fit;
        protected double opacity = 100;
        protected string upsizeFunc = "BilinearResize";
        protected string downsizeFunc = "BilinearResize";
        protected string rotateFunc = "BilinearRotate";
        protected ColorAdjustMode colorAdjust = ColorAdjustMode.None;
        protected string matrix = null;

        protected dynamic RenderFrame(OverlayInfo info)
        {
            var src = srcClip.Dynamic();
            var crop = info.GetCrop();
            var over = overClip.Dynamic();
            if (info.GetCrop() != RectangleF.Empty || info.Width != overSize.Width || info.Height != overSize.Height)
            {
                over = over.Invoke(
                    info.Width > overClip.GetVideoInfo().width ? upsizeFunc : downsizeFunc,
                    info.Width, info.Height, crop.Left, crop.Top, -crop.Right, -crop.Bottom);
            }
            var overMask = overMaskClip?.Dynamic().BicubicResize(info.Width, info.Height);

            var mergedWidth = srcSize.Width + Math.Max(-info.X, 0) + Math.Max(info.Width + info.X - srcSize.Width, 0);
            var mergedHeight = srcSize.Height + Math.Max(-info.Y, 0) + Math.Max(info.Height + info.Y - srcSize.Height, 0);
            var mergedAr = mergedWidth / (double)mergedHeight;
            var outAr = GetVideoInfo().width / (double) GetVideoInfo().height;
            var wider = mergedAr > outAr;
            if (overlayMode == OverlayMode.FitBorders)
                wider = !wider;
            var finalWidth = wider ? GetVideoInfo().width : (int)Math.Round(GetVideoInfo().width * (mergedAr / outAr));
            var finalHeight = wider ? (int)Math.Round(GetVideoInfo().height * (outAr / mergedAr)) : GetVideoInfo().height;
            var finalX = wider ? 0 : (GetVideoInfo().width - finalWidth) / 2;
            var finalY = wider ? (GetVideoInfo().height - finalHeight) / 2 : 0;

            if (colorAdjust != ColorAdjustMode.None && colorAdjust != ColorAdjustMode.Average)
            {
                var clip2Adjust = colorAdjust == ColorAdjustMode.AsOverlay ? src : over;
                var srcTest = src.Crop(Math.Max(0, info.X), Math.Max(0, info.Y),
                    -Math.Max(0, srcSize.Width - info.X - info.Width),
                    -Math.Max(0, srcSize.Height - info.Y - info.Height));
                var overTest = over.Crop(Math.Max(0, -info.X), Math.Max(0, -info.Y),
                    -Math.Max(0, -(srcSize.Width - info.X - info.Width)),
                    -Math.Max(0, -(srcSize.Height - info.Y - info.Height)));
                var maskTest = srcMaskClip?.Dynamic().Crop(Math.Max(0, info.X), Math.Max(0, info.Y),
                    -Math.Max(0, srcSize.Width - info.X - info.Width),
                    -Math.Max(0, srcSize.Height - info.Y - info.Height)); ;
                if (overMask != null)
                    maskTest = (maskTest ?? GetBlankClip(srcClip, true).Dynamic())
                        .Overlay(overMask, info.X, info.Y, mode: "darken")
                        .Crop(Math.Max(0, -info.X), Math.Max(0, -info.Y),
                            -Math.Max(0, -(srcSize.Width - info.X - info.Width)),
                            -Math.Max(0, -(srcSize.Height - info.Y - info.Height)));
                if (!GetVideoInfo().IsRGB() && !string.IsNullOrEmpty(matrix))
                {
                    srcTest = srcTest.ConvertToRgb24(matrix: matrix);
                    overTest = overTest.ConvertToRgb24(matrix: matrix);
                    maskTest = maskTest?.ConvertToRgb24(matrix: matrix);
                    clip2Adjust = clip2Adjust.ConvertToRgb24(matrix: matrix);
                }
                var sample = colorAdjust == ColorAdjustMode.AsOverlay ? srcTest : overTest;
                var reference = colorAdjust == ColorAdjustMode.AsOverlay ? overTest : srcTest;
                var adjusted = clip2Adjust.ColorAdjust(sample, reference, maskTest, maskTest, channels: lumaOnly ? "y" : "yuv");
                if (!GetVideoInfo().IsRGB() && !string.IsNullOrEmpty(matrix))
                    adjusted = adjusted.ConvertToYV24(matrix: matrix);
                if (colorAdjust == ColorAdjustMode.AsOverlay)
                    src = adjusted;
                else over = adjusted;
            }

            if (colorAdjust == ColorAdjustMode.Average)
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
                    gradient: gradientMask, noise: noiseMask, seed: dynamicNoise ? info.FrameNumber : 0);
            }

            dynamic GetSourceMask(int length, bool gradientMask, bool noiseMask)
            {
                return src.OverlayMask(
                    left: info.X < 0 ? length : 0,
                    top: info.Y < 0 ? length : 0,
                    right: srcSize.Width - info.X - info.Width < 0 ? length : 0,
                    bottom: srcSize.Height - info.Y - info.Height < 0 ? length : 0,
                    gradient: gradientMask, noise: noiseMask, seed: dynamicNoise ? info.FrameNumber : 0);
            }

            dynamic GetMask(Func<int,bool,bool,dynamic> func)
            {
                if (gradient > 0 && gradient == noise)
                    return func(gradient, true, true);
                dynamic mask = null;
                if (gradient > 0)
                    mask = func(gradient, true, false);
                if (noise > 0)
                {
                    var noiseMask = func(noise, false, true);
                    mask = mask == null ? noiseMask : mask.Overlay(noiseMask, mode: "darken");
                }
                return mask;
            }

            dynamic Rotate(dynamic clip, bool invert) => clip == null
                ? null
                : (info.Angle == 0 ? clip : clip.Invoke(rotateFunc, (invert ? -info.Angle : info.Angle) / 100.0));

            switch (overlayMode)
            {
                case OverlayMode.Fit:
                case OverlayMode.Difference:
                {
                    var mode = lumaOnly ? "luma" : "blend";
                    if (overlayMode == OverlayMode.Difference)
                        mode = "difference";
                    var mask = GetMask(GetOverMask);
                    if (overMask != null && mask != null)
                        mask = mask.Overlay(overMask, mode: "darken");
                    if (srcMaskClip != null && mask != null)
                        mask = mask.Overlay(Rotate(srcMaskClip.Dynamic().Invert(), true), -info.X, -info.Y, mode: "lighten");
                    if (mask == null && info.Angle != 0)
                        mask = ((Clip)GetBlankClip(over, true)).Dynamic();
                    var hybrid = opacity < double.Epsilon ? src : src.Overlay(Rotate(over, false), info.X, info.Y, mask: Rotate(mask, false), opacity: opacity, mode: mode);
                    if (GetVideoInfo().width == srcSize.Width && GetVideoInfo().height == srcSize.Height)
                        return hybrid;
                    return hybrid.Invoke(downsizeFunc, GetVideoInfo().width, GetVideoInfo().height);
                }
                case OverlayMode.Fill:
                {
                    var maskOver = GetMask(GetOverMask);
                    var maskSrc = GetMask(GetSourceMask);
                    if (overMask != null && maskOver != null)
                        maskOver = maskOver.Overlay(overMask, mode: "darken");
                    if (srcMaskClip != null && maskOver != null)
                        maskOver = maskOver.Overlay(srcMaskClip.Dynamic().Invert().Invoke(rotateFunc, -info.Angle / 100.0), -info.X, -info.Y, mode: "lighten");
                    dynamic hybrid = src.BlankClip(width: mergedWidth, height: mergedHeight);
                    if (opacity - 1 <= -double.Epsilon)
                        hybrid = hybrid.Overlay(src, Math.Max(0, -info.X), Math.Max(0, -info.Y));
                    else maskSrc = null;
                    if (maskOver != null || opacity - 1 < double.Epsilon)
                        hybrid = hybrid.Overlay(over.Invoke(rotateFunc, info.Angle / 100.0), Math.Max(0, info.X), Math.Max(0, info.Y));
                    if (maskOver == null && info.Angle != 0)
                        maskOver = GetBlankClip(over, true).Dynamic();
                    return hybrid.Overlay(src, Math.Max(0, -info.X), Math.Max(0, -info.Y), mask: maskSrc)
                            .Overlay(over.Invoke(rotateFunc, info.Angle / 100.0), Math.Max(0, info.X), Math.Max(0, info.Y),
                            opacity: opacity, mask: maskOver?.Invoke(rotateFunc, info.Angle / 100.0))
                            .Invoke(downsizeFunc, finalWidth, finalHeight)
                            .AddBorders(finalX, finalY, GetVideoInfo().width - finalX - finalWidth, GetVideoInfo().height - finalY - finalHeight);
                }
                case OverlayMode.FillRectangle:
                case OverlayMode.FitBorders:
                {
                    var maskOver = GetMask(GetOverMask);
                    var maskSrc = GetMask(GetSourceMask);
                    if (overMask != null && maskOver != null)
                        maskOver = maskOver.Overlay(overMask, mode: "darken");
                    if (srcMaskClip != null && maskOver != null)
                        maskOver = maskOver.Overlay(srcMaskClip.Dynamic().Invert().Invoke(rotateFunc, -info.Angle / 100.0), -info.X, -info.Y, mode: "lighten");
                    var background = src.BilinearResize(mergedWidth / 3, mergedHeight / 3).Overlay(over.BilinearResize(mergedWidth / 3, mergedHeight / 3),
                        opacity: 0.5, mask: overMask?.BilinearResize(mergedWidth / 3, mergedHeight / 3)); //TODO !!!!!!!!!!
                    for (var i = 0; i < 15; i++)
                        background = background.Blur(1.5);
                    background = background.GaussResize(mergedWidth, mergedHeight, p: 3);
                    //var background = src.BlankClip(width: mergedWidth, height: mergedHeight)
                    //        .Overlay(src, Math.Max(0, -info.X), Math.Max(0, -info.Y))
                    //        .Overlay(over.Invoke(rotateFunc, info.Angle / 100.0), Math.Max(0, info.X), Math.Max(0, info.Y));
                    if (opacity - 1 <= -double.Epsilon)
                        background = background.Overlay(over.Invoke(rotateFunc, info.Angle / 100.0), Math.Max(0, info.X), Math.Max(0, info.Y), mask: maskOver?.Invoke(rotateFunc, info.Angle / 100.0));
                    var hybrid = background.Overlay(src, Math.Max(0, -info.X), Math.Max(0, -info.Y), mask: maskSrc)
                        .Overlay(over.Invoke(rotateFunc, info.Angle / 100.0), Math.Max(0, info.X), Math.Max(0, info.Y), mask: maskOver?.Invoke(rotateFunc, info.Angle / 100.0), opacity:opacity)
                        .Invoke(downsizeFunc, finalWidth, finalHeight);
                    if (overlayMode == OverlayMode.FillRectangle)
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
                        maskOver = maskOver.Overlay(maskSrc.Invert().Invoke(rotateFunc, -info.Angle / 100.0), -info.X, -info.Y, mode: "lighten");
                    if (overMask != null && maskOver != null)
                        maskOver = maskOver.Overlay(overMask, mode: "darken");
                    if (srcMaskClip != null && maskOver != null)
                        maskOver = maskOver.Overlay(srcMaskClip.Dynamic().Invert().Invoke(rotateFunc, -info.Angle / 100.0), -info.X, -info.Y, mode: "lighten");
                    var background = over.BilinearResize(finalWidth / 4, finalHeight / 4);
                    for (var i = 0; i < 10; i++)
                        background = background.Blur(1.5);
                    var hybrid = background.GaussResize(finalWidth, finalHeight, p: 3);
                    if (maskOver != null)
                        hybrid = hybrid.Overlay(over.Invoke(rotateFunc, info.Angle / 100.0), finalX + Math.Max(0, info.X), finalY + Math.Max(0, info.Y));
                    if (maskOver == null && info.Angle != 0)
                        maskOver = GetBlankClip(over, true).Dynamic();
                    return hybrid.Overlay(src, finalX + Math.Max(0, -info.X), finalY + Math.Max(0, -info.Y))
                            .Overlay(over.Invoke(rotateFunc, info.Angle / 100.0), finalX + Math.Max(0, info.X), finalY + Math.Max(0, info.Y), mask: maskOver?.Invoke(rotateFunc, info.Angle / 100.0))
                            .Invoke(downsizeFunc, GetVideoInfo().width, GetVideoInfo().height);
                }
                case OverlayMode.Mask:
                {
                    src = GetBlankClip((Clip) src, true).Dynamic();
                    if (srcMaskClip != null)
                        src = src.Overlay(srcMaskClip, mode: "darken");
                    over = GetBlankClip((Clip) over, true).Dynamic();
                    return src.BlankClip(width: mergedWidth, height: mergedHeight)
                        .Overlay(src, Math.Max(0, -info.X), Math.Max(0, -info.Y))
                        .Overlay(over.Invoke(rotateFunc, info.Angle / 100.0), Math.Max(0, info.X), Math.Max(0, info.Y))
                        .Invoke(downsizeFunc, finalWidth, finalHeight)
                        .AddBorders(finalX, finalY, GetVideoInfo().width - finalX - finalWidth, GetVideoInfo().height - finalY - finalHeight);
                }
                default:
                    throw new AvisynthException();
            }
        }

        protected sealed override void Dispose(bool A_0)
        {
            srcClip.Dispose();
            overClip.Dispose();
            srcMaskClip?.Dispose();
            overMaskClip?.Dispose();
            base.Dispose(A_0);
        }
    }
}
