using System.Collections.Generic;
using System.Drawing;

namespace AutoOverlay.Overlay
{
    public record OverlayConfigInstance
    {
        public int Subpixel { get; set; }
        public double MinOverlayArea { get; set; }
        public double MinSourceArea { get; set; }
        public double AspectRatio1 { get; set; }
        public double AspectRatio2 { get; set; }
        public bool FixedAspectRatio { get; set; }
        public int MinSampleArea { get; set; }
        public int RequiredSampleArea { get; set; }
        public double Angle1 { get; set; }
        public double Angle2 { get; set; }
        public double MinAngleStep { get; set; }
        public double MaxAngleStep { get; set; }
        public double AngleStepCount { get; set; }
        public List<Rectangle> WarpPoints { get; set; }
        public int WarpSteps { get; set; }
        public int WarpOffset { get; set; }
        public double MaxSampleDiff { get; set; }
        public double ScaleBase { get; set; }
        public int Branches { get; set; }
        public double BranchMaxDiff { get; set; }
        public double AcceptableDiff { get; set; }
        public double Correction { get; set; }
        public double RotationCorrection { get; set; }
        public int MinX { get; set; }
        public int MaxX { get; set; }
        public int MinY { get; set; }
        public int MaxY { get; set; }
        public int MinArea { get; set; }
        public int MaxArea { get; set; }
    }
}
