using System;
using System.Drawing;

namespace AutoOverlay
{
    public abstract class AbstractOverlayInfo : IEquatable<AbstractOverlayInfo>
    {
        public abstract double Diff { get; set; }
        public abstract int X { get; set; }
        public abstract int Y { get; set; }
        public abstract int Angle { get; set; } //-36000-36000 : 100
        public abstract int Width { get; set; }
        public abstract int Height { get; set; }
        public abstract int CropLeft { get; set; }
        public abstract int CropTop { get; set; }
        public abstract int CropRight { get; set; }
        public abstract int CropBottom { get; set; }
        public abstract int BaseWidth { get; set; }
        public abstract int BaseHeight { get; set; }
        public abstract int SourceWidth { get; set; }
        public abstract int SourceHeight { get; set; }
        public abstract double Comparison { get; set; }
        public double Right => Width + X;
        public double Bottom => Height + Y;


        public double GetAspectRatio(Size overSize)
        {
            var rect = GetRectangle(overSize);
            return rect.Width / rect.Height;
        }

        public double GetAspectRatio()
        {
            var rect = GetRectangle();
            return rect.Width / rect.Height;
        }

        public int Area => Width * Height;

        public const int CROP_VALUE_COUNT = 10000;
        public const double CROP_VALUE_COUNT_R = CROP_VALUE_COUNT;

        public RectangleD GetRectangle()
        {
            return GetRectangle(new SizeD(BaseWidth, BaseHeight));
        }

        public RectangleD GetRectangle(SizeD overlaySize)
        {
            var crop = GetCrop();
            var scaleWidth = Width / (overlaySize.Width - crop.Left - crop.Right);
            var scaleHeight = Height / (overlaySize.Height - crop.Top - crop.Bottom);
            return new RectangleD(
                X - crop.Left * scaleWidth, 
                Y - crop.Top * scaleHeight,
                Width + (crop.Right + crop.Left) * scaleWidth, 
                Height + (crop.Bottom + crop.Top) * scaleHeight);
        }

        public SizeD GetUnionSize()
        {
            var over = GetRectangle();
            var width = SourceWidth + Math.Max(-X, 0) + Math.Max(over.Width + X - SourceWidth, 0);
            var height = SourceHeight + Math.Max(-Y, 0) + Math.Max(over.Height + Y - SourceHeight, 0);
            return new SizeD(width, height);
        }

        public void SetRectangle(SizeF size, RectangleF rect)
        {
            var scaleWidth = size.Width / rect.Width;
            var scaleHeight = size.Height / rect.Height;
            CropLeft = (int) Math.Abs((1 - rect.X + (int) rect.X) * scaleWidth * CROP_VALUE_COUNT_R);
            CropRight = (int) Math.Abs((rect.Right - (int) rect.Right) * scaleWidth * CROP_VALUE_COUNT_R);
            CropTop = (int) Math.Abs((1 - rect.Y + (int) rect.Y) * scaleHeight * CROP_VALUE_COUNT_R);
            CropBottom = (int) Math.Abs((rect.Bottom - (int) rect.Bottom) * scaleHeight * CROP_VALUE_COUNT_R);
            Width = (int) Math.Round(rect.Width - (CropLeft + CropRight) / CROP_VALUE_COUNT_R);
            Height = (int) Math.Round(rect.Height - (CropTop + CropBottom) / CROP_VALUE_COUNT_R);
        }

        public RectangleD GetCrop()
        {
            return RectangleD.FromLTRB(
                CropLeft / CROP_VALUE_COUNT_R, 
                CropTop / CROP_VALUE_COUNT_R, 
                CropRight / CROP_VALUE_COUNT_R,
                CropBottom / CROP_VALUE_COUNT_R);
        }

        public Rectangle GetIntCrop()
        {
            return Rectangle.FromLTRB(
                CropLeft,
                CropTop,
                CropRight,
                CropBottom);
        }

        public AbstractOverlayInfo SetIntCrop(Rectangle crop)
        {
            CropLeft = crop.Left;
            CropTop = crop.Top;
            CropRight = crop.Right;
            CropBottom = crop.Bottom;
            return this;
        }

        public AbstractOverlayInfo SetCrop(RectangleD crop)
        {
            CropLeft = IntCrop(crop.Left);
            CropTop = IntCrop(crop.Top);
            CropRight = IntCrop(crop.Right);
            CropBottom = IntCrop(crop.Bottom);
            return this;
        }

        public static int IntCrop(double crop)
        {
            return (int) Math.Round(crop * CROP_VALUE_COUNT_R);
        }

        public virtual bool Equals(AbstractOverlayInfo other)
        {
            return other != null && X == other.X && Y == other.Y
                   && Width == other.Width && Height == other.Height
                   && CropLeft == other.CropLeft && CropTop == other.CropTop
                   && CropRight == other.CropRight && CropBottom == other.CropBottom
                   && Angle == other.Angle
                   && BaseWidth == other.BaseWidth && BaseHeight == other.BaseHeight 
                   && SourceWidth == other.SourceWidth && SourceHeight == other.SourceHeight;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = X;
                hashCode = (hashCode * 397) ^ Y;
                hashCode = (hashCode * 397) ^ Width;
                hashCode = (hashCode * 397) ^ Height;
                hashCode = (hashCode * 397) ^ CropLeft;
                hashCode = (hashCode * 397) ^ CropTop;
                hashCode = (hashCode * 397) ^ CropRight;
                hashCode = (hashCode * 397) ^ CropBottom;
                hashCode = (hashCode * 397) ^ Angle;
                hashCode = (hashCode * 397) ^ BaseWidth;
                hashCode = (hashCode * 397) ^ BaseHeight;
                hashCode = (hashCode * 397) ^ SourceWidth;
                hashCode = (hashCode * 397) ^ SourceHeight;
                return hashCode;
            }
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (!(obj is AbstractOverlayInfo)) return false;
            return Equals((AbstractOverlayInfo)obj);
        }

        public double Compare(AbstractOverlayInfo other)
        {
            if (Equals(other))
                return 1;
            var rect1 = GetRectangle();
            var rect2 = other.GetRectangle();
            var intersect = RectangleF.Intersect(rect1, rect2);
            var union = RectangleF.Union(rect1, rect2);
            return (double)(intersect.Width * intersect.Height) / (union.Width * union.Height);
        }

        public double Compare(AbstractOverlayInfo other, Size size)
        {
            if (Equals(other))
                return 1;
            var rect1 = GetRectangle(size);
            var rect2 = other.GetRectangle(size);
            var intersect = RectangleF.Intersect(rect1, rect2);
            var union = RectangleF.Union(rect1, rect2);
            return (double) (intersect.Width * intersect.Height) / (union.Width * union.Height);
        }

        public bool NearlyEquals(AbstractOverlayInfo other, double tolerance)
        {
            if (other == null)
                return false;
            var comparison = Compare(other);
            return 1 - comparison <= tolerance;
        }

        public bool NearlyEquals(AbstractOverlayInfo other, Size size, double tolerance)
        {
            if (other == null)
                return false;
            var comparison = Compare(other, size);
            return 1 - comparison <= tolerance;
        }

        public void CopyFrom(AbstractOverlayInfo other)
        {
            X = other.X;
            Y = other.Y;
            Width = other.Width;
            Height = other.Height;
            CropLeft = other.CropLeft;
            CropTop = other.CropTop;
            CropRight = other.CropRight;
            CropBottom = other.CropBottom;
            Angle = other.Angle;
            BaseWidth = other.BaseWidth;
            BaseHeight = other.BaseHeight;
            SourceWidth = other.SourceWidth;
            SourceHeight = other.SourceHeight;
        }

        public override string ToString()
        {
            return $"{nameof(Diff)}: {Diff}, {nameof(X)}: {X}, {nameof(Y)}: {Y}, {nameof(Angle)}: {Angle}, " +
                   $"{nameof(Width)}: {Width}, {nameof(Height)}: {Height}, " +
                   $"{nameof(CropLeft)}: {CropLeft}, {nameof(CropTop)}: {CropTop}, {nameof(CropRight)}: {CropRight}, {nameof(CropBottom)}: {CropBottom}, ";
        }
    }
}
