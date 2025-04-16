using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using AutoOverlay;
using AutoOverlay.AviSynth;
using AvsFilterNet;

[assembly: AvisynthFilterClass(
    typeof(ComplexityOverlayMany),
    nameof(ComplexityOverlayMany),
    "cc+[Channels]s[Steps]i[Invert]b[Threads]i[Debug]b",
    OverlayConst.DEFAULT_MT_MODE)]
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

        [AvsArgument]
        public bool Invert { get; protected set; }

        [AvsArgument(Min = 0)]
        public int Threads { get; set; } = 0;

        [AvsArgument]
        public override bool Debug { get; protected set; }

        private PlaneChannel[] planeChannels;
        private ParallelOptions parallelOptions;

        protected override void Initialize(AVSValue args)
        {
            parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Threads == 0 ? -1 : Threads
            };

            var srcVi = Source.GetVideoInfo();
            var overVi = Overlays.Select(p => p.GetVideoInfo()).ToArray();

            if (overVi.Any(p => srcVi.GetSize() != p.GetSize()))
                throw new AvisynthException("Clips with different resolution");
            if (overVi.Any(p => srcVi.pixel_type != p.pixel_type))
                throw new AvisynthException("Clips with different color spaces");

            planeChannels = srcVi.pixel_type.GetPlaneChannels(Channels);
            srcVi.num_frames = Math.Min(srcVi.num_frames, overVi.Min(p => p.num_frames));
            SetVideoInfo(ref srcVi);
        }

        protected override VideoFrame GetFrame(int n)
        {
            var allPlanes = GetVideoInfo().pixel_type.GetPlanes();
            using var src = Source.GetFrame(n, StaticEnv);
            var output = StaticEnv.MakeWritable(src) ? src : NewVideoFrame(StaticEnv, src);
            if (GetVideoInfo().IsRGB() && planeChannels.Length < 3 || Source.IsRealPlanar() && planeChannels.Length < 3)
            {
                src.CopyTo(output, allPlanes);
            }
            var frames = new VideoFrame[Overlays.Length];
            try
            {
                for (var i = 0; i < Overlays.Length; i++)
                {
                    frames[i] = Overlays[i].GetFrame(n, StaticEnv);
                }
                unsafe
                {
                    Parallel.ForEach(planeChannels, parallelOptions, planeChannel =>
                    {
                        var srcPlane = new FramePlane(planeChannel, src, true);
                        var overPlains = frames.Select(frame => new FramePlane(planeChannel, frame, true)).ToArray();
                        var outPlane = new FramePlane(planeChannel, output, false);

                        var pixelSize = outPlane.pixelSize;
                        var size = new Size(outPlane.row, outPlane.height);
                        var srcStride = srcPlane.stride;
                        var strides = overPlains.Select(p => p.stride).ToArray();
                        var outStride = outPlane.stride;

                        switch (srcPlane.byteDepth)
                        {
                            case 1:
                                Parallel.For(0, size.Height, parallelOptions, y =>
                                {
                                    var srcData = (byte*)srcPlane.pointer + y * srcStride;
                                    var data = new byte*[frames.Length];
                                    for (var i = 0; i < frames.Length; i++)
                                    {
                                        data[i] = (byte*)overPlains[i].pointer + y * strides[i];
                                    }
                                    var writer = (byte*)outPlane.pointer + y * outStride;
                                    for (var x = 0; x < size.Width; x += pixelSize)
                                    {
                                        var currentComplexity = ComplexityUtils.Byte(srcData, x, y, pixelSize, srcStride, size, Steps);
                                        var currentData = srcData;
                                        for (var i = 0; i < data.Length; i++)
                                        {
                                            var nextData = data[i];
                                            var complexity = ComplexityUtils.Byte(nextData, x, y, pixelSize, strides[i], size, Steps);
                                            if (Invert ? complexity < currentComplexity : complexity > currentComplexity)
                                            {
                                                currentComplexity = complexity;
                                                currentData = nextData;
                                            }
                                        }
                                        writer[x] = currentData[x];
                                    }
                                });
                                break;
                            case 2:
                                Parallel.For(0, size.Height, parallelOptions, y =>
                                {
                                    var srcData = (ushort*)srcPlane.pointer + y * srcStride;
                                    var data = new ushort*[frames.Length];
                                    for (var i = 0; i < frames.Length; i++)
                                    {
                                        data[i] = (ushort*)overPlains[i].pointer + y * strides[i];
                                    }
                                    var writer = (ushort*)outPlane.pointer + y * outStride;
                                    for (var x = 0; x < size.Width; x += pixelSize)
                                    {
                                        var currentComplexity = ComplexityUtils.Short(srcData, x, y, pixelSize, srcStride, size, Steps);
                                        var currentData = srcData;
                                        for (var i = 0; i < data.Length; i++)
                                        {
                                            var nextData = data[i];
                                            var complexity = ComplexityUtils.Short(nextData, x, y, pixelSize, strides[i], size, Steps);
                                            if (Invert ? complexity < currentComplexity : complexity > currentComplexity)
                                            {
                                                currentComplexity = complexity;
                                                currentData = nextData;
                                            }
                                        }
                                        writer[x] = currentData[x];
                                    }
                                });
                                break;
                            case 4:
                                Parallel.For(0, size.Height, parallelOptions, y =>
                                {
                                    var srcData = (float*)srcPlane.pointer + y * srcStride;
                                    var data = new float*[frames.Length];
                                    for (var i = 0; i < frames.Length; i++)
                                    {
                                        data[i] = (float*)overPlains[i].pointer + y * strides[i];
                                    }
                                    var writer = (float*)outPlane.pointer + y * outStride;
                                    for (var x = 0; x < size.Width; x += pixelSize)
                                    {
                                        var currentComplexity = ComplexityUtils.Float(srcData, x, y, pixelSize, srcStride, size, Steps);
                                        var currentData = srcData;
                                        for (var i = 0; i < data.Length; i++)
                                        {
                                            var nextData = data[i];
                                            var complexity = ComplexityUtils.Float(nextData, x, y, pixelSize, strides[i], size, Steps);
                                            if (Invert ? complexity < currentComplexity : complexity > currentComplexity)
                                            {
                                                currentComplexity = complexity;
                                                currentData = nextData;
                                            }
                                        }
                                        writer[x] = currentData[x];
                                    }
                                });
                                break;
                        }
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
    }
}
