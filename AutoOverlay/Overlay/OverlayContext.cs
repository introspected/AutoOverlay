using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
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

        public List<ExtraClip> ExtraClips { get; }

        public YUVPlanes Plane { get; }
        public Size SubSample { get; }
        public dynamic BackgroundClip { get; }

        public bool AdjustColor { get; }
        public string AdjustChannels { get; }

        public int DefaultColor { get; }
        public int BlankColor { get; } = -1;

        public OverlayInput Input { get; }

        public List<string> Cache { get; }

        public OverlayContext(Clip source, Clip overlay, OverlayRender render, YUVPlanes plane)
        {
            Render = render;
            Plane = plane;
            AdjustColor = render.ColorAdjust > -double.Epsilon &&
                          (string.IsNullOrEmpty(render.AdjustChannels) || 
                           (render.AdjustChannels.ToUpper() + default(YUVPlanes).GetLetter()).Contains(plane.GetLetter()));

            SubSample = plane.IsChroma() ? render.ColorSpace.GetSubSample() : new Size(1, 1);

            //if (plane.IsChroma())
            //{
            //    srcCrop = srcCrop.Scale(Space.One.Divide(source.GetVideoInfo().pixel_type.GetSubSample()));
            //    overCrop = overCrop.Scale(Space.One.Divide(overlay.GetVideoInfo().pixel_type.GetSubSample()));
            //}

            Static = render.Invert
                ? new StaticContext(overlay, source, render.OverlayMask, render.SourceMask, render.BackgroundClip, plane)
                : new StaticContext(source, overlay, render.SourceMask, render.OverlayMask, render.BackgroundClip, plane);

            Source = Static.Source.Dynamic();
            Overlay = Static.Overlay.Dynamic();
            
            ExtraClips = Render.ExtraClips.Select(p => new ExtraClip
            {
                Clip = p.Clip.ExtractPlane(plane),
                Mask = PrepareMask(p.Mask, p.Clip.GetVideoInfo()),
                Info = p.Clip.ExtractPlane(plane).GetVideoInfo(),
                Opacity = p.Opacity,
                Minor = p.Minor
            }).ToList();

            SourceInfo = Static.Source.GetVideoInfo();
            OverlayInfo = Static.Overlay.GetVideoInfo();
            BackgroundClip = Static.BackgroundClip?.Dynamic();

            TargetInfo = render.GetVideoInfo();

            SourceMask = PrepareMask(Static.SourceMask, Static.SourceBase.GetVideoInfo());
            OverlayMask = PrepareMask(Static.OverlayMask, Static.OverlayBase.GetVideoInfo());

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
                AdjustChannels = render.AdjustChannels;

                if (render.ColorFramesCount > 0)
                {
                    Cache = Enumerable.Range(0, 2 + ExtraClips.Count)
                        .Select(_ => Guid.NewGuid().ToString())
                        .ToList();
                }
            }

            Input = new OverlayInput
            {
                SourceSize = SourceInfo.Size,
                OverlaySize = OverlayInfo.Size,
                TargetSize = TargetInfo.Size,
                InnerBounds = Render.InnerBounds,
                OuterBounds = Render.OuterBounds,
                OverlayBalance = Render.OverlayBalance,
                FixedSource = Render.FixedSource,
                ExtraClips = ExtraClips
            };

            dynamic PrepareMask(Clip mask, ExtraVideoInfo target)
            {
                if (mask != null)
                {
                    if (!target.Info.IsRGB() && mask.IsRealPlanar())
                        mask = mask.Dynamic().ExtractY();
                    if (plane.IsChroma() && !target.ColorSpace.WithoutSubSample())
                        mask = mask.Dynamic().BicubicResize(target.Chroma.Size);
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
            if (Cache == null) return;
            Cache.ForEach(ColorHistogramCache.Dispose);
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
    }
}
