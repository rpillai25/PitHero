using Nez.UI;

namespace PitHero.UI
{
    /// <summary>TextButton subclass exposing a reset for internal hover/press/check state.</summary>
    public class ResettableTextButton : TextButton
    {
        public ResettableTextButton(string text, TextButtonStyle style) : base(text, style) { }
        public ResettableTextButton(string text, Skin skin, string styleName = null) : base(text, skin, styleName) { }
        /// <summary>Resets internal state flags so visual style reverts to Up.</summary>
        public void ResetVisualState()
        {
            // Access protected fields from Button via inheritance
            _mouseDown = false;
            _mouseOver = false;
            _isChecked = false;
            SetDisabled(false);
        }
    }
}
