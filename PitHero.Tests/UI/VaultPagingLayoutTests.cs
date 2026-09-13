using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero;
using PitHero.Services;
using PitHero.UI;

namespace PitHero.Tests.UI
{
    /// <summary>
    /// The vault model pages its stacks by SlotsPerPage and the shop grids draw one page each; the
    /// two must agree, and the page must fit the shop window at the configured design height.
    /// </summary>
    [TestClass]
    public class VaultPagingLayoutTests
    {
        [TestMethod]
        public void SlotsPerPage_MatchesTheGrids()
        {
            Assert.AreEqual(VaultItemGrid.MaxVisible, SecondChanceMerchantVault.SlotsPerPage);
            Assert.AreEqual(VaultCrystalGrid.MaxVisible, SecondChanceMerchantVault.SlotsPerPage);
        }

        [TestMethod]
        public void Cap_StaysAt540Stacks()
        {
            Assert.AreEqual(540, SecondChanceMerchantVault.MaxStacks);
            Assert.AreEqual(SecondChanceMerchantVault.MaxPages * SecondChanceMerchantVault.SlotsPerPage,
                SecondChanceMerchantVault.MaxStacks);
        }

        [TestMethod]
        public void ItemsPage_FitsTheFittedShopWindow()
        {
            // Tab strip + content pad + grid + pager pad + pager + content pad, as laid out by
            // SecondChanceShopUI.PopulateItemsTab, must fit the shop window once it is fitted to
            // the stage (UILayout.FitHeight with the stage margins on both sides).
            float shopH = UILayout.FitHeight(GameConfig.SecondChanceShopWindowHeight, GameConfig.VirtualHeight,
                GameConfig.UIStageMargin, GameConfig.UIStageMargin);
            const float contentPad = 8f;
            const float pagerPadTop = 10f;
            const float pagerHeight = 24f;
            float needed = GameConfig.TabStripHeight + contentPad + VaultItemGrid.ContentHeight
                           + pagerPadTop + pagerHeight + contentPad;

            Assert.IsTrue(needed <= shopH, $"the items page needs {needed}px but the shop window is {shopH}px tall");
        }
    }
}
