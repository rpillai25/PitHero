using Microsoft.Xna.Framework;
using Nez;
using Nez.UI;

namespace PitHero.UI
{
    /// <summary>A TextButton that shows a windowed tooltip at the mouse cursor when hovered.</summary>
    public class HoverableTextButton : TextButton
    {
        private readonly string _tooltipText;
        private readonly Stage _stage;
        private Window _tooltipWindow;
        private bool _wasMouseOver;
        private bool _suppressed;
        private bool _tooltipEnabled = true;

        private static readonly Color BrownFontColor = new Color(71, 36, 7);

        public HoverableTextButton(string text, Skin skin, string styleName, string tooltipText, Stage stage)
            : base(text, skin, styleName)
        {
            _tooltipText = tooltipText;
            _stage = stage;

            if (_stage != null && !string.IsNullOrEmpty(_tooltipText))
                BuildTooltipWindow(skin);
        }

        /// <summary>Creates the button from an explicit style; the skin is still needed for the tooltip window.</summary>
        public HoverableTextButton(string text, TextButtonStyle style, Skin skin, string tooltipText, Stage stage)
            : base(text, style)
        {
            _tooltipText = tooltipText;
            _stage = stage;

            if (_stage != null && skin != null && !string.IsNullOrEmpty(_tooltipText))
                BuildTooltipWindow(skin);
        }

        /// <summary>
        /// Gates the tooltip without touching Touchable (the button must stay clickable when enabled).
        /// Disabling hides an open tooltip immediately.
        /// </summary>
        public void SetTooltipEnabled(bool enabled)
        {
            if (_tooltipEnabled == enabled)
                return;
            _tooltipEnabled = enabled;
            if (!enabled)
            {
                _tooltipWindow?.SetVisible(false);
                _wasMouseOver = false;
            }
        }

        private void BuildTooltipWindow(Skin skin)
        {
            _tooltipWindow = new Window("", skin);
            _tooltipWindow.SetMovable(false);
            _tooltipWindow.SetResizable(false);
            _tooltipWindow.SetKeepWithinStage(false);
            _tooltipWindow.SetColor(GameConfig.TransparentMenu);

            var label = new Label(_tooltipText, new LabelStyle { Font = Nez.Graphics.Instance.BitmapFont, FontColor = BrownFontColor });
            _tooltipWindow.Add(label).Pad(6f);
            _tooltipWindow.Pack();
            _tooltipWindow.SetVisible(false);
            _stage.AddElement(_tooltipWindow);
        }

        public override void Draw(Batcher batcher, float parentAlpha)
        {
            base.Draw(batcher, parentAlpha);

            if (_tooltipWindow == null) return;

            // Hide whenever the button's own hierarchy is invisible or the tooltip is gated off
            if (!IsVisible() || !_tooltipEnabled)
            {
                _tooltipWindow.SetVisible(false);
                _wasMouseOver = false;
                return;
            }

            bool isOver = _mouseOver;

            // Mouse left the button — clear suppression so next hover works normally
            if (!isOver && _wasMouseOver)
            {
                _tooltipWindow.SetVisible(false);
                _suppressed = false;
            }
            else if (isOver && !_suppressed)
            {
                PositionTooltip(_stage.GetMousePosition());
                _tooltipWindow.SetVisible(true);
                _tooltipWindow.ToFront();
            }

            _wasMouseOver = isOver;
        }

        /// <summary>Hides the tooltip and suppresses it until the mouse leaves and re-enters the button.</summary>
        public void HideTooltip()
        {
            _tooltipWindow?.SetVisible(false);
            _suppressed = true;
        }

        private void PositionTooltip(Vector2 mousePos)
        {
            float w = _tooltipWindow.GetWidth();
            float h = _tooltipWindow.GetHeight();
            float sw = _stage.GetWidth();
            float sh = _stage.GetHeight();

            float x = mousePos.X + 12f;
            float y = mousePos.Y + 12f;

            if (x + w > sw) x = mousePos.X - w - 4f;
            if (y + h > sh) y = mousePos.Y - h - 4f;

            _tooltipWindow.SetPosition(x, y);
        }
    }
}
