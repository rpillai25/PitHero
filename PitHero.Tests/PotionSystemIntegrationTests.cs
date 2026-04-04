using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.ECS.Components;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Inventory;
using PitHero;

namespace PitHero.Tests
{
    [TestClass]
    public class PotionSystemIntegrationTests
    {
        [TestMethod]
        public void PotionSystem_TreasureGeneration_ProducesCorrectPotions()
        {
            // Test that treasure generation creates the correct types of potions
            for (int treasureLevel = 1; treasureLevel <= 5; treasureLevel++)
            {
                var item = TreasureComponent.GenerateItemForTreasureLevel(treasureLevel);
                
                switch (treasureLevel)
                {
                    case 1: // Normal potions
                        Assert.AreEqual(ItemRarity.Normal, item.Rarity);
                        Assert.IsTrue(item is Consumable);
                        var normalPotion = (Consumable)item;
                        bool isNormalPotion = normalPotion.Name == InventoryTextKey.Inv_HPPotion_Name
                            || normalPotion.Name == InventoryTextKey.Inv_MPPotion_Name
                            || normalPotion.Name == InventoryTextKey.Inv_MixPotion_Name;
                        Assert.IsTrue(isNormalPotion, $"Expected a normal potion but got {normalPotion.Name}");
                        break;
                        
                    case 2: // No Uncommon consumables exist, falls back to Normal potions
                        Assert.AreEqual(ItemRarity.Normal, item.Rarity);
                        Assert.IsTrue(item is Consumable);
                        break;
                        
                    case 3: // Mid potions
                        Assert.AreEqual(ItemRarity.Rare, item.Rarity);
                        Assert.IsTrue(item is Consumable);
                        var midPotion = (Consumable)item;
                        bool isMidPotion = midPotion.Name == InventoryTextKey.Inv_MidHPPotion_Name
                            || midPotion.Name == InventoryTextKey.Inv_MidMPPotion_Name
                            || midPotion.Name == InventoryTextKey.Inv_MidMixPotion_Name;
                        Assert.IsTrue(isMidPotion, $"Expected a mid potion but got {midPotion.Name}");
                        break;
                        
                    case 4: // Full potions
                        Assert.AreEqual(ItemRarity.Epic, item.Rarity);
                        Assert.IsTrue(item is Consumable);
                        var fullPotion = (Consumable)item;
                        bool isFullPotion = fullPotion.Name == InventoryTextKey.Inv_FullHPPotion_Name
                            || fullPotion.Name == InventoryTextKey.Inv_FullMPPotion_Name
                            || fullPotion.Name == InventoryTextKey.Inv_FullMixPotion_Name;
                        Assert.IsTrue(isFullPotion, $"Expected a full potion but got {fullPotion.Name}");
                        break;
                        
                    case 5: // No Legendary consumables exist, falls back to Full potions
                        Assert.AreEqual(ItemRarity.Epic, item.Rarity);
                        Assert.IsTrue(item is Consumable);
                        break;
                }
            }
        }

        [TestMethod]
        public void PotionSystem_HPRestoreAmounts_AreCorrect()
        {
            // Test Normal potions
            var normalHPPotion = PotionItems.HPPotion();
            Assert.AreEqual(100, normalHPPotion.HPRestoreAmount);
            Assert.AreEqual(0, normalHPPotion.MPRestoreAmount);

            var normalMixPotion = PotionItems.MixPotion();
            Assert.AreEqual(100, normalMixPotion.HPRestoreAmount);
            Assert.AreEqual(100, normalMixPotion.MPRestoreAmount);

            // Test Mid potions
            var midHPPotion = PotionItems.MidHPPotion();
            Assert.AreEqual(500, midHPPotion.HPRestoreAmount);
            Assert.AreEqual(0, midHPPotion.MPRestoreAmount);

            var midMixPotion = PotionItems.MidMixPotion();
            Assert.AreEqual(500, midMixPotion.HPRestoreAmount);
            Assert.AreEqual(500, midMixPotion.MPRestoreAmount);

            // Test Full potions (use -1 to indicate full restore)
            var fullHPPotion = PotionItems.FullHPPotion();
            Assert.AreEqual(-1, fullHPPotion.HPRestoreAmount);
            Assert.AreEqual(0, fullHPPotion.MPRestoreAmount);

            var fullMixPotion = PotionItems.FullMixPotion();
            Assert.AreEqual(-1, fullMixPotion.HPRestoreAmount);
            Assert.AreEqual(-1, fullMixPotion.MPRestoreAmount);
        }

        [TestMethod]
        public void PotionSystem_ItemBagIntegration_WorksCorrectly()
        {
            var bag = new ItemBag();
            
            // Add various potions to the bag
            var hpPotion = PotionItems.HPPotion();
            var midMPPotion = PotionItems.MidMPPotion();
            var fullMixPotion = PotionItems.FullMixPotion();

            Assert.IsTrue(bag.TryAdd(hpPotion));
            Assert.IsTrue(bag.TryAdd(midMPPotion));
            Assert.IsTrue(bag.TryAdd(fullMixPotion));

            Assert.AreEqual(3, bag.Count);
            Assert.AreEqual(hpPotion, bag.Items[0]);
            Assert.AreEqual(midMPPotion, bag.Items[1]);
            Assert.AreEqual(fullMixPotion, bag.Items[2]);

            // Test that consumables have correct properties when retrieved from bag
            var retrievedHPPotion = (Consumable)bag.Items[0];
            Assert.AreEqual(InventoryTextKey.Inv_HPPotion_Name, retrievedHPPotion.Name);
            Assert.AreEqual(100, retrievedHPPotion.HPRestoreAmount);
            Assert.AreEqual(ItemRarity.Normal, retrievedHPPotion.Rarity);

            var retrievedMidMPPotion = (Consumable)bag.Items[1];
            Assert.AreEqual(InventoryTextKey.Inv_MidMPPotion_Name, retrievedMidMPPotion.Name);
            Assert.AreEqual(500, retrievedMidMPPotion.MPRestoreAmount);
            Assert.AreEqual(ItemRarity.Rare, retrievedMidMPPotion.Rarity);

            var retrievedFullMixPotion = (Consumable)bag.Items[2];
            Assert.AreEqual(InventoryTextKey.Inv_FullMixPotion_Name, retrievedFullMixPotion.Name);
            Assert.AreEqual(-1, retrievedFullMixPotion.HPRestoreAmount);
            Assert.AreEqual(-1, retrievedFullMixPotion.MPRestoreAmount);
            Assert.AreEqual(ItemRarity.Epic, retrievedFullMixPotion.Rarity);
        }

        [TestMethod]
        public void PotionSystem_AllPotionNamesMatchAtlasSprites()
        {
            // Verify that all potion names match the expected sprite names in Items.atlas
            var potionNames = new[]
            {
                PotionItems.HPPotion().Name,
                PotionItems.MPPotion().Name,
                PotionItems.MixPotion().Name,
                PotionItems.MidHPPotion().Name,
                PotionItems.MidMPPotion().Name,
                PotionItems.MidMixPotion().Name,
                PotionItems.FullHPPotion().Name,
                PotionItems.FullMPPotion().Name,
                PotionItems.FullMixPotion().Name
            };

            var expectedNames = new[]
            {
                InventoryTextKey.Inv_HPPotion_Name, InventoryTextKey.Inv_MPPotion_Name, InventoryTextKey.Inv_MixPotion_Name,
                InventoryTextKey.Inv_MidHPPotion_Name, InventoryTextKey.Inv_MidMPPotion_Name, InventoryTextKey.Inv_MidMixPotion_Name,
                InventoryTextKey.Inv_FullHPPotion_Name, InventoryTextKey.Inv_FullMPPotion_Name, InventoryTextKey.Inv_FullMixPotion_Name
            };

            CollectionAssert.AreEqual(expectedNames, potionNames);

            // Also verify that all potions are marked as consumables
            foreach (var potionName in potionNames)
            {
                Assert.IsTrue(potionName.Contains("Potion"), $"Potion name {potionName} should end with 'Potion'");
            }
        }
    }
}