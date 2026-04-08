using Microsoft.Xna.Framework;
using Nez;
using Nez.Systems;
using Nez.UI;
using PitHero.ECS.Components;
using PitHero.ECS.Scenes;
using PitHero.Services;
using System;

namespace PitHero.UI
{
    /// <summary>
    /// Modular settings UI that creates a gear button and settings TabPane using Nez.UI
    /// </summary>
    public class SettingsUI
    {
        private Stage _stage;
        private Table _mainTable;
        private HoverableImageButton _gearButton;
        private Window _settingsWindow;
        private bool _isVisible = false;

        // Tab management using actual TabPane
        private TabPane _tabPane;
        private Tab _windowTab;
        private Tab _sessionTab;
        private Tab _buttonsTab;

        // Window positioning controls
        private EnhancedSlider _yOffsetSlider;
        private EnhancedSlider _zoomSlider;
        private TextButton _dockTopButton;
        private TextButton _dockBottomButton;
        private TextButton _dockCenterButton;
        private Label _yOffsetLabel;
        private Label _zoomLabel;
        private TextButton _resetZoomButton;

        // Window size radio buttons
        private ButtonGroup _windowSizeButtonGroup;
        private CheckBox _normalSizeButton;
        private CheckBox _halfSizeButton;

        // New Window tab controls
        private TextButton _swapMonitorButton;
        private CheckBox _alwaysOnTopCheckBox;
        private CheckBox _autoScrollToHeroCheckBox;

        // Session tab controls
        private TextButton _saveButton;
        private TextButton _quitToTitleButton;
        private TextButton _exitButton;
        private SaveLoadUI _saveLoadUI;

        // Buttons tab controls (Replenish thresholds)
        private EnhancedSlider _replenishHPSlider;
        private EnhancedSlider _replenishMPSlider;
        private Label _replenishHPThresholdLabel;
        private Label _replenishMPThresholdLabel;

        // Confirmation dialogs
        private Window _exitConfirmationDialog;
        private Window _quitToTitleConfirmationDialog;

        private Game _game;
        private TextService _textService;
        private int _currentYOffset = 0;
        private bool _isDockedTop = false;
        private bool _isDockedBottom = true; // Default to bottom dock
        private bool _isDockedCenter = false;
        private bool _alwaysOnTop = true; // Track current always-on-top state

        // Smooth scrolling animation
        private bool _isAnimatingToOffset = false;
        private float _animationStartOffset;
        private float _animationTargetOffset;
        private float _animationDuration = 0.3f; // 300ms
        private float _animationTimer = 0f;

        // Track previous shrink mode so we can restore it after closing settings
        private bool _prevWasHalfShrink = false;
        private bool _prevWasQuarterShrink = false;

        // Add these fields
        private float _lastStageW = -1f;
        private float _lastStageH = -1f;

        // Gear button style variants
        private ImageButtonStyle _gearNormalStyle;
        private ImageButtonStyle _gearHalfStyle;
        private enum GearMode { Normal, Half }
        private GearMode _currentGearMode = GearMode.Normal;
        private bool _gearStyleChanged = false; // track size changes for gear button

        // New UI components
        private FastFUI _fastFUI;
        private HeroUI _heroUI;
        private MonsterUI _monsterUI;
        private RecruitmentNotificationUI _recruitmentNotificationUI;
        private StopAdventuringUI _stopAdventuringUI;
        private ReplenishUI _replenishUI;

        // Keyboard shortcut state
        private Microsoft.Xna.Framework.Input.KeyboardState _prevKeyboardState;

        /// <summary>Gets the HeroUI instance.</summary>
        public HeroUI HeroUI => _heroUI;

        // Window size modes
        public enum WindowSizeMode
        {
            Normal,
            Half
        }

        // Track desired size only during settings session (gets reset when settings open)
        private WindowSizeMode _desiredWindowSize = WindowSizeMode.Normal;

        public SettingsUI(Game game)
        {
            _game = game;
        }

        /// <summary>
        /// Initializes the UI components and adds them to the stage
        /// </summary>
        public void InitializeUI(Stage stage)
        {
            _stage = stage;

            // Initialize centralized UI window manager early so all UI components can use it
            UIWindowManager.Initialize(_game);

            // Initialize hover text manager
            HoverTextManager.Initialize(_stage);

            // Use PitHero skin
            var skin = PitHeroSkin.CreateSkin();

            // Create main table for layout (kept to avoid breaking other UI expectations)
            _mainTable = new Table();
            _mainTable.SetFillParent(true);
            _stage.AddElement(_mainTable);

            // Create gear button with UIGear sprite
            CreateGearButton(skin);

            // Create FastF and Hero UI components
            _fastFUI = new FastFUI();
            _fastFUI.InitializeUI(_stage);

            _heroUI = new HeroUI();
            _heroUI.InitializeUI(_stage);
            _heroUI.SetSettingsUI(this);

            _monsterUI = new MonsterUI();
            _monsterUI.InitializeUI(_stage);
            _monsterUI.SetSettingsUI(this);

            _recruitmentNotificationUI = new RecruitmentNotificationUI();
            _recruitmentNotificationUI.InitializeUI(_stage, skin);

            _stopAdventuringUI = new StopAdventuringUI();
            _stopAdventuringUI.InitializeUI(_stage);

            _replenishUI = new ReplenishUI();
            _replenishUI.InitializeUI(_stage);

            // Create settings window with TabPane (initially hidden)
            CreateSettingsWindow(skin);

            // Ensure both elements are added directly to the stage for absolute positioning
            _stage.AddElement(_gearButton);
            _stage.AddElement(_settingsWindow);

            // Set up initial layout
            LayoutUI();
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

        private void CreateGearButton(Skin skin)
        {
            // Load the UI atlas and get the gear sprites
            var uiAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/UI.atlas");
            var gearSprite = uiAtlas.GetSprite("UIGear");
            var gearSprite2x = uiAtlas.GetSprite("UIGear2x");
            var gearHighlight = uiAtlas.GetSprite("UIGearHighlight");
            var gearHighlight2x = uiAtlas.GetSprite("UIGearHighlight2x");
            var gearInverse = uiAtlas.GetSprite("UIGearInverse");
            var gearInverse2x = uiAtlas.GetSprite("UIGearInverse2x");

            // Base styles for each sprite with proper ImageDown and ImageOver
            _gearNormalStyle = new ImageButtonStyle
            {
                ImageUp = new SpriteDrawable(gearSprite),
                ImageDown = new SpriteDrawable(gearInverse),
                ImageOver = new SpriteDrawable(gearHighlight)
            };

            _gearHalfStyle = new ImageButtonStyle
            {
                ImageUp = new SpriteDrawable(gearSprite2x),
                ImageDown = new SpriteDrawable(gearInverse2x),
                ImageOver = new SpriteDrawable(gearHighlight2x)
            };

            _gearButton = new HoverableImageButton(_gearNormalStyle, "Settings");
            // Explicitly size to the image (avoids hard-coded magic numbers)
            _gearButton.SetSize(gearSprite.SourceRect.Width, gearSprite.SourceRect.Height);

            // Handle click to toggle settings visibility
            _gearButton.OnClicked += (button) => ToggleSettingsVisibility();
        }

        private void UpdateGearButtonStyleIfNeeded()
        {
            // Determine desired gear mode based on current shrink mode
            GearMode desired;
            if (WindowManager.IsHalfHeightMode())
                desired = GearMode.Half;
            else
                desired = GearMode.Normal;

            if (desired == _currentGearMode)
                return; // no change needed

            switch (desired)
            {
                case GearMode.Normal:
                    _gearButton.SetStyle(_gearNormalStyle);
                    _gearButton.SetSize(((SpriteDrawable)_gearNormalStyle.ImageUp).Sprite.SourceRect.Width, ((SpriteDrawable)_gearNormalStyle.ImageUp).Sprite.SourceRect.Height);
                    break;
                case GearMode.Half:
                    _gearButton.SetStyle(_gearHalfStyle);
                    _gearButton.SetSize(((SpriteDrawable)_gearHalfStyle.ImageUp).Sprite.SourceRect.Width, ((SpriteDrawable)_gearHalfStyle.ImageUp).Sprite.SourceRect.Height);
                    break;
            }

            _currentGearMode = desired;
            _gearStyleChanged = true; // trigger layout update
        }

        private void CreateSettingsWindow(Skin skin)
        {
            // Create settings window with TabPane
            var windowStyle = skin.Get<WindowStyle>("ph-default");
            _settingsWindow = new Window("", windowStyle); // Empty title since tabs provide context
            _settingsWindow.Pad(0); // Remove all window padding so tabs are flush with edges
            _settingsWindow.SetSize(450, 350);

            // Create TabPane with proper styling
            var tabWindowStyle = skin.Get<TabWindowStyle>("ph-default"); // Use PitHero's custom tab window style
            _tabPane = new TabPane(tabWindowStyle);

            // Create tabs with content
            var tabStyle = CreateTabStyle(skin);
            _windowTab = new Tab(GetText(TextType.UI, UITextKey.TabWindow), tabStyle);
            _sessionTab = new Tab(GetText(TextType.UI, UITextKey.TabSession), tabStyle);
            _buttonsTab = new Tab(GetText(TextType.UI, UITextKey.TabButtons), tabStyle);

            // Add content to tabs
            PopulateWindowTab(_windowTab, skin);
            PopulateSessionTab(_sessionTab, skin);
            PopulateButtonsTab(_buttonsTab, skin);

            // Add tabs to TabPane
            _tabPane.AddTab(_windowTab);
            _tabPane.AddTab(_sessionTab);
            _tabPane.AddTab(_buttonsTab);

            // Add TabPane to settings window
            _settingsWindow.Add(_tabPane).Expand().Fill().Pad(0); // No cell padding - tabs flush with window edges

            // Initially hidden
            _settingsWindow.SetVisible(false);

            // Create confirmation dialogs (initially hidden)
            CreateConfirmationDialogs(skin);
        }

        /// <summary>
        /// Creates TabStyle for individual tabs
        /// </summary>
        private TabStyle CreateTabStyle(Skin skin)
        {
            return new TabStyle
            {
                Background = null // Use transparent background
            };
        }

        /// <summary>
        /// Populates the Window tab with its content
        /// </summary>
        private void PopulateWindowTab(Tab windowTab, Skin skin)
        {
            // Create scroll pane container
            var scrollContent = new Table();

            // Always On Top checkbox
            _alwaysOnTopCheckBox = new CheckBox(GetText(TextType.UI, UITextKey.SettingsAlwaysOnTop), skin, "ph-default");
            _alwaysOnTopCheckBox.IsChecked = _alwaysOnTop;
            _alwaysOnTopCheckBox.OnChanged += (isChecked) =>
            {
                _alwaysOnTop = isChecked;
                WindowManager.SetAlwaysOnTop(_game, _alwaysOnTop);
            };
            scrollContent.Add(_alwaysOnTopCheckBox).Left().SetPadBottom(15);
            scrollContent.Row();

            // Auto-scroll to Hero checkbox
            _autoScrollToHeroCheckBox = new CheckBox(GetText(TextType.UI, UITextKey.SettingsAutoScrollToHero), skin, "ph-default");
            _autoScrollToHeroCheckBox.IsChecked = UIWindowManager.AutoScrollToHeroEnabled;
            _autoScrollToHeroCheckBox.OnChanged += (isChecked) =>
            {
                UIWindowManager.SetAutoScrollToHero(isChecked);
            };
            scrollContent.Add(_autoScrollToHeroCheckBox).Left().SetPadBottom(15);
            scrollContent.Row();

            // Swap Monitor button
            _swapMonitorButton = new TextButton(GetText(TextType.UI, UITextKey.SettingsSwapMonitor), skin, "ph-default");
            _swapMonitorButton.OnClicked += (button) =>
            {
                WindowManager.SwapToNextMonitor(_game);
                // Reapply current docking after monitor swap
                ApplyCurrentWindowPosition();
            };
            scrollContent.Add(_swapMonitorButton).Width(100f).Height(24f).SetPadBottom(15);
            scrollContent.Row();

            // Y Offset slider (left-aligned label)
            _yOffsetLabel = new Label(string.Format(GetText(TextType.UI, UITextKey.SettingsYOffset), 0), skin, "ph-default");
            scrollContent.Add(_yOffsetLabel).Left().SetPadBottom(10);
            scrollContent.Row();


            // Create table for y offset slider and reset button side by side
            var yOffsetTable = new Table();

            // Create enhanced slider with initial range for bottom dock
            _yOffsetSlider = new EnhancedSlider(-200, 0, 1, false, skin, null, false);
            _yOffsetSlider.SetValueAndCommit(0);

            // Update label during dragging (immediate feedback)
            _yOffsetSlider.OnChanged += (value) =>
            {
                _yOffsetLabel.SetText(string.Format(GetText(TextType.UI, UITextKey.SettingsYOffset), (int)value));
            };

            // Apply window position when value is committed (mouse released)
            _yOffsetSlider.OnValueCommitted += (value) =>
            {
                _currentYOffset = (int)value;
                StartSmoothScrollToOffset(_currentYOffset);
            };

            yOffsetTable.Add(_yOffsetSlider).Width(240).SetPadRight(10);

            // Reset Y Offset button
            var resetYOffsetButton = new TextButton(GetText(TextType.UI, UITextKey.ButtonReset), skin, "ph-default");
            resetYOffsetButton.OnClicked += (button) =>
            {
                _yOffsetSlider.SetValueAndCommit(0);
                _yOffsetLabel.SetText(string.Format(GetText(TextType.UI, UITextKey.SettingsYOffset), 0));
            };
            yOffsetTable.Add(resetYOffsetButton).Width(50).Height(16f);

            scrollContent.Add(yOffsetTable).SetPadBottom(20);
            scrollContent.Row();

            // Zoom level slider with reset button (left-aligned label)
            _zoomLabel = new Label(string.Format(GetText(TextType.UI, UITextKey.SettingsZoom), GameConfig.CameraDefaultZoom.ToString("F2")), skin, "ph-default");
            scrollContent.Add(_zoomLabel).Left().SetPadBottom(10);
            scrollContent.Row();

            // Create table for zoom slider and reset button side by side
            var zoomTable = new Table();

            // Create zoom slider (immediate mode since camera should update in real-time)
            _zoomSlider = new EnhancedSlider(GameConfig.CameraMinimumZoom, GameConfig.CameraMaximumZoom, 0.125f, false, skin, null, false);
            _zoomSlider.SetValueAndCommit(GameConfig.CameraDefaultZoom);

            // Update label and camera zoom immediately
            _zoomSlider.OnChanged += (value) =>
            {
                _zoomLabel.SetText(string.Format(GetText(TextType.UI, UITextKey.SettingsZoom), value.ToString("F2")));
            };

            _zoomSlider.OnValueCommitted += (value) =>
            {
                ApplyCameraZoom(value);
            };

            zoomTable.Add(_zoomSlider).Width(240).SetPadRight(10);

            // Reset zoom button
            _resetZoomButton = new TextButton(GetText(TextType.UI, UITextKey.ButtonReset), skin, "ph-default");
            _resetZoomButton.OnClicked += (button) =>
            {
                _zoomSlider.SetValueAndCommit(GameConfig.CameraDefaultZoom);
                _zoomLabel.SetText(string.Format(GetText(TextType.UI, UITextKey.SettingsZoom), GameConfig.CameraDefaultZoom.ToString("F2")));
                ApplyCameraZoom(GameConfig.CameraDefaultZoom);
            };

            zoomTable.Add(_resetZoomButton).Width(50).Height(16f);
            scrollContent.Add(zoomTable).SetPadBottom(20);
            scrollContent.Row();

            // Window Size radio buttons
            var windowSizeLabel = new Label(GetText(TextType.UI, UITextKey.SettingsWindowSize), skin, "ph-default");
            scrollContent.Add(windowSizeLabel).Left().SetPadBottom(10);
            scrollContent.Row();

            // Create ButtonGroup for window size radio buttons
            _windowSizeButtonGroup = new ButtonGroup();

            // Create radio buttons using CheckBox with "ph-default" style
            _normalSizeButton = new CheckBox(GetText(TextType.UI, UITextKey.SettingsWindowSizeNormal), skin, "ph-default");
            _halfSizeButton = new CheckBox(GetText(TextType.UI, UITextKey.SettingsWindowSizeHalf), skin, "ph-default");

            // Add buttons to ButtonGroup
            _windowSizeButtonGroup.Add(_normalSizeButton);
            _windowSizeButtonGroup.Add(_halfSizeButton);

            // Set up event handlers for window size changes - update persistent size
            _normalSizeButton.OnChanged += (isChecked) =>
            {
                if (isChecked)
                {
                    UIWindowManager.SetPersistentWindowSize(UIWindowManager.WindowSizeMode.Normal);
                    _desiredWindowSize = WindowSizeMode.Normal;
                    _zoomSlider?.SetValueAndCommit(GameConfig.CameraDefaultZoom);
                    Debug.Log("[SettingsUI] Selected Normal window size; reset zoom to 1x");
                }
            };

            _halfSizeButton.OnChanged += (isChecked) =>
            {
                if (isChecked)
                {
                    UIWindowManager.SetPersistentWindowSize(UIWindowManager.WindowSizeMode.Half);
                    _desiredWindowSize = WindowSizeMode.Half;
                    _zoomSlider?.SetValueAndCommit(GameConfig.CameraHalfSizeWindowZoom);
                    Debug.Log("[SettingsUI] Selected Half window size; applied 8x zoom");
                }
            };

            // Create table for radio buttons layout
            var windowSizeTable = new Table();
            windowSizeTable.Add(_normalSizeButton).SetPadRight(15);
            windowSizeTable.Add(_halfSizeButton);

            scrollContent.Add(windowSizeTable).Left().SetPadBottom(20);
            scrollContent.Row();

            // Dock buttons
            _dockTopButton = new TextButton(GetText(TextType.UI, UITextKey.SettingsDockTop), skin, "ph-default");
            _dockTopButton.OnClicked += (button) => DockTop();
            scrollContent.Add(_dockTopButton).Width(100f).Height(24f).SetPadBottom(10);
            scrollContent.Row();

            _dockBottomButton = new TextButton(GetText(TextType.UI, UITextKey.SettingsDockBottom), skin, "ph-default");
            _dockBottomButton.OnClicked += (button) => DockBottom();
            scrollContent.Add(_dockBottomButton).Width(100f).Height(24f).SetPadBottom(10);
            scrollContent.Row();

            _dockCenterButton = new TextButton(GetText(TextType.UI, UITextKey.SettingsDockCenter), skin, "ph-default");
            _dockCenterButton.OnClicked += (button) => DockCenter();
            scrollContent.Add(_dockCenterButton).Width(100f).Height(24f);

            // Create scroll pane with the content
            var scrollPane = new ScrollPane(scrollContent, skin, "ph-default");
            scrollPane.SetScrollingDisabled(true, false); // Only allow vertical scrolling
            scrollPane.SetFadeScrollBars(false); // Always show scroll bars when needed

            // Add scroll pane to the tab with padding on the cell, not the content
            windowTab.Add(scrollPane).Expand().Fill().Pad(20);
        }

        /// <summary>
        /// Populates the Session tab with its content
        /// </summary>
        private void PopulateSessionTab(Tab sessionTab, Skin skin)
        {
            var sessionTable = new Table();
            sessionTable.Pad(20);

            // Session label
            sessionTable.Add(new Label(GetText(TextType.UI, UITextKey.SettingsGameSession), skin, "ph-default")).Left().SetPadBottom(20);
            sessionTable.Row();

            // Save button
            _saveButton = new TextButton(GetText(TextType.UI, UITextKey.ButtonSave), skin, "ph-default");
            _saveButton.OnClicked += (button) =>
            {
                ShowSaveLoadUI(SaveLoadUI.Mode.Save);
            };
            sessionTable.Add(_saveButton).SetMinWidth(64f).Height(24f).SetPadBottom(15);
            sessionTable.Row();

            // Quit to Title button
            _quitToTitleButton = new TextButton(GetText(TextType.UI, UITextKey.ButtonQuitToTitle), skin, "ph-default");
            _quitToTitleButton.OnClicked += (button) => ShowQuitToTitleConfirmation();
            sessionTable.Add(_quitToTitleButton).SetMinWidth(64f).Height(24f).SetPadBottom(15);
            sessionTable.Row();

            // Exit button
            _exitButton = new TextButton(GetText(TextType.UI, UITextKey.ButtonExit), skin, "ph-default");
            _exitButton.OnClicked += (button) => ShowExitConfirmation();
            sessionTable.Add(_exitButton).SetMinWidth(64f).Height(24f);

            // Add content table to the tab
            sessionTab.Add(sessionTable).Expand().Fill().Top().Left();
        }

        /// <summary>
        /// Populates the Buttons tab with Replenish threshold sliders
        /// </summary>
        private void PopulateButtonsTab(Tab buttonsTab, Skin skin)
        {
            var buttonsTable = new Table();
            buttonsTable.Pad(20);

            // Replenish section
            var replenishLabel = new Label(GetText(TextType.UI, UITextKey.SettingsReplenishLabel), skin, "ph-default");
            buttonsTable.Add(replenishLabel).Left().SetPadBottom(10f);
            buttonsTable.Row();

            // HP Threshold slider row
            var hpSliderTable = new Table();
            _replenishHPThresholdLabel = new Label(string.Format(GetText(TextType.UI, UITextKey.SettingsHpThreshold), 90), skin, "ph-default");
            hpSliderTable.Add(_replenishHPThresholdLabel).SetPadRight(8);
            _replenishHPSlider = new EnhancedSlider(0, 100, 1, false, skin, null, false);
            _replenishHPSlider.SetValueAndCommit(90);
            _replenishHPSlider.OnChanged += (value) =>
            {
                _replenishHPThresholdLabel.SetText(string.Format(GetText(TextType.UI, UITextKey.SettingsHpThreshold), (int)value));
            };
            _replenishHPSlider.OnValueCommitted += (value) =>
            {
                var heroComp = GetHeroComponent();
                if (heroComp != null) heroComp.ReplenishHPThreshold = (int)value / 100f;
            };
            hpSliderTable.Add(_replenishHPSlider).Width(180);
            buttonsTable.Add(hpSliderTable).Left().SetPadBottom(8);
            buttonsTable.Row();

            // MP Threshold slider row
            var mpSliderTable = new Table();
            _replenishMPThresholdLabel = new Label(string.Format(GetText(TextType.UI, UITextKey.SettingsMpThreshold), 90), skin, "ph-default");
            mpSliderTable.Add(_replenishMPThresholdLabel).SetPadRight(8);
            _replenishMPSlider = new EnhancedSlider(0, 100, 1, false, skin, null, false);
            _replenishMPSlider.SetValueAndCommit(90);
            _replenishMPSlider.OnChanged += (value) =>
            {
                _replenishMPThresholdLabel.SetText(string.Format(GetText(TextType.UI, UITextKey.SettingsMpThreshold), (int)value));
            };
            _replenishMPSlider.OnValueCommitted += (value) =>
            {
                var heroComp = GetHeroComponent();
                if (heroComp != null) heroComp.ReplenishMPThreshold = (int)value / 100f;
            };
            mpSliderTable.Add(_replenishMPSlider).Width(180);
            buttonsTable.Add(mpSliderTable).Left();

            buttonsTab.Add(buttonsTable).Expand().Top().Left();
        }

        /// <summary>
        /// Gets the HeroComponent from the hero entity
        /// </summary>
        private HeroComponent GetHeroComponent()
        {
            var heroEntity = Core.Scene?.FindEntity("hero");
            return heroEntity?.GetComponent<HeroComponent>();
        }

        /// <summary>Shows the save/load UI in the specified mode.</summary>
        private void ShowSaveLoadUI(SaveLoadUI.Mode mode)
        {
            if (_saveLoadUI == null)
                _saveLoadUI = new SaveLoadUI();
            
            _saveLoadUI.Show(_stage, mode, () =>
            {
                // Callback when save/load UI is closed
                Debug.Log("[SettingsUI] Save/Load UI closed");
            });
        }

        private void CreateConfirmationDialogs(Skin skin)
        {
            var windowStyle = skin.Get<WindowStyle>("ph-default");

            // Exit confirmation dialog
            _exitConfirmationDialog = new Window("", windowStyle);
            _exitConfirmationDialog.SetSize(300, 150);

            var exitDialogTable = new Table();
            exitDialogTable.Pad(20);

            exitDialogTable.Add(new Label(GetText(TextType.UI, UITextKey.ConfirmExitMessage), skin, "ph-default")).SetPadBottom(20);
            exitDialogTable.Row();

            var exitButtonTable = new Table();

            var exitYesButton = new TextButton(GetText(TextType.UI, UITextKey.ButtonYes), skin, "ph-default");
            exitYesButton.OnClicked += (button) =>
            {
                HideConfirmationDialog(_exitConfirmationDialog);
                Core.Exit();
            };
            exitButtonTable.Add(exitYesButton).Width(80).Height(24).SetPadRight(10);

            var exitNoButton = new TextButton(GetText(TextType.UI, UITextKey.ButtonNo), skin, "ph-default");
            exitNoButton.OnClicked += (button) => HideConfirmationDialog(_exitConfirmationDialog);
            exitButtonTable.Add(exitNoButton).Width(80).Height(24);

            exitDialogTable.Add(exitButtonTable);

            _exitConfirmationDialog.Add(exitDialogTable).Expand().Fill();
            _exitConfirmationDialog.SetVisible(false);

            _exitConfirmationDialog.SetPosition(
                (_stage.GetWidth() - _exitConfirmationDialog.GetWidth()) / 2,
                (_stage.GetHeight() - _exitConfirmationDialog.GetHeight()) / 2
            );

            // Quit to Title confirmation dialog
            _quitToTitleConfirmationDialog = new Window("", windowStyle);
            _quitToTitleConfirmationDialog.SetSize(300, 150);

            var quitToTitleDialogTable = new Table();
            quitToTitleDialogTable.Pad(20);

            quitToTitleDialogTable.Add(new Label(GetText(TextType.UI, UITextKey.ConfirmQuitToTitleMessage), skin, "ph-default")).SetPadBottom(20);
            quitToTitleDialogTable.Row();

            var quitToTitleButtonTable = new Table();

            var quitToTitleYesButton = new TextButton(GetText(TextType.UI, UITextKey.ButtonYes), skin, "ph-default");
            quitToTitleYesButton.OnClicked += (button) =>
            {
                HideConfirmationDialog(_quitToTitleConfirmationDialog);
                var pauseService = Core.Services.GetService<PauseService>();
                if (pauseService != null)
                    pauseService.Unpause();
                Time.TimeScale = 1f;
                Core.GetGlobalManager<CoroutineManager>().StopAllCoroutines();
                Core.Scene = new TitleScreenScene();
            };
            quitToTitleButtonTable.Add(quitToTitleYesButton).Width(80).Height(24).SetPadRight(10);

            var quitToTitleNoButton = new TextButton(GetText(TextType.UI, UITextKey.ButtonNo), skin, "ph-default");
            quitToTitleNoButton.OnClicked += (button) => HideConfirmationDialog(_quitToTitleConfirmationDialog);
            quitToTitleButtonTable.Add(quitToTitleNoButton).Width(80).Height(24);

            quitToTitleDialogTable.Add(quitToTitleButtonTable);

            _quitToTitleConfirmationDialog.Add(quitToTitleDialogTable).Expand().Fill();
            _quitToTitleConfirmationDialog.SetVisible(false);

            _quitToTitleConfirmationDialog.SetPosition(
                (_stage.GetWidth() - _quitToTitleConfirmationDialog.GetWidth()) / 2,
                (_stage.GetHeight() - _quitToTitleConfirmationDialog.GetHeight()) / 2
            );
        }

        private void ShowExitConfirmation()
        {
            ShowConfirmationDialog(_exitConfirmationDialog);
        }

        private void ShowQuitToTitleConfirmation()
        {
            ShowConfirmationDialog(_quitToTitleConfirmationDialog);
        }

        private void ShowConfirmationDialog(Window dialog)
        {
            _settingsWindow.Validate();
            dialog.Validate();

            float winX = _settingsWindow.GetX();
            float winY = _settingsWindow.GetY();
            float dialogW = dialog.GetWidth();
            float dialogH = dialog.GetHeight();

            const float padding = 8f;
            float targetX = winX - dialogW - padding;
            float targetY = winY;

            float stageW = _stage.GetWidth();
            float stageH = _stage.GetHeight();
            if (targetX < 0) targetX = 0;
            if (targetY < 0) targetY = 0;
            if (targetY + dialogH > stageH) targetY = stageH - dialogH;

            dialog.SetPosition(targetX, targetY);

            if (dialog.GetStage() == null)
                _stage.AddElement(dialog);

            dialog.SetVisible(true);
            dialog.ToFront();
        }

        private void HideConfirmationDialog(Window dialog)
        {
            dialog.SetVisible(false);
        }

        /// <summary>Applies visibility and positions gear/settings window</summary>
        private void LayoutUI()
        {
            if (_gearButton.GetStage() == null)
                _stage.AddElement(_gearButton);

            if (_settingsWindow.GetStage() == null)
                _stage.AddElement(_settingsWindow);

            _settingsWindow.SetVisible(_isVisible);
            PositionUI();
        }

        private void PositionUI()
        {
            float stageW = _stage.GetWidth();
            float stageH = _stage.GetHeight();

            float gearW    = _gearButton.GetWidth();
            float gearH    = _gearButton.GetHeight();
            float fastFW   = _fastFUI.GetWidth();
            float heroW    = _heroUI.GetWidth();
            float monsterW = _monsterUI.GetWidth();
            float stopW    = _stopAdventuringUI.GetWidth();
            float replenishW = _replenishUI.GetWidth();

            // Calculate total width needed for all six buttons with padding
            float totalWidth = replenishW + stopW + fastFW + gearW + heroW + monsterW + (5 * GameConfig.UIButtonPadding);

            // Center all buttons as a group
            float startX = (stageW - totalWidth) * 0.5f;
            float buttonY = 2f;

            // Position Replenish button leftmost
            float replenishX = startX;
            _replenishUI.SetPosition(replenishX, buttonY);

            // Position Stop Adventuring button directly to the right of Replenish
            float stopX = replenishX + replenishW + GameConfig.UIButtonPadding;
            _stopAdventuringUI.SetPosition(stopX, buttonY);

            // Position FastF button directly to the right of Stop
            float fastFX = stopX + stopW + GameConfig.UIButtonPadding;
            _fastFUI.SetPosition(fastFX, buttonY);

            // Position gear button
            float gearX = fastFX + fastFW + GameConfig.UIButtonPadding;
            _gearButton.SetPosition(gearX, buttonY);

            // Position hero button
            float heroX = gearX + gearW + GameConfig.UIButtonPadding;
            _heroUI.SetPosition(heroX, buttonY);

            // Position monster button to the right of hero
            float monsterX = heroX + heroW + GameConfig.UIButtonPadding;
            _monsterUI.SetPosition(monsterX, buttonY);

            if (_isVisible)
            {
                const float padding = 4f;
                float winW = _settingsWindow.GetWidth();
                float winH = _settingsWindow.GetHeight();

                float winX = gearX + gearW + padding;
                float winY = buttonY + padding;

                if (winX + winW > stageW)
                    winX = gearX - padding - winW;

                if (winX < 0) winX = 0;
                if (winY < 0) winY = 0;
                if (winY + winH > stageH) winY = stageH - winH;

                _settingsWindow.SetPosition(winX, winY);
            }

            _lastStageW = stageW;
            _lastStageH = stageH;
        }

        /// <summary>
        /// Toggles settings visibility. When opening, remembers shrink mode and restores full size. When closing, applies persistent size.
        /// </summary>
        public void ToggleSettingsVisibility()
        {
            bool willShow = !_isVisible;
            if (willShow)
            {
                // Close HeroUI window if it's open before opening settings (single window policy)
                if (_heroUI != null && _heroUI.IsWindowVisible)
                {
                    _heroUI.ForceCloseWindow();
                    Debug.Log("[SettingsUI] Closed HeroUI window to enforce single window policy");
                }

                // Close MonsterUI window if it's open before opening settings (single window policy)
                if (_monsterUI != null && _monsterUI.IsWindowVisible)
                {
                    _monsterUI.ForceCloseWindow();
                    Debug.Log("[SettingsUI] Closed MonsterUI window to enforce single window policy");
                }

                // Use centralized UI window manager for opening behavior
                UIWindowManager.OnUIWindowOpening();

                // Update zoom slider to reflect current camera zoom
                UpdateZoomSliderFromCamera();

                // Set radio buttons to reflect persistent size (not current temporary size)
                UpdateRadioButtonsFromPersistentSize();
            }
            else
            {
                // Use centralized UI window manager for closing behavior
                UIWindowManager.OnUIWindowClosing();

                // Hide any open confirmation dialogs
                HideConfirmationDialog(_exitConfirmationDialog);
                HideConfirmationDialog(_quitToTitleConfirmationDialog);
            }

            _isVisible = willShow;
            _settingsWindow.SetVisible(_isVisible);
            var pauseService = Core.Services.GetService<PauseService>();
            if (pauseService != null)
                pauseService.IsPaused = _isVisible;
            if (_isVisible)
                _settingsWindow.ToFront();
            LayoutUI();
        }

        /// <summary>
        /// Force closes the settings window and updates internal state.
        /// Called by other UIs (HeroUI, MonsterUI) when enforcing the single window policy.
        /// </summary>
        public void ForceCloseSettings()
        {
            if (_isVisible)
            {
                _isVisible = false;
                UIWindowManager.OnUIWindowClosing();
                HideConfirmationDialog(_exitConfirmationDialog);
                HideConfirmationDialog(_quitToTitleConfirmationDialog);
                _settingsWindow.SetVisible(false);
                var pauseService = Core.Services.GetService<PauseService>();
                if (pauseService != null)
                    pauseService.IsPaused = false;
                LayoutUI();
                Debug.Log("[SettingsUI] Settings force closed by single window policy");
            }
        }

        /// <summary>
        /// Apply the persistent window size when settings UI is closed
        /// </summary>
        private void ApplyPersistentWindowSize()
        {
            // Delegate to centralized UI window manager
            UIWindowManager.ApplyPersistentWindowSize();
        }

        /// <summary>
        /// Apply the desired window size when settings UI is closed
        /// </summary>
        private void ApplyDesiredWindowSize()
        {
            // This method is now replaced by centralized UIWindowManager
            UIWindowManager.OnUIWindowClosing();
        }

        /// <summary>
        /// Update window size radio buttons to reflect persistent window size (legacy method)
        /// </summary>
        private void UpdateWindowSizeButtons()
        {
            UpdateRadioButtonsFromPersistentSize();
        }

        /// <summary>
        /// Update window size radio buttons and desired size to reflect persistent window state (legacy method)
        /// </summary>
        private void UpdateWindowSizeButtonsAndDesiredSize()
        {
            UpdateRadioButtonsFromPersistentSize();
        }

        /// <summary>
        /// Update window size radio buttons based on persistent window size (not current window state)
        /// </summary>
        private void UpdateRadioButtonsFromPersistentSize()
        {
            if (_windowSizeButtonGroup == null) return;

            // Set radio buttons based on persistent size, not current window state
            var persistentSize = UIWindowManager.PersistentWindowSize;
            switch (persistentSize)
            {
                case UIWindowManager.WindowSizeMode.Half:
                    _halfSizeButton.IsChecked = true;
                    _desiredWindowSize = WindowSizeMode.Half;
                    break;
                case UIWindowManager.WindowSizeMode.Normal:
                default:
                    _normalSizeButton.IsChecked = true;
                    _desiredWindowSize = WindowSizeMode.Normal;
                    break;
            }

            Debug.Log($"[SettingsUI] Updated radio buttons to reflect persistent size: {persistentSize}");
        }

        /// <summary>
        /// Processes keyboard shortcut presses and dispatches to the appropriate UI actions.
        /// </summary>
        private void HandleKeyboardShortcuts()
        {
            var currentKeyState = Microsoft.Xna.Framework.Input.Keyboard.GetState();

            if (_stage.GetKeyboardFocus() != null)
            {
                _prevKeyboardState = currentKeyState;
                return;
            }

            bool JustPressed(Microsoft.Xna.Framework.Input.Keys key)
                => currentKeyState.IsKeyDown(key) && !_prevKeyboardState.IsKeyDown(key);

            if (JustPressed(Microsoft.Xna.Framework.Input.Keys.R))
                _replenishUI?.TriggerReplenish();

            if (JustPressed(Microsoft.Xna.Framework.Input.Keys.S))
                _stopAdventuringUI?.TriggerToggle();

            if (JustPressed(Microsoft.Xna.Framework.Input.Keys.F))
                _fastFUI?.TriggerToggle();

            if (JustPressed(Microsoft.Xna.Framework.Input.Keys.E))
                ToggleSettingsVisibility();

            if (JustPressed(Microsoft.Xna.Framework.Input.Keys.H))
                _heroUI?.TriggerToggle();

            if (JustPressed(Microsoft.Xna.Framework.Input.Keys.I))
                _heroUI?.OpenToInventoryTab();

            if (JustPressed(Microsoft.Xna.Framework.Input.Keys.N))
                _heroUI?.OpenToHeroInfoTab();

            if (JustPressed(Microsoft.Xna.Framework.Input.Keys.B))
                _heroUI?.OpenToBehaviorTab();

            if (JustPressed(Microsoft.Xna.Framework.Input.Keys.M))
                _monsterUI?.TriggerToggle();

            _prevKeyboardState = currentKeyState;
        }

        /// <summary>
        /// Updates the UI, including button styles based on shrink mode
        /// </summary>
        public void Update()
        {
            // Update smooth scrolling animation
            UpdateSmoothScrolling();

            // Update gear button style dynamically when shrink mode changes
            UpdateGearButtonStyleIfNeeded();

            // Update FastF and Hero button styles
            _fastFUI?.Update();
            _heroUI?.Update();
            _monsterUI?.Update();
            _recruitmentNotificationUI?.Update();
            _stopAdventuringUI?.Update();
            _replenishUI?.Update();

            // Update persistent size if window size changed externally (e.g., Shift+Mouse Wheel)
            if (!_isVisible) // Only update when settings are closed
            {
                UIWindowManager.UpdatePersistentWindowSizeIfChanged();
            }

            // Reposition only if any button size changed or stage dimensions changed
            bool needsReposition = _gearStyleChanged;
            if (_fastFUI != null && _fastFUI.ConsumeStyleChangedFlag()) needsReposition = true;
            if (_heroUI != null && _heroUI.ConsumeStyleChangedFlag()) needsReposition = true;
            if (_monsterUI != null && _monsterUI.ConsumeStyleChangedFlag()) needsReposition = true;
            if (_stopAdventuringUI != null && _stopAdventuringUI.ConsumeStyleChangedFlag()) needsReposition = true;
            if (_replenishUI != null && _replenishUI.ConsumeStyleChangedFlag()) needsReposition = true;

            if (_stage.GetWidth() != _lastStageW || _stage.GetHeight() != _lastStageH)
                needsReposition = true;

            if (needsReposition)
            {
                PositionUI();
                _gearStyleChanged = false;
            }

            HandleKeyboardShortcuts();
        }

        /// <summary>
        /// Updates persistent window size if it changed externally (e.g., via Shift+Mouse Wheel)
        /// </summary>
        private void UpdatePersistentWindowSizeIfChanged()
        {
            // Delegate to centralized UI window manager
            UIWindowManager.UpdatePersistentWindowSizeIfChanged();
        }

        private void DockTop()
        {
            _isDockedTop = true;
            _isDockedBottom = false;
            _isDockedCenter = false;
            _currentYOffset = 0;
            UpdateSliderRange(0, 200);
            _yOffsetSlider.SetValueAndCommit(0);
            _yOffsetLabel.SetText(string.Format(GetText(TextType.UI, UITextKey.SettingsYOffset), 0));
            ApplyCurrentWindowPosition();
        }

        private void DockBottom()
        {
            _isDockedTop = false;
            _isDockedBottom = true;
            _isDockedCenter = false;
            _currentYOffset = 0;
            UpdateSliderRange(-200, 0);
            _yOffsetSlider.SetValueAndCommit(0);
            _yOffsetLabel.SetText(string.Format(GetText(TextType.UI, UITextKey.SettingsYOffset), 0));
            ApplyCurrentWindowPosition();
        }

        private void DockCenter()
        {
            _isDockedTop = false;
            _isDockedBottom = false;
            _isDockedCenter = true;
            _currentYOffset = 0;
            UpdateSliderRange(-200, 200);
            _yOffsetSlider.SetValueAndCommit(0);
            _yOffsetLabel.SetText(string.Format(GetText(TextType.UI, UITextKey.SettingsYOffset), 0));
            ApplyCurrentWindowPosition();
        }

        private void UpdateSliderRange(float min, float max)
        {
            _yOffsetSlider.SetMinMax(min, max);
        }

        /// <summary>
        /// Starts smooth scrolling animation to the target offset
        /// </summary>
        private void StartSmoothScrollToOffset(int targetOffset)
        {
            if (_isAnimatingToOffset)
            {
                // If already animating, update the target
                _animationTargetOffset = targetOffset;
            }
            else
            {
                _animationStartOffset = _currentYOffset;
                _animationTargetOffset = targetOffset;
                _animationTimer = 0f;
                _isAnimatingToOffset = true;
            }
        }

        /// <summary>
        /// Updates smooth scrolling animation
        /// </summary>
        private void UpdateSmoothScrolling()
        {
            if (!_isAnimatingToOffset)
                return;

            _animationTimer += Time.DeltaTime;
            float progress = Math.Min(1f, _animationTimer / _animationDuration);

            // Use easing for smooth animation (ease out)
            float easedProgress = 1f - (1f - progress) * (1f - progress);

            float currentOffset = _animationStartOffset + (_animationTargetOffset - _animationStartOffset) * easedProgress;

            // Apply the interpolated position
            int roundedOffset = (int)Math.Round(currentOffset);
            if (_isDockedTop)
            {
                WindowManager.DockTop(_game, roundedOffset);
            }
            else if (_isDockedBottom)
            {
                WindowManager.DockBottom(_game, roundedOffset);
            }
            else if (_isDockedCenter)
            {
                WindowManager.DockCenter(_game, roundedOffset);
            }

            // Check if animation is complete
            if (progress >= 1f)
            {
                _isAnimatingToOffset = false;
                _currentYOffset = (int)_animationTargetOffset;
            }
        }

        private void ApplyCurrentWindowPosition()
        {
            // Stop any ongoing animation and apply immediately
            _isAnimatingToOffset = false;

            if (_isDockedTop)
            {
                WindowManager.DockTop(_game, _currentYOffset);
            }
            else if (_isDockedBottom)
            {
                WindowManager.DockBottom(_game, _currentYOffset);
            }
            else if (_isDockedCenter)
            {
                WindowManager.DockCenter(_game, _currentYOffset);
            }
        }

        /// <summary>
        /// Apply camera zoom to the current scene's camera
        /// </summary>
        private void ApplyCameraZoom(float zoomLevel)
        {
            // Find the camera in the current scene
            var currentScene = Core.Scene;
            if (currentScene?.Camera != null)
            {
                currentScene.Camera.RawZoom = zoomLevel;
                Debug.Log($"[SettingsUI] Applied camera zoom: {zoomLevel:F2}x");
            }
            else
            {
                Debug.Warn("[SettingsUI] Could not find camera to apply zoom");
            }
        }

        /// <summary>
        /// Update zoom slider to reflect current camera zoom level
        /// </summary>
        private void UpdateZoomSliderFromCamera()
        {
            var currentScene = Core.Scene;
            if (currentScene?.Camera != null && _zoomSlider != null)
            {
                var currentZoom = currentScene.Camera.RawZoom;
                _zoomSlider.SetValueAndCommit(currentZoom);
                _zoomLabel.SetText(string.Format(GetText(TextType.UI, UITextKey.SettingsZoom), currentZoom.ToString("F2")));
                Debug.Log($"[SettingsUI] Updated zoom slider to current camera zoom: {currentZoom:F2}x");
            }
        }
    }
}