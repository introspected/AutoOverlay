using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;

namespace AutoOverlay
{
    [Serializable]
    public struct RectangleD
    {

        public const double EPSILON = 0.000001;

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
            var r = (RectangleD) obj;
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
                var hashCode = X.GetHashCode();
                hashCode = (hashCode * 397) ^ Y.GetHashCode();
                hashCode = (hashCode * 397) ^ Width.GetHashCode();
                hashCode = (hashCode * 397) ^ Height.GetHashCode();
                return hashCode;
            }
        }
    }
}
