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
        //public abstract int QuadTopLeftX { get; set; }
        //public abstract int QuadTopLeftY { get; set; }
        //public abstract int QuadTopRightX { get; set; }
        //public abstract int QuadTopRightY { get; set; }
        //public abstract int QuadBottomLeftX { get; set; }
        //public abstract int QuadBottomLeftY { get; set; }
        //public abstract int QuadBottomRightX { get; set; }
        //public abstract int QuadBottomRightY { get; set; }

        public double AspectRatio => Width / (double) Height;
        public int Area => Width * Height;

        public const int CROP_VALUE_COUNT = 10000;
        public const float CROP_VALUE_COUNT_R = CROP_VALUE_COUNT;

        public RectangleF GetRectangle(Size size)
        {
            var crop = GetCrop();
            var scaleWidth = Width / (float) size.Width;
            var scaleHeight = Height / (float) size.Height;
            return new RectangleF(
                X - crop.Left * scaleWidth, 
                Y - crop.Top * scaleHeight,
                Width + (crop.Right + crop.Left) * scaleWidth, 
                Height + (crop.Bottom + crop.Top) * scaleHeight);
        }

        public RectangleF GetCrop()
        {
            return RectangleF.FromLTRB(
                CropLeft / CROP_VALUE_COUNT_R, 
                CropTop / CROP_VALUE_COUNT_R, 
                CropRight / CROP_VALUE_COUNT_R,
                CropBottom / CROP_VALUE_COUNT_R);
        }

        public void SetCrop(RectangleF crop)
        {
            CropLeft = (int) Math.Round(crop.Left * CROP_VALUE_COUNT_R);
            CropTop = (int) Math.Round(crop.Top * CROP_VALUE_COUNT_R);
            CropRight = (int) Math.Round(crop.Right * CROP_VALUE_COUNT_R);
            CropBottom = (int) Math.Round(crop.Bottom * CROP_VALUE_COUNT_R);
        }

        public bool Equals(AbstractOverlayInfo other)
        {
            return other != null && X == other.X && Y == other.Y
                   && Width == other.Width && Height == other.Height
                   && CropLeft == other.CropLeft && CropTop == other.CropTop
                   && CropRight == other.CropRight && CropBottom == other.CropBottom
                   && Angle == other.Angle;
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

        public bool NearlyEquals(AbstractOverlayInfo other, Size size, double tolerance)
        {
            if (Equals(other))
                return true;
            var rect1 = GetRectangle(size);
            var rect2 = other.GetRectangle(size);
            var intersect = RectangleF.Intersect(rect1, rect2);
            var union = RectangleF.Union(rect1, rect2);
            return Math.Abs((union.Width * union.Height) / (intersect.Width * intersect.Height)) - 1 <= tolerance;
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
        }
    }
}
