using AutoOverlay;
using AvsFilterNet;
using System.Collections.Generic;
using System.Linq;
using AutoOverlay.AviSynth;

[assembly: AvisynthFilterClass(
    typeof(WarpTemplate), nameof(WarpTemplate),
    "c.+[Debug]b",
    OverlayConst.DEFAULT_MT_MODE)]
namespace AutoOverlay
{
    public class WarpTemplate : OverlayFilter
    {
        [AvsArgument]
        public object[] Array { get; private set; }

        [AvsArgument] 
        public override bool Debug { get; protected set; }

        private List<Clip> rects = new();

        protected override void AfterInitialize()
        {
            var defaultRadius = Array.First() is int def ? def : 1;
            var size = Child.GetVideoInfo();
            var w = size.width;// - 1;
            var h = size.height;// - 1;

            for (var i = Array.First() is int ? 1 : 0; i < Array.Length; i++)
            {
                var item = Array[i];
                var radius = i == Array.Length - 1 ? defaultRadius :
                    Array[i + 1] is int rad ? rad.Also(p => i++) : defaultRadius;
                if (item is string point)
                {
                    dynamic Rect(double x, double y) => DynamicEnv.Rect(x, y, radius, radius, Debug);
                    var rect = point switch
                    {
                        "TL" => Rect(0, 0),
                        "T" => Rect(w / 2, 0),
                        "TR" => Rect(w, 0),
                        "R" => Rect(w, h / 2),
                        "BR" => Rect(w, h),
                        "B" => Rect(w / 2, h),
                        "BL" => Rect(0, h),
                        "L" => Rect(0, h / 2),
                        "C" => Rect(w / 2, h / 2),
                        "CL" => Rect(w / 3, h / 2),
                        "CR" => Rect((2 * w) / 3, h / 2),
                        "CT" => Rect(w / 2, h / 3),
                        "CB" => Rect(w / 2, (2 * h) / 3),
                    };
                    rects.Add(rect);
                }
                else throw new AvisynthException("Invalid value: " + item);
            }

            var vi = rects.First().GetVideoInfo();
            vi.num_frames = rects.Count;
            SetVideoInfo(ref vi);
        }

        protected override VideoFrame GetFrame(int n)
        {
            return rects[n].GetFrame(n, StaticEnv);
        }

        protected override void Dispose(bool disposing)
        {
            rects.ForEach(p => p.Dispose());
            base.Dispose(disposing);
        }
    }
}
