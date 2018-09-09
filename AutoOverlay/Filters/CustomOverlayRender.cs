using System.Threading.Tasks;
using AutoOverlay;
using AvsFilterNet;

[assembly: AvisynthFilterClass(typeof(CustomOverlayRender),
    nameof(CustomOverlayRender),
    "ccc[Function]s[Width]i[Height]i[Debug]b",
    MtMode.SERIALIZED)]
namespace AutoOverlay
{
    public class CustomOverlayRender : OverlayFilter
    {
        [AvsArgument(Required = true)] public Clip Engine { get; private set; }

        [AvsArgument(Required = true)]
        public Clip Source { get; private set; }

        [AvsArgument(Required = true)]
        public Clip Overlay { get; private set; }

        [AvsArgument(Required = true)]
        public string Function { get; private set; }

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
            OverlayInfo info;
            lock (Child)
                using (var infoFrame = Child.GetFrame(n, StaticEnv))
                    info = OverlayInfo.FromFrame(infoFrame);
            var crop = info.GetCrop();
            var hybrid = DynamicEnv.Invoke(Function,
                Child, Source, Overlay, info.X, info.Y, info.Angle / 100.0, info.Width, info.Height, 
                crop.Left, crop.Top, info.CropRight, info.CropBottom, info.Diff);
            if (Debug)
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
    }
}
