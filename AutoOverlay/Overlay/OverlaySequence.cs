using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace AutoOverlay.Overlay
{
    public record OverlaySequence : IEnumerable<OverlayInfo>
    {
        private readonly SortedDictionary<int, OverlayInfo> sequence = new();

        private OverlaySequence() { }

        public static OverlaySequence Of(OverlayInfo one)
        {
            return new OverlaySequence
            {
                sequence =
                {
                    [one.FrameNumber] = one
                }
            };
        }

        public static OverlaySequence Of(IEnumerable<OverlayInfo> sequence)
        {
            var holder = new OverlaySequence();
            foreach (var info in sequence)
            {
                holder.sequence[info.FrameNumber] = info;
            }
            return holder;
        }

        public IEnumerable<int> Frames => sequence.Keys;

        public bool HasFrame(int frameNumber) => sequence.ContainsKey(frameNumber);

        public OverlayInfo this[int frameNumber] => sequence[frameNumber];

        public IEnumerable<OverlayInfo> GetNeighbours(int frameNumber, double rectangleTolerance, double diffTolerance)
        {
            var main = sequence[frameNumber];
            return new[] { -1, 1 }.SelectMany(sign =>
                Enumerable.Range(1, int.MaxValue)
                    .Select(p => frameNumber + sign * p)
                    .TakeWhile(HasFrame)
                    .Select(p => sequence[p])
                    .TakeWhile(p => p.NearlyEquals(main, rectangleTolerance) && Math.Abs(p.Diff - main.Diff) < diffTolerance));
        }

        public IEnumerator<OverlayInfo> GetEnumerator()
        {
            return sequence.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public OverlaySequence ScaleBySource(SizeD size) => Of(this.Select(p => p.ScaleBySource(size)));
    }
}
