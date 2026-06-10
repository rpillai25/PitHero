using Microsoft.Xna.Framework;
using Nez;
using Nez.UI;
using PitHero.Services;
using RolePlayingFramework.AlliedMonsters;

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

        // Reference to SettingsUI for single window policy enforcement
        private SettingsUI _settingsUI;
        private SecondChanceShopUI _secondChanceShopUI;

        private enum MonsterMode { Normal, Half }
        private MonsterMode _currentMonsterMode = MonsterMode.Normal;

        private const float SpriteSize = 32f;
        private static readonly Color BrownColor = new Color(71, 36, 7);
        private static LabelStyle BrownStyle() => new LabelStyle { Font = Graphics.Instance.BitmapFont, FontColor = BrownColor };

        /// <summary>Whether the monster window is currently visible.</summary>
        public bool IsWindowVisible => _windowVisible;
        
        public MonsterUI()
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

            _monsterButton = new HoverableImageButton(_monsterNormalStyle, GetText(TextType.UI, UITextKey.WindowMonsters));
            _monsterButton.SetSize(sprite.SourceRect.Width, sprite.SourceRect.Height);
            _monsterButton.OnClicked += (button) => TriggerToggle();
        }

        /// <summary>Sets the reference to SettingsUI for single window policy enforcement.</summary>
        public void SetSettingsUI(SettingsUI settingsUI) { _settingsUI = settingsUI; }

        /// <summary>Sets the reference to SecondChanceShopUI for single window policy enforcement.</summary>
        public void SetSecondChanceShopUI(SecondChanceShopUI secondChanceShopUI) { _secondChanceShopUI = secondChanceShopUI; }

        /// <summary>
        /// Handles the monster button click - enforces single window policy and toggles the monster window
        /// </summary>
        public void TriggerToggle()
        {
            // Properly close Settings UI if it's open (single window policy)
            _settingsUI?.ForceCloseSettings();
            _secondChanceShopUI?.ForceCloseWindow();
            ToggleMonsterWindow();
        }

        private void CreateMonsterWindow(Skin skin)
        {
            _monsterWindow = new Window(GetText(TextType.UI, UITextKey.WindowMonsters), skin);
            _monsterWindow.SetSize(460f, 280f);

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
                _monsterListTable.Add(new Label("No allied monsters yet.", BrownStyle())).Left().SetPadBottom(4f);
                _monsterListTable.Row();
                return;
            }

            // Load atlases once for sprite lookups
            Nez.Sprites.SpriteAtlas actorsAtlas = null;
            try
            {
                actorsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/Actors.atlas");
            }
            catch (System.Exception ex)
            {
                Debug.Warn($"[MonsterUI] Failed to load Actors.atlas: {ex.Message}");
            }

            Nez.Sprites.SpriteAtlas uiAtlas = null;
            try
            {
                uiAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/UI.atlas");
            }
            catch (System.Exception ex)
            {
                Debug.Warn($"[MonsterUI] Failed to load UI.atlas: {ex.Message}");
            }

            var monsters = manager.AlliedMonsters;
            const float CellSize = 48f;

            for (int i = 0; i < monsters.Count; i++)
            {
                var monster = monsters[i];

                // Build a row: [48x48 sprite cell] [textTable] [jobTable]
                var rowTable = new Table();
                rowTable.Left();

                // --- Left: monster sprite (frame 0 of MoveDown animation) ---
                Nez.Textures.Sprite monsterSprite = null;
                if (actorsAtlas != null)
                {
                    try
                    {
                        string typeName = monster.MonsterTypeName.StartsWith("Monster_")
                            ? monster.MonsterTypeName.Substring("Monster_".Length)
                            : monster.MonsterTypeName;
                        var anim = actorsAtlas.GetAnimation($"{typeName}MoveDown");
                        if (anim?.Sprites != null && anim.Sprites.Length > 0)
                            monsterSprite = anim.Sprites[0];
                    }
                    catch (System.Exception) { monsterSprite = null; }
                }

                if (monsterSprite != null)
                {
                    var spriteImage = new Image(new SpriteDrawable(monsterSprite), Nez.UI.Scaling.Fit);
                    rowTable.Add(spriteImage).Size(CellSize, CellSize).Pad(2f, 2f, 2f, 4f);
                }
                else
                {
                    rowTable.Add(new Label("?", BrownStyle())).Size(CellSize, CellSize).Pad(2f, 2f, 2f, 4f);
                }

                // --- Middle: textTable with name, stats, job ---
                var textTable = new Table();
                textTable.Top().Left();

                var monsterTypeName = GetText(TextType.Monster, monster.MonsterTypeName);
                string jobDisplayName = monster.Job switch
                {
                    MonsterJob.Farming => "Farming",
                    MonsterJob.Cooking => "Cooking",
                    MonsterJob.Fishing => "Fishing",
                    _ => "None"
                };

                var nameLabel  = new Label($"{monster.Name} ({monsterTypeName})", BrownStyle());
                var statsLabel = new Label($"Fish:{monster.FishingProficiency}  Cook:{monster.CookingProficiency}  Farm:{monster.FarmingProficiency}", BrownStyle());

                textTable.Add(nameLabel).Left();
                textTable.Row();
                textTable.Add(statsLabel).Left();

                rowTable.Add(textTable).Left().Pad(2f, 0f, 2f, 4f);

                // --- Right: jobTable with current job label + 4 job buttons ---
                var jobTable = new Table();
                jobTable.Top().Right();

                var jobHeaderLabel = new Label($"Job: {jobDisplayName}", BrownStyle());
                jobTable.Add(jobHeaderLabel).Right().SetPadBottom(2f);
                jobTable.Row();

                // Row of 4 job buttons
                var buttonsTable = new Table();
                buttonsTable.Left();

                MonsterJob[] jobs = { MonsterJob.None, MonsterJob.Farming, MonsterJob.Cooking, MonsterJob.Fishing };
                string[] jobNames = { "None", "Farming", "Cooking", "Fishing" };
                string[] spriteNames = { "JobNone", "JobFarming", "JobCooking", "JobFishing" };

                // Capture for closure
                var capturedMonster = monster;

                for (int j = 0; j < jobs.Length; j++)
                {
                    var jobValue = jobs[j];
                    var spriteName = spriteNames[j];
                    var jobName = jobNames[j];

                    Nez.Textures.Sprite jobSprite = null;
                    if (uiAtlas != null)
                    {
                        try { jobSprite = uiAtlas.GetSprite(spriteName); }
                        catch (System.Exception) { jobSprite = null; }
                    }

                    if (jobSprite != null)
                    {
                        bool isSelected = (capturedMonster.Job == jobValue);
                        var btnStyle = new ImageButtonStyle
                        {
                            ImageUp = new SpriteDrawable(jobSprite)
                        };
                        var jobBtn = new HoverableImageButton(btnStyle, jobName);
                        jobBtn.GetImage().SetColor(isSelected
                            ? Color.White
                            : new Color(128, 128, 128, 200));

                        var closuredMonster = capturedMonster;
                        var closuredJob = jobValue;
                        jobBtn.OnClicked += (_) =>
                        {
                            closuredMonster.Job = closuredJob;
                            RefreshMonsterList();
                        };

                        buttonsTable.Add(jobBtn).Size(32f, 32f).Pad(1f);
                    }
                    else
                    {
                        var fallbackLabel = new Label(jobName.Substring(0, 1), BrownStyle());
                        buttonsTable.Add(fallbackLabel).Size(32f, 32f).Pad(1f);
                    }
                }

                jobTable.Add(buttonsTable).Right();

                rowTable.Add(jobTable).Right().SetExpandX().Pad(2f, 0f, 2f, 2f);

                _monsterListTable.Add(rowTable).Left().SetExpandX().SetFillX().Pad(4f, 2f, 0f, 2f);
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

