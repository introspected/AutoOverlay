using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace AutoOverlay.Overlay
{
    public class OverlayData : IComparable<OverlayData>
    {
        public static readonly OverlayData EMPTY = new()
        {
            Diff = double.MaxValue
        };

        public static Func<OverlayData, Rectangle> SourceGetter => p => p.Source;
        public static Func<OverlayData, Rectangle> OverlayGetter => p => p.Overlay;

        public double Diff { get; set; }

        public Warp SourceWarp { get; set; } = Warp.Empty;
        public Rectangle Source { get; set; }
        public SizeD SourceBaseSize { get; set; }
        public RectangleF SourceCrop { get; set; }

        public Warp OverlayWarp { get; set; } = Warp.Empty;
        public Rectangle Overlay { get; set; }
        public SizeD OverlayBaseSize { get; set; }
        public RectangleF OverlayCrop { get; set; }

        public float OverlayAngle { get; set; }

        public double Coef { get; set; }

        public Rectangle Intersection => Rectangle.Intersect(Source, Overlay);
        public OverlayDifference Difference => new(Source, Overlay);

        public List<OverlayData> ExtraClips { get; set; } = new();

        public Rectangle Union { get; set; }

        public OverlayInfo GetOverlayInfo()
        {
            SizeD CalcScale(Size newSize, SizeD oldSize, RectangleD crop) => new(
                newSize.Width / (oldSize.Width - crop.Left - crop.Right),
                newSize.Height / (oldSize.Height - crop.Top - crop.Bottom));

            var srcScale = CalcScale(Source.Size, SourceBaseSize, SourceCrop);
            var overScale = CalcScale(Overlay.Size, OverlayBaseSize, OverlayCrop);

            SizeD CalcSize(SizeD size, SizeD scale) => new(size.Width * scale.Width, size.Height * scale.Height);

            return new OverlayInfo
            {
                Diff = Diff,
                SourceSize = CalcSize(SourceBaseSize, srcScale),
                OverlaySize = CalcSize(OverlayBaseSize, overScale),
                SourceWarp = SourceWarp.Scale(srcScale),
                OverlayWarp = OverlayWarp.Scale(overScale),
                Angle = OverlayAngle,
                Placement = Overlay.Location.AsSpace() - OverlayCrop.Location
            };
        }

        public int CompareTo(OverlayData other)
        {
            if (ReferenceEquals(this, other)) return 0;
            if (ReferenceEquals(null, other)) return 1;
            return Diff.CompareTo(other.Diff);
        }

        public override string ToString() =>
            "Render params:\n" +
            $"Source: X: {Source.X} Y: {Source.Y} Size: {Source.Width}x{Source.Height} " +
            $"Crop: {SourceCrop.Left:F2}:{SourceCrop.Top:F2}:{SourceCrop.Right:F2}:{SourceCrop.Bottom:F2}\n" +
            $"Source warp: {SourceWarp}\n" +
            $"Overlay: X: {Overlay.X} Y: {Overlay.Y} Size: {Overlay.Width}x{Overlay.Height} Angle: {OverlayAngle:F3} " +
            $"Crop: {OverlayCrop.Left:F2}:{OverlayCrop.Top:F2}:{OverlayCrop.Right:F2}:{OverlayCrop.Bottom:F2}\n" +
            $"Overlay warp: {OverlayWarp}\n" +
            $"Resize coefficient: {Coef:F5}\n" +
            ExtraClips.Select((p, i) => $"\n[Extra {i + 1}] " + p).Aggregate(new StringBuilder(), (sb, s) => sb.AppendLine(s));
    }
}
