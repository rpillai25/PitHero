using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero;
using PitHero.UI;

namespace PitHero.Tests.UI
{
    /// <summary>
    /// Layout guards for the wide, non-scrolling inventory grid: 24 columns x 5 bag rows keeps the
    /// old 120-slot capacity while fitting the configured design height.
    /// </summary>
    [TestClass]
    public class InventoryGridLayoutTests
    {
        [TestMethod]
        public void BagCapacity_MatchesTheOldTallLayout()
        {
            Assert.AreEqual(120, InventoryGrid.BagCapacity);
        }

        [TestMethod]
        public void Grid_SizesItselfToItsContent()
        {
            var grid = new InventoryGrid();

            Assert.AreEqual(InventoryGrid.ContentWidth, grid.GetWidth());
            Assert.AreEqual(InventoryGrid.ContentHeight, grid.GetHeight());
        }

        [TestMethod]
        public void GridHeight_FitsTheHeroWindowTabArea()
        {
            // The Party window spans the full design height (flush top and bottom) and the tab strip
            // comes off the top. The grid must fit what is left, since nothing scrolls.
            const float tabStripHeight = 37f;
            float tabArea = GameConfig.VirtualHeight - tabStripHeight;

            Assert.IsTrue(InventoryGrid.ContentHeight <= tabArea,
                $"grid is {InventoryGrid.ContentHeight}px but only {tabArea}px of tab area exists");
        }

        [TestMethod]
        public void GridWidth_FitsTheSecondChanceHeroPanel()
        {
            Assert.IsTrue(InventoryGrid.ContentWidth + 8f <= GameConfig.SecondChanceHeroPanelWidth,
                "the shop's hero panel must show every column");
        }

        [TestMethod]
        public void SecondChanceComposition_StaysOnTheReferenceStage()
        {
            Assert.AreEqual(GameConfig.VirtualWidth,
                GameConfig.SecondChanceHeroPanelX + GameConfig.SecondChanceHeroPanelWidth,
                "hero panel stays flush with the right edge of the 1920 reference stage");
            Assert.IsTrue(GameConfig.SecondChanceShopWindowX + GameConfig.SecondChanceShopWindowWidth
                          < GameConfig.SecondChanceHeroPanelX,
                "the two windows must not overlap each other");
        }

        [TestMethod]
        public void Merchant_IsCenteredBetweenTheShopAndHeroPanels()
        {
            const float spriteSize = 256f;
            float shopRight = GameConfig.SecondChanceShopWindowX + GameConfig.SecondChanceShopWindowWidth;
            float spriteCenter = GameConfig.SecondChanceMerchantSpriteX + spriteSize / 2f;
            float spanCenter = (shopRight + GameConfig.SecondChanceHeroPanelX) / 2f;

            // The sprite X is a whole pixel, so allow the half-pixel rounding
            Assert.IsTrue(System.Math.Abs(spriteCenter - spanCenter) <= 0.5f,
                $"merchant center {spriteCenter} should sit at the span center {spanCenter}");
        }
    }
}
