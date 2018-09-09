using System.Threading.Tasks;
using AutoOverlay;
using AvsFilterNet;

[assembly: AvisynthFilterClass(typeof(DynamicOverlayRender),
    nameof(OverlayRender),
    "ccc[SourceMask]c[OverlayMask]c" +
    "[LumaOnly]b[Width]i[Height]i[Gradient]i[Noise]i[DynamicNoise]b" +
    "[Mode]i[Opacity]f[ColorAdjust]i[Matrix]s[Upsize]s[Downsize]s[Rotate]s[Debug]b",
    MtMode.SERIALIZED)]
namespace AutoOverlay
{
    public class DynamicOverlayRender : OverlayRender
    {
        [AvsArgument(Required = true)]
        public Clip Engine { get; private set; }

        [AvsArgument(Required = true)]
        public override Clip Source { get; protected set; }
        
        [AvsArgument(Required = true)]
        public override Clip Overlay { get; protected set; }

        [AvsArgument]
        public override Clip SourceMask { get; protected set; }

        [AvsArgument]
        public override Clip OverlayMask { get; protected set; }

        [AvsArgument]
        public override bool LumaOnly { get; protected set; }

        [AvsArgument(Min = 1)]
        public override int Width { get; protected set; }

        [AvsArgument(Min = 1)]
        public override int Height { get; protected set; }

        [AvsArgument(Min = 0)]
        public override int Gradient { get; protected set; }

        [AvsArgument(Min = 0)]
        public override int Noise { get; protected set; }

        [AvsArgument(Min = 0)]
        public override bool DynamicNoise { get; protected set; } = true;

        [AvsArgument(Min = 0)]
        public override OverlayMode Mode { get; protected set; } = OverlayMode.Fit;

        [AvsArgument(Min = 0, Max = 1)]
        public override double Opacity { get; protected set; } = 1;

        [AvsArgument]
        public override ColorAdjustMode ColorAdjust { get; protected set; } = ColorAdjustMode.None;

        [AvsArgument]
        public override string Matrix { get; protected set; }

        [AvsArgument]
        public override string Upsize { get; protected set; } = OverlayUtils.DEFAULT_RESIZE_FUNCTION;

        [AvsArgument]
        public override string Downsize { get; protected set; } = OverlayUtils.DEFAULT_RESIZE_FUNCTION;

        [AvsArgument]
        public override string Rotate { get; protected set; } = "BilinearRotate";

        [AvsArgument]
        public override bool Debug { get; protected set; }

        protected override VideoFrame GetFrame(int n)
        {
            OverlayInfo info;
            //lock (Child)
            using (var infoFrame = Child.GetFrame(n, StaticEnv))
                info = OverlayInfo.FromFrame(infoFrame);
            var hybrid = RenderFrame(info);
            if (Debug)
                hybrid = hybrid.Subtitle(info.ToString().Replace("\n", "\\n"), lsp: 0);
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
