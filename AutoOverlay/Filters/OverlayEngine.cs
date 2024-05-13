using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
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
    "cc[StatFile]s" +
    "[BackwardFrames]i[ForwardFrames]i[SourceMask]c[OverlayMask]c" +
    "[MaxDiff]f[MaxDiffIncrease]f[MaxDeviation]f[ScanDistance]i[ScanScale]f[Stabilize]b" +
    "[StickLevel]f[StickDistance]f" +
    "[Configs]c[Presize]s[Resize]s[Rotate]s[Editor]b[Mode]s[ColorAdjust]i[SceneFile]s[SIMD]b[Debug]b",
    OverlayUtils.DEFAULT_MT_MODE)]
namespace AutoOverlay
{
    public class OverlayEngine : OverlayFilter
    {
        [AvsArgument(Required = true)]
        public Clip Source { get; set; }

        [AvsArgument(Required = true)]
        public Clip Overlay { get; set; }

        [AvsArgument]
        public string StatFile { get; set; }

        [AvsArgument(Min = 0, Max = 100)]
        public int BackwardFrames { get; set; } = 3;

        [AvsArgument(Min = 0, Max = 100)]
        public int ForwardFrames { get; set; } = 3;

        [AvsArgument]
        public Clip SourceMask { get; set; }

        [AvsArgument]
        public Clip OverlayMask { get; set; }

        [AvsArgument(Min = 0)]
        public double MaxDiff { get; set; } = 5;

        [AvsArgument(Min = 0)]
        public double MaxDiffIncrease { get; private set; } = 1;

        [AvsArgument(Min = 0)]
        public double MaxDeviation { get; private set; } = 1;

        [AvsArgument(Min = 0)]
        public int ScanDistance { get; private set; } = 0;

        [AvsArgument(Min = 0)]
        public double ScanScale { get; private set; } = 3;

        [AvsArgument]
        public bool Stabilize { get; private set; } = true;

        [AvsArgument(Min = 0, Max = 10)]
        public double StickLevel { get; set; } = 0;

        [AvsArgument(Min = 0, Max = 10)]
        public double StickDistance { get; set; } = 1;

        [AvsArgument]
        public OverlayConfig[] Configs { get; private set; }

        [AvsArgument]
        public string Presize { get; private set; } = OverlayUtils.DEFAULT_PRESIZE_FUNCTION;

        [AvsArgument]
        public string Resize { get; private set; } = OverlayUtils.DEFAULT_RESIZE_FUNCTION;

        [AvsArgument]
        public string Rotate { get; private set; } = OverlayUtils.DEFAULT_ROTATE_FUNCTION;

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

        public IOverlayStat OverlayStat { get; private set; }

        public ExtraVideoInfo SrcInfo { get; private set; }
        public ExtraVideoInfo OverInfo { get; private set; }

        public Clip sourcePrepared;

        public Clip overlayPrepared;

        private readonly ConcurrentDictionary<RepeatKey, OverlayInfo> repeatCache = new();
        private readonly ConcurrentDictionary<int, OverlayInfo> overlayCache = new();

        public event EventHandler<FrameEventArgs> CurrentFrameChanged;

        private IEnumerable<OverlayConfigInstance> GetConfigs() => Configs.Select(p => p.GetInstance());

        private Form form;

        public int[] SelectedFrames { get; private set; }

        public HashSet<int> KeyFrames { get; } = new();

        private static List<Predicate<OverlayData>> StickCriteria { get; } = new()
        {
            p => p.Overlay.Left == p.Source.Left,
            p => p.Overlay.Right == p.Source.Right,
            p => p.Overlay.Top == p.Source.Top,
            p => p.Overlay.Bottom == p.Source.Bottom,
        };

#if DEBUG
        Stopwatch totalWatch = new();
        Stopwatch diffWatch = new();
        Stopwatch extraWatch = new();
#endif

        protected override void AfterInitialize()
        {
            if (!Configs.Any())
                Configs = new[] { new OverlayConfig() };
            SrcInfo = Source.GetVideoInfo();
            OverInfo = Overlay.GetVideoInfo();
            if ((SrcInfo.ColorSpace ^ OverInfo.ColorSpace).HasFlag(ColorSpaces.CS_PLANAR))
                throw new AvisynthException("Both clips must be in planar or RGB color space");
            if (SrcInfo.ColorSpace.GetBitDepth() != OverInfo.ColorSpace.GetBitDepth())
                throw new AvisynthException("Both clips must have the same bit depth");

            sourcePrepared = Prepare(Source);
            overlayPrepared = Prepare(Overlay);

            MaxDeviation /= 100.0;

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
            vi.num_frames = Math.Min(SrcInfo.FrameCount, OverInfo.FrameCount);
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

            var cacheSize = ForwardFrames + BackwardFrames + 1;
            var cacheKey = StaticEnv.GetEnv2() == null ? CacheType.CACHE_25_ALL : CacheType.CACHE_GENERIC;
            Source.SetCacheHints(cacheKey, cacheSize);
            Overlay.SetCacheHints(cacheKey, cacheSize);
            sourcePrepared.SetCacheHints(cacheKey, cacheSize);
            overlayPrepared.SetCacheHints(cacheKey, cacheSize);
            SourceMask?.SetCacheHints(cacheKey, cacheSize);
            OverlayMask?.SetCacheHints(cacheKey, cacheSize);
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
            }
            ScanScale /= 1000;
            if (PanScanMode)
                Stabilize = false;
        }

        private Clip Prepare(Clip clip)
        {
            return clip.IsRealPlanar() && !clip.GetVideoInfo().IsRGB() ? clip.Dynamic().ExtractY() : clip;
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
            var frame = Debug ? GetSubtitledFrame(this + "\n" + info) : base.GetFrame(n);
            StaticEnv.MakeWritable(frame);
            unsafe
            {
                using var stream = new UnmanagedMemoryStream((byte*)frame.GetWritePtr().ToPointer(),
                    frame.GetRowSize()*frame.GetHeight(), frame.GetRowSize() * frame.GetHeight(), FileAccess.Write);
                using var writer = new BinaryWriter(stream);
                writer.Write(nameof(OverlayEngine));
                writer.Write(GetHashCode());
                info.Write(writer, info.Message);
                var history = new[] {-1, 1}
                    .SelectMany(sign => Enumerable.Range(1, OverlayUtils.ENGINE_HISTORY_LENGTH)
                        .Select(p => n + sign * p)
                        .Where(p => p >= 0)
                        .Select(p => OverlayStat[p])
                        .TakeWhile(p => p != null)
                        .OrderBy(p => p.FrameNumber));
                foreach (var overlayInfo in history)
                    overlayInfo.Write(writer);
            }
            return frame; 
        }

        private OverlayInfo Repeat(OverlayInfo testInfo, int n)
        {
            return repeatCache.GetOrAdd(new RepeatKey(testInfo, n), key => RepeatImpl(key.Info, key.FrameNumber));
        }

        private OverlayInfo PanScan(OverlayInfo testInfo, int n)
        {
            return repeatCache.GetOrAdd(new RepeatKey(testInfo, n), key => PanScanImpl(key.Info, key.FrameNumber));
        }

        private OverlayInfo AutoOverlay(int n)
        {
            return overlayCache.GetOrAdd(n, key =>
            {
                var stat = AutoOverlayImpl(n);
                repeatCache.TryAdd(new RepeatKey(stat, n), stat);
                return stat;
            });
        }

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
                if (Mode == OverlayEngineMode.UPDATE)
                {
                    var repeated = Repeat(existed, n);
                    if (Math.Abs(repeated.Diff - existed.Diff) > double.Epsilon)
                        return OverlayStat[n] = repeated;
                }
                existed.Message = "Cached frame found";
                return existed;
            }
            var info = GetOverlayInfoImpl(n, out var sb);
            Log(() => sb.ToString());
            if (Debug)
                info.Message = sb.ToString();
            else info.Message = "Frame successfully auto-aligned";
            return info;
        }

        private OverlayInfo GetDummyInfo(int n, string message)
        {
            return new()
            {
                FrameNumber = n,
                SourceSize = SrcInfo.Size,
                OverlaySize = OverInfo.Size,
                Diff = -1,
                Message = message
            };
        }

        private bool CheckDev(IEnumerable<OverlayInfo> sample)
        {
            return OverlayUtils.CheckDev(sample, MaxDiffIncrease, false);
        }

        public bool PanScanMode => ScanDistance > 0;

        private OverlayInfo GetOverlayInfoImpl(int n, out StringBuilder log)
        {
            log = new StringBuilder();
            log.AppendLine($"Frame: {n}");

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
                log.AppendLine($"New scene detected!");
                goto stabilize;
            }

            if (PanScanMode)
                prevFrames = prevFrames.TakeWhile((p, i) =>
                        i == 0 || p.NearlyEquals(prevFrames[i - 1], MaxDeviation))
                    .ToArray();
            else prevFrames = prevFrames.TakeWhile(p => p.Equals(prevInfo)).ToArray();

            var prevFramesCount = prevFrames.Length;

            log.AppendLine($"Prev frames: {prevFramesCount}");

            if (prevFramesCount == BackwardFrames)
            {
                log.AppendLine($"Analyze prev frames info:\n{prevInfo}");

                var info = PanScanMode ? PanScan(prevInfo, n) : Repeat(prevInfo, n);

                if (info.Diff > MaxDiff || !CheckDev(prevFrames.Append(info)))
                {
                    log.AppendLine($"Repeated diff: {info.Diff:F3} is not OK");
                    goto stabilize;
                }
                log.AppendLine($"Repeated diff: {info.Diff:F3} is OK");
                var checkFrames = prevFrames.Append(info).ToList();
                if (ForwardFrames > 0)
                {
                    log.AppendLine($"Analyze next frames: {ForwardFrames}");
                    var prevStat = info;
                    for (var nextFrame = n + 1;
                        nextFrame <= n + ForwardFrames && nextFrame < GetVideoInfo().num_frames && !KeyFrames.Contains(nextFrame);
                        nextFrame++)
                    {
                        log.AppendLine($"Next frame: {nextFrame}");
                        var stat = OverlayStat[nextFrame];
                        if (stat != null)
                        {
                            log.AppendLine($"Existed info found:\n{stat}");
                            if (stat.Equals(info))
                            {
                                log.AppendLine($"Existed info is equal");
                                if (stat.Diff <= MaxDiff && CheckDev(checkFrames.Append(stat)))
                                {
                                    log.AppendLine($"Existed info diff {stat.Diff:F3} is OK");
                                }
                                else
                                {
                                    log.AppendLine($"Existed info diff {stat.Diff:F3} is not OK");
                                    goto simple;
                                }
                            }
                            if (stat.NearlyEquals(info, MaxDeviation))
                            {
                                log.AppendLine($"Existed info is nearly equal. Pan&scan mode.");
                                if (ScanDistance == 0 || stat.Diff > MaxDiff || !CheckDev(checkFrames.Append(stat)))
                                    goto simple;
                                continue;
                            }
                            break;
                        }
                        prevStat = stat = PanScanMode ? PanScan(prevStat, nextFrame) : Repeat(info, nextFrame);
                        if (stat.Diff > MaxDiff || !CheckDev(checkFrames.Append(stat)))
                        {
                            log.AppendLine($"Repeated info diff {stat.Diff:F3} is not OK");
                            stat = AutoOverlay(nextFrame);
                            log.AppendLine($"Own info: {stat}");
                            if (stat.NearlyEquals(info, MaxDeviation))
                            {
                                log.AppendLine($"Own info is nearly equal. Pan&scan mode.");
                                goto simple;
                            }
                            log.AppendLine($"Next scene detected");
                            break;
                        }
                        log.AppendLine($"Repeated info diff: {stat.Diff:F3} is OK");
                    }
                }
                return OverlayStat[n] = info;
            }
            stabilize:
            if (Stabilize)
            {
                var info = AutoOverlay(n).Clone();
                if (info.Diff > MaxDiff)
                    goto simple;
                prevFrames = prevFrames.TakeWhile(p => p.Equals(info) && p.Diff <= MaxDiff).Take(BackwardFrames - 1).ToArray();
                prevFramesCount = prevFrames.Length;

                var stabilizeFrames = new List<OverlayInfo>(prevFrames) {info};
                for (var nextFrame = n + 1;
                    nextFrame < n + BackwardFrames - prevFramesCount &&
                    nextFrame < GetVideoInfo().num_frames;
                    nextFrame++)
                {
                    if (OverlayStat[nextFrame] != null)
                        goto simple;
                    var statOwn = AutoOverlay(nextFrame);
                    var statRepeated = Repeat(info, nextFrame);
                    stabilizeFrames.Add(statOwn);
                    if (!statRepeated.NearlyEquals(statOwn, MaxDeviation) || statRepeated.Diff > MaxDiff || !CheckDev(prevFrames.Concat(stabilizeFrames)))
                        goto simple;
                }

                var needAllNextFrames = false;
                if (n > 0)
                {
                    var prevStat = OverlayStat[n - 1] ?? AutoOverlay(n - 1);
                    if (prevStat.NearlyEquals(info, MaxDeviation) &&
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
                    for (var frame = n; frame < n + BackwardFrames - prevFramesCount && frame < GetVideoInfo().num_frames; frame++)
                    {
                        var stabInfo = Repeat(averageInfo, frame);
                        stabilizeFrames.Add(stabInfo);
                        if (stabInfo.Diff > MaxDiff || !CheckDev(stabilizeFrames))
                            goto simple;
                    }

                    info = stabilizeFrames.First();
                }
                for (var nextFrame = n + BackwardFrames - prevFramesCount;
                    nextFrame < n + BackwardFrames - prevFramesCount + ForwardFrames &&
                    nextFrame < GetVideoInfo().num_frames;
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
                        if (stat.NearlyEquals(info, MaxDeviation))
                        {
                            goto simple;
                        }
                        break;
                    }
                    stat = Repeat(info, nextFrame);
                    if (stat.Diff > MaxDiff || !CheckDev(stabilizeFrames.Append(stat)))
                    {
                        if (needAllNextFrames || AutoOverlay(nextFrame).NearlyEquals(info, MaxDeviation))
                            goto simple;
                        break;
                    }
                }
                for (var frame = n;
                    frame < n + BackwardFrames - prevFramesCount &&
                    frame < GetVideoInfo().num_frames;
                    frame++)
                    if (frame == n || OverlayStat[frame] == null)
                        OverlayStat[frame] = stabilizeFrames[frame - n + prevFramesCount]; // TODO BUG!!!!
                return info;
            }
            simple:
            return OverlayStat[n] = AutoOverlay(n);
        }

        private int Scale(int val, double coef) => (int)Math.Round(val * coef);

        private int Round(double val) => (int) Math.Round(val);

        public OverlayInfo AutoOverlayImpl(int n, IEnumerable<OverlayConfigInstance> configs = null, Warp parentWarp = null)
        {
            Log("\tAutoOverlay started: " + n);
#if DEBUG
            extraWatch.Reset();
            diffWatch.Reset();
            totalWatch.Restart();
#endif
            var resultSet = new SortedSet<OverlayData>();
            using (new VideoFrameCollector())
            using (new DynamicEnvironment(StaticEnv))
            {
                configs ??= GetConfigs();
                var srcPrepared = sourcePrepared;
                var overPrepared = overlayPrepared;
                if (parentWarp != null && !parentWarp.IsEmpty)
                    overPrepared = overPrepared.Dynamic().Warp(
                        parentWarp.ToArray(), relative: true,
                        resample: OverlayUtils.GetWarpResampleMode(Resize));
                foreach (var config in configs)
                {
                    var adjusted = ColorAdjust == -1;

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
                    var maxAspectRatio = Math.Max(config.AspectRatio1, config.AspectRatio2);
                    var minAspectRatio = Math.Min(config.AspectRatio1, config.AspectRatio2);
                    var minDimension = Math.Min(OverInfo.Width, OverInfo.Height);
                    var defaultShift = config.FixedAspectRatio ? 0 : (minDimension + config.Correction * 2.0) / minDimension - 1;
                    if (maxAspectRatio <= double.Epsilon)
                        maxAspectRatio = OverInfo.AspectRatio + defaultShift;
                    if (minAspectRatio <= double.Epsilon)
                        minAspectRatio = OverInfo.AspectRatio - defaultShift;

                    var angle1 = Math.Min(config.Angle1 % 360, config.Angle2 % 360);
                    var angle2 = Math.Max(config.Angle1 % 360, config.Angle2 % 360);

                    var subResultSet = new SortedSet<OverlayData>();
                    while (true)
                    {
                        int stepCount;
                        var warpPoints = adjusted ? config.WarpPoints.Select(p => (RectangleD)p).ToArray() : Array.Empty<RectangleD>();
                        for (stepCount = 0;; stepCount++)
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
                                var test1 = baseClip.GetFrame(n, StaticEnv);
                                VideoFrame test2 = testClip[n];
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
                            var xDiv = (double) OverInfo.Width / overBaseInfo.width;
                            var yDiv = (double) OverInfo.Height / overBaseInfo.height;
                            var overMaskBase = OverlayMask == null ? null : StepResize(OverlayMask, step - 1);

                            var defArea = Math.Min(SrcInfo.AspectRatio, OverInfo.AspectRatio) /
                                Math.Max(SrcInfo.AspectRatio, OverInfo.AspectRatio) * 100;
                            if (config.MinSourceArea <= double.Epsilon)
                                config.MinSourceArea = defArea;
                            if (config.MinOverlayArea <= double.Epsilon)
                                config.MinOverlayArea = defArea;

                            var minIntersectArea = (int) (srcScaledArea * config.MinSourceArea / 100.0);
                            var maxOverlayArea = (int) (srcScaledArea / (config.MinOverlayArea / 100.0));

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
                                subResultSet = new SortedSet<OverlayData> {info};
                            }
                            else
                            {
                                var bests = new List<OverlayData>(subResultSet);
                                subResultSet.Clear();
                                var warpCount = 0;
                                foreach (var best in bests)
                                {
                                    var minWidth = Round(Math.Sqrt(minIntersectArea * minAspectRatio));
                                    var maxWidth = Round(Math.Sqrt(maxOverlayArea * maxAspectRatio));

                                    if (!initStep)
                                    {
                                        minWidth = Math.Max(minWidth,
                                            (int) ((best.Overlay.Width - config.Correction) * coefDiff));
                                        maxWidth = Math.Min(maxWidth,
                                            Round((best.Overlay.Width + config.Correction) * coefDiff) + 1);
                                    }

                                    var minArea = Math.Min(
                                        config.MinArea * coefCurrent * coefCurrent,
                                        maxWidth * Round(maxWidth / minAspectRatio));

                                    var maxArea = Math.Max(
                                        Round(config.MaxArea * coefCurrent * coefCurrent),
                                        minWidth * Round(minWidth / maxAspectRatio));

                                    var warpStep = config.WarpSteps - step + 1 +
                                                   Math.Min(config.WarpOffset, stepCount - config.WarpSteps);
                                    var warperator = new WarpIterator(warpPoints, best.OverlayWarp, OverInfo.Size,
                                        overBaseSize, warpStep, config.WarpSteps); //TODO warp scale
                                    foreach (var warp in warperator)
                                    {
                                        warpCount++;
                                        Log(() => $"Step: {step} Warp: {warp}");
                                        if (!warp.IsEmpty)
                                            DynamicEnvironment.SetOwner(warp);

                                        for (var width = minWidth; width <= maxWidth; width++)
                                        {
                                            var minHeight = Round(width / maxAspectRatio);
                                            var maxHeight = Round(width / minAspectRatio);

                                            if (!initStep)
                                            {
                                                minHeight = Math.Max(minHeight,
                                                    (int) ((best.Overlay.Height - config.Correction) * coefDiff));
                                                maxHeight = Math.Min(maxHeight,
                                                    Round((best.Overlay.Height + config.Correction) * coefDiff) + 1);
                                            }

                                            for (var height = minHeight; height <= maxHeight; height++)
                                            {
                                                var area = width * height;
                                                if (area < minArea || area > maxArea)
                                                    continue;

                                                var crop = RectangleD.Empty;

                                                if (config.FixedAspectRatio)
                                                {
                                                    var cropWidth =
                                                        (float) Math.Max(0, height * maxAspectRatio - width) / 2;
                                                    cropWidth *= (float) overBase.GetVideoInfo().width / width;
                                                    var cropHeight = (float) Math.Max(0,
                                                        width / maxAspectRatio - height) / 2;
                                                    cropHeight *= (float) overBase.GetVideoInfo().height / height;
                                                    crop = RectangleD.FromLTRB(cropWidth, cropHeight, cropWidth,
                                                        cropHeight);
                                                }

                                                Rectangle searchArea;
                                                if (initStep)
                                                {
                                                    searchArea = new Rectangle(
                                                        -width + 1,
                                                        -height + 1,
                                                        width + srcScaledWidth - 2,
                                                        height + srcScaledHeight - 2
                                                    );
                                                }
                                                else
                                                {
                                                    var coefArea = (width * height) /
                                                                   (best.Overlay.Size.GetArea() * coefDiff);
                                                    searchArea = new Rectangle(
                                                        (int) ((best.Overlay.X - config.Correction) * coefArea),
                                                        (int) ((best.Overlay.Y - config.Correction) * coefArea),
                                                        Round(2 * coefArea * config.Correction) + 1,
                                                        Round(2 * coefArea * config.Correction) + 1
                                                    );
                                                }

                                                int oldMaxX = searchArea.Right - 1, oldMaxY = searchArea.Bottom - 1;
                                                searchArea.X = Math.Max(searchArea.X,
                                                    (int) (config.MinX * coefCurrent));
                                                searchArea.Y = Math.Max(searchArea.Y,
                                                    (int) (config.MinY * coefCurrent));
                                                searchArea.Width = Math.Max(1, Math.Min(oldMaxX - searchArea.X + 1,
                                                    Round(config.MaxX * coefCurrent) - searchArea.X + 1));
                                                searchArea.Height = Math.Max(1, Math.Min(oldMaxY - searchArea.Y + 1,
                                                    Round(config.MaxY * coefCurrent) - searchArea.Y + 1));

                                                double angleFrom = angle1, angleTo = angle2;

                                                if (!initStep)
                                                {
                                                    angleFrom = FindNextAngle(2, best.Overlay.Size, best.OverlayAngle,
                                                        angleFrom, false);
                                                    angleTo = FindNextAngle(2, best.Overlay.Size, best.OverlayAngle,
                                                        angleTo, true);
                                                }

                                                var size = Size.Empty;
                                                for (var angle = angleFrom; angle <= angleTo; angle++)
                                                {
                                                    var newSize = BilinearRotate.CalculateSize(width, height, angle);
                                                    if (!size.Equals(newSize))
                                                    {
                                                        size = newSize;

                                                        testParams.Add(new TestOverlay
                                                        {
                                                            Size = new Size(width, height),
                                                            Angle = size.Width == width && size.Height == height
                                                                ? 0
                                                                : (float) angle,
                                                            SearchArea = searchArea,
                                                            WarpPoints = warp,
                                                            Crop = crop
                                                        });
                                                    }
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
                                            minWidth = Math.Max(minWidth,
                                                (int) first.Overlay.Width - config.Correction);
                                            maxWidth = (int) first.Overlay.Width;
                                            //maxWidth = Math.Min(maxWidth, first.Width + config.Correction);
                                        }

                                        foreach (var res in results)
                                            subResultSet.Add(res);
                                    }
                                }

                                var acceptedResults = subResultSet.TakeWhile((p, i) =>
                                    i < config.Branches && p.Diff - subResultSet.Min.Diff < config.BranchMaxDiff);
                                if (warpCount > 1)
                                    Log(() => $"Step: {step}. Total warps: {warpCount}");
                                var acceptedWarps = new HashSet<Warp>(acceptedResults.Select(p => p.OverlayWarp));
                                var expiredWarps = subResultSet.Select(p => p.OverlayWarp)
                                    .Where(p => !acceptedWarps.Contains(p));
                                foreach (var warp in expiredWarps)
                                    DynamicEnvironment.OwnerExpired(warp);
                                subResultSet = new SortedSet<OverlayData>(acceptedResults);
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

                        var adjustedClip = AdjustClip(srcPrepared, overPrepared, subResultSet.First());
                        if (ColorAdjust == 0)
                            overPrepared = adjustedClip;
                        else srcPrepared = adjustedClip;
                        adjusted = true;
                    }
#if DEBUG
                    extraWatch.Start();
#endif
                    var subResults = new SortedSet<OverlayData>(subResultSet);
                    var bestCrops = subResults.ToList();
                    bestCrops.Add(FindBest(subResultSet));
                    for (var substep = 1; substep <= config.Subpixel; substep++)
                    {
                        var initialStep = substep == 1 ? 1 : 0;
                        var cropCoef = Math.Pow(2, -substep);
                        var testParams = new HashSet<TestOverlay>();
                        // if (substep == 1) subResults.Clear();


                        var rect = bestCrops.First().GetOverlayInfo().OverlayRectangle;
                        if (!config.FixedAspectRatio)
                        {
                            minAspectRatio = rect.Width > rect.Height
                                ? (rect.Width - config.Correction) / rect.Height
                                : rect.Width / (rect.Height + config.Correction);
                            maxAspectRatio = rect.Width > rect.Height
                                ? (rect.Width + config.Correction) / rect.Height
                                : rect.Width / (rect.Height - config.Correction);
                        }

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
                            for (var widthStep = -initialStep; widthStep <= 1; widthStep++)
                            for (var heightStep = -initialStep; heightStep <= 1; heightStep++)
                            {
                                var cropLeft = crop.Left + cropLeftCoef * cropStepHorizontal;
                                var cropTop = crop.Top + cropTopCoef * cropStepVertical;
                                var cropRight = crop.Right + cropRightCoef * cropStepHorizontal;
                                var cropBottom = crop.Bottom + cropBottomCoef * cropStepVertical;
                                var width = data.Overlay.Width + widthStep;
                                var height = data.Overlay.Height + heightStep;
                                if (config.FixedAspectRatio)
                                {
                                    var orgWidth = OverInfo.Width - (cropLeft + cropRight);
                                    var realWidth = (OverInfo.Width / orgWidth) * width;
                                    var realHeight = realWidth / OverInfo.AspectRatio;
                                    var orgHeight = OverInfo.Height / (realHeight / height);
                                    cropBottom = (int) (OverInfo.Height - orgHeight - cropTop);
                                }

                                var actualWidth = width + (width / (double) OverInfo.Width) * (cropLeft + cropRight);
                                var actualHeight = height + (height / (double) OverInfo.Height) * (cropTop + cropBottom);
                                var actualAspectRatio = actualWidth / actualHeight;

                                var x = Math.Max(config.MinX, data.Overlay.X);
                                var y = Math.Max(config.MinY, data.Overlay.Y);

                                var invalidCrop = cropLeft < 0 || cropTop < 0 || cropRight < 0 || cropBottom < 0
                                                  || (cropLeft == 0 && cropTop == 0 && cropRight == 0 && cropBottom == 0);

                                var invalidAspectRatio =
                                    (!config.FixedAspectRatio && actualAspectRatio <= minAspectRatio)
                                    || (!config.FixedAspectRatio && actualAspectRatio >= maxAspectRatio);

                                var searchArea = new Rectangle(x, y,
                                    Math.Min(2, config.MaxX - x + 1),
                                    Math.Min(2, config.MaxY - y + 1));

                                var ignore = invalidCrop || invalidAspectRatio || searchArea.Width < 1 || searchArea.Height < 1;


                                var testInfo = new TestOverlay
                                {
                                    Size = new Size(width, height),
                                    Angle = bestCrop.OverlayAngle,
                                    Crop = RectangleD.FromLTRB(cropLeft, cropTop, cropRight, cropBottom),
                                    SearchArea = searchArea,
                                    WarpPoints = bestCrop.OverlayWarp
                                };

                                if (!ignore)
                                    testParams.Add(testInfo);
                                //else if (!invalidCrop && invalidAspectRatio)
                                //    Log("Ignored: " + testInfo);
                            }
                        }

                        var testResults = PerformTest(testParams, n,
                            srcPrepared, SourceMask, overPrepared, OverlayMask, 0, 0);
                        subResults.UnionWith(testResults);

                        bestCrops = subResults.TakeWhile((p, i) => i < config.Branches && p.Diff - subResults.Min.Diff < config.BranchMaxDiff).ToList();

                        foreach (var best in bestCrops)
                            Log(() => $"Substep: {substep} X,Y: ({best.Overlay.X:F2},{best.Overlay.Y:F2}) " +
                                      $"Size: {best.GetOverlayInfo().OverlayRectangle.Width:F3}x{best.GetOverlayInfo().OverlayRectangle.Height:F3} " +
                                      $"({best.GetOverlayInfo().OverlayAspectRatio:F2}:1) " +
                                      $"Angle: {best.OverlayAngle:F2} Diff: {best.Diff:F4} Branches: {bestCrops.Count}");
                    }

#if DEBUG

                    extraWatch.Stop();
                    totalWatch.Stop();
                    Log(
                        $"Total: {totalWatch.ElapsedMilliseconds} ms. " +
                        $"Subpixel: {extraWatch.ElapsedMilliseconds} ms. " +
                        $"Diff: {diffWatch.ElapsedMilliseconds} ms. Step count: ");
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

        private Clip AdjustClip(Clip src, Clip over, OverlayData data)
        {
            var info = data.GetOverlayInfo();
            return DynamicEnv.StaticOverlayRender(
                    src,
                    over,
                    info.OverlayRectangle.Location,
                    info.Angle,
                    info.OverlayRectangle.Size,
                    warpPoints: info.OverlayWarp.ToString(),
                    diff: info.Diff,
                    sourceMask: SourceMask,
                    overlayMask: OverlayMask,
                    opacity: 0,
                    colorAdjust: ColorAdjust,
                    invert: ColorAdjust == 0)
                .Crop(0, 0, 0, 0);
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

        private sealed class RepeatKey
        {
            private readonly int hashCode;
            public OverlayInfo Info { get; }
            public int FrameNumber { get; }

            public RepeatKey(OverlayInfo info, int frameNumber)
            {
                Info = info;
                FrameNumber = frameNumber;
                hashCode = info.GetHashCode() + 37 * frameNumber;
            }

            public override int GetHashCode()
            {
                return hashCode;
            }

            public override bool Equals(object obj)
            {
                return obj is RepeatKey other && Equals(Info, other.Info) && Equals(FrameNumber, other.FrameNumber);
            }
        }

        private class TestOverlay
        {
            public Size Size { get; set; }
            public RectangleD Crop { get; set; }
            public float Angle { get; set; }
            public Warp WarpPoints { get; set; } = Warp.Empty;
            public Rectangle SearchArea { get; set; }

            public bool Equals(TestOverlay other)
            {
                return Size == other.Size && Crop.Equals(other.Crop) && 
                       Angle == other.Angle && WarpPoints.Equals(other.WarpPoints) && SearchArea.Equals(other.SearchArea);
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
                    hashCode = (hashCode * 397) ^ (int) (Angle * 10000000);
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

        private OverlayInfo PanScanImpl(OverlayInfo testInfo, int n)
        {
            return PanScanImpl(testInfo, n, ScanDistance, ScanScale, false);
        }

        public OverlayInfo PanScanImpl(OverlayInfo testInfo, int n, int delta, double scale, bool ignoreAspectRatio = true)
        {
            testInfo = testInfo.ScaleBySource(SrcInfo.Size);
            var configs = GetConfigs().Select(config =>
            {
                var ar1 = config.AspectRatio1;
                var ar2 = config.AspectRatio2;
                if (!config.FixedAspectRatio) //TODO fix
                {
                    ar1 = config.AspectRatio1 <= double.Epsilon ? OverInfo.AspectRatio : config.AspectRatio1;
                    ar2 = config.AspectRatio2 <= double.Epsilon ? OverInfo.AspectRatio : config.AspectRatio2;
                    var minAr = ignoreAspectRatio ? 0 : Math.Min(ar1, ar2);
                    var maxAr = ignoreAspectRatio ? int.MaxValue : Math.Max(ar1, ar2);
                    ar1 = Math.Min(Math.Max(testInfo.OverlayAspectRatio * 0.998, minAr), maxAr);
                    ar2 = Math.Min(Math.Max(testInfo.OverlayAspectRatio * 1.002, minAr), maxAr);
                }
                return config with
                {
                    MinX = Math.Max(config.MinX, (int)(testInfo.Placement.X - delta)),
                    MaxX = Math.Min(config.MaxX, Round(testInfo.Placement.X + delta)),
                    MinY = Math.Max(config.MinY, (int)(testInfo.Placement.Y - delta)),
                    MaxY = Math.Min(config.MaxY, Round(testInfo.Placement.Y + delta)),
                    Angle1 = testInfo.Angle, //TODO fix
                    Angle2 = testInfo.Angle, //TODO fix
                    AspectRatio1 = ar1,
                    AspectRatio2 = ar2,
                    MinArea = Math.Max(config.MinArea, (int)(testInfo.OverlaySize.Area * (1 - scale))),
                    MaxArea = Math.Min(config.MaxArea, (int)Math.Ceiling(testInfo.OverlaySize.Area * (1 + scale))),
                    WarpPoints = new List<Rectangle>()
                };
            }).ToArray();
            return AutoOverlayImpl(n, configs, testInfo.OverlayWarp);
        }

        private OverlayInfo RepeatImpl(OverlayInfo repeatInfo, int n)
        {
            var input = new OverlayInput
            {
                SourceSize = SrcInfo.Size,
                OverlaySize = OverInfo.Size,
                TargetSize = SrcInfo.Size,
                FixedSource = true
            };
            var info = RepeatImpl(OverlayMapper.For(input, repeatInfo).GetOverlayData(), n).GetOverlayInfo();
            info.FrameNumber = n;
            info.CopyFrom(repeatInfo);
            return info;
        }

        private OverlayData RepeatImpl(OverlayData testInfo, int n)
        {
            Log("\tRepeat started: " + n);

            using (new VideoFrameCollector())
            using (new DynamicEnvironment(StaticEnv))
            {
                var srcClip = sourcePrepared;
                var overClip = overlayPrepared;
                var adjustedClip = AdjustClip(srcClip, overClip, testInfo);
                if (ColorAdjust == 0)
                    overClip = adjustedClip;
                else if (ColorAdjust == 1)
                    srcClip = adjustedClip;

                var src = srcClip.GetFrame(n, StaticEnv);
                var srcMask = SourceMask?.GetFrame(n, StaticEnv);
                var overMaskClip = OverlayMask;
                if (overMaskClip == null && !testInfo.OverlayAngle.IsNearlyZero())
                    overMaskClip = OverlayUtils.GetBlankClip(overClip, true);

                VideoFrame overMask = ResizeRotate(overMaskClip, Resize, Rotate, testInfo)?[n];
                VideoFrame over = ResizeRotate(overClip, Resize, Rotate, testInfo)?[n];
                var searchArea = new Rectangle(testInfo.Overlay.Location, new Size(1, 1));
                return FindBestIntersect(
                    src, srcMask, testInfo.Source.Size,
                    over, overMask, testInfo.Overlay.Size,
                    searchArea, 0, 0);
            }
        }

        private SortedSet<OverlayData> PerformTest(
            ICollection<TestOverlay> testParams,
            int n, Clip srcBase, Clip srcMaskBase, Clip overBase, Clip overMaskBase,
            int minIntersectArea, double minOverlayArea)
        {
            if (testParams.Count == 1)
            {
                var test = testParams.First();
                if (test.SearchArea.Width * test.SearchArea.Height == 1)
                {
                    return new SortedSet<OverlayData>
                    {
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
                    };
                }
            }

            var results = new SortedSet<OverlayData>();
            var tasks = from test in testParams
                let transform = new { test.Size, test.Crop, test.Angle, test.WarpPoints }
                group test by transform
                into testGroup
                let searchAreas = testGroup.Select(p => p.SearchArea)
                let overBaseSize = new Size(overBase.GetVideoInfo().width, overBase.GetVideoInfo().height)
                let resizeFunc = overBaseSize.Equals(OverInfo.Size) ? Resize : Presize

                let maxArea = searchAreas.Aggregate(searchAreas.First(), Rectangle.Union)
                let excess = Rectangle.FromLTRB(
                    Math.Max(0, -maxArea.Right),
                    Math.Max(0, -maxArea.Bottom),
                    Math.Max(0, testGroup.Key.Size.Width + maxArea.Left - srcBase.GetVideoInfo().width),
                    Math.Max(0, testGroup.Key.Size.Height + maxArea.Top - srcBase.GetVideoInfo().height))
                let activeWidth = testGroup.Key.Size.Width - excess.Left - excess.Right
                let activeHeight = testGroup.Key.Size.Height - excess.Top - excess.Bottom
                let widthCoef = (double) overBaseSize.Width / testGroup.Key.Size.Width
                let heightCoef = (double) overBaseSize.Height / testGroup.Key.Size.Height
                let realCrop = testGroup.Key.Crop
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
                let src = srcBase.GetFrame(n, StaticEnv)
                let srcMask = srcMaskBase?.GetFrame(n, StaticEnv)
                let srcSize = new Size(srcBase.GetVideoInfo().width, srcBase.GetVideoInfo().height)
                let overClip = ResizeRotate(overBase, resizeFunc, Rotate, activeWidth, activeHeight, testGroup.Key.Angle, activeCrop, testGroup.Key.WarpPoints)
                let over = overClip.GetFrame(n, StaticEnv)
                let alwaysNullMask = overMaskBase == null && testGroup.Key.Angle == 0
                let rotationMask = overMaskBase == null && testGroup.Key.Angle != 0
                let overMask = (VideoFrame) (alwaysNullMask
                    ? null
                    : ResizeRotate(rotationMask ? OverlayUtils.GetBlankClip(overBase, true) : overMaskBase,
                        resizeFunc, Rotate, activeWidth, activeHeight, testGroup.Key.Angle, activeCrop, testGroup.Key.WarpPoints)[n])
                let overSize = new Size(activeWidth, activeHeight)
                select new { src, srcMask, srcSize, over, overMask, overSize, testGroup, activeSearchAreas, excess, overBaseSize };

            var tuples = tasks.SelectMany(task => task.activeSearchAreas.Select(searchArea => new {task, searchArea}));
            Task.WaitAll(tuples.Select(tuple => Task.Factory.StartNew(() =>
            {
                var task = tuple.task;
                var searchArea = tuple.searchArea;
#if DEBUG
                diffWatch.Start();
#endif

                OverlayData stat = FindBestIntersect(
                    task.src, task.srcMask, task.srcSize,
                    task.over, task.overMask, task.overSize,
                    searchArea, minIntersectArea, minOverlayArea);
#if DEBUG
                diffWatch.Stop(); 
#endif
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
                lock (results)
                    results.Add(stat);
                //Log($"Search finished: {stat.Overlay}: {stat.Diff}");
            })).ToArray());
            return results;
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

            var searchPoints = Enumerable.Range(searchArea.X, searchArea.Width).SelectMany(x =>
                Enumerable.Range(searchArea.Y, searchArea.Height).Select(y => new Point(x, y)));

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
                lock(best)
                    if (rmse < best.Diff)
                    {
                        best.Diff = rmse;
                        best.Overlay = new Rectangle(testPoint, overSize);
                    }
            });
            return best;
        }

        private static float FindNextAngle(int n, SizeF size, float baseAngle, double max, bool forward)
        {
            var tmpSize = BilinearRotate.CalculateSize((int) size.Width, (int) size.Height, baseAngle);
            var increment = forward ? 0.01f : -0.01f;
            var check = forward ? (Func<float, bool>)(angle => angle <= max) : (angle => angle >= max);
            for (var angle = baseAngle; check(angle); angle += increment)
            {
                var newSize = BilinearRotate.CalculateSize((int) size.Width, (int) size.Height, angle);
                if (!tmpSize.Equals(newSize))
                {
                    if (--n == 0)
                        return angle;
                    tmpSize = newSize;
                }
            }
            return (float) max;
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
