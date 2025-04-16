using System;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Forms.VisualStyles;

namespace AutoOverlay
{
    public readonly struct Space(double x, double y)
    {
        private const double EPSILON = OverlayConst.EPSILON;

        public static Space NaN { get; } = new(double.NaN, double.NaN);

        public static Space Empty { get; } = new(0, 0);
        public static Space One { get; } = new(1, 1);

        public double X { get; } = x;
        public double Y { get; } = y;

        public Space(double xy) : this(xy, xy) { }

        public static implicit operator Space(RectangleD rect) => new(rect.Width, rect.Height);
        public static implicit operator RectangleD(Space space) => RectangleD.FromLTRB(space.X, space.Y, space.X, space.Y);

        public static implicit operator Space(RectangleF rect) => new(rect.Width, rect.Height);
        public static implicit operator Space(Rectangle rect) => new(rect.Width, rect.Height);

        public static implicit operator Space(SizeD size) => new(size.Width, size.Height);
        public static implicit operator Space(Size size) => new(size.Width, size.Height);
        public static implicit operator SizeD(Space space) => new(space.X, space.Y);
        public static explicit operator Size(Space space) => new((int)Math.Round(space.X), (int)Math.Round(space.X));

        public static implicit operator Space(PointF point) => new(point.X, point.Y);
        public static implicit operator Space(Point point) => new(point.X, point.Y);
        public static implicit operator PointF(Space space) => new((float) space.X, (float) space.Y);
        public static explicit operator Point(Space space) => new((int)(space.X), (int)(space.Y));

        public static bool operator ==(Space space1, Space space2) => space1.Equals(space2);
        public static bool operator !=(Space space1, Space space2) => !space1.Equals(space2);

        public static Space operator -(Space space) => new(-space.X, -space.Y);
        public static Space operator +(Space space1, Space space2) => space1.Add(space2);
        public static Space operator -(Space space1, Space space2) => space1.Subtract(space2);
        public static Space operator *(Space space1, Space space2) => space1.Multiply(space2);
        public static Space operator /(Space space1, Space space2) => space1.Divide(space2);

        public static Space operator *(Space space, double real) => new(space.X * real, space.Y * real);
        public static Space operator /(Space space, double real) => new(space.X / real, space.Y / real);

        public double AspectRatio => X / Y;

        public Space Add(Space other) => new(X + other.X, Y + other.Y);
        public Space Subtract(Space other) => new(X - other.X, Y - other.Y);
        public Space Multiply(Space other) => new(X * other.X, Y * other.Y);
        public Space Divide(Space other) => new(X / other.X, Y / other.Y);
        public Space Abs() => Eval(Math.Abs);
        public Space Minus() => Empty.Subtract(this);

        public static Space Max(params Space[] spaces) => new(
            spaces.Select(p => p.X).Aggregate(Math.Max),
            spaces.Select(p => p.Y).Aggregate(Math.Max));
        public static Space Min(params Space[] spaces) => new(
            spaces.Select(p => p.X).Aggregate(Math.Min), 
            spaces.Select(p => p.Y).Aggregate(Math.Min));

        public Space PositiveOnly() => Max(Empty, this);
        public Space NegativeOnly() => Min(Empty, this);

        public Space Inverse() => new(1 / X, 1 / Y);
        public Space Remaining() => new(1 - X, 1 - Y);
        public bool IsNearZero() => Math.Abs(X) < EPSILON && Math.Abs(Y) < EPSILON;
        public Space AbsDiff(Space other) => new(Math.Abs(X - other.X), Math.Abs(Y - other.Y));

        public double SelectX(bool inverse = false) => inverse ? Y : X;
        public double SelectY(bool inverse = false) => inverse ? X : Y;

        public double SelectMax() => Math.Max(X, Y);
        public double SelectMin() => Math.Min(X, Y);

        public Space OnlyX(bool inverse = false) => inverse ? new Space(0, Y) : new Space(X, 0);
        public Space OnlyY(bool inverse = false) => inverse ? new Space(X, 0) : new Space(0, Y);

        public Space Eval(Func<double, double> eval) => new(eval(X), eval(Y));
        public Space Eval(Space second, Func<double, double, double> eval) => new(eval(X, second.X), eval(Y, second.Y));
        public Space Eval(Space second, Space third, Func<double, double, double, double> eval) => new(eval(X, second.X, third.X), eval(Y, second.Y, third.Y));
        public Space Eval(Space second, Space third, Space fourth, Func<double, double, double, double, double> eval) => 
            new(eval(X, second.X, third.X, fourth.X), eval(Y, second.Y, third.Y, fourth.Y));

        public Space Transpose(double aspectRatio = 1) => new(Y * aspectRatio, X / aspectRatio);

        public RectangleD Repeat() => RectangleD.FromLTRB(X, Y, X, Y);

        public bool Equals(Space other)
        {
            return Math.Abs(X - other.X) < EPSILON && Math.Abs(Y - other.Y) < EPSILON;
        }

        public override bool Equals(object obj)
        {
            return obj is Space other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Math.Round(X).GetHashCode() * 397) ^ Math.Round(Y).GetHashCode();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Round(double value) => (int) Math.Round(value);

        public override string ToString()
        {
            return $"Space: {X:F3}, {Y:F3}";
        }
    }
}
