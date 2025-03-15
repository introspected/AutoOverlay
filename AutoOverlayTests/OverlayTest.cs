using AutoOverlay.Overlay;
using AutoOverlay;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework.Internal;
using System.Drawing;
using System.Runtime.InteropServices;

namespace AutoOverlayTests
{
    [TestFixture]
    public class OverlayTest
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr LoadLibrary(string dllToLoad);

        [Test]
        public void TestOverlayWithoutRotation()
        {
            var srcSize = new Size(1920, 1080);
            var overSize = new Size(1920, 800);
            var data = new OverlayData
            {
                Source = new Rectangle(Point.Empty, srcSize),
                Overlay = new Rectangle(new Point(20,50), (Size)BilinearRotate.CalculateSize(1600, 666, 2)),
                SourceBaseSize = srcSize,
                OverlayBaseSize = overSize,
                OverlayAngle = 2,
                OverlayWarp = Warp.Empty,
                Coef = 1
            };
            var info = data.GetOverlayInfo();
            Assert.AreEqual(data.Overlay.Size, (Size)info.OverlayRectangle.Size);
            var input = new OverlayInput
            {
                SourceSize = srcSize,
                OverlaySize = overSize,
                TargetSize = srcSize,
                InnerBounds = RectangleD.Empty,
                OuterBounds = RectangleD.Empty,
                FixedSource = false,
                OverlayBalance = Space.Empty,
                ExtraClips = new List<ExtraClip>()
            };
            var data2 = OverlayMapper.For(input, info, new OverlayStabilization(0, 0, 0)).GetOverlayData();
            Assert.AreEqual(data, data2);
        }
    }
}
