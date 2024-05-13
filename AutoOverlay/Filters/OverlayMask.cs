using System;
using System.Linq;
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

        private const double FLOAT_MAX = float.MaxValue * 2.0;
        private bool rgb;
        private ushort shortMaxValue;
        private ColorSpaces pixelType;
        private Clip background;
        private PlaneChannel[] planeChannels;

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
            pixelType = vi.pixel_type = Child?.GetVideoInfo().pixel_type ?? ColorSpaces.CS_BGR24;
            vi.num_frames = Child?.GetVideoInfo().num_frames ?? (Noise ? 1000000 : 1);
            vi.fps_numerator = Child?.GetVideoInfo().fps_numerator ?? 25;
            vi.fps_denominator = Child?.GetVideoInfo().fps_denominator ?? 1;
            SetVideoInfo(ref vi);
            planeChannels = vi.IsPlanar()
                ? vi.pixel_type.GetPlaneChannels()
                    .Where(p => p.EffectivePlane != YUVPlanes.PLANAR_U && p.EffectivePlane != YUVPlanes.PLANAR_V)
                    .ToArray()
                :
                [
                    new PlaneChannel(default, default, 0,
                        pixelType.HasFlag(ColorSpaces.CS_RGBA_TYPE) ? 4 :
                        pixelType.HasFlag(ColorSpaces.CS_RGB_TYPE) ? 3 : 1, pixelType.GetBitDepth())
                ];
            rgb = GetVideoInfo().IsRGB();
            shortMaxValue = (ushort)(255 << (pixelType.GetBitDepth() - 8));
        }

        protected override void AfterInitialize()
        {
            background = rgb
                ? DynamicEnv.BlankClip(width: Width, height: Height, pixel_type: pixelType.GetName(), color: 0xFFFFFF)
                : DynamicEnv.BlankClip(width: Width, height: Height, pixel_type: pixelType.GetName(), color_yuv: 0xFF8080);
        }

        protected override VideoFrame GetFrame(int n)
        {
            var frame = background.GetFrame(n, StaticEnv);
            StaticEnv.MakeWritable(frame);
            Parallel.ForEach(planeChannels, planeChannel =>
            {
                var framePlane = new FramePlane(planeChannel, frame, false);
                var stride = framePlane.stride;
                var pixelSize = framePlane.pixelSize;
                unsafe
                {
                    void LeftRightByte(int length, Func<int, int> offset, int seed)
                    {
                        if (length == 0) return;
                        var random = new FastRandom(seed);
                        for (var x = 0; x < length; x++)
                        {
                            var data = (byte*)framePlane.pointer + offset(x) * pixelSize;
                            var gradientVal = GradientValByte(x, length);

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

                    void TopBottomByte(int length, Func<int, int> offset, int seed)
                    {
                        if (length == 0) return;
                        var random = new FastRandom(seed);
                        for (var y = 0; y < length; y++)
                        {
                            var data = (byte*)framePlane.pointer + offset(rgb ? (Height - y - 1) : y) * stride;
                            var gradientVal = GradientValByte(y, length);

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

                    void LeftRightShort(int length, Func<int, int> offset, int seed)
                    {
                        if (length == 0) return;
                        var random = new FastRandom(seed);
                        for (var x = 0; x < length; x++)
                        {
                            var data = (ushort*)framePlane.pointer + offset(x) * pixelSize;
                            var gradientVal = GradientValShort(x, length);

                            for (var y = 0; y < Height; y++, data += stride)
                            {
                                var val = gradientVal;

                                if (Noise && random.Next(length) > x && random.Next(length) > x)
                                    val = 0;
                                if (val != shortMaxValue)
                                    for (var i = 0; i < pixelSize; i++)
                                        data[i] = val;
                            }
                        }
                    }

                    void TopBottomShort(int length, Func<int, int> offset, int seed)
                    {
                        if (length == 0) return;
                        var random = new FastRandom(seed);
                        for (var y = 0; y < length; y++)
                        {
                            var data = (ushort*)framePlane.pointer +offset(rgb ? (Height - y - 1) : y) * stride;
                            var gradientVal = GradientValShort(y, length);

                            for (var x = 0; x < Width; x++, data += pixelSize)
                            {
                                var val = gradientVal;

                                if (Noise && random.Next(length) > y && random.Next(length) > y)
                                    val = 0;
                                if (val != shortMaxValue && data[0] > val)
                                    for (var i = 0; i < pixelSize; i++)
                                        data[i] = val;
                            }
                        }
                    }

                    void LeftRightFloat(int length, Func<int, int> offset, int seed)
                    {
                        if (length == 0) return;
                        var random = new FastRandom(seed);
                        for (var x = 0; x < length; x++)
                        {
                            var data = (float*)framePlane.pointer + offset(x) * pixelSize;
                            var gradientVal = GradientValFloat(x, length);

                            for (var y = 0; y < Height; y++, data += stride)
                            {
                                var val = gradientVal;

                                if (Noise && random.Next(length) > x && random.Next(length) > x)
                                    val = 0;
                                if (Math.Abs(val - float.MaxValue) > float.Epsilon)
                                    for (var i = 0; i < pixelSize; i++)
                                        data[i] = val;
                            }
                        }
                    }

                    void TopBottomFloat(int length, Func<int, int> offset, int seed)
                    {
                        if (length == 0) return;
                        var random = new FastRandom(seed);
                        for (var y = 0; y < length; y++)
                        {
                            var data = (float*)framePlane.pointer + offset(rgb ? (Height - y - 1) : y) * stride;
                            var gradientVal = GradientValFloat(y, length);

                            for (var x = 0; x < Width; x++, data += pixelSize)
                            {
                                var val = gradientVal;

                                if (Noise && random.Next(length) > y && random.Next(length) > y)
                                    val = 0;
                                if (Math.Abs(val - float.MaxValue) > float.Epsilon && data[0] > val)
                                    for (var i = 0; i < pixelSize; i++)
                                    {
                                        data[i] = val;
                                    }
                            }
                        }
                    }

                    var seed = Seed == 0 ? n : Seed;
                    //seed += (int)planeChannel.EffectivePlane;
                    switch (framePlane.byteDepth)
                    {
                        case 1:
                            Parallel.Invoke(() => LeftRightByte(Left, x => x, seed << 0), () => LeftRightByte(Right, x => Width - x - 1, seed << 1));
                            Parallel.Invoke(() => TopBottomByte(Top, y => y, seed << 2), () => TopBottomByte(Bottom, y => Height - y - 1, seed << 3));
                            break;
                        case 2:
                            Parallel.Invoke(() => LeftRightShort(Left, x => x, seed << 0), () => LeftRightShort(Right, x => Width - x - 1, seed << 1));
                            Parallel.Invoke(() => TopBottomShort(Top, y => y, seed << 2), () => TopBottomShort(Bottom, y => Height - y - 1, seed << 3));
                            break;
                        case 4:
                            Parallel.Invoke(() => LeftRightFloat(Left, x => x, seed << 0), () => LeftRightFloat(Right, x => Width - x - 1, seed << 1));
                            Parallel.Invoke(() => TopBottomFloat(Top, y => y, seed << 2), () => TopBottomFloat(Bottom, y => Height - y - 1, seed << 3));
                            break;
                    }
                }
            });
            return frame;
        }

        private byte GradientValByte(int current, int total)
        {
            return !Gradient ? byte.MaxValue : (byte) (255 * ((current + 1.0) / (total + 1)));
        }

        private ushort GradientValShort(int current, int total)
        {
            return !Gradient ? shortMaxValue : (ushort) (shortMaxValue * ((current + 1.0) / (total + 1)));
        }

        private float GradientValFloat(int current, int total)
        {
            return !Gradient ? float.MaxValue : (float)(FLOAT_MAX * ((current + 1.0) / (total + 1)) - float.MaxValue);
        }
    }
}
