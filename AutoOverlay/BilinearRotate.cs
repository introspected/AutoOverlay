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
using AvsFilterNet;

[assembly: AvisynthFilterClass(typeof(BilinearRotate), nameof(BilinearRotate), "cf", MtMode.NICE_FILTER)]
namespace AutoOverlay
{
    public class BilinearRotate : AvisynthFilter
    {
        const double π = Math.PI;
        const double π2 = Math.PI / 2;
        const double π4 = Math.PI / 4;

        private double angle;
        private bool noRotate;
        private YUVPlanes[] planes;

        public override void Initialize(AVSValue args, ScriptEnvironment env)
        {
            angle = args[1].AsFloat() % 360;

            var vi = GetVideoInfo();
            var colorSpace = vi.pixel_type;
            if (colorSpace.HasFlag(ColorSpaces.CS_INTERLEAVED))
                planes = new[] { default(YUVPlanes) };
            else if (colorSpace.HasFlag(ColorSpaces.CS_PLANAR))
                planes = new[] { YUVPlanes.PLANAR_Y, YUVPlanes.PLANAR_U, YUVPlanes.PLANAR_V };
            else env.ThrowError($"Unsupported color space: {colorSpace}");

            var newSize = CalculateSize(vi.width, vi.height, angle);
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
            var res = NewVideoFrame(env);
            var frame = Child.GetFrame(n, env);
            var vi = Child.GetVideoInfo();
            Parallel.ForEach(planes, plane =>
            {
                var zero = plane == YUVPlanes.PLANAR_U || plane == YUVPlanes.PLANAR_V ? 128 : 0;
                OverlayUtils.MemSet(res.GetWritePtr(plane), zero, res.GetPitch(plane) * res.GetHeight(plane));

                // get source image size
                var height = frame.GetHeight(plane);
                var width = vi.width / (vi.height / height);
                var oldXradius = (width - 1) / 2.0;
                var oldYradius = (height - 1) / 2.0;
                var pixelSize = frame.GetRowSize(plane) / width;

                // get destination image size
                var newWidth = GetVideoInfo().width;
                var newHeight = GetVideoInfo().height;
                var newXradius = (newWidth - 1) / 2.0;
                var newYradius = (newHeight - 1) / 2.0;

                // angle's sine and cosine
                var angleRad = -angle * Math.PI / 180;
                var angleCos = Cos(angleRad);
                var angleSin = Sin(angleRad);

                var ymax = height - 1;
                var xmax = width - 1;

                var srcStride = frame.GetPitch(plane);
                var dstOffset = res.GetPitch(plane) - res.GetRowSize(plane);

                unsafe
                {
                    var src = (byte*) frame.GetReadPtr(plane);
                    var dst = (byte*) res.GetWritePtr(plane);

                    var cy = -newYradius;
                    for (int y = 0; y < newHeight; y++, cy++)
                    {
                        // do some pre-calculations of source points' coordinates
                        // (calculate the part which depends on y-loop, but does not
                        // depend on x-loop)
                        var tx = angleSin * cy + oldXradius;
                        var ty = angleCos * cy + oldYradius;

                        var cx = -newXradius;
                        for (int x = 0; x < newWidth; x++, dst += pixelSize, cx++)
                        {
                            // coordinates of source point
                            var ox = tx + angleCos * cx;
                            var oy = ty - angleSin * cx;

                            // top-left coordinate
                            var ox1 = (int) ox;
                            var oy1 = (int) oy;

                            // validate source pixel's coordinates
                            if (ox1 >= 0 && oy1 >= 0 && ox1 < width && oy1 < height)
                            {
                                // bottom-right coordinate
                                var ox2 = ox1 == xmax ? ox1 : ox1 + 1;
                                var oy2 = oy1 == ymax ? oy1 : oy1 + 1;

                                var dx1 = ox - ox1;
                                if (dx1 < 0)
                                    dx1 = 0;
                                var dx2 = 1.0f - dx1;

                                var dy1 = oy - oy1;
                                if (dy1 < 0)
                                    dy1 = 0;
                                var dy2 = 1.0f - dy1;

                                // get four points
                                byte* p1, p2;
                                p1 = p2 = src + oy1 * srcStride;
                                p1 += ox1 * pixelSize;
                                p2 += ox2 * pixelSize;

                                byte* p3, p4;
                                p3 = p4 = src + oy2 * srcStride;
                                p3 += ox1 * pixelSize;
                                p4 += ox2 * pixelSize;

                                // interpolate using 4 points

                                for (var z = 0; z < pixelSize; z++)
                                {
                                    dst[z] = (byte) (
                                        dy2 * (dx2 * p1[z] + dx1 * p2[z]) +
                                        dy1 * (dx2 * p3[z] + dx1 * p4[z]));
                                }
                            }
                        }
                        dst += dstOffset;
                    }
                }
            });
            return res;
        }

        public static Size CalculateSize(int width, int height, double angle)
        {
            //Debug.WriteLine($"angle: {angle}");
            if (Math.Abs(angle) < float.Epsilon)
                return new Size(width, height);
            // angle's sine and cosine
            var angleRad = -angle * (Math.PI / 180);
            var angleCos = Cos(angleRad);
            var angleSin = Sin(angleRad);

            // calculate half size
            var halfWidth = width / 2.0;
            var halfHeight = height / 2.0;

            // rotate corners
            var cx1 = halfWidth * angleCos;
            var cy1 = halfWidth * angleSin;

            var cx2 = halfWidth * angleCos - halfHeight * angleSin;
            var cy2 = halfWidth * angleSin + halfHeight * angleCos;

            var cx3 = -halfHeight * angleSin;
            var cy3 = halfHeight * angleCos;

            var cx4 = 0;
            var cy4 = 0;

            // recalculate image size
            halfWidth = Math.Max(Math.Max(cx1, cx2), Math.Max(cx3, cx4)) - Math.Min(Math.Min(cx1, cx2), Math.Min(cx3, cx4));
            halfHeight = Math.Max(Math.Max(cy1, cy2), Math.Max(cy3, cy4)) - Math.Min(Math.Min(cy1, cy2), Math.Min(cy3, cy4));

            var newWidth = (int)(halfWidth * 2 + 0.5);
            var newHeight = (int)(halfHeight * 2 + 0.5);

            return new Size(newWidth, newHeight);
        }

        private static double Sin(double x)
        {
            if (x == 0) { return 0; }
            if (x < 0) { return -Sin(-x); }
            if (x > π) { return -Sin(x - π); }
            if (x > π4) { return Cos(π2 - x); }

            var x2 = x * x;

            return x * (x2 / 6 * (x2 / 20 * (x2 / 42 * (x2 / 72 * (x2 / 110 * (x2 / 156 - 1) + 1) - 1) + 1) - 1) + 1);
        }

        private static double Cos(double x)
        {
            if (x == 0) { return 1; }
            if (x < 0) { return Cos(-x); }
            if (x > π) { return -Cos(x - π); }
            if (x > π4) { return Sin(π2 - x); }

            var x2 = x * x;

            return x2 / 2 * (x2 / 12 * (x2 / 30 * (x2 / 56 * (x2 / 90 * (x2 / 132 - 1) + 1) - 1) + 1) - 1) + 1;
        }
    }
}
