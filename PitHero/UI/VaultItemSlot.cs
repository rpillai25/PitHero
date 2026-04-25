using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nez;
using Nez.BitmapFonts;
using Nez.Textures;
using Nez.UI;
using PitHero.Services;
using RolePlayingFramework.Equipment;

namespace PitHero.UI
{
    /// <summary>A single slot in the Second Chance vault item grid. Displays the item sprite and quantity badge.</summary>
    public class VaultItemSlot : Element, IInputListener
    {
        private const float SlotSize = 32f;

        private SecondChanceMerchantVault.StackedItem _stack;

        private SpriteDrawable _backgroundDrawable;
        private SpriteDrawable _itemDrawable;
        private SpriteDrawable _highlightDrawable;
        private BitmapFont _font;

        private bool _isHovered;
        private bool _mouseDown;
        private Vector2 _mousePressPos;
        private bool _isDragging;
        private bool _itemSpriteHidden;

        public event System.Action<VaultItemSlot> OnSlotHovered;
        public event System.Action<VaultItemSlot> OnSlotUnhovered;
        public event System.Action<VaultItemSlot, Vector2> OnDragStarted;
        public event System.Action<VaultItemSlot, Vector2> OnDragMoved;
        public event System.Action<VaultItemSlot, Vector2> OnDragDropped;

        /// <summary>Gets the vault stack displayed in this slot.</summary>
        public SecondChanceMerchantVault.StackedItem Stack => _stack;

        /// <summary>Creates a new vault item slot.</summary>
        public VaultItemSlot()
        {
            SetSize(SlotSize, SlotSize);
            SetTouchable(Touchable.Enabled);

            if (Core.Content != null)
            {
                var itemsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/Items.atlas");
                var uiAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/UI.atlas");

                var bgSprite = itemsAtlas.GetSprite("Inventory");
                if (bgSprite != null)
                    _backgroundDrawable = new SpriteDrawable(bgSprite);

                var hlSprite = uiAtlas.GetSprite("HighlightBox");
                if (hlSprite != null)
                    _highlightDrawable = new SpriteDrawable(hlSprite);
            }

            if (Core.Content != null)
            {
                try { _font = Core.Content.LoadBitmapFont(GameConfig.FontPathHudSmall); }
                catch { if (Graphics.Instance != null) _font = Graphics.Instance.BitmapFont; }
            }
        }

        /// <summary>Sets the vault stack to display in this slot. Pass null for empty.</summary>
        public void SetStack(SecondChanceMerchantVault.StackedItem stack)
        {
            _stack = stack;
            _itemSpriteHidden = false;
            _itemDrawable = null;
            if (_stack?.ItemTemplate != null && Core.Content != null)
            {
                try
                {
                    var itemsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/Items.atlas");
                    var sprite = itemsAtlas.GetSprite(_stack.ItemTemplate.SpriteName);
                    if (sprite != null)
                        _itemDrawable = new SpriteDrawable(sprite);
                }
                catch { }
            }
        }

        /// <summary>Hides the item sprite (e.g. during drag).</summary>
        public void SetItemSpriteHidden(bool hidden) => _itemSpriteHidden = hidden;

        public override float PreferredWidth => SlotSize;
        public override float PreferredHeight => SlotSize;

        /// <summary>Draws the slot background, item sprite, quantity badge, and hover highlight.</summary>
        public override void Draw(Batcher batcher, float parentAlpha)
        {
            float x = GetX();
            float y = GetY();
            float w = GetWidth();
            float h = GetHeight();
            var color = Color.White * parentAlpha;

            // Draw background at the same translucent alpha used by InventorySlot so the
            // parchment window shows through (opaque = black slots; alpha 100 ≈ 39% opacity)
            _backgroundDrawable?.Draw(batcher, x, y, w, h, new Color(255, 255, 255, 100));

            if (_stack != null && !_itemSpriteHidden)
            {
                _itemDrawable?.Draw(batcher, x, y, w, h, color);

                // Draw quantity badge if > 1
                if (_stack.Quantity > 1 && _font != null)
                {
                    var qty = _stack.Quantity.ToString();
                    batcher.DrawString(_font, qty, new Vector2(x + 2, y + h - _font.LineHeight), Color.White);
                }
            }

            if (_isHovered)
                _highlightDrawable?.Draw(batcher, x, y, w, h, color);
        }

        #region IInputListener

        void IInputListener.OnMouseEnter()
        {
            _isHovered = true;
            if (_stack != null)
                OnSlotHovered?.Invoke(this);
        }

        void IInputListener.OnMouseExit()
        {
            _isHovered = false;
            OnSlotUnhovered?.Invoke(this);
        }

        void IInputListener.OnMouseMoved(Vector2 mousePos)
        {
            if (!_mouseDown || _stack == null) return;

            if (!_isDragging)
            {
                float dx = mousePos.X - _mousePressPos.X;
                float dy = mousePos.Y - _mousePressPos.Y;
                float distSq = dx * dx + dy * dy;
                float threshold = GameConfig.DragThresholdPixels;
                if (distSq >= threshold * threshold)
                {
                    _isDragging = true;
                    OnDragStarted?.Invoke(this, mousePos);
                }
            }

            if (_isDragging)
                OnDragMoved?.Invoke(this, mousePos);
        }

        bool IInputListener.OnLeftMousePressed(Vector2 mousePos)
        {
            _mouseDown = true;
            _mousePressPos = mousePos;
            _isDragging = false;
            return true;
        }

        bool IInputListener.OnRightMousePressed(Vector2 mousePos)
        {
            return false;
        }

        void IInputListener.OnLeftMouseUp(Vector2 mousePos)
        {
            bool wasDragging = _isDragging;
            _mouseDown = false;
            _isDragging = false;

            if (wasDragging)
                OnDragDropped?.Invoke(this, mousePos);
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
