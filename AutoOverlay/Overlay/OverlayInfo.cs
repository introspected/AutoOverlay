using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using AutoOverlay.Overlay;
using AvsFilterNet;

namespace AutoOverlay
{
    public sealed class OverlayInfo : IComparable<OverlayInfo>, IEquatable<OverlayInfo>, ICloneable
    {
        private static readonly OverlayStatFormat format = new(OverlayConst.OVERLAY_FORMAT_VERSION);

        public static readonly OverlayInfo EMPTY = new()
        {
            Diff = double.MaxValue
        };

        public double Diff { get; set; }
        public SizeD SourceSize { get; set; }  // Before rotation
        public SizeD OverlaySize { get; set; }  // Before rotation
        public Space Placement { get; set; } // After rotation
        public float Angle { get; set; }
        public Warp SourceWarp { get; set; } = Warp.Empty;
        public Warp OverlayWarp { get; set; } = Warp.Empty;

        public double Comparison { get; set; } = 2;

        public bool Modified { get; set; }

        public bool ProbablyChanged { get; set; }

        public bool KeyFrame { get; set; }

        public string Message { get; set; }

        public string Branch { get; set; } = string.Empty;

        public int FrameNumber { get; set; } // zero based

        public double SourceAspectRatio => SourceSize.AspectRatio;

        public double OverlayAspectRatio => OverlaySize.AspectRatio;

        public RectangleD SourceRectangle => new(PointF.Empty, SourceSize);

        public RectangleD OverlayRectangle => new(Placement, OverlaySize);

        public RectangleD Union => RectangleD.Union(SourceRectangle, OverlayRectangle);

        public RectangleD Intersection => RectangleD.Intersect(SourceRectangle, OverlayRectangle);

        object ICloneable.Clone() => MemberwiseClone();

        public OverlayInfo Clone() => (OverlayInfo)MemberwiseClone();

        public OverlayInfo Invert()
        {
            var info = Clone();
            info.SourceSize = OverlaySize;
            info.OverlaySize = SourceSize;
            info.Placement = -Placement;
            info.Angle = -Angle;
            info.SourceSize = OverlaySize;
            info.OverlayWarp = SourceWarp;
            return info;
        }

        public static OverlayInfo Read(BinaryReader reader)
        {
            var header = reader.ReadInt32();
            if (header != nameof(OverlayInfo).GetHashCode())
                return null;
            var message = reader.ReadString();
            var keyFrame = reader.ReadBoolean();
            var info = format.ReadFrame(reader);
            info.Message = message;
            info.KeyFrame = keyFrame;
            return info;
        }

        public void Write(BinaryWriter writer, string message = null)
        {
            writer.Write(nameof(OverlayInfo).GetHashCode());
            writer.Write(message ?? string.Empty);
            writer.Write(KeyFrame);
            format.WriteFrame(writer, this);
        }

        public static List<OverlayInfo> FromFrame(VideoFrame frame)
        {
            unsafe
            {
                using var stream = new UnmanagedMemoryStream(
                    (byte*)frame.GetReadPtr().ToPointer(),
                    frame.GetRowSize() * frame.GetHeight(),
                    frame.GetRowSize() * frame.GetHeight(),
                    FileAccess.Read);
                using var reader = new BinaryReader(stream);
                var list = new List<OverlayInfo>(OverlayConst.ENGINE_HISTORY_LENGTH);
                var caption = reader.ReadString();
                if (caption != nameof(OverlayEngine))
                    throw new AvisynthException();
                reader.ReadInt32();
                while (true)
                {
                    var info = Read(reader);
                    if (info == null)
                        break;
                    list.Add(info);
                }
                return list;
            }
        }

        public string DisplayInfo()
        {
            var key = KeyFrame ? "[KeyFrame]" : "";
            return $"Frame: {FrameNumber} {key}\n" +
                   $"Size: {OverlaySize.Width:F2}x{OverlaySize.Height:F2} ({OverlayAspectRatio:F5}:1)\n" +
                   $"Warp: {OverlayWarp}\n" +
                   $"X: {Placement.X:F2} Y: {Placement.Y:F2} Angle: {Angle:F2}\n" +
                   $"Diff: {Diff:F5}\n{Message ?? string.Empty}";
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

        public bool Equals(OverlayInfo other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return SourceSize.Equals(other.SourceSize) &&
                   OverlaySize.Equals(other.OverlaySize) &&
                   Placement.Equals(other.Placement) &&
                   Math.Abs(Angle - other.Angle) < float.Epsilon &&
                   Equals(SourceWarp, other.SourceWarp) &&
                   Equals(OverlayWarp, other.OverlayWarp);
        }

        public override bool Equals(object obj)
        {
            return ReferenceEquals(this, obj) || obj is OverlayInfo other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = SourceSize.GetHashCode();
                hashCode = (hashCode * 397) ^ OverlaySize.GetHashCode();
                hashCode = (hashCode * 397) ^ Placement.GetHashCode();
                hashCode = (hashCode * 397) ^ Math.Round(Angle).GetHashCode();
                hashCode = (hashCode * 397) ^ (SourceWarp != null ? SourceWarp.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (OverlayWarp != null ? OverlayWarp.GetHashCode() : 0);
                return hashCode;
            }
        }

        public double Compare(OverlayInfo other)
        {
            if (Equals(other))
                return 1;
            var intersect = RectangleD.Intersect(Union, other.Union);
            var union = RectangleD.Union(Union, other.Union);
            return intersect.Area / union.Area;
        }

        // positive crop when aligned image was cropped for rendering
        // negative crop when aligned image was enlarged for rendering
        // crop in pixels based on current image size
        public OverlayInfo ScaleBySource(SizeD sourceSize, RectangleD crop = default)
        {
            var info = Clone();
            var nonCroppedSize = new SizeD(
                sourceSize.Width + crop.Left + crop.Right,
                sourceSize.Height + crop.Top + crop.Bottom);
            var scale = new SizeD(nonCroppedSize.Width / SourceSize.Width, nonCroppedSize.Height / SourceSize.Height);
            info.SourceSize = sourceSize;
            info.OverlaySize = new SizeD(OverlaySize.Width * scale.Width, OverlaySize.Height * scale.Height);
            info.Placement = new Space(Placement.X * scale.Width + crop.Left, Placement.Y * scale.Height + crop.Top);
            return info;
        }

        public OverlayInfo CropOverlay(SizeD overlaySize, RectangleD crop)
        {
            var info = Clone();
            var nonCroppedSize = new SizeD(
                OverlaySize.Width + crop.Left + crop.Right,
                OverlaySize.Height + crop.Top + crop.Bottom);
            var scale = new SizeD(nonCroppedSize.Width / OverlaySize.Width, nonCroppedSize.Height / OverlaySize.Height);
            crop = crop.Scale(scale);
            info.OverlaySize = new SizeD(
                OverlaySize.Width * scale.Width - crop.Left - crop.Right, 
                OverlaySize.Height * scale.Height - crop.Top - crop.Bottom);
            info.Placement = new Space(Placement.X - crop.Left, Placement.Y - crop.Top);
            return info;
        }

        public double GetEqualityLevel(OverlayInfo other)
        {
            if (Equals(other))
            {
                return 1;
            }
            var rect1 = OverlayRectangle;
            var rect2 = other.ScaleBySource(SourceSize).OverlayRectangle;
            var intersect = RectangleD.Intersect(rect1, rect2);
            var union = RectangleD.Union(rect1, rect2);
            return (intersect.Width * intersect.Height) / (union.Width * union.Height);
        }

        public bool NearlyEquals(OverlayInfo other, double tolerance)
        {
            if (other == null)
                return false;
            var comparison = GetEqualityLevel(other);
            return 1 - comparison <= tolerance;
        }

        public void CopyFrom(OverlayInfo other)
        {
            Placement = other.Placement;
            SourceSize = other.SourceSize;
            OverlaySize = other.OverlaySize;
            Angle = other.Angle;
            SourceWarp = other.SourceWarp;
            OverlayWarp = other.OverlayWarp;
        }

        public override string ToString()
        {
            return $"{nameof(Diff)}: {Diff:F5}, {nameof(Placement)}: {Placement.X:F3}, {Placement.Y:F3}, {nameof(Angle)}: {Angle:F3}, " +
                   $"{nameof(SourceSize)}: {SourceSize.Width:F2}x{SourceSize.Height:F2}, " +
                   $"{nameof(OverlaySize)}: {OverlaySize.Width:F2}x{OverlaySize.Height:F2}, {nameof(Warp)}: {OverlayWarp}, ";
        }
    }
}
