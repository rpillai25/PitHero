using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Services;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Inventory;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Stats;

namespace PitHero.Tests
{
    /// <summary>
    /// Synergies are simulation state: the resolver must find them from the bag alone, recompute only
    /// when a bag slot's item changed, and ignore equipment cells — with no Party window involved (the
    /// replay divergence at tick 248700 came from UI-driven detection that also matched gear cells).
    /// </summary>
    [TestClass]
    public class HeroSynergyResolverTests
    {
        private static Gear MakeGear(string name, ItemKind kind)
            => new Gear(name, kind, ItemRarity.Normal, "desc", 10, new StatBlock(0, 0, 0, 0), atk: 1, def: 1);

        private static Hero MakeKnight() => new Hero("Test Knight", new Knight(), 1, new StatBlock(10, 10, 10, 10));

        [TestMethod]
        public void Sync_DetectsBagPatternWithoutAnyUi_AndAppliesPassives()
        {
            var hero = MakeKnight();
            var bag = new ItemBag("Inventory", PartyGridLayout.BagCapacity);
            // Knight "Shield Mastery" is [Sword][Shield] side by side: bag slots 0 and 1 share row 3
            bag.SetSlotItem(0, MakeGear("Sword", ItemKind.WeaponSword));
            bag.SetSlotItem(1, MakeGear("Shield", ItemKind.Shield));

            var resolver = new HeroSynergyResolver();
            float deflectBefore = hero.DeflectChance;
            resolver.Sync(hero, bag, null);

            Assert.AreEqual(1, resolver.RecomputeCount);
            bool found = false;
            for (int i = 0; i < hero.ActiveSynergyGroups.Count; i++)
                if (hero.ActiveSynergyGroups[i].Pattern.Id == "knight.shield_mastery") found = true;
            Assert.IsTrue(found, "shield mastery should be active from the bag alone");
            Assert.IsTrue(hero.DeflectChance > deflectBefore, "synergy passive must be applied to the hero");
            Assert.IsTrue(HeroSynergyResolver.IsBagIndexInSynergy(hero, 0));
            Assert.IsTrue(HeroSynergyResolver.IsBagIndexInSynergy(hero, 1));
            Assert.IsFalse(HeroSynergyResolver.IsBagIndexInSynergy(hero, 2));
        }

        [TestMethod]
        public void Sync_RecomputesOnlyWhenABagSlotChanged()
        {
            var hero = MakeKnight();
            var bag = new ItemBag("Inventory", PartyGridLayout.BagCapacity);
            var sword = MakeGear("Sword", ItemKind.WeaponSword);
            bag.SetSlotItem(0, sword);

            var resolver = new HeroSynergyResolver();
            resolver.Sync(hero, bag, null);
            resolver.Sync(hero, bag, null);
            resolver.Sync(hero, bag, null);
            Assert.AreEqual(1, resolver.RecomputeCount, "unchanged bag must not recompute");

            // Moving the same item to another slot is a change
            bag.SetSlotItem(0, null);
            bag.SetSlotItem(5, sword);
            resolver.Sync(hero, bag, null);
            Assert.AreEqual(2, resolver.RecomputeCount);

            // Equipment is not part of the synergy grid: equipping must not trigger a recompute
            hero.SetEquipmentSlot(EquipmentSlot.Hat, MakeGear("Helm", ItemKind.HatHelm));
            resolver.Sync(hero, bag, null);
            Assert.AreEqual(2, resolver.RecomputeCount);
        }

        [TestMethod]
        public void EquipmentCellsNeverEnterTheSynergyGrid()
        {
            var hero = MakeKnight();
            var bag = new ItemBag("Inventory", PartyGridLayout.BagCapacity);
            Assert.IsTrue(hero.SetEquipmentSlot(EquipmentSlot.WeaponShield1, MakeGear("Sword", ItemKind.WeaponSword)));
            bag.SetSlotItem(3, MakeGear("BagShield", ItemKind.Shield));

            var grid = new IItem[PartyGridLayout.Width, PartyGridLayout.Height];
            PartyGridLayout.FillSynergyGrid(grid, bag);
            for (int x = 0; x < PartyGridLayout.Width; x++)
                for (int y = 0; y < PartyGridLayout.BagRowStart; y++)
                    Assert.IsNull(grid[x, y], $"row {y} is an equipment/name row and must stay empty");
            Assert.IsNotNull(grid[3, PartyGridLayout.BagRowStart]);

            var resolver = new HeroSynergyResolver();
            resolver.Sync(hero, bag, null);
            Assert.AreEqual(0, hero.ActiveSynergyGroups.Count, "a sword worn by the hero must not complete a pattern");
        }

        [TestMethod]
        public void Layout_BagIndexRoundTripsThroughCells()
        {
            for (int i = 0; i < PartyGridLayout.BagCapacity; i++)
            {
                var cell = PartyGridLayout.BagIndexToCell(i);
                Assert.AreEqual(i, PartyGridLayout.CellToBagIndex(cell.X, cell.Y));
            }
            Assert.AreEqual(-1, PartyGridLayout.CellToBagIndex(PartyGridLayout.HeroCol, 1));
        }
    }
}
