using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace AutoOverlay.Overlay
{
    public class WarpIterator : IEnumerable<Warp>
    {
        private readonly RectangleD[] warpPoints;
        private readonly Warp super;
        public SizeD SizeCoef { get; }
        private readonly Size size;
        private readonly int iteration;
        private readonly int warpSteps;
        private bool skip;
        private double prevDiff = double.MaxValue;

        private readonly HashSet<Warp> history;

        public WarpIterator(RectangleD[] points, Warp warp, Size clipSize, Size newSize, int iteration, int warpSteps)
        {
            warpPoints = points;

            //warp = warp.Reverse();
            //if (iteration % 2 == 0)
            //    warpPoints = warpPoints.Reverse().ToArray();

            SizeCoef = new SizeD((double) newSize.Width / clipSize.Width, (double) newSize.Height / clipSize.Height);
            super = warp.Scale(SizeCoef);
            size = newSize;
            this.iteration = iteration;
            this.warpSteps = warpSteps;

            history = [super];

            if (super.IsEmpty && iteration > 0)
            {
                super = new Warp(warpPoints.Length);
                for (var j = 0; j < warpPoints.Length; j++)
                {
                    var jPoint = warpPoints[j].Scale(SizeCoef);
                    super[j] = new RectangleD(jPoint.X, jPoint.Y, 0, 0);
                }
            }
        }

        public void Analyze(double diff)
        {
            if (diff - prevDiff > -double.Epsilon)
                skip = true;
            else prevDiff = diff;
        }

        public IEnumerator<Warp> GetEnumerator()
        {
            yield return super;
            if (!warpPoints.Any() || iteration < 1 || iteration > warpSteps)
                yield break;

            var warp = (Warp) super.Clone();

            for (var i = 0; i < warpPoints.Length; i++)
            {
                var point = warpPoints[i].Scale(SizeCoef);

                var horizontal = Process(
                    p => p.X, p => p.Width, p => p.Width,
                    p => new RectangleD(point.X, point.Y, p, super[i].Height));
                var vertical = Process(
                    p => p.Y, p => p.Height, p => p.Height, 
                    p => new RectangleD(point.X, point.Y, super[i].Width, p));
                var both = Process(
                    p => p.Y, p => p.Height, p => p.Height,
                    p => new RectangleD(point.X, point.Y, p, p));
                var result = iteration % 2 == 0
                    ? horizontal.Union(vertical) //.Union(both)
                    : vertical.Union(horizontal);//.Union(both);
                foreach (var w in result)
                    yield return w;

                IEnumerable<Warp> Process(
                    Func<RectangleD, double> coordinate,
                    Func<RectangleD, double> dimension,
                    Func<Size, int> sizeDimension,
                    Func<double, RectangleD> construct)
                {
                    var best = dimension(super[i]);
                    for (var sign = -1; sign <= 1; sign++)
                    {
                        if (skip)
                        {
                            skip = false;
                            if (i == 1) continue;
                        }
                        var length = Step(sign, dimension);
                        var newCoordinate = coordinate(point) + length;
                        if (newCoordinate > double.Epsilon
                            && newCoordinate < sizeDimension(size) - double.Epsilon
                            && (newCoordinate <= dimension(point) + double.Epsilon 
                                || newCoordinate >= sizeDimension(size) - dimension(point) - double.Epsilon))
                            continue;
                        warp[i] = construct(length);
                        if (warp.Fake || history.Contains(warp)) continue;
                        var copy = (Warp) warp.Clone();
                        history.Add(copy);
                        yield return copy;
                        if (!skip)
                            best = length;
                    }
                    super[i] = construct(best);
                }

                double Step(int sign, Func<RectangleD, double> dimension)
                {
                    var step = sign * dimension(point) / warpSteps;
                    if (super.IsEmpty)
                        return step;
                    var current = dimension(super[i]);
                    //if (current * step < 0)
                    //    step /= 2;
                    return current + step;
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
