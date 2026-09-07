using Microsoft.Xna.Framework;
using Nez;
using Nez.Textures;
using Nez.UI;
using PitHero.Artifacts;

namespace PitHero.UI
{
    /// <summary>
    /// One inventory-style cell showing an artifact (or nothing). Draws the shared slot background,
    /// the artifact sprite, a hover box with the artifact's name, and raises <see cref="OnClicked"/>
    /// with the artifact when a filled slot is clicked. Used by the Party Artifacts tab and the shop.
    /// </summary>
    public class ArtifactSlot : Element, IInputListener
    {
        private static readonly Color SlotBgColor = new Color(255, 255, 255, 100); // same translucency as the inventory UI

        private SpriteDrawable _background;
        private Sprite _selectBox;
        private SpriteDrawable _icon;
        private ArtifactType? _artifact;
        private string _hoverText;
        private bool _hovered;

        /// <summary>Fired when the player left-clicks a filled slot.</summary>
        public event System.Action<ArtifactType> OnClicked;

        /// <summary>The artifact shown, or null for an empty slot.</summary>
        public ArtifactType? Artifact => _artifact;

        /// <summary>Creates an empty slot of the configured size.</summary>
        public ArtifactSlot()
        {
            SetTouchable(Touchable.Enabled);
            SetSize(GameConfig.ArtifactSlotSize, GameConfig.ArtifactSlotSize);

            if (Core.Content != null)
            {
                var itemsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/Items.atlas");
                var bgSprite = itemsAtlas?.GetSprite("Inventory");
                if (bgSprite != null)
                    _background = new SpriteDrawable(bgSprite);
                var uiAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/UI.atlas");
                _selectBox = uiAtlas?.GetSprite("SelectBox");
            }
        }

        /// <summary>Shows <paramref name="artifact"/> (null clears the slot). <paramref name="hoverText"/> is the name shown on hover.</summary>
        public void SetArtifact(ArtifactType? artifact, string hoverText)
        {
            _artifact = artifact;
            _hoverText = hoverText;
            _icon = null;
            if (artifact.HasValue && Core.Content != null)
            {
                var itemsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/Items.atlas");
                var sprite = itemsAtlas?.GetSprite(ArtifactCatalog.GetSpriteName(artifact.Value));
                if (sprite != null)
                    _icon = new SpriteDrawable(sprite);
            }
        }

        public override void Draw(Batcher batcher, float parentAlpha)
        {
            _background?.Draw(batcher, GetX(), GetY(), GetWidth(), GetHeight(), SlotBgColor);
            _icon?.Draw(batcher, GetX(), GetY(), GetWidth(), GetHeight(), Color.White);
            if (_hovered && _artifact.HasValue && _selectBox != null)
                new SpriteDrawable(_selectBox).Draw(batcher, GetX(), GetY(), GetWidth(), GetHeight(), Color.White);
        }

        void IInputListener.OnMouseEnter()
        {
            _hovered = true;
            if (_artifact.HasValue && !string.IsNullOrEmpty(_hoverText))
            {
                var stage = GetStage();
                if (stage != null)
                {
                    var mp = stage.GetMousePosition();
                    HoverTextManager.ShowHoverText(_hoverText, mp.X + 12f, mp.Y - 4f);
                }
            }
        }

        void IInputListener.OnMouseExit()
        {
            _hovered = false;
            HoverTextManager.HideHoverText();
        }

        void IInputListener.OnMouseMoved(Vector2 mousePos) { }

        bool IInputListener.OnLeftMousePressed(Vector2 mousePos) => _artifact.HasValue;

        void IInputListener.OnLeftMouseUp(Vector2 mousePos)
        {
            if (_artifact.HasValue)
            {
                HoverTextManager.HideHoverText();
                OnClicked?.Invoke(_artifact.Value);
            }
        }

        bool IInputListener.OnRightMousePressed(Vector2 mousePos) => false;

        void IInputListener.OnRightMouseUp(Vector2 mousePos) { }

        bool IInputListener.OnMouseScrolled(int mouseWheelDelta) => false;
    }
}
