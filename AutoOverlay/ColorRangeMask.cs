using AutoOverlay;
using AvsFilterNet;

[assembly: AvisynthFilterClass(typeof(ColorRangeMask), nameof(ColorRangeMask), "c[low]i[high]i", MtMode.NICE_FILTER)]
namespace AutoOverlay
{
    public class ColorRangeMask : AvisynthFilter
    {
        private int low, high;
        private int width, height;
        private bool realPlanar;

        public override void Initialize(AVSValue args, ScriptEnvironment env)
        {
            low = args[1].AsInt(byte.MinValue);
            high = args[2].AsInt(byte.MaxValue);
            width = GetVideoInfo().width;
            height = GetVideoInfo().height;
            realPlanar = OverlayUtils.IsRealPlanar(Child);
        }

        public override VideoFrame GetFrame(int n, ScriptEnvironment env)
        {
            var resFrame = NewVideoFrame(env);
            if (realPlanar)
                OverlayUtils.ResetChroma(resFrame);
            using (var srcFrame = base.GetFrame(n, env))
            {
                unsafe
                {
                    var src = (byte*) srcFrame.GetReadPtr();
                    var srcStride = srcFrame.GetPitch();
                    var dest = (byte*) resFrame.GetWritePtr();
                    var destStride = resFrame.GetPitch();
                    var rowSize = resFrame.GetRowSize();
                    
                    for (var y = 0; y < height; y++, src += srcStride, dest += destStride)
                    {
                        for (var x = 0; x < rowSize; x++)
                        {
                            var val = src[x];
                            dest[x] = val >= low && val <= high ? byte.MaxValue : byte.MinValue;
                        }
                    }
                }
            }
            return resFrame;
        }
    }
}
