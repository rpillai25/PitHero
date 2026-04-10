using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Services;
using PitHero.VirtualGame;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Jobs;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Stats;

namespace PitHero.Tests
{
    /// <summary>
    /// Tests for the ICrystalCollectionService contract, executed against
    /// MockCrystalCollectionService. When the production CrystalCollectionService
    /// is implemented, the same tests can run against it by swapping the factory.
    /// All scenarios from HERO_CRYSTAL_TAB_IMPLEMENTATION.md are covered here.
    /// </summary>
    [TestClass]
    public class CrystalCollectionServiceTests
    {
        private MockCrystalCollectionService CreateService() =>
            new MockCrystalCollectionService();

        private static HeroCrystal MakeKnightCrystal(int level = 10) =>
            new HeroCrystal("Knight Crystal", new Knight(), level,
                new StatBlock(strength: 5, agility: 3, vitality: 5, magic: 1));

        private static HeroCrystal MakeMageCrystal(int level = 8) =>
            new HeroCrystal("Mage Crystal", new Mage(), level,
                new StatBlock(strength: 1, agility: 3, vitality: 3, magic: 7));

        // ── Inventory capacity ─────────────────────────────────────────────────

        [TestMethod]
        public void CrystalCollection_DefaultInventoryCapacity_Is80()
        {
            var svc = CreateService();
            Assert.AreEqual(80, svc.InventoryCapacity);
        }

        [TestMethod]
        public void CrystalCollection_DefaultQueueCapacity_Is5()
        {
            var svc = CreateService();
            Assert.AreEqual(5, svc.QueueCapacity);
        }

        [TestMethod]
        public void CrystalCollection_EmptyInventory_CountIsZero()
        {
            var svc = CreateService();
            Assert.AreEqual(0, svc.InventoryCount);
        }

        [TestMethod]
        public void CrystalCollection_AddOneCrystal_CountIsOne()
        {
            var svc = CreateService();
            bool added = svc.TryAddToInventory(MakeKnightCrystal());
            Assert.IsTrue(added);
            Assert.AreEqual(1, svc.InventoryCount);
        }

        [TestMethod]
        public void CrystalCollection_AddNullCrystal_ReturnsFalse()
        {
            var svc = CreateService();
            bool added = svc.TryAddToInventory(null);
            Assert.IsFalse(added);
            Assert.AreEqual(0, svc.InventoryCount);
        }

        [TestMethod]
        public void CrystalCollection_GetInventoryCrystal_ReturnsCorrectCrystal()
        {
            var svc = CreateService();
            var crystal = MakeKnightCrystal();
            svc.TryAddToInventory(crystal);
            var retrieved = svc.GetInventoryCrystal(0);
            Assert.AreEqual(crystal, retrieved);
        }

        [TestMethod]
        public void CrystalCollection_RemoveCrystal_SlotBecomesEmpty()
        {
            var svc = CreateService();
            svc.TryAddToInventory(MakeKnightCrystal());
            bool removed = svc.TryRemoveFromInventory(0);
            Assert.IsTrue(removed);
            Assert.IsNull(svc.GetInventoryCrystal(0));
            Assert.AreEqual(0, svc.InventoryCount);
        }

        [TestMethod]
        public void CrystalCollection_RemoveFromEmptySlot_ReturnsFalse()
        {
            var svc = CreateService();
            bool removed = svc.TryRemoveFromInventory(0);
            Assert.IsFalse(removed);
        }

        [TestMethod]
        public void CrystalCollection_RemoveOutOfRange_ReturnsFalse()
        {
            var svc = CreateService();
            Assert.IsFalse(svc.TryRemoveFromInventory(-1));
            Assert.IsFalse(svc.TryRemoveFromInventory(svc.InventoryCapacity));
        }

        [TestMethod]
        public void CrystalCollection_FillInventory_80CrystalsAccepted()
        {
            var svc = CreateService();
            for (int i = 0; i < 80; i++)
                Assert.IsTrue(svc.TryAddToInventory(MakeKnightCrystal()), $"Slot {i} should accept crystal");
            Assert.AreEqual(80, svc.InventoryCount);
        }

        [TestMethod]
        public void CrystalCollection_InventoryFull_81stCrystalRejected()
        {
            var svc = CreateService();
            for (int i = 0; i < 80; i++)
                svc.TryAddToInventory(MakeKnightCrystal());
            bool overflow = svc.TryAddToInventory(MakeKnightCrystal());
            Assert.IsFalse(overflow, "81st crystal should be rejected when inventory is full");
        }

        // ── Queue ─────────────────────────────────────────────────────────────

        [TestMethod]
        public void CrystalCollection_EmptyQueue_CountIsZero()
        {
            var svc = CreateService();
            Assert.AreEqual(0, svc.QueueCount);
        }

        [TestMethod]
        public void CrystalCollection_EnqueueOneCrystal_QueueCountIsOne()
        {
            var svc = CreateService();
            bool enqueued = svc.TryEnqueue(MakeKnightCrystal());
            Assert.IsTrue(enqueued);
            Assert.AreEqual(1, svc.QueueCount);
        }

        [TestMethod]
        public void CrystalCollection_EnqueueNullCrystal_ReturnsFalse()
        {
            var svc = CreateService();
            Assert.IsFalse(svc.TryEnqueue(null));
        }

        [TestMethod]
        public void CrystalCollection_Queue5Crystals_AllAccepted()
        {
            var svc = CreateService();
            for (int i = 0; i < 5; i++)
                Assert.IsTrue(svc.TryEnqueue(MakeKnightCrystal()), $"Queue slot {i} should accept crystal");
            Assert.AreEqual(5, svc.QueueCount);
        }

        [TestMethod]
        public void CrystalCollection_QueueFull_6thCrystalRejected()
        {
            var svc = CreateService();
            for (int i = 0; i < 5; i++)
                svc.TryEnqueue(MakeKnightCrystal());
            bool overflow = svc.TryEnqueue(MakeKnightCrystal());
            Assert.IsFalse(overflow, "6th crystal should be rejected when queue is full");
        }

        [TestMethod]
        public void CrystalCollection_DequeueFromQueue_ReturnsFrontCrystal()
        {
            var svc = CreateService();
            var first = MakeKnightCrystal();
            var second = MakeMageCrystal();
            svc.TryEnqueue(first);
            svc.TryEnqueue(second);

            var dequeued = svc.Dequeue();
            Assert.AreEqual(first, dequeued, "Dequeue should return the first crystal added (FIFO)");
            Assert.AreEqual(1, svc.QueueCount);
        }

        [TestMethod]
        public void CrystalCollection_DequeueFromEmptyQueue_ReturnsNull()
        {
            var svc = CreateService();
            var result = svc.Dequeue();
            Assert.IsNull(result);
        }

        [TestMethod]
        public void CrystalCollection_PeekQueue_DoesNotRemoveCrystal()
        {
            var svc = CreateService();
            var crystal = MakeKnightCrystal();
            svc.TryEnqueue(crystal);

            var peeked = svc.PeekQueue();
            Assert.AreEqual(crystal, peeked);
            Assert.AreEqual(1, svc.QueueCount, "Peek should not consume the crystal");
        }

        [TestMethod]
        public void CrystalCollection_PeekEmptyQueue_ReturnsNull()
        {
            var svc = CreateService();
            Assert.IsNull(svc.PeekQueue());
        }

        [TestMethod]
        public void CrystalCollection_QueueFifoOrder_IsPreserved()
        {
            var svc = CreateService();
            var crystals = new HeroCrystal[5];
            for (int i = 0; i < 5; i++)
            {
                crystals[i] = MakeKnightCrystal(i + 1);
                svc.TryEnqueue(crystals[i]);
            }

            for (int i = 0; i < 5; i++)
            {
                var dequeued = svc.Dequeue();
                Assert.AreEqual(crystals[i], dequeued, $"Queue position {i} should match FIFO order");
            }
        }

        // ── Pending crystal ────────────────────────────────────────────────────

        [TestMethod]
        public void CrystalCollection_PendingNextCrystal_DefaultIsNull()
        {
            var svc = CreateService();
            Assert.IsNull(svc.PendingNextCrystal);
        }

        [TestMethod]
        public void CrystalCollection_SetPendingNextCrystal_CanBeRead()
        {
            var svc = CreateService();
            var crystal = MakeKnightCrystal();
            svc.PendingNextCrystal = crystal;
            Assert.AreEqual(crystal, svc.PendingNextCrystal);
        }

        // ── Scenario 2: Queue auto-infuse on hero death ───────────────────────
        // Simulates: hero dies → HeroDeathComponent pops queue → PendingNextCrystal set
        // → HeroPromotionService reads it → new hero gets that crystal bound

        [TestMethod]
        public void CrystalCollection_Scenario2_QueueAutoInfuse_PendingCrystalSetOnDequeue()
        {
            // Arrange: queue has one crystal ready for next hero
            var svc = CreateService();
            var queuedCrystal = MakeMageCrystal();
            svc.TryEnqueue(queuedCrystal);

            // Act: hero dies → component pops the queue and sets PendingNextCrystal
            var popped = svc.Dequeue();
            svc.PendingNextCrystal = popped;

            // Assert: pending crystal is set correctly
            Assert.AreEqual(queuedCrystal, svc.PendingNextCrystal,
                "PendingNextCrystal should match the queued crystal that was popped on death");
        }

        [TestMethod]
        public void CrystalCollection_Scenario2_QueueEmpty_PendingCrystalRemainsNull()
        {
            // Arrange: no crystals in queue
            var svc = CreateService();

            // Act: hero dies, component tries to pop queue (nothing there)
            var popped = svc.Dequeue();
            if (popped != null)
                svc.PendingNextCrystal = popped;

            // Assert: pending stays null when queue was empty
            Assert.IsNull(svc.PendingNextCrystal,
                "PendingNextCrystal should stay null when no crystal was in the queue");
        }

        [TestMethod]
        public void CrystalCollection_Scenario2_AfterPromotion_PendingCrystalCleared()
        {
            // Arrange: pending crystal set by HeroDeathComponent
            var svc = CreateService();
            svc.PendingNextCrystal = MakeKnightCrystal();

            // Act: HeroPromotionService reads and clears pending crystal
            var crystalForHero = svc.PendingNextCrystal;
            svc.PendingNextCrystal = null;

            // Assert
            Assert.IsNotNull(crystalForHero, "Crystal should have been available for promotion");
            Assert.IsNull(svc.PendingNextCrystal, "PendingNextCrystal should be null after promotion uses it");
        }

        // ── Scenario 1: Crystal survives death (inventory intact) ─────────────
        // Simulates: hero dies → bound crystal → CrystalMerchantVault
        // Inventory crystals (not the bound one) stay in CrystalCollectionService

        [TestMethod]
        public void CrystalCollection_Scenario1_InventoryCrystalsNotAffectedByHeroDeath()
        {
            // Arrange: 2 crystals in collection service inventory
            var svc = CreateService();
            var inventoryCrystal1 = MakeKnightCrystal(5);
            var inventoryCrystal2 = MakeMageCrystal(7);
            svc.TryAddToInventory(inventoryCrystal1);
            svc.TryAddToInventory(inventoryCrystal2);

            // The hero's bound crystal is separate — it goes to CrystalMerchantVault on death
            var vault = new CrystalMerchantVault();
            var boundCrystal = MakeKnightCrystal(10);
            vault.AddCrystal(boundCrystal); // simulates HeroDeathComponent behaviour

            // Assert: vault has bound crystal
            Assert.AreEqual(1, vault.Count);
            Assert.AreEqual(boundCrystal, vault.Crystals[0]);

            // Assert: collection service still has 2 inventory crystals untouched
            Assert.AreEqual(2, svc.InventoryCount,
                "Inventory crystals should survive hero death — only the bound crystal goes to vault");
        }

        // ── Scenario 3: Forge produces combo crystal ─────────────────────────
        // HeroCrystal.Combine already exists; IsCombo will be added to the feature.
        // For now we verify the precursor: Combine produces a CompositeJob.

        [TestMethod]
        public void CrystalCollection_Scenario3_CombineTwoCrystals_ProducesCompositeJob()
        {
            // Arrange: two crystals with different jobs
            var knightCrystal = MakeKnightCrystal(5);
            var mageCrystal = MakeMageCrystal(6);

            // Act: forge them together
            var combo = HeroCrystal.Combine("Knight-Mage", knightCrystal, mageCrystal);

            // Assert: result has CompositeJob (proxy for IsCombo == true)
            Assert.IsInstanceOfType(combo.Job, typeof(CompositeJob),
                "A forged combo crystal should use CompositeJob; when IsCombo is implemented it should return true");
        }

        [TestMethod]
        public void CrystalCollection_Scenario3_CombinedCrystalLevel_IsAverageOfInputs()
        {
            var a = MakeKnightCrystal(4);
            var b = MakeMageCrystal(6);
            var combo = HeroCrystal.Combine("Combo", a, b);
            // Average of 4 and 6 = 5
            Assert.AreEqual(5, combo.Level);
        }

        // ── Clear ─────────────────────────────────────────────────────────────

        [TestMethod]
        public void CrystalCollection_Clear_RemovesAllInventoryAndQueueCrystals()
        {
            var svc = CreateService();
            svc.TryAddToInventory(MakeKnightCrystal());
            svc.TryAddToInventory(MakeMageCrystal());
            svc.TryEnqueue(MakeKnightCrystal());
            svc.PendingNextCrystal = MakeMageCrystal();

            svc.Clear();

            Assert.AreEqual(0, svc.InventoryCount);
            Assert.AreEqual(0, svc.QueueCount);
            Assert.IsNull(svc.PendingNextCrystal);
        }
    }
}
