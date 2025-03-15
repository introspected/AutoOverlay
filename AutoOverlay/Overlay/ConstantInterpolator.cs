namespace AutoOverlay.Overlay
{
    public record ConstantInterpolator(double Value) : IInterpolator
    {
        public double Interpolate(double t) => Value;
    }
}
