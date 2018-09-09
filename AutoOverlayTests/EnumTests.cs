using AutoOverlay;
using AvsFilterNet;
using NUnit.Framework;

namespace AutoOverlayTests
{
    [TestFixture]
    public class EnumTests
    {
        [Test]
        public void TestChromaSubsampling()
        {
            Assert.AreEqual(1, ColorSpaces.CS_YV24.GetWidthSubsample());
            Assert.AreEqual(2, ColorSpaces.CS_YV12.GetWidthSubsample());
            Assert.AreEqual(4, ColorSpaces.CS_YV411.GetWidthSubsample());
        }
    }
}
