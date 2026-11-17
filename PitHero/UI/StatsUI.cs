using Nez;
using Nez.UI;
using PitHero.Services;

namespace PitHero.UI
{
    /// <summary>
    /// The "Stats" top-bar button (issue #456): the last icon on the bar. Clicking it toggles the
    /// tabbed Stats window, which the scene owns and wires in through <see cref="StatsRequested"/>.
    /// </summary>
    public class StatsUI
    {
        private const string SpriteBaseName = "UIFarmOptions";

        private Stage _stage;
        private HoverableImageButton _button;
        private ImageButtonStyle _normalStyle;
        private ImageButtonStyle _halfStyle;
        private TextService _textService;

        private enum ButtonMode { Normal, Half }
        private ButtonMode _currentMode = ButtonMode.Normal;
        private bool _styleChanged;

        /// <summary>Fired when the button is clicked; the scene toggles the Stats window.</summary>
        public System.Action StatsRequested;

        /// <summary>The button element (the Stats window exempts it from outside-click dismissal).</summary>
        public Element Button => _button;

        private string GetText(string key)
        {
            if (_textService == null && Core.Services != null)
                _textService = Core.Services.GetService<TextService>();
            return _textService?.DisplayText(TextType.UI, key) ?? key;
        }

        /// <summary>Creates the button and adds it to the stage.</summary>
        public void InitializeUI(Stage stage)
        {
            _stage = stage;
            var uiAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/UI.atlas");
            var sprite = uiAtlas.GetSprite(SpriteBaseName);

            _normalStyle = new ImageButtonStyle
            {
                ImageUp = new SpriteDrawable(sprite),
                ImageDown = new SpriteDrawable(uiAtlas.GetSprite(SpriteBaseName + "Inverse")),
                ImageOver = new SpriteDrawable(uiAtlas.GetSprite(SpriteBaseName + "Highlight"))
            };
            _halfStyle = ButtonSprite2xFactory.CreateHalfStyle(uiAtlas, SpriteBaseName);

            _button = new HoverableImageButton(_normalStyle, GetText(UITextKey.ButtonStats));
            _button.ClickSoundCategory = ButtonClickCategory.TopBar;
            _button.SetSize(sprite.SourceRect.Width, sprite.SourceRect.Height);
            _button.OnClicked += (_) =>
            {
                _button.DismissHoverText();
                StatsRequested?.Invoke();
            };
            _stage.AddElement(_button);
        }

        private void UpdateButtonStyleIfNeeded()
        {
            var desired = WindowManager.IsHalfHeightMode() ? ButtonMode.Half : ButtonMode.Normal;
            if (desired == _currentMode)
                return;
            var style = desired == ButtonMode.Half ? _halfStyle : _normalStyle;
            _button.SetStyle(style);
            var up = ((SpriteDrawable)style.ImageUp).Sprite;
            _button.SetSize(up.SourceRect.Width, up.SourceRect.Height);
            _currentMode = desired;
            _styleChanged = true;
        }

        /// <summary>Per-frame update: swaps the half-size style with the window mode.</summary>
        public void Update() => UpdateButtonStyleIfNeeded();

        /// <summary>True once after a style (and size) change, so the bar re-lays out.</summary>
        public bool ConsumeStyleChangedFlag()
        {
            if (!_styleChanged)
                return false;
            _styleChanged = false;
            return true;
        }

        public void SetPosition(float x, float y) => _button?.SetPosition(x, y);

        /// <summary>Enables/disables hit-testing; disabled while the top UI bar is hidden off-screen.</summary>
        public void SetTouchable(Touchable touchable) => _button?.SetTouchable(touchable);

        public float GetWidth() => _button?.GetWidth() ?? 0f;

        public float GetHeight() => _button?.GetHeight() ?? 0f;
    }
}
