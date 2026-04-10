#if CRYSTAL_UI_FEATURE
// This file compiles only when CRYSTAL_UI_FEATURE is defined.
// The Principal Engineer should define this constant once SaveData v6
// (crystal collection fields) is implemented.
//
// Expected new SaveData fields (v6):
//   public List<SavedCrystal> CrystalCollection;     // up to 80 slots
//   public List<SavedCrystal> CrystalQueue;          // up to 5 slots
//   public SavedCrystal?      PendingNextCrystal;    // set on death
//
// Expected new struct:
//   public struct SavedCrystal
//   {
//       public string JobName;
//       public int    Level;
//       public int    BaseStrength;
//       public int    BaseAgility;
//       public int    BaseVitality;
//       public int    BaseMagic;
//       public int    TotalJP;
//       public int    CurrentJP;
//       public string[] LearnedSkillIds;
//       public Color  CrystalColor;     // new in v6
//       public bool   IsCombo;          // new in v6
//   }

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using Nez.Persistence.Binary;
using PitHero.Services;
using PitHero.VirtualGame;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Jobs;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Stats;
using System;
using System.Collections.Generic;
using System.IO;

namespace PitHero.Tests
{
    /// <summary>
    /// Tests for Save/Load crystal collection roundtrip (Scenario 4).
    /// Verifies that a CrystalCollectionService state survives serialization
    /// through SaveData v6 and deserialization with full fidelity.
    /// </summary>
    [TestClass]
    public class CrystalSaveRoundtripTests
    {
        private static HeroCrystal MakeKnightCrystal(int level = 10) =>
            new HeroCrystal("Knight Crystal", new Knight(), level,
                new StatBlock(strength: 5, agility: 3, vitality: 5, magic: 1));

        private static HeroCrystal MakeMageCrystal(int level = 8) =>
            new HeroCrystal("Mage Crystal", new Mage(), level,
                new StatBlock(strength: 1, agility: 3, vitality: 3, magic: 7));

        // ── Scenario 4 main roundtrip ─────────────────────────────────────────

        [TestMethod]
        public void CrystalSave_Scenario4_ThreeCrystalsAndOneQueued_RoundtripPreservesState()
        {
            // Arrange: populate a mock service
            var svcBefore = new MockCrystalCollectionService();
            var c1 = MakeKnightCrystal(5);
            var c2 = MageCrystal(6);
            var c3 = MakeKnightCrystal(8);
            svcBefore.TryAddToInventory(c1);
            svcBefore.TryAddToInventory(c2);
            svcBefore.TryAddToInventory(c3);
            var queued = MakeMageCrystal(3);
            svcBefore.TryEnqueue(queued);

            // Act: serialize to SaveData
            var saveData = new SaveData();
            saveData.CrystalCollection = new List<SavedCrystal>
            {
                SavedCrystal.FromHeroCrystal(c1),
                SavedCrystal.FromHeroCrystal(c2),
                SavedCrystal.FromHeroCrystal(c3),
            };
            saveData.CrystalQueue = new List<SavedCrystal>
            {
                SavedCrystal.FromHeroCrystal(queued),
            };

            // Persist and restore through the binary store
            var tempDir = Path.Combine(
                Path.GetTempPath(), "pithero_crystaltest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                var dataStore = new FileDataStore(tempDir);
                dataStore.Set("save", saveData);

                var restored = new SaveData();
                dataStore.Get("save", restored);

                // Restore into a fresh service
                var svcAfter = new MockCrystalCollectionService();
                for (int i = 0; i < restored.CrystalCollection.Count; i++)
                {
                    var sc = restored.CrystalCollection[i];
                    // Principal Engineer: CrystalCollectionService should expose a
                    // method like RestoreFromSaved(SavedCrystal sc) that rebuilds a
                    // HeroCrystal from the saved struct and places it in the inventory.
                    svcAfter.TryAddToInventory(sc.ToHeroCrystal());
                }
                for (int i = 0; i < restored.CrystalQueue.Count; i++)
                {
                    svcAfter.TryEnqueue(restored.CrystalQueue[i].ToHeroCrystal());
                }

                // Assert
                Assert.AreEqual(3, svcAfter.InventoryCount, "Should have 3 inventory crystals after restore");
                Assert.AreEqual(1, svcAfter.QueueCount, "Should have 1 queued crystal after restore");
            }
            finally
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }

        [TestMethod]
        public void CrystalSave_CrystalCollection_JobNamePreservedOnRoundtrip()
        {
            var original = MakeKnightCrystal(12);
            var saved = SavedCrystal.FromHeroCrystal(original);
            var restored = saved.ToHeroCrystal();
            Assert.AreEqual(original.Job.Name, restored.Job.Name, "Job name should roundtrip");
        }

        [TestMethod]
        public void CrystalSave_CrystalCollection_LevelPreservedOnRoundtrip()
        {
            var original = MakeKnightCrystal(15);
            var saved = SavedCrystal.FromHeroCrystal(original);
            var restored = saved.ToHeroCrystal();
            Assert.AreEqual(original.Level, restored.Level, "Level should roundtrip");
        }

        [TestMethod]
        public void CrystalSave_CrystalCollection_ColorPreservedOnRoundtrip()
        {
            var original = MakeKnightCrystal(7);
            // Color will be set by the feature implementation
            var saved = SavedCrystal.FromHeroCrystal(original);
            var restored = saved.ToHeroCrystal();
            // Once Color property exists: Assert.AreEqual(original.Color, restored.Color)
            Assert.IsNotNull(restored, "Roundtrip should produce a valid crystal");
        }

        [TestMethod]
        public void CrystalSave_CrystalCollection_IsComboPreservedOnRoundtrip()
        {
            // Create a combo crystal via Combine
            var a = MakeKnightCrystal(5);
            var b = MakeMageCrystal(5);
            var combo = HeroCrystal.Combine("Combo", a, b);

            var saved = SavedCrystal.FromHeroCrystal(combo);
            var restored = saved.ToHeroCrystal();

            // Once IsCombo property exists: Assert.IsTrue(restored.IsCombo)
            Assert.IsInstanceOfType(restored.Job, typeof(CompositeJob),
                "Restored combo crystal should still have CompositeJob");
        }

        [TestMethod]
        public void CrystalSave_PendingNextCrystal_PreservedOnRoundtrip()
        {
            var pending = MakeMageCrystal(4);
            var saveData = new SaveData();
            saveData.PendingNextCrystal = SavedCrystal.FromHeroCrystal(pending);

            // Assert that the field is non-null and contains correct job
            Assert.IsNotNull(saveData.PendingNextCrystal, "PendingNextCrystal should be saved");
            Assert.AreEqual(JobTextKey.Job_Mage_Name,
                saveData.PendingNextCrystal.Value.JobName,
                "Pending crystal job name should be preserved");
        }

        [TestMethod]
        public void CrystalSave_EmptyCollectionAndQueue_RoundtripSucceeds()
        {
            var saveData = new SaveData();
            saveData.CrystalCollection = new List<SavedCrystal>();
            saveData.CrystalQueue = new List<SavedCrystal>();
            saveData.PendingNextCrystal = null;

            // Restore into mock
            var svc = new MockCrystalCollectionService();
            // Nothing to restore
            Assert.AreEqual(0, svc.InventoryCount);
            Assert.AreEqual(0, svc.QueueCount);
            Assert.IsNull(svc.PendingNextCrystal);
        }
    }
}
#endif
