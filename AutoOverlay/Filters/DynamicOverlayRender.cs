using System.Collections.Generic;
using System.Drawing;
using AutoOverlay;
using AutoOverlay.AviSynth;
using AvsFilterNet;

[assembly: AvisynthFilterClass(typeof(DynamicOverlayRender),
    nameof(OverlayRender),
    "ccc[SourceMask]c[OverlayMask]c[SourceCrop]c[OverlayCrop]c[SourceChromaLocation]s[OverlayChromaLocation]s" +
    "[ExtraClips]c[Preset]s[InnerBounds]c[OuterBounds]c[OverlayBalanceX]f[OverlayBalanceY]f[FixedSource]b[OverlayOrder]i" +
    "[StabilizationDiffTolerance]f[StabilizationAreaTolerance]f[StabilizationLength]i" +
    "[OverlayMode]s[Width]i[Height]i[PixelType]s[Gradient]i[Noise]i" +
    "[BorderControl]i[BorderMaxDeviation]f[BorderOffset]c[SrcColorBorderOffset]c[OverColorBorderOffset]c" +
    "[MaskMode]b[Opacity]f[ColorAdjust]f[ColorBuckets]i[ColorDither]f[ColorExclude]i[ColorFramesCount]i[ColorFramesDiff]f" +
    "[ColorMaxDeviation]f[ColorBufferedExtrapolation]b[GradientColor]f[ColorMatchTarget]c[AdjustChannels]s[Matrix]s" +
    "[SourceMatrix]s[OverlayMatrix]s[Upsize]s[Downsize]s[ChromaResize]s[Rotate]s[Preview]b[Debug]b[Invert]b" +
    "[Background]s[BackgroundClip]c[BlankColor]i[BackBalance]f[BackBlur]i[FullScreen]b[EdgeGradient]s[BitDepth]i",
    OverlayConst.DEFAULT_MT_MODE)]
namespace AutoOverlay
{
    public class DynamicOverlayRender : OverlayRender
    {
        [AvsArgument(Required = true)]
        public Clip Engine { get; private set; }

        [AvsArgument(Required = true)]
        public override Clip Source { get; protected set; }
        
        [AvsArgument(Required = true)]
        public override Clip Overlay { get; protected set; }

        [AvsArgument]
        public override Clip SourceMask { get; protected set; }

        [AvsArgument]
        public override Clip OverlayMask { get; protected set; }

        [AvsArgument]
        public override RectangleD SourceCrop { get; protected set; }

        [AvsArgument]
        public override RectangleD OverlayCrop { get; protected set; }

        [AvsArgument]
        public override ChromaLocation? SourceChromaLocation { get; protected set; }

        [AvsArgument]
        public override ChromaLocation? OverlayChromaLocation { get; protected set; }

        [AvsArgument]
        public override OverlayClip[] ExtraClips { get; protected set; }

        [AvsArgument]
        public override OverlayRenderPreset Preset { get; protected set; }

        [AvsArgument(Min = 0)]
        public override RectangleD InnerBounds { get; protected set; }

        [AvsArgument(Min = 0)]
        public override RectangleD OuterBounds { get; protected set; }

        [AvsArgument(Min = -1, Max = 1)]
        public override Space OverlayBalance { get; set; }

        [AvsArgument]
        public override bool FixedSource { get; protected set; }

        [AvsArgument(Min = 0)]
        public override int OverlayOrder { get; protected set; }

        [AvsArgument(Min = 0)]
        public override double StabilizationDiffTolerance { get; protected set; } = 200;

        [AvsArgument(Min = 0)]
        public override double StabilizationAreaTolerance { get; protected set; } = 1.5;

        [AvsArgument(Min = 0, Max = OverlayConst.ENGINE_HISTORY_LENGTH)]
        public override int StabilizationLength { get; protected set; }

        [AvsArgument]
        public override string OverlayMode { get; protected set; } = "blend";

        [AvsArgument(Min = 1)]
        public override int Width { get; protected set; }

        [AvsArgument(Min = 1)]
        public override int Height { get; protected set; }

        [AvsArgument]
        public override string PixelType { get; protected set; }

        [AvsArgument(Min = 0)]
        public override int Gradient { get; protected set; }

        [AvsArgument(Min = 0)]
        public override int Noise { get; protected set; }

        [AvsArgument(Min = 0, Max = OverlayConst.ENGINE_HISTORY_LENGTH, Unused = true)]
        public override int BorderControl { get; protected set; } = 0;

        [AvsArgument(Min = 0, Unused = true)]
        public override double BorderMaxDeviation { get; protected set; } = 0.5;

        [AvsArgument(Min = 0, Unused = true)]
        public override Rectangle BorderOffset { get; protected set; } = Rectangle.Empty;

        [AvsArgument(Min = 0, Unused = true)]
        public override Rectangle SrcColorBorderOffset { get; protected set; } = Rectangle.Empty;

        [AvsArgument(Min = 0, Unused = true)]
        public override Rectangle OverColorBorderOffset { get; protected set; } = Rectangle.Empty;

        [AvsArgument]
        public override bool MaskMode { get; protected set; }

        [AvsArgument(Min = 0, Max = 1)]
        public override double Opacity { get; protected set; } = 1;

        [AvsArgument(Min = -1, Max = 1)]
        public override double ColorAdjust { get; protected set; } = -1;

        [AvsArgument(Min = 10, Max = 1000000)]
        public override int ColorBuckets { get; protected set; } = OverlayConst.COLOR_BUCKETS_COUNT;

        [AvsArgument(Min = 0, Max = 1)]
        public override double ColorDither { get; protected set; } = OverlayConst.COLOR_DITHER;

        [AvsArgument(Min = 0, Max = 100)]
        public override int ColorExclude { get; protected set; } = 0;

        [AvsArgument(Min = 0, Max = OverlayConst.ENGINE_HISTORY_LENGTH)]
        public override int ColorFramesCount { get; protected set; } = 0;

        [AvsArgument(Min = 0)]
        public override double ColorFramesDiff { get; protected set; } = 0.1;

        [AvsArgument(Min = 0)]
        public override double ColorMaxDeviation { get; protected set; } = 0.5;

        [AvsArgument]
        public override bool ColorBufferedExtrapolation { get; protected set; } = true;

        [AvsArgument(Min = 0, Max = 1000000)]
        public override double GradientColor { get; protected set; }

        [AvsArgument]
        public override Clip ColorMatchTarget { get; protected set; }

        [AvsArgument]
        public override string AdjustChannels { get; protected set; }

        [AvsArgument]
        public override string Matrix { get; protected set; }

        [AvsArgument]
        public override string SourceMatrix { get; protected set; }

        [AvsArgument]
        public override string OverlayMatrix { get; protected set; }

        [AvsArgument]
        public override string Upsize { get; protected set; }

        [AvsArgument]
        public override string Downsize { get; protected set; }

        [AvsArgument]
        public override string ChromaResize { get; protected set; }

        [AvsArgument]
        public override string Rotate { get; protected set; } = "BilinearRotate";

        [AvsArgument]
        public override bool Preview { get; protected set; }

        [AvsArgument]
        public override bool Debug { get; protected set; }

        [AvsArgument]
        public override bool Invert { get; protected set; }

        [AvsArgument]
        public override BackgroundMode Background { get; protected set; } = BackgroundMode.BLANK;

        [AvsArgument]
        public override Clip BackgroundClip { get; protected set; }

        [AvsArgument]
        public override int BlankColor { get; protected set; } = -1;

        [AvsArgument(Min = -1, Max = 1)]
        public override double BackBalance { get; protected set; } = 0;

        [AvsArgument(Min = 0, Max = 100)]
        public override int BackBlur { get; protected set; } = 15;

        [AvsArgument]
        public override bool FullScreen { get; protected set; }

        [AvsArgument]
        public override EdgeGradient EdgeGradient { get; protected set; } = EdgeGradient.NONE;

        [AvsArgument(Min = 8, Max = 16)]
        public override int BitDepth { get; protected set; }

        protected override List<OverlayInfo> GetOverlayInfo(int n)
        {
            using var frame = Engine.GetFrame(n, StaticEnv);
            return OverlayInfo.FromFrame(frame);
        }
    }
}
