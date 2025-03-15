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
        public MinMax Range { get; set; }
        private readonly double offset;
        private readonly double step;
        private readonly double lowestColor;
        private readonly double highestColor;
        private readonly double minColor;
        private readonly double maxColor;
        private readonly double? constant;
        private readonly int depth;

        public static ColorHistogram Compose(bool forceMain, params ColorHistogram[] histograms)
        {
            if (histograms.Length == 0)
                throw new AvisynthException("Empty histograms");
            if (histograms.Length == 1)
                return histograms.First();
            return new ColorHistogram(histograms, forceMain);
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
            Values = new double[Length];
            Total = new double[Length];
            offset = Range.Min;
            step = (Range.Max - Range.Min) / (Length - 1d);
            if (step == 0)
            {
                constant = Range.Min;
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

            var pixelCount = gradient.HasValue
                ? framePlane.FillGradientHistogram(gradient.Value, maskPlane, Values, offset, step)
                : framePlane.FillHistogram(maskPlane, Values, offset, step);

            double divider = pixelCount;

            if (gradient.HasValue)
                divider *= gradient.Value.topLeft + gradient.Value.topRight + gradient.Value.bottomRight + gradient.Value.bottomLeft - 4;
            var sum = 0d;
            for (var i = 0; i < Values.Length; i++)
            {
                Values[i] /= divider;
                Total[i] = sum += Values[i];
            }

            (lowestColor, minColor, highestColor, maxColor) = GetMinMaxColor(planeChannel.EffectivePlane, limitedRange);
        }

        private ColorHistogram(int length, PlaneChannel planeChannel, bool limitedRange, double offset, MinMax range,  double step, double? constant, double[] values, int pixelCount)
        {
            depth = planeChannel.Depth;
            var levels = 1L << depth;
            Length = length > levels ? (int)levels : length;
            Values = values;
            Total = new double[values.Length];
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

            (lowestColor, minColor, highestColor, maxColor) = GetMinMaxColor(planeChannel.EffectivePlane, limitedRange);
        }

        private (double depthMin, double depthMax, double planeMin, double planeMax) GetMinMaxColor(YUVPlanes plane, bool limitedRange)
        {
            if (depth == 32)
            {
                var min = plane.IsChroma() ? -0.5 : 0;
                var max = plane.IsChroma() ? 0.5 : 1;
                return (min, float.MinValue, max, float.MaxValue);
            }
            var depthMin = 0;
            var depthMax = (1 << depth) - 1;
            if (limitedRange && !plane.IsRgb())
            {
                var planeMin = 16 << (depth - 8);
                var limit = plane.IsLuma() ? 235 : 240;
                var planeMax = limit << (depth - 8);
                return (depthMin, planeMin, depthMax, planeMax);
            }
            return (depthMin, depthMin, depthMax, depthMax);
        }

        private ColorHistogram(ColorHistogram[] histograms, bool forceMain)
        {
            var main = histograms.First();
            Length = main.Length;
            Values = new double[Length];
            Total = new double[Length];
            Range = new MinMax(histograms.Min(p => p.Range.Min), histograms.Max(p => p.Range.Max));

            offset = Range.Min;
            step = (Range.Max - Range.Min) / (Length - 1d);
            if (step == 0)
            {
                constant = Range.Min;
                return;
            }
            lowestColor = main.lowestColor;
            highestColor = main.highestColor;
            minColor = main.minColor;
            maxColor = main.maxColor;
            depth = main.depth;

            var histCount = histograms.Length;
            unsafe
            {
                fixed (double* values = Values)
                fixed (double* total = Total)
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
                                total[index] += 1;
                                continue;
                            }
                            values[index] += weight;
                            total[index] += weight;
                            if (weight < 1)
                            {
                                weight = 1 - weight;
                                index++;
                                values[index] += weight;
                                total[index] += weight;
                            }
                            continue;
                        }
                        fixed (double* histValues = histograms[j].Values)
                        fixed (double* histTotal = histograms[j].Total)
                        {
                            for (var i = 0; i < Length; i++)
                            {
                                var histColor = histOffset + histStep * i;
                                var color = (histColor - offset) / step;
                                //var round = Math.Round(color);
                                //if (color.IsNearlyEquals(round))
                                //{
                                //    var roundIndex = (int)round;
                                //    values[roundIndex] += histValues[i];
                                //    total[roundIndex] += histTotal[i];
                                //    continue;
                                //}
                                var weight = 1 - (color - Math.Floor(color));
                                var index = (int)color;
                                values[index] += histValues[i] * weight;
                                total[index] += histTotal[i] * weight;
                                if (weight < 1)
                                {
                                    weight = 1 - weight;
                                    index++;
                                    values[index] += histValues[i] * weight;
                                    total[index] += histTotal[i] * weight;
                                }
                            }
                        }
                    }

                    var sum = 0.0;
                    for (var i = 0; i < Length; i++)
                    {
                        total[i] = sum += values[i] /= histCount;
                        //total[i] /= histCount;
                    }
                }
            }

            if (forceMain && histograms.Length > 1)
            {
                var min = Values.TakeWhile(p => p == 0).Count();
                var max = Values.Length - Values.Reverse().TakeWhile(p => p == 0).Count() - 1;
                var added = 0.0;
                var forced = (double[]) main.Values.Clone();
                for (var i = 0; i < min; i++)
                    added += forced[i] = Values[i];
                for (var i = max + 1; i < Values.Length; i++)
                    added += forced[i] = Values[i];
                var coef = 1 - added;
                for (var i = min; i <= max; i++)
                    forced[i] *= coef;
                Values = forced;
            }
        }

        public Lut GetLut(ColorHistogram reference, double dither, double intensity, double exclude)
        {
            return new Lut(Values, reference.Values, dither, intensity, exclude);
        }

        public IInterpolator GetInterpolator(ColorHistogram reference, double intensity, double exclude)
        {
            var sampleInterpolator = GetColorInterpolator();
            var referenceInterpolator = reference.GetColorInterpolator();

            if (referenceInterpolator is ConstantInterpolator)
                return referenceInterpolator;

            var map = new SortedDictionary<double, double>();
            unsafe
            {
                fixed (double* values = Values)
                fixed (double* total = Total)
                {
                    for (var i = 0; i < Length; i++)
                    {
                        if (values[i] > exclude)
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
                        if (values[i] > exclude)
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
                // TODO don't overwrite
                map[lowestColor] = reference.lowestColor;
                map[highestColor] = reference.highestColor;
                map[minColor] = reference.minColor;
                map[maxColor] = reference.maxColor;
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
