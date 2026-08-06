using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PitHero.Tests
{
    /// <summary>Tests for WindowManager.ClampRectToBounds (pure math used by free-move window mode, issue #364)</summary>
    [TestClass]
    public class WindowClampTests
    {
        [TestMethod]
        public void FullWidthWindow_XPinnedToBoundsOrigin()
        {
            // Window as wide as the display: horizontal movement is impossible
            int x = 500, y = 300;
            WindowManager.ClampRectToBounds(ref x, ref y, 1920, 360, 0, 0, 1920, 1080);

            Assert.AreEqual(0, x);
            Assert.AreEqual(300, y);
        }

        [TestMethod]
        public void FullWidthWindow_YClampsAtTopAndBottom()
        {
            int x = 0, y = -50;
            WindowManager.ClampRectToBounds(ref x, ref y, 1920, 360, 0, 0, 1920, 1080);
            Assert.AreEqual(0, y);

            y = 2000;
            WindowManager.ClampRectToBounds(ref x, ref y, 1920, 360, 0, 0, 1920, 1080);
            Assert.AreEqual(1080 - 360, y);
        }

        [TestMethod]
        public void HalfSizeWindow_InteriorPositionUnchanged()
        {
            int x = 400, y = 200;
            WindowManager.ClampRectToBounds(ref x, ref y, 960, 180, 0, 0, 1920, 1080);

            Assert.AreEqual(400, x);
            Assert.AreEqual(200, y);
        }

        [TestMethod]
        public void HalfSizeWindow_ClampsOnAllFourEdges()
        {
            int x = -100, y = -100;
            WindowManager.ClampRectToBounds(ref x, ref y, 960, 180, 0, 0, 1920, 1080);
            Assert.AreEqual(0, x);
            Assert.AreEqual(0, y);

            x = 5000; y = 5000;
            WindowManager.ClampRectToBounds(ref x, ref y, 960, 180, 0, 0, 1920, 1080);
            Assert.AreEqual(1920 - 960, x);
            Assert.AreEqual(1080 - 180, y);
        }

        [TestMethod]
        public void NegativeOriginMonitor_ClampsToNegativeCoordinates()
        {
            // Secondary monitor left of primary: bounds origin at (-2560, 0)
            int x = -9999, y = 500;
            WindowManager.ClampRectToBounds(ref x, ref y, 1280, 360, -2560, 0, 2560, 1440);
            Assert.AreEqual(-2560, x);
            Assert.AreEqual(500, y);

            x = 100; // past the right edge of the negative-origin monitor (max is -2560 + 2560 - 1280 = -1280)
            WindowManager.ClampRectToBounds(ref x, ref y, 1280, 360, -2560, 0, 2560, 1440);
            Assert.AreEqual(-1280, x);
        }

        [TestMethod]
        public void WindowLargerThanBoundsAxis_PinsToBoundsOrigin()
        {
            int x = 300, y = 300;
            WindowManager.ClampRectToBounds(ref x, ref y, 2500, 1500, 0, 0, 1920, 1080);

            Assert.AreEqual(0, x);
            Assert.AreEqual(0, y);
        }
    }
}
