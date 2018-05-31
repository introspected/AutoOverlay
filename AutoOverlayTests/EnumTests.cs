using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
            Assert.AreEqual(1, OverlayUtils.GetWidthSubsample(ColorSpaces.CS_YV24));
            Assert.AreEqual(2, OverlayUtils.GetWidthSubsample(ColorSpaces.CS_YV12));
            Assert.AreEqual(4, OverlayUtils.GetWidthSubsample(ColorSpaces.CS_YV411));
        }
    }
}
