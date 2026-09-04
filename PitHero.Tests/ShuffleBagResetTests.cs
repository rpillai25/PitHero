using RolePlayingFramework.Utils;

namespace PitHero.Tests
{
    /// <summary>ShuffleBag.Reset must restore the exact starting arrangement so a seeded consumer replays the same draws.</summary>
    [TestClass]
    public class ShuffleBagResetTests
    {
        [TestMethod]
        public void Reset_RestoresOriginalOrderAndFullCursor()
        {
            var bag = new ShuffleBag<int>(4);
            for (int i = 0; i < 4; i++)
                bag.Add(i);

            var first = new int[6];
            var rng1 = new System.Random(123);
            for (int i = 0; i < first.Length; i++)
                first[i] = bag.Next(rng1);

            bag.Reset();
            Assert.AreEqual(4, bag.Remaining);

            var second = new int[6];
            var rng2 = new System.Random(123);
            for (int i = 0; i < second.Length; i++)
                second[i] = bag.Next(rng2);

            CollectionAssert.AreEqual(first, second, "same rolls after Reset must reproduce the same draw sequence");
        }

        [TestMethod]
        public void Clear_ForgetsTheSnapshot()
        {
            var bag = new ShuffleBag<int>(2);
            bag.Add(7);
            bag.Clear();
            bag.Reset();
            Assert.AreEqual(0, bag.Count);
            Assert.AreEqual(0, bag.Remaining);
        }
    }
}
