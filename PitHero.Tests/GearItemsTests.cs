using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.Balance;
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
            
            // Verify attack bonus matches BalanceConfig calculation for Pit 5, Normal rarity
            int expectedAttack = BalanceConfig.CalculateEquipmentAttackBonus(5, ItemRarity.Normal);
            Assert.AreEqual(expectedAttack, sword.AttackBonus);
            Assert.AreEqual("Basic sword for beginners.", sword.Description);
            Assert.AreEqual(0, sword.DefenseBonus);
            Assert.AreEqual(0, sword.HPBonus);
            Assert.AreEqual(0, sword.MPBonus);
        }

        [TestMethod]
        public void LongSword_ShouldHaveCorrectProperties()
        {
            var sword = GearItems.LongSword();
            
            Assert.IsNotNull(sword);
            Assert.AreEqual("LongSword", sword.Name);
            Assert.AreEqual(ItemKind.WeaponSword, sword.Kind);
            Assert.AreEqual(ItemRarity.Normal, sword.Rarity);
            
            // Verify attack bonus matches BalanceConfig calculation for Pit 15, Normal rarity
            int expectedAttack = BalanceConfig.CalculateEquipmentAttackBonus(15, ItemRarity.Normal);
            Assert.AreEqual(expectedAttack, sword.AttackBonus);
            Assert.AreEqual("Longer sword for seasoned warriors.", sword.Description);
            Assert.AreEqual(0, sword.DefenseBonus);
        }

        [TestMethod]
        public void WoodenShield_ShouldHaveCorrectProperties()
        {
            var shield = GearItems.WoodenShield();
            
            Assert.IsNotNull(shield);
            Assert.AreEqual("WoodenShield", shield.Name);
            Assert.AreEqual(ItemKind.Shield, shield.Kind);
            Assert.AreEqual(ItemRarity.Normal, shield.Rarity);
            
            // Verify defense bonus matches BalanceConfig calculation for Pit 5, Normal rarity
            int expectedDefense = BalanceConfig.CalculateEquipmentDefenseBonus(5, ItemRarity.Normal);
            Assert.AreEqual(expectedDefense, shield.DefenseBonus);
            Assert.AreEqual("No adventurer should be without one.", shield.Description);
            Assert.AreEqual(0, shield.AttackBonus);
            Assert.AreEqual(0, shield.HPBonus);
            Assert.AreEqual(0, shield.MPBonus);
        }

        [TestMethod]
        public void IronShield_ShouldHaveCorrectProperties()
        {
            var shield = GearItems.IronShield();
            
            Assert.IsNotNull(shield);
            Assert.AreEqual("IronShield", shield.Name);
            Assert.AreEqual(ItemKind.Shield, shield.Kind);
            Assert.AreEqual(ItemRarity.Normal, shield.Rarity);
            
            // Verify defense bonus matches BalanceConfig calculation for Pit 15, Normal rarity
            int expectedDefense = BalanceConfig.CalculateEquipmentDefenseBonus(15, ItemRarity.Normal);
            Assert.AreEqual(expectedDefense, shield.DefenseBonus);
            Assert.AreEqual(0, shield.AttackBonus);
        }

        [TestMethod]
        public void SquireHelm_ShouldHaveCorrectProperties()
        {
            var helm = GearItems.SquireHelm();
            
            Assert.IsNotNull(helm);
            Assert.AreEqual("SquireHelm", helm.Name);
            Assert.AreEqual(ItemKind.HatHelm, helm.Kind);
            Assert.AreEqual(ItemRarity.Normal, helm.Rarity);
            
            // Verify defense bonus matches BalanceConfig calculation for Pit 5, Normal rarity
            int expectedDefense = BalanceConfig.CalculateEquipmentDefenseBonus(5, ItemRarity.Normal);
            Assert.AreEqual(expectedDefense, helm.DefenseBonus);
            Assert.AreEqual("Helm used by novices.", helm.Description);
            Assert.AreEqual(0, helm.AttackBonus);
            Assert.AreEqual(0, helm.HPBonus);
            Assert.AreEqual(0, helm.MPBonus);
        }

        [TestMethod]
        public void IronHelm_ShouldHaveCorrectProperties()
        {
            var helm = GearItems.IronHelm();
            
            Assert.IsNotNull(helm);
            Assert.AreEqual("IronHelm", helm.Name);
            Assert.AreEqual(ItemKind.HatHelm, helm.Kind);
            Assert.AreEqual(ItemRarity.Normal, helm.Rarity);
            
            // Verify defense bonus matches BalanceConfig calculation for Pit 15, Normal rarity
            int expectedDefense = BalanceConfig.CalculateEquipmentDefenseBonus(15, ItemRarity.Normal);
            Assert.AreEqual(expectedDefense, helm.DefenseBonus);
            Assert.AreEqual(0, helm.AttackBonus);
        }

        [TestMethod]
        public void LeatherArmor_ShouldHaveCorrectProperties()
        {
            var armor = GearItems.LeatherArmor();
            
            Assert.IsNotNull(armor);
            Assert.AreEqual("LeatherArmor", armor.Name);
            Assert.AreEqual(ItemKind.ArmorMail, armor.Kind);
            Assert.AreEqual(ItemRarity.Normal, armor.Rarity);
            
            // Verify defense bonus matches BalanceConfig calculation for Pit 5, Normal rarity
            int expectedDefense = BalanceConfig.CalculateEquipmentDefenseBonus(5, ItemRarity.Normal);
            Assert.AreEqual(expectedDefense, armor.DefenseBonus);
            Assert.AreEqual("Basic armor for adventurers.", armor.Description);
            Assert.AreEqual(0, armor.AttackBonus);
            Assert.AreEqual(0, armor.HPBonus);
            Assert.AreEqual(0, armor.MPBonus);
        }

        [TestMethod]
        public void IronArmor_ShouldHaveCorrectProperties()
        {
            var armor = GearItems.IronArmor();
            
            Assert.IsNotNull(armor);
            Assert.AreEqual("IronArmor", armor.Name);
            Assert.AreEqual(ItemKind.ArmorMail, armor.Kind);
            Assert.AreEqual(ItemRarity.Normal, armor.Rarity);
            
            // Verify defense bonus matches BalanceConfig calculation for Pit 15, Normal rarity
            int expectedDefense = BalanceConfig.CalculateEquipmentDefenseBonus(15, ItemRarity.Normal);
            Assert.AreEqual(expectedDefense, armor.DefenseBonus);
            Assert.AreEqual(0, armor.AttackBonus);
        }

        [TestMethod]
        public void RingOfPower_ShouldHaveCorrectProperties()
        {
            var ring = GearItems.RingOfPower();
            
            Assert.IsNotNull(ring);
            Assert.AreEqual("RingOfPower", ring.Name);
            Assert.AreEqual(ItemKind.Accessory, ring.Kind);
            Assert.AreEqual(ItemRarity.Uncommon, ring.Rarity);
            
            // Verify stat bonus matches BalanceConfig calculation for Pit 15, Uncommon rarity
            int expectedStat = BalanceConfig.CalculateEquipmentStatBonus(15, ItemRarity.Uncommon);
            Assert.AreEqual(expectedStat, ring.StatBonus.Strength);
            Assert.AreEqual("Gives a boost to strength.", ring.Description);
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
            Assert.AreEqual(ItemRarity.Rare, necklace.Rarity);
            
            // Verify stat bonus matches BalanceConfig calculation for Pit 20, Rare rarity
            int expectedStat = BalanceConfig.CalculateEquipmentStatBonus(20, ItemRarity.Rare);
            int expectedHP = expectedStat * 5;
            Assert.AreEqual(expectedStat, necklace.StatBonus.Vitality);
            Assert.AreEqual(expectedHP, necklace.HPBonus);
            Assert.AreEqual("Adventurers wear this to for longevity.", necklace.Description);
            Assert.AreEqual(0, necklace.StatBonus.Strength);
            Assert.AreEqual(0, necklace.StatBonus.Agility);
            Assert.AreEqual(0, necklace.StatBonus.Magic);
            Assert.AreEqual(0, necklace.AttackBonus);
            Assert.AreEqual(0, necklace.DefenseBonus);
            Assert.AreEqual(0, necklace.MPBonus);
        }

        [TestMethod]
        public void ProtectRing_ShouldHaveCorrectProperties()
        {
            var ring = GearItems.ProtectRing();
            
            Assert.IsNotNull(ring);
            Assert.AreEqual("ProtectRing", ring.Name);
            Assert.AreEqual(ItemKind.Accessory, ring.Kind);
            Assert.AreEqual(ItemRarity.Normal, ring.Rarity);
            
            // Verify bonuses match BalanceConfig calculation for Pit 12, Normal rarity
            int expectedDefense = BalanceConfig.CalculateEquipmentDefenseBonus(12, ItemRarity.Normal);
            int expectedStat = BalanceConfig.CalculateEquipmentStatBonus(12, ItemRarity.Normal);
            Assert.AreEqual(expectedDefense, ring.DefenseBonus);
            Assert.AreEqual(expectedStat, ring.StatBonus.Vitality);
            Assert.AreEqual("Wear this for more protection\nfrom physical attacks.", ring.Description);
            Assert.AreEqual(0, ring.AttackBonus);
            Assert.AreEqual(0, ring.HPBonus);
            Assert.AreEqual(0, ring.MPBonus);
        }

        [TestMethod]
        public void MagicChain_ShouldHaveCorrectProperties()
        {
            var chain = GearItems.MagicChain();
            
            Assert.IsNotNull(chain);
            Assert.AreEqual("MagicChain", chain.Name);
            Assert.AreEqual(ItemKind.Accessory, chain.Kind);
            Assert.AreEqual(ItemRarity.Uncommon, chain.Rarity);
            
            // Verify bonuses match BalanceConfig calculation for Pit 18, Uncommon rarity
            int expectedStat = BalanceConfig.CalculateEquipmentStatBonus(18, ItemRarity.Uncommon);
            int expectedMP = expectedStat * 3;
            Assert.AreEqual(expectedStat, chain.StatBonus.Magic);
            Assert.AreEqual(expectedMP, chain.MPBonus);
            Assert.AreEqual("Mages wear this to enhance their spells.", chain.Description);
            Assert.AreEqual(0, chain.StatBonus.Strength);
            Assert.AreEqual(0, chain.StatBonus.Agility);
            Assert.AreEqual(0, chain.StatBonus.Vitality);
            Assert.AreEqual(0, chain.AttackBonus);
            Assert.AreEqual(0, chain.DefenseBonus);
            Assert.AreEqual(0, chain.HPBonus);
        }

        [TestMethod]
        public void BalanceConfig_AttackBonusProgression()
        {
            // Test attack bonus progression across pit levels
            int pit1 = BalanceConfig.CalculateEquipmentAttackBonus(1, ItemRarity.Normal);
            int pit25 = BalanceConfig.CalculateEquipmentAttackBonus(25, ItemRarity.Normal);
            int pit50 = BalanceConfig.CalculateEquipmentAttackBonus(50, ItemRarity.Normal);
            int pit75 = BalanceConfig.CalculateEquipmentAttackBonus(75, ItemRarity.Normal);
            int pit100 = BalanceConfig.CalculateEquipmentAttackBonus(100, ItemRarity.Normal);

            // Verify progression is increasing
            Assert.IsTrue(pit1 < pit25);
            Assert.IsTrue(pit25 < pit50);
            Assert.IsTrue(pit50 < pit75);
            Assert.IsTrue(pit75 < pit100);
        }

        [TestMethod]
        public void BalanceConfig_DefenseBonusProgression()
        {
            // Test defense bonus progression across pit levels
            int pit1 = BalanceConfig.CalculateEquipmentDefenseBonus(1, ItemRarity.Normal);
            int pit25 = BalanceConfig.CalculateEquipmentDefenseBonus(25, ItemRarity.Normal);
            int pit50 = BalanceConfig.CalculateEquipmentDefenseBonus(50, ItemRarity.Normal);
            int pit75 = BalanceConfig.CalculateEquipmentDefenseBonus(75, ItemRarity.Normal);
            int pit100 = BalanceConfig.CalculateEquipmentDefenseBonus(100, ItemRarity.Normal);

            // Verify progression is increasing
            Assert.IsTrue(pit1 < pit25);
            Assert.IsTrue(pit25 < pit50);
            Assert.IsTrue(pit50 < pit75);
            Assert.IsTrue(pit75 < pit100);
        }

        [TestMethod]
        public void BalanceConfig_RarityMultipliers()
        {
            // Test that rarity properly multiplies bonuses
            int normalAtk = BalanceConfig.CalculateEquipmentAttackBonus(10, ItemRarity.Normal);
            int uncommonAtk = BalanceConfig.CalculateEquipmentAttackBonus(10, ItemRarity.Uncommon);
            int rareAtk = BalanceConfig.CalculateEquipmentAttackBonus(10, ItemRarity.Rare);
            int epicAtk = BalanceConfig.CalculateEquipmentAttackBonus(10, ItemRarity.Epic);
            int legendaryAtk = BalanceConfig.CalculateEquipmentAttackBonus(10, ItemRarity.Legendary);

            // Verify rarity increases bonuses
            Assert.IsTrue(normalAtk < uncommonAtk);
            Assert.IsTrue(uncommonAtk < rareAtk);
            Assert.IsTrue(rareAtk < epicAtk);
            Assert.IsTrue(epicAtk < legendaryAtk);
        }
    }
}
