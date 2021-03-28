using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Threading.Tasks;
using AutoOverlay.Filters;
using AutoOverlay.Histogram;
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
        public abstract int BorderControl { get; protected set; }
        public abstract double BorderMaxDeviation { get; protected set; }
        public abstract Rectangle BorderOffset { get; protected set; }
        public abstract Rectangle SrcColorBorderOffset { get; protected set; }
        public abstract Rectangle OverColorBorderOffset { get; protected set; }
        public abstract FramingMode Mode { get; protected set; }
        public abstract double Opacity { get; protected set; }
        public abstract string Upsize { get; protected set; }
        public abstract string Downsize { get; protected set; }
        public abstract string Rotate { get; protected set; }
        public abstract double ColorAdjust { get; protected set; }
        public abstract int ColorFramesCount { get; protected set; }
        public abstract double ColorFramesDiff { get; protected set; }
        public abstract double ColorMaxDeviation { get; protected set; }
        public abstract string Matrix { get; protected set; }
        public abstract bool Invert { get; protected set; }
        public abstract bool Extrapolation { get; protected set; }
        public abstract int BlankColor { get; protected set; }
        public abstract double Background { get; protected set; }
        public abstract int BackBlur { get; protected set; }
        public abstract int BitDepth { get; protected set; }
        public abstract bool SIMD { get; protected set; }
        #endregion

        private YUVPlanes[] planes;

        private Size targetSubsample;

        private readonly List<OverlayContext> contexts = new List<OverlayContext>();

        protected abstract List<OverlayInfo> GetOverlayInfo(int n);

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
            var cacheSize = ColorFramesCount * 2 + 1;
            var cacheKey = StaticEnv.GetEnv2() == null ? CacheType.CACHE_25_ALL : CacheType.CACHE_GENERIC;
            Child.SetCacheHints(cacheKey, cacheSize);
            Source.SetCacheHints(cacheKey, cacheSize);
            Overlay.SetCacheHints(cacheKey, cacheSize);
            SourceMask?.SetCacheHints(cacheKey, cacheSize);
            OverlayMask?.SetCacheHints(cacheKey, cacheSize);
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
            if (vi.pixel_type.HasFlag(ColorSpaces.CS_UPlaneFirst))
                vi.pixel_type = (vi.pixel_type ^ ColorSpaces.CS_UPlaneFirst) | ColorSpaces.CS_VPlaneFirst;
            if (srcBitDepth != overBitDepth && ColorAdjust > 0 && ColorAdjust < 1)
                throw new AvisynthException("ColorAdjust -1, 0, 1 only allowed when video bit depth is different");
            var targetBitDepth = BitDepth > 0 ? BitDepth : ColorAdjust >= 1 - double.Epsilon ? overBitDepth : srcBitDepth;
            vi.pixel_type = vi.pixel_type.ChangeBitDepth(targetBitDepth);
            vi.num_frames = Child.GetVideoInfo().num_frames;
            planes = OverlayUtils.GetPlanes(vi.pixel_type);
            targetSubsample = new Size(srcInfo.pixel_type.GetWidthSubsample(), srcInfo.pixel_type.GetHeightSubsample());
            SetVideoInfo(ref vi);
        }

        protected override void AfterInitialize()
        {
            BorderMaxDeviation /= 100.0;
            ColorMaxDeviation /= 100.0;
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
            var history = GetOverlayInfo(n);
            if (Invert)
                history.ForEach(p => p.Invert());
            var info = history.First();//.Resize(Source.GetSize(), Overlay.GetSize());

            var res = NewVideoFrame(StaticEnv);

            var outClips = contexts.Select(ctx => RenderFrame(ctx, history));

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

        protected dynamic RenderFrame(OverlayContext ctx, List<OverlayInfo> history)
        {
            var frameCtx = new FrameContext(ctx, history.First());
            var currentFrame = frameCtx.Info.FrameNumber;

            if (ctx.AdjustColor && Mode != FramingMode.Mask)
            {
                frameCtx.Source = AdjustClip(ColorAdjust, p => p.Source, p => p.SourceTest, p => p.OverlayTest, p => p.MaskTest, ctx.SourceCache);
                frameCtx.Overlay = AdjustClip(1 - ColorAdjust, p => p.Overlay, p => p.OverlayTest, p => p.SourceTest, p => p.MaskTest, ctx.OverlayCache);

                dynamic AdjustClip(double intensity, Func<FrameContext, dynamic> source, Func<FrameContext, dynamic> sample, 
                    Func<FrameContext, dynamic> reference, Func<FrameContext, dynamic> mask, HistogramCache cache)
                {
                    if (intensity <= double.Epsilon || intensity - 1 >= double.Epsilon)
                        return source(frameCtx);
                    if (ColorFramesCount > 0)
                        using (new VideoFrameCollector())
                        {
                            var frames = new SortedSet<int>();
                            foreach (var nearInfo in new[] {-1, 0, 1}.SelectMany(sign =>
                                history
                                    .Where(p => Math.Abs(p.FrameNumber - currentFrame) <= ColorFramesCount)
                                    .Where(p => p.FrameNumber.CompareTo(currentFrame) == sign)
                                    .OrderBy(p => sign * p.FrameNumber)
                                    .TakeWhile((p, i) =>
                                        p.FrameNumber == (currentFrame + i * sign + sign) &&
                                        (!p.KeyFrame || p.FrameNumber == currentFrame) &&
                                        p.NearlyEquals(history.First(), ColorMaxDeviation))))
                            {
                                var nearCtx = new FrameContext(ctx, nearInfo);
                                var nearFrame = nearInfo.FrameNumber;
                                var colorMask = new Lazy<VideoFrame>(() => mask?.Invoke(nearCtx)?[nearFrame]);
                                cache.GetFrame(nearFrame,
                                    () => Extrapolation ? source(nearCtx)[nearFrame] : null,
                                    () => sample(nearCtx)[nearFrame],
                                    () => reference(nearCtx)[nearFrame],
                                    () => colorMask.Value,
                                    () => colorMask.Value);
                                frames.Add(nearInfo.FrameNumber);
                            }
                            cache = cache.SubCache(frames.First(), frames.Last());
                        }
                    var res = source(frameCtx).ColorAdjust(sample(frameCtx), reference(frameCtx), mask(frameCtx), mask(frameCtx), 
                        intensity: intensity, channels: ctx.AdjustChannels, debug: Debug, SIMD: SIMD, extrapolation: Extrapolation, 
                        cacheId: cache?.Id, adjacentFramesCount: ColorFramesCount, adjacentFramesDiff: ColorFramesDiff, dynamicNoise: DynamicNoise);
                    if (!GetVideoInfo().IsRGB() && !string.IsNullOrEmpty(Matrix))
                        res = res.ConvertToYV24(matrix: Matrix);
                    return res;
                }
            }

            var info = frameCtx.Info;
            var src = frameCtx.Source;
            var over = frameCtx.Overlay;
            var overMask = frameCtx.OverlayMask;
            var srcSize = frameCtx.SourceSize;

            dynamic GetOverMask(int length, bool gradientMask, bool noiseMask)
            {
                var checkFrames = new[] {-1, 0, 1}.SelectMany(sign =>
                    history
                        .Where(p => Math.Abs(p.FrameNumber - info.FrameNumber) <= BorderControl)
                        .Where(p => p.FrameNumber.CompareTo(info.FrameNumber) == sign)
                        .OrderBy(p => sign * p.FrameNumber)
                        .TakeWhile((p, i) => 
                            p.FrameNumber == (info.FrameNumber + i * sign + sign) &&
                            (!p.KeyFrame || p.FrameNumber == currentFrame) &&
                            p.NearlyEquals(history.First(), BorderMaxDeviation))).ToList();

                int GetLength(Func<OverlayInfo, bool> func) => checkFrames.Any(func) ? length : 0;

                return over.OverlayMask(
                    left: GetLength(p => p.X > BorderOffset.Left),
                    top: GetLength(p => p.Y > BorderOffset.Top),
                    right: GetLength(p => p.SourceWidth - p.X - p.Width > BorderOffset.Right),
                    bottom: GetLength(p => p.SourceHeight - p.Y - p.Height > BorderOffset.Bottom),
                    gradient: gradientMask, noise: noiseMask,
                    seed: DynamicNoise ? info.FrameNumber + (int) ctx.Plane : 0);
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
                //if (Gradient > 0 && Gradient == Noise)
                //    return func(Gradient, true, true);
                dynamic mask = null;
                if (Gradient > 0)
                    mask = func(Gradient, true, false);
                if (Noise > 0)
                {
                    var noiseMask = func(Noise, false, true);
                    mask = mask == null ? noiseMask : noiseMask.Overlay(mask, mode: "lighten").Overlay(mask, mask: mask.Invert()); //mask.Overlay(noiseMask, mode: "darken");
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

            var maskOver = GetMask(GetOverMask);
            if (overMask != null)
                maskOver = maskOver == null ? overMask : maskOver.Overlay(overMask, mode: "darken");
            if (ctx.SourceMask != null && maskOver != null)
                maskOver = maskOver.Overlay(Rotate(ctx.SourceMask.Invert(), true), -info.X, -info.Y, mode: "lighten");
            if (maskOver == null && info.Angle != 0)
                maskOver = Rotate(((Clip) GetBlankClip(over, true)).Dynamic(), false);

            switch (Mode)
            {
                case FramingMode.Fit:
                    {
                        return Opacity <= double.Epsilon ? src : src.Overlay(Rotate(over, false), info.X, info.Y, mask: maskOver, opacity: Opacity, mode: OverlayMode);
                    }
                case FramingMode.Fill:
                    {
                        var maskSrc = GetMask(GetSourceMask);
                        dynamic hybrid = InitClip(src, frameParams.MergedWidth, frameParams.MergedHeight, ctx.BlankColor);
                        if (maskSrc != null || Opacity - 1 <= -double.Epsilon)
                            hybrid = hybrid.Overlay(src, Math.Max(0, -info.X), Math.Max(0, -info.Y));
                        if (maskOver != null || Opacity - 1 < double.Epsilon)
                            hybrid = hybrid.Overlay(over.Invoke(this.Rotate, info.Angle / 100.0), Math.Max(0, info.X), Math.Max(0, info.Y));

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
                        var maskSrc = GetMask(GetSourceMask);
                        var background = GetBackground(frameParams.MergedWidth, frameParams.MergedHeight);
                        if (Opacity - 1 <= -double.Epsilon)
                            background = background.Overlay(over.Invoke(this.Rotate, info.Angle / 100.0), Math.Max(0, info.X), Math.Max(0, info.Y), mask: maskOver);
                        var hybrid = background.Overlay(src, Math.Max(0, -info.X), Math.Max(0, -info.Y), mask: maskSrc)
                            .Overlay(over.Invoke(this.Rotate, info.Angle / 100.0),
                                x: Math.Max(0, info.X),
                                y: Math.Max(0, info.Y),
                                mask: maskOver,
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
                        var hybrid = GetBackground(frameParams.FinalWidth, frameParams.FinalHeight);
                        if (maskOver != null)
                            hybrid = hybrid.Overlay(over.Invoke(this.Rotate, info.Angle / 100.0), 
                                frameParams.FinalX + Math.Max(0, info.X), frameParams.FinalY + Math.Max(0, info.Y));
                        return hybrid.Overlay(src, frameParams.FinalX + Math.Max(0, -info.X), frameParams.FinalY + Math.Max(0, -info.Y))
                                .Overlay(over.Invoke(this.Rotate, info.Angle / 100.0), 
                                    frameParams.FinalX + Math.Max(0, info.X), frameParams.FinalY + Math.Max(0, info.Y), 
                                    mask: maskOver)
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
