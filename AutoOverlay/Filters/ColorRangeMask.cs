using System.Threading.Tasks;
using AutoOverlay;
using AvsFilterNet;

[assembly: AvisynthFilterClass(typeof(ColorRangeMask), nameof(ColorRangeMask), "c[low]i[high]i", OverlayUtils.DEFAULT_MT_MODE)]
namespace AutoOverlay
{
    public class ColorRangeMask : AvisynthFilter
    {
        private int low, high;
        private int max;
        private int width, height;
        private bool realPlanar;
        private bool hdr;

        public override void Initialize(AVSValue args, ScriptEnvironment env)
        {
            var vi = Child.GetVideoInfo();
            low = args[1].AsInt(0);
            high = args[2].AsInt((1 << vi.pixel_type.GetBitDepth()) - 1);
            width = vi.width* (vi.IsRGB() ? 3 : 1);
            height = vi.height;
            realPlanar = Child.IsRealPlanar();
            hdr = vi.pixel_type.GetBitDepth() > 8;
        }

        public override VideoFrame GetFrame(int n, ScriptEnvironment env)
        {
            var resFrame = NewVideoFrame(env);
            if (realPlanar)
                OverlayUtils.ResetChroma(resFrame);
            using var srcFrame = base.GetFrame(n, env);
            unsafe
            {
                var srcStride = srcFrame.GetPitch();
                var destStride = resFrame.GetPitch();
                if (hdr)
                {
                    var src = (ushort*) srcFrame.GetReadPtr();
                    var dest = (ushort*) resFrame.GetWritePtr();

                    Parallel.For(0, height, y =>
                    {
                        var srcData = src + y * srcStride;
                        var destData = dest + y * destStride;
                        for (var x = 0; x < width; x++)
                        {
                            var val = srcData[x];
                            destData[x] = val >= low && val <= high ? ushort.MaxValue : ushort.MinValue;
                        }
                    });
                }
                else
                {
                    var src = (byte*) srcFrame.GetReadPtr();
                    var dest = (byte*) resFrame.GetWritePtr();

                    Parallel.For(0, height, y =>
                    {
                        var srcData = src + y * srcStride;
                        var destData = dest + y * destStride;
                        for (var x = 0; x < width; x++)
                        {
                            var val = srcData[x];
                            destData[x] = val >= low && val <= high ? byte.MaxValue : byte.MinValue;
                        }
                    });
                }
            }

            return resFrame;
        }
    }
}
