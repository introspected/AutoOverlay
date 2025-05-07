using System.Linq;
using System.Threading.Tasks;
using AutoOverlay;
using AvsFilterNet;

[assembly: AvisynthFilterClass(
    typeof(CombinePlanesMT), nameof(CombinePlanesMT),
    "ccc[Alpha]c[PixelType]s",
    MtMode.NICE_FILTER)]
namespace AutoOverlay
{
    public class CombinePlanesMT : AvisynthFilter
    {
        private Input[] inputs;

        private readonly bool oneFirst = false;

        public sealed override void Initialize(AVSValue args, ScriptEnvironment env)
        {
            var vi = Child.GetVideoInfo();
            vi.pixel_type = args[4].AsString().ParseColorSpace();
            SetVideoInfo(ref vi);
            var planes = vi.pixel_type.GetPlanes();
            inputs = new[] { Child, args[1].AsClip(), args[2].AsClip(), args[3].AsClip() }
                .Take(planes.Length)
                .Select((p, i) => new Input(p, planes[i]))
                .Reverse()
                .ToArray();
        }

        public override VideoFrame GetFrame(int n, ScriptEnvironment env)
        {
            var tasks = inputs.Select(input => Task.Factory.StartNew(() => new
            {
                input.Plane,
                Frame = input.Clip.GetFrame(n, env)
            })).ToList();

            var output = NewVideoFrame(env, tasks.First().Result.Frame);

            Parallel.ForEach(tasks, task =>
            {
                using var frame = task.Result.Frame;
                var plane = task.Result.Plane;
                env.BitBlt(output.GetWritePtr(plane), output.GetPitch(plane),
                    frame.GetReadPtr(), frame.GetPitch(),
                    frame.GetRowSize(), frame.GetHeight());
            });
            return output;
        }

        protected override void Dispose(bool A_0)
        {
            foreach (var input in inputs)
            {
                input.Clip?.Dispose();
            }

            base.Dispose(A_0);
        }

        private record Input(Clip Clip, YUVPlanes Plane);
    }
}
