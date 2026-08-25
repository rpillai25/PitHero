using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Services;
using RolePlayingFramework.Equipment;

namespace PitHero.Tests
{
    /// <summary>
    /// A stack of consumables is sold through the quantity dialog, which pays for the chosen count and
    /// leaves the rest in the bag. The dialog's running total is unitPrice * quantity, so the payout
    /// must match that exactly, and the split must not couple the sold units to the ones kept.
    /// </summary>
    [TestClass]
    public class StackSellPricingTests
    {
        private sealed class TestPotion : Consumable
        {
            private readonly int _hp;

            public TestPotion(string name, int price, int hp)
                : base(name, ItemRarity.Normal, "desc", price, hp)
            {
                _hp = hp;
            }

            public override Consumable CreateFreshInstance() => new TestPotion(Name, Price, _hp);
        }

        [TestMethod]
        public void SellingAStack_PaysUnitPriceTimesStackCount()
        {
            var potion = new TestPotion("MidHPPotion", price: 100, hp: 50);
            potion.StackCount = 16;

            int expected = potion.GetSellPrice() * 16;
            int paid = ItemSellHelper.SellItemDirect(potion, "test");

            Assert.IsTrue(expected > 0, "Sanity: the potion should be worth something");
            Assert.AreEqual(expected, paid,
                "The card shows GetSellPrice() * StackCount — the payout must match it exactly");
        }

        [TestMethod]
        public void SellingASingleUnit_PaysTheUnitPrice()
        {
            var potion = new TestPotion("HPPotion", price: 40, hp: 20);
            potion.StackCount = 1;

            Assert.AreEqual(potion.GetSellPrice(), ItemSellHelper.SellItemDirect(potion, "test"));
        }

        [TestMethod]
        public void SellingPartOfAStack_PaysForTheChosenCountAndKeepsTheRest()
        {
            var bag = new RolePlayingFramework.Inventory.ItemBag(capacity: 10);
            var potion = new TestPotion("MidHPPotion", price: 100, hp: 50);
            potion.StackCount = 16;
            bag.SetSlotItem(0, potion);

            int paid = ItemSellHelper.SellBagItemQuantity(bag, 0, 5, "test");

            Assert.AreEqual(potion.GetSellPrice() * 5, paid,
                "Payout must equal the dialog's running total: unit price times the chosen quantity");
            Assert.AreEqual(11, ((Consumable)bag.GetSlotItem(0)).StackCount,
                "The unsold remainder must stay in the bag");
        }

        [TestMethod]
        public void SellingPartOfAStack_DoesNotShareTheStackObject()
        {
            // The sold units go to the vault as their own instance. A shared reference would make the
            // vault stack and the bag stack move together on any later change.
            var bag = new RolePlayingFramework.Inventory.ItemBag(capacity: 10);
            var potion = new TestPotion("MidHPPotion", price: 100, hp: 50);
            potion.StackCount = 16;
            bag.SetSlotItem(0, potion);

            ItemSellHelper.SellBagItemQuantity(bag, 0, 6, "test");
            var kept = (Consumable)bag.GetSlotItem(0);

            Assert.AreEqual(10, kept.StackCount);
            Assert.AreSame(potion, kept, "The bag keeps the original instance, decremented in place");
        }

        [TestMethod]
        public void SellingTheWholeStackByQuantity_EmptiesTheSlot()
        {
            var bag = new RolePlayingFramework.Inventory.ItemBag(capacity: 10);
            var potion = new TestPotion("MidHPPotion", price: 100, hp: 50);
            potion.StackCount = 4;
            bag.SetSlotItem(0, potion);

            int paid = ItemSellHelper.SellBagItemQuantity(bag, 0, 4, "test");

            Assert.AreEqual(potion.GetSellPrice() * 4, paid);
            Assert.IsNull(bag.GetSlotItem(0), "Selling the whole stack must clear the slot");
        }

        [TestMethod]
        public void SellingMoreThanTheStackHolds_SellsOnlyWhatIsThere()
        {
            var bag = new RolePlayingFramework.Inventory.ItemBag(capacity: 10);
            var potion = new TestPotion("MidHPPotion", price: 100, hp: 50);
            potion.StackCount = 3;
            bag.SetSlotItem(0, potion);

            // int.MaxValue is what the single-action card passes for "the whole stack".
            int paid = ItemSellHelper.SellBagItemQuantity(bag, 0, int.MaxValue, "test");

            Assert.AreEqual(potion.GetSellPrice() * 3, paid, "Must never pay for units that do not exist");
            Assert.IsNull(bag.GetSlotItem(0));
        }

        [TestMethod]
        public void SellingZeroOrFewer_DoesNothing()
        {
            var bag = new RolePlayingFramework.Inventory.ItemBag(capacity: 10);
            var potion = new TestPotion("MidHPPotion", price: 100, hp: 50);
            potion.StackCount = 8;
            bag.SetSlotItem(0, potion);

            Assert.AreEqual(0, ItemSellHelper.SellBagItemQuantity(bag, 0, 0, "test"));
            Assert.AreEqual(0, ItemSellHelper.SellBagItemQuantity(bag, 0, -4, "test"));
            Assert.AreEqual(8, ((Consumable)bag.GetSlotItem(0)).StackCount, "The stack must be untouched");
        }

        [TestMethod]
        public void SellingGear_IgnoresStackingAndPaysOnce()
        {
            var sword = new Gear("TestSword", ItemKind.WeaponSword, ItemRarity.Normal, "desc", 200,
                                 new RolePlayingFramework.Stats.StatBlock(0, 0, 0, 0), atk: 5);

            Assert.AreEqual(sword.GetSellPrice(), ItemSellHelper.SellItemDirect(sword, "test"),
                "Non-consumables are never stacked, so a quantity selector must never apply to them");
        }
    }
}
