using System;
using System.Drawing;

namespace AutoOverlay.Overlay
{
    [Serializable]
    public struct RectangleD : IEquatable<RectangleD>
    {

        public const double EPSILON = 0.000001;

        private const long MULT = (int) (1 / EPSILON);

        public static readonly RectangleD Empty;

        public double X { get; set; }

        public double Y { get; set; }

        public double Width { get; set; }

        public double Height { get; set; }

        public double Left => X;

        public double Top => Y;

        public double Right => Width + X;

        public double Bottom => Height + Y;

        public bool IsEmpty => Math.Abs(Width) < EPSILON && Math.Abs(Height) < EPSILON;

        public SizeD Size => new SizeD(Width, Height);

        public RectangleD(double x, double y, double width, double height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public double AspectRatio => Width / Height;

        public RectangleD Scale(double coef)
        {
            return Scale(coef, coef);
        }

        public RectangleD Scale(SizeD coefs)
        {
            return Scale(coefs.Width, coefs.Height);
        }

        public RectangleD Scale(double coefWidth, double coefHeight)
        {
            return new RectangleD(X * coefWidth, Y * coefHeight, Width * coefWidth, Height * coefHeight);
        }

        public static implicit operator RectangleD(Rectangle r)
        {
            return new RectangleD(r.X, r.Y, r.Width, r.Height);
        }

        public static implicit operator RectangleD(RectangleF r)
        {
            return new RectangleD(r.X, r.Y, r.Width, r.Height);
        }

        public static implicit operator RectangleF(RectangleD r)
        {
            return new RectangleF((float) r.X, (float) r.Y, (float) r.Width, (float) r.Height);
        }

        public override bool Equals(object obj)
        {
            if (!(obj is RectangleD)) return false;
            return Equals((RectangleD) obj);
        }
        public bool Equals(RectangleD r)
        {
            return Math.Abs(X - r.X) < EPSILON && Math.Abs(Y - r.Y) < EPSILON &&
                   Math.Abs(Width - r.Width) < EPSILON && Math.Abs(Height - r.Height) < EPSILON;
        }

        public static bool operator ==(RectangleD left, RectangleD right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(RectangleD left, RectangleD right)
        {
            return !left.Equals(right);
        }

        public static RectangleD FromLTRB(double left, double top, double right, double bottom)
        {
            return new RectangleD(left, top, right - left, bottom - top);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = ((long) (MULT * X)).GetHashCode();
                hashCode = (hashCode * 397) ^ ((long)(MULT * Y)).GetHashCode();
                hashCode = (hashCode * 397) ^ ((long)(MULT * Width)).GetHashCode();
                hashCode = (hashCode * 397) ^ ((long)(MULT * Height)).GetHashCode();
                return hashCode;
            }
        }
    }
}
