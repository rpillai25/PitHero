using Microsoft.Xna.Framework;
using Nez;
using Nez.UI;
using Nez.Textures;
using RolePlayingFramework.Equipment;

namespace PitHero.UI
{
    /// <summary>UI component representing a single inventory slot.</summary>
    public class InventorySlot : Element, IInputListener
    {
        private readonly InventorySlotData _slotData;
        private readonly Sprite _backgroundSprite;
        private Sprite _selectBoxSprite;
        private Sprite _highlightBoxSprite;
        
        private SpriteDrawable _backgroundDrawable;
        private SpriteDrawable _selectBoxDrawable;
        private SpriteDrawable _highlightBoxDrawable;
        
        public event System.Action<InventorySlot> OnSlotClicked;
        public event System.Action<InventorySlot> OnSlotHovered;
        public event System.Action<InventorySlot> OnSlotUnhovered;

        public InventorySlotData SlotData => _slotData;

        public InventorySlot(InventorySlotData slotData)
        {
            _slotData = slotData;
            
            // Load atlases
            var itemsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/Items.atlas");
            var uiAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/UI.atlas");
            
            // Set up background sprite based on slot type
            var spriteKey = _slotData.SlotType switch
            {
                InventorySlotType.Inventory => "Inventory",
                InventorySlotType.Shortcut => "Shortcut",
                InventorySlotType.Equipment => "Equipment",
                _ => null
            };

            if (spriteKey != null)
            {
                _backgroundSprite = itemsAtlas.GetSprite(spriteKey);
                _backgroundDrawable = new SpriteDrawable(_backgroundSprite);
            }
            
            // Pre-load select and highlight sprites
            _selectBoxSprite = uiAtlas.GetSprite("SelectBox");
            _selectBoxDrawable = new SpriteDrawable(_selectBoxSprite);
            
            _highlightBoxSprite = uiAtlas.GetSprite("HighlightBox");
            _highlightBoxDrawable = new SpriteDrawable(_highlightBoxSprite);
            
            // Set size to 32x32 pixels
            SetSize(32f, 32f);
        }

        public override void Draw(Batcher batcher, float parentAlpha)
        {
            // Only draw if not a null slot
            if (_slotData.SlotType == InventorySlotType.Null)
                return;

            // Draw background
            if (_backgroundDrawable != null)
            {
                _backgroundDrawable.Draw(batcher, GetX(), GetY(), GetWidth(), GetHeight(), Color.White);
            }

            // Draw select box if hovered
            if (_slotData.IsHovered && _selectBoxDrawable != null)
            {
                _selectBoxDrawable.Draw(batcher, GetX(), GetY(), GetWidth(), GetHeight(), Color.White);
            }

            // Draw highlight box if highlighted
            if (_slotData.IsHighlighted && _highlightBoxDrawable != null)
            {
                _highlightBoxDrawable.Draw(batcher, GetX(), GetY(), GetWidth(), GetHeight(), Color.White);
            }

            // TODO: Draw item icon if slot has an item
            
            base.Draw(batcher, parentAlpha);
        }

        #region IInputListener Implementation

        void IInputListener.OnMouseEnter()
        {
            _slotData.IsHovered = true;
            OnSlotHovered?.Invoke(this);
        }

        void IInputListener.OnMouseExit()
        {
            _slotData.IsHovered = false;
            OnSlotUnhovered?.Invoke(this);
        }

        void IInputListener.OnMouseMoved(Vector2 mousePos)
        {
            // No specific behavior needed for mouse movement
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

        void IInputListener.OnLeftMouseUp(Vector2 mousePos)
        {
        }

        void IInputListener.OnRightMouseUp(Vector2 mousePos)
        {
        }

        bool IInputListener.OnMouseScrolled(int mouseWheelDelta)
        {
            return false;
        }

        #endregion
    }
}