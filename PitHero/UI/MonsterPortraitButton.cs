using Microsoft.Xna.Framework;
using Nez;
using Nez.Textures;
using Nez.UI;

namespace PitHero.UI
{
    /// <summary>
    /// A monster's roster portrait (issue #413): draws the sprite scaled to fit its cell, a 1px white
    /// outline while hovered to signal it is clickable, a hover caption, and fires
    /// <see cref="OnClicked"/> on left-mouse-up (the roster opens the job-levels card).
    /// </summary>
    public class MonsterPortraitButton : Element, IInputListener
    {
        private const float OutlineThickness = 1f;

        private readonly Sprite _sprite;
        private readonly string _hoverText;
        private bool _hovered;

        /// <summary>Fired when the portrait is left-clicked.</summary>
        public event System.Action OnClicked;

        public MonsterPortraitButton(Sprite sprite, string hoverText, float size)
        {
            _sprite = sprite;
            _hoverText = hoverText;
            SetTouchable(Touchable.Enabled);
            SetSize(size, size);
        }

        public override void Draw(Batcher batcher, float parentAlpha)
        {
            if (_sprite != null)
            {
                // Fit inside the cell, preserving aspect (the same result as Image + Scaling.Fit)
                float srcW = _sprite.SourceRect.Width;
                float srcH = _sprite.SourceRect.Height;
                float scale = System.MathF.Min(GetWidth() / srcW, GetHeight() / srcH);
                float w = srcW * scale;
                float h = srcH * scale;
                float x = GetX() + (GetWidth() - w) * 0.5f;
                float y = GetY() + (GetHeight() - h) * 0.5f;
                batcher.Draw(_sprite.Texture2D, new Rectangle((int)x, (int)y, (int)w, (int)h), _sprite.SourceRect, Color.White);
            }

            if (_hovered)
            {
                float x = GetX(), y = GetY(), w = GetWidth(), h = GetHeight();
                batcher.DrawRect(x, y, w, OutlineThickness, Color.White);
                batcher.DrawRect(x, y + h - OutlineThickness, w, OutlineThickness, Color.White);
                batcher.DrawRect(x, y, OutlineThickness, h, Color.White);
                batcher.DrawRect(x + w - OutlineThickness, y, OutlineThickness, h, Color.White);
            }
        }

        void IInputListener.OnMouseEnter()
        {
            _hovered = true;
            if (string.IsNullOrEmpty(_hoverText))
                return;
            var stage = GetStage();
            var mp = stage != null ? stage.GetMousePosition() : new Vector2(GetX(), GetY());
            HoverTextManager.ShowHoverText(_hoverText, mp.X + 12f, mp.Y - 4f);
        }

        void IInputListener.OnMouseExit()
        {
            _hovered = false;
            HoverTextManager.HideHoverText();
        }

        void IInputListener.OnMouseMoved(Vector2 mousePos) { }

        bool IInputListener.OnLeftMousePressed(Vector2 mousePos) => true;

        void IInputListener.OnLeftMouseUp(Vector2 mousePos)
        {
            HoverTextManager.HideHoverText();
            OnClicked?.Invoke();
        }

        bool IInputListener.OnRightMousePressed(Vector2 mousePos) => false;

        void IInputListener.OnRightMouseUp(Vector2 mousePos) { }

        bool IInputListener.OnMouseScrolled(int mouseWheelDelta) => false;
    }
}
