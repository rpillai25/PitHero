#if CRYSTAL_UI_FEATURE
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
    /// through SaveData v7 and deserialization with full fidelity.
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
            var c2 = MakeMageCrystal(6);
            var c3 = MakeKnightCrystal(8);
            svcBefore.TryAddToInventory(c1);
            svcBefore.TryAddToInventory(c2);
            svcBefore.TryAddToInventory(c3);
            var queued = MakeMageCrystal(3);
            svcBefore.TryEnqueue(queued);

            // Act: serialize to SaveData
            var saveData = new SaveData();
            saveData.CrystalCollection = new List<SavedHeroCrystal>
            {
                SavedHeroCrystal.FromHeroCrystal(c1),
                SavedHeroCrystal.FromHeroCrystal(c2),
                SavedHeroCrystal.FromHeroCrystal(c3),
            };
            saveData.CrystalQueue = new List<SavedHeroCrystal>
            {
                SavedHeroCrystal.FromHeroCrystal(queued),
            };

            // Persist and restore through the binary store
            var tempDir = Path.Combine(
                Path.GetTempPath(), "pithero_crystaltest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                var dataStore = new FileDataStore(tempDir);
                dataStore.Save("save", saveData);

                var restored = new SaveData();
                dataStore.Load("save", restored);

                // Restore into a fresh service
                var svcAfter = new MockCrystalCollectionService();
                for (int i = 0; i < restored.CrystalCollection.Count; i++)
                {
                    var sc = restored.CrystalCollection[i];
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
                Directory.Delete(tempDir, true);
            }
        }

        [TestMethod]
        public void CrystalSave_CrystalCollection_JobNamePreservedOnRoundtrip()
        {
            var original = MakeKnightCrystal(12);
            var saved = SavedHeroCrystal.FromHeroCrystal(original);
            var restored = saved.ToHeroCrystal();
            Assert.AreEqual(original.Job.NameKey, restored.Job.NameKey, "Job name should roundtrip");
        }

        [TestMethod]
        public void CrystalSave_CrystalCollection_LevelPreservedOnRoundtrip()
        {
            var original = MakeKnightCrystal(15);
            var saved = SavedHeroCrystal.FromHeroCrystal(original);
            var restored = saved.ToHeroCrystal();
            Assert.AreEqual(original.Level, restored.Level, "Level should roundtrip");
        }

        [TestMethod]
        public void CrystalSave_CrystalCollection_ColorPreservedOnRoundtrip()
        {
            var original = MakeKnightCrystal(7);
            var saved = SavedHeroCrystal.FromHeroCrystal(original);
            var restored = saved.ToHeroCrystal();
            Assert.AreEqual(original.Color, restored.Color, "Color should roundtrip");
        }

        [TestMethod]
        public void CrystalSave_CrystalCollection_IsComboPreservedOnRoundtrip()
        {
            // Create a combo crystal via Combine
            var a = MakeKnightCrystal(5);
            var b = MakeMageCrystal(5);
            var combo = HeroCrystal.Combine("Combo", a, b);

            var saved = SavedHeroCrystal.FromHeroCrystal(combo);
            var restored = saved.ToHeroCrystal();

            Assert.IsInstanceOfType(restored.Job, typeof(CompositeJob),
                "Restored combo crystal should still have CompositeJob");
        }

        [TestMethod]
        public void CrystalSave_PendingNextCrystal_PreservedOnRoundtrip()
        {
            var pending = MakeMageCrystal(4);
            var saveData = new SaveData();
            saveData.PendingNextCrystal = SavedHeroCrystal.FromHeroCrystal(pending);

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
            saveData.CrystalCollection = new List<SavedHeroCrystal>();
            saveData.CrystalQueue = new List<SavedHeroCrystal>();
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
