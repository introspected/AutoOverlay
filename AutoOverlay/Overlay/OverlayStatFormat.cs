using System;
using System.Drawing;
using System.IO;
using AutoOverlay.Overlay;

namespace AutoOverlay
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
                    case 5:
                        return 40 + Warp.MAX_POINTS * 4 * 4;
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
                    return new LegacyOverlayInfo
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
                    }.Convert();
                case 2:
                    return new LegacyOverlayInfo
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
                    }.Convert();
                case 3:
                    return new LegacyOverlayInfo
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
                    }.Convert();
                case 4:
                    return new LegacyOverlayInfo
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
                    }.Convert();
                case 5:
                    return new OverlayInfo
                    {
                        FrameNumber = num,
                        Diff = reader.ReadDouble(),
                        Placement = new Space(reader.ReadSingle(), reader.ReadSingle()),
                        SourceSize = new SizeD(reader.ReadSingle(), reader.ReadSingle()),
                        OverlaySize = new SizeD(reader.ReadSingle(), reader.ReadSingle()),
                        Angle = reader.ReadSingle(),
                        OverlayWarp = Warp.Read(reader)
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
                    writer.Write((float) info.Placement.X);
                    writer.Write((float) info.Placement.Y);
                    writer.Write((float) info.SourceSize.Width);
                    writer.Write((float) info.SourceSize.Height);
                    writer.Write((float) info.OverlaySize.Width);
                    writer.Write((float) info.OverlaySize.Height);
                    writer.Write(info.Angle);
                    info.OverlayWarp.Write(writer);
                    break;
                default:
                    throw new InvalidOperationException();
            }
        }
    }
}
