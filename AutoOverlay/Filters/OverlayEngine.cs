using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AutoOverlay;
using AutoOverlay.AviSynth;
using AutoOverlay.Forms;
using AutoOverlay.Overlay;
using AvsFilterNet;

[assembly: AvisynthFilterClass(
    typeof(OverlayEngine),
    nameof(OverlayEngine),
    "cc[SourceMask]c[OverlayMask]c[StatFile]s[Preset]s" +
    "[SceneBuffer]i[ShakeBuffer]i[Stabilize]b[Scan]b[Correction]b" +
    "[FrameDiffTolerance]f[FrameAreaTolerance]f[SceneDiffTolerance]f[SceneAreaTolerance]f[FrameDiffBias]f[MaxDiff]f" +
    "[LegacyMode]b[BackwardFrames]i[ForwardFrames]i[MaxDiffIncrease]f[ScanDistance]i[ScanScale]f[StickLevel]f[StickDistance]f" +
    "[Configs]c[Presize]s[Resize]s[Rotate]s[Editor]b[Mode]s[ColorAdjust]i[SceneFile]s[SIMD]b[Debug]b",
    OverlayConst.DEFAULT_MT_MODE)]
namespace AutoOverlay
{
    public class OverlayEngine : OverlayFilter
    {
        #region Presets
        static OverlayEngine()
        {
            Presets.Add<OverlayEnginePreset, OverlayEngine>(new()
            {
                [OverlayEnginePreset.Low] = new()
                {
                    [nameof(SceneBuffer)] = _ => 10,
                    [nameof(ShakeBuffer)] = _ => 1,
                    [nameof(FrameDiffTolerance)] = _ => 10,
                    [nameof(SceneDiffTolerance)] = _ => 75,
                    [nameof(MaxDiff)] = _ => 20,
                },
                [OverlayEnginePreset.Medium] = new()
                {
                    // defaults
                },
                [OverlayEnginePreset.High] = new()
                {
                    [nameof(SceneBuffer)] = _ => 20,
                    [nameof(ShakeBuffer)] = _ => 3,
                    [nameof(FrameDiffTolerance)] = _ => 5,
                    [nameof(SceneDiffTolerance)] = _ => 25,
                    [nameof(MaxDiff)] = _ => 10,
                },
            });
        }
        #endregion

        #region Properties
        [AvsArgument(Required = true)]
        public Clip Source { get; set; }

        [AvsArgument(Required = true)]
        public Clip Overlay { get; set; }

        [AvsArgument]
        public Clip SourceMask { get; set; }

        [AvsArgument]
        public Clip OverlayMask { get; set; }

        [AvsArgument]
        public string StatFile { get; set; }

        [AvsArgument]
        public OverlayEnginePreset Preset { get; private set; } = OverlayEnginePreset.Medium;

        [AvsArgument(Min = 0, Max = 1000)]
        public int SceneBuffer { get; private set; } = 15; // frames

        [AvsArgument(Min = 0, Max = 100)]
        public int ShakeBuffer { get; private set; } = 2; // frames

        [AvsArgument]
        public bool Stabilize { get; private set; } = true;

        [AvsArgument]
        public bool Scan { get; private set; } = true;

        [AvsArgument]
        public bool Correction { get; private set; } = true;

        [AvsArgument]
        public double FrameDiffTolerance { get; private set; } = 7; // %

        [AvsArgument]
        public double FrameAreaTolerance { get; private set; } = 0.2; // %

        [AvsArgument]
        public double SceneDiffTolerance { get; private set; } = 50; // %

        [AvsArgument]
        public double SceneAreaTolerance { get; private set; } = 0.5; // %

        [AvsArgument]
        public double FrameDiffBias { get; private set; } = 1.5;

        [AvsArgument(Min = 0)]
        public double MaxDiff { get; set; } = 15;

        #region Legacy
        [AvsArgument]
        public bool LegacyMode { get; set; }

        [AvsArgument(Min = 0, Max = 100)]
        public int BackwardFrames { get; set; } = 3;

        [AvsArgument(Min = 0, Max = 100)]
        public int ForwardFrames { get; set; } = 3;

        [AvsArgument(Min = 0)]
        public double MaxDiffIncrease { get; private set; } = 1;

        [AvsArgument(Min = 0)]
        public int ScanDistance { get; private set; } = 0;

        [AvsArgument(Min = 0)]
        public double ScanScale { get; private set; } = 3;
        #endregion

        [AvsArgument(Min = 0, Max = 10)]
        public double StickLevel { get; set; } = 0;

        [AvsArgument(Min = 0, Max = 10)]
        public double StickDistance { get; set; } = 1;

        [AvsArgument]
        public OverlayConfig[] Configs { get; private set; }

        [AvsArgument]
        public string Presize { get; private set; }

        [AvsArgument]
        public string Resize { get; private set; }

        [AvsArgument]
        public string Rotate { get; private set; } = OverlayConst.DEFAULT_ROTATE_FUNCTION;

        [AvsArgument]
        public bool Editor { get; private set; }

        [AvsArgument]
        public OverlayEngineMode Mode { get; private set; } = OverlayEngineMode.DEFAULT;

        [AvsArgument(Min = -1, Max = 1)]
        public int ColorAdjust { get; private set; } = -1;

        [AvsArgument]
        public string SceneFile { get; private set; }

        [AvsArgument]
        public bool SIMD { get; private set; } = true;

        [AvsArgument]
        public override bool Debug { get; protected set; }
        #endregion

        public IOverlayStat OverlayStat { get; private set; }

        public ExtraVideoInfo SrcInfo { get; private set; }
        public ExtraVideoInfo OverInfo { get; private set; }

        public Clip sourcePrepared;
        public Clip overlayPrepared;

        private readonly OverlayCache cache;

        public event EventHandler<FrameEventArgs> CurrentFrameChanged;

        internal IEnumerable<OverlayConfigInstance> GetConfigs() => Configs.Select(p => p.GetInstance());

        private Form form;

        public int[] SelectedFrames { get; private set; }

        public HashSet<int> KeyFrames { get; } = new();

        private int framesTotal;

        private static readonly Predicate<OverlayData>
            LeftStick = p => p.Overlay.Left == p.Source.Left && p.OverlayCrop.Left.IsNearlyZero(),
            RightStick = p => p.Overlay.Right == p.Source.Right && p.OverlayCrop.Right.IsNearlyZero(),
            TopStick = p => p.Overlay.Top == p.Source.Top && p.OverlayCrop.Top.IsNearlyZero(),
            BottomStick = p => p.Overlay.Bottom == p.Source.Bottom && p.OverlayCrop.Bottom.IsNearlyZero(),
            HorizontalStick = p => LeftStick(p) && RightStick(p),
            VerticalStick = p => TopStick(p) && BottomStick(p),
            HorizontalSymmetry = p => p.OverlayCrop.Left.IsNearlyEquals(p.OverlayCrop.Right)
                                      && p.Intersection.Left - p.Union.Left == p.Union.Right - p.Intersection.Right,
            VerticalSymmetry = p => p.OverlayCrop.Top.IsNearlyEquals(p.OverlayCrop.Bottom)
                                    && p.Intersection.Top - p.Union.Top == p.Union.Bottom - p.Intersection.Bottom,
            CenterStick = p => p.OverlayCrop.IsEmpty && (HorizontalStick(p) && VerticalSymmetry(p) || VerticalStick(p) && HorizontalSymmetry(p)),
            OriginalSize = p => p.GetOverlayInfo().OverlaySize.Equals(p.OverlayBaseSize);

        private static List<Predicate<OverlayData>> StickCriteria { get; } =
        [
            HorizontalStick, VerticalStick, CenterStick, OriginalSize
        ];

        public OverlayEngine()
        {
            cache = new(this);
        }

        protected override void AfterInitialize()
        {
            FrameDiffTolerance /= 100;
            FrameAreaTolerance /= 100;
            SceneDiffTolerance /= 100;
            SceneAreaTolerance /= 100;

            var cacheSize = SceneBuffer * 2 + 1;
            Source = Source.Dynamic().Cache(cacheSize);
            Overlay = Overlay.Dynamic().Cache(cacheSize);
            SourceMask = SourceMask?.Dynamic()?.Cache(cacheSize);
            OverlayMask = OverlayMask?.Dynamic()?.Cache(cacheSize);

            Presize ??= OverlayConst.DEFAULT_PRESIZE_FUNCTION;
            Resize ??= StaticEnv.FunctionCoalesce(
                OverlayConst.DEFAULT_RESIZE_FUNCTION + "MT", 
                OverlayConst.DEFAULT_RESIZE_FUNCTION);

            Presize = Presize.ToLower();
            Resize = Resize.ToLower();

            if (!Configs.Any())
                Configs = [new OverlayConfig()];
            SrcInfo = Source.GetVideoInfo();
            OverInfo = Overlay.GetVideoInfo();
            if ((SrcInfo.ColorSpace ^ OverInfo.ColorSpace).HasFlag(ColorSpaces.CS_PLANAR))
                throw new AvisynthException("Both clips must be in planar or RGB color space");
            if (SrcInfo.ColorSpace.GetBitDepth() != OverInfo.ColorSpace.GetBitDepth())
                throw new AvisynthException("Both clips must have the same bit depth");

            sourcePrepared = Prepare(Source);
            overlayPrepared = Prepare(Overlay);

            OverlayStat = new FileOverlayStat(StatFile, SrcInfo.Size, OverInfo.Size);

            if (SceneFile != null)
            {
                foreach (var line in File.ReadLines(SceneFile))
                {
                    if (int.TryParse(line, out var frame))
                        KeyFrames.Add(frame);
                }
            }

            var vi = GetVideoInfo();
            framesTotal = vi.num_frames = Math.Min(SrcInfo.FrameCount, OverInfo.FrameCount);
            if (Mode is OverlayEngineMode.PROCESSED or OverlayEngineMode.UNPROCESSED)
            {
                SelectedFrames = new int[vi.num_frames];
                var index = 0;
                var prev = 0;
                foreach (var info in OverlayStat.Frames)
                {
                    if (SelectedFrames.Length == index)
                        break;
                    var missed = info.FrameNumber - prev - 1;
                    if (Mode is OverlayEngineMode.PROCESSED)
                        SelectedFrames[index++] = info.FrameNumber;
                    else if (missed > 0)
                    {
                        for (var i = 1; i <= missed; i++)
                            SelectedFrames[index++] = prev + i;
                    }
                    prev = info.FrameNumber;
                }
                if (Mode is OverlayEngineMode.UNPROCESSED)
                    for (var i = prev + 1; i < vi.num_frames; i++)
                        SelectedFrames[index++] = i;
                vi.num_frames = index;
            }
            SetVideoInfo(ref vi);

            if (Editor)
            {
                form?.SafeInvoke(p => p.Close());
                var env = StaticEnv;
                var sync = SynchronizationContext.Current ?? new SynchronizationContext();
                var thread = new Thread(() =>
                {
                    Application.Run(form = new OverlayEditor(this, env, sync));
                });
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                Thread.Sleep(100);
            }
            ScanScale /= 1000;
            if (PanScanMode)
                Stabilize = false;
        }

        private Clip Prepare(Clip clip)
        {
            if (clip.GetVideoInfo().IsRGB())
                return clip.Dynamic().ConvertToRgb();
            if (clip.IsRealPlanar())
                return clip.Dynamic().ExtractY();
            return clip;
        }

        protected override VideoFrame GetFrame(int n)
        {
            if (Mode is OverlayEngineMode.PROCESSED or OverlayEngineMode.UNPROCESSED)
            {
                n = SelectedFrames[n];
            }
            var info = GetOverlayInfo(n);
            info.KeyFrame = KeyFrames.Contains(n);
            CurrentFrameChanged?.Invoke(this, new FrameEventArgs(n));
            var frame = Debug ? GetSubtitledFrame(() => this + "\n" + info) : base.GetFrame(n);
            StaticEnv.MakeWritable(frame);
            unsafe
            {
                using var stream = new UnmanagedMemoryStream((byte*)frame.GetWritePtr().ToPointer(),
                    frame.GetRowSize() * frame.GetHeight(), frame.GetRowSize() * frame.GetHeight(), FileAccess.Write);
                using var writer = new BinaryWriter(stream);
                writer.Write(nameof(OverlayEngine));
                writer.Write(GetHashCode());
                info.Write(writer, info.Message);
                var history = Enumerable.Range(1, OverlayConst.ENGINE_HISTORY_LENGTH)
                    .SelectMany(p => new[] { n - p, n + p })
                    .Where(p => p >= 0 && p < framesTotal)
                    .Select(p => OverlayStat[p])
                    .Where(p => p != null)
                    .Peek(p => p.KeyFrame = KeyFrames.Contains(p.FrameNumber));
                foreach (var overlayInfo in history)
                    overlayInfo.Write(writer);
            }
            return frame;
        }

        internal OverlayInfo ScanImpl(OverlayInfo precursor, int n) => PanScanImpl(
            precursor, n,
            (int)Math.Round(precursor.SourceRectangle.Width * SceneAreaTolerance),
            SceneAreaTolerance);

        public OverlayInfo GetOverlayInfo(int n)
        {
            if (Mode == OverlayEngineMode.ERASE)
            {
                OverlayStat[n] = null;
                return GetDummyInfo(n, "Frame align info was erased");
            }
            var existed = OverlayStat[n];
            if (existed == null && Mode == OverlayEngineMode.READONLY)
            {
                return GetDummyInfo(n, "Unprocessed frame [readonly mode enabled]");
            }
            if (existed != null)
            {
                switch (Mode)
                {
                    case OverlayEngineMode.UPDATE:
                        var repeated = RepeatImpl(existed, n);
                        if (Math.Abs(repeated.Diff - existed.Diff) > double.Epsilon)
                            return OverlayStat[n] = repeated;
                        break;
                    case OverlayEngineMode.ENHANCE:
                        var enhanced = OverlayStat[n] = PanScanImpl(existed, n, 2, 0.002, false, arVariance: 0.01); // Enhance(existed, n);
                        enhanced.Message = "Frame enhanced";
                        return enhanced;
                }
                existed.Message = "Cached frame found";
                return existed;
            }
            var sb = new StringBuilder("\nAuto-align\n");
            Func<int, Action<Func<string>>, OverlayInfo> impl = LegacyMode ? GetOverlayInfoLegacy : GetOverlayInfoImpl;
            var info = impl(n, Debug ? log =>
            {
                var message = log();
                sb.AppendLine(message.Replace('\n', ' '));
                Log(() => $"Frame {n}: {message}");
            }
            : _ => { });
            DisableLog(sb.ToString);
            info.Message = Debug ? sb.ToString() : "Frame successfully auto-aligned";
            return info;
        }

        private OverlayInfo GetDummyInfo(int n, string message) => new()
        {
            FrameNumber = n,
            SourceSize = SrcInfo.Size,
            OverlaySize = OverInfo.Size,
            Diff = -1,
            Message = message
        };

        private bool CheckDev(IEnumerable<OverlayInfo> sample)
        {
            return OverlayUtils.CheckDev(sample, MaxDiffIncrease, false);
        }

        public bool PanScanMode => ScanDistance > 0;

        private double FrameDiff(OverlayInfo main, OverlayInfo other) => FrameDiff(main.Diff, other.Diff);

        private double FrameDiff(double main, double other) => (other + FrameDiffBias) / (main + FrameDiffBias) - 1;

        private bool CheckFrameArea(OverlayInfo first, OverlayInfo second) => first.NearlyEquals(second, FrameAreaTolerance);

        private bool CheckSceneArea(OverlayInfo first, OverlayInfo second) => first.NearlyEquals(second, SceneAreaTolerance);

        private OverlayInfo GetOverlayInfoImpl(int n, Action<Func<string>> log)
        {
            cache.NextFrame(n);
#if DEBUG
            if (Debug && n == 0 && StatFile == null)
                return OverlayStat[n] = AutoAlign(n);
#endif
            if (ShakeBuffer == 0)
                return OverlayStat[n] = AutoAlign(n);

            // Direction: 1 - forward, -1 - backward
            var direction = n == 0
                            || OverlayStat[n - 1] != null
                            || n == framesTotal - 1
                            || OverlayStat[n + 1] == null ? 1 : -1;

            int stabKeyFrame, scanKeyFrame;
            stabKeyFrame = scanKeyFrame = Enumerable.Range(1, ShakeBuffer)
                .Select(p => n + p * -direction)
                .Where(p => p >= 0 && p < framesTotal)
                .Select(p => new
                {
                    Frame = p,
                    Stat = OverlayStat[p],
                    Prev = p == 0 || p == framesTotal ? null : OverlayStat[p + direction]
                })
                .TakeWhile(p => p.Stat != null && (p.Prev == null || CheckSceneArea(p.Stat, p.Prev)) && !KeyFrames.Contains(p.Frame + (direction == 1 ? 1 : 0)))
                .LastOrDefault()
                ?.Frame ?? n;
            
            Stabilize:
            while (Stabilize)
            {
                log(() => $"Stabilization from {stabKeyFrame} frame");
                var stabBuffer = GetShakeBuffer(stabKeyFrame);
                var stabDiff = stabBuffer.Average(p => p.Diff);
                // Try to stabilize buffer frames
                var stableSequence = stabBuffer
                    .Distinct()
                    .Select(p => new
                    {
                        Sequence = stabBuffer.Select(f => new
                        {
                            Original = f,
                            Repeated = f.Equals(p) ? f : cache.Repeat(p, f.FrameNumber)
                        }).Select(f => new
                        {
                            f.Original,
                            f.Repeated,
                            FrameDiff = FrameDiff(f.Original, f.Repeated)
                        })
                    })
                    .Select(p => new
                    {
                        p.Sequence,
                        Passed = p.Sequence.All(f => f.FrameDiff <= FrameDiffTolerance),
                        //Passed = FrameDiff(stabDiff, p.Sequence.Average(f => f.Repeated.Diff)) <= FrameDiffTolerance,
                        AverageDiff = p.Sequence.Average(f => f.Repeated.Diff),
                    })
                    .Peek(p => log(() => $"""
                                      Align: {p.Sequence.First().Repeated}\n
                                      Own Diff: [{string.Join(", ", p.Sequence.Select(f => f.Original.Diff.ToString("F5")))}]\n
                                      Repeated Diff: [{string.Join(", ", p.Sequence.Select(f => f.Repeated.Diff.ToString("F5")))}]\n
                                      Delta: [{string.Join(", ", p.Sequence.Select(f => f.FrameDiff.ToString("F5")))}]\n
                                      Avg Own Diff: {stabDiff:F5}
                                      Avg Repeated Diff: {p.AverageDiff:F5}
                                      Avg Delta: {FrameDiff(stabDiff, p.AverageDiff):F5}
                                      Passed: {p.Passed}\n
                                      """))
                    .Where(p => p.Passed)
                    .OrderBy(p => p.AverageDiff)
                    .FirstOrDefault()
                    ?.Sequence?.ToList();

                if (stableSequence == null)
                {
                    log(() => "Buffer is unstable");
                    if (!Scan && stabBuffer.All(p => p.FrameNumber != n))
                    {
                        log(() => "Trying stabilize new scene");
                        stabKeyFrame = n;
                        continue;
                    }
                    break;
                }
                // If buffer stabilization successful, check current scene
                var keyframe = stableSequence.First().Repeated;
                var predicted = cache.Repeat(keyframe, n);
                if (FrameDiff(keyframe, predicted) > SceneDiffTolerance)
                {
                    var own = cache.Align(n, predicted.OverlayWarp);
                    var diff = FrameDiff(own, predicted);
                    if (!CheckSceneArea(own, predicted) && diff > FrameDiffTolerance)
                    {
                        log(() => $"Prediction failed: own diff={own.Diff:F5}, predicted diff={predicted.Diff:F5} ({diff * 100:F5}%)");
                        stabKeyFrame = n;
                        continue;
                    }
                }
                if (CheckScene(predicted, (frame, info) => cache.Repeat(info, frame), false))
                {
                    log(() => "Stabilization successful");
                    if (Correction)
                        stableSequence.ForEach(p => OverlayStat[p.Repeated.FrameNumber] = p.Repeated);
                    else stableSequence.ForEach(p => OverlayStat[p.Repeated.FrameNumber] ??= p.Repeated);
                    return OverlayStat[n] = predicted;
                }
                log(() => "Scene is not stable");
                break;
            }

            while (Scan)
            {
                log(() => $"Scanning from {scanKeyFrame} frame");
                var scanBuffer = GetShakeBuffer(scanKeyFrame);
                var prev = scanBuffer.First();
                var scanDetected = true;
                foreach (var stat in scanBuffer)
                {
                    if (!CheckSceneArea(prev, stat))
                    {
                        scanDetected = false;
                        break;
                    }
                    prev = stat;
                }

                if (!scanDetected)
                {
                    if (Stabilize && stabKeyFrame != n)
                    {
                        log(() => "Unstable buffer, trying stabilize new scene");
                        stabKeyFrame = n;
                        goto Stabilize;
                    }
                    if (scanKeyFrame != n)
                    {
                        log(() => "Unstable buffer, trying scan new scene");
                        scanKeyFrame = n;
                        continue;
                    }
                    break;
                }
                var keyframe = scanBuffer.First();
                var predicted = cache.Scan(keyframe, n);
                var diff = FrameDiff(keyframe, predicted);
                if (diff > SceneDiffTolerance || !CheckSceneArea(keyframe, predicted))
                {
                    log(() => $"Prediction failed: prev diff={keyframe.Diff:F5}, predicted diff={predicted.Diff:F5} ({diff * 100:F5}%)");
                    if (stabKeyFrame != n && Stabilize)
                    {
                        log(() => "Trying stabilize new scene");
                        stabKeyFrame = n;
                        goto Stabilize;
                    }

                    if (scanKeyFrame == n)
                        break;
                    log(() => "Trying scan new scene");
                    scanKeyFrame = n;
                    continue;
                }
                if (CheckScene(predicted, (frame, info) => cache.Scan(info, frame), true))
                {
                    log(() => "Scan successful");
                    foreach (var info in scanBuffer)
                        OverlayStat[info.FrameNumber] ??= info;
                    return OverlayStat[n] = predicted;
                }
                log(() => "Scan canceled");
                break;
            }
            return OverlayStat[n] = cache.Align(n);

            // Get shake buffer near current frame according to direction with auto-aligning unknown frames
            List<OverlayInfo> GetShakeBuffer(int start)
            {
                var frames = Enumerable.Range(0, ShakeBuffer)
                    .Select(p => start + p * direction)
                    .Where(p => p >= 0 && p < framesTotal)
                    .ToArray();
                return frames
                    .Select(p => OverlayStat[p] ?? cache.Align(p))
                    .Peek(p => log(() => $"Frame {p.FrameNumber}: diff={p.Diff:F5}"))
                    .OrderBy(p => -direction * p.FrameNumber)
                    .ToList();
            }

            //Check scene buffer according to direction if shake buffer is valid
            bool CheckScene(OverlayInfo target, Func<int, OverlayInfo, OverlayInfo> predict, bool scan) => new[] { -1, 1 }
                .Select(direction =>
                {
                    LinkedList<OverlayInfo> history = new();
                    history.AddLast(target);
                    var scene = Enumerable.Range(1, SceneBuffer)
                        .Select(p => n + p * direction)
                        .Where(p => p >= 0 && p < framesTotal);
                    var prev = target;
                    var keyframe = target;
                    foreach (var frame in scene)
                    {
                        var prevKeyFrame = direction == 1 ? frame : (frame - 1);
                        if (prevKeyFrame != n && KeyFrames.Contains(prevKeyFrame))
                        {
                            log(() => $"Key frame achieved: {prevKeyFrame}");
                            return true;
                        }

                        OverlayInfo predicted;
                        if (!scan || (predicted = OverlayStat[frame] ?? (cache.IsAligned(frame, prev.OverlayWarp) ? cache.Align(frame, prev.OverlayWarp) : null)) == null
                                  || !CheckSceneArea(predicted, prev))
                            predicted = predict(frame, prev);

                        history.AddLast(predicted);

                        var maxDiffExceed = predicted.Diff > MaxDiff;
                        var sequenceNotValid = !CheckFrameSequence(history, true);

                        if (maxDiffExceed || sequenceNotValid || cache.IsAligned(frame, prev.OverlayWarp))
                        {
                            if (maxDiffExceed || sequenceNotValid)
                                log(() => $"{frame}: Max Diff Exceed: {maxDiffExceed}, Sequence Not Valid: {sequenceNotValid}");

                            var own = OverlayStat[frame] ?? cache.Align(frame, prev.OverlayWarp);

                            history.RemoveLast();
                            history.AddLast(own);

                            var newSequenceBetter = sequenceNotValid && CheckFrameSequence(history, false);
                            var probablyNewScene = !CheckSceneArea(prev, own);

                            if (newSequenceBetter || probablyNewScene)
                            {
                                log(() => $"{frame}: New Sequence Better: {newSequenceBetter}, Probably New Scene: {probablyNewScene}");

                                var prevAligned = OverlayStat[prev.FrameNumber] ?? cache.Align(prev.FrameNumber, keyframe.OverlayWarp);
                                var prevDiff = FrameDiff(prevAligned, prev);

                                if (!CheckSceneArea(prevAligned, own))
                                {
                                    log(() => $"New scene detected at {frame}");
                                    return true;
                                }

                                if (prevDiff > FrameDiffTolerance || !CheckFrameArea(prevAligned, prev))
                                {
                                    log(() => $"Scan detected at {frame}");
                                    return false;
                                }
                            }

                            if (FrameDiff(own, predicted) <= FrameDiffTolerance && CheckFrameArea(own, predicted))
                            {
                                history.Clear();
                                history.AddLast(predicted);
                            }
                        }

                        prev = predicted;
                    }
                    return true;
                }).All(p => p);

            bool CheckFrameSequence(ICollection<OverlayInfo> sequence, bool abs)
            {
                var values = sequence.Select(p => p.Diff).ToList();
                var trend = 0.0;
                for (var i = 1; i < values.Count; i++)
                {
                    trend += values[i] - values[i - 1];
                }

                var mean = values.Average();
                var threshold = mean * SceneDiffTolerance;
                Log(() => $"Trend: {trend:F3} Threshold: {threshold:F3} Abs: {abs} Sequence: [{string.Join(", ", values.Select(val => $"{val:F2}"))}]");
                return (abs ? Math.Abs(trend) : trend) <= threshold;
            }
        }

        private OverlayInfo GetOverlayInfoLegacy(int n, Action<Func<string>> log)
        {
            log(() => $"Frame: {n}");

            if (BackwardFrames == 0) goto simple;

            var backwardFramesCount = Math.Min(n, BackwardFrames);

            var prevInfo = n > 0 ? OverlayStat[n - 1] : null;
            var prevFrames = Enumerable.Range(0, n)
                .Reverse()
                .Select(p => OverlayStat[p])
                .TakeWhile((p, i) => i >= 0 && i < backwardFramesCount && p != null && p.Diff <= MaxDiff && !KeyFrames.Contains(i))
                .ToArray();

            if (KeyFrames.Contains(n))
            {
                log(() => "New scene detected!");
                goto stabilize;
            }

            if (PanScanMode)
                prevFrames = prevFrames.TakeWhile((p, i) =>
                        i == 0 || CheckSceneArea(p, prevFrames[i - 1]))
                    .ToArray();
            else prevFrames = prevFrames.TakeWhile(p => p.Equals(prevInfo)).ToArray();

            var prevFramesCount = prevFrames.Length;

            log(() => $"Prev frames: {prevFramesCount}");

            if (prevFramesCount == BackwardFrames)
            {
                log(() => $"Analyze prev frames info:\n{prevInfo}");

                var info = PanScanMode ? cache.LegacyScan(prevInfo, n) : cache.Repeat(prevInfo, n);

                if (info.Diff > MaxDiff || !CheckDev(prevFrames.Append(info)))
                {
                    log(() => $"Repeated diff: {info.Diff:F3} is not OK");
                    goto stabilize;
                }
                log(() => $"Repeated diff: {info.Diff:F3} is OK");
                var checkFrames = prevFrames.Append(info).ToList();
                if (ForwardFrames > 0)
                {
                    log(() => $"Analyze next frames: {ForwardFrames}");
                    var prevStat = info;
                    for (var nextFrame = n + 1;
                        nextFrame <= n + ForwardFrames && nextFrame < framesTotal && !KeyFrames.Contains(nextFrame);
                        nextFrame++)
                    {
                        log(() => $"Next frame: {nextFrame}");
                        var stat = OverlayStat[nextFrame];
                        if (stat != null)
                        {
                            log(() => $"Existed info found:\n{stat}");
                            if (stat.Equals(info))
                            {
                                log(() => "Existed info is equal");
                                if (stat.Diff <= MaxDiff && CheckDev(checkFrames.Append(stat)))
                                {
                                    log(() => $"Existed info diff {stat.Diff:F3} is OK");
                                }
                                else
                                {
                                    log(() => $"Existed info diff {stat.Diff:F3} is not OK");
                                    goto simple;
                                }
                            }
                            if (CheckSceneArea(stat, info))
                            {
                                log(() => "Existed info is nearly equal. Pan&scan mode.");
                                if (ScanDistance == 0 || stat.Diff > MaxDiff || !CheckDev(checkFrames.Append(stat)))
                                    goto simple;
                                continue;
                            }
                            break;
                        }
                        prevStat = stat = PanScanMode ? cache.LegacyScan(prevStat, nextFrame) : cache.Repeat(info, nextFrame);
                        if (stat.Diff > MaxDiff || !CheckDev(checkFrames.Append(stat)))
                        {
                            log(() => $"Repeated info diff {stat.Diff:F3} is not OK");
                            stat = cache.Align(nextFrame);
                            log(() => $"Own info: {stat}");
                            if (CheckSceneArea(stat, info))
                            {
                                log(() => "Own info is nearly equal. Pan&scan mode.");
                                goto simple;
                            }
                            log(() => "Next scene detected");
                            break;
                        }
                        log(() => $"Repeated info diff: {stat.Diff:F3} is OK");
                    }
                }
                return OverlayStat[n] = info;
            }
            stabilize:
            if (Stabilize)
            {
                var info = cache.Align(n).Clone();
                if (info.Diff > MaxDiff)
                    goto simple;
                prevFrames = prevFrames.TakeWhile(p => p.Equals(info) && p.Diff <= MaxDiff).Take(BackwardFrames - 1).ToArray();
                prevFramesCount = prevFrames.Length;

                var stabilizeFrames = new List<OverlayInfo>(prevFrames) { info };
                for (var nextFrame = n + 1;
                    nextFrame < n + BackwardFrames - prevFramesCount &&
                    nextFrame < framesTotal;
                    nextFrame++)
                {
                    if (OverlayStat[nextFrame] != null)
                        goto simple;
                    var statOwn = cache.Align(nextFrame);
                    var statRepeated = cache.Repeat(info, nextFrame);
                    stabilizeFrames.Add(statOwn);
                    if (!CheckSceneArea(statRepeated, statOwn) || statRepeated.Diff > MaxDiff || !CheckDev(prevFrames.Concat(stabilizeFrames)))
                        goto simple;
                }

                var needAllNextFrames = false;
                if (n > 0)
                {
                    var prevStat = OverlayStat[n - 1] ?? cache.Align(n - 1);
                    if (CheckSceneArea(prevStat, info) &&
                        CheckDev(stabilizeFrames.Append(prevStat)) && prevStat.Diff < MaxDiff)
                        needAllNextFrames = true;
                }
                if (prevFrames.Length == 0)
                {
                    var averageInfo = stabilizeFrames.Distinct()
                        .Select(p => new { Info = p, Count = stabilizeFrames.Count(p.Equals) })
                        .OrderByDescending(p => p.Count)
                        .ThenBy(p => p.Info.Diff)
                        .First()
                        .Info;

                    stabilizeFrames.Clear();
                    for (var frame = n; frame < n + BackwardFrames - prevFramesCount && frame < framesTotal; frame++)
                    {
                        var stabInfo = cache.Repeat(averageInfo, frame);
                        stabilizeFrames.Add(stabInfo);
                        if (stabInfo.Diff > MaxDiff || !CheckDev(stabilizeFrames))
                            goto simple;
                    }

                    info = stabilizeFrames.First();
                }
                for (var nextFrame = n + BackwardFrames - prevFramesCount;
                    nextFrame < n + BackwardFrames - prevFramesCount + ForwardFrames &&
                    nextFrame < framesTotal;
                    nextFrame++)
                {
                    var stat = OverlayStat[nextFrame];
                    if (stat != null)
                    {
                        if (stat.Equals(info))
                        {
                            if (stat.Diff <= MaxDiff && CheckDev(stabilizeFrames.Append(stat)))
                                continue;
                            goto simple;
                        }
                        if (CheckSceneArea(stat, info))
                        {
                            goto simple;
                        }
                        break;
                    }
                    stat = cache.Repeat(info, nextFrame);
                    if (stat.Diff > MaxDiff || !CheckDev(stabilizeFrames.Append(stat)))
                    {
                        if (needAllNextFrames || CheckSceneArea(cache.Align(nextFrame), info))
                            goto simple;
                        break;
                    }
                }
                for (var frame = n;
                    frame < n + BackwardFrames - prevFramesCount &&
                    frame < framesTotal;
                    frame++)
                    if (frame == n || OverlayStat[frame] == null)
                        OverlayStat[frame] = stabilizeFrames[frame - n + prevFramesCount]; // TODO BUG!!!!
                return info;
            }
            simple:
            return OverlayStat[n] = cache.Align(n);
        }

        private int Scale(int val, double coef) => (int)Math.Round(val * coef);

        private int Round(double val) => (int)Math.Round(val);

        private int Floor(double val) => (int)Math.Floor(val);

        private int Ceiling(double val) => (int)Math.Ceiling(val);

        public OverlayInfo AutoAlign(int n, IEnumerable<OverlayConfigInstance> configs = null, Warp parentWarp = null, OverlayInfo external = null)
        {
            DisableLog(() => "AutoAlign started: " + n);
#if DEBUG
            Stopwatch totalWatch = new();
            Stopwatch extraWatch = new();
            Stopwatch avsWatch = new();
            totalWatch.Start();
#endif
            var existedAlign = external ?? OverlayStat[n];
            var preliminaryColorAdjust = existedAlign != null && ColorAdjust >= 0;

            var resultSet = new SortedSet<OverlayData>();
            using (new VideoFrameCollector())
            using (new DynamicEnvironment(StaticEnv))
            {
                configs ??= GetConfigs();
                var srcPrepared = sourcePrepared;
                var overPrepared = overlayPrepared;

                if (preliminaryColorAdjust)
                {
                    var input = new OverlayInput
                    {
                        SourceSize = SrcInfo.Size,
                        OverlaySize = OverInfo.Size,
                        TargetSize = SrcInfo.Size,
                        FixedSource = true
                    };
                    var data = OverlayMapper.For(input, existedAlign, default).GetOverlayData();
                    var adjusted = AdjustColor(srcPrepared, overPrepared, data);
                    if (ColorAdjust == 0)
                        overPrepared = adjusted;
                    else srcPrepared = adjusted;
                }

                if (parentWarp != null && !parentWarp.IsEmpty)
                    overPrepared = overPrepared.Dynamic().Warp(
                        parentWarp.ToArray(), relative: true,
                        resample: OverlayUtils.GetWarpResampleMode(Resize));
                foreach (var config in configs)
                {
                    var adjusted = ColorAdjust == -1 || preliminaryColorAdjust;

                    double Coef(int step) => Math.Pow(config.ScaleBase, 1 - step);

                    int BaseResizeStep(int resizeStep) => resizeStep - Round(Math.Log(2, config.ScaleBase));

                    Clip StepResize(Clip clip, int resizeStep, Size minSize = default)
                    {
                        if (resizeStep <= 1)
                            return clip;
                        var coef = Coef(resizeStep);
                        var baseStep = BaseResizeStep(resizeStep);
                        var vi = clip.GetVideoInfo();
                        var width = Scale(vi.width, coef);
                        var height = Scale(vi.height, coef);
                        var baseClip = StepResize(clip, baseStep);
                        if (minSize != Size.Empty && (width < minSize.Width || height < minSize.Height))
                            return baseClip;
                        return ResizeRotate(baseClip, Presize, Rotate, width, height);
                    }

                    var (minAspectRatio, maxAspectRatio) = FindMinMaxAr(config);

                    var angle1 = Math.Min(config.Angle1 % 360, config.Angle2 % 360);
                    var angle2 = Math.Max(config.Angle1 % 360, config.Angle2 % 360);

                    var subResultSet = new SortedSet<OverlayData>();
                    while (true)
                    {
                        int stepCount;
                        var warpPoints = adjusted && (parentWarp?.IsEmpty ?? true) ? config.WarpPoints.Select(p => (RectangleD)p).ToArray() : [];
                        for (stepCount = 0; ; stepCount++)
                        {
                            var testArea = Coef(stepCount + 1) * Coef(stepCount + 1) * SrcInfo.Area;
                            if (testArea < config.MinSampleArea)
                                break;
                            if (testArea < config.RequiredSampleArea)
                            {
                                var baseStep = BaseResizeStep(stepCount + 1);
                                var baseClip = StepResize(srcPrepared, baseStep);
                                var testSize = new Size(baseClip.GetVideoInfo().width, baseClip.GetVideoInfo().height);
                                var testClip = ResizeRotate(StepResize(srcPrepared, stepCount + 1), Presize, Rotate,
                                    testSize.Width, testSize.Height);
                                using var test1 = baseClip.GetFrame(n, StaticEnv);
                                using VideoFrame test2 = testClip[n];
                                var diff = FindBestIntersect(test1, null, testSize, test2, null, testSize,
                                    new Rectangle(0, 0, 1, 1), 0, 0).Diff;
                                if (diff > config.MaxSampleDiff)
                                    break;
                            }
                        }

                        var lastStep = Math.Max(0, -config.Subpixel) + 1;
                        for (var step = stepCount; step > 0; step--)
                        {
                            var initStep = !subResultSet.Any();
                            if (initStep)
                                subResultSet.Add(OverlayData.EMPTY);
                            var fakeStep = step < lastStep;

                            var coefDiff = initStep ? 1 : Coef(step) / Coef(step + 1);
                            var coefCurrent = Coef(step);

                            int srcScaledWidth = Scale(SrcInfo.Width, coefCurrent),
                                srcScaledHeight = Scale(SrcInfo.Height, coefCurrent);
                            var srcScaledArea = srcScaledWidth * srcScaledHeight;

                            var srcBase = StepResize(srcPrepared, step);
                            var srcBaseInfo = srcBase.GetVideoInfo();
                            var srcBaseSize = new Size(srcBaseInfo.width, srcBaseInfo.height);
                            var srcMaskBase = SourceMask == null ? null : StepResize(SourceMask, step);
                            var minOverBaseSize = new Size(
                                Round(subResultSet.First().Overlay.Width * config.ScaleBase * config.ScaleBase),
                                Round(subResultSet.First().Overlay.Height * config.ScaleBase * config.ScaleBase));
                            var overBase = StepResize(overPrepared, step - 1, minOverBaseSize);
                            var overBaseInfo = overBase.GetVideoInfo();
                            var overBaseSize = new Size(overBaseInfo.width, overBaseInfo.height);
                            var xDiv = (double)OverInfo.Width / overBaseInfo.width;
                            var yDiv = (double)OverInfo.Height / overBaseInfo.height;
                            var overMaskBase = OverlayMask == null ? null : StepResize(OverlayMask, step - 1, minOverBaseSize);

                            var correction = initStep || angle1 == 0 && angle2 == 0 ? config.Correction : (config.Correction + config.RotationCorrection);

                            var defArea = Math.Min(SrcInfo.AspectRatio, OverInfo.AspectRatio) /
                                Math.Max(SrcInfo.AspectRatio, OverInfo.AspectRatio) * 100;
                            if (config.MinSourceArea <= double.Epsilon)
                                config.MinSourceArea = defArea;
                            if (config.MinOverlayArea <= double.Epsilon)
                                config.MinOverlayArea = defArea;

                            var minIntersectArea = (int)(srcScaledArea * config.MinSourceArea / 100.0);
                            var maxOverlayArea = (int)(srcScaledArea / (config.MinOverlayArea / 100.0));

                            var testParams = new HashSet<TestOverlay>();

                            if (fakeStep && !initStep)
                            {
                                var best = subResultSet.Min;
                                var info = new OverlayData
                                {
                                    Source = new Rectangle(Point.Empty, SrcInfo.Size),
                                    Overlay = best.Overlay.Scale(coefDiff),
                                    SourceBaseSize = srcBaseSize,
                                    OverlayBaseSize = overBaseSize,
                                    OverlayAngle = best.OverlayAngle,
                                    OverlayWarp = best.OverlayWarp.Scale(coefDiff)
                                };
                                if (step == 1)
                                    info = RepeatImpl(info, n);
                                subResultSet = [info];
                            }
                            else
                            {
                                var bests = new List<OverlayData>(subResultSet);
                                subResultSet.Clear();
                                foreach (var best in bests)
                                {
                                    var bestOverSize = best.Overlay.Size;

                                    var minWidth = Math.Sqrt(minIntersectArea * minAspectRatio).Floor();
                                    var maxWidth = Math.Sqrt(maxOverlayArea * maxAspectRatio).Ceiling();

                                    if (!initStep)
                                    {
                                        minWidth = Math.Max(minWidth, ((bestOverSize.Width - correction) * coefDiff).Floor());
                                        maxWidth = Math.Min(maxWidth, ((bestOverSize.Width + correction) * coefDiff).Ceiling());
                                    }

                                    var minArea = Math.Min(
                                        config.MinArea * coefCurrent * coefCurrent,
                                        maxWidth * Round(maxWidth / minAspectRatio));

                                    var maxArea = Math.Max(
                                        Round(config.MaxArea * coefCurrent * coefCurrent),
                                        minWidth * Round(minWidth / maxAspectRatio));

                                    var warpStep = config.WarpSteps - step + 1 + Math.Min(config.WarpOffset, stepCount - config.WarpSteps);
                                    var warperator = new WarpIterator(warpPoints, best.OverlayWarp, OverInfo.Size,
                                        overBaseSize, warpStep, config.WarpSteps); //TODO warp scale
                                    foreach (var warp in warperator)
                                    {
                                        if (!warp.IsEmpty)
                                        {
                                            DynamicEnvironment.SetOwner(warp);
                                            Log(() => $"Step: {step} Warp: {warp}");
                                        }

                                        for (var width = minWidth; width <= maxWidth; width++)
                                        {
                                            var minHeight = (width / maxAspectRatio).Floor();
                                            var maxHeight = (width / minAspectRatio).Ceiling();

                                            if (!initStep)
                                            {
                                                minHeight = Math.Max(minHeight, ((bestOverSize.Height - correction) * coefDiff).Floor());
                                                maxHeight = Math.Min(maxHeight, ((bestOverSize.Height + correction) * coefDiff).Ceiling());
                                            }

                                            for (var height = minHeight; height <= maxHeight; height++)
                                            {
                                                var area = width * height;
                                                if (area < minArea || area > maxArea)
                                                    continue;

                                                var crop = RectangleD.Empty;

                                                if (config.FixedAspectRatio)
                                                {
                                                    var cropWidth = (float)Math.Max(0, height * maxAspectRatio - width) / 2;
                                                    cropWidth *= (float)overBase.GetVideoInfo().width / width;
                                                    var cropHeight = (float)Math.Max(0, width / maxAspectRatio - height) / 2;
                                                    cropHeight *= (float)overBase.GetVideoInfo().height / height;
                                                    crop = RectangleD.FromLTRB(cropWidth, cropHeight, cropWidth, cropHeight);
                                                }

                                                Rectangle searchArea;
                                                if (initStep)
                                                {
                                                    searchArea = new Rectangle(
                                                        1 - width,
                                                        1 - height,
                                                        width + srcScaledWidth - 2,
                                                        height + srcScaledHeight - 2
                                                    );
                                                }
                                                else
                                                {
                                                    var coefArea = new Space(width, height) / bestOverSize.AsSpace();
                                                    var left = Math.Max(config.MinX * coefCurrent, (best.Overlay.X - correction) * coefArea.X).Floor();
                                                    var top = Math.Max(config.MinY * coefCurrent, (best.Overlay.Y - correction) * coefArea.Y).Floor();
                                                    searchArea = Rectangle.FromLTRB(left, top,
                                                        Math.Max(left, Math.Min(config.MaxX * coefCurrent, (best.Overlay.X + correction) * coefArea.X)).Ceiling(),
                                                        Math.Max(top, Math.Min(config.MaxY * coefCurrent, (best.Overlay.Y + correction) * coefArea.Y)).Ceiling()
                                                    );
                                                }

                                                double angleFrom = angle1, angleTo = angle2;

                                                var angles = new List<double>();

                                                if (!initStep)
                                                {
                                                    var lookupRange = Math.Min(config.MaxAngleStep*2, angleTo - angleFrom);
                                                    var lookup = lookupRange / Math.Pow(1.5, stepCount - step + 1);
                                                    angleFrom = Math.Max(angleFrom, best.OverlayAngle - lookup);
                                                    angleTo = Math.Min(angleTo, best.OverlayAngle + lookup);
                                                }

                                                var angleRange = angleTo - angleFrom;

                                                var angleStep = angleRange / config.AngleStepCount;
                                                if (angleStep > config.MaxAngleStep)
                                                    angleStep = angleRange / ((int)(angleRange / config.MaxAngleStep) + 1);

                                                if (angleStep == 0)
                                                {
                                                    angles.Add(angleFrom);
                                                }
                                                else
                                                {
                                                    for (var angle = angleFrom; angle <= angleTo; angle += angleStep)
                                                        if (Math.Sign(angleFrom) == Math.Sign(angleTo) || Math.Abs(angle) >= config.MinAngleStep)
                                                            angles.Add(angle);

                                                    if (angleFrom < 0 && angleTo > 0)
                                                        angles.Add(0);

                                                    if (height == minHeight && width == minWidth)
                                                        Log(() => $"Step {step} AngleStep: {angleStep:F3} Angles: {string.Join(", ", angles.Select(p => $"{p:F3}"))}");
                                                }

                                                foreach (var angle in angles)
                                                {
                                                    testParams.Add(new TestOverlay
                                                    {
                                                        Size = new Size(width, height),
                                                        Angle = (float)angle,
                                                        SearchArea = searchArea,
                                                        WarpPoints = warp,
                                                        Crop = crop,
#if DEBUG
                                                            Watch = avsWatch
#endif
                                                    });
                                                }
                                            }
                                        }

                                        var results = PerformTest(testParams, n,
                                            srcBase, srcMaskBase, overBase, overMaskBase,
                                            minIntersectArea, config.MinOverlayArea);
                                        testParams.Clear();

                                        DynamicEnvironment.SetOwner(null);

                                        if (results.Any())
                                        {
                                            var first = results.First();
                                            warperator.Analyze(first.Diff);
                                            minWidth = Math.Max(minWidth, first.Overlay.Width - correction).Floor();
                                            maxWidth = Math.Min(minWidth, first.Overlay.Width + correction).Ceiling();
                                        }

                                        foreach (var res in results)
                                            subResultSet.Add(res);
                                    }
                                }

                                var acceptedResults = subResultSet.TakeWhile((p, i) =>
                                    i < config.Branches && p.Diff - subResultSet.Min.Diff < config.BranchMaxDiff).ToArray();
                                var acceptedWarps = new HashSet<Warp>(acceptedResults.Select(p => p.OverlayWarp));
                                var expiredWarps = subResultSet.Select(p => p.OverlayWarp)
                                    .Where(p => !acceptedWarps.Contains(p));
                                foreach (var warp in expiredWarps)
                                    DynamicEnvironment.OwnerExpired(warp);
                                subResultSet = new(acceptedResults);
                                if (acceptedResults.Any() && StickLevel > 0) 
                                    subResultSet.Add(FindBest(acceptedResults));
                            }

                            foreach (var best in subResultSet)
                            {
                                best.OverlayWarp = best.OverlayWarp.Scale(xDiv, yDiv);
                                Log(() =>
                                    $"Step: {step} X,Y: ({best.Overlay.X:F0},{best.Overlay.Y}) Size: {best.Overlay.Width:F0}x{best.Overlay.Height:F0} " +
                                    $"({best.GetOverlayInfo().OverlayAspectRatio:F2}:1) Angle: {best.OverlayAngle:F3} Warp: {best.OverlayWarp} " +
                                    $"Diff: {best.Diff:F4} Branches: {subResultSet.Count}");
                            }
                        }

                        if (adjusted || !subResultSet.Any())
                        {
                            break;
                        }

                        var adjustedClip = AdjustColor(srcPrepared, overPrepared, subResultSet.First());
                        if (ColorAdjust == 0)
                            overPrepared = adjustedClip;
                        else srcPrepared = adjustedClip;
                        adjusted = true;
                    }
#if DEBUG
                    extraWatch.Start();
#endif
                    var subResults = AutoAlignSubpixel(n, subResultSet, config, srcPrepared, overPrepared);

#if DEBUG

                    extraWatch.Stop();
                    totalWatch.Stop();
                    Log(() =>
                        $"Total: {totalWatch.ElapsedMilliseconds} ms. " +
                        $"Subpixel: {extraWatch.ElapsedMilliseconds} ms. " +
                        $"Avs time: {avsWatch.ElapsedMilliseconds} ms.");
#endif

                    resultSet.UnionWith(subResults);
                    if (FindBest(resultSet).Diff <= config.AcceptableDiff)
                        break;
                }
            }

            if (!resultSet.Any())
                return OverlayInfo.EMPTY;
            var result = FindBest(resultSet).GetOverlayInfo();

            result.FrameNumber = n;
            if (parentWarp != null)
                result.OverlayWarp = parentWarp;

            return result;
        }

        public OverlayInfo Enhance(OverlayInfo prototype, int n)
        {
            var input = new OverlayInput
            {
                SourceSize = SrcInfo.Size,
                OverlaySize = OverInfo.Size,
                TargetSize = SrcInfo.Size,
                FixedSource = true
            };
            var prototypeData = OverlayMapper.For(input, prototype, default).GetOverlayData();

            using (new VideoFrameCollector())
            using (new DynamicEnvironment(StaticEnv))
            {
                var srcClip = sourcePrepared;
                var overClip = overlayPrepared;
                var adjustedClip = AdjustColor(srcClip, overClip, prototypeData);
                if (ColorAdjust == 0)
                    overClip = adjustedClip;
                else if (ColorAdjust == 1)
                    srcClip = adjustedClip;

                var updatedData = RepeatImpl(prototypeData, n, srcClip, overClip);
                prototypeData.Diff = updatedData.Diff;

                var enhancedData = AutoAlignSubpixel(n, prototypeData.Enumerate(), GetConfigs().First(), srcClip, overClip);
                return FindBest(enhancedData).GetOverlayInfo();
            }
        }

        private SortedSet<OverlayData> AutoAlignSubpixel(
            int frameNumber,
            IEnumerable<OverlayData> overlayData,
            OverlayConfigInstance config,
            dynamic src, dynamic over)
        {
            var subResults = new SortedSet<OverlayData>(overlayData);
            var bestCrops = subResults.ToHashSet();

            var (minAspectRatio, maxAspectRatio) = FindMinMaxAr(config);

            var allTests = new HashSet<TestOverlay>();
            var rect = bestCrops.First().GetOverlayInfo().OverlayRectangle;

            if (!config.FixedAspectRatio)
            {
                var (minAr, maxAr) = CorrectAspectRatio(rect.Size, config, 0.5);
                minAspectRatio = Math.Max(minAr, minAspectRatio);
                maxAspectRatio = Math.Min(maxAr, maxAspectRatio);
            }

            for (var substep = 1; substep <= config.Subpixel; substep++)
            {
                var cropCoef = Math.Pow(2, -substep);
                var testParams = new HashSet<TestOverlay>();

                var cropStepHorizontal = cropCoef * OverInfo.Width / rect.Width;
                var cropStepVertical = cropCoef * OverInfo.Height / rect.Height;

                foreach (var bestCrop in bestCrops)
                {
                    var data = bestCrop;
                    var crop = data.OverlayCrop;
                    for (var cropLeftCoef = -1; cropLeftCoef <= 1; cropLeftCoef++)
                    for (var cropTopCoef = -1; cropTopCoef <= 1; cropTopCoef++)
                    for (var cropRightCoef = -1; cropRightCoef <= 1; cropRightCoef++)
                    for (var cropBottomCoef = -1; cropBottomCoef <= 1; cropBottomCoef++)
                    {
                        var x = data.Overlay.X;
                        var y = data.Overlay.Y;

                        var width = data.Overlay.Width;
                        var height = data.Overlay.Height;

                        var cropLeft = crop.Left + cropLeftCoef * cropStepHorizontal;
                        var cropTop = crop.Top + cropTopCoef * cropStepVertical;
                        var cropRight = crop.Right + cropRightCoef * cropStepHorizontal;
                        var cropBottom = crop.Bottom + cropBottomCoef * cropStepVertical;

                        if (cropLeft < 0)
                        {
                            cropLeft++;
                            width--;
                        }
                        else if (cropLeft > 1)
                        {
                            cropLeft--;
                            width++;
                            x--;
                        }

                        if (cropRight < 0)
                        {
                            cropRight++;
                            width--;
                        }
                        else if (cropRight > 1)
                        {
                            cropRight--;
                            height++;
                        }

                        if (cropTop < 0)
                        {
                            cropTop++;
                            height--;
                        }
                        else if (cropTop > 1)
                        {
                            cropTop--;
                            height++;
                            y--;
                        }

                        if (cropBottom < 0)
                        {
                            cropBottom++;
                            height--;
                        }
                        else if (cropBottom > 1)
                        {
                            cropBottom--;
                            height++;
                        }

                        if (config.FixedAspectRatio)
                        {
                            var orgWidth = OverInfo.Width - (cropLeft + cropRight);
                            var realWidth = (OverInfo.Width / orgWidth) * width;
                            var realHeight = realWidth / OverInfo.AspectRatio;
                            var orgHeight = OverInfo.Height / (realHeight / height);
                            cropBottom = OverInfo.Height - orgHeight - cropTop;
                        }

                        var actualWidth = width * (OverInfo.Width / (OverInfo.Width - (cropLeft + cropRight)));
                        var actualHeight = height * (OverInfo.Height / (OverInfo.Height - (cropTop + cropBottom)));

                        var actualAspectRatio = actualWidth / actualHeight;

                        var noCrop = cropLeft.IsNearlyZero() && cropTop.IsNearlyZero() && cropRight.IsNearlyZero() && cropBottom.IsNearlyZero();

                        var invalidAspectRatio = !config.FixedAspectRatio &&
                                                 (actualAspectRatio < minAspectRatio ||
                                                  actualAspectRatio > maxAspectRatio);

                        var searchArea = Rectangle.FromLTRB(
                            Math.Max(config.MinX, (x - config.Correction).Floor()),
                            Math.Max(config.MinY, (y - config.Correction).Floor()),
                            (x + config.Correction).Ceiling(),
                            (y + config.Correction).Ceiling());

                        var ignore = noCrop || invalidAspectRatio || searchArea.Width < 0 || searchArea.Height < 0;

                        if (ignore) continue;

                        var testInfo = new TestOverlay
                        {
                            Size = new Size(width, height),
                            Angle = bestCrop.OverlayAngle,
                            Crop = RectangleD.FromLTRB(cropLeft, cropTop, cropRight, cropBottom),
                            SearchArea = searchArea,
                            WarpPoints = bestCrop.OverlayWarp,
                        };
                        if (!allTests.Contains(testInfo))
                        {
                            testParams.Add(testInfo);
                            allTests.Add(testInfo);
                        }
                    }
                }

                var testResults = PerformTest(testParams, frameNumber,
                    src, SourceMask, over, OverlayMask, 0, 0);
                subResults.UnionWith(testResults);

                bestCrops = subResults.TakeWhile((p, i) => i < config.Branches && p.Diff - subResults.Min.Diff < config.BranchMaxDiff).ToHashSet();
                if (subResults.Any() && StickLevel > 0)
                    bestCrops.Add(FindBest(subResults));

                foreach (var best in bestCrops)
                    DisableLog(() => $"Substep: {substep} X,Y: ({best.Overlay.X:F2},{best.Overlay.Y:F2}) " +
                              $"Size: {best.GetOverlayInfo().OverlayRectangle.Width:F3}x{best.GetOverlayInfo().OverlayRectangle.Height:F3} " +
                              $"({best.GetOverlayInfo().OverlayAspectRatio:F2}:1) " +
                              $"Angle: {best.OverlayAngle:F2} Diff: {best.Diff:F4} Branches: {bestCrops.Count}");
            }

            return subResults;
        }

        private Clip AdjustColor(Clip src, Clip over, OverlayData data)
        {
            var info = data.GetOverlayInfo();
            var warp = info.OverlayWarp;
            return DynamicEnv.StaticOverlayRender(
                    src,
                    over,
                    info.OverlayRectangle.Location,
                    info.Angle,
                    info.OverlayRectangle.Size,
                    warpPoints: warp.IsEmpty ? null : warp.ToString(),
                    diff: info.Diff,
                    sourceMask: SourceMask,
                    overlayMask: OverlayMask,
                    opacity: 0,
                    gradient: 0,
                    preset: "fitsource",
                    upsize: Presize.StartsWith("Simd") ? Presize.Substring(4) : Presize,
                    colorAdjust: ColorAdjust,
                    invert: ColorAdjust == 0);
        }

        private OverlayData FindBest(IEnumerable<OverlayData> list)
        {
            list = list.OrderBy(p => p.Diff).ToList();
            var best = list.OrderBy(p => p.Diff).FirstOrDefault() ?? OverlayData.EMPTY;

            if (StickLevel > float.Epsilon)
            {
                var bestInfo = best.GetOverlayInfo();
                var sticked = list
                    .Where(p => p.Diff - best.Diff <= StickLevel)
                    .Select(p => new
                    {
                        Data = p,
                        Info = p.GetOverlayInfo(),
                        StickCriteriaCount = StickCriteria.Count(c => c.Invoke(p))
                    })
                    .Where(p => p.StickCriteriaCount > 0)
                    .Where(p => Math.Abs(p.Info.Placement.X - bestInfo.Placement.X) <= StickDistance)
                    .Where(p => Math.Abs(p.Info.Placement.Y - bestInfo.Placement.Y) <= StickDistance)
                    .Where(p => Math.Abs(p.Info.OverlayRectangle.Right - bestInfo.OverlayRectangle.Right) <= StickDistance)
                    .Where(p => Math.Abs(p.Info.OverlayRectangle.Bottom - bestInfo.OverlayRectangle.Bottom) <= StickDistance)
                    .OrderByDescending(p => p.StickCriteriaCount)
                    .ThenBy(p => p.Data.Diff)
                    .FirstOrDefault()?.Data;
                return sticked ?? best;
            }

            return best;
        }

        private class TestOverlay
        {
            public Size Size { get; set; }
            public RectangleD Crop { get; set; }
            public float Angle { get; set; }
            public Warp WarpPoints { get; set; } = Warp.Empty;
            public Rectangle SearchArea { get; set; }
#if DEBUG
            public Stopwatch Watch { get; set; }
#endif

            public bool Equals(TestOverlay other)
            {
                return Size == other.Size && Crop.Equals(other.Crop) &&
                       Math.Abs(Angle - other.Angle) < float.Epsilon && 
                       WarpPoints.Equals(other.WarpPoints) && SearchArea.Equals(other.SearchArea);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                return obj is TestOverlay o && Equals(o);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = Size.GetHashCode();
                    hashCode = (hashCode * 397) ^ Crop.GetHashCode();
                    hashCode = (hashCode * 397) ^ (int)(Angle * 10000000);
                    hashCode = (hashCode * 397) ^ WarpPoints.GetHashCode();
                    hashCode = (hashCode * 397) ^ SearchArea.GetHashCode();
                    return hashCode;
                }
            }

            public override string ToString()
            {
                return $"{nameof(Size)}: {Size}, " +
                       $"{nameof(Crop)}: ({Crop.Left}, {Crop.Top}, {Crop.Right}, {Crop.Bottom}), " +
                       $"{WarpPoints}, " +
                       $"{nameof(Angle)}: {Angle}, {nameof(SearchArea)}: {SearchArea}";
            }
        }

        internal OverlayInfo PanScanImpl(OverlayInfo precursor, int n)
        {
            return PanScanImpl(precursor, n, ScanDistance, ScanScale, false);
        }

        public OverlayInfo PanScanImpl(OverlayInfo precursor, int n, int delta, double scale, bool ignoreAspectRatio = true, double arVariance = 0.002)
        {
            precursor = precursor.ScaleBySource(SrcInfo.Size);
            var configs = GetConfigs().Select(config =>
            {
                var (minAr, maxAr) = ignoreAspectRatio ? (0, int.MaxValue) : FindMinMaxAr(config);
                if (!config.FixedAspectRatio) //TODO fix
                {
                    minAr = Math.Min(Math.Max(precursor.OverlayAspectRatio * (1 - arVariance), minAr), maxAr);
                    maxAr = Math.Min(Math.Max(precursor.OverlayAspectRatio * (1 + arVariance), minAr), maxAr);
                }
                return config with
                {
                    MinX = Math.Max(config.MinX, (int)(precursor.Placement.X - delta)),
                    MaxX = Math.Min(config.MaxX, Round(precursor.Placement.X + delta)),
                    MinY = Math.Max(config.MinY, (int)(precursor.Placement.Y - delta)),
                    MaxY = Math.Min(config.MaxY, Round(precursor.Placement.Y + delta)),
                    Angle1 = precursor.Angle, //TODO fix
                    Angle2 = precursor.Angle, //TODO fix
                    AspectRatio1 = minAr,
                    AspectRatio2 = maxAr,
                    MinArea = Math.Max(config.MinArea, (int)(precursor.OverlaySize.Area * (1 - scale))),
                    MaxArea = Math.Min(config.MaxArea, (int)Math.Ceiling(precursor.OverlaySize.Area * (1 + scale))),
                    WarpPoints = []
                };
            }).ToArray();
            return AutoAlign(n, configs, precursor.OverlayWarp, precursor);
        }

        public OverlayInfo RepeatImpl(OverlayInfo repeatInfo, int n)
        {
            var input = new OverlayInput
            {
                SourceSize = SrcInfo.Size,
                OverlaySize = OverInfo.Size,
                TargetSize = SrcInfo.Size,
                FixedSource = true
            };
            var info = RepeatImpl(OverlayMapper.For(input, repeatInfo, default).GetOverlayData(), n).GetOverlayInfo();
            info.FrameNumber = n;
            info.CopyFrom(repeatInfo);
            return info;
        }

        public OverlayData RepeatImpl(OverlayData testInfo, int n)
        {
            using (new VideoFrameCollector())
            using (new DynamicEnvironment(StaticEnv))
            {
                var srcClip = sourcePrepared;
                var overClip = overlayPrepared;
                var adjustedClip = AdjustColor(srcClip, overClip, testInfo);
                if (ColorAdjust == 0)
                    overClip = adjustedClip;
                else if (ColorAdjust == 1)
                    srcClip = adjustedClip;

                return RepeatImpl(testInfo, n, srcClip, overClip);
            }
        }

        private OverlayData RepeatImpl(OverlayData testInfo, int n, Clip srcClip, Clip overClip)
        {
            var src = srcClip.GetFrame(n, StaticEnv);
            var srcMask = SourceMask?.GetFrame(n, StaticEnv);
            var overMaskClip = OverlayMask;
            if (overMaskClip == null && !testInfo.OverlayAngle.IsNearlyZero())
                overMaskClip = AvsUtils.GetBlankClip(overClip, true);

            VideoFrame overMask = ResizeRotate(overMaskClip, Resize, Rotate, testInfo)?[n];
            VideoFrame over = ResizeRotate(overClip, Resize, Rotate, testInfo)?[n];
            var searchArea = new Rectangle(testInfo.Overlay.Location, new Size(1, 1));
            return FindBestIntersect(
                src, srcMask, testInfo.Source.Size,
                over, overMask, testInfo.Overlay.Size,
                searchArea, 0, 0);
        }

        private SortedSet<OverlayData> PerformTest(
            ICollection<TestOverlay> testParams,
            int n, Clip srcBase, Clip srcMaskBase, Clip overBase, Clip overMaskBase,
            int minIntersectArea, double minOverlayArea)
        {
            if (testParams.Count == 1)
            {
                var test = testParams.First();
                if (test.SearchArea is { Width: 0, Height: 0 })
                {
                    return
                    [
                        new()
                        {
                            Source = new Rectangle(Point.Empty, srcBase.GetSize()),
                            Overlay = new(test.SearchArea.Location, test.Size),
                            OverlayBaseSize = overBase.GetSize(),
                            OverlayCrop = test.Crop,
                            OverlayAngle = test.Angle,
                            OverlayWarp = test.WarpPoints,
                            SourceBaseSize = srcBase.GetSize()
                        }
                    ];
                }
            }

            var srcSize = srcBase.GetSize();
            var overBaseSize = overBase.GetSize();
            var hq = overBaseSize.Equals(OverInfo.Size);
            var resizeFunc = hq ? Resize : Presize;

#if DEBUG
            testParams.FirstOrDefault()?.Watch?.Start();
#endif
            using var srcFrame = srcBase.GetFrame(n, StaticEnv);
            using var srcMaskFrame = srcMaskBase?.GetFrame(n, StaticEnv);
#if DEBUG
            testParams.FirstOrDefault()?.Watch?.Stop();
#endif

            var tasks = from test in testParams
                        let transform = new
                        {
                            test.Size,
                            test.Crop,
                            test.Angle,
                            test.WarpPoints,
#if DEBUG
                    test.Watch
#endif
                        }
                        group test by transform
                into testGroup
                        let searchAreas = testGroup.Select(p => p.SearchArea)

                        let maxArea = searchAreas.Aggregate(searchAreas.First(), Rectangle.Union)
                        let realCrop = testGroup.Key.Crop
                        let realResizeFunc = realCrop.IsEmpty || !resizeFunc.StartsWith("simd") ? resizeFunc : resizeFunc.Substring(4)
                        let excess = testGroup.Key.Angle != 0 || realResizeFunc.StartsWith("simd") ? Rectangle.Empty : Rectangle.FromLTRB(
                            Math.Max(0, -maxArea.Right),
                            Math.Max(0, -maxArea.Bottom),
                            Math.Max(0, testGroup.Key.Size.Width + maxArea.Left - srcSize.Width),
                            Math.Max(0, testGroup.Key.Size.Height + maxArea.Top - srcSize.Height))
                        let activeWidth = testGroup.Key.Size.Width - excess.Left - excess.Right
                        let activeHeight = testGroup.Key.Size.Height - excess.Top - excess.Bottom
                        let widthCoef = (double)overBaseSize.Width / testGroup.Key.Size.Width
                        let heightCoef = (double)overBaseSize.Height / testGroup.Key.Size.Height
                        let activeCrop = RectangleD.FromLTRB(
                            realCrop.Left + excess.Left * widthCoef,
                            realCrop.Top + excess.Top * heightCoef,
                            realCrop.Right + excess.Right * widthCoef,
                            realCrop.Bottom + excess.Bottom * heightCoef)
                        let activeSearchAreas = searchAreas.Select(searchArea => searchArea with
                        {
                            X = searchArea.X + excess.Left,
                            Y = searchArea.Y + excess.Top
                        })
#if DEBUG
                let watchStart = testGroup.Key.Watch?.Also(p => p.Start())
#endif
                        let overClip = ResizeRotate(overBase, realResizeFunc, Rotate, activeWidth, activeHeight, testGroup.Key.Angle, activeCrop, testGroup.Key.WarpPoints)
                        let over = (VideoFrame)overClip.GetFrame(n, StaticEnv)
                        let alwaysNullMask = overMaskBase == null && testGroup.Key.Angle == 0
                        let rotationMask = overMaskBase == null && testGroup.Key.Angle != 0
                        let overMask = alwaysNullMask
                            ? null
                            : (VideoFrame)ResizeRotate(rotationMask ? AvsUtils.GetBlankClip(overBase, true) : overMaskBase,
                                realResizeFunc, Rotate, activeWidth, activeHeight, testGroup.Key.Angle, activeCrop, testGroup.Key.WarpPoints)[n]
                        let overSize = new Size(activeWidth, activeHeight)
#if DEBUG
                let watchStop = testGroup.Key.Watch?.Also(p => p.Stop())
#endif
                        select new { srcFrame, srcMaskFrame, srcSize, over, overMask, overSize, testGroup, activeSearchAreas, excess, overBaseSize };


            var tuples = tasks.SelectMany(task => task.activeSearchAreas.Select(searchArea => new
            {
                task,
                searchArea
            }));
            var array = Task.WhenAll(tuples.Select(tuple => Task.Run(() =>
            {
                var task = tuple.task;
                var searchArea = tuple.searchArea;
                var stat = FindBestIntersect(
                    task.srcFrame, task.srcMaskFrame, task.srcSize,
                    task.over, task.overMask, task.overSize,
                    searchArea, minIntersectArea, minOverlayArea);
                stat.OverlayAngle = task.testGroup.Key.Angle;
                stat.OverlayCrop = task.testGroup.Key.Crop;
                stat.OverlayWarp = task.testGroup.Key.WarpPoints;
                stat.OverlayBaseSize = task.overBaseSize;
                stat.Source = new Rectangle(Point.Empty, task.srcSize);
                stat.Overlay = new Rectangle(
                    stat.Overlay.X - task.excess.X,
                    stat.Overlay.Y - task.excess.Y,
                    task.testGroup.Key.Size.Width,
                    task.testGroup.Key.Size.Height);
                stat.SourceBaseSize = task.srcSize;

                return (stat, task); // task is important to avoid premature disposing
            })))
            .Result;
            return new(array.Select(p => p.stat));
        }

        private OverlayData FindBestIntersect(
            VideoFrame src, VideoFrame srcMask, Size srcSize,
            VideoFrame over, VideoFrame overMask, Size overSize,
            Rectangle searchArea, int minIntersectArea, double minOverlayArea)
        {
            var pixelSize = src.GetRowSize() / srcSize.Width;

            var rgb = pixelSize == 3;

            var srcData = src.GetReadPtr();
            var srcStride = src.GetPitch();
            var overStride = over.GetPitch();
            var overData = over.GetReadPtr();
            var srcMaskData = srcMask?.GetReadPtr() ?? IntPtr.Zero;
            var srcMaskStride = srcMask?.GetPitch() ?? 0;
            var overMaskStride = overMask?.GetPitch() ?? 0;
            var overMaskData = overMask?.GetReadPtr() ?? IntPtr.Zero;
            var depth = SrcInfo.ColorSpace.GetBitDepth();

            var best = new OverlayData
            {
                Diff = double.MaxValue,
                Overlay = new Rectangle(searchArea.Location, overSize)
            };

            if (searchArea is { Width: 0, Height: 0 })
                return best;

            var searchPoints = Enumerable.Range(searchArea.X, searchArea.Width + 1).SelectMany(x =>
                Enumerable.Range(searchArea.Y, searchArea.Height + 1).Select(y => new Point(x, y)));

            Parallel.ForEach(searchPoints, testPoint =>
            {
                var sampleHeight = Math.Min(overSize.Height - Math.Max(0, -testPoint.Y), srcSize.Height - Math.Max(0, testPoint.Y));
                var srcShift = Math.Max(0, testPoint.Y);
                var overShift = Math.Max(0, -testPoint.Y);
                if (rgb)
                {
                    srcShift = srcSize.Height - srcShift - sampleHeight;
                    overShift = overSize.Height - overShift - sampleHeight;
                }
                var srcOffset = srcData + srcShift * srcStride;
                var overOffset = overData + overShift * overStride;
                var srcMaskOffset = srcMaskData + srcShift * srcMaskStride;
                var overMaskOffset = overMaskData + overShift * overMaskStride;
                var sampleWidth = Math.Min(overSize.Width - Math.Max(0, -testPoint.X), srcSize.Width - Math.Max(0, testPoint.X));
                double sampleArea = sampleWidth * sampleHeight;

                if (sampleArea < minIntersectArea
                    || sampleArea / (overSize.Width * overSize.Height) < minOverlayArea / 100.0)
                    return;

                var srcRow = srcOffset + Math.Max(0, testPoint.X) * pixelSize;
                var overRow = overOffset + Math.Max(0, -testPoint.X) * pixelSize;
                var srcMaskRow = srcMaskOffset + Math.Max(0, testPoint.X) * pixelSize;
                var overMaskRow = overMaskOffset + Math.Max(0, -testPoint.X) * pixelSize;
                var srcMaskPtr = srcMask == null ? IntPtr.Zero : srcMaskRow;
                var overMaskPtr = overMask == null ? IntPtr.Zero : overMaskRow;
                var squaredSum = NativeUtils.SquaredDifferenceSum(
                    srcRow, srcStride, srcMaskPtr, srcMaskStride,
                    overRow, overStride, overMaskPtr, overMaskStride,
                    sampleWidth * pixelSize, sampleHeight, depth, SIMD);
                var rmse = Math.Sqrt(squaredSum);
                lock (best)
                    if (rmse < best.Diff)
                    {
                        best.Diff = rmse;
                        best.Overlay = new Rectangle(testPoint, overSize);
                    }
            });
            return best;
        }

        private (double min, double max) FindMinMaxAr(OverlayConfigInstance config)
        {
            if (config.AspectRatio1 != 0 && config.AspectRatio2 != 0)
                return (Math.Min(config.AspectRatio1, config.AspectRatio2), Math.Max(config.AspectRatio1, config.AspectRatio2));

            var (minDefaultRatio, maxDefaultRatio) = CorrectAspectRatio(OverInfo.Size, config);

            if (config.AspectRatio1 == 0 && config.AspectRatio2 == 0)
                return (minDefaultRatio, maxDefaultRatio);
            var knownAr = config.AspectRatio1 == 0 ? config.AspectRatio2 : config.AspectRatio1;
            return (Math.Min(knownAr, minDefaultRatio), Math.Max(knownAr, maxDefaultRatio));
        }

        private (double min, double max) CorrectAspectRatio(SizeD size, OverlayConfigInstance config, double add = 0)
        {
            var rotationCorr = config.Angle1 == 0 && config.Angle2 == 0 ? 0 : config.RotationCorrection;
            var correction = config.FixedAspectRatio ? 0 : config.Correction + rotationCorr;
            correction += add;

            var wider = size.Width > size.Height;
            var min = wider
                ? size.Width / (size.Height + correction)
                : (size.Width - correction) / size.Height;
            var max = wider
                ? size.Width / (size.Height - correction)
                : (size.Width + correction) / size.Height;
            return (min, max);
        }

        protected sealed override void Dispose(bool A_0)
        {
            form?.SafeInvoke(p => p.Close());
            sourcePrepared.Dispose();
            overlayPrepared.Dispose();
            OverlayStat.Dispose();
            base.Dispose(A_0);
        }
    }
}
