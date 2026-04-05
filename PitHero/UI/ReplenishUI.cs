using Nez;
using Nez.UI;
using PitHero.ECS.Components;
using PitHero.Services;

namespace PitHero.UI
{
    /// <summary>
    /// UI for the Replenish button that triggers smart HP/MP replenishment for the party.
    /// </summary>
    public class ReplenishUI
    {
        private Stage _stage;
        private HoverableImageButton _button;
        private TextService _textService;

        private ImageButtonStyle _normalStyle;
        private ImageButtonStyle _halfStyle;

        private enum ButtonMode { Normal, Half }
        private ButtonMode _currentMode = ButtonMode.Normal;

        private bool _styleChanged = false;

        public ReplenishUI()
        {
        }

        /// <summary>
        /// Safely retrieves TextService. Returns null if Core is not initialized (e.g., in unit tests).
        /// </summary>
        private TextService GetTextService()
        {
            if (_textService == null && Core.Services != null)
            {
                _textService = Core.Services.GetService<TextService>();
            }
            return _textService;
        }

        /// <summary>
        /// Gets localized text or falls back to key name if TextService unavailable.
        /// </summary>
        private string GetText(TextType type, string key)
        {
            var service = GetTextService();
            return service?.DisplayText(type, key) ?? key.ToString();
        }

        /// <summary>
        /// Initializes the button and adds it to the stage
        /// </summary>
        public void InitializeUI(Stage stage)
        {
            _stage = stage;

            var skin = PitHeroSkin.CreateSkin();

            CreateButton(skin);

            _stage.AddElement(_button);
        }

        private void CreateButton(Skin skin)
        {
            var uiAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/UI.atlas");

            // Use Gear sprites as placeholder
            var sprite = uiAtlas.GetSprite("UIGear");
            var sprite2x = uiAtlas.GetSprite("UIGear2x");
            var highlight = uiAtlas.GetSprite("UIGearHighlight");
            var highlight2x = uiAtlas.GetSprite("UIGearHighlight2x");
            var inverse = uiAtlas.GetSprite("UIGearInverse");
            var inverse2x = uiAtlas.GetSprite("UIGearInverse2x");

            _normalStyle = new ImageButtonStyle
            {
                ImageUp = new SpriteDrawable(sprite),
                ImageDown = new SpriteDrawable(inverse),
                ImageOver = new SpriteDrawable(highlight)
            };

            _halfStyle = new ImageButtonStyle
            {
                ImageUp = new SpriteDrawable(sprite2x),
                ImageDown = new SpriteDrawable(inverse2x),
                ImageOver = new SpriteDrawable(highlight2x)
            };

            _button = new HoverableImageButton(_normalStyle, GetText(TextType.UI, UITextKey.ButtonReplenish));
            _button.SetSize(sprite.SourceRect.Width, sprite.SourceRect.Height);

            _button.OnClicked += (button) => OnReplenishClicked();
        }

        /// <summary>
        /// Handle Replenish button click - activates smart replenish on the hero component
        /// </summary>
        private void OnReplenishClicked()
        {
            var heroEntity = Core.Scene?.FindEntity("hero");
            var heroComponent = heroEntity?.GetComponent<HeroComponent>();

            if (heroComponent != null)
            {
                heroComponent.ActivateReplenish();
                Debug.Log("[ReplenishUI] Replenish activated");
            }
        }

        /// <summary>
        /// Update button style based on current window mode
        /// </summary>
        public void UpdateButtonStyleIfNeeded()
        {
            ButtonMode desired = WindowManager.IsHalfHeightMode() ? ButtonMode.Half : ButtonMode.Normal;

            if (desired == _currentMode)
                return;

            ImageButtonStyle style = desired == ButtonMode.Half ? _halfStyle : _normalStyle;

            _button.SetStyle(style);
            _button.SetHoverText(GetText(TextType.UI, UITextKey.ButtonReplenish));
            _button.SetSize(
                ((SpriteDrawable)style.ImageUp).Sprite.SourceRect.Width,
                ((SpriteDrawable)style.ImageUp).Sprite.SourceRect.Height
            );

            _currentMode = desired;
            _styleChanged = true;
        }

        /// <summary>
        /// Position the button at the specified coordinates
        /// </summary>
        public void SetPosition(float x, float y)
        {
            _button?.SetPosition(x, y);
        }

        /// <summary>
        /// Get the button width
        /// </summary>
        public float GetWidth()
        {
            return _button?.GetWidth() ?? 0f;
        }

        /// <summary>
        /// Get the button height
        /// </summary>
        public float GetHeight()
        {
            return _button?.GetHeight() ?? 0f;
        }

        /// <summary>
        /// Consume style changed flag
        /// </summary>
        public bool ConsumeStyleChangedFlag()
        {
            if (_styleChanged)
            {
                _styleChanged = false;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Update method called each frame
        /// </summary>
        public void Update()
        {
            UpdateButtonStyleIfNeeded();
        }
    }
}
