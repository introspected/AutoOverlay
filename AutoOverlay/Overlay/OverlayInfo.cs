using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using AutoOverlay.Overlay;
using AvsFilterNet;

namespace AutoOverlay
{
    public sealed class OverlayInfo : IComparable<OverlayInfo>, IEquatable<OverlayInfo>, ICloneable
    {
        public static readonly OverlayInfo EMPTY = new()
        {
            Diff = double.MaxValue
        };

        public double Diff { get; set; }
        public SizeD SourceSize { get; set; }
        public SizeD OverlaySize { get; set; }
        public Space Placement { get; set; }
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

        private static readonly OverlayStatFormat format = new(OverlayUtils.OVERLAY_FORMAT_VERSION);

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
                var list = new List<OverlayInfo>();
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

        public double SourceAspectRatio => SourceSize.AspectRatio;

        public double OverlayAspectRatio => OverlaySize.AspectRatio;

        public RectangleD SourceRectangle => new(PointF.Empty, SourceSize);

        public RectangleD OverlayRectangle => new(Placement, OverlaySize);

        public RectangleD Union => RectangleD.Union(SourceRectangle, OverlayRectangle);

        public RectangleD Intersection => RectangleD.Intersect(SourceRectangle, OverlayRectangle);

        public bool Equals(OverlayInfo other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return SourceSize.Equals(other.SourceSize) &&
                   OverlaySize.Equals(other.OverlaySize) &&
                   Placement.Equals(other.Placement) &&
                   Angle.Equals(other.Angle) &&
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
                hashCode = (hashCode * 397) ^ Angle.GetHashCode();
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

        public OverlayInfo ScaleBySource(SizeD sourceSize)
        {
            var info = Clone();
            var scale = new SizeD(sourceSize.Width / SourceSize.Width, sourceSize.Height / SourceSize.Height);
            info.SourceSize = sourceSize;
            info.OverlaySize = new SizeD(OverlaySize.Width * scale.Width, OverlaySize.Height * scale.Height);
            info.Placement = new Space(Placement.X * scale.Width, Placement.Y * scale.Height);
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
            return (double)(intersect.Width * intersect.Height) / (union.Width * union.Height);
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
                   $"{nameof(SourceSize)}: {SourceSize.Width:F2}x{SourceSize.Height}, " +
                   $"{nameof(OverlaySize)}: {OverlaySize.Width:F2}x{OverlaySize.Height}, {nameof(Warp)}: {OverlayWarp}, ";
        }

        public RectangleD GetCanvas(OverlayInput input)
        {
            if (input.FixedSource)
                return SourceRectangle.Expand(input.TargetSize.GetAspectRatio());
            var union = input.ExtraClips
                .Select(p => p.Item2.ScaleBySource(SourceSize))
                .Select(p => p.Union)
                .Aggregate(Union, (acc, rect) => acc.Union(rect));

            var canvas = union.Expand(input.TargetSize.GetAspectRatio());
            var balanceCoef = (input.OverlayBalance + Space.One) / 2;
            var center = (SourceRectangle.Location + SourceRectangle.AsSpace() / 2) * balanceCoef.Remaining() +
                         (OverlayRectangle.Location + OverlayRectangle.AsSpace() / 2) * balanceCoef;

            // Outer bounds
            var borderShares = RectangleD.Difference(canvas, union, false).BorderShares;
            var crop = borderShares.Eval(
                canvas.AsSpace().Repeat(),
                input.OuterBounds,
                (share, length, max) => max > 1.01 ? Math.Max(length * share - max, 0) : length * Math.Max(0, share - max));
            canvas = canvas.Crop(crop);

            var limit = input.InnerBounds.Eval(canvas.AsSpace().Repeat(), (mult, length) => mult > 1.01 ? mult : length * mult);
            var diff = RectangleD.Difference(SourceRectangle, OverlayRectangle, true);

            // Inner bounds
            IEnumerable<RectangleD> IterateCrop(RectangleD canvas, RectangleD limit)
            {
                var crops = new HashSet<RectangleD>();

                void ConditionalAdd(RectangleD emptyArea, params RectangleD[] variants)
                {
                    if (emptyArea.Area > 0 && emptyArea.Intersect(canvas).Area > 0)
                        foreach (var variant in variants)
                            crops.Add(variant);
                }

                ConditionalAdd(diff.LeftTop,
                    RectangleD.FromLTRB(diff.LeftTop.Right - canvas.Left - limit.Left, 0, 0, 0),
                    RectangleD.FromLTRB(0, diff.LeftTop.Bottom - canvas.Top - limit.Top, 0, 0));
                ConditionalAdd(diff.RightTop,
                    RectangleD.FromLTRB(0, 0, canvas.Right - diff.RightTop.Left - limit.Right, 0),
                    RectangleD.FromLTRB(0, diff.RightTop.Bottom - canvas.Top - limit.Top, 0, 0));
                ConditionalAdd(diff.RightBottom,
                    RectangleD.FromLTRB(0, 0, canvas.Right - diff.RightBottom.Left - limit.Right, 0),
                    RectangleD.FromLTRB(0, 0, 0, canvas.Bottom - diff.RightBottom.Top - limit.Bottom));
                ConditionalAdd(diff.LeftBottom,
                    RectangleD.FromLTRB(diff.LeftBottom.Right - canvas.Left - limit.Left, 0, 0, 0),
                    RectangleD.FromLTRB(0, 0, 0, canvas.Bottom - diff.LeftBottom.Top - limit.Bottom));

                crops.RemoveWhere(p => !p.LTRB().Any(p => p > RectangleD.EPSILON));
                if (!crops.Any())
                {
                    yield return canvas;
                }

                foreach (var c in crops
                             .Where(p => p.LTRB().Any(p => p > RectangleD.EPSILON))
                             .Select(canvas.Crop)
                             .Select(p => p.Crop(input.TargetSize.GetAspectRatio(), center))
                             .SelectMany(p => IterateCrop(p, limit)))
                {
                    yield return c;
                }
            }


            var maxArea = IterateCrop(canvas, limit)
                .Union(IterateCrop(canvas, RectangleD.Empty))
                .Distinct()
                .Max(p => p.Area);


            return IterateCrop(canvas, limit)
                .Union(IterateCrop(canvas, RectangleD.Empty))
                .Where(p => Math.Abs(p.Area - maxArea) < OverlayUtils.EPSILON)
                .Aggregate((acc, c) => acc.Union(c))
                .Crop(input.TargetSize.GetAspectRatio(), center);
        }

        public OverlayData GetOverlayData(OverlayInput input)
        {
            var canvas = GetCanvas(input);

            var coef = input.TargetSize.AsSpace() / canvas;

            var offset = -canvas.Location;
            canvas = new RectangleD(Space.Empty, input.TargetSize);
            
            (Rectangle region, RectangleD crop, Warp warp) GetRegionAndCrop(RectangleD rect, SizeF size, Warp warpIn)
            {
                rect = rect.Offset(offset).Scale(coef);
                var targetRect = RectangleD.Intersect(canvas, rect).Floor();
                var crop = rect.Eval(targetRect, (t, u) => Math.Abs(t - u));
                crop = crop.Scale(size.AsSpace() / rect.Size);
                return (targetRect, crop.IsEmpty ? RectangleD.Empty : crop, warpIn.Scale(coef));
            }

            var (targetSrc, srcCrop, srcWarp) = GetRegionAndCrop(SourceRectangle, input.SourceSize, SourceWarp);
            var (targetOver, overCrop, overWarp) = GetRegionAndCrop(OverlayRectangle, input.OverlaySize, OverlayWarp);

            var extraClips = from p in input.ExtraClips
                             let tuple = p.Item1
                             let info = p.Item2.ScaleBySource(SourceSize)
                             let regionAndCrop = GetRegionAndCrop(info.OverlayRectangle, tuple.Info.Size, info.OverlayWarp)
                             select new OverlayData
                             {
                                 Diff = Diff,
                                 SourceBaseSize = input.SourceSize,
                                 Source = targetSrc,
                                 SourceCrop = srcCrop,
                                 SourceWarp = srcWarp,
                                 OverlayBaseSize = tuple.Info.Size,
                                 Overlay = regionAndCrop.region,
                                 OverlayCrop = regionAndCrop.crop,
                                 OverlayWarp = regionAndCrop.warp,
                                 Coef = coef.X,
                             };
            var extraClipList = extraClips.ToList();

            return new OverlayData
            {
                Diff = Diff,
                SourceBaseSize = input.SourceSize,
                Source = targetSrc,
                SourceCrop = srcCrop,
                SourceWarp = srcWarp,
                OverlayBaseSize = input.OverlaySize,
                Overlay = targetOver,
                OverlayCrop = overCrop,
                OverlayWarp = overWarp,
                Coef = coef.X,
                ExtraClips = extraClipList,
                Union = extraClipList
                    .Select(p => p.Overlay)
                    .Aggregate(Rectangle.Union(targetSrc, targetOver), Rectangle.Union)
            }.Also(data => data.ExtraClips.ForEach(p => p.Union = data.Union));
        }
    }
}
