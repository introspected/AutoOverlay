using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using AutoOverlay;
using AutoOverlay.AviSynth;
using AvsFilterNet;

[assembly: AvisynthFilterClass(
    typeof(ComplexityOverlay),
    nameof(ComplexityOverlay),
    "cc[Channels]s[Steps]i[Preference]f[Mask]b[Smooth]f[Threads]i[Debug]b",
    OverlayUtils.DEFAULT_MT_MODE)]
namespace AutoOverlay
{
    public class ComplexityOverlay : OverlayFilter
    {
        [AvsArgument(Required = true)]
        public Clip Source { get; private set; }

        [AvsArgument(Required = true)]
        public Clip Overlay { get; private set; }

        [AvsArgument]
        public string Channels { get; set; }

        [AvsArgument(Min = 1, Max = 64)]
        public int Steps { get; set; } = 1;

        [AvsArgument(Min = -255, Max = 255)]
        public double Preference { get; set; } = 0;

        [AvsArgument]
        public bool Mask { get; protected set; }

        [AvsArgument(Min = 0, Max = 1.58)]
        public double Smooth { get; protected set; }

        [AvsArgument(Min = 0)]
        public int Threads { get; set; } = 0;

        [AvsArgument]
        public override bool Debug { get; protected set; }

        private YUVPlanes[] planes;
        private int[] realChannels;
        private ParallelOptions parallelOptions;

        protected override void Initialize(AVSValue args)
        {
            parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Threads == 0 ? -1 : Threads
            };
            planes = GetVideoInfo().pixel_type.HasFlag(ColorSpaces.CS_INTERLEAVED)
                ? new[] { default(YUVPlanes) }
                : (Channels ?? "yuv").ToCharArray().Select(p => Enum.Parse(typeof(YUVPlanes), "PLANAR_" + p, true))
                .Cast<YUVPlanes>().ToArray();
            realChannels = GetVideoInfo().IsPlanar()
                ? new[] { 0 }
                : (Channels ?? "rgb").ToLower().ToCharArray().Select(p => "bgr".IndexOf(p)).ToArray();

            var vi = Child.GetVideoInfo();
            SetVideoInfo(ref vi);
        }

        protected override VideoFrame GetFrame(int n)
        {
            var allPlanes = OverlayUtils.GetPlanes(GetVideoInfo().pixel_type);
            if (Smooth > 0)
                return Copy(GetFrameWithSmooth(n));
            var output = NewVideoFrame(StaticEnv);
            using (var src = Source.GetFrame(n, StaticEnv))
            using (var over = Overlay.GetFrame(n, StaticEnv))
            {
                if (GetVideoInfo().IsRGB() && realChannels.Length < 3 || Source.IsRealPlanar() && planes.Length < 3)
                {
                    Parallel.ForEach(allPlanes, parallelOptions, plane => OverlayUtils.CopyPlane(src, output, plane));
                }
                unsafe
                {
                    Parallel.ForEach(planes, parallelOptions, plane =>
                    {
                        var pixelSize = GetVideoInfo().IsRGB() ? 3 : 1;
                        
                        var size = new Size(src.GetRowSize(plane), src.GetHeight(plane));
                        var srcStride = src.GetPitch(plane);
                        var overStride = over.GetPitch(plane);
                        Parallel.ForEach(realChannels, parallelOptions, channel =>
                        {
                            Parallel.For(0, size.Height, parallelOptions, y =>
                            {
                                var srcData = (byte*) src.GetReadPtr(plane) + y * srcStride + channel;
                                var overData = (byte*) over.GetReadPtr(plane) + y * overStride + channel;
                                var writer = (byte*) output.GetWritePtr(plane) + y * output.GetPitch(plane) + channel;
                                for (var x = 0; x < size.Width; x += pixelSize)
                                {
                                    var srcComplexity = GetComplexity(srcData, x, y, pixelSize, srcStride, size, Steps);
                                    var overComplexity = GetComplexity(overData, x, y, pixelSize, overStride, size, Steps);
                                    var diff = srcComplexity - overComplexity;
                                    var srcPreferred = diff > Preference;
                                    if (Mask)
                                        writer[x] = srcPreferred ? byte.MinValue : byte.MaxValue;
                                    else writer[x] = srcPreferred ? srcData[x] : overData[x];
                                }
                            });
                        });
                    });
                }
            }
            return output;
        }

        private VideoFrame GetFrameWithSmooth(int n)
        {
            if (GetVideoInfo().pixel_type.IsRealPlanar())
            {
                dynamic ProcessPlane(YUVPlanes plane, dynamic srcClip, dynamic overClip) => planes.Contains(plane)
                    ? DynamicEnv.Invoke(nameof(ComplexityOverlay), srcClip, overClip, mask: Mask, steps: Steps, smooth: Smooth, preference: Preference)
                    : Mask ? srcClip.BlankClip(color_yuv: 0x00800000) : srcClip;

                var y = ProcessPlane(YUVPlanes.PLANAR_Y, Source.Dynamic().ExtractY(), Overlay.Dynamic().ExtractY());
                var u = ProcessPlane(YUVPlanes.PLANAR_U, Source.Dynamic().ExtractU(), Overlay.Dynamic().ExtractU());
                var v = ProcessPlane(YUVPlanes.PLANAR_V, Source.Dynamic().ExtractV(), Overlay.Dynamic().ExtractV());

                return DynamicEnv.CombinePlanes(y, u, v, planes: "YUV", sample_clip: Source)[n];
            }
            var mask = DynamicEnv.Invoke(nameof(ComplexityOverlay), Source, Overlay, mask: true, steps: Steps, preference: Preference).Blur(Smooth);
            return Mask ? mask[n] : Source.Dynamic().Overlay(Overlay, mask: mask)[n];
        }

        private static unsafe float GetComplexity(byte* data, int x, int y, int pitch, int pixelSize, Size size, int stepCount)
        {
            var value = data[x];
            var sum = 0;
            var count = 0;
            for (var step = -stepCount; step <= stepCount; step++)
            {
                var xTest = x + step * pixelSize;
                if (xTest < 0 || xTest >= size.Width)
                    continue;
                var subStepCount = stepCount - Math.Abs(step);
                for (var subStep = -subStepCount; subStep <= subStepCount; subStep++)
                {
                    if (step == 0 && subStep == 0)
                        continue;

                    var yTest = y + subStep;
                    if (yTest >= 0 && yTest < size.Height)
                    {
                        sum += (data + pitch * subStep)[xTest];
                        count++;
                    }
                }
            }

            return Math.Abs(value - (float) sum / count);
        }
    }
}
