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
            var main = inputs.First().Clip.GetFrame(n, env);
            var output = NewVideoFrame(env, main);
            
            void Copy(Input input, VideoFrame overrideFrame)
            {
                using var frame = overrideFrame ?? input.Clip.GetFrame(n, env);
                env.BitBlt(output.GetWritePtr(input.Plane), output.GetPitch(input.Plane),
                    frame.GetReadPtr(), frame.GetPitch(),
                    frame.GetRowSize(), frame.GetHeight());
            }

            if (oneFirst)
                Copy(inputs.First(), main);
            var loop = inputs.Select((input, i) => (input, i)).Skip(oneFirst ? 1 : 0);
            Parallel.ForEach(loop, (p, i) => Copy(p.input, p.i == 0 ? main : null));
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
