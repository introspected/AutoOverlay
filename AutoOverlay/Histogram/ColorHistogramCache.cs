using AvsFilterNet;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;

namespace AutoOverlay.Histogram
{
    public class ColorHistogramCache(ColorMatchTuple[] tuples, int length, bool limitedRange, double? gradient)
    {
        private static readonly ConcurrentDictionary<string, ColorHistogramCache> caches = new();

        private readonly ConcurrentDictionary<int, FrameCache> cache = new();

        private readonly ParallelOptions parallelOptions = new();

        private readonly Corner[] corners = gradient.HasValue ? Enum.GetValues(typeof(Corner)).Cast<Corner>().ToArray() : [default];

        public class PlaneHistograms
        {
            public ColorHistogram Sample { get; set; }
            public ColorHistogram Reference { get; set; }

            private double diffCache = double.NaN;

            public double Diff
            {
                get
                {
                    if (!double.IsNaN(diffCache)) 
                        return diffCache;
                    var diffLength = Math.Min(Sample.Length, Reference.Length);
                    var diff = 0.0;
                    var sampleMult = (double)Sample.Length / diffLength;
                    var refMult = (double)Reference.Length / diffLength;
                    unsafe
                    {
                        fixed (double* sample = Sample.Total)
                        fixed (double* reference = Reference.Total)
                            for (var i = 0; i < diffLength; i++)
                            {
                                var sampleIndex = (int)(sampleMult * i);
                                var refIndex = (int)(refMult * i);
                                var delta = reference[refIndex] - sample[sampleIndex];
                                diff += delta * delta;
                            }
                    }

                    return diffCache = Math.Sqrt(diff);
                }
            }
        }

        public class FrameCache : Dictionary<(YUVPlanes, Corner), PlaneHistograms>
        {
            public bool Active { get; set; } = true;
        }

        public static ColorHistogramCache GetOrAdd(string id, Func<ColorHistogramCache> supplier)
        {
            if (id == null) return supplier();
            return caches.GetOrAdd(id, _ => supplier());
        }

        public static void Dispose(string id)
        {
            if (caches.TryRemove(id, out var cache))
                cache.cache.Clear();
        }

        public Dictionary<(YUVPlanes, Corner), PlaneHistograms> Compose(int frame, int buffer, double diff, bool forceMain)
        {
            var main = cache[frame];
            var neighbours = new[] { -1, 1 }.SelectMany(sign => Enumerable.Range(1, buffer)
                    .Select(p => frame + sign * p)
                    .TakeWhile(p => cache.ContainsKey(p))
                    .Select(p => cache[p])
                    .TakeWhile(frameCache => frameCache is { Active: true } 
                                             && main.Keys.All(plane => Math.Abs(main[plane].Diff - frameCache[plane].Diff) < diff)))
                .ToList();
            return main.Keys.ToDictionary(p => p, key => new PlaneHistograms
            {
                Sample = ColorHistogram.Compose(forceMain, main[key].Sample.Enumerate()
                    .Union(neighbours.Select(p => p[key].Sample)).ToArray()),
                Reference = ColorHistogram.Compose(forceMain, main[key].Reference.Enumerate()
                    .Union(neighbours.Select(p => p[key].Reference)).ToArray()),
            });
        }

        public void Shrink(int first, int last, bool deactivate = true)
        {
            foreach (var frame in cache.Keys)
            {
                if (frame < first || frame > last)
                    cache.TryRemove(frame, out _);
                else if (deactivate && cache.TryGetValue(frame, out var value))
                    value.Active = false;
            }
        }

        public FrameCache this[int frame] => cache.TryGetValue(frame, out var value) ? value : null;

        public bool Contains(int frame) => cache.ContainsKey(frame);

        public FrameCache GetOrAdd(int frame,
            dynamic sample, dynamic reference, dynamic sampleMask, dynamic referenceMask,
            Rectangle srcCrop = default, Rectangle sampleCrop = default, Rectangle refCrop = default) =>
            cache.GetOrAdd(frame, n =>
            {
                //Debug.WriteLine("Cache frame: " + n);
                using (new VideoFrameCollector())
                    return PrepareFrame(sample[n], reference[n], sampleMask?[n], referenceMask?[n],
                        srcCrop, sampleCrop, refCrop);
            }).Also(p => p.Active = true);

        private FrameCache PrepareFrame(
            VideoFrame sample, VideoFrame reference, VideoFrame sampleMask, VideoFrame referenceMask,
            Rectangle srcCrop, Rectangle sampleCrop, Rectangle refCrop)
        {
            var frameCache = new FrameCache();

            if (gradient.HasValue)
                Parallel.ForEach(tuples, parallelOptions, p =>
                {
                    Dictionary<Corner, ColorHistogram> sampleMap = null;
                    Dictionary<Corner, ColorHistogram> referenceMap = null;

                    Parallel.Invoke(parallelOptions,
                        () => sampleMap = ColorHistogram.Gradient(length, p.Sample, sample, p.SampleMask, sampleMask, limitedRange),
                        () => referenceMap = ColorHistogram.Gradient(length, p.Reference, reference, p.ReferenceMask, referenceMask, limitedRange)
                    );

                    Parallel.ForEach(corners, corner =>
                    {
                        var value = new PlaneHistograms();
                        lock (frameCache)
                            frameCache[(p.Output.EffectivePlane, corner)] = value;

                        value.Sample = sampleMap[corner];
                        value.Reference = referenceMap[corner];
                    });
                });
            else Parallel.ForEach(tuples.SelectMany(tuple => corners.Select(corner => (tuple, corner))), parallelOptions, p =>
            {
                var value = new PlaneHistograms();
                lock (frameCache)
                    frameCache[(p.tuple.Output.EffectivePlane, p.corner)] = value;
                CornerGradient? cornerGradient = gradient.HasValue
                    ? CornerGradient.Of(
                        p.corner == Corner.TopLeft ? gradient.Value : 1,
                        p.corner == Corner.TopRight ? gradient.Value : 1,
                        p.corner == Corner.BottomRight ? gradient.Value : 1,
                        p.corner == Corner.BottomLeft ? gradient.Value : 1)
                    : null;

                Parallel.Invoke(parallelOptions,
                    () => value.Sample = new ColorHistogram(length, p.tuple.Sample, sample, p.tuple.SampleMask, sampleMask, limitedRange, cornerGradient),
                    () => value.Reference = new ColorHistogram(length, p.tuple.Reference, reference, p.tuple.ReferenceMask, referenceMask, limitedRange, cornerGradient)
                );
            });
            return frameCache;
        }
    }
}
