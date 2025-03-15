using AvsFilterNet;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace AutoOverlay.Overlay
{
    [Serializable]
    public readonly struct RectangleD : IEquatable<RectangleD>
    {
        public static readonly double EPSILON = OverlayConst.EPSILON;

        private static readonly long MULT = (int) (1 / EPSILON);

        public static readonly RectangleD Empty;

        public static readonly RectangleD One = FromLTRB(1, 1, 1, 1);

        public double X { get; }

        public double Y { get; }

        public double Width { get; }

        public double Height { get; }

        public double Left => X;

        public double Top => Y;

        public double Right => Width + X;

        public double Bottom => Height + Y;

        public bool IsEmpty => ltrb 
            ? Math.Abs(Left) < EPSILON && Math.Abs(Top) < EPSILON && Math.Abs(Right) < EPSILON && Math.Abs(Bottom) < EPSILON 
            : Math.Abs(Width) < EPSILON && Math.Abs(Height) < EPSILON;

        public SizeD Size => new(Width, Height);

        private readonly bool ltrb;

        public RectangleD(Space location, SizeD size) : this(location.X, location.Y, size.Width, size.Height)
        {
        }

        public RectangleD(double x, double y, double width, double height)
            : this(x, y, width, height, false)
        {
        }

        private RectangleD(double x, double y, double width, double height, bool ltrb)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
            this.ltrb = ltrb;
        }

        public double AspectRatio => Width / Height;

        public Space Location => new(X, Y);

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
            return new RectangleD(X * coefWidth, Y * coefHeight, Width * coefWidth, Height * coefHeight, ltrb);
        }

        public static RectangleD Union(RectangleD a, RectangleD b)
        {
            var x1 = Math.Min(a.X, b.X);
            var x2 = Math.Max(a.X + a.Width, b.X + b.Width);
            var y1 = Math.Min(a.Y, b.Y);
            var y2 = Math.Max(a.Y + a.Height, b.Y + b.Height);

            return new RectangleD(x1, y1, x2 - x1, y2 - y1);
        }

        public static RectangleD Intersect(RectangleD a, RectangleD b)
        {
            var x1 = Math.Max(a.X, b.X);
            var x2 = Math.Min(a.X + a.Width, b.X + b.Width);
            var y1 = Math.Max(a.Y, b.Y);
            var y2 = Math.Min(a.Y + a.Height, b.Y + b.Height);

            return x2 >= x1 && y2 >= y1 ? new RectangleD(x1, y1, x2 - x1, y2 - y1) : Empty;
        }

        public static OverlayDifferenceD Difference(RectangleD a, RectangleD b, bool onlyEmpty)
        {
            return new OverlayDifferenceD(a, b, onlyEmpty);
        }

        public bool Contains(double x, double y)
        {
            return X <= x && x <= X + Width && Y <= y && y <= Y + Height;
        }

        public bool Contains(Space point)
        {
            return Contains(point.X, point.Y);
        }

        public RectangleD Scale(Space coef) => ltrb
            ? FromLTRB(Left * coef.X, Top * coef.Y, Right * coef.X, Bottom * coef.Y)
            : new(X * coef.X, Y * coef.Y, Width * coef.X, Height * coef.Y);

        public RectangleD Offset(Space space) => new(X + space.X, Y + space.Y, Width, Height);

        public RectangleD Offset(double x, double y) => new(X + x, Y + y, Width, Height);

        public RectangleD Eval(Func<double, double> eval) => FromLTRB(eval(Left), eval(Top), eval(Right), eval(Bottom));

        public RectangleD Eval(RectangleD other, Func<double, double, double> eval) => 
            FromLTRB(eval(Left, other.Left), eval(Top, other.Top), eval(Right, other.Right), eval(Bottom, other.Bottom));

        public RectangleD Eval(RectangleD second, RectangleD third, Func<double, double, double, double> eval) =>
            FromLTRB(eval(Left, second.Left, third.Left), 
                eval(Top, second.Top, third.Top), 
                eval(Right, second.Right, third.Right), 
                eval(Bottom, second.Bottom, third.Bottom));

        public Rectangle Floor() => Rectangle.FromLTRB(
            (int) Math.Ceiling(Math.Round(Left, OverlayConst.FRACTION)),
            (int) Math.Ceiling(Math.Round(Top, OverlayConst.FRACTION)),
            (int) Math.Floor(Math.Round(Right, OverlayConst.FRACTION)),
            (int) Math.Floor(Math.Round(Bottom, OverlayConst.FRACTION)));

        public Space AsSpace() => new(Width, Height);

        public Space LeftTop => new(Left, Top);
        public Space RightTop => new(Right, Top);
        public Space RightBottom => new(Right, Bottom);
        public Space LeftBottom => new(Left, Bottom);

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

        public static explicit operator Rectangle(RectangleD r)
        {
            if (new[] { r.Left, r.Top, r.Right, r.Bottom }.Any(p => p % 1 != 0))
            {
                throw new AvisynthException("Only integer values allowed");
            }
            return new Rectangle((int) r.X, (int) r.Y, (int) r.Width, (int) r.Height);
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
            return new RectangleD(left, top, right - left, bottom - top, true);
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

        public RectangleD Expand(double aspectRatio)
        {
            if (AspectRatio > aspectRatio)
            {
                var height = Width / aspectRatio;
                return new RectangleD(X, Y - (height - Height) / 2, Width, height);
            }
            var width = Height * aspectRatio;
            return new RectangleD(X - (width - Width) / 2, Y, width, Height);
        }

        public RectangleD Crop(RectangleD crop) => Crop(crop.Left, crop.Top, crop.Right, crop.Bottom);

        public RectangleD Crop(double left, double top, double right, double bottom) =>
            FromLTRB(Left + left, Top + top, Right - right, Bottom - bottom);

        public RectangleD Crop(double aspectRatio) => Crop(aspectRatio, AsSpace() / 2);

        public RectangleD Crop(double aspectRatio, Space center)
        {
            if (AspectRatio > aspectRatio)
            {
                var newWidth = Height * aspectRatio;
                return new RectangleD(Math.Max(X, Math.Min(Right - newWidth, center.X - newWidth / 2)), Y, newWidth, Height);
            }
            var newHeight = Width / aspectRatio;
            return new RectangleD(X, Math.Max(Y, Math.Min(Bottom - newHeight, center.Y - newHeight / 2)), Width, newHeight);
        }


        public override string ToString() => IsEmpty ? "Empty" : ltrb
            ? $"Left={Left}, Top={Top}, Right={Right}, Bottom={Bottom}"
            : $"X={X}, Y={Y}, Width={Width}, Height={Height}";

        public Space Joined => new(Left + Right, Top + Bottom);

        public RectangleD TakeHorizontal(bool yes = true) =>
            FromLTRB(yes ? Left : 0, yes ? 0 : Top, yes ? Right : 0, yes ? 0 : Bottom);

        public RectangleD TakeVertical(bool yes = true) => TakeHorizontal(!yes);

        public IEnumerable<double> LTRB()
        {
            yield return Left;
            yield return Top;
            yield return Right;
            yield return Bottom;
        }

        public RectangleD Union(RectangleD other)
        {
            return Union(this, other);
        }

        public RectangleD Intersect(RectangleD other)
        {
            return Intersect(this, other);
        }

        public double Area => Width * Height;

        public RectangleD Remaining() => FromLTRB(1 - Left, 1 - Top, 1 - Right, 1 - Bottom);
    }
}
