using System.Collections.Generic;
using System.Linq;
using AutoOverlay;
using AutoOverlay.Filters;
using AutoOverlay.Overlay;
using AvsFilterNet;
using NUnit.Framework;

namespace AutoOverlayTests
{
    [TestFixture]
    public class WarpTest
    {
        [Test]
        public void TestEquality()
        {
            var a = new Warp(1)
            {
                [0] = new RectangleD(0, 0, 0, 0)
            };
            var b = (Warp) a.Clone();
            b[0] = new RectangleD(0, 0, -0.15, -0.15); 
            Assert.AreEqual(a, a);
            Assert.AreEqual(b, b);
            Assert.AreNotEqual(a, b);
            Assert.AreNotEqual(a.GetHashCode(), b.GetHashCode());

            var warps = new HashSet<Warp> { a, b };
            Assert.AreNotEqual(warps.First(), warps.Last());
        }

        [Test]
        public void TestParseSuccess()
        {
            var warp = new Warp(2)
            {
                [0] = new RectangleD(0, 0, -3, 0),
                [1] = new RectangleD(1000, 500, 3, 4.4)
            };
            Assert.IsTrue(Warp.TryParse(warp.ToString(), out var parsed));
            Assert.AreEqual(warp, parsed);
        }

        [Test]
        public void TestParseFail()
        {
            Assert.IsFalse(Warp.TryParse("1,2 [3,3], 3,1 [1,2]", out var parsed));
        }
    }
}
