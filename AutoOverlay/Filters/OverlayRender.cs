using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
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
        public abstract OverlayClip[] ExtraClips { get; protected set; }

        public abstract RectangleD InnerBounds { get; protected set; } // 0-1
        public abstract RectangleD OuterBounds { get; protected set; } // 0-1
        public abstract Space OverlayBalance { get; set; } // 0-1 (0 - source, 1 - overlay)
        public abstract bool FixedSource { get; protected set; }
        public abstract int OverlayOrder { get; protected set; }

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
        public abstract bool MaskMode { get; protected set; }
        public abstract double Opacity { get; protected set; }
        public abstract string Upsize { get; protected set; }
        public abstract string Downsize { get; protected set; }
        public abstract string Rotate { get; protected set; }
        public abstract double ColorAdjust { get; protected set; }
        public abstract ColorInterpolation ColorInterpolation { get; protected set; }
        public abstract double ColorExclude { get; protected set; }
        public abstract int ColorFramesCount { get; protected set; }
        public abstract double ColorFramesDiff { get; protected set; }
        public abstract double ColorMaxDeviation { get; protected set; }
        public abstract string Matrix { get; protected set; }
        public abstract bool Invert { get; protected set; }
        public abstract bool Extrapolation { get; protected set; }
        public abstract BackgroundMode Background { get; protected set; }
        public abstract Clip BackgroundClip { get; protected set; }
        public abstract int BlankColor { get; protected set; }
        public abstract double BackBalance { get; protected set; }
        public abstract int BackBlur { get; protected set; }
        public abstract bool FullScreen { get; protected set; }
        public abstract EdgeGradient EdgeGradient { get; protected set; }
        public abstract int BitDepth { get; protected set; }
        public abstract bool SIMD { get; protected set; }
        #endregion

        public ColorSpaces ColorSpace { get; protected set; }

        private YUVPlanes[] planes;

        private Size targetSubsample;

        private readonly List<OverlayContext> contexts = new();

        protected abstract List<OverlayInfo> GetOverlayInfo(int n);

        protected override void Initialize(AVSValue args)
        {
            base.Initialize(args);
            ExtraClips ??= Array.Empty<OverlayClip>();
            if (OverlayOrder > ExtraClips.Length)
                throw new AvisynthException("Overlay order should be less than extra clips count");
            if (ColorAdjust is > float.Epsilon and < 1 - float.Epsilon && ExtraClips.Length > 0)
                throw new AvisynthException("Color adjust only [-1, 0, 1] supported with extra clips");
            if (Invert && ExtraClips.Any())
                throw new AvisynthException("Invert mode is not compatible with extra overlay clips");
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
            Upsize ??= Downsize ?? OverlayUtils.DEFAULT_RESIZE_FUNCTION;
            Downsize ??= Upsize;
            OverlayMode ??= "blend";
            var cacheSize = ColorFramesCount * 2 + 1;
            var cacheKey = StaticEnv.GetEnv2() == null ? CacheType.CACHE_25_ALL : CacheType.CACHE_GENERIC;
            foreach (var clip in new[] { Child, Source, Overlay, SourceMask, OverlayMask })
            {
                clip?.SetCacheHints(cacheKey, cacheSize);
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
            if (PixelType != null)
            {
                if (!Enum.TryParse("CS_" + PixelType, out vi.pixel_type))
                    throw new AvisynthException("Invalid PixelType");
            }
            if (vi.pixel_type.HasFlag(ColorSpaces.CS_UPlaneFirst))
                vi.pixel_type = (vi.pixel_type ^ ColorSpaces.CS_UPlaneFirst) | ColorSpaces.CS_VPlaneFirst;
            if (srcBitDepth != overBitDepth && ColorAdjust > 0 && ColorAdjust < 1)
                throw new AvisynthException("ColorAdjust -1, 0, 1 only allowed when video bit depth is different");
            BitDepth = BitDepth > 0 ? BitDepth : ColorAdjust >= 1 - double.Epsilon ? overBitDepth : srcBitDepth;
            ColorSpace = vi.pixel_type = vi.pixel_type.ChangeBitDepth(BitDepth);
            vi.num_frames = Child.GetVideoInfo().num_frames;
            planes = OverlayUtils.GetPlanes(vi.pixel_type);
            targetSubsample = new Size(ColorSpace.GetWidthSubsample(), ColorSpace.GetHeightSubsample());
            SetVideoInfo(ref vi);
        }

        protected override void AfterInitialize()
        {
            if (MaskMode)
            {
                Source = GetBlankClip(Source, true);
                Overlay = GetBlankClip(Overlay, true);
                foreach (var overlayClip in ExtraClips)
                    overlayClip.Clip = GetBlankClip(overlayClip.Clip, true);
            }
            BorderMaxDeviation /= 100.0;
            ColorMaxDeviation /= 100.0;
            if (!string.IsNullOrEmpty(Matrix))
            {
                Source = ConvertToRgb(Source);
                Overlay = ConvertToRgb(Overlay);
                foreach (var overlayClip in ExtraClips)
                    overlayClip.Clip = ConvertToRgb(overlayClip.Clip);

                Clip ConvertToRgb(Clip clp)
                {
                    var vi = clp.GetVideoInfo();
                    return vi.IsRGB() ? clp : vi.pixel_type.GetBitDepth() == 8 ?
                        clp.Dynamic().ConvertToRgb24(matrix: Matrix) :
                        clp.Dynamic().ConvertToRgb48(matrix: Matrix);
                }
            }
            var withoutSubSample = Source.GetVideoInfo().pixel_type.WithoutSubSample() &&
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
                history = history.Select(p => p.Invert().ScaleBySource(Overlay.GetSize())).ToList();
            var info = history.First();

            var res = NewVideoFrame(StaticEnv);

            var outClips = contexts.Select(ctx => RenderFrame(ctx, history));

            var hybrid = contexts.Count == 1 ? outClips.First() : DynamicEnv.CombinePlanes(outClips, 
                planes: contexts.Select(p => p.Plane.GetLetter()).Aggregate(string.Concat),
                pixel_type: $"YUV4{4 / targetSubsample.Width}{(4 / targetSubsample.Width - 4) + 4 / targetSubsample.Height}P{GetVideoInfo().pixel_type.GetBitDepth()}");

            if (!GetVideoInfo().IsRGB() && !string.IsNullOrEmpty(Matrix))
            {
                hybrid = hybrid.Invoke(OverlayUtils.GetConvertFunction(GetVideoInfo().pixel_type), matrix: Matrix);
            }
            else if (GetVideoInfo().pixel_type != ((Clip) hybrid).GetVideoInfo().pixel_type)
            {
                hybrid = hybrid.Invoke(OverlayUtils.GetConvertFunction(GetVideoInfo().pixel_type));
            }

            if (Debug)
            {
                //var resizedInfo = info.Resize(contexts.First().SourceInfo.Size, contexts.First().OverlayInfo.Size);
                var alignParams = info.DisplayInfo();
                var extra = ExtraClips.Select(p => p.GetOverlayInfo(n)).ToArray();
                var layers = OverlayLayer.GetLayers(contexts.First(), history, extra);
                var renderParams = layers[0].Data.ToString();
                var debugInfo = alignParams + "\n\n" + renderParams;
                hybrid = hybrid.Subtitle(debugInfo.Replace("\n", "\\n"), lsp: 0, size: 14);

                RenderPreview(ref hybrid, contexts.First(), info, extra.Select(p => p.First()).ToList());
            }

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
            var currentFrame = history.First().FrameNumber;
            var extra = ExtraClips.Select(p => p.GetOverlayInfo(currentFrame)).ToArray();
            var layers = OverlayLayer.GetLayers(ctx, history, extra);
            var primary = layers.First();
            var secondary = layers.Skip(1).First();

            IEnumerable<(OverlayInfo, List<OverlayLayer>)> GetLayerHistory(int length, double deviation) => new[] { -1, 0, 1 }
                .SelectMany(sign => history
                    .Where(p => Math.Abs(p.FrameNumber - currentFrame) <= length)
                    .Where(p => p.FrameNumber.CompareTo(currentFrame) == sign)
                    .OrderBy(p => sign * p.FrameNumber)
                    .Select(p => new
                    {
                        Info = p,
                        Layers = OverlayLayer.GetLayers(ctx, new List<OverlayInfo>(1) { p },
                            extra.Select(s => new List<OverlayInfo>(1) { s.First(d => d.FrameNumber == p.FrameNumber) })
                                .ToArray())
                    })
                    .TakeWhile((p, i) =>
                        p.Info.FrameNumber == (currentFrame + i * sign + sign) &&
                        (!p.Info.KeyFrame || p.Info.FrameNumber == currentFrame) &&
                        p.Info.NearlyEquals(history.First(), deviation)))
                .Select(p => (p.Info, p.Layers));

            if (ctx.AdjustColor && !MaskMode)
            {
                primary.Clip = AdjustClip(ColorAdjust, primary, secondary, ctx.Cache?[primary.Index]);
                secondary.Clip = AdjustClip(1 - ColorAdjust, secondary, primary, ctx.Cache?[secondary.Index]);
                var extraReference = ColorAdjust.IsNearlyZero() ? primary : secondary;
                foreach (var layer in layers.Skip(2))
                    layer.Clip = AdjustClip(1, layer, extraReference, ctx.Cache?[layer.Index]);

                dynamic GetMask(OverlayLayer source, OverlayLayer reference)
                {
                    var srcMask = Crop(source.Mask, source.Rectangle, reference.Rectangle);
                    var refMask = Crop(reference.Mask, reference.Rectangle, source.Rectangle);
                    return (srcMask, refMask) switch
                    {
                        (not null, not null) => srcMask.Mask.Overlay(refMask, mode: "darken"),
                        _ => srcMask ?? refMask
                    };
                }

                dynamic AdjustClip(double intensity, OverlayLayer source, OverlayLayer reference, HistogramCache cache)
                {
                    if (intensity <= double.Epsilon || intensity - 1 >= double.Epsilon)
                        return source.Clip;
                    var mask = GetMask(source, reference);
                    if (ColorFramesCount > 0)
                        using (new VideoFrameCollector())
                        {
                            var frames = new SortedSet<int>();
                            foreach (var (nearInfo, nearLayers) in GetLayerHistory(ColorFramesCount, ColorMaxDeviation))
                            {
                                var nearFrame = nearInfo.FrameNumber;
                                var colorMask = new Lazy<VideoFrame>(() => GetMask(
                                        nearLayers[source.Index],
                                        nearLayers[reference.Index])
                                    ?[nearFrame]);
                                cache.GetFrame(nearFrame,
                                    () => Extrapolation ? nearLayers[source.Index].Clip[nearFrame] : null,
                                    () => Crop(nearLayers[source.Index].Clip, source.Rectangle, reference.Rectangle)[nearFrame],
                                    () => Crop(nearLayers[reference.Index].Clip, reference.Rectangle, source.Rectangle)[nearFrame],
                                    () => colorMask.Value,
                                    () => colorMask.Value);
                                frames.Add(nearInfo.FrameNumber);
                            }
                            cache = cache.SubCache(frames.First(), frames.Last());
                        }
                    return source.Clip.ColorAdjust(
                        Crop(source.Clip, source.Rectangle, reference.Rectangle),
                        Crop(reference.Clip, reference.Rectangle, source.Rectangle), 
                        mask, mask,
                        intensity: intensity, channels: ctx.AdjustChannels, debug: Debug, SIMD: SIMD, 
                        extrapolation: Extrapolation, exclude: ColorExclude, interpolation: ColorInterpolation,
                        cacheId: cache?.Id, adjacentFramesCount: ColorFramesCount, adjacentFramesDiff: ColorFramesDiff, dynamicNoise: DynamicNoise);
                }
            }

            bool IsFullScreen(Rectangle rect) => rect.Location.IsEmpty && rect.Size == ctx.TargetInfo.Size;

            dynamic GetCanvas()
            {
                var size = FullScreen ? ctx.TargetInfo.Size : primary.Union.Size;
                var blank = ctx.BackgroundClip ?? InitClip(ctx.Source, ctx.TargetInfo.Width, ctx.TargetInfo.Height, ctx.DefaultColor);

                dynamic Finalize(dynamic background) => size == ctx.TargetInfo.Size
                    ? background
                    : blank.Overlay(background, primary.Union.Location);

                switch (Background)
                {
                    case BackgroundMode.BLANK:
                        return ctx.BlankColor == -1 ? blank : Finalize(InitClip(ctx.Source, size.Width, size.Height, ctx.BlankColor));
                    case BackgroundMode.BLUR:
                        var background = primary.Clip.BilinearResize(size.Width / 3, size.Height / 3)
                            .Overlay(secondary.Clip.BilinearResize(size.Width / 3, size.Height / 3),
                                opacity: (BackBalance + 1) / 2,
                                mask: secondary.Mask?.BilinearResize(size.Width / 3, size.Height / 3));
                        for (var i = 0; i < BackBlur; i++)
                            background = background.Blur(1.5);
                        return Finalize(background.GaussResize(size, p: 3));
                }
                return blank;
            }
            var maybeFullScreen = layers
                .Skip(0)
                .Reverse()
                .First(p => !p.Opacity.IsNearlyZero());

            if (OverlayMode == "blend" 
                && maybeFullScreen.Opacity.IsNearlyEquals(1)
                && IsFullScreen(maybeFullScreen.Rectangle))
                return primary.Clip;

            // Rendering
            var hybrid = GetCanvas();

            dynamic GetOverMask(
                OverlayLayer layer,
                EdgeGradient edgeGradient,
                int gradient,
                int noise)
            {
                if (gradient == 0 && noise == 0 || edgeGradient == EdgeGradient.NONE)
                    return null;
                var nearData = GetLayerHistory(BorderControl, BorderMaxDeviation)
                    .Select(p => p.Item2[layer.Index])
                    .ToList();
                Rectangle GetBorders(int length)
                {
                    int GetLength(Func<OverlayLayer, bool> func) => nearData.Any(func) ? length : 0;

                    int IfNotForced(int value) => edgeGradient == EdgeGradient.FULL ? 0 : value;

                    return Rectangle.FromLTRB(
                        GetLength(p => p.Rectangle.X > BorderOffset.Left + IfNotForced(p.Union.Left)),
                        GetLength(p => p.Rectangle.Y > BorderOffset.Top + IfNotForced(p.Union.Top)),
                        GetLength(p => ctx.TargetInfo.Size.Width - p.Rectangle.Right > BorderOffset.Right +
                            IfNotForced(ctx.TargetInfo.Size.Width - p.Union.Right)),
                        GetLength(p => ctx.TargetInfo.Size.Height - p.Rectangle.Bottom > BorderOffset.Bottom +
                            IfNotForced(ctx.TargetInfo.Size.Height - p.Union.Bottom))
                    );
                }

                dynamic mask = null;
                var gradientBorders = GetBorders(gradient);
                var noiseBorders = GetBorders(gradient);
                dynamic gradientMask = null, noiseMask = null;
                if (gradient > 0 && !gradientBorders.IsEmpty)
                    gradientMask = layer.Clip.OverlayMask(
                        1, 1,
                        gradientBorders,
                        gradient: true);
                if (noise > 0 && !noiseBorders.IsEmpty)
                    noiseMask = layer.Clip.OverlayMask(
                        1, 1,
                        noiseBorders,
                        gradient: gradient, noise: noise,
                        seed: DynamicNoise ? currentFrame + (int)ctx.Plane : 0);
                return gradientMask != null && noiseMask != null
                    ? noiseMask.Overlay(gradientMask, mode: "lighten")
                        .Overlay(gradientMask, mask: gradientMask.Invert())
                    : gradientMask ?? noiseMask;
            }

            void OverlayOnTop(
                OverlayLayer layer, 
                bool withMask,
                string overlayMode,
                double opacity,
                EdgeGradient edgeGradient)
            {
                if (overlayMode == "blend" 
                    && IsFullScreen(layer.Rectangle) 
                    && opacity.IsNearlyEquals(1)
                    && (!withMask || layer.Mask == null))
                {
                    hybrid = layer.Clip;
                    return;
                }
                var edgeMask = GetOverMask(layer, edgeGradient, Gradient, Noise);
                var mask = !withMask || layer.Mask == null ? edgeMask : layer.Mask.Overlay(edgeMask, mode: "darken");
                if (mask != null)
                    Log(() => $"Layer: {((Clip)layer.Clip).GetVideoInfo().GetSize()} mask: {((Clip)mask).GetVideoInfo().GetSize()}");
                if (!opacity.IsNearlyZero())
                    hybrid = hybrid.Overlay(
                        layer.Clip,
                        layer.Rectangle.Location,
                        mode: overlayMode,
                        opacity: opacity,
                        mask: mask);

                void FillChromeBorder(int length, Func<int, dynamic> crop, Func<int, (int, int)> location)
                {
                    if (length > 0)
                    {
                        var coordinates = location(length);
                        Clip cropped = crop(length);
                        Log($"Fix borders at {coordinates}: {cropped.GetVideoInfo().GetSize()}");
                        hybrid = hybrid.Overlay(
                            cropped,
                            coordinates.Item1,
                            coordinates.Item2,
                            mode: overlayMode,
                            opacity: opacity);
                    }
                }

                FillChromeBorder(layer.ExtraBorders.Left,
                    length => layer.Clip.Crop(0, 0, length - layer.Rectangle.Width, 0),
                    length => (layer.Rectangle.Left - length, layer.Rectangle.Top));
                FillChromeBorder(layer.ExtraBorders.Top,
                    length => layer.Clip.Crop(0, 0, 0, length - layer.Rectangle.Height),
                    length => (layer.Rectangle.Left, layer.Rectangle.Top - length));
                FillChromeBorder(layer.ExtraBorders.Right,
                    length => layer.Clip.Crop(layer.Rectangle.Width - length, 0, 0, 0),
                    length => (layer.Rectangle.Right, layer.Rectangle.Top));
                FillChromeBorder(layer.ExtraBorders.Bottom,
                    length => layer.Clip.Crop(0, layer.Rectangle.Height - length, 0, 0),
                    length => (layer.Rectangle.Left, layer.Rectangle.Bottom));
            }

            foreach (var layer in layers.Where(p => p.Mask != null || !p.Opacity.IsNearlyEquals(1) || Gradient > 0 || Noise > 0 || OverlayMode != "blend"))
                OverlayOnTop(layer, false, "blend", 1, EdgeGradient);

            foreach (var layer in layers)
                OverlayOnTop(layer, true, 
                    layer.Index == 0 ? "blend" : OverlayMode, 
                    layer.Opacity, 
                    EdgeGradient == EdgeGradient.NONE ? EdgeGradient.INSIDE : EdgeGradient);

            return hybrid;
        }

        protected void RenderPreview(ref dynamic hybrid, OverlayContext ctx, OverlayInfo info, List<OverlayInfo> extra)
        {
            var previewSize = (Size)(ctx.TargetInfo.Size.AsSpace() / 3).Eval(p => p - p % 2);
            var previewRect = new RectangleF(new Point(ctx.TargetInfo.Width - previewSize.Width - 10, 10), previewSize).Floor();

            var input = new OverlayInput
            {
                TargetSize = ctx.TargetInfo.Size,
                OuterBounds = OuterBounds,
                InnerBounds = InnerBounds,
                OverlayBalance = OverlayBalance,
                FixedSource = FixedSource,
                ExtraClips = ctx.ExtraClips
                    .Select((p, i) => Tuple.Create(p, extra[i]))
                    .ToList()
            };
            var canvasReal = info.GetCanvas(input);

            var union = extra
                .Select(p => p.ScaleBySource(info.SourceSize))
                .Select(p => p.OverlayRectangle)
                .Aggregate(RectangleD.Empty, RectangleD.Union)
                .Union(info.SourceRectangle)
                .Union(info.OverlayRectangle);

            var totalArea = canvasReal.Union(union);
            var coef = (previewSize.AsSpace() / totalArea).SelectMin();
            var offset = Space.Max(-canvasReal.Location, -union.Location);

            var canvas = (Rectangle) canvasReal.Offset(offset).Scale(coef);
            var src = (Rectangle) info.SourceRectangle.Offset(offset).Scale(coef);
            var over = (Rectangle) info.OverlayRectangle.Offset(offset).Scale(coef);
            var extraClips = extra
                .Select(p => p.ScaleBySource(info.SourceSize))
                .Select(p => (Rectangle) p.OverlayRectangle.Offset(offset).Scale(coef));

            dynamic PreviewClip(Size size, int color) =>
                DynamicEnv.BlankClip(width: size.Width, height: size.Height, color: color);

            dynamic PreviewMask(Size size) =>
                PreviewClip(size.Eval(p => p - 4), 0).AddBorders(2, 2, 2, 2, color: 0xFFFFFF);

            var preview = hybrid
                .Crop(previewRect.Location, previewRect.Right - ctx.TargetInfo.Width, previewRect.Bottom - ctx.TargetInfo.Height)
                .ConvertToRgb24()
                .Overlay(PreviewClip(canvas.Size, 0x0000FF), canvas.Location, mask: PreviewMask(canvas.Size), opacity: 0.75)
                .Overlay(PreviewClip(src.Size, 0xFF0000), src.Location, mask: PreviewMask(src.Size), opacity: 0.75)
                .Overlay(PreviewClip(over.Size, 0x00FF00), over.Location, mask: PreviewMask(over.Size), opacity: 0.75);
            foreach (var extraClip in extraClips)
                preview = preview.Overlay(PreviewClip(extraClip.Size, 0x808080), extraClip.Location, mask: PreviewMask(extraClip.Size), opacity: 0.75);

            hybrid = hybrid.Overlay(preview, previewRect.Location);
        }

        private static dynamic Crop(dynamic clip, Rectangle src, Rectangle over) => clip?.Crop(
            Math.Max(0, over.Left - src.Left),
            Math.Max(0, over.Top - src.Top),
            -Math.Max(0, src.Right - over.Right),
            -Math.Max(0, src.Bottom - over.Bottom)
        );

        protected sealed override void Dispose(bool A_0)
        {
            contexts.ForEach(p => p.Dispose());
            base.Dispose(A_0);
        }
    }
}
