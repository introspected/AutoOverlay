using System;
using System.IO;
using AutoOverlay;
using AvsFilterNet;

[assembly: AvisynthFilterClass(typeof(OverlayConfig),
    nameof(OverlayConfig),
    "[MinOverlayArea]f[MinSourceArea]f[AspectRatio1]f[AspectRatio2]f[Angle1]f[Angle2]f" +
    "[MinSampleArea]i[RequiredSampleArea]i[MaxSampleDifference]f[Subpixel]i[ScaleBase]f[Branches]i[AcceptableDiff]f[Correction]i" +
    "[minX]i[maxX]i[minY]i[maxY]i[minArea]i[maxArea]i[fixedAspectRatio]b",
    MtMode.SERIALIZED)]
namespace AutoOverlay
{
    [Serializable]
    public class OverlayConfig : OverlayFilter
    {
        public double MinOverlayArea { get; set; } = 0;
        public double MinSourceArea { get; set; } = 0;

        public double AspectRatio1 { get; set; } = 0;
        public double AspectRatio2 { get; set; } = 0;

        public double Angle1 { get; set; } = 0;
        public double Angle2 { get; set; } = 0;

        public int MinSampleArea { get; set; } = 1000;
        public int RequiredSampleArea { get; set; } = 1000;
        public double MaxSampleDifference { get; set; } = 5;

        public int Subpixel { get; set; } = 0;
        public double ScaleBase { get; set; } = 1.5;
        public int Branches { get; set; } = 1;
        public double BranchCorrelation { get; set; } = 0.5;

        public double AcceptableDiff { get; set; } = 5;
        public int Correction { get; set; } = 1;

        public int MinX { get; set; } = short.MinValue;
        public int MaxX { get; set; } = short.MaxValue;
        public int MinY { get; set; } = short.MinValue;
        public int MaxY { get; set; } = short.MaxValue;
        public int MinArea { get; set; } = 1;
        public int MaxArea { get; set; } = short.MaxValue * short.MaxValue;

        public bool FixedAspectRatio { get; set; }

        protected override void Initialize(AVSValue args)
        {
            base.Initialize(args);
            MinOverlayArea = args[0].AsFloat(MinOverlayArea);
            MinSourceArea = args[1].AsFloat(MinSourceArea);
            AspectRatio1 = args[2].AsFloat(AspectRatio1);
            AspectRatio2 = args[3].AsFloat(AspectRatio2);
            Angle1 = args[4].AsFloat(Angle1);
            Angle2 = args[5].AsFloat(Angle2);
            MinSampleArea = args[6].AsInt(MinSampleArea);
            RequiredSampleArea = args[7].AsInt(RequiredSampleArea);
            MaxSampleDifference = args[8].AsFloat(MaxSampleDifference);
            Subpixel = args[9].AsInt(Subpixel);
            ScaleBase = args[10].AsFloat(ScaleBase);
            Branches = args[11].AsInt(Branches);
            AcceptableDiff = args[12].AsFloat(AcceptableDiff);
            Correction = args[13].AsInt(Correction);
            MinX = args[14].AsInt(MinX);
            MaxX = args[15].AsInt(MaxX);
            MinY = args[16].AsInt(MinY);
            MaxY = args[17].AsInt(MaxY);
            MinArea = args[18].AsInt(MinArea);
            MaxArea = args[19].AsInt(MaxArea);
            FixedAspectRatio = args[20].AsBool(FixedAspectRatio);
            if (Math.Abs(AspectRatio1 - AspectRatio2) >= double.Epsilon && FixedAspectRatio)
                throw new AvisynthException("Aspect ratio must be fixed");
            if (Math.Abs(AspectRatio1 - AspectRatio2) < double.Epsilon && !args[20].Defined())
                FixedAspectRatio = true;
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
                    writer.Write(MaxSampleDifference);
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
                        MaxSampleDifference = reader.ReadDouble(),
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
                        FixedAspectRatio = reader.ReadBoolean()
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
                   $"MaxSampleDifference={MaxSampleDifference}, Subpixel={Subpixel},\n" +
                   $"ScaleBase={ScaleBase:F1}, PointCount={Branches},\n" +
                   $"AcceptableDiff={AcceptableDiff:F2}, Correction={Correction}";
        }

        protected bool Equals(OverlayConfig other)
        {
            return MinOverlayArea.Equals(other.MinOverlayArea) && MinSourceArea.Equals(other.MinSourceArea) 
                && AspectRatio1.Equals(other.AspectRatio1) && AspectRatio2.Equals(other.AspectRatio2) 
                && Angle1.Equals(other.Angle1) && Angle2.Equals(other.Angle2) && MinSampleArea == other.MinSampleArea 
                && RequiredSampleArea == other.RequiredSampleArea && MaxSampleDifference.Equals(other.MaxSampleDifference) 
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
                hashCode = (hashCode * 397) ^ MaxSampleDifference.GetHashCode();
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
