using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AutoOverlay
{
    public interface IOverlayStat : IDisposable
    {
        IEnumerable<OverlayInfo> Frames { get; }
        OverlayInfo this[int frameNumber] { get; set; }
        void Save(params OverlayInfo[] infoList);
    }
}
