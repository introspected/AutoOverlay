using System;
using System.Drawing;
using System.Threading.Tasks;
using AutoOverlay;
using AvsFilterNet;

[assembly: AvisynthFilterClass(typeof(StaticOverlayRender),
    nameof(StaticOverlayRender),
    "cc[X]i[Y]i[Angle]f[OverlayWidth]i[OverlayHeight]i[CropLeft]f[CropTop]f[CropRight]f[CropBottom]f[Diff]f" +
    "[SourceMask]c[OverlayMask]c[OverlayMode]s[Width]i[Height]i[PixelType]s[Gradient]i[Noise]i[DynamicNoise]b[BorderOffset]c[SrcColorBorderOffset]c[OverColorBorderOffset]c" +
    "[Mode]i[Opacity]f[ColorAdjust]f[AdjustChannels]s[Matrix]s[Upsize]s[Downsize]s[Rotate]s[SIMD]b[Debug]b[Invert]b[Extrapolation]b[BlankColor]i[Background]f[BackBlur]i",
    OverlayUtils.DEFAULT_MT_MODE)]
namespace AutoOverlay
{
    public class StaticOverlayRender : OverlayRender
    {
        [AvsArgument(Required = true)]
        public override Clip Source { get; protected set; }

        [AvsArgument(Required = true)]
        public override Clip Overlay { get; protected set; }

        [AvsArgument(Required = true)]
        public int X { get; private set; }

        [AvsArgument(Required = true)]
        public int Y { get; private set; }

        [AvsArgument(Min = -360, Max = 360)]
        public double Angle { get; private set; }

        [AvsArgument(Min = 1)]
        public int OverlayWidth { get; private set; }

        [AvsArgument(Min = 1)]
        public int OverlayHeight { get; private set; }

        [AvsArgument(Min = 0)]
        public double CropLeft { get; private set; }

        [AvsArgument(Min = 0)]
        public double CropTop { get; private set; }

        [AvsArgument(Min = 0)]
        public double CropRight { get; private set; }

        [AvsArgument(Min = 0)]
        public double CropBottom { get; private set; }

        [AvsArgument]
        public double Diff { get; private set; }

        [AvsArgument]
        public override Clip SourceMask { get; protected set; }

        [AvsArgument]
        public override Clip OverlayMask { get; protected set; }

        [AvsArgument]
        public override string OverlayMode { get; protected set; } = "blend";

        [AvsArgument(Min = 0)]
        public override int Width { get; protected set; }

        [AvsArgument(Min = 0)]
        public override int Height { get; protected set; }

        [AvsArgument]
        public override string PixelType { get; protected set; }

        [AvsArgument(Min = 0)]
        public override int Gradient { get; protected set; }

        [AvsArgument(Min = 0)]
        public override int Noise { get; protected set; }

        [AvsArgument(Min = 0)]
        public override bool DynamicNoise { get; protected set; } = true;

        [AvsArgument(Min = 0)]
        public override Rectangle BorderOffset { get; protected set; } = Rectangle.Empty;

        [AvsArgument(Min = 0)]
        public override Rectangle SrcColorBorderOffset { get; protected set; } = Rectangle.Empty;

        [AvsArgument(Min = 0)]
        public override Rectangle OverColorBorderOffset { get; protected set; } = Rectangle.Empty;

        [AvsArgument(Min = 0)]
        public override FramingMode Mode { get; protected set; } = FramingMode.Fit;

        [AvsArgument(Min = 0, Max = 1)]
        public override double Opacity { get; protected set; } = 1;

        [AvsArgument(Min = -1, Max = 1)]
        public override double ColorAdjust { get; protected set; } = -1;

        [AvsArgument]
        public override string AdjustChannels { get; protected set; }

        [AvsArgument]
        public override string Matrix { get; protected set; }

        [AvsArgument]
        public override string Upsize { get; protected set; } = OverlayUtils.DEFAULT_RESIZE_FUNCTION;

        [AvsArgument]
        public override string Downsize { get; protected set; } = OverlayUtils.DEFAULT_RESIZE_FUNCTION;

        [AvsArgument]
        public override string Rotate { get; protected set; } = "BilinearRotate";

        [AvsArgument]
        public override bool SIMD { get; protected set; } = true;

        [AvsArgument]
        public override bool Debug { get; protected set; }

        [AvsArgument]
        public override bool Invert { get; protected set; }

        [AvsArgument]
        public override bool Extrapolation { get; protected set; } = true;

        [AvsArgument]
        public override int BlankColor { get; protected set; } = -1;

        [AvsArgument(Min = -1, Max = 1)]
        public override double Background { get; protected set; } = 0;

        [AvsArgument(Min = 0, Max = 100)]
        public override int BackBlur { get; protected set; } = 15;

        private OverlayInfo overlaySettings;

        protected override void Initialize(AVSValue args)
        {
            base.Initialize(args);
            var srcInfo = Source.GetVideoInfo();
            var overInfo = Overlay.GetVideoInfo();
            overlaySettings = new OverlayInfo
            {
                X = X,
                Y = Y,
                Angle = (int) Math.Round(Angle*100),
                Width = OverlayWidth == 0 ? overInfo.width : OverlayWidth,
                Height = OverlayHeight == 0 ? overInfo.height : OverlayHeight,
                Diff = Diff,
                BaseWidth = overInfo.width,
                BaseHeight = overInfo.height,
                SourceWidth = srcInfo.width,
                SourceHeight = srcInfo.height
            };
            overlaySettings.SetCrop(RectangleF.FromLTRB(
                (float) CropLeft,
                (float) CropTop,
                (float) CropRight,
                (float) CropBottom));
        }

        protected override OverlayInfo GetOverlayInfo(int n)
        {
            var info = overlaySettings.Clone();
            info.FrameNumber = n;
            return info;
        }
    }
}
