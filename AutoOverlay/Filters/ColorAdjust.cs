using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoOverlay;
using AutoOverlay.Filters;
using AvsFilterNet;
using MathNet.Numerics.Interpolation;

[assembly: AvisynthFilterClass(
    typeof(ColorAdjust), nameof(ColorAdjust),
    "c[Sample]c[Reference]c[SampleMask]c[ReferenceMask]c[Intensity]f[LimitedRange]b" +
    "[Channels]s[Dither]f[Exclude]f[Interpolation]s[Extrapolation]b[SIMD]b[Debug]b",
    OverlayUtils.DEFAULT_MT_MODE)]
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

        [AvsArgument(Min = 0, Max = 1)]
        public double Intensity { get; set; } = 1;

        [AvsArgument]
        public bool LimitedRange { get; set; } = true;

        [AvsArgument]
        public string Channels { get; set; }

        [AvsArgument(Min = 0, Max = 1)]
        public double Dither { get; set; } = 0.95;

        [AvsArgument(Min = 0, Max = 1)]
        public double Exclude { get; set; } = 0;

        [AvsArgument]
        public ColorInterpolation Interpolation { get; set; } = ColorInterpolation.Spline;

        [AvsArgument]
        public bool Extrapolation { get; protected set; } = true;

        [AvsArgument]
        public bool SIMD { get; private set; } = true;

        [AvsArgument]
        public override bool Debug { get; protected set; }
        
        private const int tr = 0;
        private YUVPlanes[] planes;
        private int[] realChannels;
        private int sampleBits, referenceBits;

        protected override void Initialize(AVSValue args)
        {
            LimitedRange = LimitedRange && GetVideoInfo().IsPlanar();
            planes = GetVideoInfo().pixel_type.HasFlag(ColorSpaces.CS_INTERLEAVED)
                ? new[] {default(YUVPlanes)}
                : (Channels ?? "yuv").ToCharArray().Select(p => Enum.Parse(typeof(YUVPlanes), "PLANAR_" + p, true))
                    .Cast<YUVPlanes>().ToArray();
            realChannels = GetVideoInfo().IsPlanar()
                ? new[] {0}
                : (Channels ?? "rgb").ToLower().ToCharArray().Select(p => "bgr".IndexOf(p)).ToArray();
            if (!OverlayUtils.IsRealPlanar(Child))
                planes = new[] { default(YUVPlanes) };
            sampleBits = Sample.GetVideoInfo().pixel_type.GetBitDepth();
            referenceBits = Reference.GetVideoInfo().pixel_type.GetBitDepth();
            var vi = GetVideoInfo();
            vi.pixel_type = vi.pixel_type.ChangeBitDepth(referenceBits);
            SetVideoInfo(ref vi);
            var cacheSize = tr * 2 + 1;
            var cacheKey = StaticEnv.GetEnv2() == null ? CacheType.CACHE_25_ALL : CacheType.CACHE_GENERIC;
            Child.SetCacheHints(cacheKey, cacheSize);
            Sample.SetCacheHints(cacheKey, cacheSize);
            Reference.SetCacheHints(cacheKey, cacheSize);
            SampleMask?.SetCacheHints(cacheKey, cacheSize);
            ReferenceMask?.SetCacheHints(cacheKey, cacheSize);
            if (Intensity < 1 && sampleBits != referenceBits)
                throw new AvisynthException("Intensity < 1 is not allowed when sample and reference bit depth are not equal");
        }

        public int GetLowColor(int bits)
        {
            if (!LimitedRange)
                return 0;
            return 16 << (bits - 8);
        }

        public int GetHighColor(int bits, YUVPlanes plane)
        {
            if (!LimitedRange)
                return (1 << bits) - 1;
            var sdr = plane == YUVPlanes.PLANAR_U || plane == YUVPlanes.PLANAR_V ? 240 : 235;
            return sdr << (bits - 8);
        }

        protected override VideoFrame GetFrame(int n)
        {
            var input = Child.GetFrame(n, StaticEnv);
            if (Intensity < double.Epsilon)
                return input;
            var sampleFrames = Enumerable.Range(n - tr, tr * 2 + 1).Select(p => Sample.GetFrame(p, StaticEnv)).ToList();
            var referenceFrames = Enumerable.Range(n - tr, tr * 2 + 1).Select(p => Reference.GetFrame(p, StaticEnv)).ToList();
            var inputFrames = tr == 0
                ? new List<VideoFrame> {input}
                : Enumerable.Range(n - tr, tr * 2 + 1).Select(p => Child.GetFrame(p, StaticEnv)).ToList();
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
                        int[] sampleHist = null, referenceHist = null, srcHist = null;

                        Parallel.Invoke(
                            () => sampleHist = GetHistogram(sampleFrames, sampleMaskFrame, pixelSize, channel, plane, Sample.GetVideoInfo().pixel_type, sampleBits, false),
                            () => referenceHist = GetHistogram(referenceFrames, refMaskFrame, pixelSize, channel, plane, Reference.GetVideoInfo().pixel_type, referenceBits, LimitedRange),
                            () => srcHist = Extrapolation ? GetHistogram(inputFrames, null, pixelSize, channel, plane, Child.GetVideoInfo().pixel_type, sampleBits, LimitedRange) : null);

                        var map = GetTransitionMap(sampleHist, referenceHist, n, plane);

                        if (Extrapolation)
                            Extrapolate(map, srcHist, GetLowColor(referenceBits), GetHighColor(referenceBits, plane));

                        Interpolate(map, GetLowColor(referenceBits), GetHighColor(referenceBits, plane));

                        if (Intensity < 1)
                        {
                            var decreased = new ColorMap(sampleBits, n, Dither);
                            for (var color = 0; color < 1 << sampleBits; color++)
                            {
                                decreased.Add(color, map.Average(color) * Intensity + color * (1 - Intensity));
                            }
                            map = decreased;
                        }

                        var tuple = map.GetColorsAndWeights();

                        NativeUtils.ApplyColorMap(
                            input.GetReadPtr(plane), input.GetPitch(plane), sampleBits > 8,
                            output.GetWritePtr(plane), output.GetPitch(plane), referenceBits > 8,
                            input.GetRowSize(plane), input.GetHeight(plane), pixelSize, channel,
                            map.FixedMap, tuple.Item1, tuple.Item2);
                    });
                });
            }

            if (!writable)
                input.Dispose();
            sampleFrames.ForEach(p => p.Dispose());
            referenceFrames.ForEach(p => p.Dispose()) ;
            return output;
        }

        private void Extrapolate(ColorMap map, int[] srcHist, int minColor, int maxColor, int limit = 17770)
        {
            var min = srcHist.TakeWhile(p => p == 0).Count();
            var max = srcHist.Length - srcHist.Reverse().TakeWhile(p => p == 0).Count() - 1;

            var first = map.First();
            var last = map.Last();

            var sampleCount = Math.Min(limit, last - first + 1);

            if (min < first)
            {
                var mappedColors = Enumerable.Range(first, sampleCount).Where(map.Contains).ToList();
                var avgDiff = mappedColors.Sum(p => map.Average(p) - p) / mappedColors.Count;
                var mapped = min + avgDiff;
                if (mapped > minColor)
                {
                    map.Add(min, mapped);
                    Log(() => $"Min: {min} -> {mapped:F3}");
                }
            }

            if (max > last)
            {
                var mappedColors = Enumerable.Range(last - sampleCount + 1, sampleCount).Where(map.Contains).ToList();
                var avgDiff = mappedColors.Sum(p => map.Average(p) - p) / mappedColors.Count;
                var mapped = max + avgDiff;
                if (mapped < maxColor)
                {
                    map.Add(max, mapped);
                    Log(() => $"Max: {max} -> {mapped:F3}");
                }
            }
        }

        private void Interpolate(ColorMap map, int min, int max)
        {
            var interpolator = GetInterpolator(map);

            if (interpolator == null) return;

            var firstOldColor = map.First();
            var lastOldColor = map.Last();
            for (var oldColor = 0; oldColor < map.FixedMap.Length; oldColor++)
            {
                if (oldColor < firstOldColor || oldColor > lastOldColor)
                    map.Add(oldColor, oldColor);
                else if (!map.Contains(oldColor))
                {
                    var interpolated = interpolator.Interpolate(oldColor);
                    interpolated = Math.Min(max, Math.Max(min, interpolated));
                    map.Add(oldColor, interpolated);
                }
            }
        }

        private IInterpolation GetInterpolator(ColorMap map)
        {
            var size = map.FixedMap.Length;
            var points = new List<Tuple<int, double>>(map.FixedMap.Length);
            for (var oldColor = 0; oldColor < size; oldColor++)
            {
                var fixedColor = map.FixedMap[oldColor];
                if (fixedColor >= 0)
                {
                    points.Add(Tuple.Create(oldColor, (double) fixedColor));
                }
                else if (map.DynamicMap[oldColor].Any())
                {
                    points.Add(Tuple.Create(oldColor, map.DynamicMap[oldColor].Sum(pair => pair.Key * pair.Value)));
                }
            }
            var xValues = new double[points.Count];
            var yValues = new double[points.Count];
            for (var i = 0; i < points.Count; i++)
            {
                var point = points[i];
                xValues[i] = point.Item1;
                yValues[i] = point.Item2;
            }

            if (xValues.Length < 2)
                return null;
            return GetInterpolator(Interpolation, xValues, yValues);
        }

        private static IInterpolation GetInterpolator(ColorInterpolation interpolation, double[] x, double[] y)
        {
            switch (interpolation)
            {
                case ColorInterpolation.Akima:
                    return CubicSpline.InterpolateAkimaSorted(x, y);
                case ColorInterpolation.Spline:
                    return CubicSpline.InterpolateNaturalSorted(x, y);
                case ColorInterpolation.Linear:
                    return LinearSpline.InterpolateSorted(x, y);
                default:
                    throw new ArgumentException("interpolation");
            }
        }

        private ColorMap GetTransitionMap(int[] sampleHist, int[] referenceHist, int n, YUVPlanes plane)
        {
            var map = new ColorMap(sampleBits, n, Intensity < 1 ? 1 : Dither);
            var highRefColor = GetHighColor(referenceBits, plane);

            for (int newColor = GetLowColor(referenceBits), oldColor = -1, restPixels = 0; newColor <= highRefColor; newColor++)
            {
                var refCount = referenceHist[newColor];
                while (refCount > 0)
                {
                    var add = Math.Min(restPixels, refCount);
                    if (add > 0)
                    {
                        var weight = add / (double) sampleHist[oldColor];
                        map.Add(oldColor, newColor, weight);
                        refCount -= add;
                        restPixels -= add;
                    }
                    else
                    {
                        restPixels = sampleHist[++oldColor];
                    }
                }
            }
            return map;
        }

        private int[] GetUniHistogram(uint[] hist)
        {
            var uni = new int[hist.Length];
            var total = (uint) hist.Cast<int>().Sum();
            var newRest = int.MaxValue;
            for (var color = 0; color < hist.Length; color++)
            {
                if (hist[color] / (double) total < Exclude)
                {
                    total -= hist[color];
                    hist[color] = 0;
                }
            }
            var mult = int.MaxValue / (double) total;
            var rest = total;
            var max = new
            {
                Color = -1,
                Count = 0
            };
            for (var color = 0; color < hist.Length; color++)
            {
                var old = hist[color];
                rest -= old;
                var expanded = (int) Math.Round(old * mult);
                //if (total == 0)
                //    expanded = newTotal;
                newRest -= expanded;
                uni[color] = expanded;
                if (expanded > max.Count)
                {
                    max = new
                    {
                        Color = color,
                        Count = expanded
                    };
                }
            }
            uni[max.Color] += newRest;
            if (uni.Sum() != int.MaxValue)
                throw new InvalidOperationException();
            return uni;
        }

        private int[] GetHistogram(IEnumerable<VideoFrame> frames, VideoFrame maskFrame, int pixelSize, int channel, YUVPlanes plane, ColorSpaces pixelType, int bits, bool limitedRange)
        {
            var hist = new uint[1 << bits];
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
                    maskPtr, maskPitch, widthMult, SIMD);
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
    }
}
