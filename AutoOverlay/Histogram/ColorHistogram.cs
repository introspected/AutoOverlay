using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AutoOverlay.Overlay;
using AvsFilterNet;

namespace AutoOverlay.Histogram
{
    public class ColorHistogram
    {
        public int Length { get; }
        public double[] Values { get; }
        public double[] Total { get; }
        public long PixelCount { get; }
        public MinMax Range { get; set; }
        private readonly double offset;
        private readonly double step;
        private readonly double depthMinColor;
        private readonly double depthMaxColor;
        private readonly double rangeMinColor;
        private readonly double rangeMaxColor;
        private readonly double? constant;
        private readonly int depth;

        public static ColorHistogram Compose(params ColorHistogram[] histograms)
        {
            if (histograms.Length == 0)
                throw new AvisynthException("Empty histograms");
            if (histograms.Length == 1)
                return histograms.First();
            return new ColorHistogram(histograms);
        }

        public static Dictionary<Corner, ColorHistogram> Gradient(
            int length, PlaneChannel planeChannel, VideoFrame frame, PlaneChannel maskChannel, VideoFrame mask, bool limitedRange)
        {
            var histograms = new Dictionary<Corner, ColorHistogram>();
            
            var framePlane = new FramePlane(planeChannel, frame, true);
            var levels = 1L << planeChannel.Depth;
            length = length > levels ? (int)levels : length;
            var range = length == levels ? new MinMax(planeChannel.Depth) : new MinMax(framePlane);

            var offset = range.Min;
            var step = (range.Max - range.Min) / (length - 1.0);

            FramePlane? maskPlane = null;
            if (mask != null)
            {
                var plane = new FramePlane(maskChannel, mask, true);
                plane.pixelSize *= plane.row / framePlane.row;
                plane.stride *= plane.height / framePlane.height;
                maskPlane = plane;
            }

            var tlValues = new double[length];
            var trValues = new double[length];
            var brValues = new double[length];
            var blValues = new double[length];

            double? constant = null;
            var pixelCount = framePlane.width * framePlane.height;
            if (step == 0)
                constant = range.Min;
            else
                pixelCount = framePlane.FillGradientHistograms(maskPlane, tlValues, trValues, brValues, blValues, offset, step);

            histograms[Corner.TopLeft] = new ColorHistogram(length, planeChannel, limitedRange, offset, range, step, constant, tlValues, pixelCount);
            histograms[Corner.TopRight] = new ColorHistogram(length, planeChannel, limitedRange, offset, range, step, constant, trValues, pixelCount);
            histograms[Corner.BottomRight] = new ColorHistogram(length, planeChannel, limitedRange, offset, range, step, constant, brValues, pixelCount);
            histograms[Corner.BottomLeft] = new ColorHistogram(length, planeChannel, limitedRange, offset, range, step, constant, blValues, pixelCount);

            return histograms;
        }

        public ColorHistogram(
            int length, 
            PlaneChannel planeChannel, 
            VideoFrame frame, 
            PlaneChannel maskChannel, 
            VideoFrame mask, 
            bool limitedRange, 
            CornerGradient? gradient)
        {
            var framePlane = new FramePlane(planeChannel, frame, true);

            depth = planeChannel.Depth;
            var levels = 1L << depth;
            Length = length > levels ? (int)levels : length;
            Range = Length == levels ? new MinMax(depth) : new MinMax(framePlane);

            //if (framePlane.byteDepth == 4)
            //    System.Diagnostics.Debug.WriteLine($"Plane {planeChannel.EffectivePlane} Min {Range.Min} Max {Range.Max}");

            Values = new double[Length];
            Total = new double[Length];
            offset = Range.Min;
            step = (Range.Max - Range.Min) / (Length - 1d);
            if (step == 0)
            {
                constant = Range.Min;
                PixelCount = framePlane.width * framePlane.height;
                return;
            }
            FramePlane? maskPlane = null;
            if (mask != null)
            {
                var plane = new FramePlane(maskChannel, mask, true);
                plane.pixelSize *= plane.row / framePlane.row;
                plane.stride *= plane.height / framePlane.height;
                maskPlane = plane;
            }

            PixelCount = gradient.HasValue
                ? framePlane.FillGradientHistogram(gradient.Value, maskPlane, Values, offset, step)
                : framePlane.FillHistogram(maskPlane, Values, offset, step);

            double divider = PixelCount;

            if (gradient.HasValue)
                divider *= gradient.Value.topLeft + gradient.Value.topRight + gradient.Value.bottomRight + gradient.Value.bottomLeft - 4;
            var sum = 0d;
            for (var i = 0; i < Values.Length; i++)
            {
                Values[i] /= divider;
                Total[i] = sum += Values[i];
            }

            (depthMinColor, rangeMinColor, depthMaxColor, rangeMaxColor) = GetMinMaxColor(planeChannel.EffectivePlane, limitedRange);
        }

        private ColorHistogram(int length, PlaneChannel planeChannel, bool limitedRange, double offset, MinMax range,  double step, double? constant, double[] values, int pixelCount)
        {
            depth = planeChannel.Depth;
            var levels = 1L << depth;
            Length = length > levels ? (int)levels : length;
            Values = values;
            Total = new double[values.Length];
            PixelCount = pixelCount;
            Range = range;
            this.offset = offset;
            this.step = step;
            this.constant = constant;

            if (constant.HasValue)
                return;

            double sum = 0;
            for (var i = 0; i < Values.Length; i++)
            {
                Values[i] /= pixelCount;
                Total[i] = sum += Values[i];
            }

            (depthMinColor, rangeMinColor, depthMaxColor, rangeMaxColor) = GetMinMaxColor(planeChannel.EffectivePlane, limitedRange);
        }

        private ColorHistogram(ColorHistogram[] histograms)
        {
            var main = histograms.First();
            Length = main.Length;
            Values = new double[Length];
            Total = new double[Length];
            Range = new MinMax(histograms.Min(p => p.Range.Min), histograms.Max(p => p.Range.Max));

            PixelCount = histograms.Sum(p => p.PixelCount);

            offset = Range.Min;
            step = (Range.Max - Range.Min) / (Length - 1d);
            if (step == 0)
            {
                constant = Range.Min;
                return;
            }
            depthMinColor = main.depthMinColor;
            depthMaxColor = main.depthMaxColor;
            rangeMinColor = main.rangeMinColor;
            rangeMaxColor = main.rangeMaxColor;
            depth = main.depth;

            var histCount = histograms.Length;
            unsafe
            {
                fixed (double* values = Values)
                {
                    for (var j = 0; j < histCount; j++)
                    {
                        var histStep = histograms[j].step;
                        var histOffset = histograms[j].offset;
                        if (histograms[j].constant != null)
                        {
                            var histColor = histograms[j].constant.Value;
                            var color = (histColor - offset) / step;
                            var weight = 1 - (color - Math.Floor(color));
                            var index = (int)color;
                            if (weight <= double.Epsilon)
                            {
                                values[index] += 1;
                                continue;
                            }
                            values[index] += weight;
                            if (weight < 1)
                            {
                                weight = 1 - weight;
                                index++;
                                values[index] += weight;
                            }
                            continue;
                        }
                        fixed (double* histValues = histograms[j].Values)
                        {
                            for (var i = 0; i < Length; i++)
                            {
                                var histValue = histValues[i];
                                if (histValue == 0) continue;
                                var histColor = histOffset + histStep * i;
                                var color = (histColor - offset) / step;
                                var weight = 1 - (color - Math.Floor(color));
                                var index = (int)color;
                                values[index] += histValue * weight;
                                if (weight < 1)
                                {
                                    weight = 1 - weight;
                                    index++;
                                    values[index] += histValue * weight;
                                }
                            }
                        }
                    }

                    var sum = 0.0;
                    fixed (double* total = Total)
                        for (var i = 0; i < Length; i++)
                        {
                            total[i] = sum += values[i] /= histCount;
                        }
                }
            }
        }

        private (double depthMin, double rangeMin, double depthMax, double rangeMax) GetMinMaxColor(YUVPlanes plane, bool limitedRange)
        {
            if (depth == 32)
            {
                double min = Math.Min(-3, Range.Min), max = Math.Max(3, Range.Max);
                if (limitedRange)
                {
                    min = plane.IsChroma() ? -0.5 : 0;
                    max = plane.IsChroma() ? 0.5 : 1;
                }
                return (float.MinValue, min, float.MaxValue, max);
            }
            var depthMin = 0;
            var depthMax = (1 << depth) - 1;
            if (limitedRange && !plane.IsRgb())
            {
                var rangeMin = 16 << (depth - 8);
                var limit = plane.IsLuma() ? 235 : 240;
                var rangeMax = limit << (depth - 8);
                return (depthMin, rangeMin, depthMax, rangeMax);
            }
            return (depthMin, depthMin, depthMax, depthMax);
        }

        public Lut GetLut(ColorHistogram reference, double dither, double intensity, int exclude)
        {
            return new Lut(Values, reference.Values, dither, intensity);
        }

        public IInterpolator GetInterpolator(ColorHistogram reference, double intensity, int exclude)
        {
            var sampleInterpolator = GetColorInterpolator();
            var referenceInterpolator = reference.GetColorInterpolator();

            if (referenceInterpolator is ConstantInterpolator)
                return referenceInterpolator;

            var map = new SortedDictionary<double, double>();
            var sampleExclude = exclude / (double)PixelCount;
            var refExclude = exclude / (double)reference.PixelCount;
            unsafe
            {
                fixed (double* values = Values)
                fixed (double* total = Total)
                {
                    for (var i = 0; i < Length; i++)
                    {
                        if (values[i] > sampleExclude)
                        {
                            var sampleColor = offset + i * step;
                            var referenceColor = referenceInterpolator.Interpolate(total[i]);
                            map[sampleColor] = sampleColor * (1 - intensity) + intensity * referenceColor;
                        }
                    }
                }

                fixed (double* values = reference.Values)
                fixed (double* total = reference.Total)
                {
                    for (var i = 0; i < reference.Length; i++)
                    {
                        if (values[i] > refExclude)
                        {
                            var sampleColor = sampleInterpolator.Interpolate(total[i]);
                            var referenceColor = reference.offset + i * reference.step;
                            map[sampleColor] = sampleColor * (1 - intensity) + intensity * referenceColor;
                        }
                    }
                }
                if (sampleInterpolator is IDisposable disposable1)
                    disposable1.Dispose();
                if (referenceInterpolator is IDisposable disposable2)
                    disposable2.Dispose();
            }

            var isConstant = map.Values.Count == 1;

            if (!isConstant)
            {
                map[depthMinColor] = reference.depthMinColor;
                map[depthMaxColor] = reference.depthMaxColor;
                map[rangeMinColor] = reference.rangeMinColor;
                map[rangeMaxColor] = reference.rangeMaxColor;
            }
            return isConstant
                ? new ConstantInterpolator(map.Values.First())
                : new ManagedInterpolator(map.Keys.ToArray(), map.Values.ToArray());
        }

        private IInterpolator GetColorInterpolator()
        {
            if (constant.HasValue)
                return new ConstantInterpolator(constant.Value);
            var weights = new List<double>(Values.Length);
            var colors = new List<double>(Values.Length);
            for (var i = 0; i < Values.Length; i++)
            {
                if (Values[i] > double.Epsilon)
                {
                    weights.Add(Total[i] + i * 0.000000000000000001);
                    colors.Add(offset + i * step);
                }
            }
            if (colors.Count == 1)
                return new ConstantInterpolator(colors.First());

            return new ManagedInterpolator(weights.ToArray(), colors.ToArray());
        }
    }
}
