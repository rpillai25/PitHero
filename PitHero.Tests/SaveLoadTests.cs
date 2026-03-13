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
                original.JobName = "Knight";
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
                original.CrystalJobName = "Knight";
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
                    new SavedAlliedMonster { Name = "Bob", MonsterTypeName = "Slime", FishingProficiency = 3, CookingProficiency = 5, FarmingProficiency = 7 }
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
                original.JobName = "Mage";
                original.Level = 1;

                dataStore.Save("test_empty.bin", original);

                var loaded = new SaveData();
                dataStore.Load("test_empty.bin", loaded);

                Assert.AreEqual("EmptyHero", loaded.HeroName);
                Assert.AreEqual("Mage", loaded.JobName);
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
            var jobNames = new string[] { "Knight", "Mage", "Monk", "Priest", "Archer", "Thief" };
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
            var job = JobFactory.CreateJob("Knight-Mage");
            Assert.IsNotNull(job);
            Assert.AreEqual("Knight-Mage", job.Name);
        }

        /// <summary>Verifies JobFactory defaults to Knight for unknown job names.</summary>
        [TestMethod]
        public void JobFactory_CreateJob_DefaultsToKnightForUnknown()
        {
            var job = JobFactory.CreateJob("UnknownJob");
            Assert.IsNotNull(job);
            Assert.AreEqual("Knight", job.Name);
        }
    }
}
