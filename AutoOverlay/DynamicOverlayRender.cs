using System.Drawing;
using System.Threading.Tasks;
using AutoOverlay;
using AvsFilterNet;

[assembly: AvisynthFilterClass(typeof(DynamicOverlayRender),
    nameof(OverlayRender),
    "ccc[SourceMask]c[OverlayMask]c[LumaOnly]b[Width]i[Height]i[Gradient]i[Noise]i[DynamicNoise]b[Mode]i[Opacity]f[colorAdjust]i[matrix]s[Upsize]s[Downsize]s[Rotate]s[Debug]b",
    MtMode.NICE_FILTER)]
namespace AutoOverlay
{
    public class DynamicOverlayRender : OverlayRender
    {
        protected override void Initialize(AVSValue args)
        {
            srcClip = args[1].AsClip();
            overClip = args[2].AsClip();
            srcSize = new Size(srcClip.GetVideoInfo().width, srcClip.GetVideoInfo().height);
            overSize = new Size(overClip.GetVideoInfo().width, overClip.GetVideoInfo().height);
            srcMaskClip = args[3].IsClip() ? args[3].AsClip() : null;
            overMaskClip = args[4].IsClip() ? args[4].AsClip() : null;
            lumaOnly = args[5].AsBool(lumaOnly);
            var width = args[6].AsInt(srcClip.GetVideoInfo().width);
            var height = args[7].AsInt(srcClip.GetVideoInfo().height);

            var vi = srcClip.GetVideoInfo();
            vi.width = width;
            vi.height = height;
            vi.pixel_type = srcClip.GetVideoInfo().pixel_type;
            vi.num_frames = Child.GetVideoInfo().num_frames;
            SetVideoInfo(ref vi);

            gradient = args[8].AsInt(gradient);
            noise = args[9].AsInt(noise);
            dynamicNoise = args[10].AsBool(dynamicNoise);
            overlayMode = (OverlayMode) args[11].AsInt((int) overlayMode);
            opacity = args[12].AsFloat(opacity);
            colorAdjust = (ColorAdjustMode) args[13].AsInt((int) colorAdjust);
            matrix = args[14].AsString(matrix);
            upsizeFunc = args[15].AsString(upsizeFunc);
            downsizeFunc = args[16].AsString(downsizeFunc);
            rotateFunc = args[17].AsString(rotateFunc);
            debug = args[18].AsBool(debug);
        }
        
        protected override VideoFrame GetFrame(int n)
        {
            OverlayInfo info;
            lock (Child)
                using (var infoFrame = Child.GetFrame(n, StaticEnv))
                    info = OverlayInfo.FromFrame(infoFrame);
            var hybrid = RenderFrame(info);
            if (debug)
                return hybrid.Subtitle(info.ToString().Replace("\n", "\\n"), lsp: 0)[n];

            var res = NewVideoFrame(StaticEnv);
            using (VideoFrame frame = hybrid[n])
            {
                Parallel.ForEach(new[] {YUVPlanes.PLANAR_Y, YUVPlanes.PLANAR_U, YUVPlanes.PLANAR_V}, plane =>
                {
                    for (var y = 0; y < frame.GetHeight(plane); y++)
                        OverlayUtils.CopyMemory(res.GetWritePtr(plane) + y * res.GetPitch(plane),
                            frame.GetReadPtr(plane) + y * frame.GetPitch(plane), res.GetRowSize(plane));
                });
            }
            return res;
        }
    }
}
