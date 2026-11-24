using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.Balance;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Jobs;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Mercenaries;
using RolePlayingFramework.Stats;
using System;

namespace PitHero.Tests
{
    /// <summary>Issue #317: the Monk knuckle and Archer bow lines.</summary>
    [TestClass]
    public class KnuckleAndBowGearTests
    {
        private static readonly (Func<Gear> create, string name, ItemKind kind, int pitLevel, ItemRarity rarity)[] Items =
        {
            (GearItems.LeatherWraps,   "LeatherWraps",   ItemKind.WeaponKnuckle, 1,  ItemRarity.Normal),
            (GearItems.StoneKnuckles,  "StoneKnuckles",  ItemKind.WeaponKnuckle, 5,  ItemRarity.Normal),
            (GearItems.EmberFists,     "EmberFists",     ItemKind.WeaponKnuckle, 9,  ItemRarity.Normal),
            (GearItems.QuartzKnuckles, "QuartzKnuckles", ItemKind.WeaponKnuckle, 13, ItemRarity.Uncommon),
            (GearItems.ShadowClaws,    "ShadowClaws",    ItemKind.WeaponKnuckle, 18, ItemRarity.Uncommon),
            (GearItems.MagmaKnuckles,  "MagmaKnuckles",  ItemKind.WeaponKnuckle, 23, ItemRarity.Uncommon),
            (GearItems.TwigBow,        "TwigBow",        ItemKind.WeaponBow,     1,  ItemRarity.Normal),
            (GearItems.BoneBow,        "BoneBow",        ItemKind.WeaponBow,     5,  ItemRarity.Normal),
            (GearItems.FlintBow,       "FlintBow",       ItemKind.WeaponBow,     9,  ItemRarity.Normal),
            (GearItems.CrystalBow,     "CrystalBow",     ItemKind.WeaponBow,     13, ItemRarity.Uncommon),
            (GearItems.ShadowLongbow,  "ShadowLongbow",  ItemKind.WeaponBow,     18, ItemRarity.Uncommon),
            (GearItems.InfernoBow,     "InfernoBow",     ItemKind.WeaponBow,     23, ItemRarity.Uncommon),
        };

        [TestMethod]
        public void AllItems_HaveExpectedNameKindRarityAndAttack()
        {
            for (int i = 0; i < Items.Length; i++)
            {
                var it = Items[i];
                var gear = it.create();
                Assert.AreEqual(it.name, gear.Name);
                Assert.AreEqual(it.name, gear.SpriteName, "icon sprite name must match the atlas entry");
                Assert.AreEqual(it.kind, gear.Kind, it.name);
                Assert.AreEqual(it.rarity, gear.Rarity, it.name);
                Assert.AreEqual(BalanceConfig.CalculateEquipmentAttackBonus(it.pitLevel, it.rarity), gear.AttackBonus, it.name);
                Assert.AreEqual(0, gear.DefenseBonus, it.name);
                Assert.IsTrue(gear.Price > 0, it.name);
            }
        }

        [TestMethod]
        public void AllItems_HaveAnIconInTheItemsAtlas()
        {
            var path = System.IO.Path.Combine(AppContext.BaseDirectory, "Content", "Atlases", "Items.atlas");
            Assert.IsTrue(System.IO.File.Exists(path), $"Items.atlas not deployed at {path}");
            var names = new System.Collections.Generic.HashSet<string>();
            foreach (var line in System.IO.File.ReadAllLines(path))
            {
                if (line.Length > 0 && !char.IsWhiteSpace(line[0]))
                    names.Add(line.Trim());
            }
            for (int i = 0; i < Items.Length; i++)
                Assert.IsTrue(names.Contains(Items[i].name), $"{Items[i].name} has no sprite in Items.atlas");
        }

        [TestMethod]
        public void AllItems_RoundTripThroughItemRegistry()
        {
            for (int i = 0; i < Items.Length; i++)
            {
                Assert.IsTrue(ItemRegistry.TryCreateItem(Items[i].name, out var item), $"{Items[i].name} is not registered for save/load");
                Assert.AreEqual(Items[i].kind, item.Kind);
            }
        }

        [TestMethod]
        public void Knuckles_AreMonkOnly_BowsAreArcherOnly()
        {
            Assert.AreEqual(JobType.Monk, GearItems.ShadowClaws().AllowedJobs);
            Assert.AreEqual(JobType.Archer, GearItems.ShadowLongbow().AllowedJobs);
        }

        [TestMethod]
        public void Hero_SetEquipmentSlot_AcceptsBowInWeaponSlotOnly()
        {
            var hero = new RolePlayingFramework.Heroes.Hero("Test", JobFactory.CreateJob("Archer"), 1, new StatBlock(4, 3, 5, 1));
            var bow = GearItems.CrystalBow();

            Assert.IsTrue(hero.SetEquipmentSlot(EquipmentSlot.WeaponShield1, bow));
            Assert.AreSame(bow, hero.WeaponShield1);
            Assert.IsFalse(hero.SetEquipmentSlot(EquipmentSlot.WeaponShield2, GearItems.FlintBow()), "bows never go in the shield slot");
        }

        [TestMethod]
        public void Mercenary_EquipsBowAndKnuckle()
        {
            var archer = new Mercenary("A", new Archer(), 1, new StatBlock(4, 3, 5, 1));
            var bow = GearItems.BoneBow();
            Assert.IsTrue(archer.Equip(bow));
            Assert.AreSame(bow, archer.WeaponShield1);

            var monk = new Mercenary("M", new Monk(), 1, new StatBlock(4, 3, 5, 1));
            Assert.IsTrue(monk.SetEquipmentSlot(EquipmentSlot.WeaponShield1, GearItems.StoneKnuckles()));
        }
    }
}
