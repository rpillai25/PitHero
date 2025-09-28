using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using PitHero.ECS.Components;
using RolePlayingFramework.Equipment;
using System.Collections.Generic;
using System.Linq;

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
                var treasureLevel = TreasureComponent.DetermineTreasureLevel(25); // Mid-range
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
            // Test each treasure level creates the correct rarity item
            var level1Item = TreasureComponent.GenerateItemForTreasureLevel(1);
            Assert.AreEqual(ItemRarity.Normal, level1Item.Rarity);
            Assert.AreEqual("Standard Bag", level1Item.Name);

            var level2Item = TreasureComponent.GenerateItemForTreasureLevel(2);
            Assert.AreEqual(ItemRarity.Uncommon, level2Item.Rarity);
            Assert.AreEqual("Forager's Bag", level2Item.Name);

            var level3Item = TreasureComponent.GenerateItemForTreasureLevel(3);
            Assert.AreEqual(ItemRarity.Rare, level3Item.Rarity);
            Assert.AreEqual("Traveller's Bag", level3Item.Name);

            var level4Item = TreasureComponent.GenerateItemForTreasureLevel(4);
            Assert.AreEqual(ItemRarity.Epic, level4Item.Rarity);
            Assert.AreEqual("Adventurer's Bag", level4Item.Name);

            var level5Item = TreasureComponent.GenerateItemForTreasureLevel(5);
            Assert.AreEqual(ItemRarity.Legendary, level5Item.Rarity);
            Assert.AreEqual("Merchant's Bag", level5Item.Name);
        }

        [TestMethod]
        public void TreasureComponent_ContainedItem_CanBeSetAndRetrieved()
        {
            var component = new TreasureComponent();
            var testItem = BagItems.ForagersBag();

            Assert.IsNull(component.ContainedItem);
            
            component.ContainedItem = testItem;
            Assert.IsNotNull(component.ContainedItem);
            Assert.AreEqual("Forager's Bag", component.ContainedItem.Name);
            Assert.AreEqual(ItemRarity.Uncommon, component.ContainedItem.Rarity);
        }

        [TestMethod]
        public void TreasureComponent_InitializeForPitLevel_SetsLevelAndItem()
        {
            var component = new TreasureComponent();
            
            // Test early pit level (should always be level 1)
            component.InitializeForPitLevel(5);
            Assert.AreEqual(1, component.Level);
            Assert.IsNotNull(component.ContainedItem);
            Assert.AreEqual(ItemRarity.Normal, component.ContainedItem.Rarity);

            // Test higher pit level (should potentially have higher levels)
            component.InitializeForPitLevel(100);
            Assert.IsTrue(component.Level >= 1 && component.Level <= 5);
            Assert.IsNotNull(component.ContainedItem);
            
            // Verify that the item rarity matches the treasure level
            var expectedRarity = RarityUtils.GetRarityForTreasureLevel(component.Level);
            Assert.AreEqual(expectedRarity, component.ContainedItem.Rarity);
        }
    }
}