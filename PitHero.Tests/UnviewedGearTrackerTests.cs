using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Services;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Stats;

namespace PitHero.Tests
{
    [TestClass]
    public class UnviewedGearTrackerTests
    {
        [TestInitialize]
        public void Setup()
        {
            // Tracker is static session state; reset between tests
            UnviewedGearTracker.ClearAll();
        }

        private static Gear MakeGear(string name = "Sword")
            => new Gear(name, ItemKind.WeaponSword, ItemRarity.Normal, "desc", 10, new StatBlock(1, 0, 0, 0));

        [TestMethod]
        public void MarkNew_Gear_IsUnviewed()
        {
            var gear = MakeGear();
            UnviewedGearTracker.MarkNew(gear);

            Assert.IsTrue(UnviewedGearTracker.IsUnviewed(gear));
            Assert.AreEqual(1, UnviewedGearTracker.Count);
        }

        [TestMethod]
        public void MarkNew_Consumable_IsIgnored()
        {
            var potion = PotionItems.HPPotion();
            UnviewedGearTracker.MarkNew(potion);

            Assert.IsFalse(UnviewedGearTracker.IsUnviewed(potion));
            Assert.AreEqual(0, UnviewedGearTracker.Count);
        }

        [TestMethod]
        public void MarkNew_SameInstanceTwice_IsIdempotent()
        {
            var gear = MakeGear();
            UnviewedGearTracker.MarkNew(gear);
            UnviewedGearTracker.MarkNew(gear);

            Assert.AreEqual(1, UnviewedGearTracker.Count);
        }

        [TestMethod]
        public void MarkNew_SameNamedInstances_TrackedIndependently()
        {
            // Reference semantics: two identical-looking gear instances are distinct
            var gear1 = MakeGear();
            var gear2 = MakeGear();
            UnviewedGearTracker.MarkNew(gear1);

            Assert.IsTrue(UnviewedGearTracker.IsUnviewed(gear1));
            Assert.IsFalse(UnviewedGearTracker.IsUnviewed(gear2));
        }

        [TestMethod]
        public void ClearAll_EmptiesTracker()
        {
            UnviewedGearTracker.MarkNew(MakeGear("A"));
            UnviewedGearTracker.MarkNew(MakeGear("B"));
            Assert.AreEqual(2, UnviewedGearTracker.Count);

            UnviewedGearTracker.ClearAll();

            Assert.AreEqual(0, UnviewedGearTracker.Count);
        }

        [TestMethod]
        public void IsUnviewed_Null_ReturnsFalse()
        {
            Assert.IsFalse(UnviewedGearTracker.IsUnviewed(null));
        }
    }
}
