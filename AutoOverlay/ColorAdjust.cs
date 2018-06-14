using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoOverlay;
using AvsFilterNet;

[assembly: AvisynthFilterClass(
    typeof(ColorAdjust), nameof(ColorAdjust),
    "ccc[sampleMask]c[refMask]c[limitedRange]b[planes]s[dither]f",
    MtMode.NICE_FILTER)]
namespace AutoOverlay
{
    public class ColorAdjust : AvisynthFilter
    {
        private Clip sampleClip, referenceClip, sampleMaskClip, refMaskClip;
        private double dither = 0.95;
        private const int tr = 0;
        private YUVPlanes[] planes;
        private bool limitedRange = true;
        private int sampleBits, referenceBits;

        public override void Initialize(AVSValue args, ScriptEnvironment env)
        {
            sampleClip = args[1].AsClip();
            referenceClip = args[2].AsClip();
            sampleMaskClip = args[3].IsClip() ? args[3].AsClip() : null;
            refMaskClip = args[4].IsClip() ? args[4].AsClip() : null;
            limitedRange = GetVideoInfo().IsPlanar() && args[5].AsBool(limitedRange);
            planes = args[6].AsString("yuv").ToCharArray().Select(p => Enum.Parse(typeof(YUVPlanes), "PLANAR_" + p, true)).Cast<YUVPlanes>().ToArray();
            if (!OverlayUtils.IsRealPlanar(Child))
                planes = new[] { default(YUVPlanes) };
            dither = args[7].AsFloat(dither);
            var vi = GetVideoInfo();
            var refVi = referenceClip.GetVideoInfo();
            sampleBits = vi.pixel_type.GetBitDepth();
            referenceBits = refVi.pixel_type.GetBitDepth();
            vi.pixel_type = vi.pixel_type.ChangeBitDepth(referenceBits);
            SetVideoInfo(ref vi);
        }

        private int GetLowColor(int bits)
        {
            if (!limitedRange)
                return 0;
            return 16 << (bits - 8);
        }

        private int GetHighColor(int bits, YUVPlanes plane)
        {
            if (!limitedRange)
                return (1 << bits) - 1;
            var sdr = plane == YUVPlanes.PLANAR_U || plane == YUVPlanes.PLANAR_V ? 240 : 235;
            return sdr << (bits - 8);
        }

        public override VideoFrame GetFrame(int n, ScriptEnvironment env)
        {
            var sampleFrames = Enumerable.Range(n - tr, tr * 2 + 1).Select(p => sampleClip.GetFrame(p, env)).ToList();
            var referenceFrames = Enumerable.Range(n - tr, tr * 2 + 1).Select(p => referenceClip.GetFrame(p, env)).ToList();
            var output = NewVideoFrame(env);
            using (var input = Child.GetFrame(n, env))
            using (var sampleMaskFrame = sampleMaskClip?.GetFrame(n, env))
            using (var refMaskFrame = refMaskClip?.GetFrame(n, env))
            {
                var pixelSize = sampleClip.GetVideoInfo().IsRGB() ? 3 : 1;
                Parallel.ForEach(planes, plane =>
                {
                    Parallel.For(0, pixelSize, channel =>
                    {
                        int[] sampleHist = null, referenceHist = null;
                        Parallel.Invoke(
                            () => sampleHist = GetHistogram(sampleFrames, sampleMaskFrame, pixelSize, channel, plane, sampleClip.GetVideoInfo().pixel_type, sampleBits, false),
                            () => referenceHist = GetHistogram(referenceFrames, refMaskFrame, pixelSize, channel, plane, referenceClip.GetVideoInfo().pixel_type, referenceBits, limitedRange));
                        
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
            sampleFrames.ForEach(p => p.Dispose());
            referenceFrames.ForEach(p => p.Dispose());
            return output;
        }

        private ColorMap GetTransitionMap(int[] sampleHist, int[] referenceHist, int n, YUVPlanes plane)
        {
            var map = new ColorMap(sampleBits, n, dither);
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
#if DEBUG
            if (uni.Sum() != int.MaxValue)
                throw new InvalidOperationException();
#endif
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

        protected override void Dispose(bool A_0)
        {
            sampleClip.Dispose();
            referenceClip.Dispose();
            sampleMaskClip?.Dispose();
            refMaskClip?.Dispose();
            base.Dispose(A_0);
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
