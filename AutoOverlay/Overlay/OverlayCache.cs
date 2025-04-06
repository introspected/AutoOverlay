using System;
using System.Collections.Concurrent;
using System.Linq;

namespace AutoOverlay.Overlay
{
    public class OverlayCache(OverlayEngine engine)
    {
        private readonly ConcurrentDictionary<IKey, OverlayInfo> cache = new();

        public void NextFrame(int n)
        {
            var limit = engine.SceneBuffer + 1;
            foreach (var key in cache.Keys.Where(p => Math.Abs(p.Frame - n) > limit))
                cache.TryRemove(key, out _);
        }

        public bool IsAligned(int frame, Warp warp = null) => cache.ContainsKey(new AlignKey(frame, warp));

        public bool IsScanned(int frame, OverlayInfo precursor) => cache.ContainsKey(new ScanKey(frame, precursor));

        public bool IsRepeated(int frame, OverlayInfo reference) => cache.ContainsKey(new RepeatKey(frame, reference));

        public OverlayInfo Align(int frame, Warp warp = null) => Update(
            new AlignKey(frame, warp),
            _ => engine.AutoAlign(frame, engine.GetConfigs(), warp),
            (_, info) => (new RepeatKey(frame, info), info),
            (_, info) => (new ScanKey(frame, info), info));

        public OverlayInfo Repeat(OverlayInfo reference, int frame)
        {
            var existed = engine.OverlayStat[frame];
            return reference.Equals(existed)
                ? existed
                : Update(
                    new RepeatKey(frame, reference),
                    _ => engine.RepeatImpl(reference, frame));
        }

        public OverlayInfo Scan(OverlayInfo precursor, int frame) => Update(
            new ScanKey(frame, precursor),
            _ => engine.ScanImpl(precursor, frame),
            (_, info) => (new RepeatKey(frame, info), info),
            (_, info) => (new ScanKey(precursor.FrameNumber, info), precursor));

        public OverlayInfo LegacyScan(OverlayInfo precursor, int frame) => Update(
            new ScanKey(frame, precursor),
            _ => engine.PanScanImpl(precursor, frame),
            (_, info) => (new RepeatKey(frame, info), info),
            (_, info) => (new ScanKey(precursor.FrameNumber, info), precursor));

        private OverlayInfo Update<TKey>(
        TKey key,
        Func<TKey, OverlayInfo> function,
            params Func<TKey, OverlayInfo, (IKey, OverlayInfo)>[] extras) where TKey : IKey
        {
            var value = cache.GetOrAdd(key, _ =>
            {
                engine.Log(key.ToString);
                return function(key);
            }).Clone().Also(p => p.FrameNumber = key.Frame);
            foreach (var func in extras)
            {
                var tuple = func(key, value);
                cache[tuple.Item1] = tuple.Item2;
            }
            return value;
        }

        interface IKey
        {
            int Frame { get; }
        }

        sealed record AlignKey(int Frame, Warp Warp) : IKey;

        sealed record RepeatKey(int Frame, OverlayInfo Reference) : IKey;

        sealed record ScanKey(int Frame, OverlayInfo Precursor) : IKey;
    }
}
