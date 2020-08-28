using System;
using System.Drawing;
using System.IO;
using AutoOverlay.Stat;
using AvsFilterNet;

namespace AutoOverlay
{
    public class OverlayInfo : AbstractOverlayInfo, ICloneable, IComparable<OverlayInfo>
    {
        public static readonly OverlayInfo EMPTY = new OverlayInfo
        {
            Diff = double.MaxValue
        };

        public override double Diff { get; set; }
        public override int X { get; set; }
        public override int Y { get; set; }
        public override int Angle { get; set; }
        public override int Width { get; set; }
        public override int Height { get; set; }
        public override int CropLeft { get; set; }
        public override int CropTop { get; set; }
        public override int CropRight { get; set; }
        public override int CropBottom { get; set; }
        public override int BaseWidth { get; set; }
        public override int BaseHeight { get; set; }
        public override int SourceWidth { get; set; }
        public override int SourceHeight { get; set; }
        public override double Comparison { get; set; } = 2;

        public string Branch { get; set; } = string.Empty;

        public int FrameNumber { get; set; }

        private static readonly OverlayStatFormat format = new OverlayStatFormat(3);

        object ICloneable.Clone() => MemberwiseClone();

        public OverlayInfo Clone() => (OverlayInfo)MemberwiseClone();

        public OverlayInfo Shrink()
        {
            return Shrink(new Size(SourceWidth, SourceHeight), new Size(BaseWidth, BaseHeight));
        }

        public OverlayInfo Shrink(Size srcSize, Size overSize)
        {
            var info = Clone();
            var excess = Rectangle.FromLTRB(
                Math.Max(0, -info.X),
                Math.Max(0, -info.Y),
                Math.Max(0, info.Width + info.X - srcSize.Width),
                Math.Max(0, info.Height + info.Y - srcSize.Height));
            var widthCoef = (double) overSize.Width / info.Width;
            var heightCoef = (double) overSize.Height / info.Height;
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

        public OverlayInfo Invert()
        {
            var info = Invert(new Size(SourceWidth, SourceHeight), new Size(BaseWidth, BaseHeight));
            info.BaseWidth = SourceWidth;
            info.BaseHeight = SourceHeight;
            info.SourceWidth = BaseWidth;
            info.SourceHeight = BaseHeight;
            return info;
        }

        public OverlayInfo Invert(Size srcSize, Size overSize)
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
            info.X = (int) Math.Ceiling(invertedRect.X);
            info.Y = (int) Math.Ceiling(invertedRect.Y);
            info.Width = (int) Math.Floor(invertedRect.Right) - info.X;
            info.Height = (int) Math.Floor(invertedRect.Bottom) - info.Y;
            info.SetCrop(RectangleD.FromLTRB(
                info.X - invertedRect.X,
                info.Y - invertedRect.Y,
                invertedRect.Right - info.Width - info.X,
                invertedRect.Bottom - info.Height - info.Y
            ));
            return info.Resize(srcSize, invertedRect.Size, rect.Size, overSize);
        }

        public OverlayInfo Resize(SizeD newUnionSize)
        {
            var oldUnionSize = GetUnionSize();
            var coefWidth = newUnionSize.Width / oldUnionSize.Width;
            var coefHeight = newUnionSize.Height / oldUnionSize.Height;

            return Resize( new Size(BaseWidth, BaseHeight));
        }

        public OverlayInfo Resize(Size newSrcSize, Size newOverSize)
        {
            var info = Resize(new Size(BaseWidth, BaseHeight), newOverSize, new Size(SourceWidth, SourceHeight), newSrcSize);
            info.BaseWidth = newOverSize.Width;
            info.BaseHeight = newOverSize.Height;
            info.SourceWidth = newSrcSize.Width;
            info.SourceHeight = newSrcSize.Height;
            return info;
        }

        public OverlayInfo Resize(SizeD oldOverSize, SizeD newOverSize, SizeD oldSrcSize, SizeD newSrcSize)
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
            coefWidth = newOverSize.Width / rect.Width;
            coefHeight = newOverSize.Height / rect.Height;
            info.SourceWidth = (int) Math.Floor(newSrcSize.Width);
            info.SourceHeight = (int) Math.Floor(newSrcSize.Height);
            info.X = (int) Math.Ceiling(rect.X);
            info.Y = (int) Math.Ceiling(rect.Y);
            info.Width = (int) Math.Floor(rect.Right) - info.X;
            info.Height = (int) Math.Floor(rect.Bottom) - info.Y;
            info.SetCrop(RectangleD.FromLTRB(
                (info.X - rect.X) * coefWidth,
                (info.Y - rect.Y) * coefHeight,
                (rect.Right - info.Width - info.X) * coefWidth,
                (rect.Bottom - info.Height - info.Y) * coefHeight
            ));
            return info;
        }

        public void ToFrame(VideoFrame frame)
        {
            unsafe
            {
                using (var stream = new UnmanagedMemoryStream((byte*)frame.GetWritePtr().ToPointer(),
                    frame.GetRowSize(), frame.GetRowSize(), FileAccess.Write))
                using (var writer = new BinaryWriter(stream))
                {
                    writer.Write(nameof(OverlayInfo));
                    format.WriteFrame(writer, this);
                }
            }
        }

        public static OverlayInfo FromFrame(VideoFrame frame)
        {
            unsafe
            {
                using (var stream = new UnmanagedMemoryStream((byte*)frame.GetReadPtr().ToPointer(),
                    frame.GetRowSize(), frame.GetRowSize(), FileAccess.Read))
                using (var reader = new BinaryReader(stream))
                {
                    var header = reader.ReadString();
                    if (header != nameof(OverlayInfo))
                        throw new AvisynthException();
                    return format.ReadFrame(reader);
                }
            }
        }

        public string DisplayInfo()
        {
            var crop = GetCrop();
            return $"Frame: {FrameNumber}\n" +
                   $"Size: {Width}x{Height} ({GetAspectRatio():F5}:1)\n" +
                   $"Crop: {crop.Left:0.###}:{crop.Top:0.###}:{crop.Right:0.###}:{crop.Bottom:0.###}\n" +
                   $"X: {X} Y: {Y} Angle: {Angle/100.0:F2}\n" +
                   $"Diff: {Diff:F5}";
        }

        public int CompareTo(OverlayInfo other)
        {
            if (ReferenceEquals(this, other)) return 0;
            if (ReferenceEquals(null, other)) return 1;
            var compareFrame = FrameNumber.CompareTo(other.FrameNumber);
            if (compareFrame != 0)
                return compareFrame;
            return Diff.CompareTo(other.Diff);
        }
    }
}
