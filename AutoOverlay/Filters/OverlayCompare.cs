using AutoOverlay;
using AutoOverlay.AviSynth;
using AvsFilterNet;

[assembly: AvisynthFilterClass(typeof(OverlayCompare),
    nameof(OverlayCompare),
    "ccc[SourceText]s[OverlayText]s[SourceColor]i[OverlayColor]i[BorderSize]i[Opacity]f[Width]i[Height]i[Debug]b",
    MtMode.SERIALIZED)]
namespace AutoOverlay
{
    public class OverlayCompare : OverlayFilter
    {
        [AvsArgument(Required = true)]
        public Clip Engine { get; private set; }
        [AvsArgument(Required = true)]
        public Clip Source { get; private set; }
        [AvsArgument(Required = true)]
        public Clip Overlay { get; private set; }
        [AvsArgument]
        public string SourceText { get; private set; } = "Source";
        [AvsArgument]
        public string OverlayText { get; private set; } = "Overlay";
        [AvsArgument]
        public int SourceColor { get; private set; } = 0xFF;
        [AvsArgument]
        public int OverlayColor { get; private set; }= 0xFF00;
        [AvsArgument(Min = 1, Max = 10)]
        public int BorderSize { get; private set; } = 2;
        [AvsArgument(Min = 0, Max = 1)]
        public double Opacity { get; private set; } = 0.51;
        [AvsArgument(Min = 1)]
        public int Width { get; private set; }
        [AvsArgument(Min = 1)]
        public int Height { get; private set; }
        [AvsArgument]
        public override bool Debug { get; protected set; }

        protected override void Initialize(AVSValue args)
        {
            var vi = Source.GetVideoInfo();
            if (Width > 0)
                vi.width = Width;
            if (Height > 0)
                vi.height = Height;
            SetVideoInfo(ref vi);
        }

        protected override VideoFrame GetFrame(int n)
        {
            var srcCropped = Source.Dynamic()
                .Crop(BorderSize, BorderSize, -BorderSize, -BorderSize)
                .AddBorders(BorderSize, BorderSize, BorderSize, BorderSize, SourceColor);
            var overCropped = Overlay.Dynamic()
                .Crop(BorderSize, BorderSize, -BorderSize, -BorderSize)
                .AddBorders(BorderSize, BorderSize, BorderSize, BorderSize, OverlayColor);
            return Child.Dynamic().OverlayRender(srcCropped, overCropped, 
                opacity: Opacity, mode: (int) FramingMode.Fill,
                width: GetVideoInfo().width, height: GetVideoInfo().height, debug: Debug)
                .Subtitle(SourceText, size: 32, text_color: SourceColor, align: 1)
                .Subtitle(OverlayText, size: 32, text_color: OverlayColor, align: 3)[n];
        }
    }
}
