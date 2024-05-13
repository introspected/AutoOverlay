using AvsFilterNet;

namespace AutoOverlay.Overlay
{
    public class MinMax
    {
        public double Min { get; private set; }
        public double Max { get; private set; }

        public MinMax(int depth)
        {
            Min = 0;
            Max = (1 << depth) - 1;
        }

        public MinMax(PlaneChannel planeChannel, VideoFrame frame)
        {
            var framePlane = new FramePlane(planeChannel, frame, true);
            var minMax = NativeUtils.FindMinMax(framePlane);
            Min = minMax.Item1;
            Max = minMax.Item2;
        }
    }
}
