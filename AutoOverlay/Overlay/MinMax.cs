using AvsFilterNet;

namespace AutoOverlay.Overlay
{
    public record MinMax(double Min, double Max)
    {
        public MinMax(int depth) : this(0, (1 << depth) - 1)
        {

        }

        public MinMax(PlaneChannel planeChannel, VideoFrame frame) : this(new FramePlane(planeChannel, frame, true))
        {

        }

        public MinMax(FramePlane framePlane) : this(0, 0)
        {
            var minMax = NativeUtils.FindMinMax(framePlane);
            Min = minMax.Item1;
            Max = minMax.Item2;
        }
    }
}
