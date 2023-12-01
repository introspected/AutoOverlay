using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using AvsFilterNet;

namespace AutoOverlay
{
    public class FileOverlayStat : IOverlayStat
    {
        private readonly Stream stream;
        private readonly BinaryReader reader;

        private readonly Size srcSize, overSize;
        private readonly string statFile;

        private readonly OverlayStatFormat format;

        public FileOverlayStat(string statFile)
        {
            this.statFile = statFile == null ? null : Path.GetFullPath(statFile);
            if (!File.Exists(statFile))
                throw new AvisynthException("Stat file not found");
            stream = new BufferedStream(new FileStream(statFile, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite));
            reader = new BinaryReader(stream);
            var header = (byte) stream.ReadByte();
            if (header == 0 || header > OverlayUtils.OVERLAY_FORMAT_VERSION)
                throw new AvisynthException("Unsupported stat file version");
            format = new OverlayStatFormat(header);
        }

        public FileOverlayStat(string statFile, Size srcSize, Size overSize, byte version = OverlayUtils.OVERLAY_FORMAT_VERSION)
        {
            this.srcSize = srcSize;
            this.overSize = overSize;
            this.statFile = statFile == null ? null : Path.GetFullPath(statFile);
            stream = string.IsNullOrEmpty(statFile) ? 
                new BufferedStream(new MemoryStream()) : 
                new BufferedStream(new FileStream(statFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite));
            reader = new BinaryReader(stream);
            format = new OverlayStatFormat(version);
        }

        public IEnumerable<OverlayInfo> Frames
        {
            get
            {
                lock (stream)
                {
                    if (stream.Length == 0)
                        yield break;
                    CheckHeader();
                    for (var position = 1; position < stream.Length - 1; position += format.FrameSize)
                    {
                        stream.Position = position;
                        var info = format.ReadFrame(reader);
                        if (info != null)
                            yield return info;
                    }
                }
            }
        }

        public OverlayInfo this[int frameNumber]
        {
            get
            {
                lock (stream)
                {
                    if (stream.Length == 0)
                        return null;
                    CheckHeader();
                    stream.Position = 1 + format.FrameSize * frameNumber;
                    if (stream.Position >= stream.Length)
                        return null;
                    return format.ReadFrame(reader);
                }
            }
            set
            {
                lock (stream)
                {
                    WriteHeader();
                    stream.Position = 1 + format.FrameSize * frameNumber;
                    var writer = new BinaryWriter(stream);
                    if (value == null)
                    {
                        writer.Write(0);
                    }
                    else
                    {
                        value.FrameNumber = frameNumber;
                        format.WriteFrame(writer, value);
                    }
                    writer.Flush();
                }
            }
        }

        public void Save(params OverlayInfo[] infoList)
        {
            Save(infoList.AsEnumerable());
        }

        public void Save(IEnumerable<OverlayInfo> frames)
        {
            lock (stream)
            {
                WriteHeader();
                var writer = new BinaryWriter(stream);
                foreach (var info in frames)
                {
                    stream.Position = 1 + format.FrameSize * info.FrameNumber;
                    format.WriteFrame(writer, info);
                }
                writer.Flush();
            }
        }

        private void WriteHeader()
        {
            stream.Position = 0;
            if (stream.Length == 0)
                stream.WriteByte(format.Version);
        }

        private void CheckHeader()
        {
            stream.Position = 0;
            var header = (byte) stream.ReadByte();
            if (header > 0 && header < format.Version)
            {
                var backupFile = statFile + $".v{header}.bak";
                File.Copy(statFile, backupFile, true);
                stream.Position = 0;
                stream.WriteByte(format.Version);
                using var backup = new FileOverlayStat(backupFile, srcSize, overSize, header);
                var writer = new BinaryWriter(stream);
                for (var position = 1; position < stream.Length - 1; position += format.FrameSize)
                {
                    stream.Position = position;
                    writer.Write(0);
                }

                Save(backup.Frames.Select(p =>
                {
                    if (p.SourceSize.IsEmpty)
                        p.SourceSize = srcSize;
                    return p;
                }));
            }
            else if (header != format.Version)
            {
                throw new ArgumentException("Wrong file header");
            }
        }

        public void Dispose()
        {
            lock (stream)
            {
                try
                {
                    stream.Flush();
                    stream.Dispose();
                } catch { }
            }
        }
    }
}
