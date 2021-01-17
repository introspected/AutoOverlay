using System;
using System.Threading.Tasks;
using AutoOverlay;
using AutoOverlay.AviSynth;
using AvsFilterNet;

[assembly: AvisynthFilterClass(typeof(OverlayMask),
    nameof(OverlayMask),
    "[template]c[width]i[height]i[left]i[top]i[right]i[bottom]i[noise]b[gradient]b[seed]i",
    OverlayUtils.DEFAULT_MT_MODE)]
namespace AutoOverlay
{
    public class OverlayMask : OverlayFilter
    {

        [AvsArgument(Min = 1)]
        public int Width { get; protected set; }

        [AvsArgument(Min = 1)]
        public int Height { get; protected set; }

        [AvsArgument(Min = 0)]
        public  int Left { get; protected set; }

        [AvsArgument(Min = 0)]
        public int Top { get; protected set; }

        [AvsArgument(Min = 0)]
        public int Right { get; protected set; }

        [AvsArgument(Min = 0)]
        public int Bottom { get; protected set; }

        [AvsArgument]
        public bool Noise { get; protected set; }

        [AvsArgument]
        public bool Gradient { get; protected set; }

        [AvsArgument]
        public int Seed { get; protected set; }

        private bool realPlanar, rgb;

        protected override void Initialize(AVSValue args)
        {
            Width = Child?.GetVideoInfo().width ?? args[1].AsInt();
            Height = Child?.GetVideoInfo().height ?? args[2].AsInt();

            Left = args[3].AsInt(Left);
            Top = args[4].AsInt(Top);
            Right = args[5].AsInt(Right);
            Bottom = args[6].AsInt(Bottom);

            Noise = args[7].AsBool(Noise);
            Gradient = args[8].AsBool(Gradient);
            if (!Noise && !Gradient)
                StaticEnv.ThrowError("No gradient, no noise");
            Seed = args[9].AsInt(Seed);

            var vi = GetVideoInfo();
            vi.width = Width;
            vi.height = Height;
            vi.pixel_type = Child?.GetVideoInfo().pixel_type ?? ColorSpaces.CS_BGR24;
            vi.num_frames = Child?.GetVideoInfo().num_frames ?? (Noise ? 1000000 : 1);
            vi.fps_numerator = Child?.GetVideoInfo().fps_numerator ?? 25;
            vi.fps_denominator = Child?.GetVideoInfo().fps_denominator ?? 1;
            SetVideoInfo(ref vi);
            realPlanar = Child == null || Child.IsRealPlanar(); //Y8 is interleaved
            rgb = GetVideoInfo().IsRGB();
        }

        protected override VideoFrame GetFrame(int n)
        {
            var frame = NewVideoFrame(StaticEnv);
            if (realPlanar)
                OverlayUtils.ResetChroma(frame);
            OverlayUtils.MemSet(frame.GetWritePtr(), byte.MaxValue, frame.GetHeight() * frame.GetPitch());
            var stride = frame.GetPitch();
            var pixelSize = frame.GetRowSize() / GetVideoInfo().width;
            var random = new FastRandom(Seed == 0 ? n : Seed);
            unsafe
            {
                void LeftRight(int length, Func<int, int> offset)
                {
                    if (length == 0) return;
                    for (var x = 0; x < length; x++)
                    {
                        var data = (byte*) frame.GetWritePtr() + offset(x) * pixelSize;
                        var gradientVal = GradientVal(x, length);

                        for (var y = 0; y < Height; y++, data += stride)
                        {
                            var val = gradientVal;

                            if (Noise && random.Next(length) > x && random.Next(length) > x)
                                val = 0;
                            if (val != byte.MaxValue)
                                for (var i = 0; i < pixelSize; i++)
                                    data[i] = val;
                        }
                    }
                }

                void TopBottom(int length, Func<int, int> offset)
                {
                    if (length == 0) return;
                    for (var y = 0; y < length; y++)
                    {
                        var data = (byte*) frame.GetWritePtr() + offset(rgb ? (Height - y - 1) : y) * stride;
                        var gradientVal = GradientVal(y, length);

                        for (var x = 0; x < Width; x++, data += pixelSize)
                        {
                            var val = gradientVal;

                            if (Noise && random.Next(length) > y && random.Next(length) > y)
                                val = 0;
                            if (val != byte.MaxValue && data[0] > val)
                                for (var i = 0; i < pixelSize; i++)
                                    data[i] = val;
                        }
                    }
                }
                Parallel.Invoke(() => LeftRight(Left, x => x), () => LeftRight(Right, x => Width - x - 1));
                Parallel.Invoke(() => TopBottom(Top, y => y), () => TopBottom(Bottom, y => Height - y - 1));
            }
            return frame;
        }

        private byte GradientVal(int current, int total)
        {
            return !Gradient ? byte.MaxValue : (byte) (255 * ((current + 1.0) / (total + 1)));
        }
    }
}
