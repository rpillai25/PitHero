using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Services;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Inventory;
using RolePlayingFramework.Jobs;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Stats;

namespace PitHero.Tests
{
    /// <summary>
    /// Manual job change plumbing (issue #379): the outgoing crystal returns to the crystal
    /// inventory (Second Chance vault only as full-inventory fallback) and equipment carries
    /// over to the new hero, falling back bag → vault for items the new job can't equip.
    /// </summary>
    [TestClass]
    public class ManualJobChangeTests
    {
        private static readonly StatBlock BaseStats = new StatBlock(5, 5, 5, 5);

        private static HeroCrystal CreateCrystal(IJob job, int level = 1)
            => new HeroCrystal("Test Crystal", job, level, BaseStats);

        private static Gear CreateGear(string name, ItemKind kind, JobType allowedJobs)
            => new Gear(name, kind, ItemRarity.Normal, "desc", 10, new StatBlock(0, 0, 0, 0), allowedJobs: allowedJobs);

        // ── Crystal routing ──────────────────────────────────────────────────────

        [TestMethod]
        public void ReturnCrystal_LandsInCrystalInventory()
        {
            var service = new CrystalCollectionService();
            var vault = new SecondChanceMerchantVault();
            var crystal = CreateCrystal(new Knight());

            bool inInventory = HeroJobChangeHelper.ReturnCrystalToInventory(crystal, service, vault);

            Assert.IsTrue(inInventory);
            Assert.AreEqual(1, service.InventoryCount);
            Assert.AreEqual(0, vault.CrystalCount);
        }

        [TestMethod]
        public void ReturnCrystal_FullInventory_FallsBackToVault()
        {
            var service = new CrystalCollectionService();
            var vault = new SecondChanceMerchantVault();
            for (int i = 0; i < service.InventoryCapacity; i++)
                Assert.IsTrue(service.TryAddToInventory(CreateCrystal(new Knight())));

            var crystal = CreateCrystal(new Mage());
            bool inInventory = HeroJobChangeHelper.ReturnCrystalToInventory(crystal, service, vault);

            Assert.IsFalse(inInventory);
            Assert.AreEqual(service.InventoryCapacity, service.InventoryCount);
            Assert.AreEqual(1, vault.CrystalCount);
            Assert.AreSame(crystal, vault.LostCrystals[0]);
        }

        [TestMethod]
        public void ReturnCrystal_NullCrystal_NoOp()
        {
            var service = new CrystalCollectionService();
            var vault = new SecondChanceMerchantVault();

            Assert.IsFalse(HeroJobChangeHelper.ReturnCrystalToInventory(null, service, vault));
            Assert.AreEqual(0, service.InventoryCount);
            Assert.AreEqual(0, vault.CrystalCount);
        }

        // ── Equipment transfer ───────────────────────────────────────────────────

        [TestMethod]
        public void TransferEquipment_EquippableGear_KeepsSlots()
        {
            var oldHero = new Hero("Old", new Knight(), 10, BaseStats);
            var newHero = new Hero("New", new Mage(), 1, BaseStats);
            var sword = CreateGear("Sword", ItemKind.WeaponSword, JobType.All);
            var armor = CreateGear("Mail", ItemKind.ArmorMail, JobType.All);
            var ring = CreateGear("Ring", ItemKind.Accessory, JobType.All);
            Assert.IsTrue(oldHero.SetEquipmentSlot(EquipmentSlot.WeaponShield1, sword));
            Assert.IsTrue(oldHero.SetEquipmentSlot(EquipmentSlot.Armor, armor));
            Assert.IsTrue(oldHero.SetEquipmentSlot(EquipmentSlot.Accessory1, ring));

            var bag = new ItemBag("Inventory", 10);
            HeroJobChangeHelper.TransferEquipment(oldHero, newHero, bag, null);

            Assert.AreSame(sword, newHero.WeaponShield1);
            Assert.AreSame(armor, newHero.Armor);
            Assert.AreSame(ring, newHero.Accessory1);
        }

        [TestMethod]
        public void TransferEquipment_JobRestrictedGear_GoesToBag()
        {
            var oldHero = new Hero("Old", new Knight(), 10, BaseStats);
            var newHero = new Hero("New", new Mage(), 1, BaseStats);
            var knightSword = CreateGear("Knight Sword", ItemKind.WeaponSword, JobType.Knight);
            Assert.IsTrue(oldHero.SetEquipmentSlot(EquipmentSlot.WeaponShield1, knightSword));

            var bag = new ItemBag("Inventory", 10);
            HeroJobChangeHelper.TransferEquipment(oldHero, newHero, bag, null);

            Assert.IsNull(newHero.WeaponShield1);
            bool inBag = false;
            for (int i = 0; i < bag.Items.Count; i++)
                if (ReferenceEquals(bag.Items[i], knightSword)) { inBag = true; break; }
            Assert.IsTrue(inBag, "Unequippable item should land in the bag");
        }

        [TestMethod]
        public void TransferEquipment_BagFull_GoesToVault()
        {
            var oldHero = new Hero("Old", new Knight(), 10, BaseStats);
            var newHero = new Hero("New", new Mage(), 1, BaseStats);
            var knightSword = CreateGear("Knight Sword", ItemKind.WeaponSword, JobType.Knight);
            Assert.IsTrue(oldHero.SetEquipmentSlot(EquipmentSlot.WeaponShield1, knightSword));

            var bag = new ItemBag("Inventory", 1);
            Assert.IsTrue(bag.TryAdd(CreateGear("Filler", ItemKind.Accessory, JobType.All)));
            var vault = new SecondChanceMerchantVault();

            HeroJobChangeHelper.TransferEquipment(oldHero, newHero, bag, vault);

            Assert.IsNull(newHero.WeaponShield1);
            Assert.AreEqual(1, vault.StackCount);
        }

        [TestMethod]
        public void TransferEquipment_EmptySlots_NoOp()
        {
            var oldHero = new Hero("Old", new Knight(), 10, BaseStats);
            var newHero = new Hero("New", new Mage(), 1, BaseStats);
            var bag = new ItemBag("Inventory", 10);
            var vault = new SecondChanceMerchantVault();

            HeroJobChangeHelper.TransferEquipment(oldHero, newHero, bag, vault);

            Assert.IsNull(newHero.WeaponShield1);
            Assert.IsNull(newHero.Armor);
            Assert.IsNull(newHero.Hat);
            Assert.IsNull(newHero.WeaponShield2);
            Assert.IsNull(newHero.Accessory1);
            Assert.IsNull(newHero.Accessory2);
            Assert.AreEqual(0, vault.StackCount);
        }

        [TestMethod]
        public void TransferEquipment_NullHeroes_NoThrow()
        {
            var hero = new Hero("Solo", new Knight(), 1, BaseStats);
            HeroJobChangeHelper.TransferEquipment(null, hero, null, null);
            HeroJobChangeHelper.TransferEquipment(hero, null, null, null);
        }
    }
}
