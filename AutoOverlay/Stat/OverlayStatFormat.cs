using System;
using System.IO;
using AutoOverlay.Overlay;

namespace AutoOverlay
{
    public class OverlayStatFormat(byte version)
    {
        public byte Version { get; } = version;

        public int FrameSize => Version switch
        {
            1 or 2 => 26,
            3 => 38,
            4 => 38 + Warp.MAX_POINTS * 4 * 4,
            5 => 40 + Warp.MAX_POINTS * 4 * 4,
            6 => 64 + Warp.MAX_POINTS * 8 * 4,
            _ => throw new InvalidOperationException()
        };

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
                        Warp = Warp.Read(reader, p => p.ReadSingle())
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
                        OverlayWarp = Warp.Read(reader, p => p.ReadSingle())
                    };
                case 6:
                    return new OverlayInfo
                    {
                        FrameNumber = num,
                        Diff = reader.ReadDouble(),
                        Placement = new Space(reader.ReadDouble(), reader.ReadDouble()),
                        SourceSize = new SizeD(reader.ReadDouble(), reader.ReadDouble()),
                        OverlaySize = new SizeD(reader.ReadDouble(), reader.ReadDouble()),
                        Angle = reader.ReadSingle(),
                        OverlayWarp = Warp.Read(reader, p => p.ReadDouble())
                    };
                default:
                    throw new InvalidOperationException();
            }
        }

        public void WriteFrame(BinaryWriter writer, OverlayInfo info)
        {
            switch (Version)
            {
                case OverlayConst.OVERLAY_FORMAT_VERSION:
                    writer.Write(info.FrameNumber + 1);
                    writer.Write(info.Diff);
                    writer.Write(info.Placement.X);
                    writer.Write(info.Placement.Y);
                    writer.Write(info.SourceSize.Width);
                    writer.Write(info.SourceSize.Height);
                    writer.Write(info.OverlaySize.Width);
                    writer.Write(info.OverlaySize.Height);
                    writer.Write(info.Angle);
                    info.OverlayWarp.Write(writer);
                    break;
                default:
                    throw new InvalidOperationException();
            }
        }
    }
}
