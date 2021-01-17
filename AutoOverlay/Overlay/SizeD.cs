using System;
using System.Drawing;

namespace AutoOverlay.Overlay
{
    public struct SizeD
    {
        public const double EPSILON = 0.000001;

        public double Width { get; set; }

        public double Height { get; set; }

        public double Area => Width * Height;

        public SizeD(double width, double height)
        {
            Width = width;
            Height = height;
        }

        public SizeD Invert()
        {
            return new SizeD(1 / Width, 1 / Height);
        }

        public double AspectRatio => Width / Height;

        public static implicit operator SizeD(Size size)
        {
            return new SizeD(size.Width, size.Height);
        }

        public static implicit operator SizeD(SizeF size)
        {
            return new SizeD(size.Width, size.Height);
        }

        public static implicit operator SizeF(SizeD size)
        {
            return new SizeF((float) size.Width, (float) size.Height);
        }

        public override bool Equals(object obj)
        {
            if (!(obj is SizeD)) return false;
            var s = (SizeD) obj;
            return Math.Abs(Width - s.Width) < EPSILON && Math.Abs(Height - s.Height) < EPSILON;
        }

        public static bool operator ==(SizeD left, SizeD right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SizeD left, SizeD right)
        {
            return !left.Equals(right);
        }
        
        public override int GetHashCode()
        {
            unchecked
            {
                return (Width.GetHashCode() * 397) ^ Height.GetHashCode();
            }
        }
    }
}