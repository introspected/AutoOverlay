using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using AutoOverlay.AviSynth;
using AutoOverlay.Histogram;
using AvsFilterNet;

namespace AutoOverlay.Overlay
{
    public class OverlayContext : IDisposable, ICloneable
    {
        public dynamic Source { get; }
        public dynamic Overlay { get; }
        public dynamic SourceMask { get; }
        public dynamic OverlayMask { get; }

        public OverlayRender Render { get; }

        public StaticContext Static { get; }

        public ExtraVideoInfo SourceInfo { get; }

        public ExtraVideoInfo OverlayInfo { get; }

        public ExtraVideoInfo TargetInfo { get; }

        public List<Clip> ExtraClips { get; }
        public List<dynamic> ExtraMasks { get; }
        public List<double> ExtraOpacity { get; }

        public YUVPlanes Plane { get; }
        public dynamic BackgroundClip { get; }

        public bool AdjustColor { get; }
        public string AdjustChannels { get; }

        public int DefaultColor { get; }
        public int BlankColor { get; } = -1;

        public List<HistogramCache> Cache { get; }

        private readonly HashSet<IDisposable> disposables = new();

        public OverlayContext(OverlayRender render, YUVPlanes plane)
        {
            Render = render;
            Plane = plane;
            AdjustColor = render.ColorAdjust > -double.Epsilon &&
                          (string.IsNullOrEmpty(render.AdjustChannels) || 
                           (render.AdjustChannels.ToUpper() + default(YUVPlanes).GetLetter()).Contains(plane.GetLetter()));

            Static = render.Invert
                ? new StaticContext(render.Overlay, render.Source, render.OverlayMask, render.SourceMask, render.BackgroundClip, plane)
                : new StaticContext(render.Source, render.Overlay, render.SourceMask, render.OverlayMask, render.BackgroundClip, plane);

            Source = Static.Source.Dynamic();
            Overlay = Static.Overlay.Dynamic();
            
            ExtraClips = Render.ExtraClips.Select(p => p.Clip.ExtractPlane(plane)).ToList();

            SourceInfo = Static.Source.GetVideoInfo();
            OverlayInfo = Static.Overlay.GetVideoInfo();
            BackgroundClip = Static.BackgroundClip?.Dynamic();

            TargetInfo = render.GetVideoInfo();

            SourceMask = PrepareMask(Static.SourceMask, Static.SourceBase.GetVideoInfo());
            OverlayMask = PrepareMask(Static.OverlayMask, Static.OverlayBase.GetVideoInfo());
            ExtraMasks = Render.ExtraClips.Select(p => PrepareMask(p.Mask, p.Clip.GetVideoInfo())).ToList();
            ExtraOpacity = Render.ExtraClips.Select(p => p.Opacity).ToList();

            disposables.Add(render.Source);
            disposables.Add(render.Overlay);
            disposables.Add(render.SourceMask);
            disposables.Add(render.OverlayMask);
            disposables.Add(Static.Source);
            disposables.Add(Static.Overlay);
            disposables.Add(Static.SourceMask);
            disposables.Add(Static.OverlayMask);

            if (plane.IsChroma())
            {
                TargetInfo = TargetInfo.Chroma;
            }

            var isRgb = TargetInfo.Info.IsRGB() || render.Matrix != null;
            DefaultColor = Plane.IsChroma() ? 0x808080 : isRgb ? 0 : 0x008080;
            if (render.BlankColor != -1)
            {
                BlankColor = render.BlankColor >= 0 ? render.BlankColor : isRgb ? 0 : 0x008080;
                if (Plane == YUVPlanes.PLANAR_U)
                    BlankColor <<= 8;
                else if (Plane == YUVPlanes.PLANAR_V)
                    BlankColor <<= 16;
                BlankColor &= 0xFFFFFF;
            }

            if (AdjustColor)
            {
                AdjustChannels = plane == default ? render.AdjustChannels : YUVPlanes.PLANAR_Y.GetLetter();

                if (render.ColorFramesCount > 0)
                {
                    var planes = TargetInfo.ColorSpace.HasFlag(ColorSpaces.CS_INTERLEAVED)
                        ? new[] { default(YUVPlanes) }
                        : (AdjustChannels ?? "yuv").ToCharArray()
                        .Select(p => Enum.Parse(typeof(YUVPlanes), "PLANAR_" + p, true))
                        .Cast<YUVPlanes>().ToArray();
                    var channels = TargetInfo.ColorSpace.HasFlag(ColorSpaces.CS_PLANAR)
                        ? new[] { 0 }
                        : (AdjustChannels ?? "rgb").ToLower().ToCharArray().Select(p => "bgr".IndexOf(p)).ToArray();
                    Cache = new List<HistogramCache>(2 + ExtraClips.Count)
                    {
                        new(planes, channels, render.SIMD, true,
                            SourceInfo.ColorSpace, OverlayInfo.ColorSpace,
                            SourceInfo.ColorSpace, render.ColorFramesCount, true, new ParallelOptions())
                    };
                    Cache.AddRange(Enumerable.Range(0, 1 + ExtraClips.Count)
                        .Select(i => new HistogramCache(planes, channels, render.SIMD, true,
                            OverlayInfo.ColorSpace, SourceInfo.ColorSpace,
                            OverlayInfo.ColorSpace, render.ColorFramesCount, true, new ParallelOptions())));
                }
            }

            dynamic PrepareMask(Clip mask, ExtraVideoInfo target)
            {
                if (mask != null)
                {
                    if (!target.Info.IsRGB() && mask.IsRealPlanar())
                        mask = mask.Dynamic().ExtractY();
                    if (plane.IsChroma() && !target.ColorSpace.WithoutSubSample())
                        mask = mask.Dynamic().BicubicResize(target.Chroma.Size);
                    disposables.Add(mask);
                }

                return mask?.Dynamic();
            }
        }

        public object Clone()
        {
            return MemberwiseClone();
        }

        public void Dispose()
        {
            foreach (var disposable in disposables)
            {
                disposable?.Dispose();
            }
            Cache?.ForEach(p => HistogramCache.Dispose(p.Id));
        }

        public class StaticContext
        {
            public Clip SourceBase { get; }
            public Clip OverlayBase { get; }
            public Clip Source { get; }
            public Clip Overlay { get; }
            public Clip SourceMask { get; }
            public Clip OverlayMask { get; }
            public Clip BackgroundClip { get; }

            public StaticContext(Clip source, Clip overlay, Clip sourceMask, Clip overlayMask, Clip backgroundClip, YUVPlanes plane)
            {
                SourceBase = source;
                OverlayBase = overlay;
                Source = SourceBase.ExtractPlane(plane);
                Overlay = OverlayBase.ExtractPlane(plane);
                SourceMask = sourceMask;
                OverlayMask = overlayMask;
                BackgroundClip = backgroundClip?.ExtractPlane(plane);
            }
        }

        public class FrameParams
        {
            public int MergedWidth { get; set; }
            public int MergedHeight { get; set; }
            public double MergedAr { get; set; }
            public double OutAr { get; set; }
            public bool Wider { get; set; }
            public int FinalWidth { get; set; }
            public int FinalHeight { get; set; }
            public int FinalX { get; set; }
            public int FinalY { get; set; }
        }
    }
}
