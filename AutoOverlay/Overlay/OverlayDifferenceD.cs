using System;
using System.Collections;
using System.Collections.Generic;

namespace AutoOverlay.Overlay
{
    public class OverlayDifferenceD : IEnumerable<RectangleD>
    {
        public static readonly OverlayDifferenceD EMPTY = new(RectangleD.Empty, RectangleD.Empty, true);

        public RectangleD LeftTop { get; }
        public RectangleD RightTop { get; }
        public RectangleD RightBottom { get; }
        public RectangleD LeftBottom { get; }

        public RectangleD Union { get; }
        public RectangleD Intersection { get; }


        public OverlayDifferenceD(RectangleD a, RectangleD b, bool onlyEmpty)
        {
            Union = RectangleD.Union(a, b);
            Intersection = RectangleD.Intersect(a, b);

            RectangleD Calculate(Func<RectangleD, Space> criteria)
            {
                var unionPoint = criteria(Union);
                var interPoint = criteria(Intersection);
                var location = Space.Min(unionPoint, interPoint, criteria(a), criteria(b));
                var antiLocation = Space.Max(unionPoint, interPoint, criteria(a), criteria(b));
                var contains = a.Contains(unionPoint) || b.Contains(unionPoint);
                return new RectangleD(location, onlyEmpty && contains ? Space.Empty : antiLocation - location);
            }

            LeftTop = Calculate(p => p.LeftTop);
            RightTop = Calculate(p => p.RightTop);
            RightBottom = Calculate(p => p.RightBottom);
            LeftBottom = Calculate(p => p.LeftBottom);
        }

        public RectangleD Borders => RectangleD.FromLTRB(
            Math.Max(LeftTop.X, LeftBottom.X),
            Math.Max(LeftTop.Y, RightTop.Y),
            Math.Min(RightTop.X, RightBottom.X),
            Math.Min(LeftBottom.Y, RightBottom.Y));

        public RectangleD BorderLengths => RectangleD.FromLTRB(
            LeftTop.Width,
            LeftTop.Height,
            RightBottom.Width,
            RightBottom.Height);

        public RectangleD BorderShares => BorderLengths.Eval(
            Union.AsSpace().Repeat(),
            (border, total) => border / total);

        public IEnumerator<RectangleD> GetEnumerator()
        {
            yield return LeftTop;
            yield return RightTop;
            yield return RightBottom;
            yield return LeftBottom;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
