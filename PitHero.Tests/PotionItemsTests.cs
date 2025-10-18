using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Stats;

namespace PitHero.Tests
{
    [TestClass]
    public class PotionItemsTests
    {
        [TestMethod]
        public void PotionItems_NormalPotions_HaveCorrectProperties()
        {
            var hpPotion = PotionItems.HPPotion();
            Assert.AreEqual("HPPotion", hpPotion.Name);
            Assert.AreEqual(ItemRarity.Normal, hpPotion.Rarity);
            Assert.AreEqual(100, hpPotion.HPRestoreAmount);
            Assert.AreEqual(0, hpPotion.MPRestoreAmount);

            var apPotion = PotionItems.MPPotion();
            Assert.AreEqual("APPotion", apPotion.Name);
            Assert.AreEqual(ItemRarity.Normal, apPotion.Rarity);
            Assert.AreEqual(0, apPotion.HPRestoreAmount);
            Assert.AreEqual(100, apPotion.MPRestoreAmount);

            var mixPotion = PotionItems.MixPotion();
            Assert.AreEqual("MixPotion", mixPotion.Name);
            Assert.AreEqual(ItemRarity.Normal, mixPotion.Rarity);
            Assert.AreEqual(100, mixPotion.HPRestoreAmount);
            Assert.AreEqual(100, mixPotion.MPRestoreAmount);
        }

        [TestMethod]
        public void PotionItems_MidPotions_HaveCorrectProperties()
        {
            var midHpPotion = PotionItems.MidHPPotion();
            Assert.AreEqual("MidHPPotion", midHpPotion.Name);
            Assert.AreEqual(ItemRarity.Rare, midHpPotion.Rarity);
            Assert.AreEqual(500, midHpPotion.HPRestoreAmount);
            Assert.AreEqual(0, midHpPotion.MPRestoreAmount);

            var midApPotion = PotionItems.MidMPPotion();
            Assert.AreEqual("MidAPPotion", midApPotion.Name);
            Assert.AreEqual(ItemRarity.Rare, midApPotion.Rarity);
            Assert.AreEqual(0, midApPotion.HPRestoreAmount);
            Assert.AreEqual(500, midApPotion.MPRestoreAmount);

            var midMixPotion = PotionItems.MidMixPotion();
            Assert.AreEqual("MidMixPotion", midMixPotion.Name);
            Assert.AreEqual(ItemRarity.Rare, midMixPotion.Rarity);
            Assert.AreEqual(500, midMixPotion.HPRestoreAmount);
            Assert.AreEqual(500, midMixPotion.MPRestoreAmount);
        }

        [TestMethod]
        public void PotionItems_FullPotions_HaveCorrectProperties()
        {
            var fullHpPotion = PotionItems.FullHPPotion();
            Assert.AreEqual("FullHPPotion", fullHpPotion.Name);
            Assert.AreEqual(ItemRarity.Epic, fullHpPotion.Rarity);
            Assert.AreEqual(-1, fullHpPotion.HPRestoreAmount); // -1 indicates full restore
            Assert.AreEqual(0, fullHpPotion.MPRestoreAmount);

            var fullApPotion = PotionItems.FullMPPotion();
            Assert.AreEqual("FullAPPotion", fullApPotion.Name);
            Assert.AreEqual(ItemRarity.Epic, fullApPotion.Rarity);
            Assert.AreEqual(0, fullApPotion.HPRestoreAmount);
            Assert.AreEqual(-1, fullApPotion.MPRestoreAmount); // -1 indicates full restore

            var fullMixPotion = PotionItems.FullMixPotion();
            Assert.AreEqual("FullMixPotion", fullMixPotion.Name);
            Assert.AreEqual(ItemRarity.Epic, fullMixPotion.Rarity);
            Assert.AreEqual(-1, fullMixPotion.HPRestoreAmount); // -1 indicates full restore
            Assert.AreEqual(-1, fullMixPotion.MPRestoreAmount); // -1 indicates full restore
        }

        [TestMethod]
        public void Consumable_WithoutRestoreAmounts_UsesDefaults()
        {
            var bag = BagItems.StandardBag();
            Assert.AreEqual(0, bag.HPRestoreAmount);
            Assert.AreEqual(0, bag.MPRestoreAmount);
        }
    }
}