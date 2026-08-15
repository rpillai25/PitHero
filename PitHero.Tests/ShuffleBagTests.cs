using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.Utils;

namespace PitHero.Tests
{
    /// <summary>
    /// ShuffleBag invariants: every cycle draws the exact added composition (no repeats
    /// beyond the marble counts), the bag refills after exhaustion, and NextFromRoll is
    /// fully deterministic for a given roll sequence (it consumes no RNG itself).
    /// </summary>
    [TestClass]
    public class ShuffleBagTests
    {
        private static Dictionary<string, int> DrawFullCycle(ShuffleBag<string> bag, System.Random rng)
        {
            var counts = new Dictionary<string, int>();
            int cycle = bag.Count;
            for (int i = 0; i < cycle; i++)
            {
                var item = bag.Next(rng);
                counts.TryGetValue(item, out int c);
                counts[item] = c + 1;
            }
            return counts;
        }

        [TestMethod]
        public void FullCycle_DrawsExactComposition()
        {
            var bag = new ShuffleBag<string>(20);
            bag.Add("crit", 1);
            bag.Add("miss", 19);

            var rng = new System.Random(1234);
            for (int cycle = 0; cycle < 5; cycle++)
            {
                var counts = DrawFullCycle(bag, rng);
                Assert.AreEqual(1, counts["crit"], $"cycle {cycle}: expected exactly 1 crit per 20 draws");
                Assert.AreEqual(19, counts["miss"], $"cycle {cycle}: expected exactly 19 misses per 20 draws");
            }
        }

        [TestMethod]
        public void Refill_HappensAutomaticallyAfterExhaustion()
        {
            var bag = new ShuffleBag<int>(3);
            bag.Add(7, 3);
            Assert.AreEqual(3, bag.Remaining);

            var rng = new System.Random(5);
            for (int i = 0; i < 3; i++) bag.Next(rng);
            Assert.AreEqual(0, bag.Remaining);

            // Next draw refills transparently
            Assert.AreEqual(7, bag.Next(rng));
            Assert.AreEqual(2, bag.Remaining);
        }

        [TestMethod]
        public void NextFromRoll_IsDeterministicForFixedRolls()
        {
            var bagA = new ShuffleBag<int>(4);
            var bagB = new ShuffleBag<int>(4);
            for (int i = 0; i < 4; i++) { bagA.Add(i); bagB.Add(i); }

            float[] rolls = { 0.1f, 0.9f, 0.5f, 0.0f, 0.7f, 0.3f, 0.99f, 0.2f };
            for (int i = 0; i < rolls.Length; i++)
                Assert.AreEqual(bagA.NextFromRoll(rolls[i]), bagB.NextFromRoll(rolls[i]), $"draw {i} diverged");
        }

        [TestMethod]
        public void NextFromRoll_BoundaryRolls_StayInRange()
        {
            var bag = new ShuffleBag<int>(2);
            bag.Add(1, 1);
            bag.Add(2, 1);

            // 0f always picks the first undrawn slot; ~1f clamps to the cursor
            var lo = bag.NextFromRoll(0f);
            var hi = bag.NextFromRoll(0.9999f);
            Assert.AreNotEqual(lo, hi, "two-marble bag must yield both marbles in one cycle");
        }

        [TestMethod]
        public void SingleItemBag_AlwaysReturnsThatItem()
        {
            var bag = new ShuffleBag<string>(1);
            bag.Add("only");
            for (int i = 0; i < 5; i++)
                Assert.AreEqual("only", bag.NextFromRoll(0.42f));
        }

        [TestMethod]
        public void AddMidCycle_ResetsCycle()
        {
            var bag = new ShuffleBag<int>(4);
            bag.Add(1, 2);
            bag.NextFromRoll(0.5f);
            Assert.AreEqual(1, bag.Remaining);

            bag.Add(2, 2);
            Assert.AreEqual(4, bag.Count);
            Assert.AreEqual(4, bag.Remaining, "Add must restart the cycle with a full bag");
        }

        [TestMethod]
        public void Clear_EmptiesBag()
        {
            var bag = new ShuffleBag<int>(2);
            bag.Add(1, 2);
            bag.Clear();
            Assert.AreEqual(0, bag.Count);
            Assert.AreEqual(0, bag.Remaining);
        }
    }
}
