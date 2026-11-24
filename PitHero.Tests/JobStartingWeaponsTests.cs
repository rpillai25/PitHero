using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Jobs;
using RolePlayingFramework.Stats;
using PitHero;

namespace PitHero.Tests
{
    [TestClass]
    public class JobStartingWeaponsTests
    {
        [TestMethod]
        public void Knight_StartsWithRustyBlade()
        {
            var weapon = JobStartingWeapons.CreateStartingWeapon(JobType.Knight);

            Assert.IsNotNull(weapon);
            Assert.AreEqual("RustyBlade", weapon.Name);
            Assert.AreEqual(ItemKind.WeaponSword, weapon.Kind);
        }

        [TestMethod]
        public void Thief_StartsWithRustyDagger()
        {
            var weapon = JobStartingWeapons.CreateStartingWeapon(JobType.Thief);

            Assert.IsNotNull(weapon);
            Assert.AreEqual("RustyDagger", weapon.Name);
            Assert.AreEqual(ItemKind.WeaponKnife, weapon.Kind);
        }

        [TestMethod]
        public void Mage_StartsWithRustyDagger()
        {
            var weapon = JobStartingWeapons.CreateStartingWeapon(JobType.Mage);

            Assert.IsNotNull(weapon);
            Assert.AreEqual("RustyDagger", weapon.Name);
            Assert.AreEqual(ItemKind.WeaponKnife, weapon.Kind);
        }

        [TestMethod]
        public void Priest_StartsWithMallet()
        {
            var weapon = JobStartingWeapons.CreateStartingWeapon(JobType.Priest);

            Assert.IsNotNull(weapon);
            Assert.AreEqual("Mallet", weapon.Name);
            Assert.AreEqual(ItemKind.WeaponHammer, weapon.Kind);
        }

        [TestMethod]
        public void Monk_StartsWithLeatherWraps()
        {
            var weapon = JobStartingWeapons.CreateStartingWeapon(JobType.Monk);

            Assert.IsNotNull(weapon);
            Assert.AreEqual("LeatherWraps", weapon.Name);
            Assert.AreEqual(ItemKind.WeaponKnuckle, weapon.Kind);
        }

        [TestMethod]
        public void Archer_StartsWithTwigBow()
        {
            var weapon = JobStartingWeapons.CreateStartingWeapon(JobType.Archer);

            Assert.IsNotNull(weapon);
            Assert.AreEqual("TwigBow", weapon.Name);
            Assert.AreEqual(ItemKind.WeaponBow, weapon.Kind);
        }

        [TestMethod]
        public void None_ReturnsNull()
        {
            Assert.IsNull(JobStartingWeapons.CreateStartingWeapon(JobType.None));
        }

        [TestMethod]
        public void StartingWeapons_AreEquippableByTheirJob()
        {
            var jobFlags = new[] { JobType.Knight, JobType.Thief, JobType.Mage, JobType.Priest, JobType.Monk, JobType.Archer };
            for (int i = 0; i < jobFlags.Length; i++)
            {
                var weapon = JobStartingWeapons.CreateStartingWeapon(jobFlags[i]);
                Assert.IsNotNull(weapon);
                Assert.IsTrue((weapon.AllowedJobs & jobFlags[i]) != 0,
                    $"{jobFlags[i]} cannot equip its own starting weapon");
            }
        }

        [TestMethod]
        public void Hero_TryEquip_StartingWeapon_Succeeds()
        {
            var jobNames = new[] { "Knight", "Thief", "Mage", "Priest", "Monk", "Archer" };
            for (int i = 0; i < jobNames.Length; i++)
            {
                var job = JobFactory.CreateJob(jobNames[i]);
                var hero = new RolePlayingFramework.Heroes.Hero("Test", job, 1, new StatBlock(4, 3, 5, 1));
                var weapon = JobStartingWeapons.CreateStartingWeapon(job.JobFlag);

                Assert.IsNotNull(weapon);
                Assert.IsTrue(hero.TryEquip(weapon), $"{jobNames[i]} failed to equip starting weapon");
                Assert.AreSame(weapon, hero.WeaponShield1);
            }
        }
    }
}
