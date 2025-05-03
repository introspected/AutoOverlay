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
    "cc[Channels]s[Steps]i[Preference]f[Mask]b[Smooth]f[Invert]b[Threads]i[Debug]b",
    OverlayConst.DEFAULT_MT_MODE)]
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

        [AvsArgument]
        public bool Invert { get; protected set; }

        [AvsArgument(Min = 0)]
        public int Threads { get; set; } = 0;

        [AvsArgument]
        public override bool Debug { get; protected set; }

        private YUVPlanes[] planes;
        private PlaneChannel[] planeChannels;
        private ParallelOptions parallelOptions;
        private dynamic child;

        protected override void Initialize(AVSValue args)
        {
            parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Threads == 0 ? -1 : Threads
            };

            var srcVi = Source.GetVideoInfo();
            var overVi = Overlay.GetVideoInfo();

            if (srcVi.IsRGB() && !srcVi.IsPlanar() && Smooth > 0)
                throw new AvisynthException("Interleaved RGB with smooth not supported yet");
            if (srcVi.GetSize() != overVi.GetSize())
                throw new AvisynthException("Clips with different resolution");
            if (srcVi.pixel_type != overVi.pixel_type)
                throw new AvisynthException("Clips with different color spaces");

            planeChannels = srcVi.pixel_type.GetPlaneChannels(Channels);
            planes = srcVi.pixel_type.GetPlanes();
            srcVi.num_frames = Math.Min(srcVi.num_frames, overVi.num_frames);
            SetVideoInfo(ref srcVi);
        }

        protected override void AfterInitialize()
        {
            if (Smooth == 0) return;
            if (GetVideoInfo().pixel_type.IsRealPlanar())
            {
                dynamic ProcessPlane(YUVPlanes plane, dynamic srcClip, dynamic overClip) => planeChannels.Select(p => p.Plane).Contains(plane)
                    ? srcClip.ComplexityOverlay(overClip, mask: Mask, steps: Steps, smooth: Smooth, preference: Preference)
                    : Mask ? srcClip.BlankClip(color_yuv: 0x800000) : srcClip;

                var clips = planes.Select(p => ProcessPlane(p, Source.ExtractPlane(p).Dynamic(), Overlay.ExtractPlane(p).Dynamic())).ToArray();
                var letters = planes.Select(p => p.GetLetter()).Aggregate(string.Concat);

                child = DynamicEnv.CombinePlanes(clips, planes: letters, sample_clip: Source);
            }
            else
            {
                var mask = DynamicEnv.ComplexityOverlay(Source, Overlay, mask: true, steps: Steps, preference: Preference).Blur(Smooth);
                child = Mask ? mask : Source.Dynamic().Overlay(Overlay, mask: mask);
            }
        }

        protected override VideoFrame GetFrame(int n)
        {
            if (child != null)
                return child[n];
            using var src = Source.GetFrame(n, StaticEnv);
            using var over = Overlay.GetFrame(n, StaticEnv);
            var output = NewVideoFrame(StaticEnv, src);
            if (GetVideoInfo().IsRGB() && planeChannels.Length < 3 || Source.IsRealPlanar() && planeChannels.Length < 3)
            {
                src.CopyTo(output, planes);
            }
            unsafe
            {
                Parallel.ForEach(planeChannels, parallelOptions, planeChannel =>
                {
                    var srcPlane = new FramePlane(planeChannel, src, true);
                    var overPlane = new FramePlane(planeChannel, over, true);
                    var outPlane = new FramePlane(planeChannel, output, false);

                    var pixelSize = outPlane.pixelSize;
                    var size = new Size(outPlane.row, outPlane.height);
                    var srcStride = srcPlane.stride;
                    var overStride = overPlane.stride;
                    var outStride = outPlane.stride;

                    switch (srcPlane.byteDepth)
                    {
                        case 1:
                            Parallel.For(0, size.Height, parallelOptions, y =>
                            {
                                var srcData = (byte*)srcPlane.pointer + y * srcStride;
                                var overData = (byte*)overPlane.pointer + y * overStride;
                                var writer = (byte*)outPlane.pointer + y * outStride;
                                for (var x = 0; x < size.Width; x += pixelSize)
                                {
                                    var srcComplexity = ComplexityUtils.Byte(srcData, x, y, pixelSize, srcStride, ref size, Steps);
                                    var overComplexity = ComplexityUtils.Byte(overData, x, y, pixelSize, overStride, ref size, Steps);
                                    var diff = srcComplexity - overComplexity;
                                    var srcPreferred = Invert ? diff < Preference : diff > Preference;
                                    if (Mask)
                                        writer[x] = srcPreferred ? byte.MinValue : byte.MaxValue;
                                    else writer[x] = srcPreferred ? srcData[x] : overData[x];
                                }
                            });
                            break;
                        case 2:
                            ushort min = 0;
                            ushort max = (ushort)((1 << planeChannel.Depth) - 1);
                            Parallel.For(0, size.Height, parallelOptions, y =>
                            {
                                var srcData = (ushort*)srcPlane.pointer + y * srcStride;
                                var overData = (ushort*)overPlane.pointer + y * overStride;
                                var writer = (ushort*)outPlane.pointer + y * outStride;
                                for (var x = 0; x < size.Width; x += pixelSize)
                                {
                                    var srcComplexity = ComplexityUtils.Short(srcData, x, y, pixelSize, srcStride, ref size, Steps);
                                    var overComplexity = ComplexityUtils.Short(overData, x, y, pixelSize, overStride, ref size, Steps);
                                    var diff = srcComplexity - overComplexity;
                                    var srcPreferred = Invert ? diff < Preference : diff > Preference;
                                    if (Mask)
                                        writer[x] = srcPreferred ? min : max;
                                    else writer[x] = srcPreferred ? srcData[x] : overData[x];
                                }
                            });
                            break;
                        case 4:
                            Parallel.For(0, size.Height, parallelOptions, y =>
                            {
                                var srcData = (float*)srcPlane.pointer + y * srcStride;
                                var overData = (float*)overPlane.pointer + y * overStride;
                                var writer = (float*)outPlane.pointer + y * outStride;
                                for (var x = 0; x < size.Width; x += pixelSize)
                                {
                                    var srcComplexity = ComplexityUtils.Float(srcData, x, y, pixelSize, srcStride, ref size, Steps);
                                    var overComplexity = ComplexityUtils.Float(overData, x, y, pixelSize, overStride, ref size, Steps);
                                    var diff = srcComplexity - overComplexity;
                                    var srcPreferred = Invert ? diff < Preference : diff > Preference;
                                    if (Mask)
                                        writer[x] = srcPreferred ? 0 : 1;
                                    else writer[x] = srcPreferred ? srcData[x] : overData[x];
                                }
                            });
                            break;
                    }
                });
            }

            return output;
        }

        protected override void Dispose(bool disposing)
        {
            child?.Dispose();
            base.Dispose(disposing);
        }
    }
}
