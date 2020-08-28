using System.Drawing;
using System.Threading.Tasks;
using AutoOverlay;
using AvsFilterNet;

[assembly: AvisynthFilterClass(typeof(DynamicOverlayRender),
    nameof(OverlayRender),
    "ccc[SourceMask]c[OverlayMask]c" +
    "[OverlayMode]s[Width]i[Height]i[PixelType]s[Gradient]i[Noise]i[DynamicNoise]b[BorderOffset]c[SrcColorBorderOffset]c[OverColorBorderOffset]c" +
    "[Mode]i[Opacity]f[ColorAdjust]f[AdjustChannels]s[Matrix]s[Upsize]s[Downsize]s[Rotate]s[SIMD]b[Debug]b[Invert]b[Extrapolation]b[BlankColor]i" +
    "[Background]f[BackBlur]i",
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

        [AvsArgument(Min = 0)]
        public override Rectangle BorderOffset { get; protected set; } = Rectangle.Empty;

        [AvsArgument(Min = 0)]
        public override Rectangle SrcColorBorderOffset { get; protected set; } = Rectangle.Empty;

        [AvsArgument(Min = 0)]
        public override Rectangle OverColorBorderOffset { get; protected set; } = Rectangle.Empty;

        [AvsArgument]
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

        protected override OverlayInfo GetOverlayInfo(int n)
        {
            using (var infoFrame = Child.GetFrame(n, StaticEnv))
                return OverlayInfo.FromFrame(infoFrame);
        }
    }
}
