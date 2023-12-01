using System;
using System.Drawing;
using AutoOverlay.Overlay;

namespace AutoOverlay
{
    public sealed class LegacyOverlayInfo : IComparable<LegacyOverlayInfo>, IEquatable<LegacyOverlayInfo>, ICloneable
    {
        public static readonly LegacyOverlayInfo EMPTY = new()
        {
            Diff = double.MaxValue
        };

        public double Diff { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Angle { get; set; }
        public Warp Warp { get; set; } = Warp.Empty;
        public int Width { get; set; }
        public int Height { get; set; }
        public int CropLeft { get; set; }
        public int CropTop { get; set; }
        public int CropRight { get; set; }
        public int CropBottom { get; set; }
        public int BaseWidth { get; set; }
        public int BaseHeight { get; set; }
        public int SourceWidth { get; set; }
        public int SourceHeight { get; set; }
        public double Comparison { get; set; } = 2;

        public bool Modified { get; set; }

        public bool ProbablyChanged { get; set; }

        public bool KeyFrame { get; set; }

        public string Message { get; set; }

        public string Branch { get; set; } = string.Empty;

        public int FrameNumber { get; set; } // zero based

        private static readonly OverlayStatFormat format = new OverlayStatFormat(OverlayUtils.OVERLAY_FORMAT_VERSION);

        object ICloneable.Clone() => MemberwiseClone();

        public LegacyOverlayInfo Clone() => (LegacyOverlayInfo) MemberwiseClone();

        public LegacyOverlayInfo Shrink()
        {
            return Shrink(new Size(SourceWidth, SourceHeight), new Size(BaseWidth, BaseHeight));
        }

        public LegacyOverlayInfo Shrink(Size srcSize, Size overSize)
        {
            var info = Clone();
            var excess = Rectangle.FromLTRB(
                Math.Max(0, -info.X),
                Math.Max(0, -info.Y),
                Math.Max(0, info.Width + info.X - srcSize.Width),
                Math.Max(0, info.Height + info.Y - srcSize.Height));
            var widthCoef = (double)overSize.Width / info.Width;
            var heightCoef = (double)overSize.Height / info.Height;
            info.Width -= excess.Left + excess.Right;
            info.Height -= excess.Top + excess.Bottom;
            info.X = Math.Max(0, info.X);
            info.Y = Math.Max(0, info.Y);
            var crop = info.GetCrop();
            info.SetCrop(RectangleD.FromLTRB(
                crop.Left + excess.Left * widthCoef,
                crop.Top + excess.Top * heightCoef,
                crop.Right + excess.Right * widthCoef,
                crop.Bottom + excess.Bottom * heightCoef));
            return info;
        }

        public LegacyOverlayInfo Invert()
        {
            var info = Invert(new Size(SourceWidth, SourceHeight), new Size(BaseWidth, BaseHeight));
            info.BaseWidth = SourceWidth;
            info.BaseHeight = SourceHeight;
            info.SourceWidth = BaseWidth;
            info.SourceHeight = BaseHeight;
            return info;
        }

        public LegacyOverlayInfo Invert(Size srcSize, Size overSize)
        {
            var rect = GetRectangle(overSize);
            var info = Clone();
            var invertedRect = new RectangleD(
                -rect.X,
                -rect.Y,
                srcSize.Width,
                srcSize.Height
            );
            info.Angle = -Angle;
            info.X = (int)Math.Ceiling(invertedRect.X);
            info.Y = (int)Math.Ceiling(invertedRect.Y);
            info.Width = (int)Math.Floor(invertedRect.Right) - info.X;
            info.Height = (int)Math.Floor(invertedRect.Bottom) - info.Y;
            info.SetCrop(RectangleD.FromLTRB(
                info.X - invertedRect.X,
                info.Y - invertedRect.Y,
                invertedRect.Right - info.Width - info.X,
                invertedRect.Bottom - info.Height - info.Y
            ));
            return info.Resize(srcSize, invertedRect.Size, rect.Size, overSize);
        }

        public LegacyOverlayInfo Resize(SizeD newUnionSize)
        {
            var oldUnionSize = GetUnionSize();
            var coefWidth = newUnionSize.Width / oldUnionSize.Width;
            var coefHeight = newUnionSize.Height / oldUnionSize.Height;

            return Resize(new Size(BaseWidth, BaseHeight));
        }

        public LegacyOverlayInfo Resize(Size newSrcSize, Size newOverSize)
        {
            var info = Resize(new Size(BaseWidth, BaseHeight), newOverSize, new Size(SourceWidth, SourceHeight),
                newSrcSize);
            info.BaseWidth = newOverSize.Width;
            info.BaseHeight = newOverSize.Height;
            info.SourceWidth = newSrcSize.Width;
            info.SourceHeight = newSrcSize.Height;
            return info;
        }

        public LegacyOverlayInfo Resize(SizeD oldOverSize, SizeD newOverSize, SizeD oldSrcSize, SizeD newSrcSize)
        {
            var rect = GetRectangle(oldOverSize);
            var coefWidth = newSrcSize.Width / oldSrcSize.Width;
            var coefHeight = newSrcSize.Height / oldSrcSize.Height;
            rect = new RectangleD(
                rect.X * coefWidth,
                rect.Y * coefHeight,
                rect.Width * coefWidth,
                rect.Height * coefHeight
            );
            var info = Clone();
            info.Warp = Warp.Scale(coefWidth, coefHeight);
            coefWidth = newOverSize.Width / rect.Width;
            coefHeight = newOverSize.Height / rect.Height;
            info.SourceWidth = (int)Math.Floor(newSrcSize.Width);
            info.SourceHeight = (int)Math.Floor(newSrcSize.Height);
            info.X = (int)Math.Ceiling(rect.X);
            info.Y = (int)Math.Ceiling(rect.Y);
            info.Width = (int)Math.Floor(rect.Right) - info.X;
            info.Height = (int)Math.Floor(rect.Bottom) - info.Y;
            info.SetCrop(RectangleD.FromLTRB(
                (info.X - rect.X) * coefWidth,
                (info.Y - rect.Y) * coefHeight,
                (rect.Right - info.Width - info.X) * coefWidth,
                (rect.Bottom - info.Height - info.Y) * coefHeight
            ));
            return info;
        }

        public string DisplayInfo()
        {
            var crop = GetCrop();
            var key = KeyFrame ? "[KeyFrame]" : "";
            return $"Frame: {FrameNumber} {key}\n" +
                   $"Size: {Width}x{Height} ({GetAspectRatio():F5}:1)\n" +
                   $"Crop: {crop.Left:0.###}:{crop.Top:0.###}:{crop.Right:0.###}:{crop.Bottom:0.###}\n" +
                   $"Warp: {Warp}\n" +
                   $"X: {X} Y: {Y} Angle: {Angle / 100.0:F2}\n" +
                   $"Diff: {Diff:F5}\n{Message ?? string.Empty}";
        }

        public int CompareTo(LegacyOverlayInfo other)
        {
            if (ReferenceEquals(this, other)) return 0;
            if (ReferenceEquals(null, other)) return 1;
            var compareFrame = FrameNumber.CompareTo(other.FrameNumber);
            if (compareFrame != 0)
                return compareFrame;
            return Diff.CompareTo(other.Diff);
        }

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
            CropLeft = (int)Math.Abs((1 - rect.X + (int)rect.X) * scaleWidth * CROP_VALUE_COUNT_R);
            CropRight = (int)Math.Abs((rect.Right - (int)rect.Right) * scaleWidth * CROP_VALUE_COUNT_R);
            CropTop = (int)Math.Abs((1 - rect.Y + (int)rect.Y) * scaleHeight * CROP_VALUE_COUNT_R);
            CropBottom = (int)Math.Abs((rect.Bottom - (int)rect.Bottom) * scaleHeight * CROP_VALUE_COUNT_R);
            Width = (int)Math.Round(rect.Width - (CropLeft + CropRight) / CROP_VALUE_COUNT_R);
            Height = (int)Math.Round(rect.Height - (CropTop + CropBottom) / CROP_VALUE_COUNT_R);
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

        public LegacyOverlayInfo SetIntCrop(Rectangle crop)
        {
            CropLeft = crop.Left;
            CropTop = crop.Top;
            CropRight = crop.Right;
            CropBottom = crop.Bottom;
            return this;
        }

        public LegacyOverlayInfo SetCrop(RectangleD crop)
        {
            CropLeft = IntCrop(crop.Left);
            CropTop = IntCrop(crop.Top);
            CropRight = IntCrop(crop.Right);
            CropBottom = IntCrop(crop.Bottom);
            return this;
        }

        public static int IntCrop(double crop)
        {
            return (int)Math.Round(crop * CROP_VALUE_COUNT_R);
        }

        public bool Equals(LegacyOverlayInfo other)
        {
            return other != null && X == other.X && Y == other.Y
                   && Width == other.Width && Height == other.Height
                   && CropLeft == other.CropLeft && CropTop == other.CropTop
                   && CropRight == other.CropRight && CropBottom == other.CropBottom
                   && Angle == other.Angle && Warp.Equals(other.Warp)
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
            if (!(obj is LegacyOverlayInfo)) throw new InvalidOperationException();
            return Equals((LegacyOverlayInfo)obj);
        }

        public double Compare(LegacyOverlayInfo other)
        {
            if (Equals(other))
                return 1;
            var rect1 = GetRectangle();
            var rect2 = other.GetRectangle();
            var intersect = RectangleF.Intersect(rect1, rect2);
            var union = RectangleF.Union(rect1, rect2);
            return (double)(intersect.Width * intersect.Height) / (union.Width * union.Height);
        }

        public double Compare(LegacyOverlayInfo other, Size size)
        {
            if (Equals(other))
                return 1;
            var rect1 = GetRectangle(size);
            var rect2 = other.GetRectangle(size);
            var intersect = RectangleF.Intersect(rect1, rect2);
            var union = RectangleF.Union(rect1, rect2);
            return (double)(intersect.Width * intersect.Height) / (union.Width * union.Height);
        }

        public bool NearlyEquals(LegacyOverlayInfo other, double tolerance)
        {
            if (other == null)
                return false;
            var comparison = Compare(other);
            return 1 - comparison <= tolerance;
        }

        public bool NearlyEquals(LegacyOverlayInfo other, Size size, double tolerance)
        {
            if (other == null)
                return false;
            var comparison = Compare(other, size);
            return 1 - comparison <= tolerance;
        }

        public void CopyFrom(LegacyOverlayInfo other)
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
            Warp = other.Warp;
            BaseWidth = other.BaseWidth;
            BaseHeight = other.BaseHeight;
            SourceWidth = other.SourceWidth;
            SourceHeight = other.SourceHeight;
        }

        public override string ToString()
        {
            return $"{nameof(Diff)}: {Diff}, {nameof(X)}: {X}, {nameof(Y)}: {Y}, {nameof(Angle)}: {Angle}, " +
                   $"{nameof(Width)}: {Width}, {nameof(Height)}: {Height}, {nameof(Warp)}: {Warp}, " +
                   $"{nameof(CropLeft)}: {CropLeft}, {nameof(CropTop)}: {CropTop}, {nameof(CropRight)}: {CropRight}, {nameof(CropBottom)}: {CropBottom}, ";
        }

        public OverlayInfo Convert()
        {
            var rectangle = GetRectangle();
            var scale = new SizeD(rectangle.Width / BaseWidth, rectangle.Height / BaseHeight);
            return new OverlayInfo
            {
                Placement = new PointF((float) rectangle.X, (float) rectangle.Y),
                SourceSize = SourceWidth == 0 ? SizeD.Empty : new SizeD(SourceWidth, SourceHeight),
                OverlaySize = rectangle.Size,
                Angle = Angle / 100f,
                OverlayWarp = Warp.Scale(scale),
                Diff = Diff,
                FrameNumber = FrameNumber
            };
        }
    }
}