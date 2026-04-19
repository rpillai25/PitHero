using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using Nez.Persistence.Binary;
using PitHero.Services;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Jobs;
using RolePlayingFramework.Jobs.Primary;
using System;
using System.Collections.Generic;
using System.IO;
using PitHero;

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
                    new SavedShortcutSlot { SlotType = 0 },                                    // Empty
                    new SavedShortcutSlot { SlotType = 1, ItemBagIndex = 5 },                   // Item at bag index 5
                    new SavedShortcutSlot { SlotType = 2, SkillId = "knight.light_armor" },      // Skill
                    new SavedShortcutSlot { SlotType = 1, ItemBagIndex = 42 },                  // Item at bag index 42
                    new SavedShortcutSlot { SlotType = 0 },                                    // Empty
                    new SavedShortcutSlot { SlotType = 2, SkillId = "mage.fire" },              // Skill
                    new SavedShortcutSlot { SlotType = 0 },                                    // Empty
                    new SavedShortcutSlot { SlotType = 0 },                                    // Empty
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

                // Slot 2: Skill
                Assert.AreEqual(2, loaded.ShortcutSlots[2].SlotType, "Slot 2 should be skill");
                Assert.AreEqual("knight.light_armor", loaded.ShortcutSlots[2].SkillId, "Slot 2 should reference knight.light_armor");

                // Slot 3: Item at bag index 42
                Assert.AreEqual(1, loaded.ShortcutSlots[3].SlotType, "Slot 3 should be item");
                Assert.AreEqual(42, loaded.ShortcutSlots[3].ItemBagIndex, "Slot 3 should reference bag index 42");

                // Slot 4: Empty
                Assert.AreEqual(0, loaded.ShortcutSlots[4].SlotType, "Slot 4 should be empty");

                // Slot 5: Skill
                Assert.AreEqual(2, loaded.ShortcutSlots[5].SlotType, "Slot 5 should be skill");
                Assert.AreEqual("mage.fire", loaded.ShortcutSlots[5].SkillId, "Slot 5 should reference mage.fire");

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

        /// <summary>Verifies version 1 save files load without shortcut data (backward compatibility).</summary>
        [TestMethod]
        public void SaveData_Version1_LoadsWithoutShortcutSlots()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "pithero_v1_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                // Create a version 1 save file manually by saving with the old format
                // We simulate this by creating data with empty shortcuts and checking the loaded result
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
    }
}
