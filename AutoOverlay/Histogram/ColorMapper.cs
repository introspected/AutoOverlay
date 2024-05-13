using System;
using System.Linq;
using System.Threading.Tasks;
using AvsFilterNet;
using MathNet.Numerics.Interpolation;

namespace AutoOverlay.Histogram
{
    public sealed class ColorMapper
    {
        private readonly IInterpolation averageInterpolation;
        private readonly double min;
        private readonly double max;

        public ColorMapper(IInterpolation averageInterpolation, double min, double max)
        {
            this.averageInterpolation = averageInterpolation;
            this.min = min;
            this.max = max;
        }

        public void Apply(PlaneChannel inPlaneChannel, PlaneChannel outPlaneChannel, VideoFrame input, VideoFrame output, int? seed)
        {
            var n = 2;
            var inPlanes = new FramePlane(inPlaneChannel, input, true).Split(n);
            var outPlanes = new FramePlane(outPlaneChannel, output, false).Split(n);

            Parallel.ForEach(Enumerable.Range(0, n)
                    .Select(i => new { inPlane = inPlanes[i], outPlane = outPlanes[i], Num = i }),
                tuple => NativeUtils.ApplyHistogram(tuple.inPlane, tuple.outPlane, averageInterpolation, min, max, seed << tuple.Num));
        }
    }
}
