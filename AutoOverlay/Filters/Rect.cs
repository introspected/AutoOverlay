using System;
using System.IO;
using AutoOverlay;
using AvsFilterNet;

[assembly: AvisynthFilterClass(typeof(Rect), nameof(Rect), "iiii[Debug]b", MtMode.NICE_FILTER)]
namespace AutoOverlay
{
    [Serializable]
    public class Rect : OverlayFilter
    {
        [AvsArgument]
        public int Left { get; protected set; }

        [AvsArgument]
        public int Top { get; protected set; }

        [AvsArgument]
        public int Right { get; protected set; }

        [AvsArgument]
        public int Bottom { get; protected set; }

        [AvsArgument]
        public override bool Debug { get; protected set; }

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

        public static Rect FromFrame(VideoFrame frame)
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
                return new Rect
                {
                    Left = reader.ReadInt32(),
                    Top = reader.ReadInt32(),
                    Right = reader.ReadInt32(),
                    Bottom = reader.ReadInt32()
                };
            }
        }

        protected bool Equals(Rect other)
        {
            return Left == other.Left && Top == other.Top && Right == other.Right && Bottom == other.Bottom;
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
                var hashCode = Left;
                hashCode = (hashCode * 397) ^ Top;
                hashCode = (hashCode * 397) ^ Right;
                hashCode = (hashCode * 397) ^ Bottom;
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
