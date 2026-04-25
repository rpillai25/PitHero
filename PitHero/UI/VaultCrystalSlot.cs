using Microsoft.Xna.Framework;
using Nez;
using Nez.UI;
using RolePlayingFramework.Heroes;

namespace PitHero.UI
{
    /// <summary>A single slot in the Second Chance vault crystal grid. Displays a vault crystal and supports drag-and-drop purchase.</summary>
    public class VaultCrystalSlot : Element, IInputListener
    {
        private const float SlotSize = 32f;

        private HeroCrystal _crystal;

        private SpriteDrawable _backgroundDrawable;
        private SpriteDrawable _baseDrawable;
        private SpriteDrawable _crystalDrawable;
        private SpriteDrawable _masterStarDrawable;
        private SpriteDrawable _selectBoxDrawable;

        private bool _isHovered;
        private bool _crystalHidden;
        private bool _mouseDown;
        private Vector2 _mousePressPos;
        private bool _isDragging;

        public event System.Action<VaultCrystalSlot> OnSlotClicked;
        public event System.Action<VaultCrystalSlot> OnSlotHovered;
        public event System.Action<VaultCrystalSlot> OnSlotUnhovered;
        public event System.Action<VaultCrystalSlot, Vector2> OnDragStarted;
        public event System.Action<VaultCrystalSlot, Vector2> OnDragMoved;
        public event System.Action<VaultCrystalSlot, Vector2> OnDragDropped;

        /// <summary>Gets the crystal displayed in this slot.</summary>
        public HeroCrystal Crystal => _crystal;

        /// <summary>Creates a new vault crystal slot.</summary>
        public VaultCrystalSlot()
        {
            SetSize(SlotSize, SlotSize);
            SetTouchable(Touchable.Enabled);

            if (Core.Content != null)
            {
                var itemsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/Items.atlas");
                var uiAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/UI.atlas");

                var bgSprite = itemsAtlas.GetSprite("Inventory");
                if (bgSprite != null) _backgroundDrawable = new SpriteDrawable(bgSprite);

                var baseSprite = itemsAtlas.GetSprite("HeroCrystalBase");
                if (baseSprite != null) _baseDrawable = new SpriteDrawable(baseSprite);

                var crystalSprite = itemsAtlas.GetSprite("HeroCrystal");
                if (crystalSprite != null) _crystalDrawable = new SpriteDrawable(crystalSprite);

                var masterStarSprite = itemsAtlas.GetSprite("CrystalMasterStar");
                if (masterStarSprite != null) _masterStarDrawable = new SpriteDrawable(masterStarSprite);

                var selSprite = uiAtlas.GetSprite("SelectBox");
                if (selSprite != null) _selectBoxDrawable = new SpriteDrawable(selSprite);
            }
        }

        /// <summary>Sets the vault crystal to display. Pass null for empty.</summary>
        public void SetCrystal(HeroCrystal crystal)
        {
            _crystal = crystal;
            _crystalHidden = false;
        }

        /// <summary>Hides the crystal sprite (e.g., during drag).</summary>
        public void SetCrystalHidden(bool hidden) => _crystalHidden = hidden;

        /// <summary>Manually sets hover state (used when a dismiss layer intercepts mouse events).</summary>
        public void SetHovered(bool hovered)
        {
            if (_isHovered == hovered) return;
            _isHovered = hovered;
            if (hovered) OnSlotHovered?.Invoke(this);
            else OnSlotUnhovered?.Invoke(this);
        }

        public override float PreferredWidth => SlotSize;
        public override float PreferredHeight => SlotSize;

        /// <summary>Draws slot background, crystal, and hover highlight.</summary>
        public override void Draw(Batcher batcher, float parentAlpha)
        {
            float x = GetX();
            float y = GetY();
            float w = GetWidth();
            float h = GetHeight();

            if (_backgroundDrawable != null)
                _backgroundDrawable.Draw(batcher, x, y, w, h, new Color(255, 255, 255, 100));

            if (_crystal != null && !_crystalHidden)
            {
                _baseDrawable?.Draw(batcher, x, y, w, h, Color.White);
                _crystalDrawable?.Draw(batcher, x, y, w, h, _crystal.Color);
                if (_crystal.Mastered && _masterStarDrawable != null)
                    _masterStarDrawable.Draw(batcher, x + w - 12f, y, 12f, 12f, Color.White);
            }

            if (_isHovered && _selectBoxDrawable != null)
                _selectBoxDrawable.Draw(batcher, x, y, w, h, Color.White);

            base.Draw(batcher, parentAlpha);
        }

        #region IInputListener

        bool IInputListener.OnLeftMousePressed(Vector2 mousePos)
        {
            _mouseDown = true;
            _mousePressPos = mousePos;
            _isDragging = false;
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

        void IInputListener.OnMouseMoved(Vector2 mousePos)
        {
            if (!_mouseDown) return;
            if (!_isDragging)
            {
                float threshold = GameConfig.DragThresholdPixels;
                if (Vector2.DistanceSquared(mousePos, _mousePressPos) >= threshold * threshold)
                {
                    _isDragging = true;
                    OnDragStarted?.Invoke(this, mousePos);
                }
            }
            if (_isDragging)
                OnDragMoved?.Invoke(this, mousePos);
        }

        void IInputListener.OnLeftMouseUp(Vector2 mousePos)
        {
            bool wasDragging = _isDragging;
            _mouseDown = false;
            _isDragging = false;
            if (wasDragging)
            {
                OnDragDropped?.Invoke(this, mousePos);
                return;
            }
            OnSlotClicked?.Invoke(this);
        }

        void IInputListener.OnRightMouseUp(Vector2 mousePos) { }
        bool IInputListener.OnMouseScrolled(int mouseWheelDelta) => false;

        #endregion
    }
}
