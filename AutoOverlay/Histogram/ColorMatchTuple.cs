using AvsFilterNet;
using System;
using System.Linq;

namespace AutoOverlay
{
    public record ColorMatchTuple(
        PlaneChannel Sample,
        PlaneChannel Reference,
        PlaneChannel SampleMask,
        PlaneChannel ReferenceMask,
        PlaneChannel Input,
        PlaneChannel Output)
    {
        public static ColorMatchTuple[] Compose(
            Clip input, 
            Clip sample, 
            Clip reference, 
            string channels,
            bool greyMask,
            string plane)
        {
            channels = channels?.ToLower();
            var rgb = input.GetVideoInfo().IsRGB();
            var refPixelType = reference.GetVideoInfo().pixel_type;
            var inputPixelType = input.GetVideoInfo().pixel_type.VPlaneFirst();
            var effectivePlane = plane.GetPlane();
            var samplePlaneChannels = sample.GetVideoInfo().pixel_type.GetPlaneChannels(effectivePlane);
            var referencePlaneChannels = refPixelType.GetPlaneChannels(effectivePlane);
            var inputPlaneChannels = inputPixelType.GetPlaneChannels(effectivePlane);
            var outputPlaneChannels = inputPixelType.ChangeBitDepth(refPixelType.GetBitDepth()).GetPlaneChannels(effectivePlane);
            var samplePlanes = samplePlaneChannels.Select(p => p.EffectivePlane).ToHashSet();
            var referencePlanes = referencePlaneChannels.Select(p => p.EffectivePlane).ToHashSet();
            var matchPlanes = samplePlanes.Intersect(referencePlanes)
                .Where(p => channels?.Contains(p.GetKey()) ?? true)
                .ToHashSet();
            var maskPlane = effectivePlane == default ? YUVPlanes.PLANAR_Y : effectivePlane;
            return matchPlanes.Select(p => new ColorMatchTuple(
                    samplePlaneChannels.First(c => c.EffectivePlane == p),
                    referencePlaneChannels.First(c => c.EffectivePlane == p),
                    samplePlaneChannels.First(c => c.EffectivePlane == (greyMask && !rgb ? maskPlane : p)),
                    referencePlaneChannels.First(c => c.EffectivePlane == (greyMask && !rgb ? maskPlane : p)),
                    inputPlaneChannels.First(c => c.EffectivePlane == p),
                    outputPlaneChannels.First(c => c.EffectivePlane == p)))
                .ToArray();
        }
    }
}
