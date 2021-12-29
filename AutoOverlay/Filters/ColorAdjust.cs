using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoOverlay;
using AutoOverlay.AviSynth;
using AutoOverlay.Histogram;
using AvsFilterNet;
using MathNet.Numerics.Interpolation;

[assembly: AvisynthFilterClass(
    typeof(ColorAdjust), nameof(ColorAdjust),
    "c[Sample]c[Reference]c[SampleMask]c[ReferenceMask]c[GreyMask]b[Intensity]f[Seed]i" +
    "[AdjacentFramesCount]i[AdjacentFramesDiff]f[LimitedRange]b[Channels]s[Dither]f[Exclude]f" +
    "[Interpolation]s[Extrapolation]b[DynamicNoise]b[SIMD]b[Threads]i[Debug]b[CacheId]s",
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

        [AvsArgument]
        public bool GreyMask { get; protected set; } = true;

        [AvsArgument(Min = 0, Max = 1)]
        public double Intensity { get; set; } = 1;

        [AvsArgument]
        public int Seed { get; set; }

        [AvsArgument(Min = 0, Max = OverlayUtils.ENGINE_HISTORY_LENGTH)]
        public int AdjacentFramesCount { get; set; } = 0;

        [AvsArgument(Min = 0)]
        public double AdjacentFramesDiff { get; set; } = 1;

        [AvsArgument]
        public bool LimitedRange { get; set; } = true;

        [AvsArgument]
        public string Channels { get; set; }

        [AvsArgument(Min = 0, Max = 1)]
        public double Dither { get; set; } = 0.95;

        [AvsArgument(Min = 0, Max = 1)]
        public double Exclude { get; set; } = 0;

        [AvsArgument]
        public ColorInterpolation Interpolation { get; set; } = ColorInterpolation.Linear;

        [AvsArgument]
        public bool Extrapolation { get; protected set; } = false;

        [AvsArgument]
        public bool DynamicNoise { get; private set; } = true;

        [AvsArgument]
        public bool SIMD { get; private set; } = true;

        [AvsArgument(Min = 0)]
        public int Threads { get; set; } = 0;

        [AvsArgument]
        public override bool Debug { get; protected set; }

        [AvsArgument]
        public string CacheId { get; protected set; }

        private YUVPlanes[] planes;
        private int[] realChannels;
        private int sampleBits, referenceBits;

        private HistogramCache histogramCache;
        private ParallelOptions parallelOptions;

        protected override void Initialize(AVSValue args)
        {
            parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Threads == 0 ? -1 : Threads
            };
            LimitedRange = LimitedRange && GetVideoInfo().IsPlanar();
            planes = GetVideoInfo().pixel_type.HasFlag(ColorSpaces.CS_INTERLEAVED)
                ? new[] {default(YUVPlanes)}
                : (Channels ?? "yuv").ToCharArray().Select(p => Enum.Parse(typeof(YUVPlanes), "PLANAR_" + p, true))
                    .Cast<YUVPlanes>().ToArray();
            realChannels = GetVideoInfo().IsPlanar()
                ? new[] {0}
                : (Channels ?? "rgb").ToLower().ToCharArray().Select(p => "bgr".IndexOf(p)).ToArray();
            if (!Child.IsRealPlanar())
                planes = new[] { default(YUVPlanes) };
            sampleBits = Sample.GetVideoInfo().pixel_type.GetBitDepth();
            referenceBits = Reference.GetVideoInfo().pixel_type.GetBitDepth();
            var vi = GetVideoInfo();
            vi.pixel_type = vi.pixel_type.ChangeBitDepth(referenceBits);
            SetVideoInfo(ref vi);
            var cacheSize = AdjacentFramesCount * 2 + 1;
            var cacheKey = StaticEnv.GetEnv2() == null ? CacheType.CACHE_25_ALL : CacheType.CACHE_GENERIC;
            Child.SetCacheHints(cacheKey, cacheSize);
            Sample.SetCacheHints(cacheKey, cacheSize);
            Reference.SetCacheHints(cacheKey, cacheSize);
            SampleMask?.SetCacheHints(cacheKey, cacheSize);
            ReferenceMask?.SetCacheHints(cacheKey, cacheSize);
            if (Intensity < 1 && sampleBits != referenceBits)
                throw new AvisynthException("Intensity < 1 is not allowed when sample and reference bit depth are not equal");

            histogramCache = !string.IsNullOrEmpty(CacheId)
                ? HistogramCache.Get(CacheId)
                : new HistogramCache(planes, realChannels, SIMD, LimitedRange,
                    Sample.GetVideoInfo().pixel_type, Reference.GetVideoInfo().pixel_type,
                    Child.GetVideoInfo().pixel_type, AdjacentFramesCount, GreyMask, parallelOptions);

            if (histogramCache == null)
                throw new AvisynthException($"$Histogram cache with ID: {CacheId} not found");
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
            if (Intensity <= double.Epsilon)
                return input;
            var writable = GetVideoInfo().pixel_type == Child.GetVideoInfo().pixel_type && StaticEnv.MakeWritable(input);
            var output = writable ? input : NewVideoFrame(StaticEnv);
            var pixelSize = Sample.GetVideoInfo().IsRGB() ? 3 : 1;
            var firstFrame = Math.Max(0, n - AdjacentFramesCount);
            var lastFrame = Math.Min(Child.GetVideoInfo().num_frames - 1, n + AdjacentFramesCount);
            var dimensions = new[]
            {
                Enumerable.Range(n, lastFrame - n + 1),
                Enumerable.Range(firstFrame, n - firstFrame)
            }.SelectMany(range => range.Select(frame =>
            {
                using (new VideoFrameCollector())
                    return string.IsNullOrEmpty(CacheId) || frame == n
                        ? histogramCache.GetFrame(frame,
                            () => Extrapolation ? (frame == n ? input : Child.GetFrame(frame, StaticEnv)) : null,
                            () => Sample.GetFrame(frame, StaticEnv),
                            () => Reference.GetFrame(frame, StaticEnv),
                            () => SampleMask?.GetFrame(frame, StaticEnv),
                            () => ReferenceMask?.GetFrame(frame, StaticEnv))
                        : histogramCache[frame];
            }).TakeWhile(dims =>
            {
                var current = histogramCache[n];
                return dims != null && current.All(pair =>
                    current == dims || !dims[pair.Key].Empty &&
                    CompareHist(dims[pair.Key].DiffHist, pair.Value.DiffHist) < AdjacentFramesDiff);
            }).SelectMany(p => p)).ToList();
            Parallel.ForEach(planes, parallelOptions, plane =>
            {
                Parallel.ForEach(realChannels, parallelOptions, channel =>
                {
                    var currentDimensions = dimensions
                        .Where(p => p.Key.Equal(plane, channel))
                        .Select(p => p.Value).ToArray();
                    var sampleHist = AverageHist(sampleBits, currentDimensions.Select(p => p.SampleHist).ToArray());
                    var referenceHist = AverageHist(referenceBits, currentDimensions.Select(p => p.ReferenceHist).ToArray());

                    if (sampleHist == null || referenceHist == null)
                        return;

                    var map = GetTransitionMap(sampleHist, referenceHist, n, plane);

                    if (Extrapolation)
                    {
                        var srcHist = AverageHist(referenceBits, currentDimensions.Select(p => p.InputHist).ToArray());
                        Extrapolate(map, srcHist, GetLowColor(referenceBits), GetHighColor(referenceBits, plane));
                    }

                    Interpolate(map, GetLowColor(referenceBits), GetHighColor(referenceBits, plane), sampleBits, referenceBits);

                    if (Intensity < 1)
                    {
                        var decreased = new ColorMap(sampleBits, n, Dither);
                        for (var color = 0; color < 1 << sampleBits; color++)
                        {
                            decreased.AddReal(color, map.Average(color) * Intensity + color * (1 - Intensity));
                        }
                        map = decreased;
                    }

                    var tuple = map.GetColorsAndWeights();

                    NativeUtils.ApplyColorMap(DynamicNoise ? Seed^n : 0,
                        input.GetReadPtr(plane), input.GetPitch(plane), sampleBits > 8,
                        output.GetWritePtr(plane), output.GetPitch(plane), referenceBits > 8,
                        input.GetRowSize(plane), input.GetHeight(plane), pixelSize, channel,
                        map.FixedMap, tuple.Item1, tuple.Item2);
                });
            });

            if (!writable)
                input.Dispose();
            return output;
        }

        private int[] AverageHist(int bits, params int[][] histograms)
        {
            if (histograms.Any(p => p == null))
                return null;
            if (histograms.Length == 1)
                return histograms[0];
            var hist = new int[1 << bits];
            var total = 0;
            int max = -1;
            int maxIndex = -1;
            unsafe
            {
                fixed (int* buffer = hist)
                {
                    var length = hist.Length;
                    for (var i = 0; i < length; i++)
                    {
                        var current = buffer[i] = histograms.Sum(hist => hist[i] / histograms.Length);
                        total += current;
                        if (current > max)
                        {
                            max = current;
                            maxIndex = i;
                        }
                    }
                }
            }
            hist[maxIndex] += int.MaxValue - total;
            return hist;
        }

        private double CompareHist(int[] first, int[] second)
        {
            if (first.Length != second.Length)
                throw new ArgumentException();
            var squaredSum = 0.0;
            var length = first.Length;
            var divider = (double) int.MaxValue / first.Length;
            unsafe
            {
                fixed (int* buffer1 = first)
                fixed (int* buffer2 = second)
                {
                    for (var i = 0; i < length; i++)
                    {
                        var diff = (buffer1[i] - buffer2[i]) / divider;
                        squaredSum += diff * diff;
                    }
                }
            }
            squaredSum /= length;
            //var squaredSum = Enumerable.Range(0, first.Length)
            //    .Select(i => first[i] - second[i])
            //    .Select(diff => diff >> 23)
            //    .Sum(diff => Math.Pow(diff, 2)) / first.Length;
            var val = Math.Sqrt(squaredSum);
            Log(() => $"HIST DIFF: {val}");
            return val;
        }

        private void Extrapolate(ColorMap map, int[] srcHist, int minColor, int maxColor)
        {
            var min = srcHist.TakeWhile(p => p == 0).Count();
            var max = srcHist.Length - srcHist.Reverse().TakeWhile(p => p == 0).Count() - 1;

            var first = map.First();
            var last = map.Last();

            var sampleCount = last - first + 1;
            var mappedColors = Enumerable.Range(first, sampleCount).Where(map.Contains);//.Where(p => srcHist[p] > 50);
            var mappedCount = mappedColors.Count();
            var limit = Math.Max(mappedCount / 5, Math.Min(mappedCount, 10));


            if (min < first)
            {
                var avgDiff = mappedColors.Take(limit).Sum(p => map.Average(p) - p) / limit;
                var mapped = min + avgDiff;
                map.AddReal(min, Math.Max(minColor, mapped));
                Log(() => $"Min: {min} -> {mapped:F3}");
            }

            if (max > last)
            {
                var avgDiff = mappedColors.Reverse().Take(limit).Sum(p => map.Average(p) - p) / limit;
                var mapped = max + avgDiff;
                map.AddReal(max, Math.Min(maxColor, mapped));
                Log(() => $"Max: {max} -> {mapped:F3}");
            }
        }

        private void Interpolate(ColorMap map, int min, int max, int srcBits, int refBits)
        {
            var mult = Math.Pow(2, refBits - srcBits);
            var interpolator = GetInterpolator(map, mult);

            if (interpolator == null) return;

            var firstOldColor = map.First();
            var lastOldColor = map.Last();
            for (var oldColor = 0; oldColor < map.FixedMap.Length; oldColor++)
            {
                if (oldColor < firstOldColor || oldColor > lastOldColor)
                    map.AddReal(oldColor, oldColor * mult);
                else if (!map.Contains(oldColor))
                {
                    var interpolated = interpolator.Interpolate(oldColor);
                    interpolated = Math.Min(max, Math.Max(min, interpolated));
                    map.AddReal(oldColor, interpolated);
                }
                else if (map.FixedMap[oldColor] < 0)
                {
                    var m = map.DynamicMap[oldColor];
                    var weights = m.Values.Sum();
                    if (weights < 1 - double.Epsilon)
                    {
                        var interpolated = interpolator.Interpolate(oldColor);
                        interpolated = Math.Min(max, Math.Max(min, interpolated));
                        map.AddReal(oldColor, interpolated, 1 - weights);
                        continue;
                        var coef = 1 / weights;
                        foreach (var color in m.Keys.ToArray())
                        {
                            m[color] *= coef;
                        }
                    }
                }
            }
        }

        private IInterpolation GetInterpolator(ColorMap map, double mult)
        {
            var size = map.FixedMap.Length;
            var points = new List<Tuple<int, double>>(map.FixedMap.Length);
            for (var oldColor = 0; oldColor < size; oldColor++)
            {
                if (map.Contains(oldColor))
                    points.Add(Tuple.Create(oldColor, map.Average(oldColor)));
            }
            var xValues = new double[points.Count];
            var yValues = new double[points.Count];
            for (var i = 0; i < points.Count; i++)
            {
                var point = points[i];
                xValues[i] = point.Item1;
                yValues[i] = point.Item2;
            }

            if (xValues.Length == 1)
                return GetInterpolator(Interpolation, new[] {xValues[0], xValues[0]}, new[] {yValues[0], yValues[0]}, mult);
            return GetInterpolator(Interpolation, xValues, yValues, mult);
        }

        private static IInterpolation GetInterpolator(ColorInterpolation interpolation, double[] x, double[] y, double mult)
        {
            switch (interpolation)
            {
                case ColorInterpolation.Akima:
                    return CubicSpline.InterpolateAkimaSorted(x, y);
                case ColorInterpolation.Spline:
                    return CubicSpline.InterpolateNaturalSorted(x, y);
                case ColorInterpolation.Linear:
                    return LinearSpline.InterpolateSorted(x, y);
                case ColorInterpolation.None:
                    return new NoneInterpolator(mult);
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
                //var excludeReference = (double) refCount / int.MaxValue < Exclude;
                while (refCount > 0)
                {
                    var add = Math.Min(restPixels, refCount);
                    if (add > 0)
                    {
                        var exclude = (((double) add) / int.MaxValue) < Exclude;
                        if (!exclude)
                        {
                            var weight = add / (double) sampleHist[oldColor];
                            map.Add(oldColor, newColor, weight);
                        }
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

        protected override void Dispose(bool A_0)
        {
            base.Dispose(A_0);
            HistogramCache.Dispose(histogramCache.Id);
        }

        private class NoneInterpolator : IInterpolation
        {
            public double Interpolate(double t) => t * mult;

            public double Differentiate(double t) => t * mult;
            public double Differentiate2(double t) => t * mult;

            public double Integrate(double t) => t * mult;
            public double Integrate(double a, double b) => a * mult;

            public bool SupportsDifferentiation => false;
            public bool SupportsIntegration => false;

            private double mult;

            public NoneInterpolator(double mult)
            {
                this.mult = mult;
            }
        }
    }
}
