using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Services;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Inventory;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Mercenaries;
using RolePlayingFramework.Stats;

namespace PitHero.Tests
{
    /// <summary>
    /// Tests for pre-pit auto-purchasing from the Second Chance vault (issue #345):
    /// gold-buffer and bag-space gating, "better than anything the party possesses" gear selection,
    /// rarity/type filters, the mercenary opt-in, and idempotent consumable top-up.
    /// </summary>
    [TestClass]
    public class AutoItemPurchaseServiceTests
    {
        private sealed class TestPotion : Consumable
        {
            private readonly int _hp;
            private readonly int _mp;
            public TestPotion(string name, int price, int hp, int mp = 0, int stackSize = 4)
                : base(name, ItemRarity.Normal, "desc", price, hp, mp)
            {
                _hp = hp;
                _mp = mp;
                StackSize = stackSize;
            }
            public override Consumable CreateFreshInstance() => new TestPotion(Name, Price, _hp, _mp, StackSize);
        }

        private static Gear MakeGear(string name, ItemKind kind, int score, ItemRarity rarity = ItemRarity.Normal, int price = 100)
            => new Gear(name, kind, rarity, "desc", price, new StatBlock(0, 0, 0, 0), atk: score);

        private static Hero MakeHero() => new Hero("TestHero", new Knight(), 10, new StatBlock(12, 10, 12, 5));

        private static AutoItemPurchaseService MakeService(GameStateService gameState, SecondChanceMerchantVault vault, int goldBuffer = 0)
        {
            var seedService = new AutoSeedPurchaseService(null, null, gameState, null) { GoldBuffer = goldBuffer };
            return new AutoItemPurchaseService(gameState, vault, seedService) { Enabled = true };
        }

        private static int RunPass(AutoItemPurchaseService svc, Hero hero, ItemBag bag, IReadOnlyList<Mercenary> mercs = null)
            => svc.RunPurchasePass(hero, bag, mercs, new List<IItem>());

        // ── Gear ──────────────────────────────────────────────────────────────────

        [TestMethod]
        public void Gear_BuysUpgradeIntoBag()
        {
            var gameState = new GameStateService { Funds = 1000 };
            var vault = new SecondChanceMerchantVault();
            vault.AddItem(MakeGear("GreatSword", ItemKind.WeaponSword, 50, price: 200));

            var svc = MakeService(gameState, vault);
            var bag = new ItemBag("Test", 10);

            Assert.AreEqual(1, RunPass(svc, MakeHero(), bag));
            Assert.AreEqual(800, gameState.Funds);
            Assert.AreEqual(1, bag.Count);
            Assert.AreEqual(0, vault.StackCount, "The bought stack is removed from the vault");
        }

        [TestMethod]
        public void Gear_DisabledServiceBuysNothing()
        {
            var gameState = new GameStateService { Funds = 1000 };
            var vault = new SecondChanceMerchantVault();
            vault.AddItem(MakeGear("GreatSword", ItemKind.WeaponSword, 50, price: 200));

            var svc = MakeService(gameState, vault);
            svc.Enabled = false;

            Assert.AreEqual(0, RunPass(svc, MakeHero(), new ItemBag("Test", 10)));
            Assert.AreEqual(1000, gameState.Funds);
        }

        [TestMethod]
        public void Gear_RespectsGoldBuffer()
        {
            var gameState = new GameStateService { Funds = 500 };
            var vault = new SecondChanceMerchantVault();
            vault.AddItem(MakeGear("GreatSword", ItemKind.WeaponSword, 50, price: 200));

            var svc = MakeService(gameState, vault, goldBuffer: 400);

            Assert.AreEqual(0, RunPass(svc, MakeHero(), new ItemBag("Test", 10)),
                "Buying would drop funds below the gold buffer");
            Assert.AreEqual(500, gameState.Funds);
        }

        [TestMethod]
        public void Gear_RespectsGoldBufferBoundaryExactly()
        {
            var gameState = new GameStateService { Funds = 600 };
            var vault = new SecondChanceMerchantVault();
            vault.AddItem(MakeGear("GreatSword", ItemKind.WeaponSword, 50, price: 200));

            var svc = MakeService(gameState, vault, goldBuffer: 400);

            Assert.AreEqual(1, RunPass(svc, MakeHero(), new ItemBag("Test", 10)),
                "Landing exactly on the buffer is allowed");
            Assert.AreEqual(400, gameState.Funds);
        }

        [TestMethod]
        public void Gear_NoPurchaseWhenBagFull()
        {
            var gameState = new GameStateService { Funds = 1000 };
            var vault = new SecondChanceMerchantVault();
            vault.AddItem(MakeGear("GreatSword", ItemKind.WeaponSword, 50, price: 200));

            var svc = MakeService(gameState, vault);
            var bag = new ItemBag("Test", 1);
            bag.SetSlotItem(0, MakeGear("Filler", ItemKind.Accessory, 1));

            Assert.AreEqual(0, RunPass(svc, MakeHero(), bag));
            Assert.AreEqual(1000, gameState.Funds);
        }

        [TestMethod]
        public void Gear_NotBoughtWhenEquippedGearIsBetter()
        {
            var gameState = new GameStateService { Funds = 1000 };
            var vault = new SecondChanceMerchantVault();
            vault.AddItem(MakeGear("RustySword", ItemKind.WeaponSword, 5, price: 50));

            var hero = MakeHero();
            hero.SetEquipmentSlot(EquipmentSlot.WeaponShield1, MakeGear("FineSword", ItemKind.WeaponSword, 40));

            var svc = MakeService(gameState, vault);

            Assert.AreEqual(0, RunPass(svc, hero, new ItemBag("Test", 10)));
            Assert.AreEqual(1000, gameState.Funds);
        }

        [TestMethod]
        public void Gear_NotBoughtWhenBagAlreadyHoldsSomethingBetter()
        {
            var gameState = new GameStateService { Funds = 1000 };
            var vault = new SecondChanceMerchantVault();
            vault.AddItem(MakeGear("OkSword", ItemKind.WeaponSword, 20, price: 100));

            var bag = new ItemBag("Test", 10);
            bag.SetSlotItem(0, MakeGear("CarriedSword", ItemKind.WeaponSword, 40));

            var svc = MakeService(gameState, vault);

            Assert.AreEqual(0, RunPass(svc, MakeHero(), bag),
                "Gear the party already possesses counts, even when it is only carried");
            Assert.AreEqual(1000, gameState.Funds);
        }

        [TestMethod]
        public void Gear_BuysBestCandidateOnly()
        {
            var gameState = new GameStateService { Funds = 10000 };
            var vault = new SecondChanceMerchantVault();
            vault.AddItem(MakeGear("OkSword", ItemKind.WeaponSword, 20, price: 100));
            vault.AddItem(MakeGear("BestSword", ItemKind.WeaponSword, 60, price: 300));
            vault.AddItem(MakeGear("MehSword", ItemKind.WeaponSword, 30, price: 150));

            var svc = MakeService(gameState, vault);
            var bag = new ItemBag("Test", 10);

            Assert.AreEqual(1, RunPass(svc, MakeHero(), bag), "One item per member per category");
            Assert.AreEqual("BestSword", bag.GetSlotItem(0).Name);
            Assert.AreEqual(9700, gameState.Funds);
        }

        [TestMethod]
        public void Gear_RarityFilterExcludesCandidate()
        {
            var gameState = new GameStateService { Funds = 10000 };
            var vault = new SecondChanceMerchantVault();
            vault.AddItem(MakeGear("EpicSword", ItemKind.WeaponSword, 60, ItemRarity.Epic, price: 300));

            var svc = MakeService(gameState, vault);
            svc.BuyRarityAllowed[(int)ItemRarity.Epic] = false;

            Assert.AreEqual(0, RunPass(svc, MakeHero(), new ItemBag("Test", 10)));
            Assert.AreEqual(10000, gameState.Funds);
        }

        [TestMethod]
        public void Gear_TypeFilterExcludesCandidate()
        {
            var gameState = new GameStateService { Funds = 10000 };
            var vault = new SecondChanceMerchantVault();
            vault.AddItem(MakeGear("GreatSword", ItemKind.WeaponSword, 60, price: 300));
            vault.AddItem(MakeGear("GreatShield", ItemKind.Shield, 40, price: 200));

            var svc = MakeService(gameState, vault);
            svc.BuyGearTypeAllowed[(int)GearCategory.Weapon] = false;

            var bag = new ItemBag("Test", 10);
            Assert.AreEqual(1, RunPass(svc, MakeHero(), bag));
            Assert.AreEqual("GreatShield", bag.GetSlotItem(0).Name, "Weapons are excluded, shields are not");
        }

        [TestMethod]
        public void Gear_MercenariesSkippedUnlessOptedIn()
        {
            var gameState = new GameStateService { Funds = 10000 };
            var vault = new SecondChanceMerchantVault();
            vault.AddItem(MakeGear("Sword A", ItemKind.WeaponSword, 60, price: 100));
            vault.AddItem(MakeGear("Sword B", ItemKind.WeaponSword, 55, price: 100));

            var mercs = new List<Mercenary> { new Mercenary("TestMerc", new Knight(), 5, new StatBlock(10, 10, 10, 5)) };

            var svc = MakeService(gameState, vault);
            svc.PurchaseMercenaryGear = false;
            Assert.AreEqual(1, RunPass(svc, MakeHero(), new ItemBag("Test", 10), mercs),
                "Only the hero shops while the mercenary opt-in is off");

            svc.PurchaseMercenaryGear = true;
            Assert.AreEqual(1, RunPass(svc, MakeHero(), new ItemBag("Test", 10), mercs),
                "With the opt-in on the mercenary shops too");
        }

        // ── Consumables ───────────────────────────────────────────────────────────

        [TestMethod]
        public void Consumables_NotBoughtWhenNoneSelected()
        {
            var gameState = new GameStateService { Funds = 10000 };
            var vault = new SecondChanceMerchantVault();
            var potion = new TestPotion("HP Potion", 20, 100);
            potion.StackCount = 8;
            vault.AddItem(potion);

            // No ConsumableSelected entries checked — selection alone gates consumable buying
            var svc = MakeService(gameState, vault);

            Assert.AreEqual(0, RunPass(svc, MakeHero(), new ItemBag("Test", 10)));
            Assert.AreEqual(10000, gameState.Funds);
        }

        [TestMethod]
        public void Consumables_TopUpToStackTargetAndIsIdempotent()
        {
            var gameState = new GameStateService { Funds = 10000 };
            var vault = new SecondChanceMerchantVault();

            // Catalog entry 0 is the HP Potion; stock plenty of them
            var stocked = ConsumableCatalog.CreateFresh(0);
            stocked.StackCount = stocked.StackSize * 5;
            vault.AddItem(stocked);

            var svc = MakeService(gameState, vault);
            svc.ConsumableSelected[0] = true;
            svc.ConsumableStackTargets[0] = 2;

            var bag = new ItemBag("Test", 10);
            Assert.AreEqual(2, RunPass(svc, MakeHero(), bag), "Two stacks bought to reach the target");
            Assert.AreEqual(2, bag.Count);

            int fundsAfterFirstPass = gameState.Funds;
            Assert.AreEqual(0, RunPass(svc, MakeHero(), bag), "A second pass is a no-op — the target is already met");
            Assert.AreEqual(fundsAfterFirstPass, gameState.Funds);
        }

        [TestMethod]
        public void Consumables_PartialTopUpCountsWhatIsCarried()
        {
            var gameState = new GameStateService { Funds = 10000 };
            var vault = new SecondChanceMerchantVault();
            var stocked = ConsumableCatalog.CreateFresh(0);
            stocked.StackCount = stocked.StackSize * 5;
            vault.AddItem(stocked);

            var svc = MakeService(gameState, vault);
            svc.ConsumableSelected[0] = true;
            svc.ConsumableStackTargets[0] = 3;

            var bag = new ItemBag("Test", 10);
            bag.SetSlotItem(0, ConsumableCatalog.CreateFresh(0));   // one stack already carried

            Assert.AreEqual(2, RunPass(svc, MakeHero(), bag), "Only the shortfall is bought");
            Assert.AreEqual(3, bag.Count);
        }

        [TestMethod]
        public void Consumables_NotBoughtWhenVaultIsEmpty()
        {
            var gameState = new GameStateService { Funds = 10000 };
            var svc = MakeService(gameState, new SecondChanceMerchantVault());
            svc.ConsumableSelected[0] = true;

            Assert.AreEqual(0, RunPass(svc, MakeHero(), new ItemBag("Test", 10)));
            Assert.AreEqual(10000, gameState.Funds);
        }

        // ── Priority ──────────────────────────────────────────────────────────────

        [TestMethod]
        public void Priority_TopCategorySpendsTheGoldFirst()
        {
            var stocked = ConsumableCatalog.CreateFresh(0);
            int potionUnitPrice = stocked.Price;
            int oneStackCost = potionUnitPrice * stocked.StackSize;

            // Enough gold for the sword OR the potion stack, not both
            int funds = oneStackCost + 10;

            var gearFirstState = new GameStateService { Funds = funds };
            var gearFirstVault = BuildMixedVault(funds);
            var gearFirst = MakeService(gearFirstState, gearFirstVault);
            gearFirst.ConsumableSelected[0] = true;
            gearFirst.ConsumablesFirst = false;

            var gearFirstBag = new ItemBag("Test", 10);
            RunPass(gearFirst, MakeHero(), gearFirstBag);
            Assert.IsTrue(gearFirstBag.GetSlotItem(0) is IGear, "Gear-first buys the sword before any potions");

            var consumablesFirstState = new GameStateService { Funds = funds };
            var consumablesFirstVault = BuildMixedVault(funds);
            var consumablesFirst = MakeService(consumablesFirstState, consumablesFirstVault);
            consumablesFirst.ConsumableSelected[0] = true;
            consumablesFirst.ConsumablesFirst = true;

            var consumablesFirstBag = new ItemBag("Test", 10);
            RunPass(consumablesFirst, MakeHero(), consumablesFirstBag);
            Assert.IsTrue(consumablesFirstBag.GetSlotItem(0) is Consumable, "Consumables-first buys potions before the sword");
        }

        private static SecondChanceMerchantVault BuildMixedVault(int swordPrice)
        {
            var vault = new SecondChanceMerchantVault();
            vault.AddItem(MakeGear("GreatSword", ItemKind.WeaponSword, 60, price: swordPrice));
            var stocked = ConsumableCatalog.CreateFresh(0);
            stocked.StackCount = stocked.StackSize * 3;
            vault.AddItem(stocked);
            return vault;
        }

        // ── Defaults ──────────────────────────────────────────────────────────────

        [TestMethod]
        public void Defaults_MatchIssueSpec()
        {
            var svc = new AutoItemPurchaseService(new GameStateService(), new SecondChanceMerchantVault(), null);

            Assert.IsFalse(svc.Enabled, "Auto-purchase is off by default");
            Assert.IsFalse(svc.ConsumablesFirst, "Gear outranks consumables by default");
            Assert.IsFalse(svc.PurchaseMercenaryGear, "Mercenary gear is opt-in");

            for (int i = 0; i < svc.BuyRarityAllowed.Length; i++)
                Assert.IsTrue(svc.BuyRarityAllowed[i], $"Rarity {i} should be buyable by default");
            for (int i = 0; i < svc.BuyGearTypeAllowed.Length; i++)
                Assert.IsTrue(svc.BuyGearTypeAllowed[i], $"Gear category {i} should be buyable by default");
            for (int i = 0; i < svc.ConsumableSelected.Length; i++)
            {
                Assert.IsFalse(svc.ConsumableSelected[i], $"Consumable {i} should be unselected by default");
                Assert.AreEqual(1, svc.ConsumableStackTargets[i], $"Consumable {i} should default to one stack");
            }
            Assert.AreEqual(0, svc.GoldBuffer, "A null gold-buffer source reads as no floor");
        }
    }
}
