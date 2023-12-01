using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using AutoOverlay;
using AutoOverlay.AviSynth;
using AvsFilterNet;

[assembly: AvisynthFilterClass(
    typeof(ComplexityOverlayMany),
    nameof(ComplexityOverlayMany),
    "cc+[Channels]s[Steps]i[Threads]i[Debug]b",
    OverlayUtils.DEFAULT_MT_MODE)]
namespace AutoOverlay
{
    public class ComplexityOverlayMany : OverlayFilter
    {
        [AvsArgument(Required = true)]
        public Clip Source { get; private set; }

        [AvsArgument(Required = true)]
        public Clip[] Overlays { get; private set; }

        [AvsArgument]
        public string Channels { get; set; }

        [AvsArgument(Min = 1, Max = 64)]
        public int Steps { get; set; } = 1;

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
            var output = NewVideoFrame(StaticEnv);
            var frames = new VideoFrame[Overlays.Length];
            try
            {
                using var src = Source.GetFrame(n, StaticEnv);
                if (GetVideoInfo().IsRGB() && realChannels.Length < 3 || Source.IsRealPlanar() && planes.Length < 3)
                {
                    Parallel.ForEach(allPlanes, parallelOptions, plane => OverlayUtils.CopyPlane(src, output, plane));
                }
                for (var i = 0; i < Overlays.Length; i++)
                {
                    frames[i] = Overlays[i].GetFrame(n, StaticEnv);
                }
                unsafe
                {
                    Parallel.ForEach(planes, parallelOptions, plane =>
                    {
                        var srcStride = src.GetPitch(plane);
                        var strides = frames.Select(frame => frame.GetPitch(plane)).ToArray();
                        var pixelSize = GetVideoInfo().IsRGB() ? 3 : 1;

                        var size = new Size(src.GetRowSize(plane), src.GetHeight(plane));
                        Parallel.ForEach(realChannels, parallelOptions, channel =>
                        {
                            Parallel.For(0, size.Height, parallelOptions, y =>
                            {
                                var srcData = (byte*) src.GetReadPtr(plane) + y * srcStride + channel;
                                var data = new byte*[frames.Length];
                                for (var i = 0; i < frames.Length; i++)
                                {
                                    data[i] = (byte*) frames[i].GetReadPtr(plane) + y * strides[i] + channel;
                                }
                                var writer = (byte*) output.GetWritePtr(plane) + y * output.GetPitch(plane) + channel;
                                for (var x = 0; x < size.Width; x += pixelSize)
                                {
                                    var complexed = GetComplexity(srcData, x, y, pixelSize, srcStride, size, Steps);
                                    var complexedData = srcData;
                                    for (var i = 0; i < data.Length; i++)
                                    {
                                        var currentData = data[i];
                                        var complexity = GetComplexity(currentData, x, y, pixelSize, strides[i], size, Steps);
                                        if (complexity >= complexed)
                                        {
                                            complexed = complexity;
                                            complexedData = currentData;
                                        }
                                    }
                                    writer[x] = complexedData[x];
                                }
                            });
                        });
                    });
                }
            }
            finally
            {
                foreach (var frame in frames)
                    frame.Dispose();
            }
            return output;
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
