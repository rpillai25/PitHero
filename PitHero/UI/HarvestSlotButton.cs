using Microsoft.Xna.Framework;
using Nez;
using Nez.Textures;
using Nez.UI;
using PitHero.Farming;

namespace PitHero.UI
{
    /// <summary>
    /// One harvested-crop stack cell in a crop inventory grid (crop storage viewer, refrigerator).
    /// Draws the inventory-slot background, the crop sprite, a hover select box, and the stack
    /// count; fires <see cref="OnClicked"/> on left-mouse-up. Empty cells (null sprite) render the
    /// background only and are untouchable.
    /// </summary>
    public class HarvestSlotButton : Element, IInputListener
    {
        /// <summary>Standard cell size used by crop inventory grids.</summary>
        public const float DefaultSlotSize = 40f;

        // Inventory-slot background drawn at the same translucency as the inventory UI.
        private static readonly Color SlotBgColor = new Color(255, 255, 255, 100);

        private readonly SpriteDrawable _draw;
        private readonly int _count;
        private readonly string _tooltipText;
        private SpriteDrawable _background;
        private Sprite _selectBox;
        private bool _hovered;

        public event System.Action OnClicked;

        public HarvestSlotButton(Sprite sprite, CropType crop, int count, string tooltipText)
        {
            _draw        = sprite != null ? new SpriteDrawable(sprite) : null;
            _count       = count;
            _tooltipText = tooltipText;
            // Empty slots show the background only — no hover/click.
            SetTouchable(sprite != null ? Touchable.Enabled : Touchable.Disabled);
            SetSize(DefaultSlotSize, DefaultSlotSize);

            if (Core.Content != null)
            {
                var itemsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/Items.atlas");
                var bgSprite   = itemsAtlas?.GetSprite("Inventory");
                if (bgSprite != null)
                    _background = new SpriteDrawable(bgSprite);

                var uiAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/UI.atlas");
                _selectBox  = uiAtlas?.GetSprite("SelectBox");
            }
        }

        public override void Draw(Batcher batcher, float parentAlpha)
        {
            _background?.Draw(batcher, GetX(), GetY(), GetWidth(), GetHeight(), SlotBgColor);

            _draw?.Draw(batcher, GetX(), GetY(), GetWidth(), GetHeight(), Color.White);

            if (_hovered && _selectBox != null)
                new SpriteDrawable(_selectBox).Draw(
                    batcher, GetX(), GetY(), GetWidth(), GetHeight(), Color.White);

            var font = Nez.Graphics.Instance?.BitmapFont;
            if (font != null && _count > 1)
            {
                string countStr = _count.ToString();
                float tw = font.MeasureString(countStr).X;
                StackCountText.Draw(batcher, font, countStr,
                    new Vector2(GetX() + GetWidth() - tw - 2f, GetY() + GetHeight() - font.LineHeight - 1f),
                    Color.White);
            }
        }

        void IInputListener.OnMouseEnter()
        {
            _hovered = true;
            if (!string.IsNullOrEmpty(_tooltipText))
            {
                var stage = GetStage();
                var mp = stage != null ? stage.GetMousePosition() : new Vector2(GetX(), GetY());
                HoverTextManager.ShowHoverText(_tooltipText, mp.X + 12f, mp.Y - 4f);
            }
        }

        void IInputListener.OnMouseExit()
        {
            _hovered = false;
            HoverTextManager.HideHoverText();
        }

        void IInputListener.OnMouseMoved(Vector2 mousePos) { }
        bool IInputListener.OnLeftMousePressed(Vector2 mousePos) => true;
        void IInputListener.OnLeftMouseUp(Vector2 mousePos) => OnClicked?.Invoke();
        bool IInputListener.OnRightMousePressed(Vector2 mousePos) => false;
        void IInputListener.OnRightMouseUp(Vector2 mousePos) { }
        bool IInputListener.OnMouseScrolled(int mouseWheelDelta) => false;
    }
}
