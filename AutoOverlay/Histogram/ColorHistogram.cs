using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AutoOverlay.Overlay;
using AvsFilterNet;
using MathNet.Numerics.Interpolation;

namespace AutoOverlay.Histogram
{
    public class ColorHistogram
    {
        private readonly double[] values;
        private readonly double[] total;
        private readonly double offset;
        private readonly double step;
        private readonly double minColor;
        private readonly double maxColor;
        private readonly double? constant;
        private int pixelCount;

        public ColorHistogram(int length, PlaneChannel planeChannel, VideoFrame frame, PlaneChannel maskChannel, VideoFrame mask, bool limitedRange)
        {
            var depth = planeChannel.Depth;
            var levels = 1L << depth;
            if (length > levels)
                length = (int)levels;
            var range = length == levels ? new MinMax(depth) : new MinMax(planeChannel, frame);
            values = new double[length];
            total = new double[length];
            offset = range.Min;
            step = (range.Max - range.Min) / (length - 1d);
            if (step == 0)
            {
                constant = range.Min;
                return;
            }
            var framePlane = new FramePlane(planeChannel, frame, true);
            FramePlane? maskPlane = null;
            if (mask != null)
            {
                var plane = new FramePlane(maskChannel, mask, true);
                plane.pixelSize *= plane.width / framePlane.width;
                plane.stride *= plane.height / framePlane.height;
                maskPlane = plane;
            }
            pixelCount = NativeUtils.FillHistogram(framePlane, maskPlane, values, offset, step);
            var sum = 0d;
            for (var i = 0; i < values.Length; i++)
            {
                values[i] /= pixelCount;
                total[i] = sum += values[i];
            }
            if (planeChannel.Depth == 32)
            {
                minColor = float.MinValue;
                maxColor = float.MaxValue;
            }
            else
            {
                minColor = limitedRange ? 16 << (planeChannel.Depth - 8) : 0;
                maxColor = limitedRange ? 255 << (planeChannel.Depth - 8) : (1 << planeChannel.Depth) - 1;
            }
        }

        public ColorMapper Match(ColorHistogram reference, double intensity, double exclude)
        {
            var sampleInterpolator = GetColorInterpolator();
            var referenceInterpolator = reference.GetColorInterpolator();

            if (referenceInterpolator is ConstantInterpolator)
                return new ColorMapper(referenceInterpolator, minColor, maxColor);

            var map = new SortedDictionary<double, double>();
            for (var i = 0; i < values.Length; i++)
            {
                if (values[i] > exclude)
                {
                    var sampleColor = offset + i * step;

                    map[sampleColor] = referenceInterpolator.Interpolate(total[i]);
                }
            }
            for (var i = 0; i < reference.values.Length; i++)
            {
                if (reference.values[i] > exclude)
                {
                    map[sampleInterpolator.Interpolate(reference.total[i])] = reference.offset + i * reference.step;
                }
            }

            map[minColor] = reference.minColor;
            map[reference.maxColor] = reference.maxColor;
#if DEBUG
            foreach (var pair in map)
            {
                Debug.WriteLine($"{pair.Key} => {pair.Value}");
            }
#endif
            var interpolator = LinearSpline.Interpolate(map.Keys, map.Values);
            return new ColorMapper(interpolator, reference.minColor, reference.maxColor);
        }

        private IInterpolation GetColorInterpolator()
        {
            if (constant.HasValue)
                return new ConstantInterpolator(constant.Value);
            var weights = new List<double>(values.Length);
            var colors = new List<double>(values.Length);
            for (var i = 0; i < values.Length; i++)
            {
                if (values[i] > double.Epsilon)
                {
                    weights.Add(total[i]);
                    colors.Add(offset + i * step);
                }
            }
            if (colors.Count == 1)
                return new ConstantInterpolator(colors.First());
            return LinearSpline.Interpolate(weights, colors);
        }
    }
}
