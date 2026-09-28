using PitHero.Services.Replay.Frames;

namespace PitHero.Tests
{
    /// <summary>Recorder entity ids: stable while seen, released when unseen, reused from a free list, never 0.</summary>
    [TestClass]
    public class FrameEntityIdPoolTests
    {
        [TestMethod]
        public void Ids_AreStableAcrossTicks_AndReleasedWhenUnseen()
        {
            var pool = new FrameEntityIdPool();
            var a = new object();
            var b = new object();
            var c = new object();

            pool.BeginTick();
            ushort ia = pool.Acquire(a);
            ushort ib = pool.Acquire(b);
            Assert.AreEqual((ushort)1, ia);
            Assert.AreEqual((ushort)2, ib);
            Assert.AreEqual(0, pool.ReleaseUnseen());
            Assert.AreEqual(2, pool.LiveCount);

            pool.BeginTick();
            Assert.IsTrue(pool.Touch(ia, a), "cached id marks the owner seen without a lookup");
            Assert.IsFalse(pool.Touch(ia, c), "cached id refused for another owner");
            Assert.IsFalse(pool.Touch(0, a));
            Assert.AreEqual(ia, pool.Acquire(a), "same object, same id");
            Assert.AreEqual((ushort)3, pool.Acquire(c));
            Assert.IsFalse(pool.WasSeenThisTick(b));
            Assert.AreEqual(1, pool.ReleaseUnseen(), "b vanished");
            Assert.AreEqual(2, pool.LiveCount);
            Assert.AreEqual(1, pool.FreeCount);

            pool.BeginTick();
            Assert.AreEqual(ib, pool.Acquire(new object()), "the freed id is reused for a newcomer");
            Assert.AreEqual(ia, pool.Acquire(a));
            Assert.AreEqual((ushort)3, pool.Acquire(c));
            pool.ReleaseUnseen();
            Assert.AreEqual(3, pool.LiveCount);
            Assert.AreEqual(0, pool.FreeCount);

            pool.Clear();
            Assert.AreEqual(0, pool.LiveCount);
            pool.BeginTick();
            Assert.AreEqual((ushort)1, pool.Acquire(c), "numbering restarts after Clear");
        }

        [TestMethod]
        public void Acquire_ReturnsZeroOnlyWhenAll65535IdsAreLive()
        {
            var pool = new FrameEntityIdPool();
            var owners = new object[ushort.MaxValue];
            pool.BeginTick();
            for (int i = 0; i < owners.Length; i++)
            {
                owners[i] = new object();
                Assert.AreNotEqual((ushort)0, pool.Acquire(owners[i]));
            }
            Assert.AreEqual((ushort)0, pool.Acquire(new object()));
            Assert.IsTrue(pool.Exhausted);
            pool.ReleaseUnseen();
            Assert.AreEqual(ushort.MaxValue, pool.LiveCount);

            // Free one and the pool serves again
            pool.BeginTick();
            for (int i = 1; i < owners.Length; i++)
                pool.Acquire(owners[i]);
            Assert.AreEqual(1, pool.ReleaseUnseen());
            pool.BeginTick();
            Assert.AreEqual((ushort)1, pool.Acquire(new object()));
        }
    }
}
