using Nez;
using Nez.UI;
using PitHero.Services;

namespace PitHero.UI
{
    /// <summary>UI component that displays and manages the allied monster roster panel.</summary>
    public class MonsterUI
    {
        private Stage _stage;
        // TODO: Replace UIHero sprites with dedicated UIMonster sprites once art is available
        private HoverableImageButton _monsterButton;
        private Window _monsterWindow;
        private bool _windowVisible = false;
        private ImageButtonStyle _monsterNormalStyle;
        private ImageButtonStyle _monsterHalfStyle;
        private bool _styleChanged = false;
        private Table _monsterListTable;
        private ScrollPane _scrollPane;
        private Skin _skin;

        /// <summary>Whether the monster window is currently visible.</summary>
        public bool IsWindowVisible => _windowVisible;

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
            // TODO: Replace UIHero sprites with dedicated UIMonster sprites once art is available
            var uiAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/UI.atlas");
            var sprite       = uiAtlas.GetSprite("UIHero");
            var sprite2x     = uiAtlas.GetSprite("UIHero2x");
            var highlight    = uiAtlas.GetSprite("UIHeroHighlight");
            var highlight2x  = uiAtlas.GetSprite("UIHeroHighlight2x");
            var inverse      = uiAtlas.GetSprite("UIHeroInverse");
            var inverse2x    = uiAtlas.GetSprite("UIHeroInverse2x");

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

            _monsterButton = new HoverableImageButton(_monsterNormalStyle, "Monsters");
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
            _monsterWindow = new Window("Monsters", skin);
            _monsterWindow.SetSize(340f, 300f);

            _monsterListTable = new Table();
            _monsterListTable.Top().Left();

            _scrollPane = new ScrollPane(_monsterListTable, skin);
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
                _windowVisible = false;
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

            var monsters = manager.AlliedMonsters;
            for (int i = 0; i < monsters.Count; i++)
            {
                var monster = monsters[i];
                var nameLabel  = new Label($"{monster.Name} ({monster.MonsterTypeName})", _skin);
                var statsLabel = new Label($"  Battle:{monster.BattleProficiency}  Cooking:{monster.CookingProficiency}  Farming:{monster.FarmingProficiency}", _skin);
                _monsterListTable.Add(nameLabel).Left().SetPadTop(6f).SetPadBottom(2f);
                _monsterListTable.Row();
                _monsterListTable.Add(statsLabel).Left().SetPadBottom(4f);
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

        /// <summary>Updates the monster UI each frame.</summary>
        public void Update()
        {
            if (_windowVisible) PositionWindow();
        }
    }
}
