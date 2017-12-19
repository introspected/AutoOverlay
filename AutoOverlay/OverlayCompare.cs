using AutoOverlay;
using AvsFilterNet;

[assembly: AvisynthFilterClass(typeof(OverlayCompare),
    nameof(OverlayCompare),
    "ccc[sourceText]s[overlayText]s[sourceColor]i[overlayColor]i[borderSize]i[opacity]f[width]i[height]i[info]b",
    MtMode.SERIALIZED)]
namespace AutoOverlay
{
    public class OverlayCompare : OverlayFilter
    {
        private Clip src, over;
        private int srcColor = 0xFF, overColor=0xFF00;
        private int borderSize = 2;
        private string srcText = string.Empty, overText = string.Empty;
        private double opacity = 0.51;
        private bool info = false;

        protected override void Initialize(AVSValue args)
        {
            src = args[1].AsClip();
            over = args[2].AsClip();
            srcText = args[3].AsString(srcText);
            overText = args[4].AsString(overText);
            srcColor = args[5].AsInt(srcColor);
            overColor = args[6].AsInt(overColor);
            borderSize = args[7].AsInt(borderSize);
            opacity = args[8].AsFloat(opacity);
            var width = args[9].AsInt(src.GetVideoInfo().width);
            var height = args[10].AsInt(src.GetVideoInfo().height);
            var vi = src.GetVideoInfo();
            vi.width = width;
            vi.height = height;
            SetVideoInfo(ref vi);
            info = args[11].AsBool(info);
        }

        protected override VideoFrame GetFrame(int n)
        {
            var srcCropped = src.Dynamic()
                .Crop(borderSize, borderSize, -borderSize, -borderSize)
                .AddBorders(borderSize, borderSize, borderSize, borderSize, srcColor);
            var overCropped = over.Dynamic()
                .Crop(borderSize, borderSize, -borderSize, -borderSize)
                .AddBorders(borderSize, borderSize, borderSize, borderSize, overColor);
            return Child.Dynamic().OverlayRender(srcCropped, overCropped, 
                opacity: opacity, mode: (int) OverlayMode.Fill,
                width: GetVideoInfo().width, height: GetVideoInfo().height, debug: info)
                .Subtitle(srcText, size: 32, text_color: srcColor, align: 1)
                .Subtitle(overText, size: 32, text_color: overColor, align: 3)[n];
        }

        protected sealed override void Dispose(bool A_0)
        {
            src.Dispose();
            over.Dispose();
            base.Dispose(A_0);
        }
    }
}
