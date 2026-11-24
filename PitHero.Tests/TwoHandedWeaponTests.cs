using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Inventory;
using RolePlayingFramework.Jobs;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Mercenaries;
using RolePlayingFramework.Stats;

namespace PitHero.Tests
{
    /// <summary>
    /// Issue #317: Archers never carry shields, and bows are two-handed — never worn together with a
    /// shield. Real shields exclude the Archer, so the two-handed tests use a test shield any job can
    /// carry: the two-handed rule stays a safety net for any future job that has both.
    /// </summary>
    [TestClass]
    public class TwoHandedWeaponTests
    {
        private static Hero MakeArcher() => new Hero("Archer", new Archer(), 10, new StatBlock(8, 12, 8, 5));
        private static Mercenary MakeArcherMerc() => new Mercenary("Scout", new Archer(), 10, new StatBlock(8, 12, 8, 5));

        /// <summary>A shield every job may carry (real shields exclude the Archer).</summary>
        private static Gear AnyJobShield()
            => new Gear("TestBuckler", ItemKind.Shield, ItemRarity.Normal, "Test", 50, new StatBlock(0, 0, 0, 0), def: 2, allowedJobs: JobType.All);

        [TestMethod]
        public void Archers_CannotCarryRealShields()
        {
            Assert.AreEqual(JobType.All & ~JobType.Archer, GearItems.WoodenShield().AllowedJobs);
            Assert.AreEqual(Gear.ShieldJobs, Gear.GetDefaultAllowedJobs(ItemKind.Shield));

            var hero = MakeArcher();
            Assert.IsFalse(hero.CanEquipItem(GearItems.WoodenShield()));
            Assert.IsFalse(hero.SetEquipmentSlot(EquipmentSlot.WeaponShield2, GearItems.IronShield()));
            Assert.IsFalse(MakeArcherMerc().CanEquipItem(GearItems.TowerShield()));

            var knight = new Hero("Knight", new Knight(), 10, new StatBlock(12, 10, 12, 5));
            Assert.IsTrue(knight.SetEquipmentSlot(EquipmentSlot.WeaponShield2, GearItems.WoodenShield()));
        }

        [TestMethod]
        public void ShieldCard_ReadsAllButArcher()
        {
            Assert.AreEqual("All but Archer", ItemDisplayHelper.FormatAllowedJobs(Gear.ShieldJobs));
            Assert.AreEqual("Knight, Priest", ItemDisplayHelper.FormatAllowedJobs(JobType.Knight | JobType.Priest));
        }

        [TestMethod]
        public void AutoEquip_RealShield_NeverOnArcher()
        {
            var hero = MakeArcher();
            var bag = new ItemBag("Test", 10);
            var shield = GearItems.WoodenShield();
            bag.TryAdd(shield);

            Assert.IsFalse(GearAutoEquipService.IsUpgradeFor(hero, shield));
            Assert.IsFalse(GearAutoEquipService.TryAutoEquipOnHero(hero, bag, shield, out _));
            Assert.IsNull(hero.WeaponShield2);
        }

        [TestMethod]
        public void OnlyBowsAreTwoHanded()
        {
            Assert.IsTrue(GearCategoryUtils.IsTwoHanded(ItemKind.WeaponBow));
            Assert.IsFalse(GearCategoryUtils.IsTwoHanded(ItemKind.WeaponSword));
            Assert.IsFalse(GearCategoryUtils.IsTwoHanded(ItemKind.WeaponKnuckle));
            Assert.IsFalse(GearCategoryUtils.IsTwoHanded(ItemKind.Shield));
            Assert.IsFalse(GearCategoryUtils.IsTwoHanded((IItem)null));
        }

        [TestMethod]
        public void Hero_CannotEquipShieldBesideBow()
        {
            var hero = MakeArcher();
            Assert.IsTrue(hero.SetEquipmentSlot(EquipmentSlot.WeaponShield1, GearItems.TwigBow()));

            Assert.IsFalse(hero.SetEquipmentSlot(EquipmentSlot.WeaponShield2, AnyJobShield()));
            Assert.IsFalse(hero.TryEquip(AnyJobShield()));
            Assert.IsNull(hero.WeaponShield2);
        }

        [TestMethod]
        public void Hero_CannotEquipBowWhileHoldingShield()
        {
            var hero = MakeArcher();
            var shield = AnyJobShield();
            Assert.IsTrue(hero.SetEquipmentSlot(EquipmentSlot.WeaponShield2, shield));

            Assert.IsFalse(hero.SetEquipmentSlot(EquipmentSlot.WeaponShield1, GearItems.TwigBow()));
            Assert.IsFalse(hero.TryEquip(GearItems.TwigBow()));
            Assert.IsNull(hero.WeaponShield1);
            Assert.AreSame(shield, hero.WeaponShield2);
        }

        [TestMethod]
        public void Hero_OneHandedWeaponStillPairsWithShield()
        {
            var hero = new Hero("Knight", new Knight(), 10, new StatBlock(12, 10, 12, 5));
            Assert.IsTrue(hero.SetEquipmentSlot(EquipmentSlot.WeaponShield2, AnyJobShield()));
            Assert.IsTrue(hero.SetEquipmentSlot(EquipmentSlot.WeaponShield1, GearItems.ShortSword()));
        }

        [TestMethod]
        public void Mercenary_EnforcesTheSameRule()
        {
            var merc = MakeArcherMerc();
            Assert.IsTrue(merc.Equip(GearItems.BoneBow()));
            Assert.IsFalse(merc.Equip(AnyJobShield()));
            Assert.IsFalse(merc.SetEquipmentSlot(EquipmentSlot.WeaponShield2, AnyJobShield()));

            var other = MakeArcherMerc();
            Assert.IsTrue(other.SetEquipmentSlot(EquipmentSlot.WeaponShield2, AnyJobShield()));
            Assert.IsFalse(other.SetEquipmentSlot(EquipmentSlot.WeaponShield1, GearItems.BoneBow()));
        }

        [TestMethod]
        public void AutoEquip_Bow_MovesHeroShieldToBag()
        {
            var hero = MakeArcher();
            var shield = AnyJobShield();
            hero.SetEquipmentSlot(EquipmentSlot.WeaponShield2, shield);
            var bag = new ItemBag("Test", 10);
            var bow = GearItems.FlintBow();
            bag.TryAdd(bow);

            Assert.IsTrue(GearAutoEquipService.TryAutoEquipOnHero(hero, bag, bow, out var displaced));

            Assert.AreSame(bow, hero.WeaponShield1);
            Assert.IsNull(hero.WeaponShield2);
            Assert.AreSame(shield, displaced, "the shield is offered as a hand-me-down");
            Assert.AreEqual(1, bag.Count);
            Assert.AreSame(shield, bag.GetSlotItem(0));
        }

        [TestMethod]
        public void AutoEquip_Bow_FullBag_ShieldTakesTheBowsFreedCell()
        {
            // An archer from an older save: shield equipped, no weapon. The bag is full, but the bow
            // leaving it frees exactly the cell the shield needs.
            var hero = MakeArcher();
            var shield = AnyJobShield();
            hero.SetEquipmentSlot(EquipmentSlot.WeaponShield2, shield);
            var bag = new ItemBag("Test", 1);
            var bow = GearItems.FlintBow();
            bag.TryAdd(bow);

            Assert.IsTrue(GearAutoEquipService.TryAutoEquipOnHero(hero, bag, bow, out _));
            Assert.AreSame(bow, hero.WeaponShield1);
            Assert.AreSame(shield, bag.GetSlotItem(0));
        }

        [TestMethod]
        public void AutoEquip_BetterBow_ReplacesBow_NoShieldInvolved()
        {
            var hero = MakeArcher();
            var oldBow = GearItems.FlintBow();
            hero.SetEquipmentSlot(EquipmentSlot.WeaponShield1, oldBow);
            var bag = new ItemBag("Test", 1);
            var best = GearItems.CrystalBow();
            bag.TryAdd(best);

            Assert.IsTrue(GearAutoEquipService.TryAutoEquipOnHero(hero, bag, best, out var displaced));
            Assert.AreSame(best, hero.WeaponShield1);
            Assert.AreSame(oldBow, displaced);
            Assert.AreSame(oldBow, bag.GetSlotItem(0));
        }

        [TestMethod]
        public void AutoEquip_Bow_RefusedWhenBagCannotTakeShield()
        {
            var hero = MakeArcher();
            var shield = AnyJobShield();
            hero.SetEquipmentSlot(EquipmentSlot.WeaponShield2, shield);
            var bag = new ItemBag("Test", 1);
            bag.TryAdd(GearItems.ShortSword()); // the bow is not in this bag, so nothing frees up
            var bow = GearItems.FlintBow();

            Assert.IsFalse(GearAutoEquipService.TryAutoEquipOnHero(hero, bag, bow, out _));
            Assert.AreSame(shield, hero.WeaponShield2);
            Assert.IsNull(hero.WeaponShield1);
        }

        [TestMethod]
        public void AutoEquip_Shield_SkippedBesideBow()
        {
            var hero = MakeArcher();
            hero.SetEquipmentSlot(EquipmentSlot.WeaponShield1, GearItems.TwigBow());
            var bag = new ItemBag("Test", 10);
            var shield = AnyJobShield();
            bag.TryAdd(shield);

            Assert.IsFalse(GearAutoEquipService.IsUpgradeFor(hero, shield));
            Assert.IsFalse(GearAutoEquipService.TryAutoEquipOnHero(hero, bag, shield, out _));
            Assert.IsNull(hero.WeaponShield2);
            Assert.AreSame(shield, bag.GetSlotItem(0));
        }

        [TestMethod]
        public void AutoEquip_Bow_MovesMercShieldToHeroBag()
        {
            var merc = MakeArcherMerc();
            var shield = AnyJobShield();
            merc.SetEquipmentSlot(EquipmentSlot.WeaponShield2, shield);
            var bag = new ItemBag("Test", 10);
            var bow = GearItems.BoneBow();
            bag.TryAdd(bow);

            Assert.IsTrue(GearAutoEquipService.TryAutoEquipOnMercenary(merc, bag, bow, out _));
            Assert.AreSame(bow, merc.WeaponShield1);
            Assert.IsNull(merc.WeaponShield2);
            Assert.AreSame(shield, bag.GetSlotItem(0));
        }
    }
}
