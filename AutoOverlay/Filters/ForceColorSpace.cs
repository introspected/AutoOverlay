using System;
using AutoOverlay;
using AvsFilterNet;

[assembly: AvisynthFilterClass(
    typeof(ForceColorSpace), nameof(ForceColorSpace),
    "cs[width]i[height]i",
    OverlayConst.DEFAULT_MT_MODE)]
namespace AutoOverlay
{
    public class ForceColorSpace : AvisynthFilter
    {
        public override void Initialize(AVSValue args, ScriptEnvironment env)
        {
            var vi = GetVideoInfo();
            vi.pixel_type = args[1].AsString().ParseColorSpace();
            vi.width = args[2].AsInt(vi.width);
            vi.height = args[3].AsInt(vi.height);
            SetVideoInfo(ref vi);
        }
    }
}
