using AvsFilterNet;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AutoOverlay
{
    public record OverlayEngineFrame(
        List<OverlayInfo> Sequence,
        List<OverlayInfo> KeyFrames)
    {
        public static OverlayEngineFrame FromFrame(VideoFrame frame)
        {
            unsafe
            {
                using var stream = new UnmanagedMemoryStream(
                    (byte*)frame.GetReadPtr().ToPointer(),
                    frame.GetRowSize() * frame.GetHeight(),
                    frame.GetRowSize() * frame.GetHeight(),
                    FileAccess.Read);
                using var reader = new BinaryReader(stream);
                var list = new List<OverlayInfo>(OverlayConst.ENGINE_HISTORY_LENGTH);
                var caption = reader.ReadString();
                if (caption != nameof(OverlayEngine))
                    throw new AvisynthException();
                reader.ReadInt32();
                var count = reader.ReadInt32();
                for (var i = 0; i < count; i++)
                {
                    var info = OverlayInfo.Read(reader);
                    if (info == null)
                        break;
                    list.Add(info);
                }
                var keyFramesCount = reader.ReadInt32();
                var keyFrames = new List<OverlayInfo>(keyFramesCount);
                for (var i = 0; i < keyFramesCount; i++)
                    keyFrames.Add(OverlayInfo.Read(reader));
                return new OverlayEngineFrame(list, keyFrames);
            }
        }

        public void ToFrame(VideoFrame frame)
        {
            unsafe
            {
                using var stream = new UnmanagedMemoryStream((byte*)frame.GetWritePtr().ToPointer(),
                    frame.GetRowSize() * frame.GetHeight(), frame.GetRowSize() * frame.GetHeight(), FileAccess.Write);
                using var writer = new BinaryWriter(stream);
                writer.Write(nameof(OverlayEngine));
                writer.Write(GetHashCode());
                writer.Write(Sequence.Count);
                var first = Sequence.First();
                first.Write(writer, first.Message);
                foreach (var overlayInfo in Sequence.Skip(1))
                    overlayInfo.Write(writer);
                writer.Write(KeyFrames.Count);
                foreach (var keyFrame in KeyFrames)
                    keyFrame.Write(writer);
            }
        }
    }
}
