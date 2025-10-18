using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Inventory;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Jobs;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Jobs.Secondary;
using RolePlayingFramework.Jobs.Tertiary;
using RolePlayingFramework.Stats;

namespace PitHero.Tests
{
    [TestClass]
    public class ConsumableUsageTests
    {
        [TestMethod]
        public void ConsumableUsage_HPPotion_RestoresHP()
        {
            // Arrange
            var baseStats = new StatBlock(10, 10, 10, 10);
            var hero = new Hero("TestHero", new Knight(), 1, baseStats);
            var maxHP = hero.MaxHP;
            System.Diagnostics.Debug.WriteLine($"MaxHP: {maxHP}, CurrentHP: {hero.CurrentHP}");
            
            hero.TakeDamage(50); // Reduce HP
            var hpAfterDamage = hero.CurrentHP;
            System.Diagnostics.Debug.WriteLine($"After taking 50 damage, CurrentHP: {hpAfterDamage}");
            
            var hpPotion = PotionItems.HPPotion();
            System.Diagnostics.Debug.WriteLine($"HPPotion restore amount: {hpPotion.HPRestoreAmount}");

            // Act
            var success = hpPotion.Consume(hero);
            System.Diagnostics.Debug.WriteLine($"After consuming potion, CurrentHP: {hero.CurrentHP}, success: {success}");

            // Assert
            Assert.IsTrue(success, "Potion consumption should succeed");
            // Should restore 100 HP, but can't exceed maxHP
            Assert.IsTrue(hero.CurrentHP > hpAfterDamage, $"HP should increase from {hpAfterDamage} to at least {hpAfterDamage + 1}");
            Assert.IsTrue(hero.CurrentHP <= maxHP, $"HP should not exceed maxHP of {maxHP}");
        }

        [TestMethod]
        public void ConsumableUsage_APPotion_RestoresAP()
        {
            // Arrange
            var baseStats = new StatBlock(10, 10, 10, 10);
            var hero = new Hero("TestHero", new Knight(), 1, baseStats);
            var maxMP = hero.MaxMP;
            
            // Reduce AP to half
            var targetMP = maxAP / 2;
            while (hero.CurrentMP > targetAP)
            {
                hero.SpendAP(1);
            }
            
            var mpAfterSpend = hero.CurrentMP;
            var apPotion = PotionItems.MPPotion();

            // Act
            var success = apPotion.Consume(hero);

            // Assert
            Assert.IsTrue(success, "Potion consumption should succeed");
            Assert.IsTrue(hero.CurrentMP > apAfterSpend, $"MP should increase from {apAfterSpend}, but is {hero.CurrentMP}");
            Assert.IsTrue(hero.CurrentMP <= maxAP, $"MP should not exceed maxMP of {maxAP}");
        }

        [TestMethod]
        public void ConsumableUsage_MixPotion_RestoresBothHPAndAP()
        {
            // Arrange
            var baseStats = new StatBlock(10, 10, 10, 10);
            var hero = new Hero("TestHero", new Knight(), 1, baseStats);
            var maxHP = hero.MaxHP;
            var maxMP = hero.MaxMP;
            
            hero.TakeDamage(50);
            
            // Reduce AP to half
            var targetMP = maxAP / 2;
            while (hero.CurrentMP > targetAP)
            {
                hero.SpendAP(1);
            }
            
            var hpAfterDamage = hero.CurrentHP;
            var mpAfterSpend = hero.CurrentMP;
            var mixPotion = PotionItems.MixPotion();

            // Act
            var success = mixPotion.Consume(hero);

            // Assert
            Assert.IsTrue(success, "Potion consumption should succeed");
            Assert.IsTrue(hero.CurrentHP > hpAfterDamage, $"HP should increase from {hpAfterDamage}");
            Assert.IsTrue(hero.CurrentMP > apAfterSpend, $"MP should increase from {apAfterSpend}");
            Assert.IsTrue(hero.CurrentHP <= maxHP, $"HP should not exceed maxHP of {maxHP}");
            Assert.IsTrue(hero.CurrentMP <= maxAP, $"MP should not exceed maxMP of {maxAP}");
        }

        [TestMethod]
        public void ConsumableUsage_FullHPPotion_RestoresFullHP()
        {
            // Arrange
            var baseStats = new StatBlock(10, 10, 10, 10);
            var hero = new Hero("TestHero", new Knight(), 1, baseStats);
            hero.TakeDamage(50);
            var maxHP = hero.MaxHP;
            var fullHPPotion = PotionItems.FullHPPotion();

            // Act
            var success = fullHPPotion.Consume(hero);

            // Assert
            Assert.IsTrue(success, "Potion consumption should succeed");
            Assert.AreEqual(maxHP, hero.CurrentHP, "HP should be restored to max");
        }

        [TestMethod]
        public void ConsumableUsage_ItemBagConsumeFromStack_DecrementsStack()
        {
            // Arrange
            var bag = new ItemBag();
            var hpPotion = PotionItems.HPPotion();
            hpPotion.StackCount = 3;
            bag.TryAdd(hpPotion);

            // Act
            var success = bag.ConsumeFromStack(0);

            // Assert
            Assert.IsTrue(success);
            Assert.AreEqual(2, hpPotion.StackCount);
            Assert.AreEqual(hpPotion, bag.GetSlotItem(0)); // Item still in slot
        }

        [TestMethod]
        public void ConsumableUsage_ItemBagConsumeFromStack_RemovesLastItem()
        {
            // Arrange
            var bag = new ItemBag();
            var hpPotion = PotionItems.HPPotion();
            hpPotion.StackCount = 1;
            bag.TryAdd(hpPotion);

            // Act
            var success = bag.ConsumeFromStack(0);

            // Assert
            Assert.IsTrue(success);
            Assert.IsNull(bag.GetSlotItem(0)); // Item removed from slot
            Assert.AreEqual(0, bag.Count);
        }

        [TestMethod]
        public void ConsumableUsage_ItemBagConsumeFromStack_FailsOnNonConsumable()
        {
            // Arrange
            var bag = new ItemBag();
            var weapon = new TestWeapon();
            bag.TryAdd(weapon);

            // Act
            var success = bag.ConsumeFromStack(0);

            // Assert
            Assert.IsFalse(success);
            Assert.AreEqual(weapon, bag.GetSlotItem(0)); // Item still in slot
        }

        [TestMethod]
        public void ConsumableUsage_ItemBagConsumeFromStack_FailsOnEmptySlot()
        {
            // Arrange
            var bag = new ItemBag();

            // Act
            var success = bag.ConsumeFromStack(0);

            // Assert
            Assert.IsFalse(success);
        }

        [TestMethod]
        public void ConsumableUsage_ItemBagConsumeFromStack_FailsOnInvalidIndex()
        {
            // Arrange
            var bag = new ItemBag(capacity: 12);

            // Act
            var success1 = bag.ConsumeFromStack(-1);
            var success2 = bag.ConsumeFromStack(20);

            // Assert
            Assert.IsFalse(success1);
            Assert.IsFalse(success2);
        }

        [TestMethod]
        public void ConsumableUsage_MidPotions_RestoreCorrectAmounts()
        {
            // Arrange
            var baseStats = new StatBlock(10, 10, 10, 10);
            var hero = new Hero("TestHero", new Knight(), 1, baseStats);
            var maxHP = hero.MaxHP;
            var maxMP = hero.MaxMP;
            
            hero.TakeDamage(100);
            
            // Reduce AP to half
            var targetMP = maxAP / 2;
            while (hero.CurrentMP > targetAP)
            {
                hero.SpendAP(1);
            }
            
            var hpAfterDamage = hero.CurrentHP;
            var mpAfterSpend = hero.CurrentMP;
            
            var midHPPotion = PotionItems.MidHPPotion();
            var midAPPotion = PotionItems.MidMPPotion();

            // Act
            var success1 = midHPPotion.Consume(hero);
            var success2 = midAPPotion.Consume(hero);

            // Assert
            Assert.IsTrue(success1, "MidHPPotion consumption should succeed");
            Assert.IsTrue(success2, "MidAPPotion consumption should succeed");
            Assert.IsTrue(hero.CurrentHP > hpAfterDamage, $"HP should increase from {hpAfterDamage}");
            Assert.IsTrue(hero.CurrentMP > apAfterSpend, $"MP should increase from {apAfterSpend}");
            Assert.IsTrue(hero.CurrentHP <= maxHP, $"HP should not exceed maxHP of {maxHP}");
            Assert.IsTrue(hero.CurrentMP <= maxAP, $"MP should not exceed maxMP of {maxAP}");
        }

        [TestMethod]
        public void ConsumableUsage_PotionAtMaxHP_ShouldNotConsume()
        {
            // Arrange
            var baseStats = new StatBlock(10, 10, 10, 10);
            var hero = new Hero("TestHero", new Knight(), 1, baseStats);
            var maxHP = hero.MaxHP;
            
            // Hero is already at max HP
            Assert.AreEqual(maxHP, hero.CurrentHP, "Hero should start at max HP");
            
            var hpPotion = PotionItems.HPPotion();

            // Act
            var success = hpPotion.Consume(hero);

            // Assert
            Assert.IsFalse(success, "HPPotion should not be consumed when hero is at max HP");
            Assert.AreEqual(maxHP, hero.CurrentHP, "HP should remain at max");
        }

        [TestMethod]
        public void ConsumableUsage_PotionAtMaxAP_ShouldNotConsume()
        {
            // Arrange
            var baseStats = new StatBlock(10, 10, 10, 10);
            var hero = new Hero("TestHero", new Knight(), 1, baseStats);
            var maxMP = hero.MaxMP;
            
            // Hero is already at max AP
            Assert.AreEqual(maxAP, hero.CurrentMP, "Hero should start at max AP");
            
            var apPotion = PotionItems.MPPotion();

            // Act
            var success = apPotion.Consume(hero);

            // Assert
            Assert.IsFalse(success, "APPotion should not be consumed when hero is at max AP");
            Assert.AreEqual(maxAP, hero.CurrentMP, "MP should remain at max");
        }

        [TestMethod]
        public void ConsumableUsage_MixPotionAtMaxBoth_ShouldNotConsume()
        {
            // Arrange
            var baseStats = new StatBlock(10, 10, 10, 10);
            var hero = new Hero("TestHero", new Knight(), 1, baseStats);
            var maxHP = hero.MaxHP;
            var maxMP = hero.MaxMP;
            
            // Hero is already at max HP and AP
            Assert.AreEqual(maxHP, hero.CurrentHP);
            Assert.AreEqual(maxAP, hero.CurrentMP);
            
            var mixPotion = PotionItems.MixPotion();

            // Act
            var success = mixPotion.Consume(hero);

            // Assert
            Assert.IsFalse(success, "MixPotion should not be consumed when hero is at max HP and AP");
        }

        [TestMethod]
        public void ConsumableUsage_MixPotionPartialRestore_ShouldConsume()
        {
            // Arrange
            var baseStats = new StatBlock(10, 10, 10, 10);
            var hero = new Hero("TestHero", new Knight(), 1, baseStats);
            
            // Reduce only HP, keep AP at max
            hero.TakeDamage(50);
            var maxMP = hero.MaxMP;
            
            Assert.AreEqual(maxAP, hero.CurrentMP, "MP should be at max");
            Assert.IsTrue(hero.CurrentHP < hero.MaxHP, "HP should be below max");
            
            var mixPotion = PotionItems.MixPotion();

            // Act
            var success = mixPotion.Consume(hero);

            // Assert
            Assert.IsTrue(success, "MixPotion should be consumed when at least one stat can be restored");
        }

        // Helper class for testing non-consumable items
        private class TestWeapon : IItem
        {
            public string Name => "Test Sword";
            public ItemKind Kind => ItemKind.WeaponSword;
            public ItemRarity Rarity => ItemRarity.Normal;
            public string Description => "A test weapon";
            public int Price => 100;
        }
    }
}
