﻿using System.Collections.Generic;
using System.Drawing;
using AutoOverlay;
using AutoOverlay.AviSynth;
using AutoOverlay.Overlay;
using AvsFilterNet;

[assembly: AvisynthFilterClass(typeof(DynamicOverlayRender),
    nameof(OverlayRender),
    "ccc[SourceMask]c[OverlayMask]c[ExtraClips]c[Preset]s" +
    "[InnerBounds]c[OuterBounds]c[OverlayBalanceX]f[OverlayBalanceY]f[FixedSource]b[OverlayOrder]i" +
    "[OverlayMode]s[Width]i[Height]i[PixelType]s[Gradient]i[Noise]i[DynamicNoise]b" +
    "[BorderControl]i[BorderMaxDeviation]f[BorderOffset]c[SrcColorBorderOffset]c[OverColorBorderOffset]c" +
    "[MaskMode]b[Opacity]f[ColorAdjust]f[ColorBuckets]i[ColorInterpolation]s[ColorExclude]f[ColorFramesCount]i[ColorFramesDiff]f" +
    "[ColorMaxDeviation]f[AdjustChannels]s[Matrix]s[Upsize]s[Downsize]s[Rotate]s[SIMD]b[Debug]b[Invert]b" +
    "[Extrapolation]b[Background]s[BackgroundClip]c[BlankColor]i" +
    "[BackBalance]f[BackBlur]i[FullScreen]b[EdgeGradient]s[BitDepth]i",
    OverlayUtils.DEFAULT_MT_MODE)]
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

        [AvsArgument]
        public override bool DynamicNoise { get; protected set; } = true;

        [AvsArgument(Min = 0, Max = OverlayUtils.ENGINE_HISTORY_LENGTH)]
        public override int BorderControl { get; protected set; } = 0;

        [AvsArgument(Min = 0)]
        public override double BorderMaxDeviation { get; protected set; } = 0.5;

        [AvsArgument(Min = 0)]
        public override Rectangle BorderOffset { get; protected set; } = Rectangle.Empty;

        [AvsArgument(Min = 0)]
        public override Rectangle SrcColorBorderOffset { get; protected set; } = Rectangle.Empty;

        [AvsArgument(Min = 0)]
        public override Rectangle OverColorBorderOffset { get; protected set; } = Rectangle.Empty;

        [AvsArgument]
        public override bool MaskMode { get; protected set; }

        [AvsArgument(Min = 0, Max = 1)]
        public override double Opacity { get; protected set; } = 1;

        [AvsArgument(Min = -1, Max = 1)]
        public override double ColorAdjust { get; protected set; } = -1;

        [AvsArgument(Min = 3, Max = 1000000)]
        public override int ColorBuckets { get; protected set; } = 1024;

        [AvsArgument]
        public override ColorInterpolation ColorInterpolation { get; protected set; } = ColorInterpolation.Linear;

        [AvsArgument(Min = 0, Max = 1)]
        public override double ColorExclude { get; protected set; } = 0;

        [AvsArgument(Min = 0, Max = OverlayUtils.ENGINE_HISTORY_LENGTH)]
        public override int ColorFramesCount { get; protected set; } = 0;

        [AvsArgument(Min = 0)]
        public override double ColorFramesDiff { get; protected set; } = 1;

        [AvsArgument(Min = 0)]
        public override double ColorMaxDeviation { get; protected set; } = 0.5;

        [AvsArgument]
        public override string AdjustChannels { get; protected set; }

        [AvsArgument]
        public override string Matrix { get; protected set; }

        [AvsArgument]
        public override string Upsize { get; protected set; }

        [AvsArgument]
        public override string Downsize { get; protected set; }

        [AvsArgument]
        public override string Rotate { get; protected set; } = "BilinearRotate";

        [AvsArgument]
        public override bool SIMD { get; protected set; } = true;

        [AvsArgument]
        public override bool Debug { get; protected set; }

        [AvsArgument]
        public override bool Invert { get; protected set; }

        [AvsArgument]
        public override bool Extrapolation { get; protected set; } = false;

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
            using var frame = Child.GetFrame(n, StaticEnv);
            return OverlayInfo.FromFrame(frame);
        }
    }
}
