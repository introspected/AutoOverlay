using System;
using AutoOverlay;
using AvsFilterNet;

[assembly: AvisynthFilterClass(
    typeof(ForceColorSpace), nameof(ForceColorSpace),
    "cs[width]i[height]i",
    OverlayUtils.DEFAULT_MT_MODE)]
namespace AutoOverlay
{
    public class ForceColorSpace : AvisynthFilter
    {
        public override void Initialize(AVSValue args, ScriptEnvironment env)
        {
            var vi = GetVideoInfo();
            if (!Enum.TryParse("CS_" + args[1].AsString(), true, out vi.pixel_type))
                throw new AvisynthException("Wrong color space");
            vi.width = args[2].AsInt(vi.width);
            vi.height = args[3].AsInt(vi.height);
            SetVideoInfo(ref vi);
        }
    }
}
