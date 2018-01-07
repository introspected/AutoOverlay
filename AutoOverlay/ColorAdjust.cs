using System;
using System.Linq;
using System.Threading.Tasks;
using AutoOverlay;
using AvsFilterNet;

[assembly: AvisynthFilterClass(typeof(ColorAdjust), nameof(ColorAdjust), "ccc[mask]c[tr]i[limitedRange]b", MtMode.SERIALIZED)]
namespace AutoOverlay
{
    public class ColorAdjust : AvisynthFilter
    {
        private int tr = 0;
        private Clip sampleClip, referenceClip, maskClip;
        private bool limitedRange;

        public override void Initialize(AVSValue args, ScriptEnvironment env)
        {
            sampleClip = args[1].AsClip();
            referenceClip = args[2].AsClip();
            maskClip = args[3].IsClip() ? args[3].AsClip() : null;
            tr = args[4].AsInt(tr);
            limitedRange = args[5].AsBool(GetVideoInfo().IsPlanar());
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
                        System.Diagnostics.Debug.WriteLine($"Channel {channel}: ");
                        for (var i = 0; i < 256; i++)
                            //System.Diagnostics.Debug.WriteLine($"{i}: {sampleHist[i]} - {map.Select((newColor, oldColor) => new { newColor, oldColor }).Where(p => p.newColor == i).Sum(p => sampleHist[p.oldColor])} - {referenceHist[i]}");
                            System.Diagnostics.Debug.WriteIf(i != map[i], $"{i} -> {map[i]}, ");
                        System.Diagnostics.Debug.WriteLine("");
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

        private byte[] GetTransitionMap(int[] sampleHist, int[] referenceHist)
        {
            var map = new byte[256];
            for (var i = 0; i < map.Length; i++)
                map[i] = (byte) i;
            var sumSample = GetSumHistogram(sampleHist);
            var sumReference = GetSumHistogram(referenceHist);
            var minSample = sampleHist.TakeWhile(p => p == 0).Count();
            var maxSample = 255 - sampleHist.Reverse().TakeWhile(p => p == 0).Count();
            var minReference = referenceHist.TakeWhile(p => p == 0).Count();
            var maxReference = 255 - referenceHist.Reverse().TakeWhile(p => p == 0).Count();
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
            var min = 0;// limitedRange ? 16 : 0;
            var max = 255;//limitedRange ? 235 : 255;
            var coef = (minReference - min) / (double) (minSample - min + 1);
            for (var color = min; color < minSample; color++)
                map[color] = (byte) Math.Max(min, Math.Round(color * coef));
            coef = (max - maxReference) / (double)(max - maxSample + 1);
            for (var color = maxSample + 1; color <= max; color++)
                map[color] = (byte) Math.Min(max, Math.Round(color / coef));
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
