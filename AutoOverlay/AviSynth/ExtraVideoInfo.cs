using System.Drawing;
using AvsFilterNet;

namespace AutoOverlay.AviSynth
{
    public class ExtraVideoInfo
    {
        public int Width { get; }
        public int Height { get; }
        public Size Size { get; set; }
        public ExtraVideoInfo Chroma { get; set; }
        public int Area { get; }
        public double AspectRatio { get; }
        public ColorSpaces ColorSpace { get; }
        public int FrameCount { get; }
        public VideoInfo Info { get; }

        public ExtraVideoInfo(VideoInfo info)
        {
            Width = info.width;
            Height = info.height;
            Size = new Size(Width, Height);
            Area = Width * Height;
            AspectRatio = (double) Width / Height;
            ColorSpace = info.pixel_type;
            FrameCount = info.num_frames;
            Info = info;
            if (ColorSpace.IsRealPlanar())
            {
                var vi = info;
                vi.width = Width / ColorSpace.GetWidthSubsample();
                vi.height = Height / ColorSpace.GetHeightSubsample();
                vi.pixel_type = ColorSpaces.CS_Y8;
                Chroma = vi;
            }
            else Chroma = this;
        }

        public static implicit operator ExtraVideoInfo(VideoInfo info)
        {
            return new ExtraVideoInfo(info);
        }

        public static implicit operator VideoInfo(ExtraVideoInfo info)
        {
            return info.Info;
        }
    }
}
