using Nez;
using Nez.UI;

namespace PitHero.UI
{
    /// <summary>A CheckBox that shows a hover-text tooltip at the mouse cursor when hovered.</summary>
    public class HoverableCheckBox : CheckBox
    {
        private readonly string _tooltipText;
        private readonly Stage _stage;
        private bool _wasMouseOver;

        public HoverableCheckBox(string text, Skin skin, string tooltipText, Stage stage)
            : base(text, skin, "ph-default")
        {
            _tooltipText = tooltipText;
            _stage = stage;
        }

        public override void Draw(Batcher batcher, float parentAlpha)
        {
            base.Draw(batcher, parentAlpha);

            if (string.IsNullOrEmpty(_tooltipText) || _stage == null)
                return;

            if (!IsVisible())
            {
                if (_wasMouseOver)
                {
                    HoverTextManager.HideHoverText();
                    _wasMouseOver = false;
                }
                return;
            }

            bool isOver = _mouseOver;

            if (!isOver && _wasMouseOver)
            {
                HoverTextManager.HideHoverText();
            }
            else if (isOver && !_wasMouseOver)
            {
                var mp = _stage.GetMousePosition();
                HoverTextManager.ShowHoverText(_tooltipText, mp.X + 12f, mp.Y + 12f);
            }

            _wasMouseOver = isOver;
        }
    }
}
