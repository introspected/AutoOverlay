using AutoOverlay;
using AutoOverlay.AviSynth;
using AvsFilterNet;
using System;
using System.Linq;
using System.Threading.Tasks;
using AutoOverlay.Histogram;

[assembly: AvisynthFilterClass(
    typeof(ColorMatch), nameof(ColorMatch),
    "c[Sample]c[Reference]c[SampleMask]c[ReferenceMask]c[GreyMask]b[Intensity]f[Length]i[Channels]s[LimitedRange]b[Exclude]f",
    OverlayUtils.DEFAULT_MT_MODE)]
namespace AutoOverlay
{
    public class ColorMatch : OverlayFilter
    {
        [AvsArgument(Required = true)]
        public Clip Sample { get; private set; }

        [AvsArgument(Required = true)]
        public Clip Reference { get; private set; }

        [AvsArgument]
        public Clip SampleMask { get; private set; }

        [AvsArgument]
        public Clip ReferenceMask { get; private set; }

        [AvsArgument]
        public bool GreyMask { get; protected set; } = true;

        [AvsArgument(Min = 0, Max = 1)]
        public double Intensity { get; set; } = 1;

        [AvsArgument(Min = 3, Max = 1000000)]
        public int Length { get; private set; } = 1024;

        [AvsArgument]
        public string Channels { get; private set; }

        [AvsArgument] 
        public bool LimitedRange { get; private set; } = true;

        [AvsArgument(Min = 0, Max = 1)]
        public double Exclude { get; set; } = 0;

        //[AvsArgument]
        public bool Lab { get; private set; }

        private PlaneChannelTuple[] planeChannelTuples;
        private bool keepBitDepth;

        protected override void Initialize(AVSValue args)
        {
            var vi = Child.GetVideoInfo();
            if (vi.IsRGB())
                LimitedRange = false;
            var refVi = Reference.GetVideoInfo();
            keepBitDepth = vi.pixel_type.GetBitDepth() == refVi.pixel_type.GetBitDepth();
            vi.pixel_type = vi.pixel_type.ChangeBitDepth(refVi.pixel_type.GetBitDepth());
            SetVideoInfo(ref vi);
            var samplePlaneChannels = Sample.GetVideoInfo().pixel_type.GetPlaneChannels();
            var referencePlaneChannels = refVi.pixel_type.GetPlaneChannels();
            var inputPlaneChannels = Child.GetVideoInfo().pixel_type.GetPlaneChannels();
            var outputPlaneChannels = vi.pixel_type.GetPlaneChannels();
            var samplePlanes = samplePlaneChannels.Select(p => p.EffectivePlane).ToHashSet();
            var referencePlanes = referencePlaneChannels.Select(p => p.EffectivePlane).ToHashSet();
            var matchPlanes = samplePlanes.Intersect(referencePlanes);
            Channels = Channels?.ToUpper();
            if (Channels != null)
            {
                if (Lab)
                    Channels = Channels.Select(p => p switch { 'L' => 'Y', 'A' => 'U', 'B' => 'V' }).ToString();
                matchPlanes = Channels.Select(p =>
                {
                    if (Enum.TryParse("PLANAR_" + p, out YUVPlanes plane) && matchPlanes.Contains(plane))
                        return plane;
                    throw new AvisynthException("Illegal plane/channel: " + p);
                }).ToHashSet();
            }
            planeChannelTuples = matchPlanes.Select(p => new PlaneChannelTuple(
                    samplePlaneChannels.First(c => c.EffectivePlane == p),
                    referencePlaneChannels.First(c => c.EffectivePlane == p),
                    samplePlaneChannels.First(c => c.EffectivePlane == (GreyMask && !vi.IsRGB() ? YUVPlanes.PLANAR_Y : p)),
                    referencePlaneChannels.First(c => c.EffectivePlane == (GreyMask && !vi.IsRGB() ? YUVPlanes.PLANAR_Y : p)),
                    inputPlaneChannels.First(c => c.EffectivePlane == p),
                    outputPlaneChannels.First(c => c.EffectivePlane == p)))
                .ToArray();
        }

        protected override VideoFrame GetFrame(int n)
        {
            var input = Child.GetFrame(n, StaticEnv);
            using var sample = Sample.GetFrame(n, StaticEnv);
            using var reference = Reference.GetFrame(n, StaticEnv);
            using var sampleMask = SampleMask?.GetFrame(n, StaticEnv);
            using var referenceMask = ReferenceMask?.GetFrame(n, StaticEnv);

            VideoFrame output;
            if (keepBitDepth && StaticEnv.MakeWritable(input))
                output = input;
            else output = NewVideoFrame(StaticEnv);

            Parallel.ForEach(planeChannelTuples, planeChannel =>
            {
                var sampleHist = new ColorHistogram(Length, planeChannel.Sample, sample, planeChannel.SampleMask, sampleMask, false);
                var referenceHist = new ColorHistogram(Length, planeChannel.Reference, reference, planeChannel.ReferenceMask, referenceMask, LimitedRange);
                var seed = n + (int)planeChannel.Output.EffectivePlane;
                sampleHist.Match(referenceHist, Intensity, Exclude).Apply(planeChannel.Input, planeChannel.Output, input, output, seed);
            });
            return output;
        }

        private record PlaneChannelTuple(
            PlaneChannel Sample, 
            PlaneChannel Reference, 
            PlaneChannel SampleMask, 
            PlaneChannel ReferenceMask, 
            PlaneChannel Input, 
            PlaneChannel Output);
    }
}
