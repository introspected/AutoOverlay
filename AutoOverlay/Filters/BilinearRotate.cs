// Based on
// AForge Image Processing Library
// AForge.NET framework
// http://www.aforgenet.com/framework/
//
// Copyright © AForge.NET, 2005-2011
// contacts@aforgenet.com
//

using System;
using System.Drawing;
using System.Threading.Tasks;
using AutoOverlay;
using AutoOverlay.Overlay;
using AvsFilterNet;

[assembly: AvisynthFilterClass(typeof(BilinearRotate), nameof(BilinearRotate), "cf", OverlayConst.DEFAULT_MT_MODE)]
namespace AutoOverlay
{
    public class BilinearRotate : AvisynthFilter
    {
        private double angle;
        private bool noRotate;
        private PlaneChannel[] planes;

        public override void Initialize(AVSValue args, ScriptEnvironment env)
        {
            angle = args[1].AsFloat() % 360;

            if (GetVideoInfo().IsRGB())
                angle = -angle;

            var vi = GetVideoInfo();
            var colorSpace = vi.pixel_type;

            planes = colorSpace.GetPlanesOnly();

            var newSize = CalculateSize(vi.width, vi.height, angle).Floor();
            if (newSize.Width == vi.width && newSize.Height == vi.height)
                noRotate = true;
            else
            {
                vi.width = newSize.Width;
                vi.height = newSize.Height;
                SetVideoInfo(ref vi);
            }
        }

        public override VideoFrame GetFrame(int n, ScriptEnvironment env)
        {
            if (noRotate)
                return base.GetFrame(n, env);
            using var frame = Child.GetFrame(n, env);
            var res = NewVideoFrame(env, frame);
            Parallel.ForEach(planes, planeChannel =>
            {
                var plane = planeChannel.Plane;

                var zero = plane.IsChroma() ? 128 : 0;
                DotNetUtils.MemSet(res.GetWritePtr(plane), zero, res.GetPitch(plane) * res.GetHeight(plane));

                var inPlane = new FramePlane(planeChannel, frame, true);
                var outPlane = new FramePlane(planeChannel, res, false);
                NativeUtils.BilinearRotate(inPlane, outPlane, angle);
            });

            return res;
        }

        public static SizeD CalculateSize(SizeD size, double angle) => CalculateSize(size.Width, size.Height, angle);

        public static SizeD CalculateSize(double width, double height, double angle)
        {
            NativeUtils.CalculateRotationBounds(width, height, angle, out var w, out var h);
            return new(w, h);
        }
    }
}
