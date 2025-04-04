﻿using System;

namespace AutoOverlay.AviSynth
{
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class AvsArgumentAttribute : Attribute
    {
        public AvsArgumentAttribute() { }

        public AvsArgumentAttribute(params string[] values)
        {
            Values = values;
        }

        public bool Required { get; set; }

        public bool NotNull { get; set; }

        public double Min { get; set; } = double.MinValue;

        public double Max { get; set; } = double.MaxValue;

        public string[] Values { get; set; } = [];

        public bool LTRB { get; set; } = true;

        public bool Unused { get; set; }

        public bool Percent { get; set; }
    }
}
