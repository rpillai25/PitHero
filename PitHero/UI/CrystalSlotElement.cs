using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nez;
using Nez.Textures;
using Nez.UI;
using RolePlayingFramework.Heroes;
using System;

namespace PitHero.UI
{
    /// <summary>Specifies the visual kind of crystal slot.</summary>
    public enum CrystalSlotKind { Inventory, Shortcut }

    /// <summary>A single clickable crystal slot element.</summary>
    public class CrystalSlotElement : Element, IInputListener
    {
        private const float SlotSize = 32f;
        private HeroCrystal _crystal;
        private Sprite _baseSprite;
        private Sprite _crystalSprite;
        private bool _isHovered;
        private bool _isSelected;
        public CrystalSlotKind Kind;

        public event Action<CrystalSlotElement> OnSlotClicked;
        public event Action<CrystalSlotElement> OnSlotHovered;
        public event Action<CrystalSlotElement> OnSlotUnhovered;

        public HeroCrystal Crystal => _crystal;

        /// <summary>Creates a new crystal slot element.</summary>
        public CrystalSlotElement(Sprite baseSprite, Sprite crystalSprite, CrystalSlotKind kind)
        {
            _baseSprite = baseSprite;
            _crystalSprite = crystalSprite;
            Kind = kind;
            SetSize(SlotSize, SlotSize);
            SetTouchable(Touchable.Enabled);
        }

        /// <summary>Sets the crystal displayed in this slot. Null = empty.</summary>
        public void SetCrystal(HeroCrystal crystal)
        {
            _crystal = crystal;
        }

        /// <summary>Sets selection highlight.</summary>
        public void SetSelected(bool selected)
        {
            _isSelected = selected;
        }

        /// <summary>Draws the slot: background, base sprite, tinted crystal sprite, selection/hover highlights.</summary>
        public override void Draw(Batcher batcher, float parentAlpha)
        {
            var x = GetX();
            var y = GetY();
            var w = GetWidth();
            var h = GetHeight();

            // Draw dark background
            var bgColor = Kind == CrystalSlotKind.Inventory ? new Color(40, 40, 40) : new Color(50, 50, 50);
            batcher.DrawRect(x, y, w, h, bgColor);

            // Draw base sprite
            if (_baseSprite != null)
            {
                batcher.Draw(_baseSprite, new Vector2(x + w / 2, y + h / 2), Color.White, 0f, _baseSprite.Origin, 1f, SpriteEffects.None, 0f);
            }

            // Draw tinted crystal sprite if present
            if (_crystal != null && _crystalSprite != null)
            {
                batcher.Draw(_crystalSprite, new Vector2(x + w / 2, y + h / 2), _crystal.Color, 0f, _crystalSprite.Origin, 1f, SpriteEffects.None, 0f);
            }

            // Draw selection highlight
            if (_isSelected)
            {
                batcher.DrawHollowRect(x, y, w, h, Color.Gold, 2f);
            }

            // Draw hover highlight
            if (_isHovered)
            {
                batcher.DrawHollowRect(x, y, w, h, new Color(180, 180, 180), 1f);
            }

            base.Draw(batcher, parentAlpha);
        }

        bool IInputListener.OnLeftMousePressed(Vector2 mousePos)
        {
            OnSlotClicked?.Invoke(this);
            return true;
        }

        bool IInputListener.OnRightMousePressed(Vector2 mousePos)
        {
            return false;
        }

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
    }
}
