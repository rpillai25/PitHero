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

                original.EquipmentNames = new string[] { "RustyBlade", "", "SquireHelm", "", "", "" };

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
                    new SavedItem { Name = "HPPotion", IsConsumable = true, StackCount = 5, SlotIndex = 0 },
                    new SavedItem { Name = "RustyBlade", IsConsumable = false, StackCount = 0, SlotIndex = 3 }
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
            Assert.IsTrue(ItemRegistry.TryCreateItem("RustyBlade", out var sword));
            Assert.IsNotNull(sword);
            Assert.AreEqual("RustyBlade", sword.Name);
        }

        /// <summary>Verifies ItemRegistry finds known potion items.</summary>
        [TestMethod]
        public void ItemRegistry_TryCreateItem_FindsKnownPotionItems()
        {
            Assert.IsTrue(ItemRegistry.TryCreateItem("HPPotion", out var potion));
            Assert.IsNotNull(potion);
            Assert.AreEqual("HPPotion", potion.Name);
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
                Assert.AreEqual("ShortSword", savedItems[0].Name);
                Assert.AreEqual(42, savedItems[1].SlotIndex);
                Assert.AreEqual("IronHelm", savedItems[1].Name);
                Assert.AreEqual(99, savedItems[2].SlotIndex);
                Assert.AreEqual("HPPotion", savedItems[2].Name);
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
                Assert.AreEqual("ShortSword", loaded.InventoryItems[0].Name);
                Assert.AreEqual(42, loaded.InventoryItems[1].SlotIndex);
                Assert.AreEqual("IronHelm", loaded.InventoryItems[1].Name);
                Assert.AreEqual(99, loaded.InventoryItems[2].SlotIndex);
                Assert.AreEqual("HPPotion", loaded.InventoryItems[2].Name);
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
                Assert.AreEqual("ShortSword", restoredSword.Name);

                Assert.IsNull(restoredBag.GetSlotItem(16), "Slot 16 should be empty");
                Assert.IsNull(restoredBag.GetSlotItem(41), "Slot 41 should be empty");

                var restoredHelm = restoredBag.GetSlotItem(42);
                Assert.IsNotNull(restoredHelm, "IronHelm should be at slot 42");
                Assert.AreEqual("IronHelm", restoredHelm.Name);

                Assert.IsNull(restoredBag.GetSlotItem(43), "Slot 43 should be empty");
                Assert.IsNull(restoredBag.GetSlotItem(98), "Slot 98 should be empty");

                var restoredPotion = restoredBag.GetSlotItem(99);
                Assert.IsNotNull(restoredPotion, "HPPotion should be at slot 99");
                Assert.AreEqual("HPPotion", restoredPotion.Name);
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
                var service = new SaveLoadService(new FileDataStore(tempDir), tempDir);

                Assert.IsFalse(service.SlotHasData(0), "Incompatible slot should be treated as empty");
                Assert.IsNull(service.LoadFromSlot(0), "Loading an incompatible slot should return null");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        /// <summary>The autosave file a hero's id maps to (mirrors SaveLoadService.GetAutoSaveFilename).</summary>
        private static string AutoSaveFileNameFor(int heroId)
        {
            return GameConfig.AutoSaveFilePrefix + ((uint)heroId).ToString("X8") + GameConfig.AutoSaveFileExtension;
        }

        /// <summary>Builds a minimal autosave snapshot for a hero.</summary>
        private static SaveData AutoSaveSnapshot(int heroId, string heroName, int level = 1)
        {
            var data = new SaveData();
            data.HeroId = heroId;
            data.HeroName = heroName;
            data.Level = level;
            return data;
        }

        /// <summary>
        /// Verifies the per-hero autosave files (issue #409): each hero gets its own file beside the
        /// slots, snapshots are published without re-reading the disk, and a fresh service instance
        /// rediscovers every hero's autosave from disk.
        /// </summary>
        [TestMethod]
        public void SaveLoadService_AutoSave_PerHero_RoundTrip()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "pithero_autosave_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var service = new SaveLoadService(new FileDataStore(tempDir), new FileDataStore(tempDir), tempDir);
                Assert.AreEqual(0, service.AutoSaveEntries.Count, "fresh directory has no autosaves");
                Assert.IsNull(service.LoadFromAutoSave(111), "missing autosave loads as null");

                var heroA = AutoSaveSnapshot(111, "Borin", 7);
                heroA.TotalTimePlayed = 90f;
                heroA.PitLevel = 12;
                var heroB = AutoSaveSnapshot(-222, "Kaya", 3);
                heroB.PitLevel = 4;

                service.WriteAutoSave(heroA);
                service.WriteAutoSave(heroB);

                Assert.IsTrue(File.Exists(Path.Combine(tempDir, AutoSaveFileNameFor(111))), "hero A's autosave written beside the slots");
                Assert.IsTrue(File.Exists(Path.Combine(tempDir, AutoSaveFileNameFor(-222))), "a negative hero id still yields a valid filename");
                Assert.IsFalse(File.Exists(Path.Combine(tempDir, AutoSaveFileNameFor(111) + ".tmp")), "no tmp file left behind");
                Assert.IsFalse(service.SlotHasData(0), "autosaves never touch a manual slot");

                // The worker's snapshots are published by the main thread without re-reading the disk
                service.SetAutoSavePreview(heroA);
                service.SetAutoSavePreview(heroB);
                Assert.AreEqual(2, service.AutoSaveEntries.Count, "one entry per hero");
                Assert.AreEqual(-222, service.AutoSaveEntries[0].HeroId, "most recently saved hero is listed first");
                Assert.AreSame(heroB, service.AutoSaveEntries[0].Preview);

                // A fresh service (next launch) rediscovers both heroes from disk
                var reloaded = new SaveLoadService(new FileDataStore(tempDir), new FileDataStore(tempDir), tempDir);
                Assert.AreEqual(2, reloaded.AutoSaveEntries.Count);

                var loadedA = reloaded.LoadFromAutoSave(111);
                Assert.IsNotNull(loadedA);
                Assert.AreEqual("Borin", loadedA.HeroName);
                Assert.AreEqual(12, loadedA.PitLevel);
                Assert.AreEqual(90f, loadedA.TotalTimePlayed, 0.001f);
                Assert.AreEqual(90f, reloaded.LoadedTimePlayed, 0.001f, "loading an autosave seeds the played-time accumulator");

                var loadedB = reloaded.LoadFromAutoSave(-222);
                Assert.IsNotNull(loadedB);
                Assert.AreEqual("Kaya", loadedB.HeroName, "each hero loads its own autosave");
                Assert.AreEqual(4, loadedB.PitLevel);

                Assert.IsNull(reloaded.LoadFromAutoSave(999), "an unknown hero has no autosave");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        /// <summary>
        /// The regression this feature exists for: a second hero's autosave must not overwrite the
        /// first hero's, and repeated autosaves of one hero must reuse that hero's single file.
        /// </summary>
        [TestMethod]
        public void SaveLoadService_AutoSave_SecondHeroDoesNotOverwriteFirst()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "pithero_autosave2_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var service = new SaveLoadService(new FileDataStore(tempDir), new FileDataStore(tempDir), tempDir);

                var heroA = AutoSaveSnapshot(111, "Borin", 7);
                service.WriteAutoSave(heroA);
                service.SetAutoSavePreview(heroA);

                // Same hero saves again (as it does every 30 s) — still exactly one file for that hero
                var heroAgain = AutoSaveSnapshot(111, "Borin", 8);
                service.WriteAutoSave(heroAgain);
                service.SetAutoSavePreview(heroAgain);

                var heroB = AutoSaveSnapshot(222, "Kaya", 3);
                service.WriteAutoSave(heroB);
                service.SetAutoSavePreview(heroB);

                int files = Directory.GetFiles(tempDir, GameConfig.AutoSaveFilePrefix + "*" + GameConfig.AutoSaveFileExtension).Length;
                Assert.AreEqual(2, files, "one autosave file per hero, regardless of how often each saves");
                Assert.AreEqual(2, service.AutoSaveEntries.Count);

                var stillHeroA = service.LoadFromAutoSave(111);
                Assert.IsNotNull(stillHeroA, "hero A's autosave survives hero B autosaving");
                Assert.AreEqual("Borin", stillHeroA.HeroName);
                Assert.AreEqual(8, stillHeroA.Level, "hero A's autosave holds its latest snapshot");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        /// <summary>
        /// At the cap a further hero evicts the least recently written autosave, and the hero currently
        /// playing is never the one evicted.
        /// </summary>
        [TestMethod]
        public void SaveLoadService_AutoSave_CapEvictsLeastRecentlyWritten()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "pithero_autosavecap_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var service = new SaveLoadService(new FileDataStore(tempDir), new FileDataStore(tempDir), tempDir);

                // One more hero than the cap allows, each written a day further in the past than the last
                int overCap = SaveLoadService.MaxAutoSaves + 1;
                var now = DateTime.UtcNow;
                for (int i = 0; i < overCap; i++)
                {
                    int heroId = 100 + i;
                    service.WriteAutoSave(AutoSaveSnapshot(heroId, "Hero" + heroId));
                    // Hero 100 is the least recently played, hero 105 the most recent
                    File.SetLastWriteTimeUtc(Path.Combine(tempDir, AutoSaveFileNameFor(heroId)), now.AddDays(i - overCap));
                }

                service.RefreshAutoSavePreviews();
                Assert.AreEqual(overCap, service.AutoSaveEntries.Count, "the scan finds every autosave on disk");
                Assert.AreEqual(105, service.AutoSaveEntries[0].HeroId, "most recently written first");

                // The most recent hero autosaves again, pushing the list over the cap
                var current = AutoSaveSnapshot(105, "Hero105", 2);
                service.WriteAutoSave(current);
                service.SetAutoSavePreview(current);

                Assert.AreEqual(SaveLoadService.MaxAutoSaves, service.AutoSaveEntries.Count, "the cap is enforced");
                Assert.IsFalse(File.Exists(Path.Combine(tempDir, AutoSaveFileNameFor(100))), "the least recently played autosave file is deleted");
                Assert.IsNull(service.LoadFromAutoSave(100));
                for (int heroId = 101; heroId <= 105; heroId++)
                    Assert.IsNotNull(service.LoadFromAutoSave(heroId), "hero " + heroId + " keeps its autosave");

                // A brand new hero evicts the oldest of the others, never the one that just saved
                File.SetLastWriteTimeUtc(Path.Combine(tempDir, AutoSaveFileNameFor(101)), now.AddDays(-99));
                service.RefreshAutoSavePreviews();
                var newcomer = AutoSaveSnapshot(200, "Newcomer");
                service.WriteAutoSave(newcomer);
                service.SetAutoSavePreview(newcomer);

                Assert.AreEqual(SaveLoadService.MaxAutoSaves, service.AutoSaveEntries.Count);
                Assert.IsNull(service.LoadFromAutoSave(101), "the least recently played autosave goes");
                Assert.AreEqual(200, service.AutoSaveEntries[0].HeroId, "the hero that just saved is listed first");
                Assert.IsNotNull(service.LoadFromAutoSave(200), "the hero currently playing keeps its autosave");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        /// <summary>
        /// The single pre-per-hero autosave.bin is adopted as its hero's autosave on the first scan and
        /// then removed, so an existing autosave survives the upgrade.
        /// </summary>
        [TestMethod]
        public void SaveLoadService_LegacyAutoSaveFile_MigratedToPerHeroFile()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "pithero_autosavelegacy_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var legacy = AutoSaveSnapshot(777, "Legacy", 9);
                legacy.PitLevel = 21;
                new FileDataStore(tempDir).Save(GameConfig.AutoSaveLegacyFileName, legacy);
                Assert.IsTrue(File.Exists(Path.Combine(tempDir, GameConfig.AutoSaveLegacyFileName)));

                var service = new SaveLoadService(new FileDataStore(tempDir), new FileDataStore(tempDir), tempDir);

                Assert.IsFalse(File.Exists(Path.Combine(tempDir, GameConfig.AutoSaveLegacyFileName)), "the legacy file is removed once adopted");
                Assert.IsTrue(File.Exists(Path.Combine(tempDir, AutoSaveFileNameFor(777))), "it is rewritten under its hero's name");
                Assert.AreEqual(1, service.AutoSaveEntries.Count);
                Assert.AreEqual(777, service.AutoSaveEntries[0].HeroId);

                var loaded = service.LoadFromAutoSave(777);
                Assert.IsNotNull(loaded);
                Assert.AreEqual("Legacy", loaded.HeroName);
                Assert.AreEqual(21, loaded.PitLevel);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        /// <summary>An autosave file from an unsupported version is treated as empty instead of crashing.</summary>
        [TestMethod]
        public void SaveLoadService_IncompatibleAutoSaveFile_TreatedAsEmpty()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "pithero_oldautosave_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
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
                File.WriteAllBytes(Path.Combine(tempDir, AutoSaveFileNameFor(555)), bytes);

                var service = new SaveLoadService(new FileDataStore(tempDir), tempDir);

                Assert.AreEqual(0, service.AutoSaveEntries.Count, "Incompatible autosave should be treated as empty");
                Assert.IsNull(service.LoadFromAutoSave(555), "Loading an incompatible autosave should return null");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        /// <summary>
        /// FileDataStore.Save must leave the destination intact if a stale tmp file exists and must not
        /// leave a tmp file behind (the autosave rewrites the same file every 30 s).
        /// </summary>
        [TestMethod]
        public void FileDataStore_Save_ReplacesStaleTmpAndLeavesNoTmp()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "pithero_tmpfile_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                const string fileName = "save_slot_0.bin";
                // Orphaned tmp much longer than any real save
                File.WriteAllBytes(Path.Combine(tempDir, fileName + ".tmp"), new byte[64 * 1024]);

                var store = new FileDataStore(tempDir);
                var data = new SaveData();
                data.HeroName = "TmpHero";
                store.Save(fileName, data);

                Assert.IsFalse(File.Exists(Path.Combine(tempDir, fileName + ".tmp")));
                Assert.IsTrue(new FileInfo(Path.Combine(tempDir, fileName)).Length < 64 * 1024, "stale tmp bytes must not survive");

                var back = new SaveData();
                store.Load(fileName, back);
                Assert.AreEqual("TmpHero", back.HeroName);
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
                    "ShortSword", "LongSword",
                    "IronArmor",  "LeatherArmor",
                    "IronHelm",   "ClothCap",
                    "IronShield", "HideShield",
                    "RustyBlade", "CaveShiv",
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
                    "HPPotion", "MPPotion",
                    "MixPotion",
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

        /// <summary>v32 appends HeroId as the last field; it must survive a round trip.</summary>
        [TestMethod]
        public void SaveData_V32_HeroId_RoundTrip()
        {
            var ms = new MemoryStream();
            using (var writer = new BinaryPersistableWriter(ms))
            {
                var original = new SaveData();
                original.HeroName = "Identified";
                original.HeroId = -1234567;
                writer.Write(original);
            }

            var loaded = new SaveData();
            using (var rdr = new BinaryPersistableReader(new MemoryStream(ms.ToArray())))
            {
                rdr.ReadPersistableInto(loaded);
            }

            Assert.AreEqual(-1234567, loaded.HeroId);
        }

        /// <summary>
        /// A v31 file has no HeroId. It must still load, and every load must derive the SAME non-zero
        /// id from the hero design so replays recorded from that save keep matching it.
        /// </summary>
        [TestMethod]
        public void SaveData_V31_File_DerivesStableLegacyHeroId()
        {
            var ms = new MemoryStream();
            using (var writer = new BinaryPersistableWriter(ms))
            {
                var original = new SaveData();
                original.HeroName = "LegacyHero";
                original.HeroGender = Gender.Female;
                original.SkinColor = new Color(10, 20, 30, 255);
                original.HairColor = new Color(40, 50, 60, 255);
                original.HairstyleIndex = 3;
                original.ShirtColor = new Color(70, 80, 90, 255);
                original.HeroId = 999; // v32-only bytes: dropped below
                writer.Write(original);
            }

            // HeroId is the final 4 bytes of a v32 file; strip them and patch the version to 31
            byte[] v32 = ms.ToArray();
            var v31 = new byte[v32.Length - 4];
            Array.Copy(v32, 0, v31, 0, v31.Length);
            v31[0] = 31;
            v31[1] = 0;
            v31[2] = 0;
            v31[3] = 0;

            var first = new SaveData();
            using (var rdr = new BinaryPersistableReader(new MemoryStream(v31)))
                rdr.ReadPersistableInto(first);
            var second = new SaveData();
            using (var rdr = new BinaryPersistableReader(new MemoryStream(v31)))
                rdr.ReadPersistableInto(second);

            int expected = SaveData.ComputeLegacyHeroId("LegacyHero", Gender.Female,
                new Color(10, 20, 30, 255), new Color(40, 50, 60, 255), 3, new Color(70, 80, 90, 255));
            Assert.AreNotEqual(0, first.HeroId, "Legacy id must be non-zero (0 means unknown)");
            Assert.AreNotEqual(999, first.HeroId, "The v32 bytes were stripped; the id must be derived, not read");
            Assert.AreEqual(expected, first.HeroId);
            Assert.AreEqual(first.HeroId, second.HeroId, "Every load of the same old save must agree on the hero");
            Assert.AreEqual("LegacyHero", first.HeroName);
        }

        // ── v34: local artifacts, inventory sell percent, unified keep stacks (issue #411) ──

        private static SaveData RoundTrip(SaveData original)
        {
            var ms = new MemoryStream();
            using (var writer = new BinaryPersistableWriter(ms))
                writer.Write(original);
            var loaded = new SaveData();
            using (var rdr = new BinaryPersistableReader(new MemoryStream(ms.ToArray())))
                rdr.ReadPersistableInto(loaded);
            return loaded;
        }

        [TestMethod]
        public void SaveData_V34_LocalArtifacts_RoundTrip()
        {
            var original = new SaveData();
            original.LocalArtifacts = new List<int> { 5, 3, 99, 3 };   // grant order, an unknown ordinal, a duplicate

            var loaded = RoundTrip(original);

            CollectionAssert.AreEqual(new List<int> { 5, 3, 99 }, loaded.LocalArtifacts,
                "Order kept, unknown ordinal kept for a newer build, duplicate dropped");
        }

        [TestMethod]
        public void SaveData_V34_InventorySellPercent_RoundTrip()
        {
            var original = new SaveData { AutoSellInventorySellPercent = 25 };
            Assert.AreEqual(25, RoundTrip(original).AutoSellInventorySellPercent);

            var clamped = new SaveData { AutoSellInventorySellPercent = 250 };
            Assert.AreEqual(100, RoundTrip(clamped).AutoSellInventorySellPercent, "Out-of-range bytes clamp on read");
        }

        [TestMethod]
        public void SaveData_V34_Defaults()
        {
            var loaded = RoundTrip(new SaveData());
            Assert.IsNotNull(loaded.LocalArtifacts);
            Assert.AreEqual(0, loaded.LocalArtifacts.Count);
            Assert.AreEqual(GameConfig.AutoSellInventoryPercentDefault, loaded.AutoSellInventorySellPercent);
        }

        /// <summary>Strips the v34 tail (local artifacts count + ordinals, then the percent) and patches the version to 33.</summary>
        private static byte[] ToV33Bytes(SaveData original, int localArtifactCount)
        {
            var ms = new MemoryStream();
            using (var writer = new BinaryPersistableWriter(ms))
                writer.Write(original);
            byte[] v34 = ms.ToArray();
            int tail = 4 + 4 * localArtifactCount + 4 + V35Tail(original) + V36Tail(original);
            var v33 = new byte[v34.Length - tail];
            Array.Copy(v34, 0, v33, 0, v33.Length);
            v33[0] = 33;
            v33[1] = 0;
            v33[2] = 0;
            v33[3] = 0;
            return v33;
        }

        [TestMethod]
        public void SaveData_V33_File_ReadsWithV34Defaults()
        {
            var original = new SaveData();
            original.HeroId = 777;
            original.LocalArtifacts = new List<int> { 3, 5 };
            original.AutoSellInventorySellPercent = 25;

            var loaded = new SaveData();
            using (var rdr = new BinaryPersistableReader(new MemoryStream(ToV33Bytes(original, 2))))
                rdr.ReadPersistableInto(loaded);

            Assert.AreEqual(777, loaded.HeroId, "The v33 read consumed exactly the v33 bytes");
            Assert.AreEqual(0, loaded.LocalArtifacts.Count, "A v33 file has no local artifacts");
            Assert.AreEqual(GameConfig.AutoSellInventoryPercentDefault, loaded.AutoSellInventorySellPercent);
        }

        [TestMethod]
        public void SaveData_V33_File_ReconcilesKeepStacks()
        {
            int count = RolePlayingFramework.Equipment.ConsumableCatalog.Count;

            // Purchase on + item selected + target above the sell floor: the old EFFECTIVE floor was the target
            var original = new SaveData();
            original.AutoPurchaseItems = true;
            original.AutoPurchaseConsumableSelected = new bool[count];
            original.AutoPurchaseConsumableStacks = new int[count];
            original.AutoSellConsumableSelected = new bool[count];
            original.AutoSellConsumableMinStacks = new int[count];
            for (int i = 0; i < count; i++)
            {
                original.AutoPurchaseConsumableSelected[i] = i == 0;
                original.AutoPurchaseConsumableStacks[i] = 3;
                original.AutoSellConsumableSelected[i] = true;
                original.AutoSellConsumableMinStacks[i] = 1;
            }
            var loaded = new SaveData();
            using (var rdr = new BinaryPersistableReader(new MemoryStream(ToV33Bytes(original, 0))))
                rdr.ReadPersistableInto(loaded);
            Assert.AreEqual(3, loaded.AutoSellConsumableMinStacks[0], "Selected + purchasing: the floor was effectively the target");
            Assert.AreEqual(3, loaded.AutoPurchaseConsumableStacks[0]);
            Assert.AreEqual(1, loaded.AutoSellConsumableMinStacks[1], "Unselected: the sell floor stands");
            Assert.AreEqual(1, loaded.AutoPurchaseConsumableStacks[1], "…and the purchase target follows it");

            // Purchasing off: the sell floor is the single value everywhere
            original.AutoPurchaseItems = false;
            loaded = new SaveData();
            using (var rdr = new BinaryPersistableReader(new MemoryStream(ToV33Bytes(original, 0))))
                rdr.ReadPersistableInto(loaded);
            Assert.AreEqual(1, loaded.AutoSellConsumableMinStacks[0]);
            Assert.AreEqual(1, loaded.AutoPurchaseConsumableStacks[0]);

            // A v34 file is never reconciled: what was written is what is read
            var current = new SaveData();
            current.AutoPurchaseItems = true;
            current.AutoPurchaseConsumableSelected = new bool[count];
            current.AutoPurchaseConsumableStacks = new int[count];
            current.AutoSellConsumableSelected = new bool[count];
            current.AutoSellConsumableMinStacks = new int[count];
            current.AutoPurchaseConsumableSelected[0] = true;
            current.AutoPurchaseConsumableStacks[0] = 3;
            current.AutoSellConsumableMinStacks[0] = 1;
            loaded = RoundTrip(current);
            Assert.AreEqual(1, loaded.AutoSellConsumableMinStacks[0]);
            Assert.AreEqual(3, loaded.AutoPurchaseConsumableStacks[0]);
        }

        [TestMethod]
        public void ReplayIO_SaveDataBlob_RoundTripsLocalArtifacts()
        {
            var original = new SaveData();
            original.LocalArtifacts = new List<int> { 3 };

            var loaded = PitHero.Services.Replay.ReplayIO.DeserializeSaveData(PitHero.Services.Replay.ReplayIO.SerializeSaveData(original));

            Assert.IsNotNull(loaded);
            CollectionAssert.AreEqual(new List<int> { 3 }, loaded.LocalArtifacts, "A replay's start state carries the hero's local artifacts");
        }

        // ── v35 (issue #413): per-monster task progress + lifetime counters ────────────

        [TestMethod]
        public void SaveData_V35_MonsterTasksAndCounters_RoundTrip()
        {
            var original = new SaveData();
            original.AlliedMonsters.Add(new SavedAlliedMonster
            {
                Name = "Bob", MonsterTypeName = MonsterTextKey.Monster_Slime,
                FishingProficiency = 1, CookingProficiency = 2, FarmingProficiency = 3,
                MonsterJobId = 1, MonsterHouseId = 7,
                FishingTasks = 4, CookingTasks = 5, FarmingTasks = 6,
            });
            original.CropHarvestedTotals[(int)PitHero.Farming.CropType.Wheat] = 9;
            original.CropHarvestedTotals[(int)PitHero.Farming.CropType.Corn] = 18;
            original.DishesServedTotals[(int)PitHero.Dining.DishType.ApplePie] = 3;

            var loaded = RoundTrip(original);

            Assert.AreEqual(1, loaded.AlliedMonsters.Count);
            Assert.AreEqual(4, loaded.AlliedMonsters[0].FishingTasks);
            Assert.AreEqual(5, loaded.AlliedMonsters[0].CookingTasks);
            Assert.AreEqual(6, loaded.AlliedMonsters[0].FarmingTasks);
            Assert.AreEqual(7, loaded.AlliedMonsters[0].MonsterHouseId);
            Assert.AreEqual(9, loaded.CropHarvestedTotals[(int)PitHero.Farming.CropType.Wheat]);
            Assert.AreEqual(18, loaded.CropHarvestedTotals[(int)PitHero.Farming.CropType.Corn]);
            Assert.AreEqual(3, loaded.DishesServedTotals[(int)PitHero.Dining.DishType.ApplePie]);
        }

        /// <summary>Strips the v35 tail (two count-prefixed counter arrays) from a monster-less save and patches the version to 34.</summary>
        private static byte[] ToV34Bytes(SaveData original)
        {
            Assert.AreEqual(0, original.AlliedMonsters.Count, "ToV34Bytes supports monster-less saves only (section 11 also grew in v35)");
            var ms = new MemoryStream();
            using (var writer = new BinaryPersistableWriter(ms))
                writer.Write(original);
            byte[] v35 = ms.ToArray();
            int tail = V35Tail(original) + V36Tail(original) + V37Tail(original);
            var body = new byte[v35.Length - tail];
            Array.Copy(v35, 0, body, 0, body.Length);
            body[0] = 34; body[1] = 0; body[2] = 0; body[3] = 0;
            return body;
        }

        /// <summary>Byte length of the v35 section: two count-prefixed int arrays.</summary>
        private static int V35Tail(SaveData data) =>
            (4 + 4 * data.CropHarvestedTotals.Length) + (4 + 4 * data.DishesServedTotals.Length);

        /// <summary>Byte length of the v36 section: a count plus two int halves per inventory item.</summary>
        private static int V36Tail(SaveData data) => 4 + 8 * data.InventoryItems.Count;

        /// <summary>Byte length of the v37 section: a count-prefixed float array of crop demand.</summary>
        private static int V37Tail(SaveData data) => 4 + 4 * data.CropDemand.Length;

        // ── v36 (issue #414): inventory acquisition order ────────────────────────────

        [TestMethod]
        public void SaveData_V36_InventoryAcquireSeq_RoundTrip()
        {
            var original = new SaveData();
            original.InventoryItems.Add(new SavedItem { Name = "HPPotion", IsConsumable = true, StackCount = 3, SlotIndex = 4, AcquireSeq = 7 });
            original.InventoryItems.Add(new SavedItem { Name = "RustyBlade", SlotIndex = 9, AcquireSeq = 5_000_000_000L });

            var loaded = RoundTrip(original);

            Assert.AreEqual(2, loaded.InventoryItems.Count);
            Assert.AreEqual(7, loaded.InventoryItems[0].AcquireSeq);
            Assert.AreEqual(3, loaded.InventoryItems[0].StackCount);
            Assert.AreEqual(5_000_000_000L, loaded.InventoryItems[1].AcquireSeq, "sequences above int range survive");
            Assert.AreEqual(9, loaded.InventoryItems[1].SlotIndex);
        }

        [TestMethod]
        public void SaveData_V35_File_ReadsWithZeroAcquireSeq()
        {
            var original = new SaveData();
            original.HeroId = 4141;
            original.InventoryItems.Add(new SavedItem { Name = "RustyBlade", SlotIndex = 12, AcquireSeq = 99 });
            original.DishesServedTotals[0] = 6;

            var ms = new MemoryStream();
            using (var writer = new BinaryPersistableWriter(ms))
                writer.Write(original);
            byte[] v36 = ms.ToArray();
            var v35 = new byte[v36.Length - V36Tail(original) - V37Tail(original)];
            Array.Copy(v36, 0, v35, 0, v35.Length);
            v35[0] = 35; v35[1] = 0; v35[2] = 0; v35[3] = 0;

            var loaded = new SaveData();
            using (var rdr = new BinaryPersistableReader(new MemoryStream(v35)))
                rdr.ReadPersistableInto(loaded);

            Assert.AreEqual(4141, loaded.HeroId);
            Assert.AreEqual(6, loaded.DishesServedTotals[0], "The v35 section still reads in step");
            Assert.AreEqual(1, loaded.InventoryItems.Count);
            Assert.AreEqual(12, loaded.InventoryItems[0].SlotIndex);
            Assert.AreEqual(0, loaded.InventoryItems[0].AcquireSeq, "A v35 file has no acquisition order");
        }

        [TestMethod]
        public void SaveData_V34_File_ReadsWithV35Defaults()
        {
            var original = new SaveData();
            original.HeroId = 555;
            original.AutoSellInventorySellPercent = 40;
            original.CropHarvestedTotals[(int)PitHero.Farming.CropType.Wheat] = 99;
            original.DishesServedTotals[0] = 5;

            var loaded = new SaveData();
            using (var rdr = new BinaryPersistableReader(new MemoryStream(ToV34Bytes(original))))
                rdr.ReadPersistableInto(loaded);

            Assert.AreEqual(555, loaded.HeroId, "The v34 read consumed exactly the v34 bytes");
            Assert.AreEqual(40, loaded.AutoSellInventorySellPercent);
            Assert.AreEqual(PitHero.Farming.CropTypeInfo.Count, loaded.CropHarvestedTotals.Length);
            Assert.AreEqual(0, loaded.CropHarvestedTotals[(int)PitHero.Farming.CropType.Wheat], "A v34 file has harvested nothing on record");
            Assert.AreEqual(PitHero.Dining.DishTypeInfo.Count, loaded.DishesServedTotals.Length);
            Assert.AreEqual(0, loaded.DishesServedTotals[0]);
        }

        [TestMethod]
        public void SaveData_V34_MonsterRecord_ReadsWithZeroTaskProgress()
        {
            // Build a v34 file with one monster: serialise the same save with and without the monster,
            // diff to find the section-11 count field, then drop the record's three v35 task ints and the tail.
            var without = new SaveData();
            var withMonster = new SaveData();
            withMonster.AlliedMonsters.Add(new SavedAlliedMonster
            {
                Name = "Zed", MonsterTypeName = MonsterTextKey.Monster_Slime,
                FishingProficiency = 1, CookingProficiency = 1, FarmingProficiency = 1,
                MonsterJobId = 0, MonsterHouseId = 3, FishingTasks = 8, CookingTasks = 8, FarmingTasks = 8,
            });
            var msWithout = new MemoryStream();
            using (var writer = new BinaryPersistableWriter(msWithout)) writer.Write(without);
            var msWith = new MemoryStream();
            using (var writer = new BinaryPersistableWriter(msWith)) writer.Write(withMonster);
            byte[] v35 = msWithout.ToArray();
            byte[] v35With = msWith.ToArray();
            int tail = V35Tail(without) + V36Tail(without) + V37Tail(without);

            int countOffset = 0;
            while (countOffset < v35.Length && v35[countOffset] == v35With[countOffset]) countOffset++;
            int recordLen = v35With.Length - v35.Length;          // one v35 record (count field is same width)
            int recordEnd = countOffset + 4 + recordLen;           // first byte after the record
            int keepBeforeTasks = recordEnd - 12;                  // the record minus its 3 task ints

            var v34 = new byte[v35With.Length - 12 - tail];
            Array.Copy(v35With, 0, v34, 0, keepBeforeTasks);
            Array.Copy(v35With, recordEnd, v34, keepBeforeTasks, v35With.Length - tail - recordEnd);
            v34[0] = 34; v34[1] = 0; v34[2] = 0; v34[3] = 0;

            var loaded = new SaveData();
            using (var rdr = new BinaryPersistableReader(new MemoryStream(v34)))
                rdr.ReadPersistableInto(loaded);

            Assert.AreEqual(1, loaded.AlliedMonsters.Count);
            Assert.AreEqual("Zed", loaded.AlliedMonsters[0].Name);
            Assert.AreEqual(3, loaded.AlliedMonsters[0].MonsterHouseId, "Fields after the record read in step");
            Assert.AreEqual(0, loaded.AlliedMonsters[0].FarmingTasks, "A v34 monster has no task progress");
            Assert.AreEqual(0, loaded.CropHarvestedTotals[0]);
        }

        [TestMethod]
        public void SaveData_V35_Defaults()
        {
            var loaded = RoundTrip(new SaveData());
            Assert.AreEqual(PitHero.Farming.CropTypeInfo.Count, loaded.CropHarvestedTotals.Length);
            Assert.AreEqual(PitHero.Dining.DishTypeInfo.Count, loaded.DishesServedTotals.Length);
            Assert.AreEqual(GameConfig.NewGameStartingWheatSeeds, loaded.SeedInventory[(int)PitHero.Farming.CropType.Wheat]);
            Assert.AreEqual(GameConfig.NewGameStartingCornSeeds, loaded.SeedInventory[(int)PitHero.Farming.CropType.Corn]);
            Assert.AreEqual(0, loaded.SeedInventory[(int)PitHero.Farming.CropType.Tomato], "Locked crops are not gifted");
        }

        // ── v37 (issue #417): crop market demand ─────────────────────────────────────

        [TestMethod]
        public void SaveData_V37_CropDemand_RoundTrip()
        {
            var original = new SaveData();
            original.CropDemand[(int)PitHero.Farming.CropType.Wheat] = 0.45f;
            original.CropDemand[(int)PitHero.Farming.CropType.AppleTree] = GameConfig.MarketDemandFloor;

            var loaded = RoundTrip(original);

            Assert.AreEqual(PitHero.Farming.CropTypeInfo.Count, loaded.CropDemand.Length);
            Assert.AreEqual(0.45f, loaded.CropDemand[(int)PitHero.Farming.CropType.Wheat], 0.0001f);
            Assert.AreEqual(GameConfig.MarketDemandFloor, loaded.CropDemand[(int)PitHero.Farming.CropType.AppleTree], 0.0001f);
            Assert.AreEqual(1f, loaded.CropDemand[(int)PitHero.Farming.CropType.Corn], "untouched crops stay at full demand");
        }

        [TestMethod]
        public void SaveData_V36_File_ReadsWithFullDemand()
        {
            var original = new SaveData();
            original.HeroId = 7373;
            original.InventoryItems.Add(new SavedItem { Name = "RustyBlade", SlotIndex = 3, AcquireSeq = 11 });
            original.CropDemand[(int)PitHero.Farming.CropType.Wheat] = 0.3f;

            var ms = new MemoryStream();
            using (var writer = new BinaryPersistableWriter(ms))
                writer.Write(original);
            byte[] v37 = ms.ToArray();
            var v36 = new byte[v37.Length - V37Tail(original)];
            Array.Copy(v37, 0, v36, 0, v36.Length);
            v36[0] = 36; v36[1] = 0; v36[2] = 0; v36[3] = 0;

            var loaded = new SaveData();
            using (var rdr = new BinaryPersistableReader(new MemoryStream(v36)))
                rdr.ReadPersistableInto(loaded);

            Assert.AreEqual(7373, loaded.HeroId);
            Assert.AreEqual(11, loaded.InventoryItems[0].AcquireSeq, "The v36 section still reads in step");
            Assert.AreEqual(PitHero.Farming.CropTypeInfo.Count, loaded.CropDemand.Length);
            Assert.AreEqual(1f, loaded.CropDemand[(int)PitHero.Farming.CropType.Wheat], "A v36 file starts every crop at full demand");
        }

        [TestMethod]
        public void SaveData_V37_Defaults()
        {
            var loaded = RoundTrip(new SaveData());
            Assert.AreEqual(PitHero.Farming.CropTypeInfo.Count, loaded.CropDemand.Length);
            for (int i = 0; i < loaded.CropDemand.Length; i++)
                Assert.AreEqual(1f, loaded.CropDemand[i]);
        }
    }
}
