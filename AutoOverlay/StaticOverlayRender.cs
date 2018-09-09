using System;
using System.Drawing;
using AutoOverlay;
using AvsFilterNet;

[assembly: AvisynthFilterClass(typeof(StaticOverlayRender),
    nameof(StaticOverlayRender),
    "cc[X]i[Y]i[Angle]f[OverlayWidth]i[OverlayHeight]i[CropLeft]f[CropTop]f[CropRight]f[CropBottom]f[Diff]f" +
    "[SourceMask]c[OverlayMask]c[LumaOnly]b[Width]i[Height]i[Gradient]i[Noise]i[DynamicNoise]b" +
    "[Mode]i[Opacity]f[ColorAdjust]i[Matrix]s[Upsize]s[Downsize]s[Rotate]s[Debug]b",
    MtMode.SERIALIZED)]
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

        [AvsArgument(Min = 0, Max = 1)]
        public double CropLeft { get; private set; }

        [AvsArgument(Min = 0, Max = 1)]
        public double CropTop { get; private set; }

        [AvsArgument(Min = 0, Max = 1)]
        public double CropRight { get; private set; }

        [AvsArgument(Min = 0, Max = 1)]
        public double CropBottom { get; private set; }

        [AvsArgument(Min = 0)]
        public double Diff { get; private set; }

        [AvsArgument]
        public override Clip SourceMask { get; protected set; }

        [AvsArgument]
        public override Clip OverlayMask { get; protected set; }

        [AvsArgument]
        public override bool LumaOnly { get; protected set; }

        [AvsArgument(Min = 0)]
        public override int Width { get; protected set; }

        [AvsArgument(Min = 0)]
        public override int Height { get; protected set; }

        [AvsArgument(Min = 0)]
        public override int Gradient { get; protected set; }

        [AvsArgument(Min = 0)]
        public override int Noise { get; protected set; }

        [AvsArgument(Min = 0)]
        public override bool DynamicNoise { get; protected set; } = true;

        [AvsArgument(Min = 0)]
        public override OverlayMode Mode { get; protected set; } = OverlayMode.Fit;

        [AvsArgument(Min = 0, Max = 1)]
        public override double Opacity { get; protected set; } = 1;

        [AvsArgument]
        public override ColorAdjustMode ColorAdjust { get; protected set; } = ColorAdjustMode.None;

        [AvsArgument]
        public override string Matrix { get; protected set; }

        [AvsArgument]
        public override string Upsize { get; protected set; } = "BicubicResize";

        [AvsArgument]
        public override string Downsize { get; protected set; } = "BicubicResize";

        [AvsArgument]
        public override string Rotate { get; protected set; } = "BilinearRotate";

        [AvsArgument]
        public override bool Debug { get; protected set; }
        
        private OverlayInfo overlaySettings;

        protected override void Initialize(AVSValue args)
        {
            base.Initialize(args);
            overlaySettings = new OverlayInfo
            {
                X = X,
                Y = Y,
                Angle = (int) Math.Round(Angle*100),
                Width = OverlayWidth,
                Height = OverlayHeight,
                Diff = Diff
            };
            overlaySettings.SetCrop(RectangleF.FromLTRB(
                (float) CropLeft,
                (float) CropTop,
                (float) CropRight,
                (float) CropBottom));
        }

        protected override VideoFrame GetFrame(int n)
        {
            overlaySettings.FrameNumber = n;
            var hybrid = RenderFrame(overlaySettings);
            if (Debug)
                return hybrid.Subtitle(overlaySettings.ToString().Replace("\n", "\\n"), lsp: 0)[n];
            return hybrid[n];
        }
    }
}
