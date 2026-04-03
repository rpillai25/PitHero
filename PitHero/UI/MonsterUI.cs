using Nez;
using Nez.UI;
using PitHero.Services;

namespace PitHero.UI
{
    /// <summary>UI component that displays and manages the allied monster roster panel.</summary>
    public class MonsterUI
    {
        private Stage _stage;
        private HoverableImageButton _monsterButton;
        private Window _monsterWindow;
        private bool _windowVisible = false;
        private ImageButtonStyle _monsterNormalStyle;
        private ImageButtonStyle _monsterHalfStyle;
        private bool _styleChanged = false;
        private Table _monsterListTable;
        private ScrollPane _scrollPane;
        private Skin _skin;
        private TextService _textService;

        private enum MonsterMode { Normal, Half }
        private MonsterMode _currentMonsterMode = MonsterMode.Normal;

        private const float SpriteSize = 32f;

        /// <summary>Whether the monster window is currently visible.</summary>
        public bool IsWindowVisible => _windowVisible;
        
        public MonsterUI()
        {
            _textService = Core.Services.GetService<TextService>();
        }

        /// <summary>Initializes the monster UI and adds the button to the stage.</summary>
        public void InitializeUI(Stage stage)
        {
            _stage = stage;
            _skin = PitHeroSkin.CreateSkin();
            CreateMonsterButton(_skin);
            CreateMonsterWindow(_skin);
            _stage.AddElement(_monsterButton);
        }

        private void CreateMonsterButton(Skin skin)
        {
            var uiAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/UI.atlas");
            var sprite       = uiAtlas.GetSprite("UIMonster");
            var sprite2x     = uiAtlas.GetSprite("UIMonster2x");
            var highlight    = uiAtlas.GetSprite("UIMonsterHighlight");
            var highlight2x  = uiAtlas.GetSprite("UIMonsterHighlight2x");
            var inverse      = uiAtlas.GetSprite("UIMonsterInverse");
            var inverse2x    = uiAtlas.GetSprite("UIMonsterInverse2x");

            _monsterNormalStyle = new ImageButtonStyle
            {
                ImageUp   = new SpriteDrawable(sprite),
                ImageDown = new SpriteDrawable(inverse),
                ImageOver = new SpriteDrawable(highlight)
            };
            _monsterHalfStyle = new ImageButtonStyle
            {
                ImageUp   = new SpriteDrawable(sprite2x),
                ImageDown = new SpriteDrawable(inverse2x),
                ImageOver = new SpriteDrawable(highlight2x)
            };

            _monsterButton = new HoverableImageButton(_monsterNormalStyle, _textService.DisplayText(DialogueType.UI, TextKey.WindowMonsters));
            _monsterButton.SetSize(sprite.SourceRect.Width, sprite.SourceRect.Height);
            _monsterButton.OnClicked += (button) => HandleMonsterButtonClick();
        }

        private void HandleMonsterButtonClick()
        {
            // Close any other open windows (single window policy)
            var allElements = _stage.GetElements();
            for (int i = 0; i < allElements.Count; i++)
            {
                var element = allElements[i];
                if (element is Window window && window.IsVisible() && window != _monsterWindow)
                {
                    window.SetVisible(false);
                    var pauseService = Core.Services.GetService<PauseService>();
                    if (pauseService != null)
                        pauseService.IsPaused = false;
                    Debug.Log("[MonsterUI] Closed other UI window to enforce single window policy");
                    break;
                }
            }
            ToggleMonsterWindow();
        }

        private void CreateMonsterWindow(Skin skin)
        {
            _monsterWindow = new Window(_textService.DisplayText(DialogueType.UI, TextKey.WindowMonsters), skin);
            _monsterWindow.SetSize(380f, 280f);

            _monsterListTable = new Table();
            _monsterListTable.Top().Left();

            _scrollPane = new ScrollPane(_monsterListTable, skin, "ph-default");
            _scrollPane.SetScrollingDisabled(true, false);
            _scrollPane.SetFadeScrollBars(false);
            _monsterWindow.Add(_scrollPane).Expand().Fill().Pad(4f);
            _monsterWindow.SetVisible(false);
        }

        private void ToggleMonsterWindow()
        {
            _windowVisible = !_windowVisible;
            if (_windowVisible)
            {
                RefreshMonsterList();
                UIWindowManager.OnUIWindowOpening();
                _stage.AddElement(_monsterWindow);
                _monsterWindow.SetVisible(true);
                _monsterWindow.ToFront();
                PositionWindow();
                var pauseService = Core.Services.GetService<PauseService>();
                if (pauseService != null)
                    pauseService.IsPaused = true;
            }
            else
            {
                UIWindowManager.OnUIWindowClosing();
                _monsterWindow.SetVisible(false);
                _monsterWindow.Remove();
                var pauseService = Core.Services.GetService<PauseService>();
                if (pauseService != null)
                    pauseService.IsPaused = false;
            }
        }

        private void RefreshMonsterList()
        {
            _monsterListTable.Clear();
            var manager = Core.Services.GetService<AlliedMonsterManager>();
            if (manager == null || manager.Count == 0)
            {
                _monsterListTable.Add(new Label("No allied monsters yet.", _skin)).Left().SetPadBottom(4f);
                _monsterListTable.Row();
                return;
            }

            // Load the actors atlas once for sprite lookups
            Nez.Sprites.SpriteAtlas actorsAtlas = null;
            try
            {
                actorsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/Actors.atlas");
            }
            catch (System.Exception ex)
            {
                Debug.Warn($"[MonsterUI] Failed to load Actors.atlas: {ex.Message}");
            }

            var monsters = manager.AlliedMonsters;
            for (int i = 0; i < monsters.Count; i++)
            {
                var monster = monsters[i];

                // Build a row: [Sprite] [Name + Stats]
                var rowTable = new Table();
                rowTable.Left();

                // Try the down-facing frame for this monster type, fall back to placeholder
                if (actorsAtlas != null)
                {
                    Nez.Textures.Sprite monsterSprite = null;
                    try
                    {
                        monsterSprite = actorsAtlas.GetSprite($"{monster.MonsterTypeName}_WalkDown_1");
                        if (monsterSprite == null)
                            monsterSprite = actorsAtlas.GetSprite("PlaceholderMonster");
                    }
                    catch (System.Exception ex)
                    {
                        Debug.Warn($"[MonsterUI] Failed to load sprite for {monster.MonsterTypeName}: {ex.Message}");
                        monsterSprite = null;
                    }

                    if (monsterSprite != null)
                    {
                        var spriteImage = new Image(new SpriteDrawable(monsterSprite));
                        spriteImage.SetSize(SpriteSize, SpriteSize);
                        rowTable.Add(spriteImage).Size(SpriteSize, SpriteSize).Pad(2f, 2f, 2f, 8f);
                    }
                }

                // Text columns: name on top, stats on second line
                var textTable = new Table();
                textTable.Top().Left();

                var nameLabel  = new Label($"{monster.Name} ({monster.MonsterTypeName})", _skin);
                var statsLabel = new Label($"Fish:{monster.FishingProficiency}  Cook:{monster.CookingProficiency}  Farm:{monster.FarmingProficiency}", _skin);

                textTable.Add(nameLabel).Left();
                textTable.Row();
                textTable.Add(statsLabel).Left();

                rowTable.Add(textTable).Left();

                _monsterListTable.Add(rowTable).Left().Pad(4f, 2f, 0f, 2f);
                _monsterListTable.Row();
            }
        }

        private void PositionWindow()
        {
            if (_monsterButton == null || _monsterWindow == null) return;
            float btnX = _monsterButton.GetX();
            float btnW = _monsterButton.GetWidth();
            float winW = _monsterWindow.GetWidth();
            float winH = _monsterWindow.GetHeight();
            float stageW = _stage.GetWidth();
            float stageH = _stage.GetHeight();

            float winX = btnX + btnW + 4f;
            if (winX + winW > stageW) winX = btnX - 4f - winW;
            if (winX < 0) winX = 0;

            float winY = _monsterButton.GetY() + 4f;
            if (winY + winH > stageH) winY = stageH - winH;
            if (winY < 0) winY = 0;

            _monsterWindow.SetPosition(winX, winY);
        }

        /// <summary>Sets the position of the monster icon button.</summary>
        public void SetPosition(float x, float y)
        {
            _monsterButton?.SetPosition(x, y);
            if (_windowVisible) PositionWindow();
        }

        /// <summary>Returns the width of the monster icon button.</summary>
        public float GetWidth() => _monsterButton?.GetWidth() ?? 0f;

        /// <summary>Force-closes the monster window, used by single window policy.</summary>
        public void ForceCloseWindow()
        {
            if (_windowVisible)
            {
                _windowVisible = false;
                UIWindowManager.OnUIWindowClosing();
                _monsterWindow?.SetVisible(false);
                _monsterWindow?.Remove();
                var pauseService = Core.Services.GetService<PauseService>();
                if (pauseService != null)
                    pauseService.IsPaused = false;
                Debug.Log("[MonsterUI] Monster window force closed by single window policy");
            }
        }

        /// <summary>Returns true once if the button style changed (e.g. due to window resize), then resets.</summary>
        public bool ConsumeStyleChangedFlag()
        {
            if (_styleChanged)
            {
                _styleChanged = false;
                return true;
            }
            return false;
        }

        /// <summary>Switches the monster button between normal and half-height styles.</summary>
        public void UpdateButtonStyleIfNeeded()
        {
            MonsterMode desired;
            if (WindowManager.IsHalfHeightMode())
                desired = MonsterMode.Half;
            else
                desired = MonsterMode.Normal;

            if (desired == _currentMonsterMode)
                return;

            switch (desired)
            {
                case MonsterMode.Normal:
                    _monsterButton.SetStyle(_monsterNormalStyle);
                    _monsterButton.SetSize(
                        ((SpriteDrawable)_monsterNormalStyle.ImageUp).Sprite.SourceRect.Width,
                        ((SpriteDrawable)_monsterNormalStyle.ImageUp).Sprite.SourceRect.Height);
                    break;
                case MonsterMode.Half:
                    _monsterButton.SetStyle(_monsterHalfStyle);
                    _monsterButton.SetSize(
                        ((SpriteDrawable)_monsterHalfStyle.ImageUp).Sprite.SourceRect.Width,
                        ((SpriteDrawable)_monsterHalfStyle.ImageUp).Sprite.SourceRect.Height);
                    break;
            }

            _currentMonsterMode = desired;
            _styleChanged = true;
        }

        /// <summary>Updates the monster UI each frame.</summary>
        public void Update()
        {
            UpdateButtonStyleIfNeeded();
            if (_windowVisible) PositionWindow();
        }
    }
}

