using AutoOverlay.Filters;
using AutoOverlay;
using AutoOverlay.AviSynth;
using AvsFilterNet;

[assembly: AvisynthFilterClass(
    typeof(ColorMatchStep), nameof(ColorMatchStep),
    "[Sample]s[Reference]s[Space]s[Intensity]f[Merge]c[Weight]f[ChromaWeight]f[Channels]s[Length]i[Dither]f[Gradient]f[FrameBuffer]i[FrameDiff]f[Exclude]f[Debug]b",
    OverlayConst.DEFAULT_MT_MODE)]
namespace AutoOverlay
{
    public class ColorMatchStep : SupportFilter
    {
        [AvsArgument]
        public string Sample { get; set; }

        [AvsArgument]
        public string Reference { get; set; }

        [AvsArgument]
        public string Space { get; set; } 

        [AvsArgument(Min = 0, Max = 1)]
        public double Intensity { get; set; } = 1;

        [AvsArgument]
        public ColorMatchStep Merge { get; set; }

        [AvsArgument(Min = 0, Max = 1)]
        public double Weight { get; set; } = 1;

        [AvsArgument(Min = 0, Max = 1)]
        public double ChromaWeight { get; set; } = 1;

        [AvsArgument]
        public string Channels { get; set; }

        [AvsArgument(Min = 100, Max = 1000000)]
        public int Length { get; set; } = -1;

        [AvsArgument(Min = 0, Max = 1)]
        public double Dither { get; private set; } = -1;

        [AvsArgument(Min = 0, Max = 1000000)]
        public double Gradient { get; set; } = -1;

        [AvsArgument(Min = 0, Max = OverlayConst.ENGINE_HISTORY_LENGTH)]
        public int FrameBuffer { get; set; } = -1;

        [AvsArgument(Min = 0, Max = 100)]
        public double FrameDiff { get; set; } = -1;

        [AvsArgument(Min = 0, Max = 1)]
        public double Exclude { get; set; } = -1;

        [AvsArgument]
        public override bool Debug { get; protected set; }
    }
}
