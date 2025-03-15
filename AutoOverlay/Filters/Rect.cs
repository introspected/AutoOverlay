using System;
using System.Drawing;
using System.IO;
using System.Linq;
using AutoOverlay;
using AutoOverlay.AviSynth;
using AutoOverlay.Overlay;
using AvsFilterNet;

[assembly: AvisynthFilterClass(typeof(Rect), nameof(Rect), "f[Top]f[Right]f[Bottom]f[Debug]b", OverlayConst.DEFAULT_MT_MODE)]
namespace AutoOverlay
{
    [Serializable]
    public class Rect : OverlayFilter
    {
        [AvsArgument]
        public double Left { get; protected set; }

        [AvsArgument]
        public double Top { get; protected set; }

        [AvsArgument]
        public double Right { get; protected set; }

        [AvsArgument]
        public double Bottom { get; protected set; }

        [AvsArgument]
        public override bool Debug { get; protected set; }

        protected override void Initialize(AVSValue args)
        {
            base.Initialize(args);
            Top = args[1].AsFloat(Left);
            Right = args[2].AsFloat(Left);
            Bottom = args[3].AsFloat(Top);
        }

        protected override VideoFrame GetFrame(int n)
        {
            var frame = base.GetFrame(n);
            StaticEnv.MakeWritable(frame);
            unsafe
            {
                using var stream = new UnmanagedMemoryStream(
                    (byte*) frame.GetWritePtr().ToPointer(),
                    frame.GetRowSize(), frame.GetRowSize(), FileAccess.Write);
                using var writer = new BinaryWriter(stream);
                writer.Write(nameof(Rect));
                writer.Write(Left);
                writer.Write(Top);
                writer.Write(Right);
                writer.Write(Bottom);
            }
            return frame;
        }

        public static RectangleD FromFrame(VideoFrame frame, bool ltrb)
        {
            unsafe
            {
                using var stream = new UnmanagedMemoryStream(
                    (byte*) frame.GetReadPtr().ToPointer(),
                    frame.GetRowSize(), frame.GetRowSize(), FileAccess.Read);
                using var reader = new BinaryReader(stream);
                var header = reader.ReadString();
                if (header != nameof(Rect))
                    throw new AvisynthException();
                return ltrb
                    ? RectangleD.FromLTRB(
                        reader.ReadDouble(),
                        reader.ReadDouble(),
                        reader.ReadDouble(),
                        reader.ReadDouble())
                    : new RectangleD(
                        reader.ReadDouble(),
                        reader.ReadDouble(),
                        reader.ReadDouble(),
                        reader.ReadDouble());
            }
        }

        protected bool Equals(Rect other)
        {
            return Math.Abs(Left - other.Left) < OverlayConst.EPSILON &&
                   Math.Abs(Top - other.Top) < OverlayConst.EPSILON &&
                   Math.Abs(Right - other.Right) < OverlayConst.EPSILON &&
                   Math.Abs(Bottom - other.Bottom) < OverlayConst.EPSILON;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Rect) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Left.GetHashCode();
                hashCode = (hashCode * 397) ^ Top.GetHashCode();
                hashCode = (hashCode * 397) ^ Right.GetHashCode();
                hashCode = (hashCode * 397) ^ Bottom.GetHashCode();
                return hashCode;
            }
        }


        public override string ToString()
        {
            return $"Rectangle ID: {GetHashCode()}:\n" +
                   $"LTRB: ({Left},{Top},{Right},{Bottom})";
        }
    }
}
