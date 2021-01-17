using AutoOverlay;
using AutoOverlay.AviSynth;
using AutoOverlay.Filters;
using AvsFilterNet;
using NUnit.Framework;

namespace AutoOverlayTests
{
    [TestFixture]
    public class DynamicEnvironmentTest
    {
        [Test]
        public void TestSimpleCacheKey()
        {
            var a = new DynamicEnvironment.Key("Resize", new object[] { 1920, 1080 }, new string[] { null, null });
            var b = new DynamicEnvironment.Key("Resize", new object[] { 1920, 1080 }, new string[] { null, null });
            Assert.IsTrue(a.Equals(b));
        }

        [Test]
        public void TestComplexCacheKey()
        {
            var a = new DynamicEnvironment.Key("Warp", new object[] { new[] { 0.0, 0.0, 1.0, 1.5, 1000.0, 0.0, -1.0, -2.3 }, true }, new[] { null, "relative" });
            var b = new DynamicEnvironment.Key("Warp", new object[] { new[] { 0.0, 0.0, 1.0, 1.5, 1000.0, 0.0, -1.0, -2.3 }, true }, new[] { null, "relative" });
            Assert.IsTrue(a.Equals(b));
        }
    }
}
