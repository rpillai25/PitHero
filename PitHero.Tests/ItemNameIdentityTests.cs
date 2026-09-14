using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Services;
using RolePlayingFramework.Equipment;

namespace PitHero.Tests
{
    /// <summary>
    /// Issue #413: IItem.Name is the stable identity (stripped localization key + tier suffix) used
    /// by the registry, saves and replay hashes; DisplayName is the localized, spaced text. Headless
    /// the two agree, and the identity must never depend on Inventory.txt.
    /// </summary>
    [TestClass]
    public class ItemNameIdentityTests
    {
        [TestMethod]
        public void ItemNameKey_Strip_RemovesAffixesOnly()
        {
            Assert.AreEqual("RustyBlade", ItemNameKey.Strip("Inv_RustyBlade_Name"));
            Assert.AreEqual("HPPotion", ItemNameKey.Strip("Inv_HPPotion_Name"));
            Assert.AreEqual("WeakSword", ItemNameKey.Strip("WeakSword"), "Non-key strings pass through");
            Assert.AreEqual("Inv__Name", ItemNameKey.Strip("Inv__Name"), "An empty middle is not a key");
            Assert.IsNull(ItemNameKey.Strip(null));
        }

        [TestMethod]
        public void Gear_Name_IsStrippedKeyPlusTier()
        {
            var blade = GearItems.RustyBlade();
            Assert.AreEqual("RustyBlade", blade.Name);
            Assert.AreEqual("RustyBlade", blade.SpriteName, "Sprite name and identity share the stripped key");
            Assert.AreEqual("RustyBlade", blade.DisplayName, "Headless: no TextService, display falls back to the identity");

            var tier2 = Gear.CreateTierScaledCopy(blade, 2, 25);
            Assert.AreEqual("RustyBlade+2", tier2.Name);
            Assert.AreEqual("RustyBlade", tier2.SpriteName, "Tiered gear keeps the base sprite");
            Assert.AreEqual("RustyBlade+2", tier2.DisplayName);
        }

        [TestMethod]
        public void Consumable_Name_IsStrippedKey()
        {
            var potion = PotionItems.MidHPPotion();
            Assert.AreEqual("MidHPPotion", potion.Name);
            Assert.AreEqual("MidHPPotion", potion.DisplayName);
        }

        [TestMethod]
        public void ItemRegistry_RoundTripsIdentityNames()
        {
            Assert.IsTrue(ItemRegistry.TryCreateItem("RustyBlade", out var item));
            Assert.AreEqual("RustyBlade", item.Name);
            Assert.IsTrue(ItemRegistry.TryCreateItem("RustyBlade+3", out var tiered));
            Assert.AreEqual("RustyBlade+3", tiered.Name);
            Assert.IsTrue(ItemRegistry.IsKnownItemName("HPPotion"));
            Assert.IsFalse(ItemRegistry.IsKnownItemName("Inv_HPPotion_Name"), "Raw keys are no longer identities");
        }

        [TestMethod]
        public void ItemRegistry_TryResolveDisplayName_HandlesTierSuffix()
        {
            // Headless the display map is the identity map, so identity strings resolve to themselves
            Assert.IsTrue(ItemRegistry.TryResolveDisplayName("RustyBlade", out var name));
            Assert.AreEqual("RustyBlade", name);
            Assert.IsTrue(ItemRegistry.TryResolveDisplayName("RustyBlade+4", out var tiered));
            Assert.AreEqual("RustyBlade+4", tiered);
            Assert.IsFalse(ItemRegistry.TryResolveDisplayName("Not An Item", out _));
            Assert.IsFalse(ItemRegistry.TryResolveDisplayName("", out _));
        }

        [TestMethod]
        public void ConsoleSegment_Build_TagsItemsByDisplayName()
        {
            var segments = ConsoleSegment.Build("Found {0}!", ("RustyBlade+2", Microsoft.Xna.Framework.Color.White));
            Assert.AreEqual(3, segments.Length);
            Assert.AreEqual("RustyBlade+2", segments[1].ItemName, "The segment carries the identity name for the tooltip lookup");
        }
    }
}
