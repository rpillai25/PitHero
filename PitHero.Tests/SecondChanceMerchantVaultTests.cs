using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Services;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Equipment.Swords;
using RolePlayingFramework.Equipment.Armor;
using RolePlayingFramework.Equipment.Accessories;
using RolePlayingFramework.Stats;
using System.Linq;
using PitHero;

namespace PitHero.Tests
{
    /// <summary>
    /// Tests for the Second Chance Merchant Vault system.
    /// Verifies that items from deceased heroes are properly stored and stacked.
    /// </summary>
    [TestClass]
    public class SecondChanceMerchantVaultTests
    {
        private SecondChanceMerchantVault _vault;

        [TestInitialize]
        public void SetUp()
        {
            _vault = new SecondChanceMerchantVault();
        }

        // ── Helpers for eviction tests ────────────────────────────────────────────

        /// <summary>Creates a gear item with a given name and attack-score (other stats zero).</summary>
        private static Gear MakeGear(string name, int score, ItemRarity rarity = ItemRarity.Normal, int price = 100)
            => new Gear(name, ItemKind.WeaponSword, rarity, "desc", price,
                        new StatBlock(0, 0, 0, 0), atk: score);

        private sealed class TestPotion : Consumable
        {
            private readonly int _hp;
            private readonly int _mp;
            public TestPotion(string name, int price, int hp, int mp = 0)
                : base(name, ItemRarity.Normal, "desc", price, hp, mp)
            { _hp = hp; _mp = mp; }
            public override Consumable CreateFreshInstance() => new TestPotion(Name, Price, _hp, _mp);
        }

        /// <summary>
        /// Fills the vault to exactly <see cref="SecondChanceMerchantVault.MaxStacks"/> distinct
        /// gear stacks (names "VaultGear0"…"VaultGear{N-1}", score == index + baseScore).
        /// </summary>
        private void FillVaultWithDistinctGear(int baseScore = 100)
        {
            for (int i = 0; i < SecondChanceMerchantVault.MaxStacks; i++)
                _vault.AddItem(MakeGear($"VaultGear{i}", baseScore + i));
        }

        [TestMethod]
        public void AddItem_SingleItem_AddsToVault()
        {
            // Arrange
            var sword = ShortSword.Create();

            // Act
            _vault.AddItem(sword);

            // Assert
            Assert.AreEqual(1, _vault.StackCount);
            Assert.AreEqual(1, _vault.TotalItemCount);
            Assert.AreEqual(InventoryTextKey.Inv_ShortSword_Name, _vault.Stacks[0].ItemTemplate.Name);
            Assert.AreEqual(1, _vault.Stacks[0].Quantity);
        }

        [TestMethod]
        public void AddItem_TwoIdenticalGear_CreatesOneStackWithQuantityTwo()
        {
            // Arrange
            var sword1 = ShortSword.Create();
            var sword2 = ShortSword.Create();

            // Act
            _vault.AddItem(sword1);
            _vault.AddItem(sword2);

            // Assert
            Assert.AreEqual(1, _vault.StackCount, "Should have only one stack for identical items");
            Assert.AreEqual(2, _vault.Stacks[0].Quantity, "Stack should contain 2 items");
        }

        [TestMethod]
        public void AddItem_DifferentGear_CreatesSeparateStacks()
        {
            // Arrange
            var sword = ShortSword.Create();
            var armor = LeatherArmor.Create();

            // Act
            _vault.AddItem(sword);
            _vault.AddItem(armor);

            // Assert
            Assert.AreEqual(2, _vault.StackCount);
            Assert.AreEqual(2, _vault.TotalItemCount);
        }

        [TestMethod]
        public void AddItem_Consumables_StacksCorrectly()
        {
            // Arrange
            var potion1 = new HPPotion();
            potion1.StackCount = 5;
            var potion2 = new HPPotion();
            potion2.StackCount = 8;

            // Act
            _vault.AddItem(potion1);
            _vault.AddItem(potion2);

            // Assert
            Assert.AreEqual(1, _vault.StackCount, "Potions should stack together");
            Assert.AreEqual(13, _vault.Stacks[0].Quantity, "Should have combined 5 + 8 = 13 potions");
        }

        [TestMethod]
        public void AddItem_ConsumablesExceedingMaxStack_CreatesMultipleStacks()
        {
            // Arrange
            var potion1 = new HPPotion();
            potion1.StackCount = 998;
            var potion2 = new HPPotion();
            potion2.StackCount = 10;

            // Act
            _vault.AddItem(potion1);
            _vault.AddItem(potion2);

            // Assert - Total is 998 + 10 = 1008 potions, which should be split as 999 + 9
            Assert.AreEqual(2, _vault.StackCount, "Should create two stacks when exceeding max");
            
            // Find the stacks (order may vary)
            var stacks = _vault.Stacks.ToList();
            var quantities = stacks.Select(s => s.Quantity).OrderBy(q => q).ToList();
            
            Assert.AreEqual(9, quantities[0], "Second stack should have remainder (1008 - 999 = 9)");
            Assert.AreEqual(999, quantities[1], "First stack should be maxed at 999");
        }

        [TestMethod]
        public void AddItem_MaxStackOf999_VerifyCapEnforced()
        {
            // Arrange
            var potion = new HPPotion();
            potion.StackCount = 999;

            // Act
            _vault.AddItem(potion);

            // Assert
            Assert.AreEqual(999, _vault.Stacks[0].Quantity);

            // Try to add one more
            var potion2 = new HPPotion();
            potion2.StackCount = 1;
            _vault.AddItem(potion2);

            // Should create a new stack
            Assert.AreEqual(2, _vault.StackCount);
            Assert.AreEqual(1, _vault.Stacks[1].Quantity);
        }

        [TestMethod]
        public void AddItems_MultipleItems_AddsAllCorrectly()
        {
            // Arrange
            var items = new IItem[]
            {
                ShortSword.Create(),
                ShortSword.Create(),
                LeatherArmor.Create(),
                new HPPotion() { StackCount = 10 },
                new HPPotion() { StackCount = 5 },
                ProtectRing.Create()
            };

            // Act
            _vault.AddItems(items);

            // Assert
            Assert.AreEqual(4, _vault.StackCount, "Should have 4 unique stacks");
            Assert.AreEqual(19, _vault.TotalItemCount, "Total: 2 swords + 1 armor + 15 potions + 1 ring = 19");
        }

        [TestMethod]
        public void RemoveQuantity_ValidRemoval_RemovesCorrectly()
        {
            // Arrange
            var potion = new HPPotion();
            potion.StackCount = 20;
            _vault.AddItem(potion);
            var stack = _vault.Stacks[0];

            // Act
            bool result = _vault.RemoveQuantity(stack, 5);

            // Assert
            Assert.IsTrue(result);
            Assert.AreEqual(15, stack.Quantity);
            Assert.AreEqual(1, _vault.StackCount, "Stack should still exist");
        }

        [TestMethod]
        public void RemoveQuantity_RemoveAllInStack_RemovesStack()
        {
            // Arrange
            var sword = ShortSword.Create();
            _vault.AddItem(sword);
            var stack = _vault.Stacks[0];

            // Act
            bool result = _vault.RemoveQuantity(stack, 1);

            // Assert
            Assert.IsTrue(result);
            Assert.AreEqual(0, _vault.StackCount, "Stack should be removed");
        }

        [TestMethod]
        public void RemoveQuantity_InsufficientQuantity_ReturnsFalse()
        {
            // Arrange
            var sword = ShortSword.Create();
            _vault.AddItem(sword);
            var stack = _vault.Stacks[0];

            // Act
            bool result = _vault.RemoveQuantity(stack, 5);

            // Assert
            Assert.IsFalse(result);
            Assert.AreEqual(1, stack.Quantity, "Quantity should not change");
        }

        [TestMethod]
        public void Clear_WithItems_RemovesAllItems()
        {
            // Arrange
            _vault.AddItem(ShortSword.Create());
            _vault.AddItem(LeatherArmor.Create());
            _vault.AddItem(new HPPotion());

            // Act
            _vault.Clear();

            // Assert
            Assert.AreEqual(0, _vault.StackCount);
            Assert.AreEqual(0, _vault.TotalItemCount);
        }

        [TestMethod]
        public void SimulateHeroDeath_AllEquipmentAndInventory_StacksProperly()
        {
            // Arrange - Simulate a hero with:
            // - 2 different swords in inventory
            // - 1 armor equipped
            // - 16 HP potions in one stack
            // - 16 HP potions in another stack
            // - 1 accessory
            var items = new IItem[]
            {
                ShortSword.Create(),      // Inventory
                ShortSword.Create(),      // Inventory (duplicate)
                LeatherArmor.Create(),      // Equipped armor
                new HPPotion() { StackCount = 16 },  // Inventory stack 1
                new HPPotion() { StackCount = 16 },  // Inventory stack 2
                ProtectRing.Create()      // Equipped accessory
            };

            // Act
            _vault.AddItems(items);

            // Assert
            Assert.AreEqual(4, _vault.StackCount, "Should have 4 unique item types");
            
            // Find the potion stack
            var potionStack = _vault.Stacks.FirstOrDefault(s => s.ItemTemplate.Kind == ItemKind.Consumable);
            Assert.IsNotNull(potionStack);
            Assert.AreEqual(32, potionStack.Quantity, "HP Potions should stack: 16 + 16 = 32");

            // Find the sword stack
            var swordStack = _vault.Stacks.FirstOrDefault(s => s.ItemTemplate.Name == InventoryTextKey.Inv_ShortSword_Name);
            Assert.IsNotNull(swordStack);
            Assert.AreEqual(2, swordStack.Quantity, "Short Swords should stack: 2 total");
        }

        [TestMethod]
        public void AddItem_MultipleHeroDeaths_AccumulatesItems()
        {
            // Arrange - Simulate 3 heroes dying with HP potions
            var hero1Items = new IItem[] { new HPPotion() { StackCount = 20 } };
            var hero2Items = new IItem[] { new HPPotion() { StackCount = 30 } };
            var hero3Items = new IItem[] { new HPPotion() { StackCount = 15 } };

            // Act
            _vault.AddItems(hero1Items);
            _vault.AddItems(hero2Items);
            _vault.AddItems(hero3Items);

            // Assert
            Assert.AreEqual(1, _vault.StackCount, "Should have one stack of HP Potions");
            Assert.AreEqual(65, _vault.Stacks[0].Quantity, "Should have 20 + 30 + 15 = 65 potions");
        }

        [TestMethod]
        public void AddItem_LargeStacksAcrossMultipleHeroes_HandlesMaxStackCorrectly()
        {
            // Arrange - Simulate many heroes dying with many potions
            for (int i = 0; i < 10; i++)
            {
                var potion = new HPPotion() { StackCount = 150 };
                _vault.AddItem(potion);
            }

            // Act - Total should be 10 * 150 = 1500 potions

            // Assert
            Assert.AreEqual(2, _vault.Stacks.Count(s => s.ItemTemplate.Kind == ItemKind.Consumable),
                "Should have 2 stacks: 999 + 501");

            var potionStacks = _vault.Stacks.Where(s => s.ItemTemplate.Kind == ItemKind.Consumable).ToList();
            var totalPotions = potionStacks.Sum(s => s.Quantity);
            Assert.AreEqual(1500, totalPotions, "Total potions should be 1500");
        }

        // ── Capacity / eviction tests (issue #373) ────────────────────────────────

        [TestMethod]
        public void Cap_EnforceMaxStacks_540Limit()
        {
            // Add MaxStacks distinct gear items then one more (score is always strictly higher
            // than all in-vault items so the weakest in-vault item gets evicted, not incoming).
            FillVaultWithDistinctGear(baseScore: 1);
            Assert.AreEqual(SecondChanceMerchantVault.MaxStacks, _vault.StackCount,
                "Vault should be at capacity after filling");

            // The 541st item has score MaxStacks+1, stronger than all existing (score 1..MaxStacks)
            // so the weakest existing (score=1, name "VaultGear0") is evicted.
            var superGear = MakeGear("SuperGear", score: SecondChanceMerchantVault.MaxStacks + 1);
            _vault.AddItem(superGear);

            Assert.AreEqual(SecondChanceMerchantVault.MaxStacks, _vault.StackCount,
                "StackCount must stay at cap after eviction");
            Assert.IsFalse(_vault.Stacks.Any(s => s.ItemTemplate.Name == "VaultGear0"),
                "Weakest item (VaultGear0, score=1) must have been evicted");
            Assert.IsTrue(_vault.Stacks.Any(s => s.ItemTemplate.Name == "SuperGear"),
                "The superior incoming item must be present");
        }

        [TestMethod]
        public void Cap_WeakestGearEvicted_IncomingPresent()
        {
            // Vault of moderately strong gear; a stronger incoming displaces the weakest.
            for (int i = 0; i < SecondChanceMerchantVault.MaxStacks; i++)
                _vault.AddItem(MakeGear($"Gear{i}", score: 50 + i)); // scores 50..589

            var strongIncoming = MakeGear("StrongIncoming", score: 9999);
            _vault.AddItem(strongIncoming);

            Assert.AreEqual(SecondChanceMerchantVault.MaxStacks, _vault.StackCount);
            Assert.IsFalse(_vault.Stacks.Any(s => s.ItemTemplate.Name == "Gear0"),
                "Gear0 (weakest, score=50) should be gone");
            Assert.IsTrue(_vault.Stacks.Any(s => s.ItemTemplate.Name == "StrongIncoming"),
                "StrongIncoming must enter the vault");
        }

        [TestMethod]
        public void Cap_ConsumablesEvictedBeforeGear()
        {
            // 539 strong gear items + 1 cheap consumable = 540 stacks.
            for (int i = 0; i < SecondChanceMerchantVault.MaxStacks - 1; i++)
                _vault.AddItem(MakeGear($"StrongGear{i}", score: 100));

            var weakPotion = new TestPotion("WeakPotion", price: 5, hp: 10);
            weakPotion.StackCount = 1;
            _vault.AddItem(weakPotion);

            Assert.AreEqual(SecondChanceMerchantVault.MaxStacks, _vault.StackCount);

            // Now add a new gear item — consumable pass fires first and finds the weakPotion
            var newGear = MakeGear("NewGear", score: 50);
            _vault.AddItem(newGear);

            Assert.AreEqual(SecondChanceMerchantVault.MaxStacks, _vault.StackCount);
            Assert.IsFalse(_vault.Stacks.Any(s => s.ItemTemplate.Name == "WeakPotion"),
                "The consumable must be evicted before any gear");
            Assert.IsTrue(_vault.Stacks.Any(s => s.ItemTemplate.Name == "NewGear"),
                "The new gear must enter the vault");
            Assert.AreEqual(SecondChanceMerchantVault.MaxStacks,
                _vault.Stacks.Count(s => s.ItemTemplate.Kind != ItemKind.Consumable),
                "539 original gear stacks + 1 new gear = 540 gear items, 0 consumables");
        }

        [TestMethod]
        public void Cap_IncomingWeakGear_IsRejected()
        {
            // All 540 slots filled with strong gear; an arriving junk piece must be discarded.
            FillVaultWithDistinctGear(baseScore: 100); // scores 100..639

            var junkGear = MakeGear("JunkGear", score: 1); // weaker than all existing
            _vault.AddItem(junkGear);

            Assert.AreEqual(SecondChanceMerchantVault.MaxStacks, _vault.StackCount,
                "Count must not change when incoming is weakest");
            Assert.IsFalse(_vault.Stacks.Any(s => s.ItemTemplate.Name == "JunkGear"),
                "Junk gear must not enter the vault");
        }

        [TestMethod]
        public void Cap_StackingIntoExistingStack_NeverTriggersEviction()
        {
            // Fill to cap with distinct gear, one of which is a ShortSword (qty=1).
            // Adding a second ShortSword stacks into the existing slot — no new stack is created
            // and therefore no eviction should occur.
            for (int i = 0; i < SecondChanceMerchantVault.MaxStacks - 1; i++)
                _vault.AddItem(MakeGear($"UniqueGear{i}", score: 100 + i));

            var firstSword = ShortSword.Create();
            _vault.AddItem(firstSword);
            Assert.AreEqual(SecondChanceMerchantVault.MaxStacks, _vault.StackCount);

            var secondSword = ShortSword.Create();
            _vault.AddItem(secondSword);

            // Stack count must remain the same; the ShortSword stack gains +1 quantity
            Assert.AreEqual(SecondChanceMerchantVault.MaxStacks, _vault.StackCount,
                "Stacking into an existing slot must never evict anything");
            var swordStack = _vault.Stacks.FirstOrDefault(s => s.ItemTemplate.Name == firstSword.Name);
            Assert.IsNotNull(swordStack, "ShortSword stack must still exist");
            Assert.AreEqual(2, swordStack.Quantity, "Stack quantity must grow to 2");
        }

        [TestMethod]
        public void Cap_FullKeyTie_ExistingEvicted_IncomingEnters()
        {
            // All 540 items share the same gear key (score=10, Normal, price=100) but have
            // unique names.  A 541st item with the same key causes the slot at index 0 to be
            // evicted (lower index wins tie-breaking) and the incoming item to enter.
            for (int i = 0; i < SecondChanceMerchantVault.MaxStacks; i++)
                _vault.AddItem(MakeGear($"TiedGear{i}", score: 10, price: 100));

            var sameKeyIncoming = MakeGear("TiedIncoming", score: 10, price: 100);
            _vault.AddItem(sameKeyIncoming);

            Assert.AreEqual(SecondChanceMerchantVault.MaxStacks, _vault.StackCount,
                "Count must not change");
            Assert.IsFalse(_vault.Stacks.Any(s => s.ItemTemplate.Name == "TiedGear0"),
                "Index-0 item (TiedGear0) must be evicted — lower index wins the full-key tie");
            Assert.IsTrue(_vault.Stacks.Any(s => s.ItemTemplate.Name == "TiedIncoming"),
                "Incoming item with same key must enter the vault");
        }

        [TestMethod]
        public void Cap_LargeConsumable_EvictionCheckFiresPerNewStack()
        {
            // Vault: 538 strong gear + 2 weak consumables = 540 stacks.
            // Adding a strong consumable with qty=1001 (→ 2 new stacks: 999 + 2).
            // First new stack: weakest consumable (Potion0, index 538) is evicted.
            // Second new stack: weakest remaining consumable (Potion1, now at index 538) is evicted.
            for (int i = 0; i < SecondChanceMerchantVault.MaxStacks - 2; i++)
                _vault.AddItem(MakeGear($"StrongGear{i}", score: 100));

            var weakPotion0 = new TestPotion("WeakPotion0", price: 5, hp: 5);
            weakPotion0.StackCount = 1;
            _vault.AddItem(weakPotion0);

            var weakPotion1 = new TestPotion("WeakPotion1", price: 5, hp: 5);
            weakPotion1.StackCount = 1;
            _vault.AddItem(weakPotion1);

            Assert.AreEqual(SecondChanceMerchantVault.MaxStacks, _vault.StackCount);

            // Strong consumable with qty > 999 — requires 2 new stacks (999 + 2)
            var strongPotion = new TestPotion("StrongPotion", price: 200, hp: 200);
            strongPotion.StackCount = 1001;
            _vault.AddItem(strongPotion);

            Assert.AreEqual(SecondChanceMerchantVault.MaxStacks, _vault.StackCount,
                "StackCount must remain at cap");
            Assert.IsFalse(_vault.Stacks.Any(s => s.ItemTemplate.Name == "WeakPotion0"),
                "WeakPotion0 must be evicted on the first new-stack check");
            Assert.IsFalse(_vault.Stacks.Any(s => s.ItemTemplate.Name == "WeakPotion1"),
                "WeakPotion1 must be evicted on the second new-stack check");

            // Two StrongPotion stacks: 999 + 2
            var strongStacks = _vault.Stacks.Where(s => s.ItemTemplate.Name == "StrongPotion").ToList();
            Assert.AreEqual(2, strongStacks.Count, "StrongPotion must occupy 2 new stacks");
            CollectionAssert.AreEquivalent(new[] { 999, 2 }, strongStacks.Select(s => s.Quantity).ToList(),
                "Stack quantities must be 999 + 2");
        }
    }
}

