using Microsoft.Xna.Framework;
using Nez;
using Nez.UI;
using PitHero.Services;

namespace PitHero.UI
{
    /// <summary>
    /// UI for the Farm button and its sub-button row (Till, Seeds, Buildings, Options, Destroy Crops).
    /// </summary>
    public class FarmUI
    {
        private Stage _stage;
        private HoverableImageButton _farmButton;
        private TextService _textService;

        private ImageButtonStyle _farmNormalStyle;
        private ImageButtonStyle _farmHalfStyle;

        private HoverableImageButton[] _subButtons;
        private ImageButtonStyle[] _subNormalStyles;
        private ImageButtonStyle[] _subHalfStyles;

        private static readonly string[] SubButtonBaseNames =
        {
            "UITill", "UISeed", "UIBuildings", "UIFarmOptions", "UIDestroyCrop"
        };

        private static readonly string[] SubButtonTextKeys =
        {
            UITextKey.ButtonFarmTill,
            UITextKey.ButtonFarmSeeds,
            UITextKey.ButtonFarmBuildings,
            UITextKey.ButtonFarmOptions,
            UITextKey.ButtonFarmDestroyCrops,
        };

        private bool _subButtonsVisible = false;
        private bool _subButtonsToggled = false;

        private enum ButtonMode { Normal, Half }
        private ButtonMode _currentMode = ButtonMode.Normal;
        private bool _styleChanged = false;

        public bool AreSubButtonsVisible => _subButtonsVisible;

        /// <summary>Gets whether till mode is currently active.</summary>
        public bool IsInTillMode { get; private set; }

        private TextService GetTextService()
        {
            if (_textService == null && Core.Services != null)
                _textService = Core.Services.GetService<TextService>();
            return _textService;
        }

        private string GetText(TextType type, string key)
        {
            var service = GetTextService();
            return service?.DisplayText(type, key) ?? key;
        }

        public void InitializeUI(Stage stage)
        {
            _stage = stage;
            var skin = PitHeroSkin.CreateSkin();
            CreateFarmButton(skin);
            CreateSubButtons(skin);

            _stage.AddElement(_farmButton);
            foreach (var btn in _subButtons)
                _stage.AddElement(btn);
        }

        private void CreateFarmButton(Skin skin)
        {
            var uiAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/UI.atlas");

            var sprite      = uiAtlas.GetSprite("UIFarm");
            var sprite2x    = uiAtlas.GetSprite("UIFarm2x");
            var highlight   = uiAtlas.GetSprite("UIFarmHighlight");
            var highlight2x = uiAtlas.GetSprite("UIFarmHighlight2x");
            var inverse     = uiAtlas.GetSprite("UIFarmInverse");
            var inverse2x   = uiAtlas.GetSprite("UIFarmInverse2x");

            _farmNormalStyle = new ImageButtonStyle
            {
                ImageUp   = new SpriteDrawable(sprite),
                ImageDown = new SpriteDrawable(inverse),
                ImageOver = new SpriteDrawable(highlight)
            };

            _farmHalfStyle = new ImageButtonStyle
            {
                ImageUp   = new SpriteDrawable(sprite2x),
                ImageDown = new SpriteDrawable(inverse2x),
                ImageOver = new SpriteDrawable(highlight2x)
            };

            _farmButton = new HoverableImageButton(_farmNormalStyle, GetText(TextType.UI, UITextKey.ButtonFarm));
            _farmButton.SetSize(sprite.SourceRect.Width, sprite.SourceRect.Height);
            _farmButton.OnClicked += (_) => ToggleSubButtons();
        }

        private void CreateSubButtons(Skin skin)
        {
            var uiAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/UI.atlas");

            int count = SubButtonBaseNames.Length;
            _subButtons      = new HoverableImageButton[count];
            _subNormalStyles = new ImageButtonStyle[count];
            _subHalfStyles   = new ImageButtonStyle[count];

            for (int i = 0; i < count; i++)
            {
                string baseName = SubButtonBaseNames[i];
                var sprite      = uiAtlas.GetSprite(baseName);
                var sprite2x    = uiAtlas.GetSprite(baseName + "2x");
                var highlight   = uiAtlas.GetSprite(baseName + "Highlight");
                var highlight2x = uiAtlas.GetSprite(baseName + "Highlight2x");
                var inverse     = uiAtlas.GetSprite(baseName + "Inverse");
                var inverse2x   = uiAtlas.GetSprite(baseName + "Inverse2x");

                _subNormalStyles[i] = new ImageButtonStyle
                {
                    ImageUp   = new SpriteDrawable(sprite),
                    ImageDown = new SpriteDrawable(inverse),
                    ImageOver = new SpriteDrawable(highlight)
                };

                _subHalfStyles[i] = new ImageButtonStyle
                {
                    ImageUp   = new SpriteDrawable(sprite2x),
                    ImageDown = new SpriteDrawable(inverse2x),
                    ImageOver = new SpriteDrawable(highlight2x)
                };

                _subButtons[i] = new HoverableImageButton(_subNormalStyles[i], GetText(TextType.UI, SubButtonTextKeys[i]));
                _subButtons[i].SetSize(sprite.SourceRect.Width, sprite.SourceRect.Height);
                _subButtons[i].SetVisible(false);
            }

            // Wire Till button (index 0)
            _subButtons[0].OnClicked += (_) => ToggleTillMode();
        }

        private void ToggleTillMode()
        {
            IsInTillMode = !IsInTillMode;
        }

        /// <summary>Forces till mode off (e.g., when the player presses Escape).</summary>
        public void ExitTillMode()
        {
            IsInTillMode = false;
        }

        private void ToggleSubButtons()
        {
            _subButtonsVisible = !_subButtonsVisible;
            foreach (var btn in _subButtons)
                btn.SetVisible(_subButtonsVisible);
            _subButtonsToggled = true;
        }

        public void DismissSubButtons()
        {
            if (!_subButtonsVisible)
                return;
            _subButtonsVisible = false;
            foreach (var btn in _subButtons)
                btn.SetVisible(false);
            _subButtonsToggled = true;
        }

        private bool IsInsideButton(HoverableImageButton btn, Vector2 mousePos)
        {
            return mousePos.X >= btn.GetX() && mousePos.X <= btn.GetX() + btn.GetWidth()
                && mousePos.Y >= btn.GetY() && mousePos.Y <= btn.GetY() + btn.GetHeight();
        }

        private bool IsMouseOverAnyFarmButton(Vector2 mousePos)
        {
            if (IsInsideButton(_farmButton, mousePos))
                return true;
            foreach (var btn in _subButtons)
                if (IsInsideButton(btn, mousePos))
                    return true;
            return false;
        }

        public void SetPosition(float x, float y)
        {
            _farmButton?.SetPosition(x, y);
        }

        public void SetSubButtonsPosition(float startX, float y)
        {
            if (_subButtons == null)
                return;

            float x = startX;
            for (int i = 0; i < _subButtons.Length; i++)
            {
                _subButtons[i].SetPosition(x, y);
                x += _subButtons[i].GetWidth() + GameConfig.UIButtonPadding;
            }
        }

        public float GetWidth()  => _farmButton?.GetWidth()  ?? 0f;
        public float GetHeight() => _farmButton?.GetHeight() ?? 0f;

        public float GetSubButtonsHeight()
        {
            if (_subButtons == null || _subButtons.Length == 0)
                return 0f;
            return _subButtons[0].GetHeight();
        }

        public void UpdateButtonStyleIfNeeded()
        {
            ButtonMode desired = WindowManager.IsHalfHeightMode() ? ButtonMode.Half : ButtonMode.Normal;
            if (desired == _currentMode)
                return;

            ImageButtonStyle farmStyle = desired == ButtonMode.Half ? _farmHalfStyle : _farmNormalStyle;
            _farmButton.SetStyle(farmStyle);
            _farmButton.SetHoverText(GetText(TextType.UI, UITextKey.ButtonFarm));
            _farmButton.SetSize(
                ((SpriteDrawable)farmStyle.ImageUp).Sprite.SourceRect.Width,
                ((SpriteDrawable)farmStyle.ImageUp).Sprite.SourceRect.Height
            );

            for (int i = 0; i < _subButtons.Length; i++)
            {
                ImageButtonStyle subStyle = desired == ButtonMode.Half ? _subHalfStyles[i] : _subNormalStyles[i];
                _subButtons[i].SetStyle(subStyle);
                _subButtons[i].SetHoverText(GetText(TextType.UI, SubButtonTextKeys[i]));
                _subButtons[i].SetSize(
                    ((SpriteDrawable)subStyle.ImageUp).Sprite.SourceRect.Width,
                    ((SpriteDrawable)subStyle.ImageUp).Sprite.SourceRect.Height
                );
            }

            _currentMode = desired;
            _styleChanged = true;
        }

        public bool ConsumeStyleChangedFlag()
        {
            if (_styleChanged)
            {
                _styleChanged = false;
                return true;
            }
            return false;
        }

        public bool ConsumeSubButtonsToggleFlag()
        {
            if (_subButtonsToggled)
            {
                _subButtonsToggled = false;
                return true;
            }
            return false;
        }

        public void Update()
        {
            UpdateButtonStyleIfNeeded();

            if (_subButtonsVisible && Input.LeftMouseButtonPressed)
            {
                var mousePos = _stage.GetMousePosition();
                if (!IsMouseOverAnyFarmButton(mousePos))
                    DismissSubButtons();
            }
        }
    }
}
