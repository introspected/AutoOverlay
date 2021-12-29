using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace AutoOverlay
{
    public class FrameInterval : IEquatable<FrameInterval>, IEnumerable<OverlayInfo>
    {
        private readonly SortedDictionary<int, OverlayInfo> frames = new SortedDictionary<int, OverlayInfo>();

        private readonly Dictionary<object, string> cache = new Dictionary<object, string>();

        public FrameInterval(OverlayInfo frame)
        {
            if (frame == null)
                throw new ArgumentException();
            Add(frame);
        }

        public FrameInterval(IEnumerable<OverlayInfo> frames)
        {
            AddRange(frames);
            if (Length == 0)
                throw new ArgumentException();
        }

        public void Add(OverlayInfo frame)
        {
            frames[frame.FrameNumber] = frame;
            ClearCache();
        }

        public void AddRange(IEnumerable<OverlayInfo> frames)
        {
            foreach (var frame in frames)
                this.frames[frame.FrameNumber] = frame;
            ClearCache();
        }

        public OverlayInfo this[int frame]
        {
            get => Contains(frame) ? frames[frame] : null;
            set
            {
                if (Fixed)
                    foreach (var f in frames.Values)
                        f.CopyFrom(value);
                frames[frame] = value;
                value.ProbablyChanged = true;
                ClearCache();
            }
        }

        public void ClearCache()
        {
            cache.Clear();
        }

        public void RemoveIf(Func<OverlayInfo, bool> info)
        {
            frames.Values.Where(info).Select(p => p.FrameNumber).ToList().ForEach(p => frames.Remove(p));
            ClearCache();
        }

        public int First => frames.Keys.FirstOrDefault();
        public int Last => frames.Keys.LastOrDefault();
        public int Length => frames.Count;

        public bool Fixed => CheckFixed(p => p) != "vary";

        public string Interval => First == Last ? First.ToString() : $"{First} ({Length})";

        public string X => CheckFixed(p => p.X);

        public string Y => CheckFixed(p => p.Y);

        public string Size => CheckFixed(p => new Size(p.Width, p.Height), p => $"{p.Width}x{p.Height}");

        public string Warp => CheckFixed(p => p.Warp, p => "fixed");

        public string Crop => CheckFixed(p => p.GetCrop().Scale(100), 
            p => $"{p.Left:F0},{p.Top:F0},{p.Right:F0},{p.Bottom:F0}");

        public string Angle => CheckFixed(p => p.Angle);

        private string CheckFixed<T>(Func<OverlayInfo, T> read, Func<T, string> format = null)
        {
            if (cache.TryGetValue(read, out var res))
                return res;
            var value = read(frames.Values.First());
            var isFixed = frames.Values.All(p => read(p).Equals(value));
            format ??= p => p.ToString();
            return cache[read] = isFixed ? format(value) : "vary";
        }

        public double Diff => frames.Values.Sum(p => p.Diff) / frames.Count;

        public double Comparison => frames.Values.Sum(p => p.Comparison) / frames.Count;

        public bool Modified
        {
            get => frames.Values.Any(p => p.Modified);
            set
            {
                foreach (var frame in frames.Values)
                    frame.Modified = value;
            }
        }

        public void CopyFrom(FrameInterval interval)
        {
            if (interval.Fixed)
                CopyFrom(interval.frames.Values.First());
        }

        public void CopyFrom(OverlayInfo info)
        {
            foreach (var frame in frames.Values)
            {
                if (!frame.Equals(info))
                {
                    frame.Modified = true;
                    frame.CopyFrom(info);
                }
            }
            cache.Clear();
        }

        public bool Contains(int frame)
        {
            return frame >= First && frame <= Last;
        }

        public bool Equals(FrameInterval other)
        {
            return Equals(this, other);
        }

        public IEnumerator<OverlayInfo> GetEnumerator()
        {
            return frames.Values.GetEnumerator();
        }

        public override bool Equals(object obj)
        {
            if (obj is FrameInterval interval)
                return First == interval.First && Last == interval.Last;
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

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
