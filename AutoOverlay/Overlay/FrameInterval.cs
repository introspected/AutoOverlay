using System;
using System.Collections.Generic;
using System.Linq;

namespace AutoOverlay
{
    public class FrameInterval : AbstractOverlayInfo, IEquatable<FrameInterval>
    {
        public List<OverlayInfo> Frames { get; } = new List<OverlayInfo>();

        public bool Modified { get; set; }

        public OverlayInfo this[int frame] => Frames.FirstOrDefault(p => p.FrameNumber == frame);

        public int First => Frames.Count == 0 ? -1 : Frames.Min(p => p.FrameNumber);
        public int Last => Frames.Count == 0 ? -1 : Frames.Max(p => p.FrameNumber);
        public int Length => Last - First + 1;

        public string Interval => First == Last ? First.ToString() : $"{First} ({Length})";
        public string Size => $"{Width}x{Height}";

        public string Crop => $"{CropLeft / (OverlayInfo.CROP_VALUE_COUNT / 100)}," +
                              $"{CropTop / (OverlayInfo.CROP_VALUE_COUNT / 100)}," +
                              $"{CropRight / (OverlayInfo.CROP_VALUE_COUNT / 100)}," +
                              $"{CropBottom / (OverlayInfo.CROP_VALUE_COUNT / 100)}";

        public override double Diff
        {
            get => Frames.Sum(p => p.Diff) / Frames.Count;
            set => throw new InvalidOperationException("Diff is unique for each frame");
        }

        public override int X
        {
            get => Frames.FirstOrDefault()?.X ?? 0;
            set => Frames.ForEach(p => p.X = value);
        }

        public override int Y
        {
            get => Frames.FirstOrDefault()?.Y ?? 0;
            set => Frames.ForEach(p => p.Y = value);
        }

        public override int Width
        {
            get => Frames.FirstOrDefault()?.Width ?? 0;
            set => Frames.ForEach(p => p.Width = value);
        }

        public override int Height
        {
            get => Frames.FirstOrDefault()?.Height ?? 0;
            set => Frames.ForEach(p => p.Height = value);
        }

        public override int CropLeft
        {
            get => Frames.FirstOrDefault()?.CropLeft ?? 0;
            set => Frames.ForEach(p => p.CropLeft = value);
        }

        public override int CropTop
        {
            get => Frames.FirstOrDefault()?.CropTop ?? 0;
            set => Frames.ForEach(p => p.CropTop = value);
        }

        public override int CropRight
        {
            get => Frames.FirstOrDefault()?.CropRight ?? 0;
            set => Frames.ForEach(p => p.CropRight = value);
        }

        public override int CropBottom
        {
            get => Frames.FirstOrDefault()?.CropBottom ?? 0;
            set => Frames.ForEach(p => p.CropBottom = value);
        }

        public override int BaseWidth
        {
            get => Frames.FirstOrDefault()?.BaseWidth ?? 0;
            set => Frames.ForEach(p => p.BaseWidth = value);
        }

        public override int BaseHeight
        {
            get => Frames.FirstOrDefault()?.BaseHeight ?? 0;
            set => Frames.ForEach(p => p.BaseHeight = value);
        }

        public override int SourceWidth
        {
            get => Frames.FirstOrDefault()?.SourceWidth ?? 0;
            set => Frames.ForEach(p => p.SourceWidth = value);
        }

        public override int SourceHeight
        {
            get => Frames.FirstOrDefault()?.SourceHeight ?? 0;
            set => Frames.ForEach(p => p.SourceHeight = value);
        }

        public override int Angle
        {
            get => Frames.FirstOrDefault()?.Angle ?? 0;
            set => Frames.ForEach(p => p.Angle = value);
        }

        public override double Comparison
        {
            get => Frames.Sum(p => p.Comparison) / Frames.Count;
            set => throw new InvalidOperationException("Diff is unique for each frame");
        }

        public bool Contains(int frame)
        {
            return frame >= First && frame <= Last;
        }

        public bool Equals(FrameInterval other)
        {
            return Equals(this, other);
        }

        public override bool Equals(object obj)
        {
            if (!base.Equals(obj))
                return false;
            if (obj is FrameInterval)
                return First == ((FrameInterval)obj).First && Last == ((FrameInterval)obj).Last;
            return true;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = base.GetHashCode();
                hashCode = (hashCode * 397) ^ First;
                hashCode = (hashCode * 397) ^ Last;
                return hashCode;
            }
        }
    }
}
