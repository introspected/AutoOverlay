using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using AutoOverlay;
using AutoOverlay.AviSynth;
using AvsFilterNet;

[assembly: AvisynthFilterClass(typeof(OverlayConfig),
    nameof(OverlayConfig),
    "[MinOverlayArea]f[MinSourceArea]f[AspectRatio1]f[AspectRatio2]f[Angle1]f[Angle2]f" +
    "[WarpPoints]c[WarpSteps]i[WarpOffset]i[MinSampleArea]i[RequiredSampleArea]i[MaxSampleDiff]f" +
    "[Subpixel]i[ScaleBase]f[Branches]i[BranchMaxDiff]f[AcceptableDiff]f[Correction]i" +
    "[MinX]i[MaxX]i[MinY]i[MaxY]i[MinArea]i[MaxArea]i[FixedAspectRatio]b[Debug]b",
    MtMode.SERIALIZED)]
namespace AutoOverlay
{
    [Serializable]
    public class OverlayConfig : OverlayFilter
    {
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

        [AvsArgument(Max = 16)]
        public List<Rectangle> WarpPoints { get; set; } = new List<Rectangle>();

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
        public int Subpixel { get; set; }

        [AvsArgument(Min = 1.1, Max = 5)]
        public double ScaleBase { get; set; } = 1.5;

        [AvsArgument(Min = 1, Max = 100)]
        public int Branches { get; set; } = 1;

        [AvsArgument(Min = 0, Max = 100)]
        public double BranchMaxDiff { get; set; } = 0.2;

        [AvsArgument(Min = 0)]
        public double AcceptableDiff { get; set; } = 5;

        [AvsArgument(Min = 0, Max = 100)]
        public int Correction { get; set; } = 1;

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
        
        protected override VideoFrame GetFrame(int n)
        {
            var frame = base.GetFrame(n);
            StaticEnv.MakeWritable(frame);
            unsafe
            {
                using (var stream = new UnmanagedMemoryStream((byte*)frame.GetWritePtr().ToPointer(),
                    frame.GetRowSize(), frame.GetRowSize(), FileAccess.Write))
                using (var writer = new BinaryWriter(stream))
                {
                    writer.Write(nameof(OverlayConfig));
                    writer.Write(MinOverlayArea);
                    writer.Write(MinSourceArea);
                    writer.Write(AspectRatio1);
                    writer.Write(AspectRatio2);
                    writer.Write(Angle1);
                    writer.Write(Angle2);
                    writer.Write(MinSampleArea);
                    writer.Write(RequiredSampleArea);
                    writer.Write(MaxSampleDiff);
                    writer.Write(Subpixel);
                    writer.Write(ScaleBase);
                    writer.Write(Branches);
                    writer.Write(AcceptableDiff);
                    writer.Write(Correction);
                    writer.Write(MinX);
                    writer.Write(MaxX);
                    writer.Write(MinY);
                    writer.Write(MaxY);
                    writer.Write(MinArea);
                    writer.Write(MaxArea);
                    writer.Write(FixedAspectRatio);
                    writer.Write(BranchMaxDiff);
                    writer.Write(WarpSteps);
                    writer.Write(WarpOffset);
                    writer.Write(WarpPoints.Count);
                    foreach (var point in WarpPoints)
                    {
                        writer.Write(point.X);
                        writer.Write(point.Y);
                        writer.Write(point.Right);
                        writer.Write(point.Bottom);
                    }
                }
            }
            return frame;
        }

        public static OverlayConfig FromFrame(VideoFrame frame)
        {
            unsafe
            {
                using (var stream = new UnmanagedMemoryStream((byte*)frame.GetReadPtr().ToPointer(),
                    frame.GetRowSize(), frame.GetRowSize(), FileAccess.Read))
                using (var reader = new BinaryReader(stream))
                {
                    var header = reader.ReadString();
                    if (header != nameof(OverlayConfig))
                        throw new AvisynthException();
                    return new OverlayConfig
                    {
                        MinOverlayArea = reader.ReadDouble(),
                        MinSourceArea = reader.ReadDouble(),
                        AspectRatio1 = reader.ReadDouble(),
                        AspectRatio2 = reader.ReadDouble(),
                        Angle1 = reader.ReadDouble(),
                        Angle2 = reader.ReadDouble(),
                        MinSampleArea = reader.ReadInt32(),
                        RequiredSampleArea = reader.ReadInt32(),
                        MaxSampleDiff = reader.ReadDouble(),
                        Subpixel = reader.ReadInt32(),
                        ScaleBase = reader.ReadDouble(),
                        Branches = reader.ReadInt32(),
                        AcceptableDiff = reader.ReadDouble(),
                        Correction = reader.ReadInt32(),
                        MinX = reader.ReadInt32(),
                        MaxX = reader.ReadInt32(),
                        MinY = reader.ReadInt32(),
                        MaxY = reader.ReadInt32(),
                        MinArea = reader.ReadInt32(),
                        MaxArea = reader.ReadInt32(),
                        FixedAspectRatio = reader.ReadBoolean(),
                        BranchMaxDiff = reader.ReadDouble(),
                        WarpSteps = reader.ReadInt32(),
                        WarpOffset = reader.ReadInt32(),
                        WarpPoints = new List<Rectangle>(Enumerable.Range(0, reader.ReadInt32())
                            .Select(i => new Rectangle(
                                reader.ReadInt32(),
                                reader.ReadInt32(),
                                reader.ReadInt32(),
                                reader.ReadInt32())))
                    };
                }
            }
        }

        public override string ToString()
        {
            return $"Overlay Configuration ID: {GetHashCode()}:\n" +
                   $"MinOverlayArea={MinOverlayArea:F2}, MinSourceArea={MinSourceArea:F2},\n" +
                   $"AspectRatio1={AspectRatio1:F2}, AspectRatio2={AspectRatio2:F2},\n" +
                   $"Angle1={Angle1:F2}, Angle2={Angle2:F2},\n" +
                   $"MinSampleArea={MinSampleArea}, RequiredSampleArea={RequiredSampleArea},\n" +
                   $"MaxSampleDifference={MaxSampleDiff}, Subpixel={Subpixel},\n" +
                   $"ScaleBase={ScaleBase:F1}, PointCount={Branches},\n" +
                   $"AcceptableDiff={AcceptableDiff:F2}, Correction={Correction}";
        }

        protected bool Equals(OverlayConfig other)
        {
            return MinOverlayArea.Equals(other.MinOverlayArea) && MinSourceArea.Equals(other.MinSourceArea) 
                && AspectRatio1.Equals(other.AspectRatio1) && AspectRatio2.Equals(other.AspectRatio2) 
                && Angle1.Equals(other.Angle1) && Angle2.Equals(other.Angle2) && MinSampleArea == other.MinSampleArea 
                && RequiredSampleArea == other.RequiredSampleArea && MaxSampleDiff.Equals(other.MaxSampleDiff) 
                && Subpixel == other.Subpixel && ScaleBase.Equals(other.ScaleBase) && Branches == other.Branches 
                && AcceptableDiff.Equals(other.AcceptableDiff) && Correction == other.Correction;
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
                hashCode = (hashCode * 397) ^ Correction;
                return hashCode;
            }
        }
    }
}
