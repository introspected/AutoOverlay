using System;
using System.Drawing;

namespace AutoOverlay
{
    public struct SizeD(double width, double height)
    {
        public static readonly SizeD Empty = new();

        public const double EPSILON = 0.000001;

        public double Width { get; set; } = width;

        public double Height { get; set; } = height;

        public double Area => Width * Height;

        public SizeD Invert()
        {
            return new SizeD(1 / Width, 1 / Height);
        }

        public bool IsEmpty => Empty.Equals(this);

        public double AspectRatio => Width / Height;

        public Size Floor() => new((int)Width, (int)Height);

        public static implicit operator SizeD(Size size)
        {
            return new SizeD(size.Width, size.Height);
        }

        public static implicit operator SizeD(SizeF size)
        {
            return new SizeD(size.Width, size.Height);
        }

        public static explicit operator SizeF(SizeD size)
        {
            return new SizeF((float) size.Width, (float) size.Height);
        }

        public static explicit operator Size(SizeD size)
        {
            return new Size((int)Math.Round(size.Width), (int)Math.Round(size.Height));
        }

        public override bool Equals(object obj)
        {
            if (!(obj is SizeD d)) return false;
            return Math.Abs(Width - d.Width) < EPSILON && Math.Abs(Height - d.Height) < EPSILON;
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
                return (Math.Round(Width, 2).GetHashCode() * 397) ^ Math.Round(Height, 2).GetHashCode();
            }
        }

        public override string ToString() => $"Width={Width}, Height={Height}";
    }
}