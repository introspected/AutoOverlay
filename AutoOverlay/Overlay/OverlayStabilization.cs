using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoOverlay.Overlay
{
    public record struct OverlayStabilization(int Length, double DiffTolerance, double AreaTolerance);
}
