using Microsoft.Xna.Framework;
using Nez;
using Nez.UI;
using PitHero.Services;

namespace PitHero.UI
{
    /// <summary>
    /// UI for the Construction top-bar button and its sub-button row (Buildings).
    /// Owns building placement mode, previously part of FarmUI.
    /// </summary>
    public class ConstructionUI
    {
        private Stage _stage;
        private HoverableImageButton _constructionButton;
        private TextService _textService;

        private ImageButtonStyle _normalStyle;
        private ImageButtonStyle _halfStyle;

        private HoverableImageButton[] _subButtons;
        private ImageButtonStyle[] _subNormalStyles;
        private ImageButtonStyle[] _subHalfStyles;

        private static readonly string[] SubButtonBaseNames = { "UIBuildings" };
        private static readonly string[] SubButtonTextKeys  = { UITextKey.ButtonFarmBuildings };

        private bool _subButtonsVisible = false;
        private bool _subButtonsToggled = false;

        private HeroUI _heroUI;
        private MonsterUI _monsterUI;
        private SecondChanceShopUI _secondChanceShopUI;
        private FarmUI _farmUI;

        /// <summary>Wires the HeroUI cross-reference for single-window policy.</summary>
        public void SetHeroUI(HeroUI heroUI) { _heroUI = heroUI; }
        /// <summary>Wires the MonsterUI cross-reference for single-window policy.</summary>
        public void SetMonsterUI(MonsterUI monsterUI) { _monsterUI = monsterUI; }
        /// <summary>Wires the SecondChanceShopUI cross-reference for single-window policy.</summary>
        public void SetSecondChanceShopUI(SecondChanceShopUI secondChanceShopUI) { _secondChanceShopUI = secondChanceShopUI; }
        /// <summary>Wires the FarmUI cross-reference for mutual exclusion.</summary>
        public void SetFarmUI(FarmUI farmUI) { _farmUI = farmUI; }

        private enum ButtonMode { Normal, Half }
        private ButtonMode _currentMode = ButtonMode.Normal;
        private bool _styleChanged = false;

        /// <summary>Gets whether the Construction sub-buttons are currently visible.</summary>
        public bool AreSubButtonsVisible => _subButtonsVisible;

        /// <summary>Gets whether building placement mode is currently active.</summary>
        public bool IsInBuildingMode { get; private set; }

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

        /// <summary>Creates all Construction UI buttons and adds them to the stage.</summary>
        public void InitializeUI(Stage stage)
        {
            _stage = stage;
            var skin = PitHeroSkin.CreateSkin();
            CreateConstructionButton(skin);
            CreateSubButtons(skin);

            _stage.AddElement(_constructionButton);
            for (int i = 0; i < _subButtons.Length; i++)
                _stage.AddElement(_subButtons[i]);
        }

        private void CreateConstructionButton(Skin skin)
        {
            var uiAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/UI.atlas");

            var sprite    = uiAtlas.GetSprite("UIConstruction");
            var highlight = uiAtlas.GetSprite("UIConstructionHighlight");
            var inverse   = uiAtlas.GetSprite("UIConstructionInverse");

            _normalStyle = new ImageButtonStyle
            {
                ImageUp   = new SpriteDrawable(sprite),
                ImageDown = new SpriteDrawable(inverse),
                ImageOver = new SpriteDrawable(highlight)
            };

            _halfStyle = ButtonSprite2xFactory.CreateHalfStyle(uiAtlas, "UIConstruction");

            _constructionButton = new HoverableImageButton(_normalStyle, GetText(TextType.UI, UITextKey.ButtonConstruction));
            _constructionButton.ClickSoundCategory = ButtonClickCategory.TopBar;
            _constructionButton.SetSize(sprite.SourceRect.Width, sprite.SourceRect.Height);
            _constructionButton.OnClicked += (_) => ToggleSubButtons();
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

            // Wire Buildings button (index 0)
            _subButtons[0].OnClicked += (_) => ToggleBuildingMode();
        }

        /// <summary>Toggles building placement mode on; exits all Farm sub-modes for mutual exclusion.</summary>
        public void ToggleBuildingMode()
        {
            if (IsInBuildingMode)
            {
                IsInBuildingMode = false;
            }
            else
            {
                _farmUI?.ExitTillMode();            // mutual exclusion
                _farmUI?.ExitSeedMode();            // mutual exclusion
                _farmUI?.ExitRemoveCropsMode();     // mutual exclusion
                _farmUI?.ExitHarvestedCropsMode();  // mutual exclusion
                _farmUI?.ExitRestoreGrassMode();    // mutual exclusion
                IsInBuildingMode = true;
            }
        }

        /// <summary>Forces building placement mode off (e.g., when the player presses Escape or cancels placement).</summary>
        public void ExitBuildingMode()
        {
            IsInBuildingMode = false;
        }

        private void ToggleSubButtons()
        {
            _subButtonsVisible = !_subButtonsVisible;
            if (_subButtonsVisible)
            {
                _heroUI?.ForceCloseWindow();
                _monsterUI?.ForceCloseWindow();
                _secondChanceShopUI?.ForceCloseWindow();
                _farmUI?.DismissSubButtons(); // cross-dismiss Farm sub-bar
            }
            for (int i = 0; i < _subButtons.Length; i++)
                _subButtons[i].SetVisible(_subButtonsVisible);
            _subButtonsToggled = true;
        }

        /// <summary>Hides the Construction sub-button row without triggering mode exits.</summary>
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

        private bool IsMouseOverAnyConstructionButton(Vector2 mousePos)
        {
            if (IsInsideButton(_constructionButton, mousePos))
                return true;
            for (int i = 0; i < _subButtons.Length; i++)
                if (IsInsideButton(_subButtons[i], mousePos))
                    return true;
            return false;
        }

        /// <summary>Moves the Construction top-bar button to the specified position.</summary>
        public void SetPosition(float x, float y)
        {
            _constructionButton?.SetPosition(x, y);
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
            _constructionButton?.SetTouchable(touchable);
            if (_subButtons != null)
            {
                for (int i = 0; i < _subButtons.Length; i++)
                    _subButtons[i].SetTouchable(touchable);
            }
        }

        /// <summary>Returns the width of the Construction top-bar button.</summary>
        public float GetWidth()  => _constructionButton?.GetWidth()  ?? 0f;
        /// <summary>Returns the height of the Construction top-bar button.</summary>
        public float GetHeight() => _constructionButton?.GetHeight() ?? 0f;

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

            ImageButtonStyle style = desired == ButtonMode.Half ? _halfStyle : _normalStyle;
            _constructionButton.SetStyle(style);
            _constructionButton.SetHoverText(GetText(TextType.UI, UITextKey.ButtonConstruction));
            _constructionButton.SetSize(
                ((SpriteDrawable)style.ImageUp).Sprite.SourceRect.Width,
                ((SpriteDrawable)style.ImageUp).Sprite.SourceRect.Height
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

        /// <summary>Per-frame update: style swap and outside-click collapse when not in building mode.</summary>
        public void Update()
        {
            UpdateButtonStyleIfNeeded();

            // Only dismiss sub-buttons from world clicks when not in building mode.
            if (_subButtonsVisible && !IsInBuildingMode && Input.LeftMouseButtonPressed
                && Util.MouseUtils.IsMouseInsideWindow())
            {
                var mousePos = _stage.GetMousePosition();
                if (!IsMouseOverAnyConstructionButton(mousePos) && _stage.Hit(mousePos) == null)
                    DismissSubButtons();
            }
        }
    }
}
