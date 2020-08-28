using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using AutoOverlay.Overlay;
using AvsFilterNet;

namespace AutoOverlay
{
    public abstract class OverlayRender : OverlayFilter
    {
        #region Properties
        public abstract Clip Source { get; protected set; }
        public abstract Clip Overlay { get; protected set; }
        public abstract Clip SourceMask { get; protected set; }
        public abstract Clip OverlayMask { get; protected set; }
        public abstract string OverlayMode { get; protected set; }
        public abstract string AdjustChannels { get; protected set; }
        public abstract int Width { get; protected set; }
        public abstract int Height { get; protected set; }
        public abstract string PixelType { get; protected set; }
        public abstract int Gradient { get; protected set; }
        public abstract int Noise { get; protected set; }
        public abstract bool DynamicNoise { get; protected set; }
        public abstract Rectangle BorderOffset { get; protected set; }
        public abstract Rectangle SrcColorBorderOffset { get; protected set; }
        public abstract Rectangle OverColorBorderOffset { get; protected set; }
        public abstract FramingMode Mode { get; protected set; }
        public abstract double Opacity { get; protected set; }
        public abstract string Upsize { get; protected set; }
        public abstract string Downsize { get; protected set; }
        public abstract string Rotate { get; protected set; }
        public abstract double ColorAdjust { get; protected set; }
        public abstract string Matrix { get; protected set; }
        public abstract bool Invert { get; protected set; }
        public abstract bool Extrapolation { get; protected set; }
        public abstract int BlankColor { get; protected set; }
        public abstract double Background { get; protected set; }
        public abstract int BackBlur { get; protected set; }
        public abstract bool SIMD { get; protected set; }
        #endregion

        private YUVPlanes[] planes;

        private Size targetSubsample;

        private readonly List<OverlayContext> contexts = new List<OverlayContext>();

        protected abstract OverlayInfo GetOverlayInfo(int n);

        protected override void Initialize(AVSValue args)
        {
            base.Initialize(args);
            if (Invert)
            {
                if (ColorAdjust >= 0)
                    ColorAdjust = 1 - ColorAdjust;
                Initialize(Overlay, Source);
            }
            else
            {
                Initialize(Source, Overlay);
            }
        }

        private void Initialize(Clip src, Clip over)
        {
            var srcInfo = src.GetVideoInfo();
            var overInfo = over.GetVideoInfo();

            var vi = src.GetVideoInfo();
            if (Width > 0)
                vi.width = Width;
            if (Height > 0)
                vi.height = Height;
            var srcBitDepth = srcInfo.pixel_type.GetBitDepth();
            var overBitDepth = overInfo.pixel_type.GetBitDepth();
            if (srcBitDepth != overBitDepth && ColorAdjust > 0 && ColorAdjust < 1)
                throw new AvisynthException("ColorAdjust -1, 0, 1 only allowed when video bit depth is different");
            var targetBitDepth = ColorAdjust >= 1 - double.Epsilon ? overBitDepth : srcBitDepth;
            vi.pixel_type = vi.pixel_type.ChangeBitDepth(targetBitDepth);
            vi.num_frames = Child.GetVideoInfo().num_frames;
            planes = OverlayUtils.GetPlanes(vi.pixel_type);
            targetSubsample = new Size(srcInfo.pixel_type.GetWidthSubsample(), srcInfo.pixel_type.GetHeightSubsample());
            SetVideoInfo(ref vi);
        }

        protected override void AfterInitialize()
        {
            var withoutSubSample = GetVideoInfo().pixel_type.WithoutSubSample() &&
                             Source.GetVideoInfo().pixel_type.WithoutSubSample() &&
                             Overlay.GetVideoInfo().pixel_type.WithoutSubSample();
            if (withoutSubSample)
            {
                contexts.Add(new OverlayContext(this, default));
            }
            else
            {
                contexts.AddRange(planes.Select(plane => new OverlayContext(this, plane)));
            }
        }

        protected override VideoFrame GetFrame(int n)
        {
            var info = GetOverlayInfo(n);//.Resize(Source.GetSize(), Overlay.GetSize());
            if (Invert)
                info = info.Invert();

            var res = NewVideoFrame(StaticEnv);

            var outClips = contexts.Select(ctx => RenderFrame(ctx, info));

            //outClips = contexts.Select(ctx => ctx.Source);

            var hybrid = contexts.Count == 1 ? outClips.First() : DynamicEnv.CombinePlanes(outClips, 
                planes: contexts.Select(p => p.Plane.GetLetter()).Aggregate(string.Concat),
                pixel_type: $"YUV4{4 / targetSubsample.Width}{(4 / targetSubsample.Width - 4) + 4 / targetSubsample.Height}P{GetVideoInfo().pixel_type.GetBitDepth()}");

            if (BlankColor >= 0 && Mode == FramingMode.Fill)
            {
                var ctx = contexts.First();
                var frameParams = ctx.CalcFrame(info);

                var mask = DynamicEnv.StaticOverlayRender(ctx.Source.ConvertToY8(), ctx.Overlay.ConvertToY8(),
                    info.X, info.Y, info.Angle, info.Width, info.Height, info.GetCrop(), info.Diff, overlayMode: "blend",
                    width: frameParams.FinalWidth, height: frameParams.FinalHeight, mode: (int) FramingMode.Mask);
                mask = InitClip(mask, ctx.TargetInfo.Width, ctx.TargetInfo.Height, 0xFF8080).Overlay(mask, frameParams.FinalX, frameParams.FinalY);
                var background = InitClip(hybrid, ctx.TargetInfo.Width, ctx.TargetInfo.Height, ctx.BlankColor);//, ctx.TargetInfo.Info.IsRGB() ? "RGB24" : "YV24");
                hybrid = hybrid.Overlay(background, mask: mask.Invert());
            }

            if (Debug)
                hybrid = hybrid.Subtitle(info.DisplayInfo().Replace("\n", "\\n"), lsp: 0);
            using VideoFrame frame = hybrid[info.FrameNumber];
            Parallel.ForEach(planes, plane =>
            {
                for (var y = 0; y < frame.GetHeight(plane); y++)
                    OverlayUtils.CopyMemory(res.GetWritePtr(plane) + y * res.GetPitch(plane),
                        frame.GetReadPtr(plane) + y * frame.GetPitch(plane), res.GetRowSize(plane));
            });
            return res;
        }

        protected dynamic RenderFrame(OverlayContext ctx, OverlayInfo info)
        {
            info = info.Resize(ctx.SourceInfo.Size, ctx.OverlayInfo.Size);

            var src = ctx.Source;
            var over = ctx.Overlay;
            var srcSize = ctx.SourceInfo.Size;
            var overSize = ctx.OverlayInfo.Size;

            if (Mode == FramingMode.Fit && srcSize != ctx.TargetInfo.Size)
            {
                info = info.Resize(ctx.TargetInfo.Size, overSize);
                src = src.Invoke(srcSize.GetArea() < ctx.TargetInfo.Size.GetArea() ? Upsize : Downsize, ctx.TargetInfo.Width, ctx.TargetInfo.Height);
                srcSize = ctx.TargetInfo.Size;
            }

            if (Mode == FramingMode.Fit)
            {
               info = info.Shrink(srcSize, overSize);
            }

            var resizeFunc = info.Width > overSize.Width ? Upsize : Downsize;

            var crop = info.GetCrop();

            if (!crop.IsEmpty || info.Width != overSize.Width || info.Height != overSize.Height)
            {
                over = ResizeRotate(over, resizeFunc, null, info.Width, info.Height, 0, crop);
            }
            var overMask = ctx.OverlayMask?.Dynamic().BicubicResize(info.Width, info.Height);


            if (ColorAdjust > -double.Epsilon && Mode != FramingMode.Mask)
            {
                Func<dynamic, bool, dynamic> adjCrop = (clp, invert) =>
                {
                    var sign = invert ? -1 : 1;
                    return clp?.Crop(
                        Math.Max(SrcColorBorderOffset.Left, sign*(info.X + OverColorBorderOffset.Left)),
                        Math.Max(SrcColorBorderOffset.Top, sign*(info.Y + OverColorBorderOffset.Top)),
                        -Math.Max(SrcColorBorderOffset.Right, sign * (srcSize.Width - info.X - info.Width + OverColorBorderOffset.Right)),
                        -Math.Max(SrcColorBorderOffset.Bottom, sign * (srcSize.Height - info.Y - info.Height + OverColorBorderOffset.Bottom)));
                };

                var srcTest = adjCrop(src, false);
                var overTest = adjCrop(over, true);
                var maskTest = adjCrop(ctx.SourceMask, false);
                if (overMask != null)
                {
                    var input = (maskTest ?? GetBlankClip(src, true).Dynamic())
                        .Overlay(overMask, info.X, info.Y, mode: "darken");
                    maskTest = adjCrop(input, true);
                }
                if (!GetVideoInfo().IsRGB() && !string.IsNullOrEmpty(Matrix))
                {
                    srcTest = srcTest.ConvertToRgb24(matrix: Matrix);
                    overTest = overTest.ConvertToRgb24(matrix: Matrix);
                    maskTest = maskTest?.ConvertToRgb24(matrix: Matrix);
                    if (ColorAdjust > double.Epsilon)
                        src = src.ConvertToRgb24(matrix: Matrix);
                    if (ColorAdjust < 1 - double.Epsilon)
                        over = over.ConvertToRgb24(matrix: Matrix);
                }
                if (ColorAdjust > double.Epsilon)
                {
                    src = src.ColorAdjust(srcTest, overTest, maskTest, maskTest, intensity: ColorAdjust, channels: AdjustChannels, debug: Debug, SIMD: SIMD);
                    if (!GetVideoInfo().IsRGB() && !string.IsNullOrEmpty(Matrix))
                        src = src.ConvertToYV24(matrix: Matrix);
                }
                if (ColorAdjust < 1 - double.Epsilon)
                {
                    over = over.ColorAdjust(overTest, srcTest, maskTest, maskTest, intensity: 1 - ColorAdjust, channels: AdjustChannels, debug: Debug, exclude: 0, SIMD: SIMD);
                    if (!GetVideoInfo().IsRGB() && !string.IsNullOrEmpty(Matrix))
                        over = over.ConvertToYV24(matrix: Matrix);
                }
            }

            dynamic GetOverMask(int length, bool gradientMask, bool noiseMask)
            {
                return over.OverlayMask(
                    left: info.X > BorderOffset.Left ? length : 0,
                    top: info.Y > BorderOffset.Top ? length : 0,
                    right: srcSize.Width - info.X - info.Width > BorderOffset.Right ? length : 0,
                    bottom: srcSize.Height - info.Y - info.Height > BorderOffset.Bottom ? length : 0,
                    gradient: gradientMask, noise: noiseMask,
                    seed: DynamicNoise ? info.FrameNumber : 0);
            }

            dynamic GetSourceMask(int length, bool gradientMask, bool noiseMask)
            {
                return src.OverlayMask( //TODO BorderOffset
                    left: info.X < 0 ? length : 0,
                    top: info.Y < 0 ? length : 0,
                    right: srcSize.Width - info.X - info.Width < 0 ? length : 0,
                    bottom: srcSize.Height - info.Y - info.Height < 0 ? length : 0,
                    gradient: gradientMask, noise: noiseMask,
                    seed: DynamicNoise ? info.FrameNumber : 0);
            }

            dynamic GetMask(Func<int, bool, bool, dynamic> func)
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

            dynamic GetBackground(int width, int height)
            {
                var background = src.BilinearResize(width / 3, height / 3)
                    .Overlay(over.BilinearResize(width / 3, height / 3),
                        opacity: (Background + 1) / 2,
                        mask: overMask?.BilinearResize(width / 3, height / 3));
                for (var i = 0; i < BackBlur; i++)
                    background = background.Blur(1.5);
                return background.GaussResize(width, height, p: 3);
            }

            var frameParams = ctx.CalcFrame(info);

            switch (Mode)
            {
                case FramingMode.Fit:
                    {
                        var mask = GetMask(GetOverMask);
                        if (overMask != null && mask != null)
                            mask = mask.Overlay(overMask, mode: "darken");
                        if (ctx.SourceMask != null && mask != null)
                            mask = mask.Overlay(Rotate(ctx.SourceMask.Invert(), true), -info.X, -info.Y, mode: "lighten");
                        if (mask == null && info.Angle != 0)
                            mask = ((Clip) GetBlankClip(over, true)).Dynamic();
                        return Opacity < double.Epsilon ? src : src.Overlay(Rotate(over, false), info.X, info.Y, mask: Rotate(mask, false), opacity: Opacity, mode: OverlayMode);
                    }
                case FramingMode.Fill:
                    {
                        var maskOver = GetMask(GetOverMask);
                        var maskSrc = GetMask(GetSourceMask);
                        if (overMask != null && maskOver != null)
                            maskOver = maskOver.Overlay(overMask, mode: "darken");
                        if (ctx.SourceMask != null && maskOver != null)
                            maskOver = maskOver.Overlay(ctx.SourceMask.Invert().Invoke(this.Rotate, -info.Angle / 100.0), -info.X, -info.Y, mode: "lighten");
                        dynamic hybrid = InitClip(src, frameParams.MergedWidth, frameParams.MergedHeight, ctx.BlankColor);
                        if (maskSrc != null || Opacity - 1 <= -double.Epsilon)
                            hybrid = hybrid.Overlay(src, Math.Max(0, -info.X), Math.Max(0, -info.Y));
                        if (maskOver != null || Opacity - 1 < double.Epsilon)
                            hybrid = hybrid.Overlay(over.Invoke(this.Rotate, info.Angle / 100.0), Math.Max(0, info.X), Math.Max(0, info.Y));
                        if (maskOver == null && info.Angle != 0)
                            maskOver = GetBlankClip(over, true).Dynamic()?.Invoke(this.Rotate, info.Angle / 100.0);

                        var merged = hybrid.Overlay(src, Math.Max(0, -info.X), Math.Max(0, -info.Y), mask: maskSrc)
                            .Overlay(over.Invoke(this.Rotate, info.Angle / 100.0), 
                                Math.Max(0, info.X), Math.Max(0, info.Y),
                                opacity: Opacity, mask: maskOver, mode: OverlayMode);

                        var resized = merged.Invoke(Downsize, frameParams.FinalWidth, frameParams.FinalHeight);

                        return InitClip(src, ctx.TargetInfo.Width, ctx.TargetInfo.Height, ctx.DefaultColor)
                            .Overlay(resized, frameParams.FinalX, frameParams.FinalY);
                    }
                case FramingMode.FillRectangle:
                case FramingMode.FitBorders:
                    {
                        var maskOver = GetMask(GetOverMask);
                        var maskSrc = GetMask(GetSourceMask);
                        if (overMask != null && maskOver != null)
                            maskOver = maskOver.Overlay(overMask, mode: "darken");
                        if (ctx.SourceMask != null && maskOver != null)
                            maskOver = maskOver.Overlay(ctx.SourceMask.Dynamic().Invert().Invoke(this.Rotate, -info.Angle / 100.0), -info.X, -info.Y, mode: "lighten");
                        var background = GetBackground(frameParams.MergedWidth, frameParams.MergedHeight);
                        if (Opacity - 1 <= -double.Epsilon)
                            background = background.Overlay(over.Invoke(this.Rotate, info.Angle / 100.0), Math.Max(0, info.X), Math.Max(0, info.Y), mask: maskOver?.Invoke(this.Rotate, info.Angle / 100.0));
                        var hybrid = background.Overlay(src, Math.Max(0, -info.X), Math.Max(0, -info.Y), mask: maskSrc)
                            .Overlay(over.Invoke(this.Rotate, info.Angle / 100.0),
                                x: Math.Max(0, info.X),
                                y: Math.Max(0, info.Y),
                                mask: maskOver?.Invoke(this.Rotate, info.Angle / 100.0),
                                mode: OverlayMode,
                                opacity: Opacity)
                            .Invoke(Downsize, frameParams.FinalWidth, frameParams.FinalHeight);
                        if (Mode == FramingMode.FillRectangle)
                            return InitClip(src, ctx.TargetInfo.Width, ctx.TargetInfo.Height, ctx.BlankColor)
                                .Overlay(hybrid, frameParams.FinalX, frameParams.FinalY);
                        var srcRect = new Rectangle(0, 0, srcSize.Width, srcSize.Height);
                        var overRect = new Rectangle(info.X, info.Y, info.Width, info.Height);
                        var union = Rectangle.Union(srcRect, overRect);
                        var intersect = Rectangle.Intersect(srcRect, overRect);
                        var cropLeft = intersect.Left - union.Left;
                        var cropTop = intersect.Top - union.Top;
                        var cropRight = union.Right - intersect.Right;
                        var cropBottom = union.Bottom - intersect.Bottom;
                        var cropWidthCoef = cropRight == 0 ? 1 : ((double)cropLeft / cropRight) / ((double)cropLeft / cropRight + 1);
                        var cropHeightCoef = cropBottom == 0 ? 1 : ((double)cropTop / cropBottom) / ((double)cropTop / cropBottom + 1);
                        cropLeft = (int)((frameParams.FinalWidth - ctx.TargetInfo.Width) * cropWidthCoef);
                        cropTop = (int)((frameParams.FinalHeight - ctx.TargetInfo.Height) * cropHeightCoef);
                        cropRight = frameParams.FinalWidth - ctx.TargetInfo.Width - cropLeft;
                        cropBottom = frameParams.FinalHeight - ctx.TargetInfo.Height - cropTop;
                        return hybrid.Crop(cropLeft, cropTop, -cropRight, -cropBottom);
                    }
                case FramingMode.FillFull:
                    {
                        frameParams.FinalWidth = !frameParams.Wider ? frameParams.MergedWidth : (int)Math.Round(frameParams.MergedHeight * frameParams.OutAr);
                        frameParams.FinalHeight = !frameParams.Wider ? (int)Math.Round(frameParams.MergedWidth / frameParams.OutAr) : frameParams.MergedHeight;
                        frameParams.FinalX = (frameParams.FinalWidth - frameParams.MergedWidth) / 2;
                        frameParams.FinalY = (frameParams.FinalHeight - frameParams.MergedHeight) / 2;
                        var maskOver = GetMask(GetOverMask);
                        var maskSrc = GetMask(GetSourceMask);
                        if (maskSrc != null && maskOver != null)
                            maskOver = maskOver.Overlay(maskSrc.Invert().Invoke(this.Rotate, -info.Angle / 100.0), -info.X, -info.Y, mode: "lighten");
                        if (overMask != null && maskOver != null)
                            maskOver = maskOver.Overlay(overMask, mode: "darken");
                        if (ctx.SourceMask != null && maskOver != null)
                            maskOver = maskOver.Overlay(ctx.SourceMask.Dynamic().Invert().Invoke(this.Rotate, -info.Angle / 100.0), -info.X, -info.Y, mode: "lighten");
                        var hybrid = GetBackground(frameParams.FinalWidth, frameParams.FinalHeight);
                        if (maskOver != null)
                            hybrid = hybrid.Overlay(over.Invoke(this.Rotate, info.Angle / 100.0), 
                                frameParams.FinalX + Math.Max(0, info.X), frameParams.FinalY + Math.Max(0, info.Y));
                        if (maskOver == null && info.Angle != 0)
                            maskOver = GetBlankClip(over, true).Dynamic();
                        return hybrid.Overlay(src, frameParams.FinalX + Math.Max(0, -info.X), frameParams.FinalY + Math.Max(0, -info.Y))
                                .Overlay(over.Invoke(this.Rotate, info.Angle / 100.0), 
                                    frameParams.FinalX + Math.Max(0, info.X), frameParams.FinalY + Math.Max(0, info.Y), 
                                    mask: maskOver?.Invoke(this.Rotate, info.Angle / 100.0))
                                .Invoke(Downsize, ctx.TargetInfo.Width, ctx.TargetInfo.Height);
                    }
                case FramingMode.Mask:
                    {
                        if (ctx.Plane.IsChroma())
                            return InitClip(src, ctx.TargetInfo.Width, ctx.TargetInfo.Height, ctx.DefaultColor);
                        src = GetBlankClip((Clip) src, true).Dynamic();
                        if (ctx.SourceMask != null)
                            src = src.Overlay(ctx.SourceMask, mode: "darken");
                        over = GetBlankClip((Clip) over, true).Dynamic();
                        return InitClip(src, frameParams.MergedWidth, frameParams.MergedHeight, ctx.DefaultColor)
                            .Overlay(src, Math.Max(0, -info.X), Math.Max(0, -info.Y))
                            .Overlay(over.Invoke(this.Rotate, info.Angle / 100.0), Math.Max(0, info.X), Math.Max(0, info.Y))
                            .Invoke(Downsize, frameParams.FinalWidth, frameParams.FinalHeight)
                            .ColorRangeMask((1 << ctx.TargetInfo.ColorSpace.GetBitDepth()) - 1)
                            .AddBorders(frameParams.FinalX, frameParams.FinalY, ctx.TargetInfo.Width - frameParams.FinalX - frameParams.FinalWidth,
                                ctx.TargetInfo.Height - frameParams.FinalY - frameParams.FinalHeight);
                    }
                default:
                    throw new AvisynthException();
            }
        }

        protected sealed override void Dispose(bool A_0)
        {
            contexts.ForEach(p => p.Dispose());
            base.Dispose(A_0);
        }
    }
}
