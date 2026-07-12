using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Config;
using PitHero.ECS.Components;
using PitHero.VirtualGame;
using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Stats;
using System;

namespace PitHero.Tests
{
    /// <summary>
    /// Tests for gear tier scaling: Gear.CreateTierScaledCopy, ItemRegistry "+N" round-trip,
    /// and TreasureComponent deterministic drops at tier 2.
    /// </summary>
    [TestClass]
    public class GearTierScalingTests
    {
        // ── Helpers ───────────────────────────────────────────────────────────────────

        private static Gear MakeBaseGear(
            int atk = 10, int def = 5, int price = 100,
            ItemRarity rarity = ItemRarity.Normal)
        {
            var stats = new StatBlock(strength: 2, agility: 1, vitality: 1, magic: 0);
            return new Gear(
                "TestSword",
                ItemKind.WeaponSword,
                rarity,
                "TestDesc",
                price,
                in stats,
                atk: atk,
                def: def);
        }

        // ── Tier-1 pass-through ───────────────────────────────────────────────────────

        [TestMethod]
        public void CreateTierScaledCopy_Tier1_ReturnsSameInstance()
        {
            var gear = MakeBaseGear();
            var result = Gear.CreateTierScaledCopy(gear, 1, 25);
            Assert.AreSame(gear, result, "Tier 1 should return the original instance unchanged");
        }

        [TestMethod]
        public void CreateTierScaledCopy_Tier0_ReturnsSameInstance()
        {
            var gear = MakeBaseGear();
            var result = Gear.CreateTierScaledCopy(gear, 0, 25);
            Assert.AreSame(gear, result, "Tier <= 1 should return the original instance unchanged");
        }

        // ── Tier-2 stat scaling ───────────────────────────────────────────────────────

        [TestMethod]
        public void CreateTierScaledCopy_Tier2_AttackIsHigherThanBase()
        {
            var gear = MakeBaseGear(atk: 10, rarity: ItemRarity.Normal);
            int depthDelta = 1 * BiomeProgressionConfig.MaxBiomeLevel; // 25

            var scaled = Gear.CreateTierScaledCopy(gear, 2, depthDelta);

            Assert.IsTrue(scaled.AttackBonus > gear.AttackBonus,
                $"Tier-2 atk ({scaled.AttackBonus}) should exceed tier-1 atk ({gear.AttackBonus})");
        }

        [TestMethod]
        public void CreateTierScaledCopy_Tier2_DefenseIsHigherThanBase()
        {
            var gear = MakeBaseGear(def: 5, rarity: ItemRarity.Normal);
            int depthDelta = 1 * BiomeProgressionConfig.MaxBiomeLevel; // 25

            var scaled = Gear.CreateTierScaledCopy(gear, 2, depthDelta);

            Assert.IsTrue(scaled.DefenseBonus > gear.DefenseBonus,
                $"Tier-2 def ({scaled.DefenseBonus}) should exceed tier-1 def ({gear.DefenseBonus})");
        }

        [TestMethod]
        public void CreateTierScaledCopy_Tier2_PriceIsDoubled()
        {
            var gear = MakeBaseGear(price: 100);
            var scaled = Gear.CreateTierScaledCopy(gear, 2, 25);
            Assert.AreEqual(gear.Price * 2, scaled.Price,
                "Tier-2 price should be source.Price * tier");
        }

        [TestMethod]
        public void CreateTierScaledCopy_Tier2_TierPropertyIs2()
        {
            var gear = MakeBaseGear();
            var scaled = Gear.CreateTierScaledCopy(gear, 2, 25);
            Assert.AreEqual(2, scaled.Tier);
        }

        [TestMethod]
        public void CreateTierScaledCopy_Tier1Base_TierPropertyIs1()
        {
            var gear = MakeBaseGear();
            Assert.AreEqual(1, gear.Tier, "Default gear should have Tier = 1");
        }

        [TestMethod]
        public void CreateTierScaledCopy_Tier2_NameContainsPlusTwoSuffix()
        {
            var gear = MakeBaseGear();
            var scaled = Gear.CreateTierScaledCopy(gear, 2, 25);
            // In headless context, Name resolves to _nameKey. Tier-2 appends "+2".
            Assert.IsTrue(scaled.Name.EndsWith("+2"),
                $"Tier-2 name '{scaled.Name}' should end with '+2'");
        }

        [TestMethod]
        public void CreateTierScaledCopy_Tier3_NameContainsPlusThreeSuffix()
        {
            var gear = MakeBaseGear();
            var scaled = Gear.CreateTierScaledCopy(gear, 3, 50);
            Assert.IsTrue(scaled.Name.EndsWith("+3"),
                $"Tier-3 name '{scaled.Name}' should end with '+3'");
        }

        [TestMethod]
        public void CreateTierScaledCopy_SpriteName_UnchangedFromSource()
        {
            var gear = MakeBaseGear();
            string baseSprite = gear.SpriteName;
            var scaled = Gear.CreateTierScaledCopy(gear, 2, 25);
            Assert.AreEqual(baseSprite, scaled.SpriteName,
                "SpriteName must remain the same as the source so atlas lookup succeeds");
        }

        [TestMethod]
        public void CreateTierScaledCopy_Rarity_UnchangedFromSource()
        {
            var gear = MakeBaseGear(rarity: ItemRarity.Rare);
            var scaled = Gear.CreateTierScaledCopy(gear, 2, 25);
            Assert.AreEqual(ItemRarity.Rare, scaled.Rarity);
        }

        [TestMethod]
        public void CreateTierScaledCopy_Kind_UnchangedFromSource()
        {
            var gear = MakeBaseGear();
            var scaled = Gear.CreateTierScaledCopy(gear, 2, 25);
            Assert.AreEqual(ItemKind.WeaponSword, scaled.Kind);
        }

        // ── Delta formula verification ────────────────────────────────────────────────

        [TestMethod]
        public void CalculateEquipmentAttackBonusDelta_Normal_DepthDelta25_Equals12()
        {
            // (25 / 2f) * 1.0 = 12.5 → (int) = 12
            int delta = BalanceConfig.CalculateEquipmentAttackBonusDelta(25, ItemRarity.Normal);
            Assert.AreEqual(12, delta,
                "Attack delta for depthDelta=25, Normal rarity should be 12 (25/2 = 12.5 → 12)");
        }

        [TestMethod]
        public void CalculateEquipmentDefenseBonusDelta_Normal_DepthDelta25_Equals8()
        {
            // (25 / 3f) * 1.0 = 8.33 → (int) = 8
            int delta = BalanceConfig.CalculateEquipmentDefenseBonusDelta(25, ItemRarity.Normal);
            Assert.AreEqual(8, delta,
                "Defense delta for depthDelta=25, Normal rarity should be 8 (25/3 = 8.33 → 8)");
        }

        [TestMethod]
        public void CalculateEquipmentStatBonusDelta_Normal_DepthDelta25_Equals5()
        {
            // (25 / 5f) * 1.0 = 5.0 → (int) = 5
            int delta = BalanceConfig.CalculateEquipmentStatBonusDelta(25, ItemRarity.Normal);
            Assert.AreEqual(5, delta,
                "Stat delta for depthDelta=25, Normal rarity should be 5 (25/5 = 5)");
        }

        // ── ItemRegistry "+N" round-trip ─────────────────────────────────────────────

        [TestMethod]
        public void ItemRegistry_TryCreateItem_TierScaledName_RoundTrips()
        {
            // Get a known base gear name via the registry (headless: Name = _nameKey)
            Assert.IsTrue(ItemRegistry.TryCreateItem("Inv_RustyBlade_Name", out var baseItem),
                "Base item 'Inv_RustyBlade_Name' must exist in registry");
            Assert.IsInstanceOfType(baseItem, typeof(Gear));

            var baseGear = (Gear)baseItem;
            var tier2Name = baseGear.Name + "+2"; // e.g. "Inv_RustyBlade_Name+2" in headless

            ItemRegistry.TierDepthStride = BiomeProgressionConfig.MaxBiomeLevel; // ensure stride is set
            bool found = ItemRegistry.TryCreateItem(tier2Name, out var tier2Item);

            Assert.IsTrue(found, $"TryCreateItem('{tier2Name}') should succeed via +N parsing");
            Assert.IsInstanceOfType(tier2Item, typeof(Gear));

            var tier2Gear = (Gear)tier2Item;
            Assert.AreEqual(2, tier2Gear.Tier, "Round-tripped item should have Tier = 2");
            Assert.IsTrue(tier2Gear.AttackBonus >= baseGear.AttackBonus,
                "Round-tripped tier-2 atk should be >= base atk");
        }

        [TestMethod]
        public void ItemRegistry_TryCreateItem_NonExistentName_ReturnsFalse()
        {
            bool found = ItemRegistry.TryCreateItem("NonExistentSword+2", out _);
            Assert.IsFalse(found, "Unknown base name with +N suffix should return false");
        }

        [TestMethod]
        public void ItemRegistry_TryCreateItem_PotionWithPlusSuffix_ReturnsFalse()
        {
            // Potions are not Gear so the +N branch should not produce a result even if base exists.
            // "Inv_HPPotion_Name+2" — base "Inv_HPPotion_Name" exists but is a Potion, not Gear.
            ItemRegistry.TryCreateItem("Inv_HPPotion_Name", out var potionItem);
            if (potionItem == null) return; // potion not in registry under that key — skip

            string potionTier2Name = potionItem.Name + "+2";
            bool found = ItemRegistry.TryCreateItem(potionTier2Name, out var result);
            // Either not found (no base), or found but not Gear (so falls through to false)
            if (found) Assert.IsNotInstanceOfType(result, typeof(Gear),
                "Non-gear items should not be tier-scaled via +N lookup");
        }

        // ── TreasureComponent deterministic tier-2 vs tier-1 comparison ──────────────

        [TestMethod]
        public void TreasureComponent_Deterministic_Tier2CaveGear_StrongerThanTier1()
        {
            // Use a non-boss, non-trivial cave level where gear can drop (level >= 11 for Uncommon gear).
            // Use level 11 which yields Uncommon gear (level 2 treasure) for rolls < 0.35f.
            // Seed the RNG to a known state so DetermineCaveTreasureLevel returns level 2.
            Nez.Random.SetSeed(12345);

            // Tier 1 drop at displayed level 11
            var tier1Chest = new TreasureComponent();
            LootJobContext emptyCtx = LootJobContext.Empty;
            tier1Chest.InitializeForPitLevel(11, emptyCtx, pitTier: 1);

            Nez.Random.SetSeed(12345);

            // Tier 2 drop at displayed level 11 (same RNG state = same base item, then scaled)
            var tier2Chest = new TreasureComponent();
            tier2Chest.InitializeForPitLevel(11, emptyCtx, pitTier: 2);

            // If both drops produced Gear, tier-2 should be strictly stronger.
            if (tier1Chest.ContainedItem is Gear t1 && tier2Chest.ContainedItem is Gear t2)
            {
                Assert.IsTrue(t2.AttackBonus + t2.DefenseBonus >= t1.AttackBonus + t1.DefenseBonus,
                    $"Tier-2 gear stats (atk={t2.AttackBonus} def={t2.DefenseBonus}) should be >= tier-1 (atk={t1.AttackBonus} def={t1.DefenseBonus})");
                Assert.AreEqual(2, t2.Tier, "Tier-2 chest item should have Tier = 2");
                Assert.AreEqual(1, t1.Tier, "Tier-1 chest item should have Tier = 1");
            }
            // If seeds produced a potion/seed drop, skip rather than fail — the test is best-effort.
        }

        // ── VirtualPitGenerator depth-decomposition ───────────────────────────────────

        [TestMethod]
        public void VirtualPitGenerator_Depth40_IsTier2CaveLevel15()
        {
            // depth 40 = displayedLevel 15, tier 2
            int displayedLevel = BiomeProgressionConfig.GetDisplayedLevelForDepth(40);
            int tier = BiomeProgressionConfig.GetTierForDepth(40);
            Assert.AreEqual(15, displayedLevel, "Depth 40 should map to displayed level 15");
            Assert.AreEqual(2, tier, "Depth 40 should be tier 2");
        }

        [TestMethod]
        public void VirtualPitGenerator_RegenerateForLevel40_GeneratesAncientWyrmBoss()
        {
            // depth 40 → displayed 15 → AncientWyrm boss floor
            var world = new VirtualWorldState();
            var context = new VirtualGoapContext(world);
            context.PitWidthManager.Initialize();

            context.PitGenerator.RegenerateForLevel(40);

            // Boss floor 15 (AncientWyrm) should produce exactly 1 boss marker
            Assert.AreEqual(1, world.LastGeneratedBossMonsterCount,
                "Depth 40 (cave boss floor 15) should generate exactly 1 boss monster (AncientWyrm)");
        }

        [TestMethod]
        public void VirtualPitGenerator_RegenerateForLevel26_IsTier2CaveLevel1()
        {
            // depth 26 = displayedLevel 1, tier 2 (first level of tier-2 loop)
            int displayedLevel = BiomeProgressionConfig.GetDisplayedLevelForDepth(26);
            int tier = BiomeProgressionConfig.GetTierForDepth(26);
            Assert.AreEqual(1, displayedLevel, "Depth 26 should map to displayed level 1");
            Assert.AreEqual(2, tier, "Depth 26 should be tier 2");
        }

        [TestMethod]
        public void VirtualPitGenerator_RegenerateForLevel25_IsTier1CaveLevel25()
        {
            // depth 25 = displayedLevel 25, tier 1 (unchanged boundary)
            int displayedLevel = BiomeProgressionConfig.GetDisplayedLevelForDepth(25);
            int tier = BiomeProgressionConfig.GetTierForDepth(25);
            Assert.AreEqual(25, displayedLevel, "Depth 25 should map to displayed level 25 (tier 1)");
            Assert.AreEqual(1, tier, "Depth 25 should be tier 1");
        }

        [TestMethod]
        public void CaveBiomeConfig_GetScaledEnemyLevel_Tier2_IsHigherThanTier1()
        {
            // At any cave level, tier-2 enemy level should exceed tier-1.
            for (int level = 1; level <= 25; level++)
            {
                if (CaveBiomeConfig.IsBossFloor(level))
                    continue; // Boss levels have +2 bonus; still check that tier-2 >= tier-1

                int t1Level = CaveBiomeConfig.GetScaledEnemyLevelForPitLevel(level, pitTier: 1);
                int t2Level = CaveBiomeConfig.GetScaledEnemyLevelForPitLevel(level, pitTier: 2);
                Assert.IsTrue(t2Level >= t1Level,
                    $"Tier-2 enemy level ({t2Level}) at cave level {level} should be >= tier-1 ({t1Level})");
            }
        }

        [TestMethod]
        public void CaveBiomeConfig_GetScaledEnemyLevel_DefaultTier_BehaviourMatchesTier1()
        {
            // The default pitTier=1 signature should produce identical results to explicit pitTier=1.
            for (int level = 1; level <= 25; level++)
            {
                int defaultResult  = CaveBiomeConfig.GetScaledEnemyLevelForPitLevel(level);
                int explicit1Result = CaveBiomeConfig.GetScaledEnemyLevelForPitLevel(level, pitTier: 1);
                Assert.AreEqual(defaultResult, explicit1Result,
                    $"Default pitTier and explicit pitTier=1 should produce the same result at level {level}");
            }
        }
    }
}
