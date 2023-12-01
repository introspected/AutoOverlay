using System.Collections.Generic;
using AutoOverlay;
using AutoOverlay.AviSynth;
using AvsFilterNet;

[assembly: AvisynthFilterClass(
    typeof(OverlayClip),
    nameof(OverlayClip),
    "cc[mask]c[Opacity]f[Debug]b",
    OverlayUtils.DEFAULT_MT_MODE)]
namespace AutoOverlay
{
    public class OverlayClip : SupportFilter
    {
        [AvsArgument(Required = true)] 
        public Clip Clip { get; set; }

        [AvsArgument]
        public Clip Mask { get; private set; }

        [AvsArgument(Min = 0, Max = 1)]
        public double Opacity { get; private set; } = 1;

        [AvsArgument]
        public override bool Debug { get; protected set; }

        public List<OverlayInfo> GetOverlayInfo(int frameNumber) =>
            OverlayInfo.FromFrame(Child.GetFrame(frameNumber, StaticEnv));
    }
}
