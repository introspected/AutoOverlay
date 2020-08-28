using System;
using System.Collections.Generic;
using AvsFilterNet;

namespace AutoOverlay.Overlay
{
    public class OverlayContext : IDisposable, ICloneable
    {
        public dynamic Source { get; }
        public dynamic Overlay { get; }
        public dynamic SourceMask { get; }
        public dynamic OverlayMask { get; }

        public StaticContext Static { get; }

        public ExtraVideoInfo SourceInfo { get; }

        public ExtraVideoInfo OverlayInfo { get; }

        public ExtraVideoInfo TargetInfo { get; }

        public YUVPlanes Plane { get; }

        public FramingMode Mode { get; }
        public int DefaultColor { get; }
        public int BlankColor { get; }

        private readonly HashSet<IDisposable> disposables = new HashSet<IDisposable>();

        public OverlayContext(OverlayRender render, YUVPlanes plane)
        {
            Plane = plane;
            Mode = render.Mode;

            Static = render.Invert
                ? new StaticContext(render.Overlay, render.Source, render.OverlayMask, render.SourceMask, plane)
                : new StaticContext(render.Source, render.Overlay, render.SourceMask, render.OverlayMask, plane);

            Source = Static.Source.Dynamic();
            Overlay = Static.Overlay.Dynamic();

            SourceInfo = Static.Source.GetVideoInfo();
            OverlayInfo = Static.Overlay.GetVideoInfo();

            TargetInfo = render.GetVideoInfo();

            SourceMask = PrepareMask(Static.SourceMask, render.Source.GetVideoInfo());
            OverlayMask = PrepareMask(Static.OverlayMask, render.Overlay.GetVideoInfo());

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

            DefaultColor = Plane.IsChroma() ? 0x808080 : TargetInfo.Info.IsRGB() ? 0 : 0x008080;
            BlankColor = render.BlankColor >= 0 ? render.BlankColor : TargetInfo.Info.IsRGB() ? 0 : 0x008080;
            if (Plane == YUVPlanes.PLANAR_U)
                BlankColor <<= 8;
            else if (Plane == YUVPlanes.PLANAR_V)
                BlankColor <<= 16;
            BlankColor &= 0xFFFFFF;

            dynamic PrepareMask(Clip mask, ExtraVideoInfo target)
            {
                if (mask != null)
                {
                    if (!target.Info.IsRGB() && mask.IsRealPlanar())
                        mask = mask.Dynamic().ExtractY();
                    if (plane.IsChroma() && !target.ColorSpace.WithoutSubSample())
                        mask = mask.Dynamic().BilinearResize(target.Chroma.Size);
                    disposables.Add(mask);
                }

                return mask?.Dynamic();
            }

            int Round(double val) => (int) Math.Round(val);
        }

        public FrameParams CalcFrame(OverlayInfo info)
        {
            var frame = new FrameParams();
            var srcSize = SourceInfo.Size;
            frame.MergedWidth = srcSize.Width + Math.Max(-info.X, 0) + Math.Max(info.Width + info.X - srcSize.Width, 0);
            frame.MergedHeight = srcSize.Height + Math.Max(-info.Y, 0) + Math.Max(info.Height + info.Y - srcSize.Height, 0);
            frame.MergedAr = frame.MergedWidth / (double) frame.MergedHeight;
            frame.OutAr = TargetInfo.Width / (double) TargetInfo.Height;
            var wider = frame.MergedAr > frame.OutAr;
            if (Mode == FramingMode.FitBorders)
                wider = !wider;
            frame.FinalWidth = wider ? TargetInfo.Width : (int) Math.Round(TargetInfo.Width * (frame.MergedAr / frame.OutAr));
            frame.FinalHeight = wider ? (int) Math.Round(TargetInfo.Height * (frame.OutAr / frame.MergedAr)) : TargetInfo.Height;
            frame.FinalX = wider ? 0 : (TargetInfo.Width - frame.FinalWidth) / 2;
            frame.FinalY = wider ? (TargetInfo.Height - frame.FinalHeight) / 2 : 0;
            return frame;
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
        }

        public class StaticContext
        {
            public Clip Source { get; }
            public Clip Overlay { get; }
            public Clip SourceMask { get; }
            public Clip OverlayMask { get; }

            public StaticContext(Clip source, Clip overlay, Clip sourceMask, Clip overlayMask, YUVPlanes plane)
            {
                Source = ExtractPlane(source, plane);
                Overlay = ExtractPlane(overlay, plane);
                SourceMask = sourceMask;
                OverlayMask = overlayMask;
            }

            Clip ExtractPlane(Clip clip, YUVPlanes plane)
            {
                return plane == default
                    ? clip
                    : clip.Dynamic().Invoke("Extract" + plane.GetLetter());
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
