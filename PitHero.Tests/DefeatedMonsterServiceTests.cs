using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Services;
using RolePlayingFramework.Enemies;
using System.Collections.Generic;

namespace PitHero.Tests
{
    /// <summary>Tests for the defeated-monster tracking service (issue #283).</summary>
    [TestClass]
    public class DefeatedMonsterServiceTests
    {
        [TestMethod]
        [TestCategory("DefeatedMonsters")]
        public void New_HasNothingDefeated()
        {
            var service = new DefeatedMonsterService();

            Assert.IsFalse(service.IsDefeated(EnemyId.Slime));
            Assert.AreEqual(0, service.GetAll().Count);
        }

        [TestMethod]
        [TestCategory("DefeatedMonsters")]
        public void MarkDefeated_IsRecorded_AndIdempotent()
        {
            var service = new DefeatedMonsterService();

            service.MarkDefeated(EnemyId.Rat);
            service.MarkDefeated(EnemyId.Rat); // duplicate should not double-count

            Assert.IsTrue(service.IsDefeated(EnemyId.Rat));
            Assert.IsFalse(service.IsDefeated(EnemyId.Orc));
            Assert.AreEqual(1, service.GetAll().Count);
        }

        [TestMethod]
        [TestCategory("DefeatedMonsters")]
        public void ToNames_And_LoadFrom_RoundTrip()
        {
            var service = new DefeatedMonsterService();
            service.MarkDefeated(EnemyId.Slime);
            service.MarkDefeated(EnemyId.Orc);

            var names = service.ToNames();

            var restored = new DefeatedMonsterService();
            restored.LoadFrom(names);

            Assert.IsTrue(restored.IsDefeated(EnemyId.Slime));
            Assert.IsTrue(restored.IsDefeated(EnemyId.Orc));
            Assert.AreEqual(2, restored.GetAll().Count);
        }

        [TestMethod]
        [TestCategory("DefeatedMonsters")]
        public void LoadFrom_ReplacesExistingSet()
        {
            var service = new DefeatedMonsterService();
            service.MarkDefeated(EnemyId.Goblin);

            service.LoadFrom(new List<string> { EnemyId.Skeleton.ToString() });

            Assert.IsFalse(service.IsDefeated(EnemyId.Goblin), "LoadFrom should clear prior entries");
            Assert.IsTrue(service.IsDefeated(EnemyId.Skeleton));
        }

        [TestMethod]
        [TestCategory("DefeatedMonsters")]
        public void LoadFrom_IgnoresUnknownAndNullNames()
        {
            var service = new DefeatedMonsterService();

            service.LoadFrom(new List<string> { "NotARealMonster", null, EnemyId.CaveMushroom.ToString() });

            Assert.AreEqual(1, service.GetAll().Count);
            Assert.IsTrue(service.IsDefeated(EnemyId.CaveMushroom));
        }

        [TestMethod]
        [TestCategory("DefeatedMonsters")]
        public void LoadFrom_NullList_ClearsSet()
        {
            var service = new DefeatedMonsterService();
            service.MarkDefeated(EnemyId.Slime);

            service.LoadFrom(null);

            Assert.AreEqual(0, service.GetAll().Count);
        }
    }
}
