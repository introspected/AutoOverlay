using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace AutoOverlay.Overlay
{
    public class Warp : IEnumerable<RectangleD>, IEquatable<Warp>, ICloneable
    {
        public const int MAX_POINTS = 16;

        private const string PATTERN = @"(?:^|;)\s*(\d{1,5}(?:\.\d{1,2})?),(\d{1,5}(?:\.\d{1,2})?)\s*\[(-?\d{1,3}(?:\.\d{1,3})?),(-?\d{1,3}(?:\.\d{1,3})?)\]";
        private static readonly Regex regex = new Regex(PATTERN, RegexOptions.Compiled | RegexOptions.Singleline);

        public static readonly Warp Empty = new Warp(0);

        private RectangleD[] points;

        public bool IsEmpty => points.Length == 0;

        public int Length => points.Length;

        public Warp(int length)
        {
            points = new RectangleD[length];
            for (var i = 0; i < points.Length; i++)
                points[i] = new RectangleD(0,0,0,0);
        }

        public bool Fake
        {
            get
            {
                return points.Any(point =>
                    points.Any(other =>
                        point != other && Math.Abs(point.X - other.X) < RectangleD.EPSILON &&
                        Math.Abs(point.Width) > RectangleD.EPSILON &&
                        Math.Abs(point.Width - other.Width) < RectangleD.EPSILON) &&
                    points.Any(other =>
                        point != other && Math.Abs(point.Y - other.Y) < RectangleD.EPSILON &&
                        Math.Abs(point.Height) > RectangleD.EPSILON &&
                        Math.Abs(point.Height - other.Height) < RectangleD.EPSILON));
            }
        }

        public Warp Scale(double coef)
        {
            return Scale(coef, coef);
        }

        public Warp Scale(SizeD coefs)
        {
            return Scale(coefs.Width, coefs.Height);
        }

        public Warp Scale(double coefWidth, double coefHeight)
        {
            var warp = new Warp(Length);
            for (var i = 0; i < Length; i++)
            {
                warp[i] = this[i].Scale(coefWidth, coefHeight);
            }
            return warp;
        }

        public RectangleD this[int index]
        {
            get => index >= points.Length ? RectangleD.Empty : points[index];
            set => points[index] = value;
        }

        public double[] ToArray()
        {
            return points.SelectMany(p => new[] {p.X, p.Y, p.Width, p.Height}).ToArray();
        }

        public static Warp Read(BinaryReader reader)
        {
            var points = ReadImpl(reader).ToArray();
            if (!points.Any())
                return Empty;
            return new Warp(0) {points = points};
        }

        public void Write(BinaryWriter writer)
        {
            foreach (var point in points)
            {
                writer.Write((float) point.X);
                writer.Write((float) point.Y);
                writer.Write((float) point.Width);
                writer.Write((float) point.Height);
            }

            for (var i = points.Length * 4; i < MAX_POINTS * 4; i++)
            {
                writer.Write(-1.0f);
            }
        }

        private static IEnumerable<RectangleD> ReadImpl(BinaryReader reader)
        {
            for (var i = 0; i < MAX_POINTS; i++)
            {
                var point = new RectangleD(
                    reader.ReadSingle(), reader.ReadSingle(),
                    reader.ReadSingle(), reader.ReadSingle());
                if (point.X >= -RectangleD.EPSILON)
                    yield return point;
            }
        }

        public IEnumerator<RectangleD> GetEnumerator()
        {
            return ((IEnumerable<RectangleD>) points).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return points.GetEnumerator();
        }

        public bool Equals(Warp other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return this.SequenceEqual(other);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Warp) obj);
        }

        public override int GetHashCode()
        {
            return (points != null
                ? points.Select((p, i) => new {p, i}).Aggregate(0,
                    (hash, point) => unchecked(hash * 67) ^ point.p.GetHashCode())
                : 0);
        }

        public object Clone()
        {
            var warp = new Warp(Length);
            points.CopyTo(warp.points, 0);
            return warp;
        }

        public override string ToString()
        {
            if (IsEmpty)
                return "disabled";
            return string.Join("; ",
                points.Select(p => FormattableString.Invariant($"{p.X:F2},{p.Y:F2} [{p.Width:F3},{p.Height:F3}]")));
        }

        public static Warp Parse(string text)
        {
            TryParse(text, out var result);
            return result;
        }

        public static bool TryParse(string text, out Warp warp)
        {
            warp = Empty;
            if (string.IsNullOrWhiteSpace(text) || "disabled".Equals(text))
                return true;
            if (!regex.IsMatch(text))
                return false;
            var matches = regex.Matches(text);
            if (matches.Count < 3)
                return false;
            warp = new Warp(matches.Count);
            for (var i = 0; i < matches.Count; i++)
            {
                var match = matches[i];
                warp[i] = new RectangleD(
                    double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture),
                    double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture),
                    double.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture),
                    double.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture));
            }
            return true;
        }
    }
}
