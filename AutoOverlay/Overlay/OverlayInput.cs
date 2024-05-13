using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace AutoOverlay.Overlay
{
    public record OverlayInput
    {
        public Size SourceSize { get; set; }
        public Size OverlaySize { get; set; }
        public Size TargetSize { get; set; }

        public List<ExtraClip> ExtraClips { get; set; } = new();

        public RectangleD InnerBounds { get; set; } // 0-1
        public RectangleD OuterBounds { get; set; } // 0-1

        public Space OverlayBalance { get; set; } // -1-0-1 (-1 - source, 1 - overlay, 0 - median)

        public bool FixedSource { get; set; }

        public OverlayInput Scale(Size mult)
        {
            return this with
            {
                TargetSize = new Size(TargetSize.Width * mult.Width, TargetSize.Height * mult.Height)
            };
        }
    }
}
