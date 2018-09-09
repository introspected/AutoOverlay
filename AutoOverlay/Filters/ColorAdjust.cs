using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoOverlay;
using AvsFilterNet;

[assembly: AvisynthFilterClass(
    typeof(ColorAdjust), nameof(ColorAdjust),
    "c[Sample]c[Reference]c[SampleMask]c[ReferenceMask]c[LimitedRange]b[Channels]s[Dither]f",
    MtMode.NICE_FILTER)]
namespace AutoOverlay
{
    public class ColorAdjust : OverlayFilter
    {
        [AvsArgument(Required = true)]
        public Clip Sample { get; private set; }

        [AvsArgument(Required = true)]
        public Clip Reference { get; private set; }

        [AvsArgument]
        public Clip SampleMask { get; private set; }

        [AvsArgument]
        public Clip ReferenceMask { get; private set; }

        [AvsArgument]
        public bool LimitedRange { get; set; } = true;

        [AvsArgument]
        public string Channels { get; set; }

        [AvsArgument(Min = 0, Max = 1)]
        public double Dither { get; set; } = 0.95;
        
        private const int tr = 0;
        private YUVPlanes[] planes;
        private int[] realChannels;
        private int sampleBits, referenceBits;

        protected override void Initialize(AVSValue args)
        {
            LimitedRange = LimitedRange && GetVideoInfo().IsPlanar();
            planes = GetVideoInfo().IsRGB()
                ? new[] {default(YUVPlanes)}
                : (Channels ?? "yuv").ToCharArray().Select(p => Enum.Parse(typeof(YUVPlanes), "PLANAR_" + p, true))
                    .Cast<YUVPlanes>().ToArray();
            realChannels = GetVideoInfo().IsPlanar()
                ? new[] {0}
                : (Channels ?? "rgb").ToLower().ToCharArray().Select(p => "bgr".IndexOf(p)).ToArray();
            if (!OverlayUtils.IsRealPlanar(Child))
                planes = new[] { default(YUVPlanes) };
            var vi = GetVideoInfo();
            var refVi = Reference.GetVideoInfo();
            sampleBits = vi.pixel_type.GetBitDepth();
            referenceBits = refVi.pixel_type.GetBitDepth();
            vi.pixel_type = vi.pixel_type.ChangeBitDepth(referenceBits);
            SetVideoInfo(ref vi);
        }

        private int GetLowColor(int bits)
        {
            if (!LimitedRange)
                return 0;
            return 16 << (bits - 8);
        }

        private int GetHighColor(int bits, YUVPlanes plane)
        {
            if (!LimitedRange)
                return (1 << bits) - 1;
            var sdr = plane == YUVPlanes.PLANAR_U || plane == YUVPlanes.PLANAR_V ? 240 : 235;
            return sdr << (bits - 8);
        }

        protected override VideoFrame GetFrame(int n)
        {
            var input = Child.GetFrame(n, StaticEnv);
            var sampleFrames = Enumerable.Range(n - tr, tr * 2 + 1).Select(p => Sample.GetFrame(p, StaticEnv)).ToList();
            var referenceFrames = Enumerable.Range(n - tr, tr * 2 + 1).Select(p => Reference.GetFrame(p, StaticEnv)).ToList();
            var writable = GetVideoInfo().pixel_type == Child.GetVideoInfo().pixel_type && StaticEnv.MakeWritable(input);
            var output = writable ? input : NewVideoFrame(StaticEnv);
            using (var sampleMaskFrame = SampleMask?.GetFrame(n, StaticEnv))
            using (var refMaskFrame = ReferenceMask?.GetFrame(n, StaticEnv))
            {
                var pixelSize = Sample.GetVideoInfo().IsRGB() ? 3 : 1;
                Parallel.ForEach(planes, plane =>
                {
                    Parallel.ForEach(realChannels, channel =>
                    {
                        int[] sampleHist = null, referenceHist = null;
                        Parallel.Invoke(
                            () => sampleHist = GetHistogram(sampleFrames, sampleMaskFrame, pixelSize, channel, plane, Sample.GetVideoInfo().pixel_type, sampleBits, false),
                            () => referenceHist = GetHistogram(referenceFrames, refMaskFrame, pixelSize, channel, plane, Reference.GetVideoInfo().pixel_type, referenceBits, LimitedRange));
                        
                        var map = GetTransitionMap(sampleHist, referenceHist, n, plane);

                        var tuple = map.GetColorsAndWeights();

                        NativeUtils.ApplyColorMap(
                            input.GetReadPtr(plane), input.GetPitch(plane), sampleBits > 8,
                            output.GetWritePtr(plane), output.GetPitch(plane), referenceBits > 8,
                            input.GetRowSize(plane), input.GetHeight(plane), pixelSize, channel,
                            map.fixedMap, tuple.Item1, tuple.Item2);
                    });
                });
            }
            if (!writable)
                input.Dispose();
            sampleFrames.ForEach(p => p.Dispose());
            referenceFrames.ForEach(p => p.Dispose());
            return output;
        }

        private ColorMap GetTransitionMap(int[] sampleHist, int[] referenceHist, int n, YUVPlanes plane)
        {
            var map = new ColorMap(sampleBits, n, Dither);
            var highRefColor = GetHighColor(referenceBits, plane);

            for (int newColor = GetLowColor(referenceBits), oldColor = -1, lastOldColor = -1, lastNewColor = GetLowColor(referenceBits) - 1, restPixels = 0; newColor <= highRefColor; newColor++)
            {
                void MissedColors(double newColorLimit, double oldColorLimit)
                {
                    var step = (newColorLimit - lastNewColor - 1) / (oldColorLimit - lastOldColor);

                    for (var tempColor = lastOldColor + 1; tempColor < oldColorLimit; tempColor++)
                    {
                        var actualColor = lastNewColor + step * (tempColor - lastOldColor);
                        var intergerColor = Math.Truncate(actualColor);
                        var val = 1 - (actualColor - intergerColor);
                        if (tempColor == highRefColor)
                            val = 1;
                        map.Add(tempColor, (int) intergerColor, val);
                        if (val <= 1 - double.Epsilon)
                            map.Add(tempColor, (int) intergerColor + 1, 1 - val);
                    }
                }

                var refCount = referenceHist[newColor];
                var notEmpty = refCount > 0;
                while (refCount > 0)
                {
                    var add = Math.Min(restPixels, refCount);
                    if (add > 0)
                    {
                        map.Add(oldColor, newColor, add / (double) sampleHist[oldColor]);
                        refCount -= add;
                        restPixels -= add;
                        lastOldColor = oldColor;
                    }
                    else
                    {
                        restPixels = sampleHist[++oldColor];
                        if (restPixels != 0 && oldColor - lastOldColor > 1)
                            MissedColors(newColor, oldColor);
                    }
                }
                if (notEmpty)
                    lastNewColor = newColor;
                if (newColor == highRefColor)
                    MissedColors(highRefColor + 1, 1 << sampleBits);
            }
            return map;
        }

        private int[] GetUniHistogram(int[] hist)
        {
            var uni = new int[hist.Length];
            var total = hist.Sum();
            var newTotal = int.MaxValue;
            for (var color = 0; color < hist.Length; color++)
            {
                var old = hist[color];
                var mult = newTotal / (double) total;
                total -= old;
                var expanded = (int) Math.Round(old * mult);
                if (total == 0)
                    expanded = newTotal;
                newTotal -= expanded;
                uni[color] = expanded;
            }
            if (uni.Sum() != int.MaxValue)
                throw new InvalidOperationException();
            return uni;
        }

        private int[] GetHistogram(IEnumerable<VideoFrame> frames, VideoFrame maskFrame, int pixelSize, int channel, YUVPlanes plane, ColorSpaces pixelType, int bits, bool limitedRange)
        {
            var hist = new int[1 << bits];
            var chroma = plane == YUVPlanes.PLANAR_U || plane == YUVPlanes.PLANAR_V;
            var widthMult = chroma ? pixelType.GetWidthSubsample() : 1;
            var heightMult = chroma ? pixelType.GetHeightSubsample() : 1;
            var maskPitch = maskFrame?.GetPitch() * heightMult ?? 0;
            var maskPtr = maskFrame?.GetReadPtr() + channel ?? IntPtr.Zero;
            foreach (var frame in frames)
            {
                NativeUtils.FillHistogram(hist,
                    frame.GetRowSize(plane), frame.GetHeight(plane), channel,
                    frame.GetReadPtr(plane), frame.GetPitch(plane), pixelSize,
                    maskPtr, maskPitch, widthMult);
            }
            if (limitedRange)
            {
                var min = GetLowColor(bits);
                for (var color = 0; color < min ; color++)
                {
                    hist[min] += hist[color];
                    hist[color] = 0;
                }
                var max = GetHighColor(bits, plane);
                for (var color = max + 1; color < 1 << bits; color++)
                {
                    hist[max] += hist[color];
                    hist[color] = 0;
                }
            }
            return GetUniHistogram(hist);
        }

        class ColorMap
        {
            public readonly int[] fixedMap;
            private readonly Dictionary<int, double>[] dynamicMap;
            private readonly double limit;
            private readonly XorshiftRandom random;
            private bool ditherAnyway;
            private bool fastDither;

            public ColorMap(int bits, int seed, double limit)
            {
                var depth = 1 << bits;
                fixedMap = new int[depth];
                dynamicMap = new Dictionary<int, double>[depth];
                for (var i = 0; i < dynamicMap.Length; i++)
                {
                    fixedMap[i] = -1;
                    dynamicMap[i] = new Dictionary<int, double>();
                }
                random = new XorshiftRandom(seed);
                this.limit = limit;
                fastDither = limit > 0.5;
                ditherAnyway = limit > 1 - double.Epsilon;
            }

            public void Add(int oldColor, int newColor, double weight)
            {
                if (fastDither && weight >= limit)
                {
                    fixedMap[oldColor] = newColor;
                    return;
                }
                if (fixedMap[oldColor] >= 0)
                    return;
                var map = dynamicMap[oldColor];
                map[newColor] = weight;
                if (!ditherAnyway)
                {
                    var max = map.Max(p => p.Value);
                    var rest = 1 - map.Values.Sum();
                    if (rest < max && max > limit)
                        fixedMap[oldColor] = newColor;
                }
            }

            public int Next(int color)
            {
                var fixedColor = fixedMap[color];
                if (fixedColor >= 0)
                    return fixedColor;
                var val = random.NextDouble();
                var map = dynamicMap[color];
                foreach (var pair in map)
                    if ((val -= pair.Value) < double.Epsilon)
                        return pair.Key;
                throw new InvalidOperationException();
            }

            public Tuple<int[][], double[][]> GetColorsAndWeights()
            {
                var length = dynamicMap.Length;
                var colorMap = new int[length][];
                var weightMap = new double[length][];
                for (var color = 0; color < length; color++)
                {
                    if (fixedMap[color] >= 0) continue;
                    var map = dynamicMap[color];
                    var colors = colorMap[color] = new int[map.Count];
                    var weights = weightMap[color] = new double[map.Count];
                    var i = 0;
                    foreach (var pair in map)
                    {
                        colors[i] = pair.Key;
                        weights[i++] = pair.Value;
                    }
                }
                return Tuple.Create(colorMap, weightMap);
            }
        }
    }
}
