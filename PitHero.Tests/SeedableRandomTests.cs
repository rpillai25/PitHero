using PitHero.Services;
using PitHero.Services.Replay;

namespace PitHero.Tests
{
    /// <summary>
    /// Pins the seeded, state-restorable RNG that the replay system installs as Nez.Random.RNG.
    /// </summary>
    [TestClass]
    public class SeedableRandomTests
    {
        /// <summary>Two generators with the same seed produce identical sequences across every API.</summary>
        [TestMethod]
        public void SeedableRandom_SameSeed_ProducesIdenticalSequence()
        {
            var a = new SeedableRandom(12345);
            var b = new SeedableRandom(12345);
            for (int i = 0; i < 10000; i++)
            {
                Assert.AreEqual(a.Next(), b.Next());
                Assert.AreEqual(a.Next(100), b.Next(100));
                Assert.AreEqual(a.Next(-50, 50), b.Next(-50, 50));
                Assert.AreEqual(a.NextDouble(), b.NextDouble());
                Assert.AreEqual(a.NextSingle(), b.NextSingle());
            }
        }

        /// <summary>Different seeds diverge.</summary>
        [TestMethod]
        public void SeedableRandom_DifferentSeeds_Diverge()
        {
            var a = new SeedableRandom(1);
            var b = new SeedableRandom(2);
            bool anyDifferent = false;
            for (int i = 0; i < 16 && !anyDifferent; i++)
                anyDifferent = a.Next() != b.Next();
            Assert.IsTrue(anyDifferent);
        }

        /// <summary>Capturing the state mid-stream and restoring it into another instance resumes identically.</summary>
        [TestMethod]
        public void SeedableRandom_GetSetState_ResumesIdentically()
        {
            var a = new SeedableRandom(777);
            for (int i = 0; i < 123; i++)
                a.NextDouble();
            a.GetState(out uint s0, out uint s1, out uint s2, out uint s3);

            var b = new SeedableRandom(0);
            b.SetState(s0, s1, s2, s3);
            for (int i = 0; i < 1000; i++)
                Assert.AreEqual(a.Next(1000), b.Next(1000));
        }

        /// <summary>Reseed returns the generator to its canonical start.</summary>
        [TestMethod]
        public void SeedableRandom_Reseed_RestartsSequence()
        {
            var a = new SeedableRandom(99);
            int first = a.Next();
            for (int i = 0; i < 50; i++)
                a.Next();
            a.Reseed(99);
            Assert.AreEqual(first, a.Next());
        }

        /// <summary>Range helpers stay inside their documented bounds over many draws.</summary>
        [TestMethod]
        public void SeedableRandom_Bounds_AreRespected()
        {
            var r = new SeedableRandom(4242);
            for (int i = 0; i < 100000; i++)
            {
                int n = r.Next();
                Assert.IsTrue(n >= 0 && n < int.MaxValue);
                int m = r.Next(7);
                Assert.IsTrue(m >= 0 && m < 7);
                int k = r.Next(-3, 4);
                Assert.IsTrue(k >= -3 && k < 4);
                double d = r.NextDouble();
                Assert.IsTrue(d >= 0.0 && d < 1.0);
                float f = r.NextSingle();
                Assert.IsTrue(f >= 0f && f < 1f);
                long l = r.NextInt64(10, 20);
                Assert.IsTrue(l >= 10 && l < 20);
            }
            Assert.AreEqual(0, r.Next(0));
            Assert.AreEqual(5, r.Next(5, 5));
        }

        /// <summary>A range of 7 hits every value (no dead buckets in the multiply-shift reduction).</summary>
        [TestMethod]
        public void SeedableRandom_SmallRange_CoversAllValues()
        {
            var r = new SeedableRandom(31337);
            var seen = new bool[7];
            for (int i = 0; i < 5000; i++)
                seen[r.Next(7)] = true;
            for (int i = 0; i < 7; i++)
                Assert.IsTrue(seen[i], "value " + i + " never drawn");
        }

        /// <summary>NextBytes fills every byte and is reproducible.</summary>
        [TestMethod]
        public void SeedableRandom_NextBytes_FillsAndReproduces()
        {
            var a = new SeedableRandom(5);
            var b = new SeedableRandom(5);
            var ba = new byte[37];
            var bb = new byte[37];
            a.NextBytes(ba);
            b.NextBytes(bb);
            CollectionAssert.AreEqual(ba, bb);
        }

        /// <summary>InitializeSession installs the Sim stream as Nez.Random.RNG and derives an independent Loot stream.</summary>
        [TestMethod]
        public void GameRandom_InitializeSession_InstallsSimStreamAndDerivesLoot()
        {
            GameRandom.InitializeSession(2024);
            Assert.AreSame(GameRandom.Sim, Nez.Random.RNG);
            Assert.AreEqual(2024, GameRandom.MasterSeed);

            var simSeq = new float[8];
            for (int i = 0; i < simSeq.Length; i++)
                simSeq[i] = Nez.Random.NextFloat();

            GameRandom.InitializeSession(2024);
            for (int i = 0; i < simSeq.Length; i++)
                Assert.AreEqual(simSeq[i], Nez.Random.NextFloat(), "Sim stream must restart on re-initialize");

            GameRandom.InitializeSession(2024);
            var loot = new float[8];
            for (int i = 0; i < loot.Length; i++)
                loot[i] = (float)GameRandom.Loot.NextDouble();
            bool differs = false;
            for (int i = 0; i < loot.Length && !differs; i++)
                differs = loot[i] != simSeq[i];
            Assert.IsTrue(differs, "Loot stream must not mirror the Sim stream");

            Assert.IsNotNull(GameRandom.Audio);
            Assert.IsNotNull(GameRandom.Ui);
            Assert.AreNotSame(GameRandom.Sim, GameRandom.Audio);
            Assert.AreNotSame(GameRandom.Sim, GameRandom.Ui);

            // leave the global stream in a fresh state for other tests
            Nez.Random.SetSeed(System.Environment.TickCount);
        }
    }
}
