using System;
using System.IO;
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

        public int FrameNumber { get; set; }

        object ICloneable.Clone() => MemberwiseClone();

        public OverlayInfo Clone() => (OverlayInfo)MemberwiseClone();

        public void ToFrame(VideoFrame frame)
        {
            unsafe
            {
                using (var stream = new UnmanagedMemoryStream((byte*)frame.GetWritePtr().ToPointer(),
                    frame.GetRowSize(), frame.GetRowSize(), FileAccess.Write))
                using (var writer = new BinaryWriter(stream))
                {
                    writer.Write(nameof(OverlayInfo));
                    writer.Write(FrameNumber);
                    writer.Write(Width);
                    writer.Write(Height);
                    writer.Write(CropLeft);
                    writer.Write(CropTop);
                    writer.Write(CropRight);
                    writer.Write(CropBottom);
                    writer.Write(X);
                    writer.Write(Y);
                    writer.Write(Angle);
                    writer.Write(Diff);
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
                    return new OverlayInfo
                    {
                        FrameNumber = reader.ReadInt32(),
                        Width = reader.ReadInt32(),
                        Height = reader.ReadInt32(),
                        CropLeft = reader.ReadInt32(),
                        CropTop = reader.ReadInt32(),
                        CropRight = reader.ReadInt32(),
                        CropBottom = reader.ReadInt32(),
                        X = reader.ReadInt32(),
                        Y = reader.ReadInt32(),
                        Angle = reader.ReadInt32(),
                        Diff = reader.ReadDouble()
                    };
                }
            }
        }

        public override string ToString()
        {
            var crop = GetCrop();
            return $"Frame: {FrameNumber}\n" +
                   $"Size: {Width}x{Height} ({AspectRatio:F3}:1)\n" +
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
