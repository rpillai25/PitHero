using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero;
using RolePlayingFramework.Combat;

namespace PitHero.Tests
{
    /// <summary>
    /// Tests for the DamageTracker class: circular buffer, smooth decay formula,
    /// expected damage calculation, fallback behavior, and reset.
    /// </summary>
    [TestClass]
    public class DamageTrackerTests
    {
        #region Constructor Tests

        [TestMethod]
        public void Constructor_InitializesWithZeroState()
        {
            var tracker = new DamageTracker(5);
            Assert.AreEqual(0, tracker.MaxDamageTaken);
            Assert.AreEqual(0, tracker.BattleMaxDamageTaken);
            Assert.AreEqual(0, tracker.GetExpectedDamageInBattle());
            Assert.AreEqual(0, tracker.GetExpectedDamageOutOfBattle());
        }

        #endregion

        #region RecordDamage Tests

        [TestMethod]
        public void RecordDamage_UpdatesBattleMaxDamageTaken()
        {
            var tracker = new DamageTracker(5);
            tracker.RecordDamage(10);
            Assert.AreEqual(10, tracker.BattleMaxDamageTaken);

            tracker.RecordDamage(20);
            Assert.AreEqual(20, tracker.BattleMaxDamageTaken);

            tracker.RecordDamage(5);
            Assert.AreEqual(20, tracker.BattleMaxDamageTaken);
        }

        [TestMethod]
        public void RecordDamage_IgnoresZeroAndNegative()
        {
            var tracker = new DamageTracker(5);
            tracker.RecordDamage(0);
            Assert.AreEqual(0, tracker.BattleMaxDamageTaken);

            tracker.RecordDamage(-5);
            Assert.AreEqual(0, tracker.BattleMaxDamageTaken);
        }

        #endregion

        #region OnBattleStart Tests

        [TestMethod]
        public void OnBattleStart_ResetsBattleMaxDamageTaken()
        {
            var tracker = new DamageTracker(5);
            tracker.RecordDamage(50);
            Assert.AreEqual(50, tracker.BattleMaxDamageTaken);

            tracker.OnBattleStart();
            Assert.AreEqual(0, tracker.BattleMaxDamageTaken);
        }

        #endregion

        #region OnBattleEnd Tests

        [TestMethod]
        public void OnBattleEnd_FirstBattle_SetsMaxDamageTakenDirectly()
        {
            var tracker = new DamageTracker(5);
            tracker.RecordDamage(30);
            tracker.OnBattleEnd();

            Assert.AreEqual(30, tracker.MaxDamageTaken);
        }

        [TestMethod]
        public void OnBattleEnd_HigherBattleMax_TakesNewValue()
        {
            var tracker = new DamageTracker(5);

            // First battle: establish max
            tracker.RecordDamage(30);
            tracker.OnBattleEnd();
            Assert.AreEqual(30, tracker.MaxDamageTaken);

            // Second battle: higher damage
            tracker.OnBattleStart();
            tracker.RecordDamage(50);
            tracker.OnBattleEnd();
            Assert.AreEqual(50, tracker.MaxDamageTaken);
        }

        [TestMethod]
        public void OnBattleEnd_LowerBattleMax_BlendsWithDecay()
        {
            var tracker = new DamageTracker(5);

            // First battle: establish max at 100
            tracker.RecordDamage(100);
            tracker.OnBattleEnd();
            Assert.AreEqual(100, tracker.MaxDamageTaken);

            // Second battle: lower damage
            tracker.OnBattleStart();
            tracker.RecordDamage(40);
            tracker.OnBattleEnd();

            // Expected: 0.7 * 100 + 0.3 * 40 = 70 + 12 = 82
            Assert.AreEqual(82, tracker.MaxDamageTaken);
        }

        [TestMethod]
        public void OnBattleEnd_NoBattleDamage_DecaysTowardZero()
        {
            var tracker = new DamageTracker(5);

            // First battle: establish max
            tracker.RecordDamage(100);
            tracker.OnBattleEnd();

            // Second battle: no damage taken
            tracker.OnBattleStart();
            tracker.OnBattleEnd();

            // Expected: 0.7 * 100 + 0.3 * 0 = 70
            Assert.AreEqual(70, tracker.MaxDamageTaken);
        }

        [TestMethod]
        public void OnBattleEnd_NoBattleDamage_NoHistory_StaysZero()
        {
            var tracker = new DamageTracker(5);
            tracker.OnBattleStart();
            tracker.OnBattleEnd();
            Assert.AreEqual(0, tracker.MaxDamageTaken);
        }

        #endregion

        #region GetExpectedDamageInBattle Tests

        [TestMethod]
        public void GetExpectedDamageInBattle_NoHistory_ReturnsZero()
        {
            var tracker = new DamageTracker(5);
            Assert.AreEqual(0, tracker.GetExpectedDamageInBattle());
        }

        [TestMethod]
        public void GetExpectedDamageInBattle_ReturnsMaxOfRecentAvgAndScaledMax()
        {
            var tracker = new DamageTracker(5);

            // Establish max at 100
            tracker.RecordDamage(100);
            tracker.OnBattleEnd();

            // New battle with lower damage samples
            tracker.OnBattleStart();
            tracker.RecordDamage(10);
            tracker.RecordDamage(10);
            tracker.RecordDamage(10);

            // Recent average = (100 + 10 + 10 + 10) / 4 = 32 (buffer still has first battle's 100)
            // Scaled max = 100 * 7 / 10 = 70
            // Expected: Max(32, 70) = 70
            int expected = tracker.GetExpectedDamageInBattle();
            Assert.IsTrue(expected >= 70, $"Expected at least 70, got {expected}");
        }

        [TestMethod]
        public void GetExpectedDamageInBattle_HighRecentAvg_UsesRecentAvg()
        {
            var tracker = new DamageTracker(5);

            // Establish a low max
            tracker.RecordDamage(10);
            tracker.OnBattleEnd();

            // New battle with higher damage samples filling the buffer
            tracker.OnBattleStart();
            tracker.RecordDamage(50);
            tracker.RecordDamage(50);
            tracker.RecordDamage(50);
            tracker.RecordDamage(50);
            tracker.RecordDamage(50);

            // Recent average = 50 (circular buffer is full of 50s)
            // Scaled max = 50 * 7 / 10 = 35 (OnBattleEnd updated max to 50 from first battle's 10)
            // Wait - max is still 10 from first OnBattleEnd; these are current battle samples
            // Scaled max = 10 * 7 / 10 = 7
            // Expected: Max(50, 7) = 50
            int expected = tracker.GetExpectedDamageInBattle();
            Assert.AreEqual(50, expected);
        }

        #endregion

        #region GetExpectedDamageOutOfBattle Tests

        [TestMethod]
        public void GetExpectedDamageOutOfBattle_ReturnsMaxDamageTaken()
        {
            var tracker = new DamageTracker(5);
            tracker.RecordDamage(75);
            tracker.OnBattleEnd();

            Assert.AreEqual(75, tracker.GetExpectedDamageOutOfBattle());
        }

        [TestMethod]
        public void GetExpectedDamageOutOfBattle_NoHistory_ReturnsZero()
        {
            var tracker = new DamageTracker(5);
            Assert.AreEqual(0, tracker.GetExpectedDamageOutOfBattle());
        }

        #endregion

        #region Reset Tests

        [TestMethod]
        public void Reset_ClearsAllState()
        {
            var tracker = new DamageTracker(5);

            // Build up state
            tracker.RecordDamage(50);
            tracker.RecordDamage(80);
            tracker.OnBattleEnd();

            Assert.AreEqual(80, tracker.MaxDamageTaken);
            Assert.AreEqual(80, tracker.BattleMaxDamageTaken);

            // Reset
            tracker.Reset();

            Assert.AreEqual(0, tracker.MaxDamageTaken);
            Assert.AreEqual(0, tracker.BattleMaxDamageTaken);
            Assert.AreEqual(0, tracker.GetExpectedDamageInBattle());
            Assert.AreEqual(0, tracker.GetExpectedDamageOutOfBattle());
        }

        [TestMethod]
        public void Reset_AllowsFreshRecording()
        {
            var tracker = new DamageTracker(5);

            // First round
            tracker.RecordDamage(100);
            tracker.OnBattleEnd();

            // Reset
            tracker.Reset();

            // New first battle should behave as if no history
            tracker.RecordDamage(25);
            tracker.OnBattleEnd();

            Assert.AreEqual(25, tracker.MaxDamageTaken);
        }

        #endregion

        #region Circular Buffer Tests

        [TestMethod]
        public void CircularBuffer_OverwritesOldSamples()
        {
            var tracker = new DamageTracker(3);

            // Fill buffer with high values
            tracker.RecordDamage(100);
            tracker.RecordDamage(100);
            tracker.RecordDamage(100);

            // Overwrite with low values
            tracker.RecordDamage(10);
            tracker.RecordDamage(10);
            tracker.RecordDamage(10);

            // Recent average should now be 10 (all old samples overwritten)
            // Scaled max = 0 * 7/10 = 0 (no OnBattleEnd called)
            int expected = tracker.GetExpectedDamageInBattle();
            Assert.AreEqual(10, expected);
        }

        [TestMethod]
        public void CircularBuffer_PartialFill_AveragesCorrectly()
        {
            var tracker = new DamageTracker(5);

            tracker.RecordDamage(20);
            tracker.RecordDamage(30);

            // Average of 2 samples: (20 + 30) / 2 = 25
            // Scaled max = 0 * 7/10 = 0
            int expected = tracker.GetExpectedDamageInBattle();
            Assert.AreEqual(25, expected);
        }

        #endregion

        #region Multi-Battle Decay Tests

        [TestMethod]
        public void MultipleBattles_DecayConvergesToRecentDamage()
        {
            var tracker = new DamageTracker(5);

            // First battle: high damage spike
            tracker.RecordDamage(200);
            tracker.OnBattleEnd();
            Assert.AreEqual(200, tracker.MaxDamageTaken);

            // Subsequent battles: consistently low damage
            for (int i = 0; i < 10; i++)
            {
                tracker.OnBattleStart();
                tracker.RecordDamage(20);
                tracker.OnBattleEnd();
            }

            // After many low-damage battles, max should have decayed significantly
            Assert.IsTrue(tracker.MaxDamageTaken < 100,
                $"MaxDamageTaken should have decayed below 100, but is {tracker.MaxDamageTaken}");
        }

        #endregion
    }
}
