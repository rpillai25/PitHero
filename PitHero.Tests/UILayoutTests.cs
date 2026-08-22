using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero;
using PitHero.UI;

namespace PitHero.Tests
{
    /// <summary>
    /// Tests for the pure layout math behind the configurable design height
    /// (WindowManager.GetStripHeight and UILayout). Windows themselves need a Skin, so only the
    /// math is covered here.
    /// </summary>
    [TestClass]
    public class UILayoutTests
    {
        [TestMethod]
        public void GetStripHeight_IsOneToOneAtReferenceDisplayHeight()
        {
            Assert.AreEqual(GameConfig.VirtualHeight, WindowManager.GetStripHeight(GameConfig.ReferenceDisplayHeight));
        }

        [TestMethod]
        public void GetStripHeight_DoublesOnA4KMonitor()
        {
            Assert.AreEqual(GameConfig.VirtualHeight * 2, WindowManager.GetStripHeight(2 * GameConfig.ReferenceDisplayHeight));
        }

        [TestMethod]
        public void GetStripHeight_ScalesProportionallyOnA1440pMonitor()
        {
            int expected = 1440 * GameConfig.VirtualHeight / GameConfig.ReferenceDisplayHeight;
            Assert.AreEqual(expected, WindowManager.GetStripHeight(1440));
        }

        [TestMethod]
        public void FitHeight_KeepsPreferredHeightWhenItFits()
        {
            Assert.AreEqual(200f, UILayout.FitHeight(200f, 360f, 6f, 4f));
        }

        [TestMethod]
        public void FitHeight_ShrinksToTheAvailableBudget()
        {
            // 296 tall stage, window starts at y=6 and must end 4px above the bottom
            Assert.AreEqual(286f, UILayout.FitHeight(350f, 296f, 6f, 4f));
        }

        [TestMethod]
        public void FitHeight_NeverGoesBelowTheFloor()
        {
            Assert.AreEqual(UILayout.MinWindowHeight, UILayout.FitHeight(350f, 40f, 30f, 20f));
        }

        [TestMethod]
        public void ClampY_LeavesAWindowThatAlreadyFits()
        {
            Assert.AreEqual(50f, UILayout.ClampY(50f, 100f, 296f));
        }

        [TestMethod]
        public void ClampY_PushesAWindowUpOffTheBottomEdge()
        {
            Assert.AreEqual(196f, UILayout.ClampY(250f, 100f, 296f));
        }

        [TestMethod]
        public void ClampY_KeepsTheTitleBarWhenTheWindowIsTallerThanTheStage()
        {
            Assert.AreEqual(0f, UILayout.ClampY(10f, 400f, 296f));
        }

        [TestMethod]
        public void CenterY_CentersWithoutBias()
        {
            Assert.AreEqual(98f, UILayout.CenterY(100f, 296f, 0f));
        }

        [TestMethod]
        public void CenterY_AppliesTheUpwardBias()
        {
            Assert.AreEqual(68f, UILayout.CenterY(100f, 296f, 30f));
        }

        [TestMethod]
        public void CenterY_ClampsWhenTheBiasWouldPushThePanelOffTheTop()
        {
            Assert.AreEqual(0f, UILayout.CenterY(280f, 296f, 30f));
        }

        [TestMethod]
        public void SecondChanceHeroPanel_FitsTheConfiguredDesignHeight()
        {
            float h = UILayout.FitHeight(GameConfig.SecondChanceHeroPanelHeight, GameConfig.VirtualHeight,
                GameConfig.UIStageMargin, GameConfig.UIStageMargin);
            Assert.IsTrue(h + 2f * GameConfig.UIStageMargin <= GameConfig.VirtualHeight,
                "Fitted hero panel must leave both stage margins intact");
        }
    }
}
