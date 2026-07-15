using Microsoft.Xna.Framework;
using Nez;
using Nez.UI;

namespace PitHero.UI
{
    /// <summary>Label that shows a windowed tooltip at the mouse cursor when hovered.</summary>
    public class HoverableLabel : Label, IInputListener
    {
        private static readonly Color BrownFontColor = new Color(71, 36, 7);

        private readonly string _tooltipText;
        private readonly Stage _stage;
        private Window _tooltipWindow;
        private bool _hovered;

        public HoverableLabel(string text, Skin skin, string styleName, string tooltipText, Stage stage)
            : base(text, skin, styleName)
        {
            _tooltipText = tooltipText;
            _stage = stage;
            SetTouchable(Touchable.Enabled);

            if (_stage != null && !string.IsNullOrEmpty(_tooltipText))
                BuildTooltipWindow(skin);
        }

        private void BuildTooltipWindow(Skin skin)
        {
            _tooltipWindow = new Window("", skin);
            _tooltipWindow.SetMovable(false);
            _tooltipWindow.SetResizable(false);
            _tooltipWindow.SetKeepWithinStage(false);
            _tooltipWindow.SetColor(GameConfig.TransparentMenu);

            var label = new Label(_tooltipText, new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = BrownFontColor });
            _tooltipWindow.Add(label).Pad(6f);
            _tooltipWindow.Pack();
            _tooltipWindow.SetVisible(false);
            _stage.AddElement(_tooltipWindow);
        }

        void IInputListener.OnMouseEnter()
        {
            _hovered = true;
            if (_tooltipWindow == null) return;
            PositionTooltip(_stage.GetMousePosition());
            _tooltipWindow.SetVisible(true);
            _tooltipWindow.ToFront();
        }

        void IInputListener.OnMouseMoved(Vector2 mousePos)
        {
            // Nez only dispatches this while a mouse button is held (drag tracking), so the
            // per-frame follow happens in Draw instead.
        }

        void IInputListener.OnMouseExit()
        {
            _hovered = false;
            _tooltipWindow?.SetVisible(false);
        }

        public override void Draw(Batcher batcher, float parentAlpha)
        {
            base.Draw(batcher, parentAlpha);

            if (_tooltipWindow == null) return;

            // Hide whenever the label's own hierarchy is invisible (e.g. settings window closed)
            if (!IsVisible())
            {
                _tooltipWindow.SetVisible(false);
                _hovered = false;
                return;
            }

            // Follow the cursor each frame while hovered
            if (_hovered && _tooltipWindow.IsVisible())
                PositionTooltip(_stage.GetMousePosition());
        }

        private void PositionTooltip(Vector2 mousePos)
        {
            if (_tooltipWindow == null || _stage == null) return;

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

        bool IInputListener.OnLeftMousePressed(Vector2 mousePos) => false;
        bool IInputListener.OnRightMousePressed(Vector2 mousePos) => false;
        void IInputListener.OnLeftMouseUp(Vector2 mousePos) { }
        void IInputListener.OnRightMouseUp(Vector2 mousePos) { }
        bool IInputListener.OnMouseScrolled(int mouseWheelDelta) => false;
    }
}
