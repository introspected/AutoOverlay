using System;

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

        public double Min { get; set; } = double.MinValue;

        public double Max { get; set; } = double.MaxValue;

        public string[] Values { get; set; } = new string[0];
    }
}
