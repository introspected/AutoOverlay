using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace AutoOverlay.Overlay
{
    public class OverlayDifference : IEnumerable<Rectangle>
    {
        public Rectangle LeftTop { get; }
        public Rectangle RightTop { get; }
        public Rectangle RightBottom { get; }
        public Rectangle LeftBottom { get; }


        internal OverlayDifference(Rectangle a, Rectangle b)
        {
            var union = Rectangle.Union(a, b);
            var intersection = Rectangle.Intersect(a, b);

            Rectangle Calculate(Func<Rectangle, Point> criteria)
            {
                var unionPoint = criteria(union);
                var interPoint = criteria(intersection);
                var points = new[] {unionPoint, interPoint, criteria(a), criteria(b)};
                var location = new Point(points.Min(p => p.X), points.Min(p => p.Y));
                var antiLocation = new Point(points.Max(p => p.X), points.Max(p => p.Y));
                var contains = a.Contains(unionPoint) || b.Contains(unionPoint);
                return new Rectangle(location, contains ? Size.Empty : new Size(antiLocation.X - location.X, antiLocation.Y - location.Y));
            }
            LeftTop = Calculate(p => p.Location);
            RightTop = Calculate(p => new Point(p.X + p.Width, p.Y));
            RightBottom = Calculate(p => new Point(p.X + p.Width, p.Y + p.Height));
            LeftBottom = Calculate(p => new Point(p.X, p.Y + p.Height));
        }

        private OverlayDifference(OverlayDifference first, OverlayDifference second)
        {
            LeftTop = Rectangle.Union(first.LeftTop, second.LeftTop);
            RightTop = Rectangle.Union(first.RightTop, second.RightTop);
            RightBottom = Rectangle.Union(first.RightBottom, second.RightBottom);
            LeftBottom = Rectangle.Union(first.LeftBottom, second.LeftBottom);
        }

        public static OverlayDifference Union(OverlayDifference first, OverlayDifference second) => new(first, second);

        public IEnumerator<Rectangle> GetEnumerator()
        {
            yield return LeftTop;
            yield return RightTop;
            yield return RightBottom;
            yield return LeftBottom;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
