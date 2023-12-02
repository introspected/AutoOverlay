using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoOverlay.AviSynth;
using AvsFilterNet;

namespace AutoOverlay.Overlay
{
    public record ExtraClip
    {
        public Clip Clip { get; set; }

        public Clip Mask { get; set; }

        public ExtraVideoInfo Info { get; set; }

        public double Opacity { get; set; }

        public bool Minor { get; set; }
    }
}
