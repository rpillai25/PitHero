using Microsoft.Xna.Framework;
using Nez;
using Nez.UI;
using RolePlayingFramework.Heroes;
using System;

namespace PitHero.UI
{
    /// <summary>Specifies the visual kind of crystal slot.</summary>
    public enum CrystalSlotKind { Inventory, Shortcut }

    /// <summary>
    /// A single clickable crystal slot element. Renders the slot background sprite (Inventory or
    /// Shortcut), and – only when a crystal is present – the HeroCrystalBase sprite followed by the
    /// tinted HeroCrystal sprite on top. Hover uses the HighlightBox sprite; selection uses SelectBox.
    /// </summary>
    public class CrystalSlotElement : Element, IInputListener
    {
        private const float SlotSize = 32f;

        private HeroCrystal _crystal;

        // Slot background (Inventory or Shortcut sprite from Items atlas)
        private SpriteDrawable _backgroundDrawable;
        // Crystal content sprites (only drawn when a crystal is present)
        private SpriteDrawable _baseDrawable;
        private SpriteDrawable _crystalDrawable;
        // Hover / selection overlay sprites from UI atlas
        private SpriteDrawable _highlightBoxDrawable;
        private SpriteDrawable _selectBoxDrawable;

        private bool _isHovered;
        private bool _isSelected;

        public CrystalSlotKind Kind;

        public event Action<CrystalSlotElement> OnSlotClicked;
        public event Action<CrystalSlotElement> OnSlotHovered;
        public event Action<CrystalSlotElement> OnSlotUnhovered;

        public HeroCrystal Crystal => _crystal;

        /// <summary>Creates a new crystal slot element for the given kind.</summary>
        public CrystalSlotElement(CrystalSlotKind kind)
        {
            Kind = kind;
            SetSize(SlotSize, SlotSize);
            SetTouchable(Touchable.Enabled);

            if (Core.Content != null)
            {
                var itemsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/Items.atlas");
                var uiAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/UI.atlas");

                // Background: "Inventory" for inventory/forge slots, "Shortcut" for queue slots
                var bgKey = kind == CrystalSlotKind.Shortcut ? "Shortcut" : "Inventory";
                var bgSprite = itemsAtlas.GetSprite(bgKey);
                if (bgSprite != null)
                    _backgroundDrawable = new SpriteDrawable(bgSprite);

                // Crystal content
                var baseSprite = itemsAtlas.GetSprite("HeroCrystalBase");
                if (baseSprite != null)
                    _baseDrawable = new SpriteDrawable(baseSprite);

                var crystalSprite = itemsAtlas.GetSprite("HeroCrystal");
                if (crystalSprite != null)
                    _crystalDrawable = new SpriteDrawable(crystalSprite);

                // Hover / selection overlays
                var highlightSprite = uiAtlas.GetSprite("HighlightBox");
                if (highlightSprite != null)
                    _highlightBoxDrawable = new SpriteDrawable(highlightSprite);

                var selectSprite = uiAtlas.GetSprite("SelectBox");
                if (selectSprite != null)
                    _selectBoxDrawable = new SpriteDrawable(selectSprite);
            }
        }

        /// <summary>Sets the crystal displayed in this slot. Pass null for an empty slot.</summary>
        public void SetCrystal(HeroCrystal crystal) => _crystal = crystal;

        /// <summary>Sets the selection highlight state.</summary>
        public void SetSelected(bool selected) => _isSelected = selected;

        // Tell Table the slot's preferred size so it allocates 32×32 per cell.
        public override float PreferredWidth => SlotSize;
        public override float PreferredHeight => SlotSize;

        /// <summary>
        /// Draw order: slot background → crystal base (if occupied) → crystal (if occupied) →
        /// hover highlight → selection highlight.
        /// </summary>
        public override void Draw(Batcher batcher, float parentAlpha)
        {
            var x = GetX();
            var y = GetY();
            var w = GetWidth();
            var h = GetHeight();

            // 1. Slot background sprite (always drawn so empty slots are visible)
            if (_backgroundDrawable != null)
                _backgroundDrawable.Draw(batcher, x, y, w, h, new Color(255, 255, 255, 100));

            // 2. Crystal content – only when a crystal occupies this slot
            if (_crystal != null)
            {
                if (_baseDrawable != null)
                    _baseDrawable.Draw(batcher, x, y, w, h, Color.White);

                if (_crystalDrawable != null)
                    _crystalDrawable.Draw(batcher, x, y, w, h, _crystal.Color);
            }

            // 3. Hover highlight (HighlightBox sprite)
            if (_isHovered && _highlightBoxDrawable != null)
                _highlightBoxDrawable.Draw(batcher, x, y, w, h, Color.White);

            // 4. Selection highlight (SelectBox sprite)
            if (_isSelected && _selectBoxDrawable != null)
                _selectBoxDrawable.Draw(batcher, x, y, w, h, Color.White);

            base.Draw(batcher, parentAlpha);
        }

        #region IInputListener

        bool IInputListener.OnLeftMousePressed(Vector2 mousePos)
        {
            OnSlotClicked?.Invoke(this);
            return true;
        }

        bool IInputListener.OnRightMousePressed(Vector2 mousePos) => false;

        void IInputListener.OnMouseEnter()
        {
            _isHovered = true;
            OnSlotHovered?.Invoke(this);
        }

        void IInputListener.OnMouseExit()
        {
            _isHovered = false;
            OnSlotUnhovered?.Invoke(this);
        }

        void IInputListener.OnMouseMoved(Vector2 mousePos) { }
        void IInputListener.OnLeftMouseUp(Vector2 mousePos) { }
        void IInputListener.OnRightMouseUp(Vector2 mousePos) { }
        bool IInputListener.OnMouseScrolled(int mouseWheelDelta) => false;

        #endregion
    }
}
