using System;
using System.Collections.Generic;
using System.Drawing;
using AutoOverlay;
using AutoOverlay.AviSynth;
using AutoOverlay.Overlay;
using AvsFilterNet;

[assembly: AvisynthFilterClass(typeof(OverlayConfig),
    nameof(OverlayConfig),
    "[Preset]s[MinOverlayArea]f[MinSourceArea]f[AspectRatio1]f[AspectRatio2]f[Angle1]f[Angle2]f" +
    "[MinAngleStep]f[MaxAngleStep]f[AngleStepCount]f" +
    "[WarpPoints]c[WarpSteps]i[WarpOffset]i[MinSampleArea]i[RequiredSampleArea]i[MaxSampleDiff]f" +
    "[Subpixel]i[ScaleBase]f[Branches]i[BranchMaxDiff]f[AcceptableDiff]f[Correction]f[CorrectionRotation]f" +
    "[MinX]i[MaxX]i[MinY]i[MaxY]i[MinArea]i[MaxArea]i[FixedAspectRatio]b[Debug]b",
    MtMode.SERIALIZED)]
namespace AutoOverlay
{
    [Serializable]
    public class OverlayConfig : SupportFilter
    {
        #region Presets
        static OverlayConfig()
        {
            Presets.Add<OverlayConfigPreset, OverlayConfig>(new()
            {
                [OverlayConfigPreset.Low] = new()
                {
                    [nameof(Subpixel)] = _ => 0,
                    [nameof(Branches)] = _ => 1,
                    [nameof(MinSampleArea)] = _ => 1000,
                    [nameof(RequiredSampleArea)] = _ => 1000,
                },
                [OverlayConfigPreset.Medium] = new()
                {
                    // defaults
                },
                [OverlayConfigPreset.High] = new()
                {
                    [nameof(Subpixel)] = _ => 2,
                    [nameof(Branches)] = _ => 3,
                    [nameof(RequiredSampleArea)] = _ => 4000,
                    [nameof(RotationCorrection)] = _ => 1,
                },
                [OverlayConfigPreset.Extreme] = new()
                {
                    [nameof(Subpixel)] = _ => 3,
                    [nameof(Branches)] = _ => 4,
                    [nameof(MinSampleArea)] = _ => 2000,
                    [nameof(RequiredSampleArea)] = _ => 5000,
                    [nameof(RotationCorrection)] = _ => 1,
                },
                [OverlayConfigPreset.Fixed] = new()
                {
                    [nameof(Subpixel)] = _ => 0,
                    [nameof(Branches)] = _ => 1,
                    [nameof(FixedAspectRatio)] = _ => true,
                    [nameof(MinOverlayArea)] = _ => 100,
                },
            });
        }
        #endregion

        [AvsArgument]
        public OverlayConfigPreset Preset { get; private set; } = OverlayConfigPreset.Medium;

        [AvsArgument(Min = 0, Max = 100)]
        public double MinOverlayArea { get; set; }

        [AvsArgument(Min = 0, Max = 100)]
        public double MinSourceArea { get; set; }

        [AvsArgument(Min = 0)]
        public double AspectRatio1 { get; set; }

        [AvsArgument(Min = 0)]
        public double AspectRatio2 { get; set; }

        [AvsArgument(Min = -360, Max = 360)]
        public double Angle1 { get; set; }

        [AvsArgument(Min = -360, Max = 360)]
        public double Angle2 { get; set; }

        [AvsArgument(Min = 0)]
        public double MinAngleStep { get; set; } = 0.05;

        [AvsArgument(Min = 0.01)]
        public double MaxAngleStep { get; set; } = 1;

        [AvsArgument(Min = 2, Max = 10)]
        public double AngleStepCount { get; set; } = 2;

        [AvsArgument(Max = 16, LTRB = false)]
        public List<Rectangle> WarpPoints { get; set; } = new();

        [AvsArgument(Min = 1, Max = 10)]
        public int WarpSteps { get; set; } = 3;

        [AvsArgument(Min = 0, Max = 5)]
        public int WarpOffset { get; set; } = 0;

        [AvsArgument(Min = 1)]
        public int MinSampleArea { get; set; } = 1500;

        [AvsArgument(Min = 1)]
        public int RequiredSampleArea { get; set; } = 3000;

        [AvsArgument(Min = 0)]
        public double MaxSampleDiff { get; set; } = 5;

        [AvsArgument(Min = -5, Max = 5)]
        public int Subpixel { get; set; } = 1;

        [AvsArgument(Min = 1.1, Max = 5)]
        public double ScaleBase { get; set; } = 1.5;

        [AvsArgument(Min = 1, Max = 100)]
        public int Branches { get; set; } = 2;

        [AvsArgument(Min = 0, Max = 100)]
        public double BranchMaxDiff { get; set; } = 0.2;

        [AvsArgument(Min = 0)]
        public double AcceptableDiff { get; set; } = 5;

        [AvsArgument(Min = 0, Max = 100)]
        public double Correction { get; set; } = 1;

        [AvsArgument(Min = 0, Max = 100)]
        public double RotationCorrection { get; set; } = 0.5;

        [AvsArgument]
        public int MinX { get; set; } = short.MinValue;

        [AvsArgument]
        public int MaxX { get; set; } = short.MaxValue;

        [AvsArgument]
        public int MinY { get; set; } = short.MinValue;

        [AvsArgument]
        public int MaxY { get; set; } = short.MaxValue;

        [AvsArgument(Min = 1)]
        public int MinArea { get; set; } = 1;

        [AvsArgument(Min = 1)]
        public int MaxArea { get; set; } = short.MaxValue * short.MaxValue;

        [AvsArgument]
        public bool FixedAspectRatio { get; set; }

        [AvsArgument]
        public override bool Debug { get; protected set; }

        protected override void Initialize(AVSValue args)
        {
            base.Initialize(args);
            if (Math.Abs(AspectRatio1 - AspectRatio2) >= double.Epsilon && FixedAspectRatio)
                throw new AvisynthException("Aspect ratio must be fixed");
        }

        public override string ToString()
        {
            return $"Overlay Configuration ID: {GetHashCode()}:\n" +
                   $"MinOverlayArea={MinOverlayArea:F2}, MinSourceArea={MinSourceArea:F2},\n" +
                   $"AspectRatio1={AspectRatio1:F2}, AspectRatio2={AspectRatio2:F2},\n" +
                   $"Angle1={Angle1:F2}, Angle2={Angle2:F2},\n" +
                   $"MinAngleStep={MinAngleStep:F2}, MaxAngleStep={MaxAngleStep:F2},\n" +
                   $"AngleStepCount={AngleStepCount:F2},\n" +
                   $"MinSampleArea={MinSampleArea}, RequiredSampleArea={RequiredSampleArea},\n" +
                   $"MaxSampleDifference={MaxSampleDiff}, Subpixel={Subpixel},\n" +
                   $"ScaleBase={ScaleBase:F1}, PointCount={Branches},\n" +
                   $"AcceptableDiff={AcceptableDiff:F2},\n" +
                   $"Correction={Correction:F2}, RotationCorrection={RotationCorrection:F2}";
        }

        protected bool Equals(OverlayConfig other)
        {
            return MinOverlayArea.Equals(other.MinOverlayArea) && MinSourceArea.Equals(other.MinSourceArea) 
                && AspectRatio1.Equals(other.AspectRatio1) && AspectRatio2.Equals(other.AspectRatio2) 
                && Angle1.Equals(other.Angle1) && Angle2.Equals(other.Angle2) && MinSampleArea == other.MinSampleArea 
                && RequiredSampleArea == other.RequiredSampleArea && MaxSampleDiff.Equals(other.MaxSampleDiff) 
                && Subpixel == other.Subpixel && ScaleBase.Equals(other.ScaleBase) && Branches == other.Branches 
                && AcceptableDiff.Equals(other.AcceptableDiff) && Correction.Equals(other.Correction) 
                && RotationCorrection.Equals(other.RotationCorrection) && MinAngleStep.Equals(other.MinAngleStep) 
                && MaxAngleStep.Equals(other.MaxAngleStep) && AngleStepCount.Equals(other.AngleStepCount);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((OverlayConfig) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = MinOverlayArea.GetHashCode();
                hashCode = (hashCode * 397) ^ MinSourceArea.GetHashCode();
                hashCode = (hashCode * 397) ^ AspectRatio1.GetHashCode();
                hashCode = (hashCode * 397) ^ AspectRatio2.GetHashCode();
                hashCode = (hashCode * 397) ^ Angle1.GetHashCode();
                hashCode = (hashCode * 397) ^ Angle2.GetHashCode();
                hashCode = (hashCode * 397) ^ MinSampleArea;
                hashCode = (hashCode * 397) ^ RequiredSampleArea;
                hashCode = (hashCode * 397) ^ MaxSampleDiff.GetHashCode();
                hashCode = (hashCode * 397) ^ Subpixel.GetHashCode();
                hashCode = (hashCode * 397) ^ ScaleBase.GetHashCode();
                hashCode = (hashCode * 397) ^ Branches;
                hashCode = (hashCode * 397) ^ AcceptableDiff.GetHashCode();
                hashCode = (hashCode * 397) ^ Correction.GetHashCode();
                hashCode = (hashCode * 397) ^ RotationCorrection.GetHashCode();
                hashCode = (hashCode * 397) ^ AngleStepCount.GetHashCode();
                hashCode = (hashCode * 397) ^ MinAngleStep.GetHashCode();
                hashCode = (hashCode * 397) ^ MaxAngleStep.GetHashCode();
                return hashCode;
            }
        }

        public OverlayConfigInstance GetInstance() => new()
        {
            MinOverlayArea = MinOverlayArea,
            MinSourceArea = MinSourceArea,
            AspectRatio1 = AspectRatio1,
            AspectRatio2 = AspectRatio2,
            Angle1 = Angle1,
            Angle2 = Angle2,
            MinAngleStep = MinAngleStep,
            MaxAngleStep = MaxAngleStep,
            AngleStepCount = AngleStepCount,
            MinSampleArea = MinSampleArea,
            RequiredSampleArea = RequiredSampleArea,
            MaxSampleDiff = MaxSampleDiff,
            Subpixel = Subpixel,
            ScaleBase = ScaleBase,
            Branches = Branches,
            AcceptableDiff = AcceptableDiff,
            Correction = Correction,
            RotationCorrection = RotationCorrection,
            BranchMaxDiff = BranchMaxDiff,
            FixedAspectRatio = FixedAspectRatio,
            MaxArea = MaxArea,
            MaxX = MaxX,
            MaxY = MaxY,
            MinArea = MinArea,
            MinX = MinX,
            MinY = MinY,
            WarpOffset = WarpOffset,
            WarpPoints = WarpPoints,
            WarpSteps = WarpSteps,
        };
    }
}
