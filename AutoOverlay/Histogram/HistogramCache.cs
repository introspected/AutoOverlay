﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AvsFilterNet;

namespace AutoOverlay.Histogram
{
    public class HistogramCache
    {
        private static readonly Dictionary<string, HistogramCache> cacheCache = new(); // we need to go deeper

        private readonly ConcurrentDictionary<int, Lazy<Dictionary<CacheDimension, CacheValue>>> cache = new();

        private readonly YUVPlanes[] planes;
        private readonly int[] channels;
        private readonly bool simd;
        private readonly bool limitedRange;
        private readonly ColorSpaces sampleFormat;
        private readonly ColorSpaces referenceFormat;
        private readonly ColorSpaces sourceFormat;
        private readonly int pixelSize;
        private readonly int around;
        private readonly bool greyMask;
        private readonly ParallelOptions parallelOptions;

        public string Id { get; } = Guid.NewGuid().ToString();

        public HistogramCache(YUVPlanes[] planes, int[] channels, bool simd, bool limitedRange, 
            ColorSpaces sampleFormat, ColorSpaces referenceFormat, ColorSpaces sourceFormat, int around, bool greyMask, 
            ParallelOptions parallelOptions)
        {
            pixelSize = sourceFormat.HasFlag(ColorSpaces.CS_BGR) ? 3 : 1;
            this.planes = planes;
            this.channels = channels;
            this.simd = simd;
            this.limitedRange = limitedRange && pixelSize == 1;
            this.sampleFormat = sampleFormat;
            this.referenceFormat = referenceFormat;
            this.sourceFormat = sourceFormat;
            this.around = around;
            this.greyMask = greyMask;
            this.parallelOptions = parallelOptions;
            lock (cacheCache)
                cacheCache.Add(Id, this);
        }

        public static HistogramCache Get(string id)
        {
            return cacheCache.ContainsKey(id) ? cacheCache[id] : null;
        }

        public static void Dispose(string id)
        {
            var cache = Get(id);
            if (cache != null)
            {
                cache.cache.Clear();
                cacheCache.Remove(id);
            }
        }

        public HistogramCache SubCache(int first, int last)
        {
            var temp = new HistogramCache(planes, channels, simd, limitedRange, sampleFormat, referenceFormat, sourceFormat, last - first, greyMask, parallelOptions);
            for (var frame = first; frame <= last; frame++)
                if (cache.TryGetValue(frame, out var value))
                    temp.cache[frame] = value;
            return temp;
        }

        public Dictionary<CacheDimension, CacheValue> this[int frame] => cache.ContainsKey(frame) ? cache[frame].Value : null;

        public Dictionary<CacheDimension, CacheValue> GetFrame(int frame, 
            Func<VideoFrame> source, Func<VideoFrame> sample, Func<VideoFrame> reference, 
            Func<VideoFrame> sampleMask, Func<VideoFrame> referenceMask)
        {
            return cache.GetOrAdd(frame, f => new Lazy<Dictionary<CacheDimension, CacheValue>>(() =>
            {
                var minAround = Math.Max(10, around);
                if (cache.Count > minAround * 100)
                    foreach (var key in cache.Keys.Where(p => p < f - minAround || p > f + minAround))
                        cache.TryRemove(key, out _);
                return PrepareFrame(sample(), reference(), source(), sampleMask(), referenceMask());
            })).Value;
        }

        public Dictionary<CacheDimension, CacheValue> PrepareFrame(
            VideoFrame sample, VideoFrame reference, VideoFrame source,
            VideoFrame sampleMask, VideoFrame referenceMask)
        {
            var dict = new Dictionary<CacheDimension, CacheValue>();
            Parallel.ForEach(planes, parallelOptions, plane =>
            {
                Parallel.ForEach(channels, parallelOptions, channel =>
                {
                    var dimension = new CacheDimension { Plane = plane, Channel = channel };
                    var value = new CacheValue();
                    lock (dict)
                        dict[dimension] = value;
                    Parallel.Invoke(parallelOptions,
                        () => value.SampleHist = GetHistogram(sample, sampleMask, channel, plane, sampleFormat, false),
                        () => value.ReferenceHist = GetHistogram(reference, referenceMask, channel, plane, referenceFormat, limitedRange),
                        () => value.InputHist = source == null ? null : GetHistogram(source, null, channel, plane, sourceFormat, limitedRange)
                    );
                    if (value.Empty)
                        return;
                    var diffLength = Math.Min(value.SampleHist.Length, value.ReferenceHist.Length);
                    var diffHist = value.DiffHist = new int[diffLength];
                    foreach (var i in Enumerable.Range(0, diffLength))
                    {
                        // TODO HDR
                        diffHist[i] = value.ReferenceHist[i] - value.SampleHist[i];
                    }
                });
            });
            return dict;
        }

        private int[] GetHistogram(VideoFrame frame, VideoFrame maskFrame, int channel, YUVPlanes plane, ColorSpaces pixelType, bool limitedRange)
        {
            var bits = pixelType.GetBitDepth();
            var hist = new uint[1 << bits];
            var chroma = plane == YUVPlanes.PLANAR_U || plane == YUVPlanes.PLANAR_V;
            var widthMult = chroma && greyMask ? pixelType.GetWidthSubsample() : 1;
            var heightMult = chroma && greyMask ? pixelType.GetHeightSubsample() : 1;
            var maskPitch = maskFrame?.GetPitch(greyMask ? default : plane) * heightMult ?? 0;
            var maskPtr = maskFrame?.GetReadPtr(greyMask ? default : plane) + channel ?? IntPtr.Zero;
            NativeUtils.FillHistogram(hist,
                frame.GetRowSize(plane), frame.GetHeight(plane), channel,
                frame.GetReadPtr(plane), frame.GetPitch(plane), pixelSize,
                maskPtr, maskPitch, widthMult, simd);
            if (limitedRange)
            {
                var min = GetLowColor(bits);
                for (var color = 0; color < min; color++)
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

        private int[] GetUniHistogram(uint[] hist)
        {
            var length = hist.Length;
            var uni = new int[length];
            var total = (uint)hist.Cast<int>().Sum();
            var newRest = int.MaxValue;
            var mult = int.MaxValue / (double) total;
            var rest = total;

            var maxColor = -1;
            var maxCount = 0;

            unsafe
            {
                fixed (uint* input = hist)
                fixed (int* output = uni)
                {
                    for (var color = 0; color < length; color++)
                    {
                        var old = input[color];
                        rest -= old;
                        var expanded = (int)Math.Round(old * mult);
                        newRest -= expanded;
                        output[color] = expanded;
                        if (expanded > maxCount)
                        {
                            maxColor = color;
                            maxCount = expanded;
                        }
                    }
                }
            }
            if (maxColor == -1)
                return null;
            uni[maxColor] += newRest;
            if (uni.Sum() != int.MaxValue)
                throw new InvalidOperationException();
            return uni;
        }

        public int GetLowColor(int bits)
        {
            if (!limitedRange)
                return 0;
            return 16 << (bits - 8);
        }

        public int GetHighColor(int bits, YUVPlanes plane)
        {
            if (!limitedRange)
                return (1 << bits) - 1;
            var sdr = plane == YUVPlanes.PLANAR_U || plane == YUVPlanes.PLANAR_V ? 240 : 235;
            return sdr << (bits - 8);
        }

        public struct CacheDimension
        {
            private static YUVPlanes[] SAME = {YUVPlanes.PLANAR_Y, default};
            public YUVPlanes Plane { get; set; }
            public int Channel { get; set; }

            public bool Equal(YUVPlanes plane, int channel)
            {
                return Channel == channel && (Plane == plane || SAME.Contains(Plane) && SAME.Contains(plane));
            }
        }

        public class CacheValue
        {
            public int[] SampleHist { get; set; }
            public int[] ReferenceHist { get; set; }
            public int[] InputHist { get; set; }
            public int[] DiffHist { get; set; }

            public bool Empty => SampleHist == null || ReferenceHist == null;
        }
    }
}
