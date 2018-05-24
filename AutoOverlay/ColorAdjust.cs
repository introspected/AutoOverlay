using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoOverlay;
using AvsFilterNet;

[assembly: AvisynthFilterClass(
    typeof(ColorAdjust), nameof(ColorAdjust), 
    "ccc[sampleMask]c[refMask]c[limitedRange]b[planes]s[limit]f", 
    MtMode.NICE_FILTER)]
namespace AutoOverlay
{
    public class ColorAdjust : AvisynthFilter
    {
        private Clip sampleClip, referenceClip, sampleMaskClip, refMaskClip;
        private double limit = 0.95;
        private const int tr = 0;
        private YUVPlanes[] planes;
        private bool limitedRange = true;
        

        public override void Initialize(AVSValue args, ScriptEnvironment env)
        {
            sampleClip = args[1].AsClip();
            referenceClip = args[2].AsClip();
            sampleMaskClip = args[3].IsClip() ? args[3].AsClip() : null;
            refMaskClip = args[4].IsClip() ? args[4].AsClip() : null;
            limitedRange = GetVideoInfo().IsPlanar() && args[5].AsBool(limitedRange);
            planes = args[6].AsString("yuv").ToCharArray().Select(p => Enum.Parse(typeof(YUVPlanes), "PLANAR_" + p, true)).Cast<YUVPlanes>().ToArray();
            if (!OverlayUtils.IsRealPlanar(Child))
                planes = new[] {default(YUVPlanes)};
            limit = args[7].AsFloat(limit);
        }

        public override VideoFrame GetFrame(int n, ScriptEnvironment env)
        {
            var frame = Child.GetFrame(n, env);
            if (!frame.IsWritable())
                env.MakeWritable(frame);

            var sampleFrames = Enumerable.Range(n - tr, tr * 2 + 1).Select(p => sampleClip.GetFrame(p, env)).ToList();
            var referenceFrames = Enumerable.Range(n - tr, tr * 2 + 1).Select(p => referenceClip.GetFrame(p, env)).ToList();

            using (var sampleMaskFrame = sampleMaskClip?.GetFrame(n, env))
            using (var refMaskFrame = refMaskClip?.GetFrame(n, env))
            {
                var pixelSize = sampleClip.GetVideoInfo().IsRGB24() ? 3 : 1;
                Parallel.ForEach(planes, plane =>
                {
                    Parallel.For(0, pixelSize, channel =>
                    {
                        int[] sampleHist = null, referenceHist = null;
                        Parallel.Invoke(
                            () => sampleHist = GetHistogram(sampleFrames, sampleMaskFrame, pixelSize, channel, plane),
                            () => referenceHist = GetHistogram(referenceFrames, refMaskFrame, pixelSize, channel, plane));
                        var map = GetTransitionMap(sampleHist, referenceHist, n, limitedRange ? 16 : 0, limitedRange ? (plane == YUVPlanes.PLANAR_Y ? 235 : 240) : 255);
                        NativeUtils.ApplyColorMap(
                            frame.GetReadPtr(plane) + channel, frame.GetWritePtr(plane) + channel,
                            frame.GetHeight(plane), frame.GetPitch(plane), frame.GetRowSize(plane), pixelSize,
                            map.fixedMap, map.dynamicColors, map.dynamicWeights);
                        //unsafe
                        //{
                        //    var inp0 = (byte*) frame.GetReadPtr(plane) + channel;
                        //    var outp0 = (byte*) frame.GetWritePtr(plane) + channel;
                        //    for (var y = 0; y < frame.GetHeight(plane); y++)
                        //    {
                        //        var inp = inp0 + y * frame.GetPitch(plane);
                        //        var outp = outp0 + y * frame.GetPitch(plane);
                        //        for (var x = 0; x < frame.GetRowSize(plane); x += pixelSize)
                        //        {
                        //            var oldColor = inp[x];
                        //            outp[x] = map.Next(oldColor);
                        //        }
                        //    }
                        //}
                    });
                });
            }
            sampleFrames.ForEach(p => p.Dispose());
            referenceFrames.ForEach(p => p.Dispose());
            return frame;
        }

        private ColorMap GetTransitionMap(int[] sampleHist, int[] referenceHist, int n, int minColor, int maxColor)
        {
            sampleHist = GetUniHistogram(sampleHist);
            referenceHist = GetUniHistogram(referenceHist);
            var map = new ColorMap(n, limit);
            for (var color = 0; color < minColor; color++)
                map.Add(color, minColor, 1);
            for (var color = maxColor + 1; color <= 255; color++)
                map.Add(color, maxColor, 1);
            for (int newColor = minColor, oldColor = minColor - 1, lastOldColor = minColor - 1, lastNewColor = minColor - 1, restPixels = 0; newColor <= maxColor; newColor++)
            {
                void MissedColors(double newColorLimit, double oldColorLimit)
                {
                    var step = (newColorLimit - lastNewColor) / (oldColorLimit - lastOldColor);
                    
                    for (var tempColor = lastOldColor + 1; tempColor < oldColorLimit; tempColor++)
                    {
                        var actualColor = lastNewColor + step * (tempColor - lastOldColor);
                        var val = 1 - (actualColor - (int) actualColor);
                        if (tempColor == maxColor)
                            val = 1;
                        map.Add(tempColor, (int) actualColor, val);
                        if (val <= 1 - double.Epsilon)
                            map.Add(tempColor, (int) actualColor + 1, 1 - val);
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
                if (newColor == maxColor)
                    MissedColors(maxColor + 1, maxColor + 1);
            }
            map.Ready();
            return map;
        }

        private int[] GetUniHistogram(int[] hist)
        {
            var uni = new int[256];
            var total = hist.Sum();
            var newTotal = 0;
            var mult = int.MaxValue / (double) total;
            for (var color = 0; color < 256; color++)
            {
                var old = hist[color];
                var expanded = (int) Math.Round(old * mult);
                total -= old;
                if (total == 0)
                    expanded = int.MaxValue - newTotal;
                newTotal += expanded;
                uni[color] = expanded;
            }
            return uni;
        }

        private int[] GetHistogram(IEnumerable<VideoFrame> frames, VideoFrame maskFrame, int pixelSize, int channel, YUVPlanes plane)
        {
            var hist = new int[256];
            var chroma = plane == YUVPlanes.PLANAR_U || plane == YUVPlanes.PLANAR_V;
            var widthMult = chroma ? OverlayUtils.GetWidthSubsample(GetVideoInfo().pixel_type) : 1;
            var heightMult = chroma ? OverlayUtils.GetHeightSubsample(GetVideoInfo().pixel_type) : 1;
            var maskPitch = maskFrame?.GetPitch() * heightMult ?? 0;
            foreach (var frame in frames)
            {
                if (maskFrame == null)
                    hist = NativeUtils.Histogram8bit(
                        frame.GetReadPtr(plane) + channel, frame.GetPitch(plane),
                        frame.GetHeight(plane), frame.GetRowSize(plane), pixelSize);
                else
                    hist = NativeUtils.Histogram8bitMasked(
                        frame.GetRowSize(plane) / pixelSize, frame.GetHeight(plane),
                        frame.GetReadPtr(plane) + channel, frame.GetPitch(plane), pixelSize,
                        maskFrame.GetReadPtr() + channel, maskPitch, widthMult);

                //unsafe
                //{
                //    var data = (byte*) frame.GetReadPtr(plane) + channel;
                //    var mask = (byte*) (maskFrame?.GetReadPtr() ?? IntPtr.Zero) + channel;
                //    for (var y = 0;
                //        y < frame.GetHeight(plane);
                //        y++, data += frame.GetPitch(plane), mask += maskPitch)
                //    for (var x = 0; x < frame.GetRowSize(plane); x += pixelSize)
                //        if (maskFrame == null || mask[x * widthMult] > 0)
                //            hist[data[x]]++;
                //}
            }
            if (limitedRange)
            {
                for (var color = 0; color < 16; color++)
                {
                    hist[16] += hist[color];
                    hist[color] = 0;
                }
                var max = plane == YUVPlanes.PLANAR_Y ? 235 : 240;
                for (var color = max + 1; color <= 255; color++)
                {
                    hist[max] += hist[color];
                    hist[color] = 0;
                }
            }
            return hist;
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
            public readonly byte[] fixedMap;
            private readonly List<Tuple<byte, double>>[] dynamicMap;
            public readonly byte[,] dynamicColors;
            public readonly double[,] dynamicWeights;
            private readonly double limit;
            private readonly Random random;

            public ColorMap(int seed, double limit)
            {
                fixedMap = new byte[256];
                dynamicColors = new byte[256, 256];
                dynamicWeights = new double[256, 256];
                dynamicMap = new List<Tuple<byte, double>>[256];
                random = new Random(seed);
                this.limit = limit;
            }

            public void Add(int oldColor, int newColor, double weight)
            {
                if (weight >= limit)
                    fixedMap[oldColor] = (byte) newColor;
                if (fixedMap[oldColor] > 0)
                    return;
                var map = dynamicMap[oldColor];
                if (map is null)
                    dynamicMap[oldColor] = map = new List<Tuple<byte, double>>();
                map.Add(new Tuple<byte, double>((byte) newColor, weight));
            }

            public void Ready()
            {
                for (var color = 0; color < 256; color++)
                {
                    var fixedColor = fixedMap[color];
                    if (fixedColor > 0)
                        continue;
                    var list = dynamicMap[color];
                    for (var i = 0; i < list.Count; i++)
                    {
                        var tuple = list[i];
                        dynamicColors[color, i] = tuple.Item1;
                        dynamicWeights[color, i] = tuple.Item2;
                    }
                }
            }

            public byte Next(byte color)
            {
                var fixedColor = fixedMap[color];
                if (fixedColor > 0)
                    return fixedColor;
                var val = random.NextDouble();
                for (var i = 0;; i++)
                    if ((val -= dynamicWeights[color, i]) < double.Epsilon)
                        return dynamicColors[color, i];
            }
        }
    }
}
