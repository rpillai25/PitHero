using Microsoft.Xna.Framework;
using Nez;
using Nez.UI;
using PitHero.Services;

namespace PitHero.UI
{
    /// <summary>
    /// UI for the Farm top-bar button and its sub-button row.
    /// Sub-buttons: Harvested Crops, Till, Irrigation (no-op), Seeds, Remove Crops, Restore Grass, Refrigerator.
    /// Building mode has moved to ConstructionUI.
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

        // Sub-button art and text keys — 7 entries.
        private static readonly string[] SubButtonBaseNames =
        {
            "UIHarvestedCrops", // 0 Harvested Crops
            "UITill",           // 1 Till
            "UIIrrigation",     // 2 Irrigation (future feature)
            "UISeed",           // 3 Seeds
            "UIDestroyCrop",    // 4 Remove Crops
            "UIRestoreGrass",   // 5 Restore Grass
            "UIRefrigerator",   // 6 Refrigerator
        };

        private static readonly string[] SubButtonTextKeys =
        {
            UITextKey.ButtonFarmHarvestedCrops,
            UITextKey.ButtonFarmTill,
            UITextKey.ButtonFarmIrrigation,
            UITextKey.ButtonFarmSeeds,
            UITextKey.ButtonFarmDestroyCrops,
            UITextKey.ButtonFarmRestoreGrass,
            UITextKey.ButtonFarmRefrigerator,
        };

        private bool _subButtonsVisible = false;
        private bool _subButtonsToggled = false;

        private HeroUI _heroUI;
        private MonsterUI _monsterUI;
        private SecondChanceShopUI _secondChanceShopUI;
        private ConstructionUI _constructionUI;

        /// <summary>Wires the HeroUI cross-reference for single-window policy.</summary>
        public void SetHeroUI(HeroUI heroUI) { _heroUI = heroUI; }
        /// <summary>Wires the MonsterUI cross-reference for single-window policy.</summary>
        public void SetMonsterUI(MonsterUI monsterUI) { _monsterUI = monsterUI; }
        /// <summary>Wires the SecondChanceShopUI cross-reference for single-window policy.</summary>
        public void SetSecondChanceShopUI(SecondChanceShopUI secondChanceShopUI) { _secondChanceShopUI = secondChanceShopUI; }
        /// <summary>Wires the ConstructionUI cross-reference for mutual exclusion.</summary>
        public void SetConstructionUI(ConstructionUI constructionUI) { _constructionUI = constructionUI; }

        /// <summary>Fired when the Refrigerator sub-button is clicked; the scene opens the fridge dialog.</summary>
        public System.Action RefrigeratorRequested;

        private enum ButtonMode { Normal, Half }
        private ButtonMode _currentMode = ButtonMode.Normal;
        private bool _styleChanged = false;

        /// <summary>Gets whether the Farm sub-buttons are currently visible.</summary>
        public bool AreSubButtonsVisible => _subButtonsVisible;

        /// <summary>Gets whether till mode is currently active.</summary>
        public bool IsInTillMode { get; private set; }

        /// <summary>Gets whether seed planting mode is currently active.</summary>
        public bool IsInSeedMode { get; private set; }

        /// <summary>Gets whether remove-crops mode is currently active.</summary>
        public bool IsInRemoveCropsMode { get; private set; }

        /// <summary>Gets whether the Harvested Crops viewer is currently open.</summary>
        public bool IsInHarvestedCropsMode { get; private set; }

        /// <summary>Gets whether restore-grass mode is currently active.</summary>
        public bool IsInRestoreGrassMode { get; private set; }

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

        /// <summary>Creates all Farm UI buttons and adds them to the stage.</summary>
        public void InitializeUI(Stage stage)
        {
            _stage = stage;
            var skin = PitHeroSkin.CreateSkin();
            CreateFarmButton(skin);
            CreateSubButtons(skin);

            _stage.AddElement(_farmButton);
            for (int i = 0; i < _subButtons.Length; i++)
                _stage.AddElement(_subButtons[i]);
        }

        private void CreateFarmButton(Skin skin)
        {
            var uiAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/UI.atlas");

            var sprite    = uiAtlas.GetSprite("UIFarm");
            var highlight = uiAtlas.GetSprite("UIFarmHighlight");
            var inverse   = uiAtlas.GetSprite("UIFarmInverse");

            _farmNormalStyle = new ImageButtonStyle
            {
                ImageUp   = new SpriteDrawable(sprite),
                ImageDown = new SpriteDrawable(inverse),
                ImageOver = new SpriteDrawable(highlight)
            };

            _farmHalfStyle = ButtonSprite2xFactory.CreateHalfStyle(uiAtlas, "UIFarm");

            _farmButton = new HoverableImageButton(_farmNormalStyle, GetText(TextType.UI, UITextKey.ButtonFarm));
            _farmButton.ClickSoundCategory = ButtonClickCategory.TopBar;
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
                var sprite    = uiAtlas.GetSprite(baseName);
                var highlight = uiAtlas.GetSprite(baseName + "Highlight");
                var inverse   = uiAtlas.GetSprite(baseName + "Inverse");

                _subNormalStyles[i] = new ImageButtonStyle
                {
                    ImageUp   = new SpriteDrawable(sprite),
                    ImageDown = new SpriteDrawable(inverse),
                    ImageOver = new SpriteDrawable(highlight)
                };

                _subHalfStyles[i] = ButtonSprite2xFactory.CreateHalfStyle(uiAtlas, baseName);

                _subButtons[i] = new HoverableImageButton(_subNormalStyles[i], GetText(TextType.UI, SubButtonTextKeys[i]));
                _subButtons[i].ClickSoundCategory = ButtonClickCategory.TopBar;
                _subButtons[i].SetSize(sprite.SourceRect.Width, sprite.SourceRect.Height);
                _subButtons[i].SetVisible(false);
            }

            // Wire Harvested Crops button (index 0). Dismiss its hover text on click so it doesn't
            // linger over the Harvested Crops viewer that opens on top of the still-hovered button.
            _subButtons[0].OnClicked += (_) =>
            {
                _subButtons[0].DismissHoverText();
                ToggleHarvestedCropsMode();
            };

            // Wire Till button (index 1)
            _subButtons[1].OnClicked += (_) => ToggleTillMode();

            // Wire Irrigation button (index 2) — future feature, no handler
            // _subButtons[2] intentionally has no click handler

            // Wire Seeds button (index 3)
            _subButtons[3].OnClicked += (_) => ToggleSeedMode();

            // Wire Remove Crops button (index 4)
            _subButtons[4].OnClicked += (_) => ToggleRemoveCropsMode();

            // Wire Restore Grass button (index 5)
            _subButtons[5].OnClicked += (_) => ToggleRestoreGrassMode();

            // Wire Refrigerator button (index 6)
            _subButtons[6].OnClicked += (_) =>
            {
                DismissHoverText();
                DismissSubButtons();
                RefrigeratorRequested?.Invoke();
            };
        }

        private void DismissHoverText()
        {
            for (int i = 0; i < _subButtons.Length; i++)
                _subButtons[i].DismissHoverText();
        }

        private void ToggleTillMode()
        {
            // Only enters till mode — exiting is handled by SettingsUI detecting any UI click,
            // so the button is idempotent when already in till mode.
            if (!IsInTillMode)
            {
                _constructionUI?.ExitBuildingMode(); // mutual exclusion
                ExitSeedMode();                      // mutual exclusion
                ExitRemoveCropsMode();               // mutual exclusion
                ExitHarvestedCropsMode();            // mutual exclusion
                ExitRestoreGrassMode();              // mutual exclusion
                IsInTillMode = true;
            }
        }

        /// <summary>Forces till mode off (e.g., when the player presses Escape).</summary>
        public void ExitTillMode()
        {
            IsInTillMode = false;
        }

        private void ToggleSeedMode()
        {
            if (IsInSeedMode)
            {
                IsInSeedMode = false;
            }
            else
            {
                ExitTillMode();                      // mutual exclusion
                _constructionUI?.ExitBuildingMode(); // mutual exclusion
                ExitRemoveCropsMode();               // mutual exclusion
                ExitHarvestedCropsMode();            // mutual exclusion
                ExitRestoreGrassMode();              // mutual exclusion
                IsInSeedMode = true;
            }
        }

        /// <summary>Forces seed planting mode off.</summary>
        public void ExitSeedMode()
        {
            IsInSeedMode = false;
        }

        private void ToggleRemoveCropsMode()
        {
            if (IsInRemoveCropsMode)
            {
                IsInRemoveCropsMode = false;
            }
            else
            {
                ExitTillMode();                      // mutual exclusion
                _constructionUI?.ExitBuildingMode(); // mutual exclusion
                ExitSeedMode();                      // mutual exclusion
                ExitHarvestedCropsMode();            // mutual exclusion
                ExitRestoreGrassMode();              // mutual exclusion
                IsInRemoveCropsMode = true;
            }
        }

        /// <summary>Forces remove-crops mode off.</summary>
        public void ExitRemoveCropsMode()
        {
            IsInRemoveCropsMode = false;
        }

        private void ToggleRestoreGrassMode()
        {
            if (IsInRestoreGrassMode)
            {
                IsInRestoreGrassMode = false;
            }
            else
            {
                ExitTillMode();                      // mutual exclusion
                _constructionUI?.ExitBuildingMode(); // mutual exclusion
                ExitSeedMode();                      // mutual exclusion
                ExitRemoveCropsMode();               // mutual exclusion
                ExitHarvestedCropsMode();            // mutual exclusion
                IsInRestoreGrassMode = true;
            }
        }

        /// <summary>Forces restore-grass mode off.</summary>
        public void ExitRestoreGrassMode()
        {
            IsInRestoreGrassMode = false;
        }

        private void ToggleHarvestedCropsMode()
        {
            if (IsInHarvestedCropsMode)
            {
                IsInHarvestedCropsMode = false;
            }
            else
            {
                ExitTillMode();                      // mutual exclusion
                _constructionUI?.ExitBuildingMode(); // mutual exclusion
                ExitSeedMode();                      // mutual exclusion
                ExitRemoveCropsMode();               // mutual exclusion
                ExitRestoreGrassMode();              // mutual exclusion
                IsInHarvestedCropsMode = true;
            }
        }

        /// <summary>Forces the Harvested Crops viewer off.</summary>
        public void ExitHarvestedCropsMode()
        {
            IsInHarvestedCropsMode = false;
        }

        /// <summary>
        /// Opens the Harvested Crops viewer programmatically (e.g. from a Crop Storage context menu),
        /// applying the same mutual exclusion as the Farm sub-button.
        /// </summary>
        public void EnterHarvestedCropsMode()
        {
            if (IsInHarvestedCropsMode)
                return;
            ExitTillMode();                      // mutual exclusion
            _constructionUI?.ExitBuildingMode(); // mutual exclusion
            ExitSeedMode();                      // mutual exclusion
            ExitRemoveCropsMode();               // mutual exclusion
            ExitRestoreGrassMode();              // mutual exclusion
            IsInHarvestedCropsMode = true;
        }

        private void ToggleSubButtons()
        {
            _subButtonsVisible = !_subButtonsVisible;
            if (_subButtonsVisible)
            {
                _heroUI?.ForceCloseWindow();
                _monsterUI?.ForceCloseWindow();
                _secondChanceShopUI?.ForceCloseWindow();
                _constructionUI?.DismissSubButtons(); // cross-dismiss Construction sub-bar
            }
            for (int i = 0; i < _subButtons.Length; i++)
                _subButtons[i].SetVisible(_subButtonsVisible);
            _subButtonsToggled = true;
        }

        /// <summary>Hides the Farm sub-button row without triggering mode exits.</summary>
        public void DismissSubButtons()
        {
            if (!_subButtonsVisible)
                return;
            _subButtonsVisible = false;
            for (int i = 0; i < _subButtons.Length; i++)
                _subButtons[i].SetVisible(false);
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
            for (int i = 0; i < _subButtons.Length; i++)
                if (IsInsideButton(_subButtons[i], mousePos))
                    return true;
            return false;
        }

        /// <summary>Moves the Farm top-bar button to the specified position.</summary>
        public void SetPosition(float x, float y)
        {
            _farmButton?.SetPosition(x, y);
        }

        /// <summary>Positions the sub-button row starting at startX, all at height y.</summary>
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

        /// <summary>Enables/disables hit-testing; disabled while the top UI bar is hidden off-screen.</summary>
        public void SetTouchable(Touchable touchable)
        {
            _farmButton?.SetTouchable(touchable);
            if (_subButtons != null)
            {
                for (int i = 0; i < _subButtons.Length; i++)
                    _subButtons[i].SetTouchable(touchable);
            }
        }

        /// <summary>Returns the width of the Farm top-bar button.</summary>
        public float GetWidth()  => _farmButton?.GetWidth()  ?? 0f;
        /// <summary>Returns the height of the Farm top-bar button.</summary>
        public float GetHeight() => _farmButton?.GetHeight() ?? 0f;

        /// <summary>Returns the height of the sub-button row (first button's height).</summary>
        public float GetSubButtonsHeight()
        {
            if (_subButtons == null || _subButtons.Length == 0)
                return 0f;
            return _subButtons[0].GetHeight();
        }

        /// <summary>Switches button art between 1x and 2x based on the current window mode.</summary>
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

        /// <summary>Returns true once after a button style swap; resets the flag.</summary>
        public bool ConsumeStyleChangedFlag()
        {
            if (_styleChanged)
            {
                _styleChanged = false;
                return true;
            }
            return false;
        }

        /// <summary>Returns true once after the sub-buttons visibility changed; resets the flag.</summary>
        public bool ConsumeSubButtonsToggleFlag()
        {
            if (_subButtonsToggled)
            {
                _subButtonsToggled = false;
                return true;
            }
            return false;
        }

        /// <summary>Per-frame update: style swap and outside-click collapse when no sub-mode is active.</summary>
        public void Update()
        {
            UpdateButtonStyleIfNeeded();

            // Only dismiss sub-buttons from world clicks when no sub-mode is running.
            // While a sub-mode is active (placing crops, tilling, etc.) world clicks belong
            // to that mode and must not collapse the sub-button row.
            bool anySubModeActive = IsInTillMode || IsInSeedMode || IsInRemoveCropsMode
                                  || IsInHarvestedCropsMode || IsInRestoreGrassMode;
            if (_subButtonsVisible && !anySubModeActive && Input.LeftMouseButtonPressed
                && Util.MouseUtils.IsMouseInsideWindow())
            {
                var mousePos = _stage.GetMousePosition();
                if (!IsMouseOverAnyFarmButton(mousePos) && _stage.Hit(mousePos) == null)
                    DismissSubButtons();
            }
        }
    }
}
