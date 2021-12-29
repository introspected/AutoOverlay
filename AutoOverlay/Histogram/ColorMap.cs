﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace AutoOverlay.Histogram
{
    public class ColorMap
    {
        public int[] FixedMap { get; }
        public Dictionary<int, double>[] DynamicMap { get; }
        private readonly double limit;
        private bool ditherAnyway;
        private bool fastDither;

        public ColorMap(int bits, int seed, double limit)
        {
            var depth = 1 << bits;
            FixedMap = new int[depth];
            DynamicMap = new Dictionary<int, double>[depth];
            for (var i = 0; i < DynamicMap.Length; i++)
            {
                FixedMap[i] = -1;
                DynamicMap[i] = new Dictionary<int, double>();
            }
            this.limit = limit;
            fastDither = limit > 0.5;
            ditherAnyway = limit > 1 - double.Epsilon;
        }

        public double Average(int color)
        {
            var fixedColor = FixedMap[color];
            if (fixedColor >= 0)
                return fixedColor;
            var map = DynamicMap[color];
            if (!map.Any())
                return -1;
            return map.Sum(p => p.Key * p.Value);
        }

        public int First()
        {
            return Enumerable.Range(0, FixedMap.Length).TakeWhile(p => !Contains(p)).Count();
        }

        public int Last()
        {
            return FixedMap.Length - Enumerable.Range(0, FixedMap.Length).Reverse().TakeWhile(p => !Contains(p)).Count() - 1;
        }

        public bool Contains(int color)
        {
            return FixedMap[color] >= 0 || DynamicMap[color].Any();
        }

        public void AddReal(int oldColor, double newColor, double weight = 1)
        {
            var integerColor = Math.Truncate(newColor);
            var val = 1 - (newColor - integerColor);
            Add(oldColor, (int) integerColor, val * weight);
            if (val <= 1 - double.Epsilon)
                Add(oldColor, (int) integerColor + 1, (1 - val) * weight);
        }

        public void Add(int oldColor, int newColor, double weight)
        {
            if (fastDither && weight >= limit)
            {
                FixedMap[oldColor] = newColor;
                return;
            }
            if (FixedMap[oldColor] >= 0)
                return;
            var map = DynamicMap[oldColor];
            if (map.ContainsKey(newColor))
                map[newColor] = map[newColor] + weight;
            else map[newColor] = weight;
            if (!ditherAnyway)
            {
                var max = map.Max(p => p.Value);
                var rest = 1 - map.Values.Sum();
                if (rest < max && max > limit)
                    FixedMap[oldColor] = newColor;
            }
        }

        public Tuple<int[][], double[][]> GetColorsAndWeights()
        {
            var length = DynamicMap.Length;
            var colorMap = new int[length][];
            var weightMap = new double[length][];
            for (var color = 0; color < length; color++)
            {
                if (FixedMap[color] >= 0) continue;
                var map = DynamicMap[color];
                var colors = colorMap[color] = new int[map.Count];
                var weights = weightMap[color] = new double[map.Count];
                var i = 0;
                foreach (var pair in map)
                {
                    colors[i] = pair.Key;
                    weights[i++] = pair.Value + double.Epsilon;
                }
            }
            return Tuple.Create(colorMap, weightMap);
        }
    }
}
