using System;
using AutoOverlay;
using AutoOverlay.AviSynth;
using AvsFilterNet;
using System.Threading.Tasks;
using AutoOverlay.Histogram;
using System.Linq;
using System.Runtime.Remoting.Contexts;

[assembly: AvisynthFilterClass(
    typeof(ColorMatch), nameof(ColorMatch),
    "cc[Sample]c[SampleMask]c[ReferenceMask]c[GreyMask]b[Intensity]f[Length]i[Dither]f[Channels]s" +
    "[FrameBuffer]i[FrameDiff]f[BufferedExtrapolation]b[LimitedRange]b[Exclude]f[Gradient]f[Frame]i[Seed]i[Plane]s[CacheId]s",
    OverlayConst.DEFAULT_MT_MODE)]
namespace AutoOverlay
{
    public class ColorMatch : OverlayFilter
    {
        [AvsArgument(Required = true)]
        public Clip Reference { get; private set; }

        [AvsArgument]
        public Clip Sample { get; private set; }

        [AvsArgument]
        public Clip SampleMask { get; private set; }

        [AvsArgument]
        public Clip ReferenceMask { get; private set; }

        [AvsArgument]
        public bool GreyMask { get; protected set; } = true;

        [AvsArgument(Min = 0, Max = 1)]
        public double Intensity { get; set; } = 1;

        [AvsArgument(Min = 10, Max = 1000000)]
        public int Length { get; private set; } = OverlayConst.COLOR_BUCKETS_COUNT;

        [AvsArgument(Min = 0, Max = 1)]
        public double Dither { get; private set; } = OverlayConst.COLOR_DITHER;

        [AvsArgument]
        public string Channels { get; private set; }

        [AvsArgument(Min = 0, Max = OverlayConst.ENGINE_HISTORY_LENGTH)]
        public int FrameBuffer { get; set; } = 0;

        [AvsArgument(Min = 0, Max = 100)]
        public double FrameDiff { get; set; } = 1;

        [AvsArgument(Min = 0)]
        public bool BufferedExtrapolation { get; set; } = true;

        [AvsArgument] 
        public bool LimitedRange { get; private set; }

        [AvsArgument(Min = 0, Max = 1)]
        public double Exclude { get; set; } = 0;

        [AvsArgument(Min = 0, Max = 1000000)]
        public double Gradient { get; set; }

        [AvsArgument(Min = -1)]
        public int Frame { get; private set; } = -1;

        [AvsArgument]
        public int Seed { get; private set; }

        [AvsArgument]
        public string Plane { get; private set; }

        [AvsArgument]
        public string CacheId { get; protected set; }

        private ColorMatchTuple[] planeChannelTuples;
        private YUVPlanes[] planes;
        private bool keepBitDepth;

        private ColorHistogramCache histogramCache;
        private bool cornerGradient;
        private int frameCount;

        protected override void Initialize(AVSValue args)
        {
            Sample ??= Child;
            var vi = Child.GetVideoInfo();
            if (vi.IsRGB() || vi.pixel_type.HasFlag(ColorSpaces.CS_GENERIC_RGBP) || vi.pixel_type.HasFlag(ColorSpaces.CS_GENERIC_RGBAP))
                LimitedRange = false;
            var refVi = Reference.GetVideoInfo();
            var sampleVi = Sample.GetVideoInfo();
            keepBitDepth = vi.pixel_type.GetBitDepth() == refVi.pixel_type.GetBitDepth();
            vi.pixel_type = vi.pixel_type.VPlaneFirst().ChangeBitDepth(refVi.pixel_type.GetBitDepth());
            frameCount = vi.num_frames = Math.Min(vi.num_frames, Math.Min(refVi.num_frames, sampleVi.num_frames));
            SetVideoInfo(ref vi);
            planeChannelTuples = ColorMatchTuple.Compose(Child, Sample, Reference, Channels?.ToLower(), GreyMask, Plane);
            cornerGradient = Gradient > 0;
            histogramCache = ColorHistogramCache.GetOrAdd(CacheId, () => new ColorHistogramCache(planeChannelTuples, Length, LimitedRange, cornerGradient ? Gradient : null));
            planes = vi.pixel_type.GetPlanes();
        }

        protected override VideoFrame GetFrame(int n)
        {
            var input = Child.GetFrame(n, StaticEnv);

            VideoFrame output;
            if (keepBitDepth && StaticEnv.MakeWritable(input))
                output = input;
            else
            {
                output = NewVideoFrame(StaticEnv);
                if (Channels != null)
                    input.CopyTo(output, planes);
            }

            var targetFrame = Frame == -1 ? n : Frame;
            var lookAround = targetFrame.Enumerate();
            if (CacheId == null)
                lookAround = new[] { -1, 1 }
                    .SelectMany(sign => Enumerable.Range(1, FrameBuffer).Select(p => targetFrame + sign * p))
                    .Where(p => p > 0 && p < frameCount)
                    .Union(lookAround);
            foreach (var frame in lookAround)
                histogramCache.GetOrAdd(frame, Sample.Dynamic(), Reference.Dynamic(), SampleMask.Dynamic(), ReferenceMask.Dynamic());
            var buffer = histogramCache.Compose(targetFrame, FrameBuffer, FrameDiff, BufferedExtrapolation);

            void MatchColor(ColorMatchTuple tuple, Corner corner, VideoFrame outFrame)
            {
                var histograms = buffer[(tuple.Output.EffectivePlane, corner)];
                var seed = Seed^n + (int)tuple.Output.EffectivePlane;

                var fullLength = histograms.Sample.Length == 1 << tuple.Input.Depth &&
                                 histograms.Reference.Length == 1 << tuple.Reference.Depth;

                if (Dither > 0 && fullLength)
                {
                    using var lut = histograms.Sample.GetLut(histograms.Reference, Dither, Intensity, Exclude);
                    Apply(tuple.Input, tuple.Output, input, outFrame, (o, i, num) => o.ApplyLut(i, lut, seed << num));
                }
                else
                {
                    var interpolator = histograms.Sample.GetInterpolator(histograms.Reference, Intensity, Exclude);
                    Apply(tuple.Input, tuple.Output, input, outFrame, (o, i, num) => o.ApplyHistogram(i, interpolator, seed << num));
                    if (interpolator is IDisposable disposable)
                        disposable.Dispose();
                }
            }

            if (cornerGradient)
            {
                using var tl = NewVideoFrame(StaticEnv);
                using var tr = NewVideoFrame(StaticEnv);
                using var br = NewVideoFrame(StaticEnv);
                using var bl = NewVideoFrame(StaticEnv);
                Parallel.ForEach(planeChannelTuples, tuple =>
                {
                    var tlFrame = new FramePlane(tuple.Output, tl, false);
                    var trFrame = new FramePlane(tuple.Output, tr, false);
                    var brFrame = new FramePlane(tuple.Output, br, false);
                    var blFrame = new FramePlane(tuple.Output, bl, false);

                    Parallel.Invoke(
                        () => MatchColor(tuple, Corner.TopLeft, tl),
                        () => MatchColor(tuple, Corner.TopRight, tr),
                        () => MatchColor(tuple, Corner.BottomRight, br),
                        () => MatchColor(tuple, Corner.BottomLeft, bl));

                    var res = new FramePlane(tuple.Output, output, false);
                    res.GradientMerge(tlFrame, trFrame, brFrame, blFrame);
                });
            }
            else
            {
                Parallel.ForEach(planeChannelTuples, tuple => MatchColor(tuple, default, output));
            }

            if (CacheId == null)
                histogramCache.Shrink(targetFrame - OverlayConst.ENGINE_HISTORY_LENGTH * 2, targetFrame + OverlayConst.ENGINE_HISTORY_LENGTH * 2, false);
            return output;
        }

        private static void Apply(
            PlaneChannel inPlaneChannel, PlaneChannel outPlaneChannel, 
            VideoFrame input, VideoFrame output,
            Action<FramePlane, FramePlane, int> action)
        {
            var n = Environment.ProcessorCount / 2;
            var inPlanes = new FramePlane(inPlaneChannel, input, true).Split(n);
            var outPlanes = new FramePlane(outPlaneChannel, output, false).Split(n);

            Parallel.ForEach(Enumerable.Range(0, n)
                    .Select(i => new { inPlane = inPlanes[i], outPlane = outPlanes[i], Num = i }),
                tuple => action(tuple.outPlane, tuple.inPlane, tuple.Num));
        }
    }
}
