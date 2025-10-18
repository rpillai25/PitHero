using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Stats;

namespace PitHero.Tests
{
    [TestClass]
    public class GearItemsTests
    {
        [TestMethod]
        public void ShortSword_ShouldHaveCorrectProperties()
        {
            var sword = GearItems.ShortSword();
            
            Assert.IsNotNull(sword);
            Assert.AreEqual("ShortSword", sword.Name);
            Assert.AreEqual(ItemKind.WeaponSword, sword.Kind);
            Assert.AreEqual(ItemRarity.Normal, sword.Rarity);
            Assert.AreEqual("+3 Attack", sword.Description);
            Assert.AreEqual(3, sword.AttackBonus);
            Assert.AreEqual(0, sword.DefenseBonus);
            Assert.AreEqual(0, sword.HPBonus);
            Assert.AreEqual(0, sword.MPBonus);
        }

        [TestMethod]
        public void WoodenShield_ShouldHaveCorrectProperties()
        {
            var shield = GearItems.WoodenShield();
            
            Assert.IsNotNull(shield);
            Assert.AreEqual("WoodenShield", shield.Name);
            Assert.AreEqual(ItemKind.Shield, shield.Kind);
            Assert.AreEqual(ItemRarity.Normal, shield.Rarity);
            Assert.AreEqual("+2 Defense", shield.Description);
            Assert.AreEqual(0, shield.AttackBonus);
            Assert.AreEqual(2, shield.DefenseBonus);
            Assert.AreEqual(0, shield.HPBonus);
            Assert.AreEqual(0, shield.MPBonus);
        }

        [TestMethod]
        public void SquireHelm_ShouldHaveCorrectProperties()
        {
            var helm = GearItems.SquireHelm();
            
            Assert.IsNotNull(helm);
            Assert.AreEqual("SquireHelm", helm.Name);
            Assert.AreEqual(ItemKind.HatHelm, helm.Kind);
            Assert.AreEqual(ItemRarity.Normal, helm.Rarity);
            Assert.AreEqual("+2 Defense", helm.Description);
            Assert.AreEqual(0, helm.AttackBonus);
            Assert.AreEqual(2, helm.DefenseBonus);
            Assert.AreEqual(0, helm.HPBonus);
            Assert.AreEqual(0, helm.MPBonus);
        }

        [TestMethod]
        public void LeatherArmor_ShouldHaveCorrectProperties()
        {
            var armor = GearItems.LeatherArmor();
            
            Assert.IsNotNull(armor);
            Assert.AreEqual("LeatherArmor", armor.Name);
            Assert.AreEqual(ItemKind.ArmorMail, armor.Kind);
            Assert.AreEqual(ItemRarity.Normal, armor.Rarity);
            Assert.AreEqual("+3 Defense", armor.Description);
            Assert.AreEqual(0, armor.AttackBonus);
            Assert.AreEqual(3, armor.DefenseBonus);
            Assert.AreEqual(0, armor.HPBonus);
            Assert.AreEqual(0, armor.MPBonus);
        }

        [TestMethod]
        public void RingOfPower_ShouldHaveCorrectProperties()
        {
            var ring = GearItems.RingOfPower();
            
            Assert.IsNotNull(ring);
            Assert.AreEqual("RingOfPower", ring.Name);
            Assert.AreEqual(ItemKind.Accessory, ring.Kind);
            Assert.AreEqual(ItemRarity.Normal, ring.Rarity);
            Assert.AreEqual("+1 Strength", ring.Description);
            Assert.AreEqual(1, ring.StatBonus.Strength);
            Assert.AreEqual(0, ring.StatBonus.Agility);
            Assert.AreEqual(0, ring.StatBonus.Vitality);
            Assert.AreEqual(0, ring.StatBonus.Magic);
            Assert.AreEqual(0, ring.AttackBonus);
            Assert.AreEqual(0, ring.DefenseBonus);
            Assert.AreEqual(0, ring.HPBonus);
            Assert.AreEqual(0, ring.MPBonus);
        }

        [TestMethod]
        public void NecklaceOfHealth_ShouldHaveCorrectProperties()
        {
            var necklace = GearItems.NecklaceOfHealth();
            
            Assert.IsNotNull(necklace);
            Assert.AreEqual("NecklaceOfHealth", necklace.Name);
            Assert.AreEqual(ItemKind.Accessory, necklace.Kind);
            Assert.AreEqual(ItemRarity.Normal, necklace.Rarity);
            Assert.AreEqual("+10 HP", necklace.Description);
            Assert.AreEqual(0, necklace.StatBonus.Strength);
            Assert.AreEqual(0, necklace.StatBonus.Agility);
            Assert.AreEqual(0, necklace.StatBonus.Vitality);
            Assert.AreEqual(0, necklace.StatBonus.Magic);
            Assert.AreEqual(0, necklace.AttackBonus);
            Assert.AreEqual(0, necklace.DefenseBonus);
            Assert.AreEqual(10, necklace.HPBonus);
            Assert.AreEqual(0, necklace.MPBonus);
        }
    }
}
