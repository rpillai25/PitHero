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

        private HeroUI _heroUI;
        private MonsterUI _monsterUI;
        private SecondChanceShopUI _secondChanceShopUI;

        public void SetHeroUI(HeroUI heroUI) { _heroUI = heroUI; }
        public void SetMonsterUI(MonsterUI monsterUI) { _monsterUI = monsterUI; }
        public void SetSecondChanceShopUI(SecondChanceShopUI secondChanceShopUI) { _secondChanceShopUI = secondChanceShopUI; }

        private enum ButtonMode { Normal, Half }
        private ButtonMode _currentMode = ButtonMode.Normal;
        private bool _styleChanged = false;

        public bool AreSubButtonsVisible => _subButtonsVisible;

        /// <summary>Gets whether till mode is currently active.</summary>
        public bool IsInTillMode { get; private set; }

        /// <summary>Gets whether building placement mode is currently active.</summary>
        public bool IsInBuildingMode { get; private set; }

        /// <summary>Gets whether seed planting mode is currently active.</summary>
        public bool IsInSeedMode { get; private set; }

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

            // Wire Seeds button (index 1)
            _subButtons[1].OnClicked += (_) => ToggleSeedMode();

            // Wire Buildings button (index 2)
            _subButtons[2].OnClicked += (_) => ToggleBuildingMode();

            // Wire Remove Crops button (index 4)
            _subButtons[4].OnClicked += (_) => ToggleRemoveCropsMode();
        }

        private void ToggleTillMode()
        {
            // Only enters till mode — exiting is handled by SettingsUI detecting any UI click,
            // so the button is idempotent when already in till mode.
            if (!IsInTillMode)
            {
                ExitBuildingMode();     // mutual exclusion
                ExitSeedMode();         // mutual exclusion
                ExitRemoveCropsMode();  // mutual exclusion
                IsInTillMode = true;
            }
        }

        /// <summary>Forces till mode off (e.g., when the player presses Escape).</summary>
        public void ExitTillMode()
        {
            IsInTillMode = false;
        }

        private void ToggleBuildingMode()
        {
            if (IsInBuildingMode)
            {
                IsInBuildingMode = false;
            }
            else
            {
                ExitTillMode();         // mutual exclusion
                ExitSeedMode();         // mutual exclusion
                ExitRemoveCropsMode();  // mutual exclusion
                IsInBuildingMode = true;
            }
        }

        /// <summary>Forces building placement mode off.</summary>
        public void ExitBuildingMode()
        {
            IsInBuildingMode = false;
        }

        private void ToggleSeedMode()
        {
            if (IsInSeedMode)
            {
                IsInSeedMode = false;
            }
            else
            {
                ExitTillMode();         // mutual exclusion
                ExitBuildingMode();     // mutual exclusion
                ExitRemoveCropsMode();  // mutual exclusion
                IsInSeedMode = true;
            }
        }

        /// <summary>Forces seed planting mode off.</summary>
        public void ExitSeedMode()
        {
            IsInSeedMode = false;
        }

        public bool IsInRemoveCropsMode { get; private set; }

        private void ToggleRemoveCropsMode()
        {
            if (IsInRemoveCropsMode)
            {
                IsInRemoveCropsMode = false;
            }
            else
            {
                ExitTillMode();      // mutual exclusion
                ExitBuildingMode();  // mutual exclusion
                ExitSeedMode();      // mutual exclusion
                IsInRemoveCropsMode = true;
            }
        }

        /// <summary>Forces remove-crops mode off.</summary>
        public void ExitRemoveCropsMode()
        {
            IsInRemoveCropsMode = false;
        }

        private void ToggleSubButtons()
        {
            _subButtonsVisible = !_subButtonsVisible;
            if (_subButtonsVisible)
            {
                _heroUI?.ForceCloseWindow();
                _monsterUI?.ForceCloseWindow();
                _secondChanceShopUI?.ForceCloseWindow();
            }
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

            // Only dismiss sub-buttons from world clicks when no sub-mode is running.
            // While a sub-mode is active (placing crops, tilling, etc.) world clicks belong
            // to that mode and must not collapse the sub-button row.
            bool anySubModeActive = IsInTillMode || IsInBuildingMode || IsInSeedMode || IsInRemoveCropsMode;
            if (_subButtonsVisible && !anySubModeActive && Input.LeftMouseButtonPressed)
            {
                var mousePos = _stage.GetMousePosition();
                if (!IsMouseOverAnyFarmButton(mousePos) && _stage.Hit(mousePos) == null)
                    DismissSubButtons();
            }
        }
    }
}
