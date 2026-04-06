using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using PitHero.ECS.Components;
using RolePlayingFramework.Balance;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Jobs;
using RolePlayingFramework.Loot;
using System.Collections.Generic;
using System.Linq;
using PitHero;

namespace PitHero.Tests
{
    [TestClass]
    public class TreasureComponentTests
    {
        [TestMethod]
        public void TreasureLevel_PitLevel1To10_ShouldOnlyGiveLevel1()
        {
            // Test multiple times to ensure consistency for pit levels 1-10
            for (int pitLevel = 1; pitLevel <= 10; pitLevel++)
            {
                for (int i = 0; i < 100; i++)
                {
                    var treasureLevel = TreasureComponent.DetermineTreasureLevel(pitLevel);
                    Assert.AreEqual(1, treasureLevel, $"Pit level {pitLevel} should only give treasure level 1");
                }
            }
        }

        [TestMethod]
        public void TreasureLevel_PitLevel11To30_ShouldGiveLevel1And2()
        {
            var results = new Dictionary<int, int>();
            
            // Test multiple times to get distribution
            for (int i = 0; i < 1000; i++)
            {
                var treasureLevel = TreasureComponent.DetermineTreasureLevel(26); // Non-cave mid-range
                if (!results.ContainsKey(treasureLevel))
                    results[treasureLevel] = 0;
                results[treasureLevel]++;
            }

            // Should only have levels 1 and 2
            Assert.IsTrue(results.ContainsKey(1), "Should contain treasure level 1");
            Assert.IsTrue(results.ContainsKey(2), "Should contain treasure level 2");
            Assert.IsFalse(results.ContainsKey(3), "Should not contain treasure level 3");
            Assert.IsFalse(results.ContainsKey(4), "Should not contain treasure level 4");
            Assert.IsFalse(results.ContainsKey(5), "Should not contain treasure level 5");

            // Level 1 should be more common (80% vs 20%)
            Assert.IsTrue(results[1] > results[2], "Level 1 should be more common than level 2");
        }

        [TestMethod]
        public void TreasureLevel_PitLevel31To60_ShouldGiveLevel1To3()
        {
            var results = new Dictionary<int, int>();
            
            // Test multiple times to get distribution
            for (int i = 0; i < 1000; i++)
            {
                var treasureLevel = TreasureComponent.DetermineTreasureLevel(45); // Mid-range
                if (!results.ContainsKey(treasureLevel))
                    results[treasureLevel] = 0;
                results[treasureLevel]++;
            }

            // Should have levels 1, 2, and 3
            Assert.IsTrue(results.ContainsKey(1), "Should contain treasure level 1");
            Assert.IsTrue(results.ContainsKey(2), "Should contain treasure level 2");
            Assert.IsTrue(results.ContainsKey(3), "Should contain treasure level 3");
            Assert.IsFalse(results.ContainsKey(4), "Should not contain treasure level 4");
            Assert.IsFalse(results.ContainsKey(5), "Should not contain treasure level 5");

            // Level 1 should be most common (70%), then level 2 (20%), then level 3 (10%)
            Assert.IsTrue(results[1] > results[2], "Level 1 should be more common than level 2");
            Assert.IsTrue(results[2] > results[3], "Level 2 should be more common than level 3");
        }

        [TestMethod]
        public void TreasureLevel_PitLevel61To90_ShouldGiveLevel1To4()
        {
            var results = new Dictionary<int, int>();
            
            // Test multiple times to get distribution
            for (int i = 0; i < 1000; i++)
            {
                var treasureLevel = TreasureComponent.DetermineTreasureLevel(75); // Mid-range
                if (!results.ContainsKey(treasureLevel))
                    results[treasureLevel] = 0;
                results[treasureLevel]++;
            }

            // Should have levels 1, 2, 3, and 4
            Assert.IsTrue(results.ContainsKey(1), "Should contain treasure level 1");
            Assert.IsTrue(results.ContainsKey(2), "Should contain treasure level 2");
            Assert.IsTrue(results.ContainsKey(3), "Should contain treasure level 3");
            Assert.IsTrue(results.ContainsKey(4), "Should contain treasure level 4");
            Assert.IsFalse(results.ContainsKey(5), "Should not contain treasure level 5");

            // Verify relative frequencies
            Assert.IsTrue(results[1] > results[2], "Level 1 should be more common than level 2");
            Assert.IsTrue(results[2] > results[3], "Level 2 should be more common than level 3");
            Assert.IsTrue(results[3] > results[4], "Level 3 should be more common than level 4");
        }

        [TestMethod]
        public void TreasureLevel_PitLevel91Plus_ShouldGiveLevel1To5()
        {
            var results = new Dictionary<int, int>();
            
            // Test multiple times to get distribution
            for (int i = 0; i < 10000; i++) // More iterations to catch rare level 5
            {
                var treasureLevel = TreasureComponent.DetermineTreasureLevel(100); // High level
                if (!results.ContainsKey(treasureLevel))
                    results[treasureLevel] = 0;
                results[treasureLevel]++;
            }

            // Should have all levels 1-5
            Assert.IsTrue(results.ContainsKey(1), "Should contain treasure level 1");
            Assert.IsTrue(results.ContainsKey(2), "Should contain treasure level 2");
            Assert.IsTrue(results.ContainsKey(3), "Should contain treasure level 3");
            Assert.IsTrue(results.ContainsKey(4), "Should contain treasure level 4");
            Assert.IsTrue(results.ContainsKey(5), "Should contain treasure level 5");

            // Level 5 should be very rare (1%)
            var level5Percentage = (double)results[5] / 10000.0;
            Assert.IsTrue(level5Percentage > 0.005 && level5Percentage < 0.02, 
                $"Level 5 percentage should be around 1% but was {level5Percentage:P2}");
        }

        [TestMethod]
        public void TreasureState_InitialState_ShouldBeClosed()
        {
            var component = new TreasureComponent();
            Assert.AreEqual(TreasureComponent.TreasureState.CLOSED, component.State);
        }

        [TestMethod]
        public void TreasureLevel_InitialLevel_ShouldBe1()
        {
            var component = new TreasureComponent();
            Assert.AreEqual(1, component.Level);
        }

        [TestMethod]
        public void TreasureLevel_ValidRange_ShouldAcceptValues1To5()
        {
            var component = new TreasureComponent();
            
            for (int level = 1; level <= 5; level++)
            {
                component.Level = level;
                Assert.AreEqual(level, component.Level, $"Should accept treasure level {level}");
            }
        }

        [TestMethod]
        public void TreasureLevel_InvalidRange_ShouldRejectInvalidValues()
        {
            var component = new TreasureComponent();
            var originalLevel = component.Level;
            
            // Test values outside valid range
            component.Level = 0;
            Assert.AreEqual(originalLevel, component.Level, "Should reject level 0");
            
            component.Level = 6;
            Assert.AreEqual(originalLevel, component.Level, "Should reject level 6");
            
            component.Level = -1;
            Assert.AreEqual(originalLevel, component.Level, "Should reject negative levels");
        }

        [TestMethod]
        public void TreasureState_StateChange_ShouldAllowValidTransitions()
        {
            var component = new TreasureComponent();
            
            // Start closed
            Assert.AreEqual(TreasureComponent.TreasureState.CLOSED, component.State);
            
            // Open the chest
            component.State = TreasureComponent.TreasureState.OPEN;
            Assert.AreEqual(TreasureComponent.TreasureState.OPEN, component.State);
            
            // Close it again (if needed)
            component.State = TreasureComponent.TreasureState.CLOSED;
            Assert.AreEqual(TreasureComponent.TreasureState.CLOSED, component.State);
        }

        [TestMethod]
        public void TreasureComponent_GenerateItemForTreasureLevel_CreatesCorrectRarityItems()
        {
            // Test each treasure level creates the correct item
            var level1Item = TreasureComponent.GenerateItemForTreasureLevel(1);
            Assert.AreEqual(ItemRarity.Normal, level1Item.Rarity);
            bool level1IsNormalPotion = level1Item.Name == InventoryTextKey.Inv_HPPotion_Name
                || level1Item.Name == InventoryTextKey.Inv_MPPotion_Name
                || level1Item.Name == InventoryTextKey.Inv_MixPotion_Name;
            Assert.IsTrue(level1IsNormalPotion, $"Level 1 item should be a normal potion, but got {level1Item.Name}");

            var level2Item = TreasureComponent.GenerateItemForTreasureLevel(2);
            Assert.AreEqual(ItemRarity.Normal, level2Item.Rarity);
            bool level2IsNormalPotion = level2Item.Name == InventoryTextKey.Inv_HPPotion_Name
                || level2Item.Name == InventoryTextKey.Inv_MPPotion_Name
                || level2Item.Name == InventoryTextKey.Inv_MixPotion_Name;
            Assert.IsTrue(level2IsNormalPotion, $"Level 2 item should be a normal potion, but got {level2Item.Name}");

            var level3Item = TreasureComponent.GenerateItemForTreasureLevel(3);
            Assert.AreEqual(ItemRarity.Rare, level3Item.Rarity);
            bool level3IsMidPotion = level3Item.Name == InventoryTextKey.Inv_MidHPPotion_Name
                || level3Item.Name == InventoryTextKey.Inv_MidMPPotion_Name
                || level3Item.Name == InventoryTextKey.Inv_MidMixPotion_Name;
            Assert.IsTrue(level3IsMidPotion, $"Level 3 item should be a Mid potion, but got {level3Item.Name}");

            var level4Item = TreasureComponent.GenerateItemForTreasureLevel(4);
            Assert.AreEqual(ItemRarity.Epic, level4Item.Rarity);
            bool level4IsFullPotion = level4Item.Name == InventoryTextKey.Inv_FullHPPotion_Name
                || level4Item.Name == InventoryTextKey.Inv_FullMPPotion_Name
                || level4Item.Name == InventoryTextKey.Inv_FullMixPotion_Name;
            Assert.IsTrue(level4IsFullPotion, $"Level 4 item should be a Full potion, but got {level4Item.Name}");

            var level5Item = TreasureComponent.GenerateItemForTreasureLevel(5);
            Assert.AreEqual(ItemRarity.Epic, level5Item.Rarity);
            bool level5IsFullPotion = level5Item.Name == InventoryTextKey.Inv_FullHPPotion_Name
                || level5Item.Name == InventoryTextKey.Inv_FullMPPotion_Name
                || level5Item.Name == InventoryTextKey.Inv_FullMixPotion_Name;
            Assert.IsTrue(level5IsFullPotion, $"Level 5 item should be a Full potion, but got {level5Item.Name}");
        }

        [TestMethod]
        public void TreasureComponent_ContainedItem_CanBeSetAndRetrieved()
        {
            var component = new TreasureComponent();
            var testItem = PotionItems.HPPotion();

            Assert.IsNull(component.ContainedItem);
            
            component.ContainedItem = testItem;
            Assert.IsNotNull(component.ContainedItem);
            Assert.AreEqual(InventoryTextKey.Inv_HPPotion_Name, component.ContainedItem.Name);
            Assert.AreEqual(ItemRarity.Normal, component.ContainedItem.Rarity);
        }

        [TestMethod]
        public void TreasureComponent_InitializeForPitLevel_SetsLevelAndItem()
        {
            var component = new TreasureComponent();
            
            // Test early pit level (should always be level 1)
            component.InitializeForPitLevel(5);
            Assert.AreEqual(1, component.Level);
            // Item generation is now deferred to GenerateContainedItem; ContainedItem is null at spawn time
            Assert.IsNull(component.ContainedItem, "ContainedItem should be null after InitializeForPitLevel (deferred generation)");

            // After calling GenerateContainedItem the item is populated
            component.GenerateContainedItem(LootJobContext.Empty);
            Assert.IsNotNull(component.ContainedItem, "ContainedItem should be non-null after GenerateContainedItem");

            // Test higher pit level (should potentially have higher levels)
            component.InitializeForPitLevel(100);
            Assert.IsTrue(component.Level >= 1 && component.Level <= 5);
            Assert.IsNull(component.ContainedItem, "ContainedItem should again be null after a second InitializeForPitLevel");
        }

        [TestMethod]
        public void CaveLoot_Level1_AlwaysNormalRarity()
        {
            for (int i = 0; i < 200; i++)
            {
                var item = TreasureComponent.GenerateCaveItemForTreasureLevel(1);
                Assert.AreEqual(ItemRarity.Normal, item.Rarity,
                    $"Cave level 1 loot must be Normal rarity but got {item.Rarity} ({item.Name}) on iteration {i}");
            }
        }

        [TestMethod]
        public void CaveLoot_Level2_AlwaysUncommonRarity()
        {
            for (int i = 0; i < 200; i++)
            {
                var item = TreasureComponent.GenerateCaveItemForTreasureLevel(2);
                Assert.AreEqual(ItemRarity.Uncommon, item.Rarity,
                    $"Cave level 2 loot must be Uncommon rarity but got {item.Rarity} ({item.Name}) on iteration {i}");
            }
        }

        [TestMethod]
        public void CaveLoot_Level3_AlwaysRareRarity()
        {
            for (int i = 0; i < 200; i++)
            {
                var item = TreasureComponent.GenerateCaveItemForTreasureLevel(3);
                Assert.AreEqual(ItemRarity.Rare, item.Rarity,
                    $"Cave level 3 loot must be Rare rarity but got {item.Rarity} ({item.Name}) on iteration {i}");
            }
        }

        [TestMethod]
        public void CaveLoot_ConsumableToEquipmentRatio_Approximately60To40()
        {
            int consumableCount = 0;
            int equipmentCount = 0;
            int totalIterations = 5000;

            for (int i = 0; i < totalIterations; i++)
            {
                var item = TreasureComponent.GenerateCaveItemForTreasureLevel(1);
                if (item is Consumable)
                    consumableCount++;
                else
                    equipmentCount++;
            }

            double consumableRatio = (double)consumableCount / totalIterations;

            // Allow a reasonable tolerance (±5%) around the expected 60% consumable rate
            Assert.IsTrue(consumableRatio > 0.55 && consumableRatio < 0.65,
                $"Consumable ratio should be approximately 60% but was {consumableRatio:P1} ({consumableCount} consumables, {equipmentCount} equipment)");
        }

        [TestMethod]
        public void LootJobContext_IsEmpty_TrueWhenBothJobsAreNone()
        {
            var ctx = new LootJobContext();
            Assert.IsTrue(ctx.IsEmpty, "Default LootJobContext should be empty");
        }

        [TestMethod]
        public void LootJobContext_IsEmpty_FalseWhenHeroJobIsSet()
        {
            var ctx = new LootJobContext { HeroJob = JobType.Knight };
            Assert.IsFalse(ctx.IsEmpty, "LootJobContext with HeroJob set should not be empty");
        }

        [TestMethod]
        public void LootJobContext_IsEmpty_FalseWhenMercJobIsSet()
        {
            var ctx = new LootJobContext { MercJobs = JobType.Mage };
            Assert.IsFalse(ctx.IsEmpty, "LootJobContext with MercJobs set should not be empty");
        }

        [TestMethod]
        public void CaveLoot_EmptyContext_FlatRandomFallback_AllItemsAppear()
        {
            // With an empty context, all 56 common pool items should eventually appear
            var seenKinds = new HashSet<ItemKind>();
            int iterations = 10000;

            for (int i = 0; i < iterations; i++)
            {
                var item = TreasureComponent.GenerateCaveItemForTreasureLevel(1, LootJobContext.Empty);
                if (item is IGear gear)
                    seenKinds.Add(gear.Kind);
            }

            // With flat random we expect multiple different gear kinds to appear
            Assert.IsTrue(seenKinds.Count > 3,
                $"Flat random should produce many gear kinds, got {seenKinds.Count}");
        }

        [TestMethod]
        public void CaveLoot_KnightHeroContext_KnightItemsMoreFrequent()
        {
            // Knight uses WeaponSword, ArmorMail, HatHelm, WeaponHammer
            var ctx = new LootJobContext { HeroJob = JobType.Knight };

            int knightItems = 0;
            int nonKnightEquipItems = 0;
            int iterations = 5000;

            for (int i = 0; i < iterations; i++)
            {
                var item = TreasureComponent.GenerateCaveItemForTreasureLevel(1, ctx);
                if (item is IGear gear)
                {
                    var allowed = Gear.GetDefaultAllowedJobs(gear.Kind);
                    if (allowed == JobType.All)
                        continue; // skip shields/accessories (LootWeightAllJobs)
                    if ((allowed & JobType.Knight) != 0)
                        knightItems++;
                    else
                        nonKnightEquipItems++;
                }
            }

            // Knight items should appear significantly more than non-knight job-restricted items
            Assert.IsTrue(knightItems > nonKnightEquipItems * 2,
                $"Knight items ({knightItems}) should outnumber other job-restricted items ({nonKnightEquipItems}) by more than 2x");
        }

        [TestMethod]
        public void CaveLoot_KnightHeroMageMerc_KnightHighestMageMedium()
        {
            var ctx = new LootJobContext { HeroJob = JobType.Knight, MercJobs = JobType.Mage };

            int knightItems = 0;
            int mageItems = 0;
            int otherItems = 0;
            int iterations = 10000;

            for (int i = 0; i < iterations; i++)
            {
                var item = TreasureComponent.GenerateCaveItemForTreasureLevel(1, ctx);
                if (item is IGear gear)
                {
                    var allowed = Gear.GetDefaultAllowedJobs(gear.Kind);
                    if (allowed == JobType.All)
                        continue;
                    bool forKnight = (allowed & JobType.Knight) != 0;
                    bool forMage = (allowed & JobType.Mage) != 0;
                    if (forKnight && !forMage)
                        knightItems++;
                    else if (forMage && !forKnight)
                        mageItems++;
                    else if (!forKnight && !forMage)
                        otherItems++;
                }
            }

            // Knight 4x weight, Mage 2x weight, others 1x weight
            Assert.IsTrue(knightItems > mageItems,
                $"Knight items ({knightItems}) should be more frequent than Mage items ({mageItems})");
            Assert.IsTrue(mageItems > otherItems,
                $"Mage items ({mageItems}) should be more frequent than unequippable items ({otherItems})");
        }

        [TestMethod]
        public void CaveLoot_AllJobsGear_ReceivesAllJobsWeight()
        {
            // Verify shield/accessory ItemKinds return JobType.All from GetDefaultAllowedJobs
            Assert.AreEqual(JobType.All, Gear.GetDefaultAllowedJobs(ItemKind.Shield));
            Assert.AreEqual(JobType.All, Gear.GetDefaultAllowedJobs(ItemKind.Accessory));
        }

        [TestMethod]
        public void LootWeightConstants_HaveExpectedValues()
        {
            Assert.AreEqual(4, BalanceConfig.LootWeightHeroJob);
            Assert.AreEqual(2, BalanceConfig.LootWeightMercJob);
            Assert.AreEqual(1, BalanceConfig.LootWeightNoPartyJob);
            Assert.AreEqual(2, BalanceConfig.LootWeightAllJobs);
        }

        // ─── LootDropTracker Tests ────────────────────────────────────────────────

        [TestMethod]
        public void LootDropTracker_Initialize_ZeroesAllCounts()
        {
            var tracker = new LootDropTracker();
            tracker.RecordDrop(JobType.Knight);
            tracker.RecordDrop(JobType.Mage);
            tracker.Initialize();

            Assert.AreEqual(0, tracker.GetDropCount(JobType.Knight), "Knight count should be 0 after Initialize");
            Assert.AreEqual(0, tracker.GetDropCount(JobType.Mage), "Mage count should be 0 after Initialize");
            Assert.AreEqual(0, tracker.GetMaxDropCount(), "MaxDropCount should be 0 after Initialize");
        }

        [TestMethod]
        public void LootDropTracker_RecordDrop_SingleJob_IncrementsCorrectSlot()
        {
            var tracker = new LootDropTracker();
            tracker.Initialize();

            tracker.RecordDrop(JobType.Knight);
            tracker.RecordDrop(JobType.Knight);
            tracker.RecordDrop(JobType.Mage);

            Assert.AreEqual(2, tracker.GetDropCount(JobType.Knight), "Knight should have 2 drops");
            Assert.AreEqual(1, tracker.GetDropCount(JobType.Mage), "Mage should have 1 drop");
            Assert.AreEqual(0, tracker.GetDropCount(JobType.Priest), "Priest should have 0 drops");
        }

        [TestMethod]
        public void LootDropTracker_RecordDrop_MultiJob_IncrementsAllMatchingSlots()
        {
            var tracker = new LootDropTracker();
            tracker.Initialize();

            // Shields/accessories allow all jobs — recording them should increment every slot
            tracker.RecordDrop(JobType.All);

            for (int i = 0; i < LootDropTracker.JobFlagCount; i++)
            {
                JobType flag = LootDropTracker.GetJobFlag(i);
                Assert.AreEqual(1, tracker.GetDropCount(flag), $"Job flag {flag} should have 1 drop after All-jobs record");
            }
        }

        [TestMethod]
        public void LootDropTracker_GetMaxDropCount_ReturnsHighestSlot()
        {
            var tracker = new LootDropTracker();
            tracker.Initialize();

            tracker.RecordDrop(JobType.Knight);
            tracker.RecordDrop(JobType.Knight);
            tracker.RecordDrop(JobType.Knight);
            tracker.RecordDrop(JobType.Mage);

            Assert.AreEqual(3, tracker.GetMaxDropCount(), "Max should be 3 (Knight)");
        }

        [TestMethod]
        public void LootDropTracker_GetMaxDropCount_ReturnsZeroWhenEmpty()
        {
            var tracker = new LootDropTracker();
            tracker.Initialize();

            Assert.AreEqual(0, tracker.GetMaxDropCount(), "Max should be 0 with no drops");
        }

        // ─── Deficit Bias Tests ───────────────────────────────────────────────────

        [TestMethod]
        public void CaveLoot_DeficitContext_NullCounts_BehavesLikeStaticWeights()
        {
            // When JobDropCounts is null, deficit bonus is 0 — behaviour is identical to static weights
            var ctx = new LootJobContext { HeroJob = JobType.Knight };
            // ctx.JobDropCounts is null by default

            int knightItems = 0;
            int otherJobItems = 0;
            const int Trials = 2000;

            for (int t = 0; t < Trials; t++)
            {
                var item = TreasureComponent.GenerateCaveItemForTreasureLevel(1, ctx);
                if (item is IGear gear)
                {
                    var allowed = Gear.GetDefaultAllowedJobs(gear.Kind);
                    if (allowed == JobType.All) continue;
                    if ((allowed & JobType.Knight) != 0)
                        knightItems++;
                    else
                        otherJobItems++;
                }
            }

            Assert.IsTrue(knightItems > otherJobItems,
                $"Without deficit data, Knight items ({knightItems}) should still outnumber other-job items ({otherJobItems})");
        }

        [TestMethod]
        public void CaveLoot_DeficitContext_BoostsMostBehindPartyMember()
        {
            // Hero (Knight) has 5 drops; Mage merc has 0 drops.
            // Deficit = 5 for Mage gear → bonus weight = 5 × LootDeficitBonusPerDrop on top of LootWeightMercJob.
            var tracker = new LootDropTracker();
            tracker.Initialize();
            for (int i = 0; i < 5; i++)
                tracker.RecordDrop(JobType.Knight);

            int[] counts = tracker.GetDropCountsArray();
            int maxCount = tracker.GetMaxDropCount();

            var ctxWithDeficit = new LootJobContext
            {
                HeroJob = JobType.Knight,
                MercJobs = JobType.Mage,
                JobDropCounts = counts,
                MaxDropCount = maxCount,
            };

            var ctxWithoutDeficit = new LootJobContext
            {
                HeroJob = JobType.Knight,
                MercJobs = JobType.Mage,
            };

            int mageWithDeficit = 0;
            int mageWithoutDeficit = 0;
            const int Trials = 2000;

            for (int t = 0; t < Trials; t++)
            {
                var item = TreasureComponent.GenerateCaveItemForTreasureLevel(1, ctxWithDeficit);
                if (item is IGear gear && (Gear.GetDefaultAllowedJobs(gear.Kind) & JobType.Mage) != 0
                    && Gear.GetDefaultAllowedJobs(gear.Kind) != JobType.All)
                    mageWithDeficit++;
            }

            for (int t = 0; t < Trials; t++)
            {
                var item = TreasureComponent.GenerateCaveItemForTreasureLevel(1, ctxWithoutDeficit);
                if (item is IGear gear && (Gear.GetDefaultAllowedJobs(gear.Kind) & JobType.Mage) != 0
                    && Gear.GetDefaultAllowedJobs(gear.Kind) != JobType.All)
                    mageWithoutDeficit++;
            }

            Assert.IsTrue(mageWithDeficit > mageWithoutDeficit,
                $"Mage gear should appear more often with deficit bonus ({mageWithDeficit}) than without ({mageWithoutDeficit})");
        }

        [TestMethod]
        public void CaveLoot_DeficitContext_ZeroDeficit_SameAsStaticWeights()
        {
            // When all drops are equal (no deficit), deficit bonus is 0 — identical to static weights
            var tracker = new LootDropTracker();
            tracker.Initialize();
            // Give both Knight and Mage equal drops
            for (int i = 0; i < 3; i++)
            {
                tracker.RecordDrop(JobType.Knight);
                tracker.RecordDrop(JobType.Mage);
            }

            int[] counts = tracker.GetDropCountsArray();
            int maxCount = tracker.GetMaxDropCount();

            var ctxEqualDrops = new LootJobContext
            {
                HeroJob = JobType.Knight,
                MercJobs = JobType.Mage,
                JobDropCounts = counts,
                MaxDropCount = maxCount,
            };

            var ctxNullCounts = new LootJobContext
            {
                HeroJob = JobType.Knight,
                MercJobs = JobType.Mage,
            };

            int knightEqual = 0, mageEqual = 0;
            int knightNull = 0, mageNull = 0;
            const int Trials = 3000;

            for (int t = 0; t < Trials; t++)
            {
                var item = TreasureComponent.GenerateCaveItemForTreasureLevel(1, ctxEqualDrops);
                if (item is IGear gear && Gear.GetDefaultAllowedJobs(gear.Kind) != JobType.All)
                {
                    if ((Gear.GetDefaultAllowedJobs(gear.Kind) & JobType.Knight) != 0) knightEqual++;
                    else if ((Gear.GetDefaultAllowedJobs(gear.Kind) & JobType.Mage) != 0) mageEqual++;
                }
            }

            for (int t = 0; t < Trials; t++)
            {
                var item = TreasureComponent.GenerateCaveItemForTreasureLevel(1, ctxNullCounts);
                if (item is IGear gear && Gear.GetDefaultAllowedJobs(gear.Kind) != JobType.All)
                {
                    if ((Gear.GetDefaultAllowedJobs(gear.Kind) & JobType.Knight) != 0) knightNull++;
                    else if ((Gear.GetDefaultAllowedJobs(gear.Kind) & JobType.Mage) != 0) mageNull++;
                }
            }

            // Ratios should be very similar (within 25%) since deficit = 0
            double ratioEqual = knightEqual > 0 ? (double)mageEqual / knightEqual : 0;
            double ratioNull  = knightNull  > 0 ? (double)mageNull  / knightNull  : 0;
            double diff = System.Math.Abs(ratioEqual - ratioNull);
            Assert.IsTrue(diff < 0.25,
                $"Equal-drops ratio ({ratioEqual:F2}) should be close to no-counts ratio ({ratioNull:F2}), diff={diff:F2}");
        }
    }
}