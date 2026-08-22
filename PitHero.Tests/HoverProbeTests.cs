using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using Nez.UI;
using PitHero.UI;

namespace PitHero.Tests
{
    /// <summary>
    /// Covers the guard behind our periodic geometric hover polls. The bug it exists for: a tooltip
    /// raised by a bounds-only poll over an element that is no longer on screen can never be taken
    /// back, because such an element receives no mouse events at all — including the OnMouseExit the
    /// hide path depends on.
    /// </summary>
    [TestClass]
    public class HoverProbeTests
    {
        private Stage _stage;
        private Group _panel;
        private Element _slot;

        [TestInitialize]
        public void TestInitialize()
        {
            _stage = new Stage();

            _panel = new Group();
            _panel.SetBounds(100f, 50f, 200f, 200f);
            _stage.AddElement(_panel);

            _slot = new Element();
            _slot.SetBounds(10f, 10f, 32f, 32f);
            _panel.AddElement(_slot);
        }

        /// <summary>A point inside the slot in stage space.</summary>
        private static Vector2 SlotCenter => new Vector2(126f, 76f);

        [TestMethod]
        public void IsLive_AttachedAndVisible_ReturnsTrue()
        {
            Assert.IsTrue(HoverProbe.IsLive(_slot, _stage));
        }

        [TestMethod]
        public void IsLive_DetachedElement_ReturnsFalse_ThoughItsBoundsStillMatch()
        {
            // TabPane.SetActiveTab clears the tab table, which is exactly this: the element loses its
            // parent but keeps its stage reference and its laid-out coordinates.
            _panel.RemoveElement(_slot);

            // The trap this guard exists for: a detached element still answers coordinate queries, so
            // a bounds test keeps reporting a hit at a spot nothing is drawn at any more.
            var topLeft = _slot.LocalToStageCoordinates(Vector2.Zero);
            Assert.AreEqual(10f, topLeft.X, "a detached element still reports coordinates, just from a broken chain");

            Assert.IsFalse(HoverProbe.IsLive(_slot, _stage));
            Assert.IsFalse(HoverProbe.IsTopmostAt(_slot, _stage, SlotCenter));
        }

        [TestMethod]
        public void IsLive_HiddenAncestor_ReturnsFalse()
        {
            _panel.SetVisible(false);

            Assert.IsFalse(HoverProbe.IsLive(_slot, _stage));
            Assert.IsFalse(HoverProbe.IsTopmostAt(_slot, _stage, SlotCenter));
        }

        [TestMethod]
        public void IsTopmostAt_CursorOverSlot_ReturnsTrue()
        {
            Assert.IsTrue(HoverProbe.IsTopmostAt(_slot, _stage, SlotCenter));
        }

        [TestMethod]
        public void IsTopmostAt_CursorOutsideSlot_ReturnsFalse()
        {
            Assert.IsFalse(HoverProbe.IsTopmostAt(_slot, _stage, new Vector2(500f, 500f)));
        }

        [TestMethod]
        public void IsTopmostAt_HitLandsOnChild_StillCreditsTheSlot()
        {
            var icon = new Element();
            icon.SetBounds(0f, 0f, 32f, 32f);
            var slotGroup = new Group();
            slotGroup.SetBounds(60f, 10f, 32f, 32f);
            slotGroup.AddElement(icon);
            _panel.AddElement(slotGroup);

            Assert.IsTrue(HoverProbe.IsTopmostAt(slotGroup, _stage, new Vector2(176f, 76f)));
        }

        [TestMethod]
        public void IsTopmostAt_SlotCoveredByDialog_ReturnsFalse()
        {
            // A stage-centered dialog added after the panel draws — and hit-tests — on top of it.
            var dialog = new Element();
            dialog.SetBounds(0f, 0f, 400f, 400f);
            _stage.AddElement(dialog);

            Assert.IsTrue(HoverProbe.IsLive(_slot, _stage), "the slot is still on screen, just buried");
            Assert.IsFalse(HoverProbe.IsTopmostAt(_slot, _stage, SlotCenter));
        }

        [TestMethod]
        public void IsTopmostAt_NonTouchableOverlay_DoesNotHideTheSlot()
        {
            // Tooltips trail the cursor with hit-testing off precisely so they cannot mask what they
            // describe; an overlay like that must not read as covering the slot.
            var tooltip = new Element();
            tooltip.SetBounds(0f, 0f, 400f, 400f);
            tooltip.SetTouchable(Touchable.Disabled);
            _stage.AddElement(tooltip);

            Assert.IsTrue(HoverProbe.IsTopmostAt(_slot, _stage, SlotCenter));
        }

        [TestMethod]
        public void IsLive_NullArguments_ReturnFalse()
        {
            Assert.IsFalse(HoverProbe.IsLive(null, _stage));
            Assert.IsFalse(HoverProbe.IsLive(_slot, null));
        }
    }
}
