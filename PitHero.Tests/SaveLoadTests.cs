using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using Nez.Persistence.Binary;
using PitHero.Services;
using RolePlayingFramework;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Jobs;
using RolePlayingFramework.Jobs.Primary;
using System;
using System.Collections.Generic;
using System.IO;
using PitHero;
using System.Text;

namespace PitHero.Tests
{
    /// <summary>Tests for the save/load persistence system.</summary>
    [TestClass]
    public class SaveLoadTests
    {
        /// <summary>Verifies SaveData round-trip through FileDataStore preserves all fields.</summary>
        [TestMethod]
        public void SaveData_PersistAndRecover_RoundTrip()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "pithero_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var dataStore = new FileDataStore(tempDir);

                var original = new SaveData();
                original.TotalTimePlayed = 12345.5f;
                original.HeroName = "TestHero";
                original.SkinColor = new Color(100, 150, 200, 255);
                original.HairColor = new Color(50, 60, 70, 255);
                original.HairstyleIndex = 3;
                original.ShirtColor = new Color(10, 20, 30, 255);
                original.JobName = JobTextKey.Job_Knight_Name;
                original.Level = 15;
                original.Experience = 450;
                original.BaseStrength = 20;
                original.BaseAgility = 18;
                original.BaseVitality = 25;
                original.BaseMagic = 10;
                original.CurrentHP = 200;
                original.CurrentMP = 50;

                original.EquipmentNames = new string[] { InventoryTextKey.Inv_RustyBlade_Name, "", InventoryTextKey.Inv_SquireHelm_Name, "", "", "" };

                original.HasCrystal = true;
                original.CrystalJobName = JobTextKey.Job_Knight_Name;
                original.CrystalLevel = 15;
                original.CrystalBaseStrength = 4;
                original.CrystalBaseAgility = 3;
                original.CrystalBaseVitality = 5;
                original.CrystalBaseMagic = 1;
                original.TotalJP = 550;
                original.CurrentJP = 100;
                original.LearnedSkillIds = new List<string> { "skill_a", "skill_b" };
                original.SynergyPoints = new Dictionary<string, int> { { "syn1", 50 } };
                original.LearnedSynergySkillIds = new List<string> { "syn_skill_1" };
                original.DiscoveredSynergyIds = new List<string> { "syn1", "syn2" };

                original.Funds = 999;
                original.DiscoveredStencils = new Dictionary<string, int> { { "stencil_a", 1 } };
                original.PitLevel = 7;

                original.Priority1 = 0;
                original.Priority2 = 1;
                original.Priority3 = 2;
                original.HealPriority1 = 0;
                original.HealPriority2 = 1;
                original.HealPriority3 = 2;

                original.InventoryItems = new List<SavedItem>
                {
                    new SavedItem { Name = InventoryTextKey.Inv_HPPotion_Name, IsConsumable = true, StackCount = 5, SlotIndex = 0 },
                    new SavedItem { Name = InventoryTextKey.Inv_RustyBlade_Name, IsConsumable = false, StackCount = 0, SlotIndex = 3 }
                };

                original.AlliedMonsters = new List<SavedAlliedMonster>
                {
                    new SavedAlliedMonster { Name = "Bob", MonsterTypeName = MonsterTextKey.Monster_Slime, FishingProficiency = 3, CookingProficiency = 5, FarmingProficiency = 7 }
                };

                dataStore.Save("test_save.bin", original);

                var loaded = new SaveData();
                dataStore.Load("test_save.bin", loaded);

                Assert.AreEqual(original.TotalTimePlayed, loaded.TotalTimePlayed);
                Assert.AreEqual(original.HeroName, loaded.HeroName);
                Assert.AreEqual(original.SkinColor, loaded.SkinColor);
                Assert.AreEqual(original.HairColor, loaded.HairColor);
                Assert.AreEqual(original.HairstyleIndex, loaded.HairstyleIndex);
                Assert.AreEqual(original.ShirtColor, loaded.ShirtColor);
                Assert.AreEqual(original.JobName, loaded.JobName);
                Assert.AreEqual(original.Level, loaded.Level);
                Assert.AreEqual(original.Experience, loaded.Experience);
                Assert.AreEqual(original.BaseStrength, loaded.BaseStrength);
                Assert.AreEqual(original.BaseAgility, loaded.BaseAgility);
                Assert.AreEqual(original.BaseVitality, loaded.BaseVitality);
                Assert.AreEqual(original.BaseMagic, loaded.BaseMagic);
                Assert.AreEqual(original.CurrentHP, loaded.CurrentHP);
                Assert.AreEqual(original.CurrentMP, loaded.CurrentMP);

                for (int i = 0; i < 6; i++)
                    Assert.AreEqual(original.EquipmentNames[i], loaded.EquipmentNames[i] ?? "");

                Assert.AreEqual(original.HasCrystal, loaded.HasCrystal);
                Assert.AreEqual(original.CrystalJobName, loaded.CrystalJobName);
                Assert.AreEqual(original.TotalJP, loaded.TotalJP);
                Assert.AreEqual(original.CurrentJP, loaded.CurrentJP);
                Assert.AreEqual(original.LearnedSkillIds.Count, loaded.LearnedSkillIds.Count);
                Assert.AreEqual(original.SynergyPoints.Count, loaded.SynergyPoints.Count);

                Assert.AreEqual(original.Funds, loaded.Funds);
                Assert.AreEqual(original.PitLevel, loaded.PitLevel);

                Assert.AreEqual(original.InventoryItems.Count, loaded.InventoryItems.Count);
                Assert.AreEqual(original.InventoryItems[0].Name, loaded.InventoryItems[0].Name);
                Assert.AreEqual(original.InventoryItems[0].StackCount, loaded.InventoryItems[0].StackCount);
                Assert.AreEqual(original.InventoryItems[0].SlotIndex, loaded.InventoryItems[0].SlotIndex);
                Assert.AreEqual(original.InventoryItems[1].SlotIndex, loaded.InventoryItems[1].SlotIndex);

                Assert.AreEqual(original.AlliedMonsters.Count, loaded.AlliedMonsters.Count);
                Assert.AreEqual(original.AlliedMonsters[0].Name, loaded.AlliedMonsters[0].Name);
                Assert.AreEqual(original.AlliedMonsters[0].MonsterTypeName, loaded.AlliedMonsters[0].MonsterTypeName);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        /// <summary>Verifies the refrigerator section round-trips through save/load (issue #386, v28).</summary>
        [TestMethod]
        public void SaveData_Refrigerator_RoundTrip()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "pithero_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var dataStore = new FileDataStore(tempDir);

                var original = new SaveData();
                original.FridgeSlots = new List<SavedHarvestSlot>
                {
                    new SavedHarvestSlot { SlotIndex = 0, CropTypeId = 1, Count = 10 },
                    new SavedHarvestSlot { SlotIndex = 5, CropTypeId = 3, Count = 4 },
                };
                original.FridgePreStockStackSize = 3;
                original.RunnerCarryLevel = 2;

                dataStore.Save("fridge_save.bin", original);

                var loaded = new SaveData();
                dataStore.Load("fridge_save.bin", loaded);

                Assert.AreEqual(2, loaded.FridgeSlots.Count);
                Assert.AreEqual(0, loaded.FridgeSlots[0].SlotIndex);
                Assert.AreEqual(1, loaded.FridgeSlots[0].CropTypeId);
                Assert.AreEqual(10, loaded.FridgeSlots[0].Count);
                Assert.AreEqual(5, loaded.FridgeSlots[1].SlotIndex);
                Assert.AreEqual(3, loaded.FridgeSlots[1].CropTypeId);
                Assert.AreEqual(4, loaded.FridgeSlots[1].Count);
                Assert.AreEqual(3, loaded.FridgePreStockStackSize);
                Assert.AreEqual(2, loaded.RunnerCarryLevel);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        /// <summary>A fresh SaveData defaults the refrigerator to empty with the slider at 1.</summary>
        [TestMethod]
        public void SaveData_Refrigerator_DefaultsEmpty()
        {
            var data = new SaveData();
            Assert.IsNotNull(data.FridgeSlots);
            Assert.AreEqual(0, data.FridgeSlots.Count);
            Assert.AreEqual(1, data.FridgePreStockStackSize);
            Assert.AreEqual(1, data.RunnerCarryLevel);
        }

        /// <summary>Verifies the defeated-monster set round-trips through save/load (issue #283, v11).</summary>
        [TestMethod]
        public void SaveData_DefeatedMonsterTypes_RoundTrip()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "pithero_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var dataStore = new FileDataStore(tempDir);

                var original = new SaveData();
                original.DefeatedMonsterTypes = new List<string> { "Slime", "Orc", "GhostMiner" };

                dataStore.Save("defeated_save.bin", original);

                var loaded = new SaveData();
                dataStore.Load("defeated_save.bin", loaded);

                CollectionAssert.AreEqual(original.DefeatedMonsterTypes, loaded.DefeatedMonsterTypes);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        /// <summary>A fresh SaveData defaults DefeatedMonsterTypes to an empty (non-null) list.</summary>
        [TestMethod]
        public void SaveData_DefeatedMonsterTypes_DefaultsEmpty()
        {
            var data = new SaveData();
            Assert.IsNotNull(data.DefeatedMonsterTypes);
            Assert.AreEqual(0, data.DefeatedMonsterTypes.Count);
        }

        /// <summary>Verifies SaveData handles empty/minimal data correctly.</summary>
        [TestMethod]
        public void SaveData_PersistAndRecover_HandlesEmptyData()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "pithero_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var dataStore = new FileDataStore(tempDir);

                var original = new SaveData();
                original.HeroName = "EmptyHero";
                original.JobName = JobTextKey.Job_Mage_Name;
                original.Level = 1;

                dataStore.Save("test_empty.bin", original);

                var loaded = new SaveData();
                dataStore.Load("test_empty.bin", loaded);

                Assert.AreEqual("EmptyHero", loaded.HeroName);
                Assert.AreEqual(JobTextKey.Job_Mage_Name, loaded.JobName);
                Assert.AreEqual(1, loaded.Level);
                Assert.AreEqual(false, loaded.HasCrystal);
                Assert.AreEqual(0, loaded.InventoryItems.Count);
                Assert.AreEqual(0, loaded.AlliedMonsters.Count);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        /// <summary>Verifies ItemRegistry finds known gear items.</summary>
        [TestMethod]
        public void ItemRegistry_TryCreateItem_FindsKnownGearItems()
        {
            Assert.IsTrue(ItemRegistry.TryCreateItem(InventoryTextKey.Inv_RustyBlade_Name, out var sword));
            Assert.IsNotNull(sword);
            Assert.AreEqual(InventoryTextKey.Inv_RustyBlade_Name, sword.Name);
        }

        /// <summary>Verifies ItemRegistry finds known potion items.</summary>
        [TestMethod]
        public void ItemRegistry_TryCreateItem_FindsKnownPotionItems()
        {
            Assert.IsTrue(ItemRegistry.TryCreateItem(InventoryTextKey.Inv_HPPotion_Name, out var potion));
            Assert.IsNotNull(potion);
            Assert.AreEqual(InventoryTextKey.Inv_HPPotion_Name, potion.Name);
        }

        /// <summary>Verifies ItemRegistry returns false for unknown items.</summary>
        [TestMethod]
        public void ItemRegistry_TryCreateItem_ReturnsFalseForUnknownItem()
        {
            Assert.IsFalse(ItemRegistry.TryCreateItem("NonexistentSword", out var item));
            Assert.IsNull(item);
        }

        /// <summary>Verifies JobFactory creates all primary jobs.</summary>
        [TestMethod]
        public void JobFactory_CreateJob_CreatesAllPrimaryJobs()
        {
            var jobNames = new string[] { JobTextKey.Job_Knight_Name, JobTextKey.Job_Mage_Name, JobTextKey.Job_Monk_Name, JobTextKey.Job_Priest_Name, JobTextKey.Job_Archer_Name, JobTextKey.Job_Thief_Name };
            for (int i = 0; i < jobNames.Length; i++)
            {
                var job = JobFactory.CreateJob(jobNames[i]);
                Assert.IsNotNull(job);
                Assert.AreEqual(jobNames[i], job.Name);
            }
        }

        /// <summary>Verifies JobFactory creates composite jobs from hyphenated names.</summary>
        [TestMethod]
        public void JobFactory_CreateJob_CreatesCompositeJob()
        {
            var job = JobFactory.CreateJob($"{JobTextKey.Job_Knight_Name}-{JobTextKey.Job_Mage_Name}");
            Assert.IsNotNull(job);
            Assert.AreEqual($"{JobTextKey.Job_Knight_Name}-{JobTextKey.Job_Mage_Name}", job.NameKey);
        }

        /// <summary>Verifies JobFactory defaults to Knight for unknown job names.</summary>
        [TestMethod]
        public void JobFactory_CreateJob_DefaultsToKnightForUnknown()
        {
            var job = JobFactory.CreateJob("UnknownJob");
            Assert.IsNotNull(job);
            Assert.AreEqual(JobTextKey.Job_Knight_Name, job.Name);
        }

        /// <summary>Verifies non-sequential slot positions survive full ItemBag → SaveData → ItemBag round-trip.</summary>
        [TestMethod]
        public void InventorySlotPositions_NonSequential_PreservedThroughSaveLoad()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "pithero_slot_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                // Step 1: Create a bag and place items at NON-sequential positions (simulating rearrangement)
                var originalBag = new RolePlayingFramework.Inventory.ItemBag("Test Bag", 120);

                var shortSword = GearItems.ShortSword();
                var ironHelm = GearItems.IronHelm();
                var hpPotion = PotionItems.HPPotion();
                hpPotion.StackCount = 3;

                // Place items at non-default positions (as if user rearranged them)
                originalBag.SetSlotItem(15, shortSword);  // Not slot 0
                originalBag.SetSlotItem(42, ironHelm);    // Not slot 1
                originalBag.SetSlotItem(99, hpPotion);    // Not slot 2

                // Step 2: Gather items (same logic as GatherCurrentState)
                var savedItems = new List<SavedItem>();
                for (int i = 0; i < originalBag.Capacity; i++)
                {
                    var item = originalBag.GetSlotItem(i);
                    if (item != null)
                    {
                        var savedItem = new SavedItem();
                        savedItem.Name = item.Name;
                        savedItem.SlotIndex = i;
                        if (item is RolePlayingFramework.Equipment.Consumable c)
                        {
                            savedItem.IsConsumable = true;
                            savedItem.StackCount = c.StackCount;
                        }
                        savedItems.Add(savedItem);
                    }
                }

                // Verify saved positions match original placement
                Assert.AreEqual(3, savedItems.Count);
                Assert.AreEqual(15, savedItems[0].SlotIndex);
                Assert.AreEqual(InventoryTextKey.Inv_ShortSword_Name, savedItems[0].Name);
                Assert.AreEqual(42, savedItems[1].SlotIndex);
                Assert.AreEqual(InventoryTextKey.Inv_IronHelm_Name, savedItems[1].Name);
                Assert.AreEqual(99, savedItems[2].SlotIndex);
                Assert.AreEqual(InventoryTextKey.Inv_HPPotion_Name, savedItems[2].Name);
                Assert.AreEqual(3, savedItems[2].StackCount);

                // Step 3: Save through binary persistence
                var saveData = new SaveData();
                saveData.HeroName = "SlotTest";
                saveData.JobName = JobTextKey.Job_Knight_Name;
                saveData.Level = 1;
                saveData.InventoryItems = savedItems;

                var dataStore = new Nez.Persistence.Binary.FileDataStore(tempDir);
                dataStore.Save("slot_test.bin", saveData);

                // Step 4: Load from file
                var loaded = new SaveData();
                dataStore.Load("slot_test.bin", loaded);

                // Step 5: Verify loaded slot positions
                Assert.AreEqual(3, loaded.InventoryItems.Count);
                Assert.AreEqual(15, loaded.InventoryItems[0].SlotIndex);
                Assert.AreEqual(InventoryTextKey.Inv_ShortSword_Name, loaded.InventoryItems[0].Name);
                Assert.AreEqual(42, loaded.InventoryItems[1].SlotIndex);
                Assert.AreEqual(InventoryTextKey.Inv_IronHelm_Name, loaded.InventoryItems[1].Name);
                Assert.AreEqual(99, loaded.InventoryItems[2].SlotIndex);
                Assert.AreEqual(InventoryTextKey.Inv_HPPotion_Name, loaded.InventoryItems[2].Name);
                Assert.AreEqual(3, loaded.InventoryItems[2].StackCount);

                // Step 6: Restore into a new bag (same logic as ApplyPendingLoadData)
                var restoredBag = new RolePlayingFramework.Inventory.ItemBag("Test Bag", 120);

                // Clear bag first (matches new defensive code)
                for (int i = 0; i < restoredBag.Capacity; i++)
                    restoredBag.SetSlotItem(i, null);

                for (int i = 0; i < loaded.InventoryItems.Count; i++)
                {
                    var savedItem = loaded.InventoryItems[i];
                    if (ItemRegistry.TryCreateItem(savedItem.Name, out var item))
                    {
                        if (savedItem.IsConsumable && item is RolePlayingFramework.Equipment.Consumable consumable)
                            consumable.StackCount = savedItem.StackCount;
                        restoredBag.SetSlotItem(savedItem.SlotIndex, item);
                    }
                }

                // Step 7: Verify items are at correct slot positions in restored bag
                Assert.IsNull(restoredBag.GetSlotItem(0), "Slot 0 should be empty");
                Assert.IsNull(restoredBag.GetSlotItem(1), "Slot 1 should be empty");
                Assert.IsNull(restoredBag.GetSlotItem(14), "Slot 14 should be empty");

                var restoredSword = restoredBag.GetSlotItem(15);
                Assert.IsNotNull(restoredSword, "ShortSword should be at slot 15");
                Assert.AreEqual(InventoryTextKey.Inv_ShortSword_Name, restoredSword.Name);

                Assert.IsNull(restoredBag.GetSlotItem(16), "Slot 16 should be empty");
                Assert.IsNull(restoredBag.GetSlotItem(41), "Slot 41 should be empty");

                var restoredHelm = restoredBag.GetSlotItem(42);
                Assert.IsNotNull(restoredHelm, "IronHelm should be at slot 42");
                Assert.AreEqual(InventoryTextKey.Inv_IronHelm_Name, restoredHelm.Name);

                Assert.IsNull(restoredBag.GetSlotItem(43), "Slot 43 should be empty");
                Assert.IsNull(restoredBag.GetSlotItem(98), "Slot 98 should be empty");

                var restoredPotion = restoredBag.GetSlotItem(99);
                Assert.IsNotNull(restoredPotion, "HPPotion should be at slot 99");
                Assert.AreEqual(InventoryTextKey.Inv_HPPotion_Name, restoredPotion.Name);
                Assert.IsTrue(restoredPotion is RolePlayingFramework.Equipment.Consumable);
                Assert.AreEqual(3, ((RolePlayingFramework.Equipment.Consumable)restoredPotion).StackCount);

                Assert.AreEqual(3, restoredBag.Count, "Bag should have exactly 3 items");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        /// <summary>Verifies shortcut bar data survives full SaveData round-trip through binary persistence.</summary>
        [TestMethod]
        public void ShortcutBarSlots_PreservedThroughSaveLoad()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "pithero_shortcut_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var saveData = new SaveData();
                saveData.HeroName = "ShortcutTest";
                saveData.JobName = JobTextKey.Job_Knight_Name;
                saveData.Level = 1;

                // Set up shortcut slots: empty, item, skill, item, empty, skill, empty, empty
                saveData.ShortcutSlots = new List<SavedShortcutSlot>
                {
                    new SavedShortcutSlot { SlotType = 0 },                                                        // Empty
                    new SavedShortcutSlot { SlotType = 1, ItemBagIndex = 5 },                                       // Item at bag index 5
                    new SavedShortcutSlot { SlotType = 2, SkillId = "knight.light_armor", OwnerMercIndex = -1 },     // Hero-owned skill
                    new SavedShortcutSlot { SlotType = 1, ItemBagIndex = 42 },                                      // Item at bag index 42
                    new SavedShortcutSlot { SlotType = 0 },                                                        // Empty
                    new SavedShortcutSlot { SlotType = 2, SkillId = "mage.fire", OwnerMercIndex = 1 },               // Merc-owned skill
                    new SavedShortcutSlot { SlotType = 0 },                                                        // Empty
                    new SavedShortcutSlot { SlotType = 0 },                                                        // Empty
                };

                var dataStore = new FileDataStore(tempDir);
                dataStore.Save("shortcut_test.bin", saveData);

                var loaded = new SaveData();
                dataStore.Load("shortcut_test.bin", loaded);

                Assert.AreEqual(8, loaded.ShortcutSlots.Count, "Should have 8 shortcut slots");

                // Slot 0: Empty
                Assert.AreEqual(0, loaded.ShortcutSlots[0].SlotType, "Slot 0 should be empty");

                // Slot 1: Item at bag index 5
                Assert.AreEqual(1, loaded.ShortcutSlots[1].SlotType, "Slot 1 should be item");
                Assert.AreEqual(5, loaded.ShortcutSlots[1].ItemBagIndex, "Slot 1 should reference bag index 5");

                // Slot 2: Skill (hero-owned)
                Assert.AreEqual(2, loaded.ShortcutSlots[2].SlotType, "Slot 2 should be skill");
                Assert.AreEqual("knight.light_armor", loaded.ShortcutSlots[2].SkillId, "Slot 2 should reference knight.light_armor");
                Assert.AreEqual(-1, loaded.ShortcutSlots[2].OwnerMercIndex, "Slot 2 should be hero-owned");

                // Slot 3: Item at bag index 42
                Assert.AreEqual(1, loaded.ShortcutSlots[3].SlotType, "Slot 3 should be item");
                Assert.AreEqual(42, loaded.ShortcutSlots[3].ItemBagIndex, "Slot 3 should reference bag index 42");

                // Slot 4: Empty
                Assert.AreEqual(0, loaded.ShortcutSlots[4].SlotType, "Slot 4 should be empty");

                // Slot 5: Skill (merc-owned)
                Assert.AreEqual(2, loaded.ShortcutSlots[5].SlotType, "Slot 5 should be skill");
                Assert.AreEqual("mage.fire", loaded.ShortcutSlots[5].SkillId, "Slot 5 should reference mage.fire");
                Assert.AreEqual(1, loaded.ShortcutSlots[5].OwnerMercIndex, "Slot 5 should be owned by merc index 1");

                // Slots 6-7: Empty
                Assert.AreEqual(0, loaded.ShortcutSlots[6].SlotType, "Slot 6 should be empty");
                Assert.AreEqual(0, loaded.ShortcutSlots[7].SlotType, "Slot 7 should be empty");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        /// <summary>Verifies a save with an empty shortcut bar round-trips correctly.</summary>
        [TestMethod]
        public void SaveData_EmptyShortcutSlots_RoundTrip()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "pithero_v1_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var saveData = new SaveData();
                saveData.HeroName = "V1Hero";
                saveData.JobName = JobTextKey.Job_Knight_Name;
                saveData.Level = 1;
                saveData.ShortcutSlots = new List<SavedShortcutSlot>(); // Empty list still writes count=0

                var dataStore = new FileDataStore(tempDir);
                dataStore.Save("v1_test.bin", saveData);

                var loaded = new SaveData();
                dataStore.Load("v1_test.bin", loaded);

                Assert.AreEqual("V1Hero", loaded.HeroName);
                Assert.IsNotNull(loaded.ShortcutSlots, "ShortcutSlots should be initialized");
                Assert.AreEqual(0, loaded.ShortcutSlots.Count, "Empty save should have 0 shortcut slots");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        /// <summary>Verifies PitTier and TierBaseLevel round-trip through save v13.</summary>
        [TestMethod]
        public void SaveData_V13_PitTier_RoundTrip()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "pithero_tier_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var dataStore = new FileDataStore(tempDir);

                var original = new SaveData();
                original.PitLevel = 7;
                original.PitTier = 5;
                original.TierBaseLevel = 30;

                dataStore.Save("tier_save.bin", original);

                var loaded = new SaveData();
                dataStore.Load("tier_save.bin", loaded);

                Assert.AreEqual(7, loaded.PitLevel, "PitLevel should round-trip");
                Assert.AreEqual(5, loaded.PitTier, "PitTier should round-trip");
                Assert.AreEqual(30, loaded.TierBaseLevel, "TierBaseLevel should round-trip");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        /// <summary>
        /// Verifies that a SaveData with default tier values (tier=1, base=1) round-trips correctly.
        /// </summary>
        [TestMethod]
        public void SaveData_V13_DefaultTierValues_RoundTrip()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "pithero_defaulttier_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var dataStore = new FileDataStore(tempDir);

                var original = new SaveData();
                // Default values: PitTier = 1, TierBaseLevel = 1.
                Assert.AreEqual(1, original.PitTier, "Default PitTier should be 1");
                Assert.AreEqual(1, original.TierBaseLevel, "Default TierBaseLevel should be 1");

                dataStore.Save("default_tier.bin", original);

                var loaded = new SaveData();
                dataStore.Load("default_tier.bin", loaded);

                Assert.AreEqual(1, loaded.PitTier, "Loaded PitTier should default to 1");
                Assert.AreEqual(1, loaded.TierBaseLevel, "Loaded TierBaseLevel should default to 1");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        /// <summary>
        /// Verifies that AutomateSeedPurchases and AutoShopGoldBuffer round-trip correctly through
        /// Persist/Recover.
        /// </summary>
        [TestMethod]
        public void SaveData_V15_AutoShopOptions_RoundTrip()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "pithero_v15_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var dataStore = new FileDataStore(tempDir);

                var original = new SaveData();
                original.AutomateSeedPurchases = true;
                original.AutoShopGoldBuffer = 500;

                dataStore.Save("v15_autoshop.bin", original);

                var loaded = new SaveData();
                dataStore.Load("v15_autoshop.bin", loaded);

                Assert.AreEqual(true, loaded.AutomateSeedPurchases, "AutomateSeedPurchases should round-trip");
                Assert.AreEqual(500, loaded.AutoShopGoldBuffer, "AutoShopGoldBuffer should round-trip");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        /// <summary>
        /// Verifies that AutoSellCrops and AutoSellCropDesignations round-trip correctly through
        /// Persist/Recover.
        /// </summary>
        [TestMethod]
        public void SaveData_V16_AutoSellCrops_RoundTrip()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "pithero_v16_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var dataStore = new FileDataStore(tempDir);

                var original = new SaveData();
                original.AutoSellCrops = true;
                original.AutoSellCropDesignations = new bool[PitHero.Farming.CropTypeInfo.Count];
                for (int i = 0; i < original.AutoSellCropDesignations.Length; i++)
                    original.AutoSellCropDesignations[i] = true;
                original.AutoSellCropDesignations[0] = false; // mixed values to prove real round-trip

                dataStore.Save("v16_autosell.bin", original);

                var loaded = new SaveData();
                dataStore.Load("v16_autosell.bin", loaded);

                Assert.AreEqual(true, loaded.AutoSellCrops, "AutoSellCrops should round-trip");
                Assert.IsNotNull(loaded.AutoSellCropDesignations, "Designations should be recovered");
                Assert.AreEqual(false, loaded.AutoSellCropDesignations[0], "Designation[0]=false should round-trip");
                for (int i = 1; i < PitHero.Farming.CropTypeInfo.Count; i++)
                    Assert.AreEqual(true, loaded.AutoSellCropDesignations[i], $"Designation[{i}]=true should round-trip");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        /// <summary>
        /// Verifies that AutoSellExcessItems and AutoSellRarityAllowed (v21) round-trip through
        /// Persist/Recover with non-default values.
        /// </summary>
        [TestMethod]
        public void SaveData_V21_AutoSellExcessItems_RoundTrip()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "pithero_v21_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var dataStore = new FileDataStore(tempDir);

                var original = new SaveData();
                original.AutoSellExcessItems = false;               // non-default (defaults to true)
                original.AutoSellConsumablesFirst = false;          // non-default (defaults to true, v22)
                original.AutoSellRarityAllowed = new bool[] { true, true, true, false, false };

                dataStore.Save("v21_autosellexcess.bin", original);

                var loaded = new SaveData();
                dataStore.Load("v21_autosellexcess.bin", loaded);

                Assert.AreEqual(false, loaded.AutoSellExcessItems, "AutoSellExcessItems=false should round-trip");
                Assert.AreEqual(false, loaded.AutoSellConsumablesFirst, "AutoSellConsumablesFirst=false should round-trip");
                Assert.IsNotNull(loaded.AutoSellRarityAllowed, "RarityAllowed should be recovered");
                Assert.AreEqual(5, loaded.AutoSellRarityAllowed.Length);
                Assert.AreEqual(true, loaded.AutoSellRarityAllowed[(int)ItemRarity.Normal]);
                Assert.AreEqual(false, loaded.AutoSellRarityAllowed[(int)ItemRarity.Epic], "Unchecked Epic should round-trip");
                Assert.AreEqual(false, loaded.AutoSellRarityAllowed[(int)ItemRarity.Legendary], "Unchecked Legendary should round-trip");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        /// <summary>
        /// Verifies the v21 defaults: auto-sell excess items is ON with all rarities allowed,
        /// including when the saved rarity array is absent (older files / null array writes count 0).
        /// </summary>
        [TestMethod]
        public void SaveData_V21_AutoSellExcessItems_Defaults()
        {
            Assert.AreEqual(true, new SaveData().AutoSellExcessItems, "Auto-sell excess items defaults to ON");
            Assert.AreEqual(true, new SaveData().AutoSellConsumablesFirst, "Sell priority defaults to consumables-first");

            var tempDir = Path.Combine(Path.GetTempPath(), "pithero_v21d_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var dataStore = new FileDataStore(tempDir);

                // Null rarity array persists a zero count; Recover must normalize to all-true.
                var original = new SaveData();
                original.AutoSellRarityAllowed = null;

                dataStore.Save("v21_defaults.bin", original);

                var loaded = new SaveData();
                dataStore.Load("v21_defaults.bin", loaded);

                Assert.AreEqual(true, loaded.AutoSellExcessItems);
                Assert.AreEqual(true, loaded.AutoSellConsumablesFirst, "Sell priority should recover as consumables-first by default");
                Assert.IsNotNull(loaded.AutoSellRarityAllowed);
                for (int i = 0; i < loaded.AutoSellRarityAllowed.Length; i++)
                    Assert.AreEqual(true, loaded.AutoSellRarityAllowed[i], $"Rarity {i} should default to allowed");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        /// <summary>
        /// Verifies that AutoSellKeepStacks round-trips through Persist/Recover.
        /// </summary>
        [TestMethod]
        public void SaveData_V17_KeepStacks_RoundTrip()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "pithero_v17_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var dataStore = new FileDataStore(tempDir);

                var original = new SaveData();
                original.AutoSellKeepStacks = 3;

                dataStore.Save("v17_keepstacks.bin", original);

                var loaded = new SaveData();
                dataStore.Load("v17_keepstacks.bin", loaded);

                Assert.AreEqual(3, loaded.AutoSellKeepStacks, "AutoSellKeepStacks should round-trip");
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }

        /// <summary>
        /// Backwards compatibility policy: versions MinSupportedVersion..CurrentVersion load;
        /// anything below MinSupportedVersion or above CurrentVersion is rejected with
        /// InvalidDataException. Simulated by writing a current-version binary and patching the
        /// version bytes.
        /// </summary>
        [TestMethod]
        public void SaveData_UnsupportedVersionHeader_ThrowsInvalidData()
        {
            AssertHeaderRejected(SaveData.MinSupportedVersion - 1, "A version below MinSupportedVersion must be rejected");
            AssertHeaderRejected(SaveData.CurrentVersion + 1, "A newer save version must be rejected");
        }

        /// <summary>Writes a current-version save, patches its version header, and asserts the load throws.</summary>
        private static void AssertHeaderRejected(int patchedVersion, string message)
        {
            var ms = new MemoryStream();
            using (var writer = new BinaryPersistableWriter(ms))
            {
                var original = new SaveData();
                original.HeroName = "OldHero";
                writer.Write(original);
            }

            byte[] bytes = ms.ToArray();

            // Patch bytes [0-3] to the target version (little-endian int)
            bytes[0] = (byte)patchedVersion;
            bytes[1] = 0;
            bytes[2] = 0;
            bytes[3] = 0;

            var loaded = new SaveData();
            using (var rdr = new BinaryPersistableReader(new MemoryStream(bytes)))
            {
                Assert.ThrowsException<InvalidDataException>(() => rdr.ReadPersistableInto(loaded), message);
            }
        }

        /// <summary>
        /// Backwards compatibility (issue #392): a v29 dining record (no MealExpiresAtSeconds)
        /// must still read, defaulting the expiry to 0 so the pre-#392 buff is dropped cleanly.
        /// </summary>
        [TestMethod]
        public void SaveData_V29DiningRecord_ReadsWithDefaultExpiry()
        {
            var ms = new MemoryStream();
            using (var writer = new BinaryPersistableWriter(ms))
            {
                // v29 layout: OrderedDishId, HasPaid, HasEatenThisMeal, MealDishId, MealDeluxe
                writer.Write(3);
                writer.Write(true);
                writer.Write(true);
                writer.Write(5);
                writer.Write(true);
                // Trailing sentinel proves the v29 read consumed exactly the v29 bytes
                writer.Write(42);
            }

            using (var rdr = new BinaryPersistableReader(new MemoryStream(ms.ToArray())))
            {
                var record = SaveData.ReadDiningRecord(rdr, 29);
                Assert.AreEqual(3, record.OrderedDishId, "OrderedDishId should read from v29 layout");
                Assert.AreEqual(true, record.HasPaid, "HasPaid should read from v29 layout");
                Assert.AreEqual(true, record.HasEatenThisMeal, "HasEatenThisMeal should read from v29 layout");
                Assert.AreEqual(5, record.MealDishId, "MealDishId should read from v29 layout");
                Assert.AreEqual(true, record.MealDeluxe, "MealDeluxe should read from v29 layout");
                Assert.AreEqual(0f, record.MealExpiresAtSeconds, "v29 records must default expiry to 0");
                Assert.AreEqual(42, rdr.ReadInt(), "v29 read must not consume bytes past the record");
            }
        }

        /// <summary>The v30 dining record layout reads its own expiry field back.</summary>
        [TestMethod]
        public void SaveData_V30DiningRecord_ReadsExpiry()
        {
            var ms = new MemoryStream();
            using (var writer = new BinaryPersistableWriter(ms))
            {
                writer.Write(3);
                writer.Write(true);
                writer.Write(true);
                writer.Write(5);
                writer.Write(true);
                writer.Write(1234.5f);
            }

            using (var rdr = new BinaryPersistableReader(new MemoryStream(ms.ToArray())))
            {
                var record = SaveData.ReadDiningRecord(rdr, 30);
                Assert.AreEqual(1234.5f, record.MealExpiresAtSeconds, "v30 records must read the expiry stamp");
            }
        }

        /// <summary>
        /// Verifies that PartyAutoDineResume round-trips through Persist/Recover, so a save made
        /// mid-breakfast still auto-resumes the party after reload.
        /// </summary>
        [TestMethod]
        public void SaveData_V20_PartyAutoDineResume_RoundTrip()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "pithero_v20_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var dataStore = new FileDataStore(tempDir);

                var original = new SaveData();
                original.PartyAutoDineResume = true;

                dataStore.Save("v20_autodine.bin", original);

                var loaded = new SaveData();
                dataStore.Load("v20_autodine.bin", loaded);

                Assert.AreEqual(true, loaded.PartyAutoDineResume, "PartyAutoDineResume should round-trip");
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }

        /// <summary>
        /// Verifies that AutomateMonsterJobs round-trips through Persist/Recover (issue #321).
        /// </summary>
        [TestMethod]
        public void SaveData_V19_AutomateMonsterJobs_RoundTrip()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "pithero_v19_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var dataStore = new FileDataStore(tempDir);

                var original = new SaveData();
                original.AutomateMonsterJobs = true;

                dataStore.Save("v19_automation.bin", original);

                var loaded = new SaveData();
                dataStore.Load("v19_automation.bin", loaded);

                Assert.AreEqual(true, loaded.AutomateMonsterJobs, "AutomateMonsterJobs should round-trip");
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }

        /// <summary>
        /// Verifies that SaveLoadService treats a slot holding an incompatible-version file as
        /// empty instead of crashing (previews built in the constructor and explicit loads).
        /// </summary>
        [TestMethod]
        public void SaveLoadService_IncompatibleSlotFile_TreatedAsEmpty()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "pithero_oldslot_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                // Write a valid current-version save, then downgrade its version header
                var ms = new MemoryStream();
                using (var writer = new BinaryPersistableWriter(ms))
                {
                    var original = new SaveData();
                    original.HeroName = "OldHero";
                    writer.Write(original);
                }

                byte[] bytes = ms.ToArray();
                bytes[0] = (byte)(SaveData.MinSupportedVersion - 1);
                bytes[1] = 0;
                bytes[2] = 0;
                bytes[3] = 0;
                File.WriteAllBytes(Path.Combine(tempDir, "save_slot_0.bin"), bytes);

                // Constructor refreshes slot previews — must not throw on the incompatible file
                var service = new SaveLoadService(new FileDataStore(tempDir));

                Assert.IsFalse(service.SlotHasData(0), "Incompatible slot should be treated as empty");
                Assert.IsNull(service.LoadFromSlot(0), "Loading an incompatible slot should return null");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        /// <summary>
        /// Verifies the v23 sections (gear sell types, auto-purchase items, auto-equip) round-trip
        /// through Persist/Recover with non-default values.
        /// </summary>
        [TestMethod]
        public void SaveData_V23_AutomationUpdates_RoundTrip()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "pithero_v23_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var dataStore = new FileDataStore(tempDir);

                var original = new SaveData();

                // 38. Gear sell types
                original.AutoSellGearTypeAllowed = new bool[GearCategoryUtils.Count];
                for (int i = 0; i < original.AutoSellGearTypeAllowed.Length; i++)
                    original.AutoSellGearTypeAllowed[i] = true;
                original.AutoSellGearTypeAllowed[(int)GearCategory.Weapon] = false;

                // 39. Auto-purchase items
                original.AutoPurchaseItems = true;
                original.AutoPurchaseConsumablesFirst = true;      // non-default (defaults to gear-first)
                original.AutoPurchaseMercenaryGear = true;
                original.AutoPurchaseConsumables = true;
                original.AutoPurchaseRarityAllowed = new bool[] { true, true, true, false, false };
                original.AutoPurchaseGearTypeAllowed = new bool[GearCategoryUtils.Count];
                for (int i = 0; i < original.AutoPurchaseGearTypeAllowed.Length; i++)
                    original.AutoPurchaseGearTypeAllowed[i] = true;
                original.AutoPurchaseGearTypeAllowed[(int)GearCategory.Accessory] = false;
                original.AutoPurchaseConsumableSelected = new bool[ConsumableCatalog.Count];
                original.AutoPurchaseConsumableStacks = new int[ConsumableCatalog.Count];
                for (int i = 0; i < ConsumableCatalog.Count; i++)
                    original.AutoPurchaseConsumableStacks[i] = 1;
                original.AutoPurchaseConsumableSelected[2] = true;
                original.AutoPurchaseConsumableStacks[2] = 3;

                // 40. Auto-equip
                original.AutoEquipHero = false;
                original.AutoEquipMercenaries = false;

                dataStore.Save("v23_automation.bin", original);

                var loaded = new SaveData();
                dataStore.Load("v23_automation.bin", loaded);

                Assert.IsNotNull(loaded.AutoSellGearTypeAllowed);
                Assert.AreEqual(GearCategoryUtils.Count, loaded.AutoSellGearTypeAllowed.Length);
                Assert.IsFalse(loaded.AutoSellGearTypeAllowed[(int)GearCategory.Weapon], "Unchecked Weapon sell type should round-trip");
                Assert.IsTrue(loaded.AutoSellGearTypeAllowed[(int)GearCategory.Shield]);

                Assert.IsTrue(loaded.AutoPurchaseItems);
                Assert.IsTrue(loaded.AutoPurchaseConsumablesFirst);
                Assert.IsTrue(loaded.AutoPurchaseMercenaryGear);
                Assert.IsTrue(loaded.AutoPurchaseConsumables);
                Assert.IsFalse(loaded.AutoPurchaseRarityAllowed[(int)ItemRarity.Epic]);
                Assert.IsFalse(loaded.AutoPurchaseGearTypeAllowed[(int)GearCategory.Accessory]);
                Assert.IsTrue(loaded.AutoPurchaseConsumableSelected[2], "Selected consumable should round-trip");
                Assert.AreEqual(3, loaded.AutoPurchaseConsumableStacks[2], "Stack target should round-trip");
                Assert.IsFalse(loaded.AutoPurchaseConsumableSelected[0]);
                Assert.AreEqual(1, loaded.AutoPurchaseConsumableStacks[0]);

                Assert.IsFalse(loaded.AutoEquipHero);
                Assert.IsFalse(loaded.AutoEquipMercenaries);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        /// <summary>
        /// Verifies the v23 defaults: everything permissive/off, matching a fresh game. Null arrays
        /// persist a zero count, so this also covers the "older file, section absent" path.
        /// </summary>
        [TestMethod]
        public void SaveData_V23_AutomationUpdates_Defaults()
        {
            var fresh = new SaveData();
            Assert.IsFalse(fresh.AutoPurchaseItems, "Auto-purchase defaults to off");
            Assert.IsFalse(fresh.AutoPurchaseConsumablesFirst, "Purchase priority defaults to gear-first");
            Assert.IsFalse(fresh.AutoPurchaseMercenaryGear);
            Assert.IsTrue(fresh.AutoPurchaseConsumables, "Legacy v23-v25 slot; v26 removed the master flag and always writes true");
            Assert.IsTrue(fresh.AutoEquipHero, "Auto-equip defaults to on");
            Assert.IsTrue(fresh.AutoEquipMercenaries, "Auto-equip defaults to on");

            var tempDir = Path.Combine(Path.GetTempPath(), "pithero_v23d_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var dataStore = new FileDataStore(tempDir);

                var original = new SaveData();
                original.AutoSellGearTypeAllowed = null;
                original.AutoPurchaseRarityAllowed = null;
                original.AutoPurchaseGearTypeAllowed = null;
                original.AutoPurchaseConsumableSelected = null;
                original.AutoPurchaseConsumableStacks = null;

                dataStore.Save("v23_defaults.bin", original);

                var loaded = new SaveData();
                dataStore.Load("v23_defaults.bin", loaded);

                Assert.IsNotNull(loaded.AutoSellGearTypeAllowed);
                for (int i = 0; i < loaded.AutoSellGearTypeAllowed.Length; i++)
                    Assert.IsTrue(loaded.AutoSellGearTypeAllowed[i], $"Gear sell category {i} should default to allowed");

                Assert.IsNotNull(loaded.AutoPurchaseRarityAllowed);
                for (int i = 0; i < loaded.AutoPurchaseRarityAllowed.Length; i++)
                    Assert.IsTrue(loaded.AutoPurchaseRarityAllowed[i], $"Buy rarity {i} should default to allowed");

                Assert.IsNotNull(loaded.AutoPurchaseGearTypeAllowed);
                for (int i = 0; i < loaded.AutoPurchaseGearTypeAllowed.Length; i++)
                    Assert.IsTrue(loaded.AutoPurchaseGearTypeAllowed[i], $"Buy gear category {i} should default to allowed");

                Assert.IsNotNull(loaded.AutoPurchaseConsumableSelected);
                Assert.AreEqual(ConsumableCatalog.Count, loaded.AutoPurchaseConsumableSelected.Length);
                for (int i = 0; i < loaded.AutoPurchaseConsumableSelected.Length; i++)
                {
                    Assert.IsFalse(loaded.AutoPurchaseConsumableSelected[i], $"Consumable {i} should default to unselected");
                    Assert.AreEqual(1, loaded.AutoPurchaseConsumableStacks[i], $"Consumable {i} should default to one stack");
                }

                Assert.IsTrue(loaded.AutoEquipHero);
                Assert.IsTrue(loaded.AutoEquipMercenaries);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        /// <summary>Verifies the v24 auto-hire mercenary settings round-trip, including duplicate job slots.</summary>
        [TestMethod]
        public void SaveData_V24_AutoHireMercenaries_RoundTrip()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "pithero_v24_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var dataStore = new FileDataStore(tempDir);

                var original = new SaveData();
                original.AutoHireMercenariesEnabled = true;
                original.AutoHireMerc1Job = (int)JobType.Knight;
                original.AutoHireMerc2Job = (int)JobType.Knight;

                dataStore.Save("v24_autohire.bin", original);

                var loaded = new SaveData();
                dataStore.Load("v24_autohire.bin", loaded);

                Assert.IsTrue(loaded.AutoHireMercenariesEnabled, "Auto-hire enabled should round-trip");
                Assert.AreEqual((int)JobType.Knight, loaded.AutoHireMerc1Job, "Slot 1 job should round-trip");
                Assert.AreEqual((int)JobType.Knight, loaded.AutoHireMerc2Job, "Duplicate slot 2 job should round-trip");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        /// <summary>
        /// Verifies the v24 auto-hire defaults: disabled with both slots None, both on a fresh
        /// SaveData and after a default save/load cycle (the state older files recover with).
        /// </summary>
        [TestMethod]
        public void SaveData_V24_AutoHireMercenaries_Defaults()
        {
            var fresh = new SaveData();
            Assert.IsFalse(fresh.AutoHireMercenariesEnabled, "Auto-hire defaults to OFF");
            Assert.AreEqual((int)JobType.None, fresh.AutoHireMerc1Job, "Slot 1 defaults to None");
            Assert.AreEqual((int)JobType.None, fresh.AutoHireMerc2Job, "Slot 2 defaults to None");

            var tempDir = Path.Combine(Path.GetTempPath(), "pithero_v24d_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var dataStore = new FileDataStore(tempDir);
                dataStore.Save("v24_defaults.bin", new SaveData());

                var loaded = new SaveData();
                dataStore.Load("v24_defaults.bin", loaded);

                Assert.IsFalse(loaded.AutoHireMercenariesEnabled);
                Assert.AreEqual((int)JobType.None, loaded.AutoHireMerc1Job);
                Assert.AreEqual((int)JobType.None, loaded.AutoHireMerc2Job);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        /// <summary>Verifies the v25 auto-learn settings round-trip (enabled, Passive mode).</summary>
        [TestMethod]
        public void SaveData_V25_AutoLearnSkills_RoundTrip()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "pithero_v25_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var dataStore = new FileDataStore(tempDir);

                var original = new SaveData();
                original.AutoLearnSkillsEnabled = true;
                original.AutoLearnMode = (int)PitHero.Services.AutoLearnMode.Passive;

                dataStore.Save("v25_autolearn.bin", original);

                var loaded = new SaveData();
                dataStore.Load("v25_autolearn.bin", loaded);

                Assert.IsTrue(loaded.AutoLearnSkillsEnabled, "AutoLearnSkillsEnabled should round-trip");
                Assert.AreEqual((int)PitHero.Services.AutoLearnMode.Passive, loaded.AutoLearnMode,
                    "AutoLearnMode Passive should round-trip");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        /// <summary>
        /// Verifies the v25 auto-learn defaults: disabled and Smart mode, both on a fresh SaveData
        /// and after a default save/load cycle.
        /// </summary>
        [TestMethod]
        public void SaveData_V25_AutoLearnSkills_Defaults()
        {
            var fresh = new SaveData();
            Assert.IsFalse(fresh.AutoLearnSkillsEnabled, "AutoLearnSkillsEnabled defaults to OFF");
            Assert.AreEqual((int)PitHero.Services.AutoLearnMode.Smart, fresh.AutoLearnMode,
                "AutoLearnMode defaults to Smart (0)");

            var tempDir = Path.Combine(Path.GetTempPath(), "pithero_v25d_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var dataStore = new FileDataStore(tempDir);
                dataStore.Save("v25_defaults.bin", new SaveData());

                var loaded = new SaveData();
                dataStore.Load("v25_defaults.bin", loaded);

                Assert.IsFalse(loaded.AutoLearnSkillsEnabled);
                Assert.AreEqual((int)PitHero.Services.AutoLearnMode.Smart, loaded.AutoLearnMode);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        /// <summary>Verifies the v26 consumable sell options round-trip (mixed selections and floors).</summary>
        [TestMethod]
        public void SaveData_V26_ConsumableSellOptions_RoundTrip()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "pithero_v26_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var dataStore = new FileDataStore(tempDir);

                int count = RolePlayingFramework.Equipment.ConsumableCatalog.Count;
                var original = new SaveData();
                original.AutoSellConsumableSelected = new bool[count];
                original.AutoSellConsumableMinStacks = new int[count];
                for (int i = 0; i < count; i++)
                {
                    original.AutoSellConsumableSelected[i] = i % 2 == 0;
                    original.AutoSellConsumableMinStacks[i] = i % 4;
                }

                dataStore.Save("v26_sellopts.bin", original);

                var loaded = new SaveData();
                dataStore.Load("v26_sellopts.bin", loaded);

                for (int i = 0; i < count; i++)
                {
                    Assert.AreEqual(i % 2 == 0, loaded.AutoSellConsumableSelected[i], $"Selection {i} should round-trip");
                    Assert.AreEqual(i % 4, loaded.AutoSellConsumableMinStacks[i], $"Min stacks {i} should round-trip");
                }
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        /// <summary>
        /// Verifies the v26 consumable sell defaults: everything sellable with a floor of one stack,
        /// both after Recover normalizes a default save and on the wire.
        /// </summary>
        [TestMethod]
        public void SaveData_V26_ConsumableSellOptions_Defaults()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "pithero_v26d_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var dataStore = new FileDataStore(tempDir);
                dataStore.Save("v26_defaults.bin", new SaveData());

                var loaded = new SaveData();
                dataStore.Load("v26_defaults.bin", loaded);

                Assert.AreEqual(RolePlayingFramework.Equipment.ConsumableCatalog.Count, loaded.AutoSellConsumableSelected.Length);
                for (int i = 0; i < loaded.AutoSellConsumableSelected.Length; i++)
                {
                    Assert.IsTrue(loaded.AutoSellConsumableSelected[i], $"Consumable {i} should be sellable by default");
                    Assert.AreEqual(1, loaded.AutoSellConsumableMinStacks[i], $"Consumable {i} should keep one stack by default");
                }
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        /// <summary>
        /// v27 round-trip: SaveData.PlacedStencils (2 records) persists and recovers correctly
        /// through the binary serializer (section 44).
        /// </summary>
        [TestMethod]
        public void SaveData_V27_PlacedStencils_RoundTrip()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "pithero_v27_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var dataStore = new FileDataStore(tempDir);

                var original = new SaveData();
                original.HeroName = "StencilHero";
                original.PlacedStencils = new List<SavedPlacedStencil>
                {
                    new SavedPlacedStencil { PatternId = "knight.shield_mastery", AnchorX = 0, AnchorY = 3 },
                    new SavedPlacedStencil { PatternId = "knight.heavy_fortification", AnchorX = 10, AnchorY = 5 },
                };

                dataStore.Save("v27_stencils.bin", original);

                var loaded = new SaveData();
                dataStore.Load("v27_stencils.bin", loaded);

                Assert.IsNotNull(loaded.PlacedStencils, "PlacedStencils must not be null after recovery");
                Assert.AreEqual(2, loaded.PlacedStencils.Count, "Both records must survive the round-trip");

                Assert.AreEqual("knight.shield_mastery", loaded.PlacedStencils[0].PatternId);
                Assert.AreEqual(0, loaded.PlacedStencils[0].AnchorX);
                Assert.AreEqual(3, loaded.PlacedStencils[0].AnchorY);

                Assert.AreEqual("knight.heavy_fortification", loaded.PlacedStencils[1].PatternId);
                Assert.AreEqual(10, loaded.PlacedStencils[1].AnchorX);
                Assert.AreEqual(5, loaded.PlacedStencils[1].AnchorY);
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }

        /// <summary>
        /// A fresh SaveData defaults PlacedStencils to an empty (non-null) list.
        /// </summary>
        [TestMethod]
        public void SaveData_PlacedStencils_DefaultsToEmptyList()
        {
            var data = new SaveData();
            Assert.IsNotNull(data.PlacedStencils, "PlacedStencils must default to non-null");
            Assert.AreEqual(0, data.PlacedStencils.Count, "PlacedStencils must default to empty");
        }

        /// <summary>
        /// A vault with 60 item stacks (well under the 540-stack cap) survives a full
        /// SaveData binary round-trip and a vault restore with logEvictions:false without any
        /// eviction — verifying that both the persistence layer and the restore path are
        /// correct for issue #373 (no format change, cap is 540, save version stays 27).
        /// </summary>
        [TestMethod]
        public void VaultItems_60Stacks_SurviveRoundTrip()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "pithero_vault60_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                // Build a SaveData with 60 vault item entries (mix of gear and consumables).
                // Using real item names so ItemRegistry.TryCreateItem can reconstruct them.
                var original = new SaveData();
                original.HeroName = "VaultTest";
                original.JobName = JobTextKey.Job_Knight_Name;
                original.Level = 1;

                original.SecondChanceVaultItems = new List<SavedVaultItem>();

                // 50 distinct gear entries — each needs a real name known to ItemRegistry.
                // Reuse a handful of known item keys; they will stack in the vault (same
                // name → same stack), so we vary quantities to keep the count predictable.
                // Since the test validates round-trip fidelity, we store the raw
                // SecondChanceVaultItems count (60 SavedVaultItem records) not the
                // resulting vault StackCount after stacking.
                string[] gearNames = new string[]
                {
                    InventoryTextKey.Inv_ShortSword_Name, InventoryTextKey.Inv_LongSword_Name,
                    InventoryTextKey.Inv_IronArmor_Name,  InventoryTextKey.Inv_LeatherArmor_Name,
                    InventoryTextKey.Inv_IronHelm_Name,   InventoryTextKey.Inv_ClothCap_Name,
                    InventoryTextKey.Inv_IronShield_Name, InventoryTextKey.Inv_HideShield_Name,
                    InventoryTextKey.Inv_RustyBlade_Name, InventoryTextKey.Inv_CaveShiv_Name,
                };

                for (int i = 0; i < 50; i++)
                {
                    var vi = new SavedVaultItem();
                    vi.Name = gearNames[i % gearNames.Length];
                    vi.IsConsumable = false;
                    vi.Quantity = i + 1;
                    original.SecondChanceVaultItems.Add(vi);
                }

                // 10 consumable entries
                string[] potionNames = new string[]
                {
                    InventoryTextKey.Inv_HPPotion_Name, InventoryTextKey.Inv_MPPotion_Name,
                    InventoryTextKey.Inv_MixPotion_Name,
                };
                for (int i = 0; i < 10; i++)
                {
                    var vi = new SavedVaultItem();
                    vi.Name = potionNames[i % potionNames.Length];
                    vi.IsConsumable = true;
                    vi.Quantity = 5 * (i + 1);
                    original.SecondChanceVaultItems.Add(vi);
                }

                Assert.AreEqual(60, original.SecondChanceVaultItems.Count);

                // ── Binary round-trip ──────────────────────────────────────────
                var dataStore = new FileDataStore(tempDir);
                dataStore.Save("vault60.bin", original);

                var loaded = new SaveData();
                dataStore.Load("vault60.bin", loaded);

                Assert.AreEqual(60, loaded.SecondChanceVaultItems.Count,
                    "All 60 SavedVaultItem records must survive binary round-trip");

                // Spot-check first and last entries
                Assert.AreEqual(original.SecondChanceVaultItems[0].Name,
                                loaded.SecondChanceVaultItems[0].Name);
                Assert.AreEqual(original.SecondChanceVaultItems[0].Quantity,
                                loaded.SecondChanceVaultItems[0].Quantity);
                Assert.AreEqual(original.SecondChanceVaultItems[59].Name,
                                loaded.SecondChanceVaultItems[59].Name);
                Assert.AreEqual(original.SecondChanceVaultItems[59].Quantity,
                                loaded.SecondChanceVaultItems[59].Quantity);

                // ── Restore into a vault (mirrors MainGameScene logic) ─────────
                var vault = new PitHero.Services.SecondChanceMerchantVault();
                for (int i = 0; i < loaded.SecondChanceVaultItems.Count; i++)
                {
                    var vi = loaded.SecondChanceVaultItems[i];
                    if (string.IsNullOrEmpty(vi.Name)) continue;

                    if (ItemRegistry.TryCreateItem(vi.Name, out var itemTemplate))
                    {
                        if (itemTemplate is RolePlayingFramework.Equipment.Consumable consumable)
                        {
                            consumable.StackCount = vi.Quantity;
                            vault.AddItem(consumable, logEvictions: false);
                        }
                        else
                        {
                            for (int q = 0; q < vi.Quantity; q++)
                            {
                                if (ItemRegistry.TryCreateItem(vi.Name, out var gearCopy))
                                    vault.AddItem(gearCopy, logEvictions: false);
                            }
                        }
                    }
                }

                // Vault must not be empty and must be well under the 540-stack cap.
                // (Exact stack count depends on stacking of same-name gear; we only assert
                // it is non-zero and no eviction occurred — the vault is far below 540.)
                Assert.IsTrue(vault.StackCount > 0,
                    "Restored vault must contain at least some items");
                Assert.IsTrue(vault.StackCount <= PitHero.Services.SecondChanceMerchantVault.MaxStacks,
                    "Restored vault must be within the 540-stack cap");
                Assert.IsTrue(vault.TotalItemCount > 0,
                    "Restored vault must have positive total item count");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        /// <summary>
        /// Verifies the v31 gender fields round-trip for both the hero and a hired mercenary.
        /// </summary>
        [TestMethod]
        public void SaveData_V31_Gender_RoundTrip()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "pithero_v31_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var dataStore = new FileDataStore(tempDir);

                var original = new SaveData();
                original.HeroName = "GenderedHero";
                original.HeroGender = Gender.Female;
                original.HiredMercenaries = new List<SavedMercenary>
                {
                    new SavedMercenary
                    {
                        Name = "MaleMerc",
                        Gender = Gender.Male,
                        JobName = JobTextKey.Job_Knight_Name,
                        Level = 4,
                        EquipmentNames = new string[6]
                    },
                    new SavedMercenary
                    {
                        Name = "FemaleMerc",
                        Gender = Gender.Female,
                        JobName = JobTextKey.Job_Mage_Name,
                        Level = 6,
                        EquipmentNames = new string[6]
                    }
                };

                dataStore.Save("v31_gender.bin", original);

                var loaded = new SaveData();
                dataStore.Load("v31_gender.bin", loaded);

                Assert.AreEqual(Gender.Female, loaded.HeroGender, "Hero gender should round-trip");
                Assert.AreEqual("GenderedHero", loaded.HeroName, "Hero name should still follow the gender field");
                Assert.AreEqual(2, loaded.HiredMercenaries.Count);
                Assert.AreEqual(Gender.Male, loaded.HiredMercenaries[0].Gender, "Merc 0 gender should round-trip");
                Assert.AreEqual("MaleMerc", loaded.HiredMercenaries[0].Name);
                Assert.AreEqual(Gender.Female, loaded.HiredMercenaries[1].Gender, "Merc 1 gender should round-trip");
                Assert.AreEqual(JobTextKey.Job_Mage_Name, loaded.HiredMercenaries[1].JobName,
                    "Fields after the merc gender must stay aligned");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        /// <summary>
        /// Backwards compatibility for the v31 gender bump: a v30 file has no gender ints, so the
        /// hero must load as Male and every field after the gender slot must stay aligned. Built by
        /// writing a v31 stream with no mercenaries (so only the hero's gender int exists), splicing
        /// out those 4 bytes, and patching the version header down to 30.
        /// </summary>
        [TestMethod]
        public void SaveData_V30_File_LoadsWithMaleGenderDefault()
        {
            const string heroName = "OldHero";

            var ms = new MemoryStream();
            using (var writer = new BinaryPersistableWriter(ms))
            {
                var original = new SaveData();
                original.HeroName = heroName;
                original.HeroGender = Gender.Female; // must be dropped along with the bytes
                original.SkinColor = new Color(11, 22, 33, 255);
                original.HairstyleIndex = 4;
                original.JobName = JobTextKey.Job_Knight_Name;
                original.Level = 9;
                writer.Write(original);
            }

            byte[] v31 = ms.ToArray();

            // Layout: version(4) + TotalTimePlayed(4) + InGameTime(4) + length-prefixed HeroName
            // + HeroGender(4). BinaryWriter writes the 7-bit length prefix in one byte for short names.
            int genderOffset = 4 + 4 + 4 + 1 + Encoding.UTF8.GetByteCount(heroName);

            var v30 = new byte[v31.Length - 4];
            Array.Copy(v31, 0, v30, 0, genderOffset);
            Array.Copy(v31, genderOffset + 4, v30, genderOffset, v31.Length - genderOffset - 4);

            // Patch the version header down to 30 (little-endian int)
            v30[0] = 30;
            v30[1] = 0;
            v30[2] = 0;
            v30[3] = 0;

            var loaded = new SaveData();
            using (var rdr = new BinaryPersistableReader(new MemoryStream(v30)))
            {
                rdr.ReadPersistableInto(loaded);
            }

            Assert.AreEqual(Gender.Male, loaded.HeroGender, "A v30 file has no gender and must default to Male");
            Assert.AreEqual(heroName, loaded.HeroName, "Hero name must survive the v30 read");
            Assert.AreEqual(new Color(11, 22, 33, 255), loaded.SkinColor, "Fields after the gender slot must stay aligned");
            Assert.AreEqual(4, loaded.HairstyleIndex);
            Assert.AreEqual(JobTextKey.Job_Knight_Name, loaded.JobName);
            Assert.AreEqual(9, loaded.Level);
        }
    }
}
