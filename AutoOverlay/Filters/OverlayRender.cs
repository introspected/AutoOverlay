using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Threading.Tasks;
using AutoOverlay.Histogram;
using AutoOverlay.Overlay;
using AvsFilterNet;

namespace AutoOverlay
{
    public abstract class OverlayRender : OverlayFilter
    {
        #region Presets
        static OverlayRender()
        {
            Presets.Add<OverlayRenderPreset, OverlayRender>(new()
            {
                [OverlayRenderPreset.FitSource] = new()
                {
                    [nameof(FixedSource)] = _ => true,
                    [nameof(OverlayBalance)] = _ => Space.One.Minus(),
                    [nameof(Gradient)] = _ => 50,
                },
                [OverlayRenderPreset.FitScreen] = new()
                {
                    [nameof(InnerBounds)] = _ => RectangleD.One,
                    [nameof(Gradient)] = _ => 50,
                },
                [OverlayRenderPreset.FitScreenBlur] = new()
                {
                    [nameof(InnerBounds)] = _ => RectangleD.One,
                    [nameof(EdgeGradient)] = _ => EdgeGradient.INSIDE,
                    [nameof(Background)] = _ => BackgroundMode.BLUR,
                    [nameof(Gradient)] = _ => 50,
                },
                [OverlayRenderPreset.FitScreenMask] = new()
                {
                    [nameof(InnerBounds)] = _ => RectangleD.One,
                    [nameof(MaskMode)] = _ => true,
                },
                [OverlayRenderPreset.FullFrame] = new()
                {
                    [nameof(OuterBounds)] = _ => RectangleD.One,
                    [nameof(InnerBounds)] = _ => RectangleD.One,
                    [nameof(Gradient)] = _ => 50,
                },
                [OverlayRenderPreset.FullFrameBlur] = new()
                {
                    [nameof(OuterBounds)] = _ => RectangleD.One,
                    [nameof(InnerBounds)] = _ => RectangleD.One,
                    [nameof(EdgeGradient)] = _ => EdgeGradient.INSIDE,
                    [nameof(Background)] = _ => BackgroundMode.BLUR,
                    [nameof(Gradient)] = _ => 50,
                },
                [OverlayRenderPreset.FullFrameMask] = new()
                {
                    [nameof(OuterBounds)] = _ => RectangleD.One,
                    [nameof(InnerBounds)] = _ => RectangleD.One,
                    [nameof(MaskMode)] = _ => true,
                },
                [OverlayRenderPreset.Difference] = new()
                {
                    [nameof(FixedSource)] = _ => true,
                    [nameof(OverlayBalance)] = _ => Space.One.Minus(),
                    [nameof(OverlayMode)] = _ => "difference",
                    [nameof(Debug)] = _ => true,
                },
            });
        }
        #endregion

        #region Properties
        public abstract Clip Source { get; protected set; }
        public abstract Clip Overlay { get; protected set; }
        public abstract Clip SourceMask { get; protected set; }
        public abstract Clip OverlayMask { get; protected set; }
        public abstract RectangleD SourceCrop { get; protected set; }
        public abstract RectangleD OverlayCrop { get; protected set; }
        public abstract ChromaLocation? SourceChromaLocation { get; protected set; }
        public abstract ChromaLocation? OverlayChromaLocation { get; protected set; }
        public abstract OverlayClip[] ExtraClips { get; protected set; }

        public abstract OverlayRenderPreset Preset { get; protected set; }

        public abstract RectangleD InnerBounds { get; protected set; } // 0-1
        public abstract RectangleD OuterBounds { get; protected set; } // 0-1
        public abstract Space OverlayBalance { get; set; } // 0-1 (-1 - source, 1 - overlay)
        public abstract bool FixedSource { get; protected set; }
        public abstract int OverlayOrder { get; protected set; }
        
        public abstract double StabilizationDiffTolerance { get; protected set; }
        public abstract double StabilizationAreaTolerance { get; protected set; }
        public abstract int StabilizationLength { get; protected set; }

        public abstract string OverlayMode { get; protected set; }
        public abstract string AdjustChannels { get; protected set; }
        public abstract int Width { get; protected set; }
        public abstract int Height { get; protected set; }
        public abstract string PixelType { get; protected set; }
        public abstract int Gradient { get; protected set; }
        public abstract int Noise { get; protected set; }
        public abstract int BorderControl { get; protected set; }
        public abstract double BorderMaxDeviation { get; protected set; }
        public abstract Rectangle BorderOffset { get; protected set; }
        public abstract Rectangle SrcColorBorderOffset { get; protected set; }
        public abstract Rectangle OverColorBorderOffset { get; protected set; }
        public abstract bool MaskMode { get; protected set; }
        public abstract double Opacity { get; protected set; }
        public abstract string Upsize { get; protected set; }
        public abstract string Downsize { get; protected set; }
        public abstract string ChromaResize { get; protected set; }
        public abstract string Rotate { get; protected set; }

        public abstract double ColorAdjust { get; protected set; }
        public abstract int ColorBuckets { get; protected set; }
        public abstract double ColorDither { get; protected set; }
        public abstract int ColorExclude { get; protected set; }
        public abstract int ColorFramesCount { get; protected set; }
        public abstract double ColorFramesDiff { get; protected set; }
        public abstract double ColorMaxDeviation { get; protected set; }
        public abstract double GradientColor { get; protected set; }
        public abstract int[] ColorFrames { get; protected set; }
        public abstract Clip ColorMatchTarget { get; protected set; }

        public abstract string Matrix { get; protected set; }
        public abstract string SourceMatrix { get; protected set; }
        public abstract string OverlayMatrix { get; protected set; }
        public abstract bool Invert { get; protected set; }
        public abstract BackgroundMode Background { get; protected set; }
        public abstract Clip BackgroundClip { get; protected set; }
        public abstract int BlankColor { get; protected set; }
        public abstract double BackBalance { get; protected set; }
        public abstract int BackBlur { get; protected set; }
        public abstract bool FullScreen { get; protected set; }
        public abstract EdgeGradient EdgeGradient { get; protected set; }
        public abstract int BitDepth { get; protected set; }
        public abstract bool Preview { get; protected set; }
        public abstract string SourceName { get; protected set; }
        public abstract string OverlayName { get; protected set; }
        public abstract int Legend { get; protected set; }
        #endregion

        public ColorSpaces ColorSpace { get; protected set; }
        public OverlayStabilization Stabilization { get; protected set; }

        private YUVPlanes[] planes;
        private string chromaResample;

        public Func<ChromaLocation?> SrcChromaLocation { get; private set; }
        public Func<ChromaLocation?> OverChromaLocation { get; private set; }
        public Func<ChromaLocation?>[] ExtraChromaLocation { get; private set; }

        protected readonly List<OverlayContext> contexts = [];

        private Clip srcDispose, overlayDispose;

        private bool mtRender;

        protected abstract OverlayEngineFrame GetOverlayInfo(int n);

        protected override void Initialize(AVSValue args)
        {
            base.Initialize(args);
            ExtraClips ??= [];

            var cacheSize = Math.Max(10, ColorFramesCount * 2 + 1);
            srcDispose = Source;
            overlayDispose = Overlay;
            Source = Source.Dynamic().Cache(cacheSize);
            Overlay = Overlay.Dynamic().Cache(cacheSize);

            mtRender = StaticEnv.GetVar("AO_MT_RENDER", false);

            // AviSynth 3.7.4 hack
            var version = ((Clip)AvsUtils.GetBlankClip(Source, true)).GetVersion();
            if (version > 10)
            {
                static string ChangeMatrix(string matrix) => matrix?.ToLower()?.StartsWith("rec") ?? false ? "PC." + matrix.Substring(3) : matrix;

                Matrix = ChangeMatrix(Matrix);
                SourceMatrix = ChangeMatrix(SourceMatrix);
                OverlayMatrix = ChangeMatrix(OverlayMatrix);
            }

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
                Initialize(Overlay, Source, OverlayChromaLocation, SourceChromaLocation);
            }
            else
            {
                Initialize(Source, Overlay, SourceChromaLocation, OverlayChromaLocation);
            }
            Upsize ??= Downsize ?? StaticEnv.FunctionCoalesce(OverlayConst.DEFAULT_RESIZE_FUNCTION + "MT", OverlayConst.DEFAULT_RESIZE_FUNCTION);
            ChromaResize ??= Downsize ??= Upsize;

            chromaResample = ChromaResize.GetChromaResample();
            OverlayMode ??= "blend";
        }

        private void Initialize(Clip src, Clip over, ChromaLocation? srcChromaLocation, ChromaLocation? overChromaLocation)
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
            vi.pixel_type = vi.pixel_type.VPlaneFirst();
            if (PixelType == null)
            {
                PixelType = vi.pixel_type.GetName();
            }
            else
            {
                vi.pixel_type = PixelType.ParseColorSpace();
            }
            if (srcBitDepth != overBitDepth && ColorAdjust is > 0 and < 1)
                throw new AvisynthException("ColorAdjust 0, 1 only allowed when video bit depth is different");
            BitDepth = BitDepth > 0 ? BitDepth : ColorAdjust.IsNearlyEquals(1) ? overBitDepth : srcBitDepth;
            ColorSpace = vi.pixel_type = vi.pixel_type.ChangeBitDepth(BitDepth);
            vi.num_frames = Child.GetVideoInfo().num_frames.Enumerate().Union(ExtraClips.Select(p => p.Child.GetVideoInfo().num_frames)).Min();
            planes = string.IsNullOrEmpty(Matrix) ? vi.pixel_type.GetPlanes() : [YUVPlanes.PLANAR_R, YUVPlanes.PLANAR_G, YUVPlanes.PLANAR_B];
            PixelType = vi.pixel_type.GetName();
            if (ColorMatchTarget != null)
            {
                var targetSize = ColorMatchTarget.GetSize();
                vi.width = targetSize.Width;
                vi.height = targetSize.Height;
                vi.num_frames = ColorMatchTarget.GetVideoInfo().num_frames;
            }
            SetVideoInfo(ref vi);

            SrcChromaLocation = GetChromaLocation(src, srcChromaLocation);
            OverChromaLocation = GetChromaLocation(over, overChromaLocation);
            ExtraChromaLocation = ExtraClips.Select(p => GetChromaLocation(p.Clip, p.ChromaLocation)).ToArray();

            Func<ChromaLocation?> GetChromaLocation(Clip clip, ChromaLocation? predefined)
            {
                if (predefined.HasValue)
                    return () => predefined.Value;
                var property = new ClipProperty<int?>(clip, "_ChromaLocation");
                return () => property.Value.HasValue ? (ChromaLocation)property.Value : null;
            }
        }

        protected override void AfterInitialize()
        {
            if (MaskMode)
            {
                Source = AvsUtils.GetBlankClip(Source, true);
                Overlay = AvsUtils.GetBlankClip(Overlay, true);
                foreach (var overlayClip in ExtraClips)
                    overlayClip.Clip = AvsUtils.GetBlankClip(overlayClip.Clip, true);
            }
            BorderMaxDeviation /= 100.0;
            ColorMaxDeviation /= 100.0;
            StabilizationAreaTolerance /= 100.0;
            Stabilization = new OverlayStabilization(StabilizationLength, StabilizationDiffTolerance, StabilizationAreaTolerance);
            var source = Source;
            var overlay = Overlay;
            if (!string.IsNullOrEmpty(Matrix))
            {
                source = ConvertToRgb(Source, SourceMatrix ?? Matrix);
                overlay = ConvertToRgb(Overlay, OverlayMatrix ?? Matrix);
                ColorSpace = Invert != ColorAdjust.IsNearlyEquals(1) ? overlay.GetVideoInfo().pixel_type : source.GetVideoInfo().pixel_type;
                PixelType = ColorSpace.GetName();
                foreach (var overlayClip in ExtraClips)
                    overlayClip.Clip = ConvertToRgb(overlayClip.Clip, overlayClip.Matrix ?? Matrix);

                Clip ConvertToRgb(Clip clp, string matrix) => clp.Dynamic().ConvertToPlanarRGB(matrix: matrix, chromaresample: chromaResample);
            }

            var withoutSubSample = ColorSpace.IsWithoutSubSample() &&
                                   source.GetVideoInfo().pixel_type.IsWithoutSubSample() &&
                                   overlay.GetVideoInfo().pixel_type.IsWithoutSubSample() &&
                                   ExtraClips.All(p => p.Clip.GetVideoInfo().pixel_type.IsWithoutSubSample());
            if (withoutSubSample)
            {
                contexts.Add(new OverlayContext(source, overlay, this, default));
            }
            else
            {
                contexts.AddRange(planes.Select(plane => new OverlayContext(source, overlay, this, plane)));
            }
        }

        protected override VideoFrame GetFrame(int n)
        {
            var ctx = contexts.First();
            var frameInfo = GetOverlayInfo(n);
            frameInfo = AdaptOverlayInfo(frameInfo, ctx.OverlayInfo.Size, OverlayCrop, Invert);
            var info = frameInfo.Sequence.First();
            StaticEnv.SetVar("current_frame", info.FrameNumber.ToAvsValue());
            var extraFrames = GetExtraOverlayInfo(n);

            var hybrid = RenderFrame(frameInfo, extraFrames);

            if (Debug)
            {
                var alignParams = info.DisplayInfo();
                var layers = OverlayLayer.GetLayers(info.FrameNumber, contexts.First(), frameInfo, extraFrames);
                var renderParams = layers[0].Data.ToString();
                var debugInfo = alignParams + "\n\n" + renderParams;
                hybrid = hybrid.Subtitle(debugInfo.Replace("\n", "\\n") + $"\\nMT: {mtRender}", lsp: 0, size: 14);
            }
            if (Debug || Preview)
                RenderPreview(ref hybrid, contexts.First(), frameInfo.Sequence, extraFrames.Select(p => p.Sequence).ToArray());

            var chromaLocation = SrcChromaLocation() ?? OverChromaLocation();
            if (chromaLocation.HasValue)
                hybrid = hybrid.propSet("_ChromaLocation", (int)chromaLocation.Value);

            return hybrid[info.FrameNumber];
        }

        protected virtual dynamic RenderFrame(OverlayEngineFrame frameInfo, OverlayEngineFrame[] extra)
        {
            var outClips = contexts.Select(ctx => RenderFrame(ctx, frameInfo, extra));

            dynamic hybrid;
            if (contexts.Count == 1)
                hybrid = outClips.First();
            else if (mtRender)
                hybrid = DynamicEnv.CombinePlanesMT(outClips, pixelType: PixelType);
            else
                hybrid = DynamicEnv.CombinePlanes(outClips,
                    planes: contexts.Select(p => p.Plane.GetLetter()).Aggregate(string.Concat),
                    pixel_type: PixelType);

            var vi = GetVideoInfo();
            if (!vi.IsRGB() && !string.IsNullOrEmpty(Matrix))
            {
                var convertFunction = vi.pixel_type.GetConvertFunction();
                return hybrid.Invoke(convertFunction, matrix: Matrix, chromaresample: chromaResample);
            }
            if (vi.pixel_type != ((Clip)hybrid).GetVideoInfo().pixel_type)
            {
                var convertFunction = vi.pixel_type.GetConvertFunction();
                return hybrid.Invoke(convertFunction, chromaresample: chromaResample);
            }

            return hybrid;
        }

        protected dynamic RenderFrame(
            OverlayContext ctx, 
            OverlayEngineFrame frameInfo,
            OverlayEngineFrame[] extraFrames)
        {
            var history = frameInfo.Sequence;
            var currentFrame = history.First().FrameNumber;
            var layers = OverlayLayer.GetLayers(currentFrame, ctx, frameInfo, extraFrames);
            var primary = layers.First();
            var secondary = layers.Skip(1).First();

            IEnumerable<(int, List<OverlayLayer>)> GetLayerHistory(int length, double deviation) => new[] { -1, 0, 1 }
                .SelectMany(sign => history
                    .Where(p => Math.Abs(p.FrameNumber - currentFrame) <= length)
                    .Where(p => Math.Sign(p.FrameNumber.CompareTo(currentFrame)) == sign)
                    .OrderBy(p => sign * p.FrameNumber)
                    .Select(p => new
                    {
                        Info = p,
                        Prev = history.FirstOrDefault(f => f.FrameNumber == p.FrameNumber - sign),
                        Layers = sign == 0 ? layers : OverlayLayer.GetLayers(p.FrameNumber, ctx, frameInfo, extraFrames)
                    })
                    .TakeWhile(p => sign == 0 || p.Prev != null &&
                        (sign == 1 && !p.Info.KeyFrame || sign == -1 && !p.Prev.KeyFrame) &&
                        p.Info.NearlyEquals(p.Prev, deviation)))
                .Select(p => (p.Info.FrameNumber, p.Layers))
                .OrderBy(p => p.FrameNumber);

            if (ctx.AdjustColor && !MaskMode)
            {
                primary.Clip = AdjustClip(ColorAdjust, primary, secondary, ctx.Cache?[primary.Index]);
                if (ColorMatchTarget != null)
                    return primary.Clip;
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

                dynamic AdjustClip(double intensity, OverlayLayer source, OverlayLayer reference, string cacheId)
                {
                    var tuples = ColorMatchTuple.Compose(source.Clip, source.Clip, reference.Clip, AdjustChannels, true, ctx.Plane.GetLetter());
                    if (intensity <= 0)
                        return source.Clip;
                    var mask = GetMask(source, reference);

                    int[] frames = null;

                    var layerEngineFrame = source.Source ? reference.EngineFrame : source.EngineFrame;

                    var colorMode = layerEngineFrame.KeyFrames.Any() ? ColorMatchMode.Scene :
                        ColorFrames.Any() ? ColorMatchMode.Frames :
                        ColorFramesCount > 0 ? ColorMatchMode.History : ColorMatchMode.None;
                    if (layerEngineFrame.KeyFrames.Any())
                        colorMode = ColorMatchMode.Scene;

                    if (colorMode == ColorMatchMode.None)
                        cacheId = null;
                    else
                    {
                        double? cornerGradient = GradientColor > 0 ? GradientColor : null;
                        var cache = ColorHistogramCache.GetOrAdd(cacheId, () => new ColorHistogramCache(tuples, ColorBuckets, false, cornerGradient));

                        IEnumerable<(int nearFrame, OverlayLayer srcLayer, OverlayLayer refLayer)> sequence;

                        void FrameBasedSequence(List<OverlayInfo> keyFrames)
                        {
                            cache.Shrink(keyFrames.First().FrameNumber, keyFrames.Last().FrameNumber);
                            var overIndex = Math.Max(source.Index, reference.Index);
                            sequence = keyFrames
                                .Select(p => (p.FrameNumber, OverlayLayer.GetLayerPair(ctx, p, overIndex)))
                                .Select(p => source.Source
                                    ? (p.Item1, p.Item2.Item1, p.Item2.Item2)
                                    : (p.Item1, p.Item2.Item2, p.Item2.Item1));
                            frames = frameInfo.KeyFrames.Select(p => p.FrameNumber).ToArray();
                        }

                        switch (colorMode)
                        {
                            case ColorMatchMode.History:
                                cache.Shrink(currentFrame - OverlayConst.ENGINE_HISTORY_LENGTH * 2, currentFrame + OverlayConst.ENGINE_HISTORY_LENGTH * 2);
                                sequence = GetLayerHistory(ColorFramesCount, ColorMaxDeviation)
                                    .Select(p => (p.Item1, p.Item2[source.Index], p.Item2[reference.Index]));
                                break;
                            case ColorMatchMode.Scene:
                                FrameBasedSequence(layerEngineFrame.KeyFrames);
                                break;
                            case ColorMatchMode.Frames:
                                if (cache.IsEmpty)
                                {
                                    var overIndex = Math.Max(source.Index, reference.Index);
                                    var isMainOver = overIndex == ctx.Render.OverlayOrder + 1;
                                    var extraIndex = overIndex > ctx.Render.OverlayOrder ? overIndex - 2 : overIndex - 1;
                                    var overSize = isMainOver ? contexts.First().OverlayInfo.Size : contexts.First().ExtraClips[extraIndex].Info.Size;
                                    var crop = isMainOver ? OverlayCrop : ExtraClips[extraIndex].Crop;
                                    Func<int, OverlayEngineFrame> getOverlayInfo = isMainOver
                                        ? GetOverlayInfo
                                        : n => ExtraClips[extraIndex].GetOverlayInfo(n);
                                    FrameBasedSequence(ColorFrames
                                        .Select(p => AdaptOverlayInfo(getOverlayInfo(p), overSize, crop, Invert).Sequence.First())
                                        .ToList());
                                }
                                else sequence = [];
                                frames = ColorFrames;
                                break;
                            default:
                                throw new InvalidOperationException();
                        }
                        var tasks = new List<Task>();
                        foreach (var (nearFrame, srcLayer, refLayer) in sequence)
                        {
                            //System.Diagnostics.Debug.WriteLine("Near frame around " + currentFrame + ": " + nearFrame);
                            var colorMask = GetMask(srcLayer, refLayer);
                            var task = cache.GetOrAdd(nearFrame,
                                Crop(srcLayer.Clip, srcLayer.Rectangle, refLayer.Rectangle),
                                Crop(refLayer.Clip, refLayer.Rectangle, srcLayer.Rectangle),
                                colorMask, colorMask);
                            tasks.Add(task);
                        }
                        Task.WaitAll(tasks.ToArray());
                    }

                    var colorMatchSrc = ColorMatchTarget?.ExtractPlane(ctx.Plane)?.Dynamic() ?? source.Clip;
                    return colorMatchSrc.ColorMatch(
                        Crop(reference.Clip, reference.Rectangle, source.Rectangle),
                        Crop(source.Clip, source.Rectangle, reference.Rectangle),
                        mask, mask, frames: frames,
                        cacheId: cacheId, frameBuffer: ColorFramesCount, frameDiff: ColorFramesDiff,
                        length: ColorBuckets, dither: ColorDither, intensity: intensity, exclude: ColorExclude,
                        gradient: GradientColor, channels: ctx.AdjustChannels, plane: ctx.Plane.GetLetter());
                }
            }

            if (ColorMatchTarget != null)
                return primary.Clip;

            bool IsFullScreen(Rectangle rect) => rect.Location.IsEmpty && rect.Size == ctx.TargetInfo.Size;

            dynamic GetCanvas()
            {
                var size = FullScreen ? ctx.TargetInfo.Size : primary.ActiveArea.Size;
                var pixelType = ctx.SourceInfo.ColorSpace.ChangeBitDepth(BitDepth).GetName();
                var blank = ctx.BackgroundClip ?? AvsUtils.InitClip(ctx.Source, ctx.TargetInfo.Size, ctx.DefaultColor, pixelType);

                dynamic Finalize(dynamic background) => size == ctx.TargetInfo.Size
                    ? background
                    : blank.Overlay(background, primary.ActiveArea.Location);

                switch (Background)
                {
                    case BackgroundMode.BLANK:
                        return ctx.BlankColor == -1 ? blank : Finalize(AvsUtils.InitClip(ctx.Source, size, ctx.BlankColor, pixelType));
                    case BackgroundMode.BLUR:
                        var primaryFiller = primary.Clip.GaussResize(size.Width / 3, size.Height / 3, p: BackBlur);
                        var secondaryFiller = secondary.Clip.GaussResize(size.Width / 3, size.Height / 3, p: BackBlur);
                        var mask = secondary.Mask?.BilinearResize(size.Width / 3, size.Height / 3);
                        var opacity = (BackBalance + 1) / 2;
                        dynamic background;
                        if (opacity.IsNearlyEquals(0))
                            background = primaryFiller;
                        else if (opacity.IsNearlyEquals(1))
                            background = secondaryFiller;
                        else background = primaryFiller.Overlay(secondaryFiller, mask: mask, opacity: opacity);
                        return Finalize(background.GaussResize(size, p: 3));
                }
                return blank;
            }
            
            //var maybeFullScreen = layers.Last(p => !p.Opacity.IsNearlyZero());

            //// TODO Crop and resize check
            //if (false && OverlayMode == "blend" 
            //    && maybeFullScreen.Opacity.IsNearlyEquals(1)
            //    && IsFullScreen(maybeFullScreen.Rectangle)
            //    && maybeFullScreen.Mask == null)
            //    return maybeFullScreen.Clip;

            // Rendering
            var canvas = GetCanvas();
            var canvasMask = AvsUtils.GetBlankClip(canvas, true);
            var activeMask = false;
            var edgeMask = AvsUtils.GetBlankClip(canvas, false);

            var hybrid = canvas;

            var rects = DynamicEnv.UnalignedSplice(layers
                .Select(p => p.Rectangle)
                .Select(p => DynamicEnv.Rect(p.X, p.Y, p.Width, p.Height)).ToArray());

            var excludedLayers = new List<int>(layers.Count);

            var edgeMaskMode = EdgeGradient != EdgeGradient.NONE && !MaskMode && Gradient > 0;

            void OverlayOnTop(OverlayLayer layer)
            {
                if (!layer.Source)
                {
                    if (layer.Opacity.IsNearlyEquals(0) && layers
                            .Take(layer.Index)
                            .Where(p => p.Mask == null)
                            .Select(p => p.Rectangle)
                            .Any(p => Rectangle.Intersect(p, layer.Rectangle) == layer.Rectangle))
                    {
                        excludedLayers.Add(layer.Index);
                        return;
                    }

                    if (layers
                        .Skip(layer.Index + 1)
                        .Where(p => p.Mask == null && p.Opacity.IsNearlyEquals(1))
                        .Select(p => p.Rectangle)
                        .Any(p => Rectangle.Intersect(p, layer.Rectangle) == layer.Rectangle))
                    {
                        excludedLayers.Add(layer.Index);
                        return;
                    }
                }

                var overlayMode = layer.Index == 0 ? "blend" : OverlayMode;
                if (overlayMode == "blend"
                    && IsFullScreen(layer.Rectangle)
                    && layer.Opacity.IsNearlyEquals(1)
                    && layer.Mask == null)
                {
                    hybrid = layer.Clip;
                    edgeMaskMode = false;
                    canvasMask = AvsUtils.GetBlankClip(layer.Clip, false);
                    activeMask = false;
                    return;
                }


                void LogFile(dynamic mask, string name)
                {
                    if (ctx.Plane is YUVPlanes.PLANAR_Y or 0 && mask != null)
                    {
                        VideoFrame frame = mask[currentFrame];
                        frame.ToBitmap(PixelFormat.Format8bppIndexed).Save(@$"D:\TEST\{name}{layer.Index}.png");
                    }
                }

                dynamic LayerMask(EdgeGradient gradientEdge, double opacity, bool noise) => layer.Clip.LayerMask(
                    layerIndex: layer.Index,
                    canvasWidth: ctx.TargetInfo.Width,
                    canvasHeight: ctx.TargetInfo.Height,
                    gradient: (noise && Noise > 0 ? Noise : Gradient) / ctx.SubSample.Width, // TODO
                    noise: noise && Noise > 0,
                    gradientEdge: gradientEdge, // TODO fix transparent support
                    opacity: opacity,
                    layers: rects,
                    excludedLayers: excludedLayers.ToArray(),
                    seed: currentFrame //+ layer.Index
                );

                var mask = EdgeGradient == EdgeGradient.NONE && layer.Source
                           || (Gradient == 0 && Noise == 0 || MaskMode && EdgeGradient == EdgeGradient.NONE)
                           && layer.Mask == null && (layer.Opacity.IsNearlyEquals(1) || MaskMode)
                    ? null
                    : LayerMask(EdgeGradient.NONE, layer.Opacity, true);

                var backMask = ((Clip)canvasMask).ROI(layer.Rectangle);
                if (layer.Mask == null)
                {
                    var black = AvsUtils.GetBlankClip(layer.Clip, false);
                    canvasMask = canvasMask.Overlay(black, layer.Rectangle.Location);

                    if (activeMask && mask != null)
                        mask = mask.Overlay(backMask, mode: "lighten");
                }
                else
                {
                    mask = mask == null ? layer.Mask : mask.Overlay(layer.Mask, mode: "darken");
                    mask = mask.Overlay(backMask, mode: "lighten");

                    canvasMask = canvasMask.Overlay(layer.Mask.Invert(), layer.Rectangle.Location, mode: "darken");
                    activeMask = true;
                }

                if (edgeMaskMode)
                {
                    var edgeLayerMask = LayerMask(EdgeGradient, 1, false);
                    var mode = layer.Rotation ? "lighten" : "blend";
                    edgeMask = edgeMask.Overlay(layer.WhiteMask, layer.Rectangle.Location, mask: edgeLayerMask, mode: mode);
                }


                //LogFile(mask, nameof(mask));

                hybrid = hybrid.Overlay(
                    layer.Clip,
                    layer.Rectangle.Location,
                    mode: overlayMode,
                    mask: mask);

                FillChromeBorder(layer.ExtraBorders.Left,
                    (length, clip) => clip.Crop(0, 0, length - layer.Rectangle.Width, 0),
                    length => (layer.Rectangle.Left - length, layer.Rectangle.Top));
                FillChromeBorder(layer.ExtraBorders.Top,
                    (length, clip) => clip.Crop(0, 0, 0, length - layer.Rectangle.Height),
                    length => (layer.Rectangle.Left, layer.Rectangle.Top - length));
                FillChromeBorder(layer.ExtraBorders.Right,
                    (length, clip) => clip.Crop(layer.Rectangle.Width - length, 0, 0, 0),
                    length => (layer.Rectangle.Right, layer.Rectangle.Top));
                FillChromeBorder(layer.ExtraBorders.Bottom,
                    (length, clip) => clip.Crop(0, layer.Rectangle.Height - length, 0, 0),
                    length => (layer.Rectangle.Left, layer.Rectangle.Bottom));

                void FillChromeBorder(int length, Func<int, dynamic, dynamic> crop, Func<int, (int, int)> location)
                {
                    if (length > 0)
                    {
                        var coordinates = location(length);
                        Clip cropped = crop(length, layer.Clip);
                        DisableLog(() => $"Fix borders at {coordinates}: {cropped.GetVideoInfo().GetSize()}");
                        hybrid = hybrid.Overlay(
                            cropped,
                            coordinates.Item1,
                            coordinates.Item2,
                            mode: overlayMode,
                            mask: mask == null ? null : crop(length, mask));
                    }
                }
            }

            foreach (var layer in layers)
                OverlayOnTop(layer);

            if (edgeMaskMode)
                hybrid = canvas.Overlay(hybrid, mask: edgeMask);
            
            return hybrid;
        }

        private OverlayEngineFrame[] GetExtraOverlayInfo(int frame)
        {
            var ctx = contexts.First();
            return ExtraClips
                .Select((p, i) => AdaptOverlayInfo(p.GetOverlayInfo(frame), ctx.ExtraClips[i].Info.Size, p.Crop))
                .ToArray();
        }

        private OverlayEngineFrame AdaptOverlayInfo(OverlayEngineFrame engineFrame, Size overSize, RectangleD overCrop, bool invert = false)
        {
            var ctx = contexts.First();
            return new OverlayEngineFrame(Adapt(engineFrame.Sequence), Adapt(engineFrame.KeyFrames));

            List<OverlayInfo> Adapt(List<OverlayInfo> list) => list
                .Select(info => info
                    .ScaleBySource(ctx.SourceInfo.Size, SourceCrop)
                    .CropOverlay(overSize, overCrop))
                .Select(p => invert ? p.Invert() : p)
                .ToList();
        }

        private void RenderPreview(ref dynamic hybrid, OverlayContext ctx, List<OverlayInfo> history, List<OverlayInfo>[] extra)
        {
            var previewWidth = Debug ? 3 : 4;
            var previewSize = (Size)(ctx.TargetInfo.Size.AsSpace() / previewWidth).Eval(p => p - p % 4);
            var previewRect = new RectangleF(new Point(ctx.TargetInfo.Width - previewSize.Width - 20, 20), previewSize).Floor();

            var input = new OverlayInput
            {
                SourceSize = ctx.SourceInfo.Size,
                OverlaySize = ctx.OverlayInfo.Size,
                TargetSize = ctx.TargetInfo.Size,
                OuterBounds = OuterBounds,
                InnerBounds = InnerBounds,
                OverlayBalance = OverlayBalance,
                FixedSource = FixedSource,
                ExtraClips = ctx.ExtraClips
            };
            var canvasReal = OverlayMapper.For(history.First().FrameNumber, input, history, Stabilization, extra).GetCanvas();
            var info = history.First();
            var union = extra
                .Select(p => p.First())
                .Select(p => p.OverlayRectangle)
                .Aggregate(RectangleD.Empty, RectangleD.Union)
                .Union(info.SourceRectangle)
                .Union(info.OverlayRectangle);

            var totalArea = canvasReal.Union(union);
            var coef = (previewSize.AsSpace() / totalArea).SelectMin();
            var offset = Space.Max(-canvasReal.Location, -union.Location);

            Rectangle PrepareOverlay(OverlayInfo i)
            {
                var rect = i.OverlayRectangle.Offset(offset).Scale(coef);
                //rect = new RectangleD(rect.Location, BilinearRotate.CalculateSize(rect.Size, info.Angle));
                return rect.Floor();
            }

            var canvas = canvasReal.Offset(offset).Scale(coef).Floor();
            var src = info.SourceRectangle.Offset(offset).Scale(coef).Floor();
            var over = PrepareOverlay(info);

            var pixelType = GetVideoInfo().pixel_type.WithoutSubSample();

            dynamic PreviewClip(Size size, int color, double angle = 0)
            {
                var rect = DynamicEnv.BlankClip(width: size.Width, height: size.Height, color: color, pixel_type: pixelType.GetName());
                return angle == 0 ? rect : rect.BilinearRotate(angle);
            }

            var reference = hybrid;

            dynamic PreviewMask(Size size, double angle = 0) =>
                AvsUtils.InitClip(reference, size.Eval(p => p - 4), 0, pixelType.GetName())
                    .AddBorders(2, 2, 2, 2, color: 0xFFFFFF)
                    .BilinearRotate(angle);

            var preview = hybrid
                .Crop(previewRect.Location, previewRect.Right - ctx.TargetInfo.Width, previewRect.Bottom - ctx.TargetInfo.Height)
                .Invoke(pixelType.GetConvertFunction())
                .Overlay(PreviewClip(canvas.Size, 0x0000FF), canvas.Location, mask: PreviewMask(canvas.Size), opacity: 0.75)
                .Overlay(PreviewClip(src.Size, 0xFF0000), src.Location, mask: PreviewMask(src.Size), opacity: 0.75)
                .Overlay(PreviewClip(over.Size, 0x00FF00, info.Angle), over.Location, mask: PreviewMask(over.Size, info.Angle), opacity: 0.75);
            
            for (var i = 0; i < ExtraClips.Length; i++)
            {
                var clip = ExtraClips[i];
                var rect = PrepareOverlay(extra[i].First());
                var extraPreview = PreviewClip(rect.Size, clip.Color);
                preview = preview.Overlay(extraPreview, rect.Location, mask: PreviewMask(rect.Size), opacity: 0.75);
            }

            hybrid = hybrid.Overlay(preview, previewRect.Location);
            if (Legend > 0)
                RenderLegend(ref hybrid, history.First().FrameNumber);
        }

        private void RenderLegend(ref dynamic source, int frame)
        {
            var ctx = contexts.First();
            var linesCount = ExtraClips.Length + 2;
            var offset = 0;
            if (!Debug)
            {
                linesCount++;
                offset++;
            }
            var lineHeight = Legend + 2;
            var legendHeight = lineHeight * linesCount;
            var legendY = ctx.TargetInfo.Height - legendHeight - 4;
            int ClipY(int num) => lineHeight * (num + offset);

            var legend = source.Crop(0, legendY, Math.Min(ctx.TargetInfo.Width, 200), 0)
                .Subtitle(SourceName ?? "Source", y: ClipY(0), text_color: 0xFF0000, size: Legend)
                .Subtitle(OverlayName ?? "Overlay", y: ClipY(1), text_color: 0x00FF00, size: Legend);
            if (!Debug)
                legend = legend.Subtitle($"Frame: {frame}", size: Legend);
            for (var i = 0; i < ExtraClips.Length; i++)
            {
                var extraClip = ExtraClips[i];
                var name = extraClip.Name ?? $"Extra clip {i}";
                legend = legend.Subtitle(name, y: ClipY(i + 2), text_color: extraClip.Color, size: Legend);
            }

            source = source.Overlay(legend, 0, legendY);
        }

        private static dynamic Crop(dynamic clip, Rectangle src, Rectangle over) => clip?.Crop(
            Math.Max(0, over.Left - src.Left),
            Math.Max(0, over.Top - src.Top),
            -Math.Max(0, src.Right - over.Right),
            -Math.Max(0, src.Bottom - over.Bottom)
        );

        protected sealed override void Dispose(bool A_0)
        {
            srcDispose.Dispose();
            overlayDispose.Dispose();
            contexts.ForEach(p => p.Dispose());
            base.Dispose(A_0);
        }
    }
}
