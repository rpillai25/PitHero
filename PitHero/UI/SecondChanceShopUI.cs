using Nez;
using Nez.UI;
using PitHero.Services;

namespace PitHero.UI
{
    /// <summary>UI shell for the Second Chance Shop - provides a button and tabbed window for purchasing items and crystals.</summary>
    public class SecondChanceShopUI
    {
        private Stage _stage;
        private HoverableImageButton _shopButton;
        private Window _shopWindow;
        private bool _windowVisible = false;
        private ImageButtonStyle _shopNormalStyle;
        private ImageButtonStyle _shopHalfStyle;
        private bool _styleChanged = false;
        private TabPane _tabPane;
        private Tab _itemsTab;
        private Tab _crystalsTab;
        private Image _merchantSprite;
        private SettingsUI _settingsUI;
        private HeroUI _heroUI;
        private MonsterUI _monsterUI;
        private Skin _skin;
        private TextService _textService;

        private enum ShopMode { Normal, Half }
        private ShopMode _currentShopMode = ShopMode.Normal;

        /// <summary>Whether the shop window is currently visible.</summary>
        public bool IsWindowVisible => _windowVisible;

        /// <summary>Initializes the shop UI and adds the button to the stage.</summary>
        public void InitializeUI(Stage stage)
        {
            _stage = stage;
            _skin = PitHeroSkin.CreateSkin();

            var uiAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/UI.atlas");

            var sprite      = uiAtlas.GetSprite("UISecondChance");
            var sprite2x    = uiAtlas.GetSprite("UISecondChance2x");
            var highlight   = uiAtlas.GetSprite("UISecondChanceHighlight");
            var highlight2x = uiAtlas.GetSprite("UISecondChanceHighlight2x");
            var inverse     = uiAtlas.GetSprite("UISecondChanceInverse");
            var inverse2x   = uiAtlas.GetSprite("UISecondChanceInverse2x");

            _shopNormalStyle = new ImageButtonStyle
            {
                ImageUp   = new SpriteDrawable(sprite),
                ImageDown = new SpriteDrawable(inverse),
                ImageOver = new SpriteDrawable(highlight)
            };
            _shopHalfStyle = new ImageButtonStyle
            {
                ImageUp   = new SpriteDrawable(sprite2x),
                ImageDown = new SpriteDrawable(inverse2x),
                ImageOver = new SpriteDrawable(highlight2x)
            };

            _shopButton = new HoverableImageButton(_shopNormalStyle, GetText(TextType.UI, UITextKey.WindowSecondChanceShop));
            _shopButton.SetSize(sprite.SourceRect.Width, sprite.SourceRect.Height);
            _shopButton.OnClicked += (button) => TriggerToggle();

            // Load merchant sprite for display beside the window
            var merchantSprite = uiAtlas.GetSprite("SecondChanceMerchant");
            _merchantSprite = new Image(new SpriteDrawable(merchantSprite));

            CreateShopWindow(_skin);
            _stage.AddElement(_shopButton);
        }

        /// <summary>Safely retrieves TextService, returns null if Core is not initialized.</summary>
        private TextService GetTextService()
        {
            if (_textService == null && Core.Services != null)
            {
                _textService = Core.Services.GetService<TextService>();
            }
            return _textService;
        }

        /// <summary>Gets localized text or falls back to key name if TextService unavailable.</summary>
        private string GetText(TextType type, string key)
        {
            var service = GetTextService();
            return service?.DisplayText(type, key) ?? key;
        }

        /// <summary>Creates the shop window with Items and Crystals tabs.</summary>
        private void CreateShopWindow(Skin skin)
        {
            _shopWindow = new Window(GetText(TextType.UI, UITextKey.WindowSecondChanceShop), skin);
            _shopWindow.SetSize(505f, 348f);

            var tabWindowStyle = skin.Get<TabWindowStyle>("ph-default");
            _tabPane = new TabPane(tabWindowStyle);

            var tabStyle = new TabStyle { Background = null };

            _itemsTab = new Tab(GetText(TextType.UI, UITextKey.TabItems), tabStyle);
            _itemsTab.Add(new Label("Items tab - coming soon", skin, "ph-default")).Expand().Fill().Pad(8f);

            _crystalsTab = new Tab(GetText(TextType.UI, UITextKey.TabCrystals), tabStyle);
            _crystalsTab.Add(new Label("Crystals tab - coming soon", skin, "ph-default")).Expand().Fill().Pad(8f);

            _tabPane.AddTab(_itemsTab);
            _tabPane.AddTab(_crystalsTab);

            _shopWindow.Add(_tabPane).Expand().Fill().Pad(0);
            _shopWindow.SetVisible(false);
        }

        /// <summary>Enforces single window policy then toggles the shop window.</summary>
        public void TriggerToggle()
        {
            _settingsUI?.ForceCloseSettings();
            _heroUI?.ForceCloseWindow();
            _monsterUI?.ForceCloseWindow();
            ToggleShopWindow();
        }

        private void ToggleShopWindow()
        {
            _windowVisible = !_windowVisible;
            if (_windowVisible)
            {
                UIWindowManager.OnUIWindowOpening();
                _stage.AddElement(_shopWindow);
                _shopWindow.SetVisible(true);
                _shopWindow.ToFront();
                PositionWindow();
                _stage.AddElement(_merchantSprite);
                _merchantSprite.SetPosition(1150f, 255f);
                _merchantSprite.ToFront();
                var pauseService = Core.Services.GetService<PauseService>();
                if (pauseService != null)
                    pauseService.IsPaused = true;
            }
            else
            {
                UIWindowManager.OnUIWindowClosing();
                _shopWindow.SetVisible(false);
                _shopWindow.Remove();
                _merchantSprite.Remove();
                var pauseService = Core.Services.GetService<PauseService>();
                if (pauseService != null)
                    pauseService.IsPaused = false;
            }
        }

        private void PositionWindow()
        {
            if (_shopButton == null || _shopWindow == null) return;
            float btnX = _shopButton.GetX();
            float btnW = _shopButton.GetWidth();
            float winW = _shopWindow.GetWidth();
            float winH = _shopWindow.GetHeight();
            float stageW = _stage.GetWidth();
            float stageH = _stage.GetHeight();

            float winX = btnX + btnW + 4f;
            if (winX + winW > stageW) winX = btnX - 4f - winW;
            if (winX < 0) winX = 0;

            float winY = _shopButton.GetY() + 4f;
            if (winY + winH > stageH) winY = stageH - winH;
            if (winY < 0) winY = 0;

            _shopWindow.SetPosition(winX, winY);
        }

        /// <summary>Force-closes the shop window. Used by single window policy.</summary>
        public void ForceCloseWindow()
        {
            if (_windowVisible)
            {
                _windowVisible = false;
                UIWindowManager.OnUIWindowClosing();
                _shopWindow?.SetVisible(false);
                _shopWindow?.Remove();
                _merchantSprite?.Remove();
                var pauseService = Core.Services.GetService<PauseService>();
                if (pauseService != null)
                    pauseService.IsPaused = false;
                Debug.Log("[SecondChanceShopUI] Shop window force closed by single window policy");
            }
        }

        /// <summary>Sets the SettingsUI reference for single window policy.</summary>
        public void SetSettingsUI(SettingsUI settingsUI) { _settingsUI = settingsUI; }

        /// <summary>Sets the HeroUI reference for single window policy.</summary>
        public void SetHeroUI(HeroUI heroUI) { _heroUI = heroUI; }

        /// <summary>Sets the MonsterUI reference for single window policy.</summary>
        public void SetMonsterUI(MonsterUI monsterUI) { _monsterUI = monsterUI; }

        /// <summary>Sets the position of the shop icon button.</summary>
        public void SetPosition(float x, float y)
        {
            _shopButton?.SetPosition(x, y);
            if (_windowVisible) PositionWindow();
        }

        /// <summary>Returns the width of the shop icon button.</summary>
        public float GetWidth() => _shopButton?.GetWidth() ?? 0f;

        /// <summary>Returns true once if the button style changed, then resets.</summary>
        public bool ConsumeStyleChangedFlag()
        {
            if (_styleChanged)
            {
                _styleChanged = false;
                return true;
            }
            return false;
        }

        /// <summary>Switches the shop button between normal and half-height styles based on window mode.</summary>
        public void UpdateButtonStyleIfNeeded()
        {
            ShopMode desired = WindowManager.IsHalfHeightMode() ? ShopMode.Half : ShopMode.Normal;
            if (desired == _currentShopMode) return;

            switch (desired)
            {
                case ShopMode.Normal:
                    _shopButton.SetStyle(_shopNormalStyle);
                    _shopButton.SetSize(
                        ((SpriteDrawable)_shopNormalStyle.ImageUp).Sprite.SourceRect.Width,
                        ((SpriteDrawable)_shopNormalStyle.ImageUp).Sprite.SourceRect.Height);
                    break;
                case ShopMode.Half:
                    _shopButton.SetStyle(_shopHalfStyle);
                    _shopButton.SetSize(
                        ((SpriteDrawable)_shopHalfStyle.ImageUp).Sprite.SourceRect.Width,
                        ((SpriteDrawable)_shopHalfStyle.ImageUp).Sprite.SourceRect.Height);
                    break;
            }

            _currentShopMode = desired;
            _styleChanged = true;
        }

        /// <summary>Updates the shop UI each frame.</summary>
        public void Update()
        {
            UpdateButtonStyleIfNeeded();
            if (_windowVisible) PositionWindow();
        }
    }
}
