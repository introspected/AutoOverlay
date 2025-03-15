using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
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
        public abstract bool Noise { get; protected set; }
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
        public abstract int ColorBuckets { get; protected set; }
        public abstract double ColorDither { get; protected set; }
        public abstract double ColorExclude { get; protected set; }
        public abstract int ColorFramesCount { get; protected set; }
        public abstract double ColorFramesDiff { get; protected set; }
        public abstract double ColorMaxDeviation { get; protected set; }
        public abstract bool ColorBufferedExtrapolation { get; protected set; }
        public abstract double GradientColor { get; protected set; }
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
        #endregion

        public ColorSpaces ColorSpace { get; protected set; }
        public OverlayStabilization Stabilization { get; protected set; }

        private YUVPlanes[] planes;

        protected readonly List<OverlayContext> contexts = [];

        protected abstract List<OverlayInfo> GetOverlayInfo(int n);

        protected override void Initialize(AVSValue args)
        {
            base.Initialize(args);
            ExtraClips ??= [];
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
            Upsize ??= Downsize ?? StaticEnv.FunctionCoalesce(OverlayConst.DEFAULT_RESIZE_FUNCTION + "MT", OverlayConst.DEFAULT_RESIZE_FUNCTION);
            Downsize ??= Upsize;
            OverlayMode ??= "blend";
            var cacheSize = OverlayConst.ENGINE_HISTORY_LENGTH * 2 + 1;
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
            vi.num_frames = Child.GetVideoInfo().num_frames;
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

                Clip ConvertToRgb(Clip clp, string matrix) => clp.Dynamic().ConvertToPlanarRGB(matrix);
            }

            var withoutSubSample = ColorSpace.WithoutSubSample() &&
                                   source.GetVideoInfo().pixel_type.WithoutSubSample() &&
                                   overlay.GetVideoInfo().pixel_type.WithoutSubSample() &&
                                   ExtraClips.All(p => p.Clip.GetVideoInfo().pixel_type.WithoutSubSample());
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
            var history = GetOverlayInfo(n).Select(p =>
                    p.ScaleBySource(ctx.SourceInfo.Size, SourceCrop)
                        .CropOverlay(ctx.OverlayInfo.Size, OverlayCrop))
                .ToList();
            if (Invert)
                history = history.Select(p => p.Invert()).ToList();
            var info = history.First();
            var extra = GetExtraOverlayInfo(n);

            var hybrid = RenderFrame(history, extra);

            if (Debug)
            {
                var alignParams = info.DisplayInfo();
                var layers = OverlayLayer.GetLayers(info.FrameNumber, contexts.First(), history, extra);
                var renderParams = layers[0].Data.ToString();
                var debugInfo = alignParams + "\n\n" + renderParams;
                hybrid = hybrid.Subtitle(debugInfo.Replace("\n", "\\n"), lsp: 0, size: 14);
            }
            if (Debug || Preview)
                RenderPreview(ref hybrid, contexts.First(), history, extra);
            
            return hybrid[info.FrameNumber];
        }

        protected virtual dynamic RenderFrame(List<OverlayInfo> history, List<OverlayInfo>[] extra)
        {
            var outClips = contexts.Select(ctx => RenderFrame(ctx, history, extra));

            var hybrid = contexts.Count == 1 ? outClips.First() : DynamicEnv.CombinePlanes(outClips,
                planes: contexts.Select(p => p.Plane.GetLetter()).Aggregate(string.Concat),
                pixel_type: PixelType);

            var vi = GetVideoInfo();
            if (!vi.IsRGB() && !string.IsNullOrEmpty(Matrix))
            {
                var convertFunction = vi.pixel_type.GetConvertFunction();
                return hybrid.Invoke(convertFunction, matrix: Matrix);
            }
            if (vi.pixel_type != ((Clip)hybrid).GetVideoInfo().pixel_type)
            {
                var convertFunction = vi.pixel_type.GetConvertFunction();
                return hybrid.Invoke(convertFunction);
            }

            return hybrid;
        }

        protected dynamic RenderFrame(
            OverlayContext ctx, 
            List<OverlayInfo> history,
            List<OverlayInfo>[] extra)
        {
            var currentFrame = history.First().FrameNumber;
            var layers = OverlayLayer.GetLayers(currentFrame, ctx, history, extra);
            var primary = layers.First();
            var secondary = layers.Skip(1).First();

            IEnumerable<(OverlayInfo, List<OverlayLayer>)> GetLayerHistory(int length, double deviation) => new[] { -1, 0, 1 }
                .SelectMany(sign => history
                    .Where(p => Math.Abs(p.FrameNumber - currentFrame) <= length)
                    .Where(p => p.FrameNumber.CompareTo(currentFrame) == sign)
                    .Distinct()
                    .OrderBy(p => sign * p.FrameNumber)
                    .Select(p => new
                    {
                        Info = p,
                        Prev = history.FirstOrDefault(f => f.FrameNumber == p.FrameNumber - sign),
                        Layers = OverlayLayer.GetLayers(p.FrameNumber, ctx, history, extra)
                    })
                    .TakeWhile(p => sign == 0 || p.Prev != null &&
                        (sign == 1 && !p.Info.KeyFrame || sign == -1 && !p.Prev.KeyFrame) &&
                        p.Info.NearlyEquals(p.Prev, deviation)))
                .Select(p => (p.Info, p.Layers))
                .OrderBy(p => p.Info.FrameNumber);

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
                    if (intensity <= double.Epsilon || intensity - 1 >= double.Epsilon)
                        return source.Clip;
                    var mask = GetMask(source, reference);

                    if (ColorFramesCount > 0)
                    {
                        double? cornerGradient = GradientColor > 0 ? GradientColor : null;
                        var cache = ColorHistogramCache.GetOrAdd(cacheId, () => new ColorHistogramCache(tuples, ColorBuckets, false, cornerGradient));
                        cache.Shrink(currentFrame - OverlayConst.ENGINE_HISTORY_LENGTH * 3, currentFrame + OverlayConst.ENGINE_HISTORY_LENGTH * 3);
                        foreach (var (nearInfo, nearLayers) in GetLayerHistory(ColorFramesCount, ColorMaxDeviation))
                        {
                            var nearFrame = nearInfo.FrameNumber;
                            var srcLayer = nearLayers[source.Index];
                            var refLayer = nearLayers[reference.Index];
                            var colorMask = GetMask(srcLayer, refLayer);
                            cache.GetOrAdd(nearFrame,
                                Crop(srcLayer.Clip, srcLayer.Rectangle, refLayer.Rectangle),
                                Crop(refLayer.Clip, refLayer.Rectangle, srcLayer.Rectangle),
                                colorMask, colorMask);
                        }
                    }
                    else cacheId = null;

                    var colorMatchSrc = ColorMatchTarget?.ExtractPlane(ctx.Plane)?.Dynamic() ?? source.Clip;
                    return colorMatchSrc.ColorMatch(
                        Crop(reference.Clip, reference.Rectangle, source.Rectangle),
                        Crop(source.Clip, source.Rectangle, reference.Rectangle),
                        mask, mask,
                        cacheId: cacheId, frameBuffer: ColorFramesCount, frameDiff: ColorFramesDiff,
                        bufferedExtrapolation: ColorBufferedExtrapolation,
                        length: ColorBuckets, dither: ColorDither, intensity: intensity, exclude: ColorExclude,
                        gradient: GradientColor, channels: ctx.AdjustChannels, plane: ctx.Plane.GetLetter());
                }
            }

            if (ColorMatchTarget != null)
                return primary.Clip;

            bool IsFullScreen(Rectangle rect) => rect.Location.IsEmpty && rect.Size == ctx.TargetInfo.Size;

            dynamic GetCanvas()
            {
                var size = FullScreen ? ctx.TargetInfo.Size : primary.Union.Size;
                var blank = ctx.BackgroundClip ?? AvsUtils.InitClip(ctx.Source.ConvertBits(BitDepth), ctx.TargetInfo.Width, ctx.TargetInfo.Height, ctx.DefaultColor);

                dynamic Finalize(dynamic background) => size == ctx.TargetInfo.Size
                    ? background
                    : blank.Overlay(background, primary.Union.Location);

                switch (Background)
                {
                    case BackgroundMode.BLANK:
                        return ctx.BlankColor == -1 ? blank : Finalize(AvsUtils.InitClip(ctx.Source.ConvertBits(BitDepth), size.Width, size.Height, ctx.BlankColor));
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

                dynamic LayerMask(EdgeGradient gradientEdge, double opacity) => layer.Clip.LayerMask(
                    layerIndex: layer.Index,
                    canvasWidth: ctx.TargetInfo.Width,
                    canvasHeight: ctx.TargetInfo.Height,
                    gradient: Gradient / ctx.SubSample.Width, // TODO
                    noise: Noise,
                    gradientEdge: gradientEdge, // TODO fix transparent support
                    opacity: opacity,
                    layers: rects,
                    excludedLayers: excludedLayers.ToArray(),
                    seed: currentFrame //+ layer.Index
                );

                var mask = EdgeGradient == EdgeGradient.NONE && layer.Source
                           || (Gradient == 0 || MaskMode && EdgeGradient == EdgeGradient.NONE)
                           && layer.Mask == null && (layer.Opacity.IsNearlyEquals(1) || MaskMode)
                    ? null
                    : LayerMask(EdgeGradient.NONE, layer.Opacity);

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

                    canvasMask = canvasMask.Overlay(layer.Mask.Invert(), layer.Rectangle.Location);
                    activeMask = true;
                }

                if (edgeMaskMode)
                {
                    var edgeLayerMask = LayerMask(EdgeGradient, 1);
                    var mode = layer.Rotation ? "lighten" : "blend";
                    edgeMask = edgeMask.Overlay(layer.WhiteMask, layer.Rectangle.Location, mask: edgeLayerMask, mode: mode);
                }


                //LogFile(mask, nameof(mask));

                hybrid = hybrid.Overlay(
                    layer.Clip,
                    layer.Rectangle.Location,
                    mode: OverlayMode,
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

        protected List<OverlayInfo>[] GetExtraOverlayInfo(int frame)
        {
            var ctx = contexts.First();
            return ExtraClips
                .Select((p, i) => p.GetOverlayInfo(frame).Select(info =>
                        info.ScaleBySource(ctx.SourceInfo.Size, SourceCrop)
                            .CropOverlay(ctx.ExtraClips[i].Info.Size, p.Crop))
                    .ToList())
                .ToArray();
        }

        protected void RenderPreview(ref dynamic hybrid, OverlayContext ctx, List<OverlayInfo> history, List<OverlayInfo>[] extra)
        {
            var previewSize = (Size)(ctx.TargetInfo.Size.AsSpace() / 3).Eval(p => p - p % 2);
            var previewRect = new RectangleF(new Point(ctx.TargetInfo.Width - previewSize.Width - 10, 10), previewSize).Floor();

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
            var extraClips = extra
                .Select(p => p.First())
                .Select(PrepareOverlay);

            dynamic PreviewClip(Size size, int color, double angle = 0)
            {
                var rect = DynamicEnv.BlankClip(width: size.Width, height: size.Height, color: color);
                return angle == 0 ? rect : rect.BilinearRotate(angle);
            }

            dynamic PreviewMask(Size size, double angle = 0) =>
                PreviewClip(size.Eval(p => p - 4), 0).AddBorders(2, 2, 2, 2, color: 0xFFFFFF).BilinearRotate(angle);

            var preview = hybrid
                .Crop(previewRect.Location, previewRect.Right - ctx.TargetInfo.Width, previewRect.Bottom - ctx.TargetInfo.Height)
                .ConvertToRgb24()
                .Overlay(PreviewClip(canvas.Size, 0x0000FF), canvas.Location, mask: PreviewMask(canvas.Size), opacity: 0.75)
                .Overlay(PreviewClip(src.Size, 0xFF0000), src.Location, mask: PreviewMask(src.Size), opacity: 0.75)
                .Overlay(PreviewClip(over.Size, 0x00FF00, info.Angle), over.Location, mask: PreviewMask(over.Size, info.Angle), opacity: 0.75);
            foreach (var extraClip in extraClips.Select((Clip, Index) => new { Clip, Index }))
                preview = preview.Overlay(PreviewClip(
                        extraClip.Clip.Size,
                        ExtraClips[extraClip.Index].Color, 0),
                    extraClip.Clip.Location,
                    mask: PreviewMask(extraClip.Clip.Size, 0),
                    opacity: 0.75);

            hybrid = hybrid.Overlay(preview.ConvertToPlanarRGB().ConvertBits(BitDepth), previewRect.Location);
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
