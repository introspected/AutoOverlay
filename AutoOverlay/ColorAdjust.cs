using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using AutoOverlay;
using AvsFilterNet;

[assembly: AvisynthFilterClass(typeof(ColorAdjust), nameof(ColorAdjust), "ccc[mask]c[tr]i", MtMode.SERIALIZED)]
namespace AutoOverlay
{
    public class ColorAdjust : AvisynthFilter
    {
        private int tr = 0;
        private Clip sampleClip, referenceClip, maskClip;

        public override void Initialize(AVSValue args, ScriptEnvironment env)
        {
            sampleClip = args[1].AsClip();
            referenceClip = args[2].AsClip();
            maskClip = args[3].IsClip() ? args[3].AsClip() : null;
            tr = args[4].AsInt(tr);
        }

        public override VideoFrame GetFrame(int n, ScriptEnvironment env)
        {
            var frame = Child.GetFrame(n, env);
            env.MakeWritable(frame);
            using (var sampleFrame = sampleClip.GetFrame(n, env))
            using (var referenceFrame = referenceClip.GetFrame(n, env))
            using (var maskFrame = maskClip?.GetFrame(n, env))
            {
                var pixelSize = sampleClip.GetVideoInfo().IsRGB24() ? 3 : 1;
                //for (var channel = 0; channel < pixelSize; channel++)
                //var planes = sampleClip.GetVideoInfo().IsRGB24()
                //    ? new[] {default(YUVPlanes)}
                //    : new[] {YUVPlanes.PLANAR_Y, YUVPlanes.PLANAR_U, YUVPlanes.PLANAR_V};
                var planes = new[] {default(YUVPlanes)};
                Parallel.ForEach(planes, plane =>
                {
                    Parallel.For(0, pixelSize, channel =>
                    {
                        var sampleHist = GetHistogram(sampleFrame, maskFrame, pixelSize, channel, plane);
                        var referenceHist = GetHistogram(referenceFrame, maskFrame, pixelSize, channel, plane);
                        var map = GetTransitionMap(sampleHist, referenceHist);
#if DEBUG
                    Debug.WriteLine($"Channel {channel}: ");
                    for (var i = 0; i < 256; i++)
                    //    Debug.WriteLine($"{i}: {sampleHist[i]} - {map.Select((newColor, oldColor) => new { newColor, oldColor }).Where(p => p.newColor == i).Sum(p => sampleHist[p.oldColor])} - {referenceHist[i]}");
                    Debug.WriteIf(i != map[i], $"{i} -> {map[i]}, ");
                    Debug.WriteLine("");
#endif
                        unsafe
                        {
                            var data = (byte*) frame.GetWritePtr();
                            for (var y = 0;
                                y < frame.GetHeight();
                                y++, data += frame.GetPitch())
                            for (var x = 0; x < frame.GetRowSize(); x += pixelSize)
                                data[x] = map[data[x]];
                        }
                    });
                });
            }
            return frame;
        }

        private static byte[] GetTransitionMap(int[] sampleHist, int[] referenceHist)
        {
            var map = new byte[256];
            for (var i = 0; i < map.Length; i++)
                map[i] = (byte) i;
            var sumSample = GetSumHistogram(sampleHist);
            var sumReference = GetSumHistogram(referenceHist);
            var minSample = sumSample.TakeWhile(p => p == 0).Count();
            var maxSample = 255 - sumSample.Reverse().TakeWhile(p => p == 0).Count();
            var minReference = sumReference.TakeWhile(p => p == 0).Count();
            var maxReference = 255 - sumReference.Reverse().TakeWhile(p => p == 0).Count();
            for (int color = minSample, lastRef = minReference; color <= maxSample; color++)
            {
                for (var refColor = lastRef; refColor <= maxReference; refColor++)
                    if (sumSample[color] <= sumReference[refColor] + (refColor == 255 ? 0 : referenceHist[refColor + 1]/2)) //sumReference[refColor] + (refColor == 255 ? 0 : sumReference[refColor + 1] - sumReference[refColor]) / 2)
                    {
                        map[color] = (byte) Math.Max(0, lastRef = refColor);
                        if (lastRef < 255 && sumReference[lastRef + 1] == sumReference[lastRef])
                        {
                            lastRef++;
                        }
                        break;
                    }
            }
            var coef = (minReference + 0) / (double) (minSample + 1);
            for (var color = 0; color < minSample; color++)
                map[color] = (byte) Math.Round(color * coef);
            for (var color = maxSample + 1; color <= maxSample; color++)
                map[color] = (byte)Math.Round(color * coef);
            return map;
        }

        private static int[] GetSumHistogram(int[] histogram)
        {
            var sum = new int[256];
            sum[0] = histogram[0];
            for (var color = 1; color < 256; color++)
            {
                sum[color] = histogram[color] + sum[color - 1];
            }
            return sum;
        }

        private static int[] GetHistogram(VideoFrame frame, VideoFrame maskFrame, int pixelSize, int channel, YUVPlanes plane)
        {
            var hist = new int[256];
            unsafe
            {
                var data = (byte*) frame.GetReadPtr(plane) + channel;
                var mask = (byte*) (maskFrame?.GetReadPtr(plane) ?? IntPtr.Zero) + channel;
                for (var y = 0; y < frame.GetHeight(plane); y++, data += frame.GetPitch(plane), mask += maskFrame?.GetPitch(plane) ?? 0)
                for (var x = 0; x < frame.GetRowSize(plane); x += pixelSize)
                    if (maskFrame == null || mask[x] > 0)
                        hist[data[x]]++;
            }
            return hist;
        }

        protected override void Dispose(bool A_0)
        {
            sampleClip.Dispose();
            referenceClip.Dispose();
            maskClip?.Dispose();
            base.Dispose(A_0);
        }
    }
}
