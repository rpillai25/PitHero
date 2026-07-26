using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Services;
using PitHero.VirtualGame;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Inventory;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Mercenaries;
using RolePlayingFramework.Stats;
using System.Collections.Generic;

namespace PitHero.Tests
{
    /// <summary>
    /// Tests for auto-selling excess items on full-bag chest pickup:
    /// ExcessItemSellSelector selection rules, the ItemBag full-bag stacking fix,
    /// and AutoSellExcessItemsService behavior.
    /// </summary>
    [TestClass]
    public class AutoSellExcessItemsTests
    {
        private sealed class TestPotion : Consumable
        {
            private readonly int _hp;
            private readonly int _mp;
            public TestPotion(string name, int price, int hp, int mp = 0, ItemRarity rarity = ItemRarity.Normal)
                : base(name, rarity, "desc", price, hp, mp)
            {
                _hp = hp;
                _mp = mp;
            }
            public override Consumable CreateFreshInstance() => new TestPotion(Name, Price, _hp, _mp, Rarity);
        }

        private static Gear MakeGear(string name, ItemKind kind, int score, ItemRarity rarity = ItemRarity.Normal, int price = 100)
            => new Gear(name, kind, rarity, "desc", price, new StatBlock(0, 0, 0, 0), atk: score);

        // ── ExcessItemSellSelector ────────────────────────────────────────────────

        [TestMethod]
        public void Selector_ConsumableSoldBeforeGear()
        {
            var bag = new ItemBag("Test", 2);
            bag.SetSlotItem(0, MakeGear("JunkShield", ItemKind.Shield, 1));
            bag.SetSlotItem(1, new TestPotion("Potion", 20, 30));

            var selection = ExcessItemSellSelector.Select(bag, MakeGear("NewSword", ItemKind.WeaponSword, 10), null, null);

            Assert.IsTrue(selection.HasSelection);
            Assert.IsFalse(selection.SellIncoming);
            Assert.AreEqual(1, selection.BagIndex, "The consumable should sell before even weaker gear");
        }

        [TestMethod]
        public void Selector_WeakestConsumableByRestoreEffect()
        {
            var bag = new ItemBag("Test", 3);
            bag.SetSlotItem(0, new TestPotion("MidPotion", 10, 100));
            bag.SetSlotItem(1, new TestPotion("WeakPotion", 50, 30));   // pricier but weakest effect
            bag.SetSlotItem(2, new TestPotion("StrongPotion", 5, 250));

            var selection = ExcessItemSellSelector.Select(bag, MakeGear("New", ItemKind.WeaponSword, 10), null, null);

            Assert.AreEqual(1, selection.BagIndex, "Weakest restore effect wins regardless of price");
        }

        [TestMethod]
        public void Selector_ZeroEffectConsumableSortsFirst()
        {
            var bag = new ItemBag("Test", 2);
            bag.SetSlotItem(0, new TestPotion("Potion", 20, 30));
            bag.SetSlotItem(1, new TestPotion("BattleBuff", 100, 0, 0));

            var selection = ExcessItemSellSelector.Select(bag, MakeGear("New", ItemKind.WeaponSword, 10), null, null);

            Assert.AreEqual(1, selection.BagIndex);
        }

        [TestMethod]
        public void Selector_GearComparedAcrossAllTypes()
        {
            // A lone strong sword must not sell before a junk shield of a different type.
            var bag = new ItemBag("Test", 2);
            bag.SetSlotItem(0, MakeGear("LegendarySword", ItemKind.WeaponSword, 50, ItemRarity.Legendary));
            bag.SetSlotItem(1, MakeGear("JunkShield", ItemKind.Shield, 2));

            var selection = ExcessItemSellSelector.Select(bag, MakeGear("NewArmor", ItemKind.ArmorMail, 20), null, null);

            Assert.AreEqual(1, selection.BagIndex, "Weakest gear across ALL types should be chosen");
        }

        [TestMethod]
        public void Selector_RarityFilterExcludesGear()
        {
            var bag = new ItemBag("Test", 2);
            bag.SetSlotItem(0, MakeGear("LegendaryDagger", ItemKind.WeaponKnife, 1, ItemRarity.Legendary));
            bag.SetSlotItem(1, MakeGear("NormalHelm", ItemKind.HatHelm, 5, ItemRarity.Normal));

            var selection = ExcessItemSellSelector.Select(bag, MakeGear("New", ItemKind.WeaponSword, 10, ItemRarity.Normal),
                null, r => r != ItemRarity.Legendary);

            Assert.AreEqual(1, selection.BagIndex, "Legendary gear is filtered out even when weakest");
        }

        [TestMethod]
        public void Selector_RarityFilterNeverAppliesToConsumables()
        {
            var bag = new ItemBag("Test", 1);
            bag.SetSlotItem(0, new TestPotion("RarePotion", 20, 30, 0, ItemRarity.Rare));

            var selection = ExcessItemSellSelector.Select(bag, MakeGear("New", ItemKind.WeaponSword, 10),
                null, r => r == ItemRarity.Normal);

            Assert.AreEqual(0, selection.BagIndex, "Consumables are never rarity-filtered");
        }

        [TestMethod]
        public void Selector_ProtectedItemsAreSkipped()
        {
            var bag = new ItemBag("Test", 2);
            bag.SetSlotItem(0, MakeGear("WeakButProtected", ItemKind.Shield, 1));
            bag.SetSlotItem(1, MakeGear("Stronger", ItemKind.WeaponSword, 10));

            var selection = ExcessItemSellSelector.Select(bag, MakeGear("New", ItemKind.WeaponSword, 20),
                idx => idx == 0, null);

            Assert.AreEqual(1, selection.BagIndex, "Synergy/stencil-protected items must never be selected");
        }

        [TestMethod]
        public void Selector_AllProtected_SellsIncomingWhenAllowed()
        {
            var bag = new ItemBag("Test", 2);
            bag.SetSlotItem(0, MakeGear("A", ItemKind.Shield, 1));
            bag.SetSlotItem(1, MakeGear("B", ItemKind.WeaponSword, 10));

            var selection = ExcessItemSellSelector.Select(bag, MakeGear("New", ItemKind.WeaponSword, 20),
                idx => true, null);

            Assert.IsTrue(selection.SellIncoming, "With every bag item protected, the incoming item is the only candidate");
        }

        [TestMethod]
        public void Selector_AllProtectedAndIncomingDisallowed_ReturnsNone()
        {
            var bag = new ItemBag("Test", 1);
            bag.SetSlotItem(0, MakeGear("A", ItemKind.Shield, 1));

            var selection = ExcessItemSellSelector.Select(bag, MakeGear("New", ItemKind.WeaponSword, 20, ItemRarity.Legendary),
                idx => true, r => r != ItemRarity.Legendary);

            Assert.IsFalse(selection.HasSelection);
        }

        [TestMethod]
        public void Selector_IncomingWeakestGear_SellsIncoming()
        {
            var bag = new ItemBag("Test", 2);
            bag.SetSlotItem(0, MakeGear("GoodSword", ItemKind.WeaponSword, 30));
            bag.SetSlotItem(1, MakeGear("GoodShield", ItemKind.Shield, 20));

            var selection = ExcessItemSellSelector.Select(bag, MakeGear("JunkSword", ItemKind.WeaponSword, 2), null, null);

            Assert.IsTrue(selection.SellIncoming, "Junk loot must not displace better gear");
        }

        [TestMethod]
        public void Selector_IncomingWeakestConsumable_SellsIncoming()
        {
            var bag = new ItemBag("Test", 1);
            bag.SetSlotItem(0, new TestPotion("StrongPotion", 50, 200));

            var selection = ExcessItemSellSelector.Select(bag, new TestPotion("WeakPotion", 10, 20), null, null);

            Assert.IsTrue(selection.SellIncoming);
        }

        [TestMethod]
        public void Selector_Tie_PrefersBagItemOverIncoming()
        {
            var bag = new ItemBag("Test", 1);
            bag.SetSlotItem(0, MakeGear("Sword", ItemKind.WeaponSword, 5));

            var selection = ExcessItemSellSelector.Select(bag, MakeGear("Sword", ItemKind.WeaponSword, 5), null, null);

            Assert.IsFalse(selection.SellIncoming, "On an exact tie the bag item sells and the new item is kept");
            Assert.AreEqual(0, selection.BagIndex);
        }

        // ── ItemBag full-bag stacking fix ─────────────────────────────────────────

        [TestMethod]
        public void ItemBag_TryAdd_StacksIntoPartialStackWhenFull()
        {
            var bag = new ItemBag("Test", 2);
            var stack = new TestPotion("Potion", 20, 30);
            stack.StackCount = 3;
            bag.SetSlotItem(0, stack);
            bag.SetSlotItem(1, MakeGear("Sword", ItemKind.WeaponSword, 5));
            Assert.IsTrue(bag.IsFull);

            bool added = bag.TryAdd(new TestPotion("Potion", 20, 30));

            Assert.IsTrue(added, "A stackable consumable needs no empty slot and must be accepted on a full bag");
            Assert.AreEqual(4, stack.StackCount);
            Assert.AreEqual(2, bag.Count);
        }

        // ── AutoSellExcessItemsService (headless: no grid, no vault/funds) ────────

        [TestMethod]
        public void Service_Disabled_ReturnsNone()
        {
            var svc = new AutoSellExcessItemsService { Enabled = false };
            var bag = new ItemBag("Test", 1);
            bag.SetSlotItem(0, MakeGear("Sword", ItemKind.WeaponSword, 5));

            Assert.AreEqual(AutoSellOutcome.None, svc.TryMakeRoom(bag, MakeGear("New", ItemKind.Shield, 1)));
            Assert.IsNotNull(bag.GetSlotItem(0), "Nothing may be sold while disabled");
        }

        [TestMethod]
        public void Service_EnabledByDefault()
        {
            Assert.IsTrue(new AutoSellExcessItemsService().Enabled);
            var svc = new AutoSellExcessItemsService();
            for (int i = 0; i < svc.RarityAllowed.Length; i++)
                Assert.IsTrue(svc.RarityAllowed[i], $"Rarity {i} should be allowed by default");
        }

        [TestMethod]
        public void Service_BagNotFull_ReturnsNone()
        {
            var svc = new AutoSellExcessItemsService();
            var bag = new ItemBag("Test", 2);
            bag.SetSlotItem(0, MakeGear("Sword", ItemKind.WeaponSword, 5));

            Assert.AreEqual(AutoSellOutcome.None, svc.TryMakeRoom(bag, MakeGear("New", ItemKind.Shield, 1)));
        }

        [TestMethod]
        public void Service_StackablePickup_ReturnsNoneWithoutSelling()
        {
            var svc = new AutoSellExcessItemsService();
            var bag = new ItemBag("Test", 2);
            var stack = new TestPotion("Potion", 20, 30);
            stack.StackCount = 2;
            bag.SetSlotItem(0, stack);
            bag.SetSlotItem(1, MakeGear("Sword", ItemKind.WeaponSword, 5));

            Assert.AreEqual(AutoSellOutcome.None, svc.TryMakeRoom(bag, new TestPotion("Potion", 20, 30)));
            Assert.AreEqual(2, bag.Count, "No sale should happen for a stackable pickup");
        }

        [TestMethod]
        public void Service_SellsWeakestBagItemAndFreesSlot()
        {
            var svc = new AutoSellExcessItemsService();
            var bag = new ItemBag("Test", 2);
            bag.SetSlotItem(0, MakeGear("GoodSword", ItemKind.WeaponSword, 30));
            bag.SetSlotItem(1, MakeGear("JunkShield", ItemKind.Shield, 2));
            var incoming = MakeGear("NewArmor", ItemKind.ArmorMail, 20);

            var outcome = svc.TryMakeRoom(bag, incoming);

            Assert.AreEqual(AutoSellOutcome.SoldBagItem, outcome);
            Assert.IsNull(bag.GetSlotItem(1), "The weakest gear should have been sold");
            Assert.IsTrue(bag.TryAdd(incoming), "A slot must now be free for the incoming item");
        }

        [TestMethod]
        public void Service_IncomingWeakest_SoldIncoming()
        {
            var svc = new AutoSellExcessItemsService();
            var bag = new ItemBag("Test", 1);
            bag.SetSlotItem(0, MakeGear("GoodSword", ItemKind.WeaponSword, 30));

            var outcome = svc.TryMakeRoom(bag, MakeGear("Junk", ItemKind.WeaponSword, 1));

            Assert.AreEqual(AutoSellOutcome.SoldIncoming, outcome);
            Assert.IsNotNull(bag.GetSlotItem(0), "The bag item must be untouched when the incoming item sells");
        }

        // ── Virtual layer (VirtualBattleRunner.CollectChestItem) ──────────────────

        private static (VirtualBattleRunner runner, ItemBag bag) CreateVirtualRunner(int bagCapacity)
        {
            var world = new VirtualWorldState();
            world.RegeneratePit(1);
            var hero = new Hero("TestHero", new Knight(), 10, new StatBlock(12, 10, 12, 5));
            var bag = new ItemBag("Test", bagCapacity);
            var runner = new VirtualBattleRunner(world, new VirtualBattlePartyView(hero, bag));
            runner.SetHeroAlly(hero);
            runner.SetMercenaries(new List<Mercenary>(0));
            return (runner, bag);
        }

        [TestMethod]
        public void VirtualRunner_AutoSellOff_PreservesBaselineFullBagBehavior()
        {
            var (runner, bag) = CreateVirtualRunner(1);
            bag.SetSlotItem(0, MakeGear("Sword", ItemKind.WeaponSword, 30));

            runner.CollectChestItem(MakeGear("NewShield", ItemKind.Shield, 1));

            Assert.AreEqual(0, runner.ItemsAutoSold, "Auto-sell must stay inert while off (balance baseline)");
            Assert.AreEqual(0, runner.AutoSellGold);
            Assert.AreEqual(1, bag.Count, "Item is dropped on a full bag, as before");
        }

        [TestMethod]
        public void VirtualRunner_AutoSellOn_SellsWeakestAndCollects()
        {
            var (runner, bag) = CreateVirtualRunner(2);
            runner.AutoSellExcessItems = true;
            runner.AutoEquipHero = false;        // keep the collected item in the bag so the count is observable
            runner.AutoEquipMercenaries = false;
            bag.SetSlotItem(0, MakeGear("GoodSword", ItemKind.WeaponSword, 30));
            bag.SetSlotItem(1, MakeGear("JunkShield", ItemKind.Shield, 2));
            var incoming = MakeGear("NewArmor", ItemKind.ArmorMail, 20);

            runner.CollectChestItem(incoming);

            Assert.AreEqual(1, runner.ItemsAutoSold);
            Assert.IsTrue(runner.AutoSellGold > 0, "Gold from the sold junk shield should be credited");
            Assert.AreEqual(2, bag.Count, "Junk shield sold, new armor collected");
        }

        [TestMethod]
        public void Service_RarityDisallowed_ProtectsGear()
        {
            var svc = new AutoSellExcessItemsService();
            svc.RarityAllowed[(int)ItemRarity.Legendary] = false;
            var bag = new ItemBag("Test", 1);
            bag.SetSlotItem(0, MakeGear("Legendary", ItemKind.WeaponSword, 1, ItemRarity.Legendary));

            var outcome = svc.TryMakeRoom(bag, MakeGear("New", ItemKind.Shield, 50, ItemRarity.Legendary));

            Assert.AreEqual(AutoSellOutcome.None, outcome, "Disallowed rarities may never be auto-sold, incoming included");
        }
    }
}
