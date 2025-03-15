using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using AutoOverlay.Overlay;
using AvsFilterNet;

namespace AutoOverlay
{
    public static class OverlayUtils
    {
        public static double StdDev(VideoFrame frame)
        {
            var height = frame.GetHeight();
            var rowSize = frame.GetRowSize();
            var rowOffset = frame.GetPitch() - rowSize;
            long sum = 0;
            long squareSum = 0;
            unsafe
            {
                var data = (byte*)frame.GetReadPtr();
                for (var y = 0; y < height; y++, data += rowOffset)
                {
                    for (var x = 0; x < rowSize; x++)
                    {
                        var val = data[x];
                        sum += val;
                        squareSum += val * val;
                    }
                }
                double valCount = rowSize * height;
                var mean = sum / valCount;
                var variance = squareSum / valCount - mean * mean;
                return Math.Sqrt(variance);
            }
        }

        public static double GetFraction(this double val)
        {
            return val - Math.Truncate(val);
        }

        public static int GetWarpResampleMode(string resizeFunction)
        {
            if (resizeFunction.StartsWith("bilinear", StringComparison.OrdinalIgnoreCase))
                return 1;
            if (resizeFunction.StartsWith("point", StringComparison.OrdinalIgnoreCase))
                return 0;
            return 2;
        }
        public static double StdDev(IEnumerable<OverlayInfo> sample)
        {
            var mean = Mean(sample);
            return Math.Sqrt(sample.Sum(p => Math.Pow(p.Diff - mean, 2)));
        }

        public static double Mean(IEnumerable<OverlayInfo> sample)
        {
            return sample.Sum(p => p.Diff) / sample.Count();
        }

        public static bool CheckDev(IEnumerable<OverlayInfo> sample, double maxDiffIncrease, bool abs)
        {
            var mean = Mean(sample);
            return sample.All(p => (abs ? Math.Abs(p.Diff - mean) : p.Diff - mean) <= maxDiffIncrease);
        }

        public static Space AsSpace(this SizeF size) => new(size.Width, size.Height);
        public static Space AsSpace(this SizeD size) => new(size.Width, size.Height);
        public static Space AsSpace(this Size size) => size;
        public static Space AsSpace(this PointF point) => point;
        public static Space AsSpace(this Point point) => point;
        public static Space AsSpace(this RectangleF rect) => rect;
        public static Space AsSpace(this Rectangle rect) => rect;

        public static Space Median(this RectangleF rect) => new(rect.X + rect.Width/ 2, rect.Y + rect.Height / 2);

        public static Rectangle Scale(this Rectangle rect, double coef) =>
            new((int) Math.Round(rect.X * coef), 
                (int) Math.Round(rect.Y * coef), 
                (int) Math.Round(rect.Width * coef),
                (int) Math.Round(rect.Height * coef));

        public static Size Eval(this Size size, Func<int, int> eval) => new(eval(size.Width), eval(size.Height));
        public static SizeF Eval(this SizeF size, Func<float, float> eval) => new(eval(size.Width), eval(size.Height));

        public static float GetArea(this SizeF size) => size.Width * size.Height;

        public static int GetArea(this Size size) => size.Width * size.Height;

        public static Rectangle Floor(this RectangleF rect) => Rectangle.FromLTRB(
            (int) Math.Ceiling(Math.Round(rect.Left, OverlayConst.FRACTION)),
            (int) Math.Ceiling(Math.Round(rect.Top, OverlayConst.FRACTION)),
            (int) Math.Floor(Math.Round(rect.Right, OverlayConst.FRACTION)), 
            (int) Math.Floor(Math.Round(rect.Bottom, OverlayConst.FRACTION)));

        public static Size Fit(this Size size, Size other)
        {
            var ar = size.GetAspectRatio();
            var otherAr = other.GetAspectRatio();
            if (ar > otherAr)
            {
                var height = (int)Math.Round(other.Width / ar);
                return new Size(other.Width, height);
            }
            var width = (int)Math.Round(other.Height * ar);
            return new Size(width, other.Height);
        }

        public static Rectangle Inflate(this Rectangle rectangle, int coef) => new(
            rectangle.X - rectangle.Width * coef,
            rectangle.Y - rectangle.Height * coef,
            rectangle.Width * coef * 2,
            rectangle.Height * coef * 2
        );

        public static Rectangle Rotate(this Rectangle rectangle, int length) => length switch
        {
            0 => rectangle,
            1 => Rectangle.FromLTRB(rectangle.Bottom, rectangle.Left, rectangle.Top, rectangle.Right),
            2 => Rectangle.FromLTRB(rectangle.Right, rectangle.Bottom, rectangle.Left, rectangle.Top),
            3 => Rectangle.FromLTRB(rectangle.Top, rectangle.Right, rectangle.Bottom, rectangle.Left),
            _ => throw new ArgumentException("index")
        };

        public static T[] Shift<T>(this T[] array, int shift)
        {
            var result = new T[array.Length];
            Array.Copy(array, 0, result, shift, array.Length - shift);
            Array.Copy(array, array.Length - shift, result, 0, shift);
            return result;
        }

        public static FramePlane FillGradient(this FramePlane framePlane, int tl, int tr, int br, int bl, int rotate, bool noise, int seed)
        {
            var gradient = new[] { tl, tr, br, bl }.Shift(rotate);
            framePlane.FillGradient(gradient[0], gradient[1], gradient[2], gradient[3], noise, seed);
            return framePlane;
        }

        public static FramePlane Crop(this FramePlane framePlane, int left, int top, int right, int bottom, int rotate)
        {
            var gradient = new[] { left, top, right, bottom }.Shift(rotate);
            return framePlane.Crop(gradient[0], gradient[1], gradient[2], gradient[3]);
        }

        public static Size GetSize(this Clip clip)
        {
            var info = clip.GetVideoInfo();
            return info.GetSize();
        }

        public static Size GetSize(this VideoInfo info)
        {
            return new Size(info.width, info.height);
        }

        public static double GetAspectRatio(this Size size)
        {
            return (double)size.Width / size.Height;
        }

        public static float GetAspectRatio(this SizeF size)
        {
            return size.Width / size.Height;
        }

        public static Space CalcScale(this Size after, SizeD before) => after.AsSpace() / before.AsSpace();

        public static SizeD Scale(this Size size, Space scale) => scale * size;
    }
}
