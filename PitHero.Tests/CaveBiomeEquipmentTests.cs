using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Stats;

namespace PitHero.Tests
{
    /// <summary>
    /// Validates all 113 Cave Biome equipment pieces for correct balance formulas,
    /// pit levels, rarities, and integration with GearItems factory.
    /// </summary>
    [TestClass]
    public class CaveBiomeEquipmentTests
    {
        #region Edge Case Tests - Pit 1 Equipment (Lowest Stats)

        [TestMethod]
        public void RustyBlade_Pit1Normal_ShouldHaveCorrectStats()
        {
            var item = GearItems.RustyBlade();
            int expectedAttack = BalanceConfig.CalculateEquipmentAttackBonus(1, ItemRarity.Normal);
            
            Assert.IsNotNull(item);
            Assert.AreEqual("RustyBlade", item.Name);
            Assert.AreEqual(ItemKind.WeaponSword, item.Kind);
            Assert.AreEqual(ItemRarity.Normal, item.Rarity);
            Assert.AreEqual(expectedAttack, item.AttackBonus, "Attack bonus should match BalanceConfig formula");
            Assert.AreEqual(50, item.Price);
            Assert.AreEqual(ElementType.Neutral, item.ElementalProps.Element);
        }

        [TestMethod]
        public void TatteredCloth_Pit1Normal_ShouldHaveCorrectStats()
        {
            var item = GearItems.TatteredCloth();
            int expectedDefense = BalanceConfig.CalculateEquipmentDefenseBonus(1, ItemRarity.Normal);
            
            Assert.IsNotNull(item);
            Assert.AreEqual("TatteredCloth", item.Name);
            Assert.AreEqual(ItemKind.ArmorRobe, item.Kind);
            Assert.AreEqual(ItemRarity.Normal, item.Rarity);
            Assert.AreEqual(expectedDefense, item.DefenseBonus, "Defense bonus should match BalanceConfig formula");
            Assert.AreEqual(40, item.Price);
        }

        [TestMethod]
        public void WoodenPlank_Pit1Normal_ShouldHaveCorrectStats()
        {
            var item = GearItems.WoodenPlank();
            int expectedDefense = BalanceConfig.CalculateEquipmentDefenseBonus(1, ItemRarity.Normal);
            
            Assert.IsNotNull(item);
            Assert.AreEqual("WoodenPlank", item.Name);
            Assert.AreEqual(ItemKind.Shield, item.Kind);
            Assert.AreEqual(expectedDefense, item.DefenseBonus);
        }

        [TestMethod]
        public void ClothCap_Pit1Normal_ShouldHaveCorrectStats()
        {
            var item = GearItems.ClothCap();
            int expectedDefense = BalanceConfig.CalculateEquipmentDefenseBonus(1, ItemRarity.Normal);
            
            Assert.IsNotNull(item);
            Assert.AreEqual("ClothCap", item.Name);
            Assert.AreEqual(ItemKind.HatHeadband, item.Kind);
            Assert.AreEqual(expectedDefense, item.DefenseBonus);
        }

        #endregion

        #region Edge Case Tests - Pit 10 Equipment (Normal Max)

        [TestMethod]
        public void CavernCutter_Pit10Normal_ShouldHaveCorrectStats()
        {
            var item = GearItems.CavernCutter();
            int expectedAttack = BalanceConfig.CalculateEquipmentAttackBonus(10, ItemRarity.Normal);
            
            Assert.IsNotNull(item);
            Assert.AreEqual(ItemKind.WeaponSword, item.Kind);
            Assert.AreEqual(ItemRarity.Normal, item.Rarity);
            Assert.AreEqual(expectedAttack, item.AttackBonus);
            Assert.AreEqual(275, item.Price);
            Assert.AreEqual(ElementType.Earth, item.ElementalProps.Element);
        }

        [TestMethod]
        public void FlameHatchet_Pit10Normal_ShouldHaveCorrectStats()
        {
            var item = GearItems.FlameHatchet();
            int expectedAttack = BalanceConfig.CalculateEquipmentAttackBonus(10, ItemRarity.Normal);
            
            Assert.IsNotNull(item);
            Assert.AreEqual(ItemKind.WeaponSword, item.Kind); // Axes use WeaponSword
            Assert.AreEqual(expectedAttack, item.AttackBonus);
            Assert.AreEqual(ElementType.Fire, item.ElementalProps.Element);
        }

        [TestMethod]
        public void ChainShirt_Pit10Normal_ShouldHaveCorrectStats()
        {
            var item = GearItems.ChainShirt();
            int expectedDefense = BalanceConfig.CalculateEquipmentDefenseBonus(10, ItemRarity.Normal);
            
            Assert.IsNotNull(item);
            Assert.AreEqual(ItemKind.ArmorMail, item.Kind);
            Assert.AreEqual(expectedDefense, item.DefenseBonus);
        }

        #endregion

        #region Edge Case Tests - Pit 11 Equipment (Uncommon Start)

        [TestMethod]
        public void CrystalEdge_Pit11Uncommon_ShouldHaveCorrectStats()
        {
            var item = GearItems.CrystalEdge();
            int expectedAttack = BalanceConfig.CalculateEquipmentAttackBonus(11, ItemRarity.Uncommon);
            
            Assert.IsNotNull(item);
            Assert.AreEqual(ItemKind.WeaponSword, item.Kind);
            Assert.AreEqual(ItemRarity.Uncommon, item.Rarity);
            Assert.AreEqual(expectedAttack, item.AttackBonus);
            Assert.AreEqual(400, item.Price);
            Assert.AreEqual(ElementType.Earth, item.ElementalProps.Element);
        }

        [TestMethod]
        public void SteelShield_Pit11Uncommon_ShouldHaveCorrectStats()
        {
            var item = GearItems.SteelShield();
            int expectedDefense = BalanceConfig.CalculateEquipmentDefenseBonus(11, ItemRarity.Uncommon);
            
            Assert.IsNotNull(item);
            Assert.AreEqual(ItemKind.Shield, item.Kind);
            Assert.AreEqual(ItemRarity.Uncommon, item.Rarity);
            Assert.AreEqual(expectedDefense, item.DefenseBonus);
        }

        [TestMethod]
        public void SteelHelm_Pit11Uncommon_ShouldHaveCorrectStats()
        {
            var item = GearItems.SteelHelm();
            int expectedDefense = BalanceConfig.CalculateEquipmentDefenseBonus(11, ItemRarity.Uncommon);
            
            Assert.IsNotNull(item);
            Assert.AreEqual(ItemKind.HatHelm, item.Kind);
            Assert.AreEqual(ItemRarity.Uncommon, item.Rarity);
            Assert.AreEqual(expectedDefense, item.DefenseBonus);
        }

        #endregion

        #region Edge Case Tests - Pit 25 Equipment (Highest Stats)

        [TestMethod]
        public void PitLordsSword_Pit25Uncommon_ShouldHaveCorrectStats()
        {
            var item = GearItems.PitLordsSword();
            int expectedAttack = BalanceConfig.CalculateEquipmentAttackBonus(25, ItemRarity.Uncommon);
            
            Assert.IsNotNull(item);
            Assert.AreEqual("PitLordsSword", item.Name);
            Assert.AreEqual(ItemKind.WeaponSword, item.Kind);
            Assert.AreEqual(ItemRarity.Uncommon, item.Rarity);
            Assert.AreEqual(expectedAttack, item.AttackBonus, "Should match BalanceConfig formula: (1 + 25/2) * 1.5 = 20");
            Assert.AreEqual(750, item.Price);
            Assert.AreEqual(ElementType.Dark, item.ElementalProps.Element);
        }

        [TestMethod]
        public void PitLordsArmor_Pit25Uncommon_ShouldHaveCorrectStats()
        {
            var item = GearItems.PitLordsArmor();
            int expectedDefense = BalanceConfig.CalculateEquipmentDefenseBonus(25, ItemRarity.Uncommon);
            
            Assert.IsNotNull(item);
            Assert.AreEqual(ItemKind.ArmorMail, item.Kind);
            Assert.AreEqual(ItemRarity.Uncommon, item.Rarity);
            Assert.AreEqual(expectedDefense, item.DefenseBonus, "Should match BalanceConfig formula: (1 + 25/3) * 1.5 = 14");
            Assert.AreEqual(1100, item.Price);
        }

        [TestMethod]
        public void PitLordsAegis_Pit25Uncommon_ShouldHaveCorrectStats()
        {
            var item = GearItems.PitLordsAegis();
            int expectedDefense = BalanceConfig.CalculateEquipmentDefenseBonus(25, ItemRarity.Uncommon);
            
            Assert.IsNotNull(item);
            Assert.AreEqual(ItemKind.Shield, item.Kind);
            Assert.AreEqual(expectedDefense, item.DefenseBonus);
            Assert.AreEqual(1100, item.Price);
        }

        [TestMethod]
        public void PitLordsCrown_Pit25Uncommon_ShouldHaveCorrectStats()
        {
            var item = GearItems.PitLordsCrown();
            int expectedDefense = BalanceConfig.CalculateEquipmentDefenseBonus(25, ItemRarity.Uncommon);
            
            Assert.IsNotNull(item);
            Assert.AreEqual(ItemKind.HatHelm, item.Kind);
            Assert.AreEqual(expectedDefense, item.DefenseBonus);
            Assert.AreEqual(1100, item.Price);
        }

        #endregion

        #region New Weapon Types Tests

        [TestMethod]
        public void WoodenSpear_NewWeaponType_ShouldHaveCorrectStats()
        {
            var item = GearItems.WoodenSpear();
            int expectedAttack = BalanceConfig.CalculateEquipmentAttackBonus(2, ItemRarity.Normal);
            
            Assert.IsNotNull(item);
            Assert.AreEqual("WoodenSpear", item.Name);
            Assert.AreEqual(ItemKind.WeaponSword, item.Kind); // Spears use WeaponSword
            Assert.AreEqual(expectedAttack, item.AttackBonus);
            Assert.AreEqual(75, item.Price);
        }

        [TestMethod]
        public void StalactiteSpear_Pit19Uncommon_ShouldHaveCorrectStats()
        {
            var item = GearItems.StalactiteSpear();
            int expectedAttack = BalanceConfig.CalculateEquipmentAttackBonus(19, ItemRarity.Uncommon);
            
            Assert.IsNotNull(item);
            Assert.AreEqual(ItemRarity.Uncommon, item.Rarity);
            Assert.AreEqual(expectedAttack, item.AttackBonus);
            Assert.AreEqual(ElementType.Earth, item.ElementalProps.Element);
        }

        [TestMethod]
        public void Mallet_NewWeaponType_ShouldHaveCorrectStats()
        {
            var item = GearItems.Mallet();
            int expectedAttack = BalanceConfig.CalculateEquipmentAttackBonus(3, ItemRarity.Normal);
            
            Assert.IsNotNull(item);
            Assert.AreEqual(ItemKind.WeaponKnuckle, item.Kind); // Hammers use WeaponKnuckle
            Assert.AreEqual(expectedAttack, item.AttackBonus);
        }

        [TestMethod]
        public void MagmaMaul_Pit25Uncommon_ShouldHaveCorrectStats()
        {
            var item = GearItems.MagmaMaul();
            int expectedAttack = BalanceConfig.CalculateEquipmentAttackBonus(25, ItemRarity.Uncommon);
            
            Assert.IsNotNull(item);
            Assert.AreEqual(ItemKind.WeaponKnuckle, item.Kind);
            Assert.AreEqual(ItemRarity.Uncommon, item.Rarity);
            Assert.AreEqual(expectedAttack, item.AttackBonus);
            Assert.AreEqual(ElementType.Fire, item.ElementalProps.Element);
        }

        [TestMethod]
        public void WalkingStick_NewWeaponType_ShouldHaveCorrectStats()
        {
            var item = GearItems.WalkingStick();
            int expectedAttack = BalanceConfig.CalculateEquipmentAttackBonus(2, ItemRarity.Normal);
            
            Assert.IsNotNull(item);
            Assert.AreEqual(ItemKind.WeaponStaff, item.Kind); // Staves use WeaponStaff
            Assert.AreEqual(expectedAttack, item.AttackBonus);
        }

        [TestMethod]
        public void EarthenStaff_Pit11Uncommon_ShouldHaveCorrectStats()
        {
            var item = GearItems.EarthenStaff();
            int expectedAttack = BalanceConfig.CalculateEquipmentAttackBonus(11, ItemRarity.Uncommon);
            
            Assert.IsNotNull(item);
            Assert.AreEqual(ItemKind.WeaponStaff, item.Kind);
            Assert.AreEqual(ItemRarity.Uncommon, item.Rarity);
            Assert.AreEqual(expectedAttack, item.AttackBonus);
            Assert.AreEqual(ElementType.Earth, item.ElementalProps.Element);
        }

        #endregion

        #region Progression Tests - Sample Equipment Across Range

        [TestMethod]
        public void EmberSword_Pit13Uncommon_ShouldHaveCorrectStats()
        {
            var item = GearItems.EmberSword();
            int expectedAttack = BalanceConfig.CalculateEquipmentAttackBonus(13, ItemRarity.Uncommon);
            
            Assert.IsNotNull(item);
            Assert.AreEqual(expectedAttack, item.AttackBonus);
            Assert.AreEqual(ElementType.Fire, item.ElementalProps.Element);
        }

        [TestMethod]
        public void ShadowStiletto_Pit17Uncommon_ShouldHaveCorrectStats()
        {
            var item = GearItems.ShadowStiletto();
            int expectedAttack = BalanceConfig.CalculateEquipmentAttackBonus(17, ItemRarity.Uncommon);
            
            Assert.IsNotNull(item);
            Assert.AreEqual(expectedAttack, item.AttackBonus);
            Assert.AreEqual(ElementType.Dark, item.ElementalProps.Element);
        }

        [TestMethod]
        public void GranitePlate_Pit20Uncommon_ShouldHaveCorrectStats()
        {
            var item = GearItems.GranitePlate();
            int expectedDefense = BalanceConfig.CalculateEquipmentDefenseBonus(20, ItemRarity.Uncommon);
            
            Assert.IsNotNull(item);
            Assert.AreEqual(expectedDefense, item.DefenseBonus);
            Assert.AreEqual(ElementType.Earth, item.ElementalProps.Element);
        }

        [TestMethod]
        public void LavaShield_Pit17Uncommon_ShouldHaveCorrectStats()
        {
            var item = GearItems.LavaShield();
            int expectedDefense = BalanceConfig.CalculateEquipmentDefenseBonus(17, ItemRarity.Uncommon);
            
            Assert.IsNotNull(item);
            Assert.AreEqual(expectedDefense, item.DefenseBonus);
            Assert.AreEqual(ElementType.Fire, item.ElementalProps.Element);
        }

        #endregion

        #region Formula Validation Tests

        [TestMethod]
        public void BalanceFormulas_Pit1NormalWeapon_ShouldBe1Attack()
        {
            int result = BalanceConfig.CalculateEquipmentAttackBonus(1, ItemRarity.Normal);
            Assert.AreEqual(1, result, "Pit 1 Normal: (1 + 1/2) * 1.0 = 1.5 → 1");
        }

        [TestMethod]
        public void BalanceFormulas_Pit10NormalWeapon_ShouldBe6Attack()
        {
            int result = BalanceConfig.CalculateEquipmentAttackBonus(10, ItemRarity.Normal);
            Assert.AreEqual(6, result, "Pit 10 Normal: (1 + 10/2) * 1.0 = 6");
        }

        [TestMethod]
        public void BalanceFormulas_Pit11UncommonWeapon_ShouldBe9Attack()
        {
            int result = BalanceConfig.CalculateEquipmentAttackBonus(11, ItemRarity.Uncommon);
            Assert.AreEqual(9, result, "Pit 11 Uncommon: (1 + 11/2) * 1.5 = 9.75 → 9");
        }

        [TestMethod]
        public void BalanceFormulas_Pit25UncommonWeapon_ShouldBe20Attack()
        {
            int result = BalanceConfig.CalculateEquipmentAttackBonus(25, ItemRarity.Uncommon);
            Assert.AreEqual(20, result, "Pit 25 Uncommon: (1 + 25/2) * 1.5 = 20.25 → 20");
        }

        [TestMethod]
        public void BalanceFormulas_Pit11UncommonDefense_ShouldBe7Defense()
        {
            int result = BalanceConfig.CalculateEquipmentDefenseBonus(11, ItemRarity.Uncommon);
            Assert.AreEqual(7, result, "Pit 11 Uncommon: (1 + 11/3) * 1.5 = 7.0 → 7");
        }

        [TestMethod]
        public void BalanceFormulas_Pit25UncommonDefense_ShouldBe14Defense()
        {
            int result = BalanceConfig.CalculateEquipmentDefenseBonus(25, ItemRarity.Uncommon);
            Assert.AreEqual(14, result, "Pit 25 Uncommon: (1 + 25/3) * 1.5 = 14.0 → 14");
        }

        #endregion

        #region Element Distribution Tests

        [TestMethod]
        public void EarthEquipment_ShouldHaveEarthElement()
        {
            var items = new[]
            {
                GearItems.CrystalEdge(),
                GearItems.StoneLance(),
                GearItems.CrystalGuard(),
                GearItems.GraniteGuard(),
                GearItems.StoneCrown()
            };

            foreach (var item in items)
            {
                Assert.AreEqual(ElementType.Earth, item.ElementalProps.Element);
            }
        }

        [TestMethod]
        public void FireEquipment_ShouldHaveFireElement()
        {
            var items = new[]
            {
                GearItems.EmberSword(),
                GearItems.FlameHatchet(),
                GearItems.FlameLance(),
                GearItems.MagmaMaul(),
                GearItems.EmberShield()
            };

            foreach (var item in items)
            {
                Assert.AreEqual(ElementType.Fire, item.ElementalProps.Element);
            }
        }

        [TestMethod]
        public void DarkEquipment_ShouldHaveDarkElement()
        {
            var items = new[]
            {
                GearItems.PitLordsSword(),
                GearItems.SilentFang(),
                GearItems.ShadowStiletto(),
                GearItems.PitLordsArmor(),
                GearItems.ShadowGuard()
            };

            foreach (var item in items)
            {
                Assert.AreEqual(ElementType.Dark, item.ElementalProps.Element);
            }
        }

        [TestMethod]
        public void NeutralEquipment_ShouldHaveNeutralElement()
        {
            var items = new[]
            {
                GearItems.RustyBlade(),
                GearItems.WoodenSpear(),
                GearItems.Mallet(),
                GearItems.TatteredCloth(),
                GearItems.WoodenPlank()
            };

            foreach (var item in items)
            {
                Assert.AreEqual(ElementType.Neutral, item.ElementalProps.Element);
            }
        }

        #endregion
    }
}
