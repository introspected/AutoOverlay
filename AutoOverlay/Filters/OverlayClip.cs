using System.Collections.Generic;
using AutoOverlay;
using AutoOverlay.AviSynth;
using AvsFilterNet;

[assembly: AvisynthFilterClass(
    typeof(OverlayClip),
    nameof(OverlayClip),
    "cc[mask]c[Crop]c[Opacity]f[ChromaLocation]s[Matrix]s[Minor]b[Color]i[Debug]b",
    OverlayConst.DEFAULT_MT_MODE)]
namespace AutoOverlay
{
    public class OverlayClip : SupportFilter
    {
        [AvsArgument(Required = true)] 
        public Clip Clip { get; set; }

        [AvsArgument]
        public Clip Mask { get; private set; }

        [AvsArgument]
        public RectangleD Crop { get; private set; }

        [AvsArgument(Min = 0, Max = 1)]
        public double Opacity { get; private set; } = 1;

        [AvsArgument]
        public ChromaLocation? ChromaLocation { get; private set; }

        [AvsArgument]
        public string Matrix { get; private set; }

        [AvsArgument]
        public bool Minor { get; private set; }

        [AvsArgument]
        public int Color { get; private set; } = 0x808080;

        [AvsArgument]
        public override bool Debug { get; protected set; }

        public OverlayEngineFrame GetOverlayInfo(int frameNumber)
        {
            using var frame = Child.GetFrame(frameNumber, StaticEnv);
            return OverlayEngineFrame.FromFrame(frame);
        }
    }
}
