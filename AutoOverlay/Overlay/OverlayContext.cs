﻿using System;
using System.Collections.Generic;
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

        public YUVPlanes Plane { get; }

        public bool AdjustColor { get; }
        public string AdjustChannels { get; }

        public FramingMode Mode { get; }
        public int DefaultColor { get; }
        public int BlankColor { get; }

        public HistogramCache SourceCache { get; }
        public HistogramCache OverlayCache { get; }

        private readonly HashSet<IDisposable> disposables = new();

        public OverlayContext(OverlayRender render, YUVPlanes plane)
        {
            Render = render;
            Plane = plane;
            Mode = render.Mode;
            AdjustColor = render.ColorAdjust > -double.Epsilon &&
                          (string.IsNullOrEmpty(render.AdjustChannels) || 
                           (render.AdjustChannels.ToUpper() + default(YUVPlanes).GetLetter()).Contains(plane.GetLetter()));

            Static = render.Invert
                ? new StaticContext(render.Overlay, render.Source, render.OverlayMask, render.SourceMask, plane)
                : new StaticContext(render.Source, render.Overlay, render.SourceMask, render.OverlayMask, plane);

            Source = Static.Source.Dynamic();
            Overlay = Static.Overlay.Dynamic();

            SourceInfo = Static.Source.GetVideoInfo();
            OverlayInfo = Static.Overlay.GetVideoInfo();

            TargetInfo = render.GetVideoInfo();

            SourceMask = PrepareMask(Static.SourceMask, Static.SourceBase.GetVideoInfo());
            OverlayMask = PrepareMask(Static.OverlayMask, Static.OverlayBase.GetVideoInfo());

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
                    SourceCache = new HistogramCache(planes, channels, render.SIMD, true, 
                        SourceInfo.ColorSpace, OverlayInfo.ColorSpace, 
                        SourceInfo.ColorSpace, render.ColorFramesCount, true, new ParallelOptions());
                    OverlayCache = new HistogramCache(planes, channels, render.SIMD, true, 
                        OverlayInfo.ColorSpace, SourceInfo.ColorSpace, 
                        OverlayInfo.ColorSpace, render.ColorFramesCount, true, new ParallelOptions());
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

        public FrameParams CalcFrame(OverlayInfo info)
        {
            var frame = new FrameParams();
            var srcSize = SourceInfo.Size;
            frame.MergedWidth = srcSize.Width + Math.Max(-info.X, 0) + Math.Max(info.Width + info.X - srcSize.Width, 0);
            frame.MergedHeight = srcSize.Height + Math.Max(-info.Y, 0) + Math.Max(info.Height + info.Y - srcSize.Height, 0);
            frame.MergedAr = frame.MergedWidth / (double) frame.MergedHeight;
            frame.OutAr = TargetInfo.Width / (double) TargetInfo.Height;
            frame.Wider = frame.MergedAr > frame.OutAr;
            if (Mode == FramingMode.FitBorders)
                frame.Wider = !frame.Wider;
            frame.FinalWidth = frame.Wider ? TargetInfo.Width : (int) Math.Round(TargetInfo.Width * (frame.MergedAr / frame.OutAr));
            frame.FinalHeight = frame.Wider ? (int) Math.Round(TargetInfo.Height * (frame.OutAr / frame.MergedAr)) : TargetInfo.Height;
            frame.FinalX = frame.Wider ? 0 : (TargetInfo.Width - frame.FinalWidth) / 2;
            frame.FinalY = frame.Wider ? (TargetInfo.Height - frame.FinalHeight) / 2 : 0;
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
            if (SourceCache != null)
                HistogramCache.Dispose(SourceCache.Id);
            if (OverlayCache != null)
                HistogramCache.Dispose(OverlayCache.Id);
        }

        public class StaticContext
        {
            public Clip SourceBase { get; }
            public Clip OverlayBase { get; }
            public Clip Source { get; }
            public Clip Overlay { get; }
            public Clip SourceMask { get; }
            public Clip OverlayMask { get; }

            public StaticContext(Clip source, Clip overlay, Clip sourceMask, Clip overlayMask, YUVPlanes plane)
            {
                SourceBase = source;
                OverlayBase = overlay;
                Source = ExtractPlane(SourceBase, plane);
                Overlay = ExtractPlane(OverlayBase, plane);
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
