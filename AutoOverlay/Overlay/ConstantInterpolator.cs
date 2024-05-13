using MathNet.Numerics.Interpolation;

namespace AutoOverlay.Overlay
{
    public record ConstantInterpolator(double Value) : IInterpolation
    {
        public double Interpolate(double t) => Value;

        public double Differentiate(double t) => Value;
        public double Differentiate2(double t) => Value;

        public double Integrate(double t) => Value;
        public double Integrate(double a, double b) => Value;

        public bool SupportsDifferentiation => false;
        public bool SupportsIntegration => false;
    }
}
