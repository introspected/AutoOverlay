using System.Drawing;
using AutoOverlay;
using NUnit.Framework;

namespace AutoOverlayTests
{
    [TestFixture]
    public class OverlayInfoTest
    {
        [Test]
        public void TestNearlyEquals()
        {
            var info1 = new OverlayInfo
            {
                Width = 1920,
                Height = 1080,
                X = 0,
                Y = 0
            };
            var info2 = new OverlayInfo
            {
                Width = 1920,
                Height = 1080,
                X = 2,
                Y = 2
            };
            Assert.True(info1.NearlyEquals(info2, new Size(1920, 1080), 0.01));
        }

        [Test]
        public void TestShrink()
        {
            var info = new OverlayInfo
            {
                Width = 1940,
                Height = 808,
                X = -15
            };
            var shrinked = info.Shrink(new Size(1920, 1080), new Size(1280, 534));
            var target = new OverlayInfo
            {
                Width = 1920,
                Height = 808,
                CropLeft = 98969,
                CropRight = 32990
            };
            Assert.AreEqual(target, shrinked);
        }
    }
}
