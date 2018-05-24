using System;
using AutoOverlay;
using AvsFilterNet;

[assembly: AvisynthFilterClass(typeof(OverlayMask),
    nameof(OverlayMask),
    "[template]c[width]i[height]i[left]i[top]i[right]i[bottom]i[noise]b[gradient]b[seed]i",
    MtMode.NICE_FILTER)]
namespace AutoOverlay
{
    public class OverlayMask : AvisynthFilter
    {
        private int width, height;
        private int left, top, right, bottom;
        private bool noise;
        private bool gradient;
        private bool realPlanar, rgb;
        private int seed = int.MaxValue;

        public override void Initialize(AVSValue args, ScriptEnvironment env)
        {
            width = Child?.GetVideoInfo().width ?? args[1].AsInt();
            height = Child?.GetVideoInfo().height ?? args[2].AsInt();

            left = args[3].AsInt(left);
            top = args[4].AsInt(top);
            right = args[5].AsInt(right);
            bottom = args[6].AsInt(bottom);

            noise = args[7].AsBool(noise);
            gradient = args[8].AsBool(gradient);
            if (!noise && !gradient)
                env.ThrowError("No gradient, no noise");
            seed = args[9].AsInt(seed);

            var vi = GetVideoInfo();
            vi.width = width;
            vi.height = height;
            vi.pixel_type = Child?.GetVideoInfo().pixel_type ?? ColorSpaces.CS_BGR24;
            vi.num_frames = Child?.GetVideoInfo().num_frames ?? 1000;
            vi.fps_numerator = Child?.GetVideoInfo().fps_numerator ?? 25;
            vi.fps_denominator = Child?.GetVideoInfo().fps_denominator ?? 1;
            SetVideoInfo(ref vi);
            realPlanar = Child == null || OverlayUtils.IsRealPlanar(Child); //Y8 is interleaved
            rgb = GetVideoInfo().IsRGB();
        }

        public override VideoFrame GetFrame(int n, ScriptEnvironment env)
        {
            var frame = NewVideoFrame(env);
            if (realPlanar)
                OverlayUtils.ResetChroma(frame);
            OverlayUtils.MemSet(frame.GetWritePtr(), byte.MaxValue, frame.GetHeight() * frame.GetPitch());
            var stride = frame.GetPitch();
            var pixelSize = frame.GetRowSize() / GetVideoInfo().width;
            var random = new Random(seed == int.MaxValue ? n : seed);
            unsafe
            {
                void LeftRight(int length, Func<int, int> offset)
                {
                    if (length > 0)
                    {
                        for (var x = 0; x < length; x++)
                        {
                            var data = (byte*) frame.GetWritePtr() + offset(x) * pixelSize;
                            var gradientVal = GradientVal(x, length);

                            for (var y = 0; y < height; y++, data += stride)
                            {
                                var val = gradientVal;
                                if (noise && random.Next(length + 1) > x && random.Next(length + 1) > x)
                                    val = 0;
                                if (val != byte.MaxValue)
                                    for (var i = 0; i < pixelSize; i++)
                                        data[i] = val;
                            }
                        }
                    }
                }

                void TopBottom(int length, Func<int, int> offset)
                {
                    if (length > 0)
                    {
                        for (var y = 0; y < length; y++)
                        {
                            var data = (byte*) frame.GetWritePtr() + offset(rgb ? (height - y - 1) : y) * stride;
                            var gradientVal = GradientVal(y, length);

                            for (var x = 0; x < width; x++, data += pixelSize)
                            {
                                var val = gradientVal;
                                if (noise && random.Next(length + 1) > y && random.Next(length + 1) > y)
                                    val = 0;
                                if (val != byte.MaxValue && data[0] > val)
                                    for (var i = 0; i < pixelSize; i++)
                                        data[i] = val;
                            }
                        }
                    }
                }
                LeftRight(left, x => x);
                LeftRight(right, x => width - x - 1);
                TopBottom(top, y => y);
                TopBottom(bottom, y => height - y - 1);
            }
            return frame;
        }

        private byte GradientVal(int current, int total)
        {
            return !gradient ? byte.MaxValue : (byte) (255 * ((current + 1.0) / (total + 2)));
        }
    }
}
