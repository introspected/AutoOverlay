using System;
using System.Collections.Generic;
using System.IO;

namespace AutoOverlay
{
    public class FileOverlayStat : IOverlayStat
    {
        private const int RECORD_LENGHT = 4 + 4 + 2 + 2 + 2 + 2 + 2 + 2 + 2 + 2 + 2; //26

        private readonly Stream stream;

        public FileOverlayStat(string statFile)
        {
            if (string.IsNullOrEmpty(statFile))
                stream = new BufferedStream(new MemoryStream());
            else stream = new BufferedStream(new FileStream(statFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite));
        }

        public IEnumerable<OverlayInfo> Frames
        {
            get
            {
                lock (stream)
                {
                    stream.Position = 0;
                    if (stream.Length == 0)
                        yield break;
                    if (stream.ReadByte() != 1)
                        throw new ArgumentException();
                    for (var position = 1; position < stream.Length - 1; position += RECORD_LENGHT)
                    {
                        stream.Position = position;
                        var info = ReadFrame(stream);
                        if (info != null)
                            yield return info;
                    }
                }
            }
        }

        private static OverlayInfo ReadFrame(Stream stream)
        {
            var reader = new BinaryReader(stream);
            var num = reader.ReadInt32() - 1;
            if (num < 0) return null;
            return new OverlayInfo
            {
                FrameNumber = num,
                Diff = reader.ReadInt32() / 1000.0,
                X = reader.ReadInt16(),
                Y = reader.ReadInt16(),
                Width = reader.ReadInt16(),
                Height = reader.ReadInt16(),
                CropLeft = reader.ReadInt16(),
                CropTop = reader.ReadInt16(),
                CropRight = reader.ReadInt16(),
                CropBottom = reader.ReadInt16(),
                Angle = reader.ReadInt16()
            };
        }

        public OverlayInfo this[int frameNumber]
        {
            get
            {
                lock (stream)
                {
                    if (stream.Length == 0)
                        return null;
                    stream.Position = 0;
                    if (stream.ReadByte() != 1)
                        throw new ArgumentException();
                    stream.Position = 1 + RECORD_LENGHT * frameNumber;
                    if (stream.Position >= stream.Length)
                        return null;
                    return ReadFrame(stream);
                }
            }
            set
            {
                lock (stream)
                {
                    WriteHeader();
                    stream.Position = 1 + RECORD_LENGHT * frameNumber;
                    var writer = new BinaryWriter(stream);
                    if (value == null)
                    {
                        writer.Write(0);
                    }
                    else
                    {
                        value.FrameNumber = frameNumber;
                        WriteInfo(writer, value);
                    }
                    writer.Flush();
                }
            }
        }

        public void Save(params OverlayInfo[] infoList)
        {
            lock (stream)
            {
                WriteHeader();
                var writer = new BinaryWriter(stream);
                foreach (var info in infoList)
                {
                    stream.Position = 1 + RECORD_LENGHT * info.FrameNumber;
                    WriteInfo(writer, info);
                }
                writer.Flush();
            }
        }

        private void WriteHeader()
        {
            stream.Position = 0;
            if (stream.Length == 0)
                stream.WriteByte(1);
        }

        private static void WriteInfo(BinaryWriter writer, OverlayInfo info)
        {
            writer.Write(info.FrameNumber + 1);
            writer.Write((int) (info.Diff * 1000));
            writer.Write((short) info.X);
            writer.Write((short) info.Y);
            writer.Write((short) info.Width);
            writer.Write((short) info.Height);
            writer.Write((short) info.CropLeft);
            writer.Write((short) info.CropTop);
            writer.Write((short) info.CropRight);
            writer.Write((short) info.CropBottom);
            writer.Write((short) info.Angle);
        }

        public void Dispose()
        {
            lock (stream)
            {
                stream.Flush();
                stream.Dispose();
            }
        }
    }
}
