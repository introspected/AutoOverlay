using System.Threading.Tasks;
using AutoOverlay;
using AvsFilterNet;

[assembly: AvisynthFilterClass(typeof(ColorRangeMask), nameof(ColorRangeMask), "c[low]i[high]i[greyMask]b", OverlayUtils.DEFAULT_MT_MODE)]
namespace AutoOverlay
{
    public class ColorRangeMask : AvisynthFilter
    {
        private int low, high;
        private int max;
        private bool realPlanar;
        private bool hdr;
        private bool greyMask;

        public override void Initialize(AVSValue args, ScriptEnvironment env)
        {
            var vi = Child.GetVideoInfo();
            low = args[1].AsInt(0);
            high = args[2].AsInt((1 << vi.pixel_type.GetBitDepth()) - 1);
            realPlanar = Child.IsRealPlanar();
            hdr = vi.pixel_type.GetBitDepth() > 8;
            greyMask = args[3].AsBool(true);
        }

        public override VideoFrame GetFrame(int n, ScriptEnvironment env)
        {
            var resFrame = NewVideoFrame(env);
            if (realPlanar && greyMask)
                OverlayUtils.ResetChroma(resFrame);
            using var srcFrame = base.GetFrame(n, env);
            unsafe
            {
                var planes = greyMask ? new[] {default(YUVPlanes)} : OverlayUtils.GetPlanes(GetVideoInfo().pixel_type);
                Parallel.ForEach(planes, plane =>
                {
                    var srcStride = srcFrame.GetPitch(plane);
                    var destStride = resFrame.GetPitch(plane);
                    var width = resFrame.GetRowSize(plane) / (hdr ? 2 : 1);
                    var height = resFrame.GetHeight(plane);
                    if (hdr)
                    {
                        var src = (ushort*) srcFrame.GetReadPtr(plane);
                        var dest = (ushort*) resFrame.GetWritePtr(plane);

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
                        var src = (byte*) srcFrame.GetReadPtr(plane);
                        var dest = (byte*) resFrame.GetWritePtr(plane);

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
                });
            }

            return resFrame;
        }
    }
}
