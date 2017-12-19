using System;
using System.Drawing;
using AutoOverlay;
using AvsFilterNet;

[assembly: AvisynthFilterClass(typeof(StaticOverlayRender),
    nameof(StaticOverlayRender),
    "cc[x]i[y]i[angle]f[width]i[height]i[cropLeft]f[cropTop]f[cropRight]f[cropBottom]f[diff]f" +
    "[SourceMask]c[OverlayMask]c[LumaOnly]b[outWidth]i[outHeight]i[Gradient]i[Noise]i[DynamicNoise]b[Mode]i[Opacity]f[colorAdjust]i[matrix]s[Upsize]s[Downsize]s[Rotate]s[Debug]b",
    MtMode.SERIALIZED)]
namespace AutoOverlay
{
    public class StaticOverlayRender : OverlayRender
    {
        private OverlayInfo overlaySettings;

        protected override void Initialize(AVSValue args)
        {
            srcClip = args[0].AsClip();
            overClip = args[1].AsClip();
            overlaySettings = new OverlayInfo
            {
                X = args[2].AsInt(),
                Y = args[3].AsInt(),
                Angle = (int) Math.Round(args[4].AsFloat()*100),
                Width = args[5].AsInt(overClip.GetVideoInfo().width),
                Height = args[6].AsInt(overClip.GetVideoInfo().height),
                Diff = args[11].AsFloat(-1)
            };
            overlaySettings.SetCrop(RectangleF.FromLTRB(
                (float) args[7].AsFloat(),
                (float) args[8].AsFloat(),
                (float) args[9].AsFloat(),
                (float) args[10].AsFloat()));
            srcSize = new Size(srcClip.GetVideoInfo().width, srcClip.GetVideoInfo().height);
            overSize = new Size(overClip.GetVideoInfo().width, overClip.GetVideoInfo().height);
            srcMaskClip = args[12].IsClip() ? args[12].AsClip() : null;
            overMaskClip = args[13].IsClip() ? args[13].AsClip() : null;
            lumaOnly = args[14].AsBool(lumaOnly);
            var width = args[15].AsInt(srcClip.GetVideoInfo().width);
            var height = args[16].AsInt(srcClip.GetVideoInfo().height);

            var vi = srcClip.GetVideoInfo();
            vi.width = width;
            vi.height = height;
            vi.pixel_type = srcClip.GetVideoInfo().pixel_type;
            SetVideoInfo(ref vi);

            gradient = args[17].AsInt(gradient);
            noise = args[18].AsInt(noise);
            dynamicNoise = args[19].AsBool(dynamicNoise);
            overlayMode = (OverlayMode) args[20].AsInt((int)overlayMode);
            opacity = args[21].AsFloat(opacity);
            colorAdjust = (ColorAdjustMode)args[22].AsInt((int)colorAdjust);
            matrix = args[23].AsString(matrix);
            upsizeFunc = args[24].AsString(upsizeFunc);
            downsizeFunc = args[25].AsString(downsizeFunc);
            rotateFunc = args[26].AsString(rotateFunc);
            debug = args[27].AsBool(debug);
        }

        protected override VideoFrame GetFrame(int n)
        {
            overlaySettings.FrameNumber = n;
            var hybrid = RenderFrame(overlaySettings);
            if (debug)
                return hybrid.Subtitle(overlaySettings.ToString().Replace("\n", "\\n"), lsp: 0)[n];
            return hybrid[n];
        }
    }
}
