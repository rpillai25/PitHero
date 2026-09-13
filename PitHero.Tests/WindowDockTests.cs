using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PitHero.Tests
{
    /// <summary>
    /// Tests for WindowManager.DockedY — the pure math that anchors a docked strip inside the
    /// display's usable (taskbar-free) area, shared by docking, shrink/restore and monitor swap.
    /// </summary>
    [TestClass]
    public class WindowDockTests
    {
        // 1080p monitor with a 48px Windows taskbar at the bottom: usable area is (0,0,1920,1032)
        private const int UsableY = 0;
        private const int UsableH = 1032;
        private const int Strip = 264;

        [TestMethod]
        public void Bottom_SitsOnTopOfTheTaskbar()
        {
            Assert.AreEqual(1032 - 264, WindowManager.DockedY(WindowManager.DockMode.Bottom, UsableY, UsableH, Strip, 0));
        }

        [TestMethod]
        public void Bottom_HonorsTheFineTuningOffset()
        {
            Assert.AreEqual(1032 - 264 - 10, WindowManager.DockedY(WindowManager.DockMode.Bottom, UsableY, UsableH, Strip, -10));
        }

        [TestMethod]
        public void None_DocksBottomLikeTheStartupStrip()
        {
            Assert.AreEqual(1032 - 264, WindowManager.DockedY(WindowManager.DockMode.None, UsableY, UsableH, Strip, 0));
        }

        [TestMethod]
        public void Top_StartsBelowATopTaskbar()
        {
            // Taskbar moved to the top edge: usable area starts at y=48
            Assert.AreEqual(48, WindowManager.DockedY(WindowManager.DockMode.Top, 48, 1032, Strip, 0));
            Assert.AreEqual(48 + 6, WindowManager.DockedY(WindowManager.DockMode.Top, 48, 1032, Strip, 6));
        }

        [TestMethod]
        public void Center_CentersInsideTheUsableArea()
        {
            Assert.AreEqual((1032 - 264) / 2, WindowManager.DockedY(WindowManager.DockMode.Center, UsableY, UsableH, Strip, 0));
        }

        [TestMethod]
        public void NegativeOriginMonitor_UsesTheDisplayOrigin()
        {
            // Secondary monitor above the primary: usable area (0,-1440,2560,1400) with a 40px taskbar
            Assert.AreEqual(-1440 + 1400 - 264, WindowManager.DockedY(WindowManager.DockMode.Bottom, -1440, 1400, Strip, 0));
            Assert.AreEqual(-1440, WindowManager.DockedY(WindowManager.DockMode.Top, -1440, 1400, Strip, 0));
        }

        [TestMethod]
        public void FullBoundsFallback_MatchesTheOldBehavior()
        {
            // When SDL can't report a usable area the full display bounds are used: old y = h - strip
            Assert.AreEqual(1080 - 264, WindowManager.DockedY(WindowManager.DockMode.Bottom, 0, 1080, Strip, 0));
        }
    }
}
