using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.UI;

namespace PitHero.Tests.UI
{
    /// <summary>
    /// Anchor guards for the graphical close button (issue #399): it hangs fully outside a window's
    /// left edge and flush against it, centered on the window, and never leaves the stage.
    /// </summary>
    [TestClass]
    public class WindowCloseButtonLayoutTests
    {
        private const float BtnW = 32f;
        private const float BtnH = 32f;

        [TestMethod]
        public void Anchor_SitsFlushAgainstTheLeftEdge()
        {
            var pos = WindowCloseButton.AnchorLeftOf(500f, 20f, 300f, BtnW, BtnH, 296f);

            // The button's right edge touches the window's left border - no gap
            Assert.AreEqual(500f, pos.X + BtnW);
        }

        [TestMethod]
        public void Anchor_CentersOnTheWindow()
        {
            var pos = WindowCloseButton.AnchorLeftOf(500f, 20f, 300f, BtnW, BtnH, 400f);

            // Button center matches the window center: 20 + 300/2 = 170
            Assert.AreEqual(170f, pos.Y + BtnH / 2f);
        }

        [TestMethod]
        public void Anchor_ClampsToTheStageWhenTheWindowIsFlushLeft()
        {
            var pos = WindowCloseButton.AnchorLeftOf(0f, 0f, 200f, BtnW, BtnH, 296f);

            Assert.AreEqual(0f, pos.X);
        }

        [TestMethod]
        public void Anchor_ClampsYWhenTheWindowOverflowsTheStage()
        {
            // Window taller than the stage: centering would push the button off the bottom
            var pos = WindowCloseButton.AnchorLeftOf(500f, 0f, 600f, BtnW, BtnH, 296f);

            Assert.IsTrue(pos.Y >= 0f, "button must not sit above the stage");
            Assert.IsTrue(pos.Y + BtnH <= 296f, "button must not sit below the stage");
        }
    }
}
