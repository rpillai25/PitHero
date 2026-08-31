using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using PitHero.Services;
using RolePlayingFramework.Equipment;

namespace PitHero.Tests
{
    /// <summary>
    /// Tests for issue #404: ConsoleSegment item-name auto-tagging (console hover tooltips)
    /// and ItemRegistry.IsKnownItemName rules. Registry keys are raw localization keys
    /// (e.g. "Inv_RustyBlade_Name") in headless test hosts.
    /// </summary>
    [TestClass]
    public class EventConsoleItemTagTests
    {
        private const string KnownItemName = "Inv_RustyBlade_Name";

        // ── ConsoleSegment.Build auto-tagging ────────────────────────────────────────

        [TestMethod]
        public void Build_TagsRegisteredItemNameArg()
        {
            var segments = ConsoleSegment.Build("{0} found {1}.",
                ("Hero", GameConfig.ConsoleColorHeroName),
                (KnownItemName, GameConfig.RARITY_NORMAL));

            Assert.AreEqual(4, segments.Length, "hero, literal, item, literal");
            Assert.IsNull(segments[0].ItemName, "Hero name arg must not be tagged as an item");
            Assert.IsNull(segments[1].ItemName, "Literal text must not be tagged");
            Assert.AreEqual(KnownItemName, segments[2].Text);
            Assert.AreEqual(KnownItemName, segments[2].ItemName, "Registered item arg must carry its name");
            Assert.IsNull(segments[3].ItemName, "Trailing literal must not be tagged");
        }

        [TestMethod]
        public void Build_TagsTierScaledName()
        {
            string tieredName = KnownItemName + "+2";
            var segments = ConsoleSegment.Build("{0} found {1}.",
                ("Hero", GameConfig.ConsoleColorHeroName),
                (tieredName, GameConfig.RARITY_NORMAL));

            Assert.AreEqual(tieredName, segments[2].ItemName, "Tier-scaled '+N' name must be tagged");
            Assert.IsTrue(ItemRegistry.TryCreateItem(tieredName, out var item),
                "Tagged tier-scaled name must resolve to an item for the tooltip");
            Assert.IsNotNull(item);
        }

        [TestMethod]
        public void Build_DoesNotTagUnknownArg()
        {
            var segments = ConsoleSegment.Build("{0} defeated {1}!",
                ("Hero", GameConfig.ConsoleColorHeroName),
                ("Ancient Wyrm", GameConfig.ConsoleColorEnemyName));

            for (int i = 0; i < segments.Length; i++)
                Assert.IsNull(segments[i].ItemName, $"Segment {i} ('{segments[i].Text}') must not be tagged");
        }

        // ── ItemRegistry.IsKnownItemName ─────────────────────────────────────────────

        [TestMethod]
        public void IsKnownItemName_RegisteredBaseName_True()
        {
            Assert.IsTrue(ItemRegistry.IsKnownItemName(KnownItemName));
        }

        [TestMethod]
        public void IsKnownItemName_TierScaledName_True()
        {
            Assert.IsTrue(ItemRegistry.IsKnownItemName(KnownItemName + "+2"));
            Assert.IsTrue(ItemRegistry.IsKnownItemName(KnownItemName + "+7"));
        }

        [TestMethod]
        public void IsKnownItemName_Rejections()
        {
            Assert.IsFalse(ItemRegistry.IsKnownItemName(null));
            Assert.IsFalse(ItemRegistry.IsKnownItemName(string.Empty));
            Assert.IsFalse(ItemRegistry.IsKnownItemName("NonExistentSword"));
            Assert.IsFalse(ItemRegistry.IsKnownItemName("NonExistentSword+2"));
            Assert.IsFalse(ItemRegistry.IsKnownItemName(KnownItemName + "+1"),
                "Tier must be > 1, matching TryCreateItem's rule");
        }

        // ── ConsoleSegment back-compat ───────────────────────────────────────────────

        [TestMethod]
        public void ConsoleSegment_TwoArgCtor_NullItemName()
        {
            var segment = new ConsoleSegment("plain text", Color.White);
            Assert.IsNull(segment.ItemName);
        }
    }
}
