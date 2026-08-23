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
            float tabArea = GameConfig.VirtualHeight - GameConfig.TabStripHeight;

            Assert.IsTrue(InventoryGrid.ContentHeight <= tabArea,
                $"grid is {InventoryGrid.ContentHeight}px but only {tabArea}px of tab area exists");
        }

        [TestMethod]
        public void NameRow_FitsTwoLinesOfText()
        {
            // Equip-slot names stack first name over last name, so the row above the slots has to
            // hold two lines of Express (GameConfig.FontMainUI, line height 9).
            const float expressLineHeight = 9f;

            Assert.IsTrue(InventoryGrid.NameRowHeight >= expressLineHeight * 2f,
                $"name row is {InventoryGrid.NameRowHeight}px, too short for two {expressLineHeight}px lines");
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
            // The sprite's own width drives its X at runtime; all this constant has to be is the
            // midpoint of the gap between the two windows.
            float shopRight = GameConfig.SecondChanceShopWindowX + GameConfig.SecondChanceShopWindowWidth;
            float spanCenter = (shopRight + GameConfig.SecondChanceHeroPanelX) / 2f;

            Assert.AreEqual(spanCenter, GameConfig.SecondChanceMerchantSpriteCenterX, 0.001f,
                "the merchant is centered on the span between the shop and hero panels");
        }
    }
}
