using Microsoft.Xna.Framework;
using Nez;
using Nez.UI;

namespace PitHero.UI
{
    /// <summary>A CheckBox that shows a windowed tooltip at the mouse cursor when hovered,
    /// matching the HoverableTextButton / HoverableLabel tooltip styling.</summary>
    public class HoverableCheckBox : CheckBox
    {
        private static readonly Color BrownFontColor = new Color(71, 36, 7);

        private readonly string _tooltipText;
        private readonly Stage _stage;
        private Window _tooltipWindow;
        private bool _wasMouseOver;

        public HoverableCheckBox(string text, Skin skin, string tooltipText, Stage stage)
            : base(text, skin, "ph-default")
        {
            _tooltipText = tooltipText;
            _stage = stage;

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

            // Hide whenever the checkbox's own hierarchy is invisible
            if (!IsVisible())
            {
                _tooltipWindow.SetVisible(false);
                _wasMouseOver = false;
                return;
            }

            bool isOver = _mouseOver;

            if (!isOver && _wasMouseOver)
            {
                _tooltipWindow.SetVisible(false);
            }
            else if (isOver)
            {
                PositionTooltip(_stage.GetMousePosition());
                _tooltipWindow.SetVisible(true);
                _tooltipWindow.ToFront();
            }

            _wasMouseOver = isOver;
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
