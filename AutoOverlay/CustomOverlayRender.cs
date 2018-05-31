using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoOverlay;
using AvsFilterNet;

[assembly: AvisynthFilterClass(typeof(CustomOverlayRender),
    nameof(CustomOverlayRender),
    "cccs[Width]i[Height]i[Debug]b",
    MtMode.NICE_FILTER)]
namespace AutoOverlay
{
    public class CustomOverlayRender : OverlayFilter
    {
        private Clip srcClip;
        private Clip overClip;
        private string userFunction;

        protected override void Initialize(AVSValue args)
        {
            srcClip = args[1].AsClip();
            overClip = args[2].AsClip();
            userFunction = args[3].AsString();
            debug = args[6].AsBool(debug);
            var width = args[4].AsInt(srcClip.GetVideoInfo().width);
            var height = args[5].AsInt(srcClip.GetVideoInfo().height);
            var vi = GetVideoInfo();
            vi.width = width;
            vi.height = height;
            vi.pixel_type = srcClip.GetVideoInfo().pixel_type;
            vi.fps_numerator = srcClip.GetVideoInfo().fps_numerator;
            vi.fps_denominator = srcClip.GetVideoInfo().fps_denominator;
            SetVideoInfo(ref vi);
        }

        protected override VideoFrame GetFrame(int n)
        {
            OverlayInfo info;
            lock (Child)
                using (var infoFrame = Child.GetFrame(n, StaticEnv))
                    info = OverlayInfo.FromFrame(infoFrame);
            var crop = info.GetCrop();
            var hybrid = DynamicEnv.Invoke(userFunction,
                Child, srcClip, overClip, info.X, info.Y, info.Angle / 100.0, info.Width, info.Height, 
                crop.Left, crop.Top, info.CropRight, info.CropBottom, info.Diff);
            if (debug)
                hybrid = hybrid.Subtitle(info.ToString().Replace("\n", "\\n"), lsp: 0);
            var res = NewVideoFrame(StaticEnv);
            using (VideoFrame frame = hybrid[n])
            {
                Parallel.ForEach(new[] { YUVPlanes.PLANAR_Y, YUVPlanes.PLANAR_U, YUVPlanes.PLANAR_V }, plane =>
                {
                    for (var y = 0; y < frame.GetHeight(plane); y++)
                        OverlayUtils.CopyMemory(res.GetWritePtr(plane) + y * res.GetPitch(plane),
                            frame.GetReadPtr(plane) + y * frame.GetPitch(plane), res.GetRowSize(plane));
                });
            }
            return res;
        }

        protected sealed override void Dispose(bool A_0)
        {
            srcClip.Dispose();
            overClip.Dispose();
            base.Dispose(A_0);
        }
    }
}
