using System.Linq;
using System.Threading.Tasks;
using AutoOverlay;
using AutoOverlay.AviSynth;
using AvsFilterNet;

[assembly: AvisynthFilterClass(typeof(CustomOverlayRender),
    nameof(CustomOverlayRender),
    "ccc[Function]s[Width]i[Height]i[Debug]b",
    OverlayConst.DEFAULT_MT_MODE)]
namespace AutoOverlay
{
    public class CustomOverlayRender : OverlayFilter
    {
        [AvsArgument(Required = true)]
        public Clip Engine { get; private set; }

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
                    info = OverlayInfo.FromFrame(infoFrame).First().ScaleBySource(Source.GetVideoInfo().GetSize());
            var hybrid = DynamicEnv.Invoke(Function,
                Engine, Source, Overlay, info.Placement, info.Angle, info.OverlaySize, info.Diff);
            if (Debug)
                hybrid = hybrid.Subtitle(info.ToString().Replace("\n", "\\n"), lsp: 0);
            var res = NewVideoFrame(StaticEnv);
            using VideoFrame frame = hybrid[n];
            frame.CopyTo(res, GetVideoInfo().pixel_type.GetPlanes());
            return res;
        }
    }
}
