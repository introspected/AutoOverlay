using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AutoOverlay.Overlay;

namespace AutoOverlay.Stat
{
    public class OverlayStatFormat
    {
        public byte Version { get; }

        public int FrameSize
        {
            get
            {
                switch (Version)
                {
                    case 1:
                    case 2:
                        return 26;
                    case 3:
                        return 38;
                    case 4:
                        return 38 + Warp.MAX_POINTS * 4 * 4;
                    default: 
                        throw new InvalidOperationException();
                }
            }
        }

        public OverlayStatFormat(byte version)
        {
            Version = version;
        }

        public OverlayInfo ReadFrame(BinaryReader reader)
        {
            var num = reader.ReadInt32() - 1;
            if (num < 0) return null;
            switch (Version)
            {
                case 1:
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
                case 2:
                    return new OverlayInfo
                    {
                        FrameNumber = num,
                        Diff = reader.ReadInt32() / 10000.0,
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
                case 3:
                    return new OverlayInfo
                    {
                        FrameNumber = num,
                        Diff = reader.ReadDouble(),
                        X = reader.ReadInt16(),
                        Y = reader.ReadInt16(),
                        Width = reader.ReadInt16(),
                        Height = reader.ReadInt16(),
                        CropLeft = reader.ReadInt16(),
                        CropTop = reader.ReadInt16(),
                        CropRight = reader.ReadInt16(),
                        CropBottom = reader.ReadInt16(),
                        Angle = reader.ReadInt16(),
                        BaseWidth = reader.ReadInt16(),
                        BaseHeight = reader.ReadInt16(),
                        SourceWidth = reader.ReadInt16(),
                        SourceHeight = reader.ReadInt16()
                    };
                case 4:
                    return new OverlayInfo
                    {
                        FrameNumber = num,
                        Diff = reader.ReadDouble(),
                        X = reader.ReadInt16(),
                        Y = reader.ReadInt16(),
                        Width = reader.ReadInt16(),
                        Height = reader.ReadInt16(),
                        CropLeft = reader.ReadInt16(),
                        CropTop = reader.ReadInt16(),
                        CropRight = reader.ReadInt16(),
                        CropBottom = reader.ReadInt16(),
                        Angle = reader.ReadInt16(),
                        BaseWidth = reader.ReadInt16(),
                        BaseHeight = reader.ReadInt16(),
                        SourceWidth = reader.ReadInt16(),
                        SourceHeight = reader.ReadInt16(),
                        Warp = Warp.Read(reader)
                    };
                default:
                    throw new InvalidOperationException();
            }
        }

        public void WriteFrame(BinaryWriter writer, OverlayInfo info)
        {
            switch (Version)
            {
                case OverlayUtils.OVERLAY_FORMAT_VERSION:
                    writer.Write(info.FrameNumber + 1);
                    writer.Write(info.Diff);
                    writer.Write((short) info.X);
                    writer.Write((short) info.Y);
                    writer.Write((short) info.Width);
                    writer.Write((short) info.Height);
                    writer.Write((short) info.CropLeft);
                    writer.Write((short) info.CropTop);
                    writer.Write((short) info.CropRight);
                    writer.Write((short) info.CropBottom);
                    writer.Write((short) info.Angle);
                    writer.Write((short) info.BaseWidth);
                    writer.Write((short) info.BaseHeight);
                    writer.Write((short) info.SourceWidth);
                    writer.Write((short) info.SourceHeight);
                    info.Warp.Write(writer);
                    break;
                default:
                    throw new InvalidOperationException();
            }
        }
    }
}
