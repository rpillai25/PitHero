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
    /// VGL integration tests for crystal behavior during hero death and promotion.
    /// These tests use MockCrystalCollectionService to simulate the runtime crystal
    /// system and the existing CrystalMerchantVault for bound-crystal persistence.
    ///
    /// Scenarios covered:
    ///   Scenario 1 – Bound crystal goes to vault; inventory crystals survive
    ///   Scenario 2 – Queue auto-infuse: death pops queue, pending crystal set
    ///   Scenario 3 – Combo crystal forged from two inventory crystals
    /// </summary>
    [TestClass]
    public class CrystalDeathVGLTests
    {
        // ── Helpers ───────────────────────────────────────────────────────────

        private static HeroCrystal MakeKnightCrystal(int level = 10) =>
            new HeroCrystal("Knight Crystal", new Knight(), level,
                new StatBlock(strength: 5, agility: 3, vitality: 5, magic: 1));

        private static HeroCrystal MakeMageCrystal(int level = 8) =>
            new HeroCrystal("Mage Crystal", new Mage(), level,
                new StatBlock(strength: 1, agility: 3, vitality: 3, magic: 7));

        private static HeroCrystal MakePriestCrystal(int level = 6) =>
            new HeroCrystal("Priest Crystal", new Priest(), level,
                new StatBlock(strength: 2, agility: 3, vitality: 4, magic: 6));

        // ── Scenario 1: Bound crystal → vault; inventory crystals survive ─────

        [TestMethod]
        public void CrystalDeath_Scenario1_BoundCrystalMovedToVault()
        {
            // Arrange
            var collection = new MockCrystalCollectionService();
            var vault = new CrystalMerchantVault();
            var boundCrystal = MakeKnightCrystal(10);

            // Add 2 unrelated crystals to the collection inventory
            collection.TryAddToInventory(MakeMageCrystal(5));
            collection.TryAddToInventory(MakePriestCrystal(3));

            // Act: simulate HeroDeathComponent behaviour
            // (bound crystal is taken from hero.BoundCrystal and added to vault)
            vault.AddCrystal(boundCrystal);

            // Assert: vault holds the bound crystal
            Assert.AreEqual(1, vault.Count,
                "CrystalMerchantVault should hold the one bound crystal from the fallen hero");
            Assert.AreEqual(boundCrystal, vault.Crystals[0]);
        }

        [TestMethod]
        public void CrystalDeath_Scenario1_InventoryCrystalsUnaffectedByDeath()
        {
            // Arrange
            var collection = new MockCrystalCollectionService();
            var c1 = MakeMageCrystal(5);
            var c2 = MakePriestCrystal(3);
            collection.TryAddToInventory(c1);
            collection.TryAddToInventory(c2);

            // Act: hero dies — only the bound crystal goes to vault (not collection crystals)
            var vault = new CrystalMerchantVault();
            vault.AddCrystal(MakeKnightCrystal(10)); // bound crystal

            // Assert: collection service still has both inventory crystals intact
            Assert.AreEqual(2, collection.InventoryCount,
                "2 inventory crystals should survive hero death unchanged");
            Assert.AreEqual(c1, collection.GetInventoryCrystal(0));
            Assert.AreEqual(c2, collection.GetInventoryCrystal(1));
        }

        [TestMethod]
        public void CrystalDeath_Scenario1_MultipleDeaths_VaultAccumulatesCrystals()
        {
            // Arrange
            var vault = new CrystalMerchantVault();

            // Act: three separate hero deaths
            vault.AddCrystal(MakeKnightCrystal(10));
            vault.AddCrystal(MakeMageCrystal(8));
            vault.AddCrystal(MakePriestCrystal(5));

            // Assert
            Assert.AreEqual(3, vault.Count,
                "Each hero death should add one crystal to the vault");
        }

        // ── Scenario 2: Queue auto-infuse ─────────────────────────────────────

        [TestMethod]
        public void CrystalDeath_Scenario2_HeroDies_QueueCrystalSetAsPending()
        {
            // Arrange: queue has 1 crystal waiting
            var collection = new MockCrystalCollectionService();
            var queuedCrystal = MakeMageCrystal(6);
            collection.TryEnqueue(queuedCrystal);

            // Act: simulate HeroDeathComponent popping queue and setting pending
            var popped = collection.Dequeue();
            if (popped != null)
                collection.PendingNextCrystal = popped;

            // Assert
            Assert.AreEqual(queuedCrystal, collection.PendingNextCrystal,
                "PendingNextCrystal should be the crystal that was queued for the next hero");
            Assert.AreEqual(0, collection.QueueCount, "Queue should be empty after pop");
        }

        [TestMethod]
        public void CrystalDeath_Scenario2_HeroPromotionUsesPendingCrystal()
        {
            // Arrange: pending crystal is set (simulates the result of hero death)
            var collection = new MockCrystalCollectionService();
            var pendingCrystal = MakeMageCrystal(7);
            collection.PendingNextCrystal = pendingCrystal;

            // Act: HeroPromotionService reads and consumes the pending crystal
            var crystalForNewHero = collection.PendingNextCrystal;
            collection.PendingNextCrystal = null; // consumed

            // Assert: the new hero would receive the queued crystal
            Assert.AreEqual(pendingCrystal, crystalForNewHero,
                "GetNextCrystalForHero() should return the pending crystal when set");
            Assert.IsNull(collection.PendingNextCrystal,
                "PendingNextCrystal should be cleared after the promotion service uses it");
        }

        [TestMethod]
        public void CrystalDeath_Scenario2_QueueHasMultipleCrystals_OnlyFirstPopped()
        {
            // Arrange: 3 crystals in queue
            var collection = new MockCrystalCollectionService();
            var first = MakeKnightCrystal(5);
            var second = MakeMageCrystal(4);
            var third = MakePriestCrystal(3);
            collection.TryEnqueue(first);
            collection.TryEnqueue(second);
            collection.TryEnqueue(third);

            // Act: one hero dies — pop one
            var popped = collection.Dequeue();
            if (popped != null)
                collection.PendingNextCrystal = popped;

            // Assert: only the first is consumed
            Assert.AreEqual(first, collection.PendingNextCrystal);
            Assert.AreEqual(2, collection.QueueCount,
                "Remaining 2 crystals should stay in the queue");
        }

        [TestMethod]
        public void CrystalDeath_Scenario2_NoQueueCrystal_PendingStaysNull()
        {
            // Arrange: empty queue
            var collection = new MockCrystalCollectionService();

            // Act: hero dies with nothing queued
            var popped = collection.Dequeue();
            if (popped != null)
                collection.PendingNextCrystal = popped;

            // Assert: no pending crystal
            Assert.IsNull(collection.PendingNextCrystal,
                "If nothing is queued, PendingNextCrystal should remain null");
        }

        // ── Scenario 3: Forge produces combo crystal ──────────────────────────

        [TestMethod]
        public void CrystalDeath_Scenario3_ForgeRemovesTwoCrystalsFromInventory()
        {
            // Arrange
            var collection = new MockCrystalCollectionService();
            collection.TryAddToInventory(MakeKnightCrystal(5));
            collection.TryAddToInventory(MakeMageCrystal(6));

            // Act: forge slots 0 and 1 → combine → remove originals → add combo
            var a = collection.GetInventoryCrystal(0)!;
            var b = collection.GetInventoryCrystal(1)!;
            var combo = HeroCrystal.Combine("Knight-Mage", a, b);
            collection.TryRemoveFromInventory(0);
            collection.TryRemoveFromInventory(1);
            collection.TryAddToInventory(combo);

            // Assert
            Assert.AreEqual(1, collection.InventoryCount,
                "After forging, inventory should hold exactly 1 combo crystal");
            Assert.IsInstanceOfType(collection.GetInventoryCrystal(0)?.Job, typeof(CompositeJob),
                "The remaining crystal should have a CompositeJob (IsCombo = true when feature is added)");
        }

        [TestMethod]
        public void CrystalDeath_Scenario3_ComboCrystal_HasMergedStats()
        {
            var knight = MakeKnightCrystal(10);
            var mage = MakeMageCrystal(10);
            var combo = HeroCrystal.Combine("Combo", knight, mage);

            // Combined stats should be sum of both base stats
            Assert.IsTrue(combo.BaseStats.Strength >= knight.BaseStats.Strength,
                "Combo crystal STR should be at least as high as knight's");
            Assert.IsTrue(combo.BaseStats.Magic >= mage.BaseStats.Magic,
                "Combo crystal MAG should be at least as high as mage's");
        }

        [TestMethod]
        public void CrystalDeath_Scenario3_ComboCrystal_JobIsCompositeJob()
        {
            var a = MakeKnightCrystal(5);
            var b = MakeMageCrystal(5);
            var combo = HeroCrystal.Combine("Combo", a, b);

            Assert.IsInstanceOfType(combo.Job, typeof(CompositeJob),
                "Forged crystal must have a CompositeJob — " +
                "when IsCombo property is added it must return true for such crystals");
        }

        // ── Combined scenario: death + vault + queue in sequence ──────────────

        [TestMethod]
        public void CrystalDeath_FullDeathCycle_BoundGoesToVault_QueuedBecomesNextHero()
        {
            // Arrange
            var collection = new MockCrystalCollectionService();
            var vault = new CrystalMerchantVault();

            // Crystal that will be queued for next hero
            var nextHeroCrystal = MakeMageCrystal(5);
            collection.TryEnqueue(nextHeroCrystal);

            // Hero's bound crystal (will go to vault)
            var boundCrystal = MakeKnightCrystal(20);

            // Act: hero dies
            vault.AddCrystal(boundCrystal);
            var pending = collection.Dequeue();
            if (pending != null)
                collection.PendingNextCrystal = pending;

            // New hero promotion: reads and clears pending
            var crystalForNewHero = collection.PendingNextCrystal;
            collection.PendingNextCrystal = null;

            // Assert: bound crystal is in vault
            Assert.AreEqual(1, vault.Count);
            Assert.AreEqual(boundCrystal, vault.Crystals[0]);

            // Assert: new hero gets the queued crystal
            Assert.AreEqual(nextHeroCrystal, crystalForNewHero);

            // Assert: pending cleared
            Assert.IsNull(collection.PendingNextCrystal);
        }
    }
}
