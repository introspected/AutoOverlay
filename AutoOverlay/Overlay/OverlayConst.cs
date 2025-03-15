﻿using AvsFilterNet;
using System;
using System.Drawing;

namespace AutoOverlay
{
    public class OverlayConst
    {
        public const string DEFAULT_PRESIZE_FUNCTION = "BilinearResize";
        public const string DEFAULT_RESIZE_FUNCTION = "Spline16Resize";
        public const string DEFAULT_ROTATE_FUNCTION = "BilinearRotate";
        public const MtMode DEFAULT_MT_MODE = MtMode.SERIALIZED;
        public const int OVERLAY_FORMAT_VERSION = 6;
        public const int ENGINE_HISTORY_LENGTH = 20;
        public const int ENGINE_TOTAL_FRAMES = ENGINE_HISTORY_LENGTH * 2 + 1;

        public const int FRACTION = 7;
        public static readonly double EPSILON = Math.Pow(10, -FRACTION);

        public static readonly Size NO_SUB_SAMPLE = new(1, 1);

        public const int COLOR_BUCKETS_COUNT = 2000;
        public const double COLOR_DITHER = 0.95;
    }
}
