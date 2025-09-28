using Microsoft.Xna.Framework;
using Nez;
using Nez.UI;
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
        private CheckBox _quarterSizeButton;
        
        // New Window tab controls
        private TextButton _swapMonitorButton;
        private CheckBox _alwaysOnTopCheckBox;
        
        // Session tab controls
        private TextButton _saveButton;
        private TextButton _quitButton;

        // Confirmation dialog
        private Window _confirmationDialog;

        private Game _game;
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
        private ImageButtonStyle _gearQuarterStyle;
        private enum GearMode { Normal, Half, Quarter }
        private GearMode _currentGearMode = GearMode.Normal;
        private bool _gearStyleChanged = false; // track size changes for gear button

        // New UI components
        private FastFUI _fastFUI;
        private HeroUI _heroUI;
        
        // Window size modes
        private enum WindowSizeMode
        {
            Normal,
            Half,
            Quarter
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

            // Use default skin
            var skin = Skin.CreateDefaultSkin();

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

            // Create settings window with TabPane (initially hidden)
            CreateSettingsWindow(skin);

            // Ensure both elements are added directly to the stage for absolute positioning
            _stage.AddElement(_gearButton);
            _stage.AddElement(_settingsWindow);

            // Set up initial layout
            LayoutUI();
        }

        private void CreateGearButton(Skin skin)
        {
            // Load the UI atlas and get the gear sprites
            var uiAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/UI.atlas");
            var gearSprite = uiAtlas.GetSprite("UIGear");
            var gearSprite2x = uiAtlas.GetSprite("UIGear2x");
            var gearSprite4x = uiAtlas.GetSprite("UIGear4x");
            var gearHighlight = uiAtlas.GetSprite("UIGearHighlight");
            var gearHighlight2x = uiAtlas.GetSprite("UIGearHighlight2x");
            var gearHighlight4x = uiAtlas.GetSprite("UIGearHighlight4x");
            var gearInverse = uiAtlas.GetSprite("UIGearInverse");
            var gearInverse2x = uiAtlas.GetSprite("UIGearInverse2x");
            var gearInverse4x = uiAtlas.GetSprite("UIGearInverse4x");

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

            _gearQuarterStyle = new ImageButtonStyle
            {
                ImageUp = new SpriteDrawable(gearSprite4x),
                ImageDown = new SpriteDrawable(gearInverse4x),
                ImageOver = new SpriteDrawable(gearHighlight4x)
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
            if (WindowManager.IsQuarterHeightMode())
                desired = GearMode.Quarter;
            else if (WindowManager.IsHalfHeightMode())
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
                case GearMode.Quarter:
                    _gearButton.SetStyle(_gearQuarterStyle);
                    _gearButton.SetSize(((SpriteDrawable)_gearQuarterStyle.ImageUp).Sprite.SourceRect.Width, ((SpriteDrawable)_gearQuarterStyle.ImageUp).Sprite.SourceRect.Height);
                    break;
            }

            _currentGearMode = desired;
            _gearStyleChanged = true; // trigger layout update
        }

        private void CreateSettingsWindow(Skin skin)
        {
            // Create settings window with TabPane
            var windowStyle = skin.Get<WindowStyle>();
            _settingsWindow = new Window("Settings", windowStyle);
            _settingsWindow.SetSize(450, 350);
            
            // Create TabPane with proper styling
            var tabWindowStyle = CreateTabWindowStyle(skin);
            _tabPane = new TabPane(tabWindowStyle);
            
            // Create tabs with content
            var tabStyle = CreateTabStyle(skin);
            _windowTab = new Tab("Window", tabStyle);
            _sessionTab = new Tab("Session", tabStyle);
            
            // Add content to tabs
            PopulateWindowTab(_windowTab, skin);
            PopulateSessionTab(_sessionTab, skin);
            
            // Add tabs to TabPane
            _tabPane.AddTab(_windowTab);
            _tabPane.AddTab(_sessionTab);
            
            // Add TabPane to settings window
            _settingsWindow.Add(_tabPane).Expand().Fill();

            // Initially hidden
            _settingsWindow.SetVisible(false);
            
            // Create confirmation dialog (initially hidden)
            CreateConfirmationDialog(skin);
        }

        /// <summary>
        /// Creates TabWindowStyle for the TabPane
        /// </summary>
        private TabWindowStyle CreateTabWindowStyle(Skin skin)
        {
            var tabButtonStyle = new TabButtonStyle();
            tabButtonStyle.LabelStyle = skin.Get<LabelStyle>();
            
            // Use button styles for tab button states
            var buttonStyle = skin.Get<TextButtonStyle>();
            tabButtonStyle.Inactive = buttonStyle.Up;
            tabButtonStyle.Active = buttonStyle.Down;
            tabButtonStyle.Hover = buttonStyle.Over;
            tabButtonStyle.Locked = buttonStyle.Disabled;
            tabButtonStyle.PaddingTop = 2f;
            
            return new TabWindowStyle
            {
                Background = null, // Use window background instead
                TabButtonStyle = tabButtonStyle
            };
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
            scrollContent.Pad(20);

            // Always On Top checkbox
            _alwaysOnTopCheckBox = new CheckBox("Always On Top", skin);
            _alwaysOnTopCheckBox.IsChecked = _alwaysOnTop;
            _alwaysOnTopCheckBox.OnChanged += (isChecked) => {
                _alwaysOnTop = isChecked;
                WindowManager.SetAlwaysOnTop(_game, _alwaysOnTop);
            };
            scrollContent.Add(_alwaysOnTopCheckBox).Left().SetPadBottom(15);
            scrollContent.Row();

            // Swap Monitor button
            _swapMonitorButton = new TextButton("Swap Monitor", skin);
            _swapMonitorButton.OnClicked += (button) => {
                WindowManager.SwapToNextMonitor(_game);
                // Reapply current docking after monitor swap
                ApplyCurrentWindowPosition();
            };
            scrollContent.Add(_swapMonitorButton).Width(200).SetPadBottom(15);
            scrollContent.Row();

            // Y Offset slider (left-aligned label)
            _yOffsetLabel = new Label("Y Offset: 0", skin);
            scrollContent.Add(_yOffsetLabel).Left().SetPadBottom(10);
            scrollContent.Row();

            // Create enhanced slider with initial range for bottom dock
            _yOffsetSlider = new EnhancedSlider(-200, 0, 1, false, skin, null, false);
            _yOffsetSlider.SetValueAndCommit(0);
            
            // Update label during dragging (immediate feedback)
            _yOffsetSlider.OnChanged += (value) => {
                _yOffsetLabel.SetText($"Y Offset: {(int)value}");
            };
            
            // Apply window position when value is committed (mouse released)
            _yOffsetSlider.OnValueCommitted += (value) => {
                _currentYOffset = (int)value;
                StartSmoothScrollToOffset(_currentYOffset);
            };
            
            scrollContent.Add(_yOffsetSlider).Width(300).SetPadBottom(20);
            scrollContent.Row();

            // Zoom level slider with reset button (left-aligned label)
            _zoomLabel = new Label("Zoom: 1.00x", skin);
            scrollContent.Add(_zoomLabel).Left().SetPadBottom(10);
            scrollContent.Row();

            // Create table for zoom slider and reset button side by side
            var zoomTable = new Table();
            
            // Create zoom slider (immediate mode since camera should update in real-time)
            _zoomSlider = new EnhancedSlider(GameConfig.CameraMinimumZoom, GameConfig.CameraMaximumZoom, 0.125f, false, skin, null, false);
            _zoomSlider.SetValueAndCommit(GameConfig.CameraDefaultZoom);
            
            // Update label and camera zoom immediately
            _zoomSlider.OnChanged += (value) => {
                _zoomLabel.SetText($"Zoom: {value:F2}x");
            };
            
            _zoomSlider.OnValueCommitted += (value) => {
                ApplyCameraZoom(value);
            };
            
            zoomTable.Add(_zoomSlider).Width(240).SetPadRight(10);
            
            // Reset zoom button
            _resetZoomButton = new TextButton("Reset", skin);
            _resetZoomButton.OnClicked += (button) => {
                _zoomSlider.SetValueAndCommit(GameConfig.CameraDefaultZoom);
                _zoomLabel.SetText($"Zoom: {GameConfig.CameraDefaultZoom:F2}x");
                ApplyCameraZoom(GameConfig.CameraDefaultZoom);
            };
            
            zoomTable.Add(_resetZoomButton).Width(50);
            scrollContent.Add(zoomTable).SetPadBottom(20);
            scrollContent.Row();

            // Window Size radio buttons
            var windowSizeLabel = new Label("Window Size:", skin);
            scrollContent.Add(windowSizeLabel).Left().SetPadBottom(10);
            scrollContent.Row();

            // Create ButtonGroup for window size radio buttons
            _windowSizeButtonGroup = new ButtonGroup();
            
            // Create radio buttons using CheckBox
            _normalSizeButton = new CheckBox("Normal", skin);
            _halfSizeButton = new CheckBox("Half", skin);
            _quarterSizeButton = new CheckBox("Quarter", skin);
            
            // Add buttons to ButtonGroup
            _windowSizeButtonGroup.Add(_normalSizeButton);
            _windowSizeButtonGroup.Add(_halfSizeButton);
            _windowSizeButtonGroup.Add(_quarterSizeButton);
            
            // Set up event handlers for window size changes - update persistent size
            _normalSizeButton.OnChanged += (isChecked) => {
                if (isChecked) 
                {
                    UIWindowManager.SetPersistentWindowSize(UIWindowManager.WindowSizeMode.Normal);
                    _desiredWindowSize = WindowSizeMode.Normal;
                    Debug.Log("[SettingsUI] Selected Normal window size");
                }
            };
            
            _halfSizeButton.OnChanged += (isChecked) => {
                if (isChecked) 
                {
                    UIWindowManager.SetPersistentWindowSize(UIWindowManager.WindowSizeMode.Half);
                    _desiredWindowSize = WindowSizeMode.Half;
                    Debug.Log("[SettingsUI] Selected Half window size");
                }
            };
            
            _quarterSizeButton.OnChanged += (isChecked) => {
                if (isChecked) 
                {
                    UIWindowManager.SetPersistentWindowSize(UIWindowManager.WindowSizeMode.Quarter);
                    _desiredWindowSize = WindowSizeMode.Quarter;
                    Debug.Log("[SettingsUI] Selected Quarter window size");
                }
            };
            
            // Create table for radio buttons layout
            var windowSizeTable = new Table();
            windowSizeTable.Add(_normalSizeButton).SetPadRight(15);
            windowSizeTable.Add(_halfSizeButton).SetPadRight(15);
            windowSizeTable.Add(_quarterSizeButton);
            
            scrollContent.Add(windowSizeTable).Left().SetPadBottom(20);
            scrollContent.Row();

            // Dock buttons
            _dockTopButton = new TextButton("Dock Top", skin);
            _dockTopButton.OnClicked += (button) => DockTop();
            scrollContent.Add(_dockTopButton).Width(150).SetPadBottom(10);
            scrollContent.Row();

            _dockBottomButton = new TextButton("Dock Bottom", skin);
            _dockBottomButton.OnClicked += (button) => DockBottom();
            scrollContent.Add(_dockBottomButton).Width(150).SetPadBottom(10);
            scrollContent.Row();

            _dockCenterButton = new TextButton("Dock Center", skin);
            _dockCenterButton.OnClicked += (button) => DockCenter();
            scrollContent.Add(_dockCenterButton).Width(150);

            // Create scroll pane with the content
            var scrollPane = new ScrollPane(scrollContent, skin);
            scrollPane.SetScrollingDisabled(true, false); // Only allow vertical scrolling
            scrollPane.SetFadeScrollBars(false); // Always show scroll bars when needed
            
            // Add scroll pane to the tab
            windowTab.Add(scrollPane).Expand().Fill();
        }

        /// <summary>
        /// Populates the Session tab with its content
        /// </summary>
        private void PopulateSessionTab(Tab sessionTab, Skin skin)
        {
            var sessionTable = new Table();
            sessionTable.Pad(20);

            // Session label
            sessionTable.Add(new Label("Game Session", skin)).Left().SetPadBottom(20);
            sessionTable.Row();

            // Save button (disabled placeholder)
            _saveButton = new TextButton("Save", skin);
            _saveButton.SetDisabled(true);
            _saveButton.OnClicked += (button) => {
                Debug.Log("Save functionality not yet implemented");
            };
            sessionTable.Add(_saveButton).Width(150).SetPadBottom(15);
            sessionTable.Row();

            // Quit button
            _quitButton = new TextButton("Quit", skin);
            _quitButton.OnClicked += (button) => ShowQuitConfirmation();
            sessionTable.Add(_quitButton).Width(150);

            // Add content table to the tab
            sessionTab.Add(sessionTable).Expand().Fill().Top().Left();
        }

        private void CreateConfirmationDialog(Skin skin)
        {
            var windowStyle = skin.Get<WindowStyle>();
            _confirmationDialog = new Window("Really Quit?", windowStyle);
            _confirmationDialog.SetSize(300, 150);
            
            var dialogTable = new Table();
            dialogTable.Pad(20);
            
            dialogTable.Add(new Label("Are you sure you want to quit?", skin)).SetPadBottom(20);
            dialogTable.Row();
            
            var buttonTable = new Table();
            
            var yesButton = new TextButton("Yes", skin);
            yesButton.OnClicked += (button) => {
                HideQuitConfirmation();
                Core.Exit();
            };
            buttonTable.Add(yesButton).Width(80).SetPadRight(10);
            
            var noButton = new TextButton("No", skin);
            noButton.OnClicked += (button) => HideQuitConfirmation();
            buttonTable.Add(noButton).Width(80);
            
            dialogTable.Add(buttonTable);
            
            _confirmationDialog.Add(dialogTable).Expand().Fill();
            _confirmationDialog.SetVisible(false);
            
            _confirmationDialog.SetPosition(
                (_stage.GetWidth() - _confirmationDialog.GetWidth()) / 2,
                (_stage.GetHeight() - _confirmationDialog.GetHeight()) / 2
            );
        }

        private void ShowQuitConfirmation()
        {
            _settingsWindow.Validate();
            _confirmationDialog.Validate();

            float winX = _settingsWindow.GetX();
            float winY = _settingsWindow.GetY();
            float dialogW = _confirmationDialog.GetWidth();
            float dialogH = _confirmationDialog.GetHeight();

            const float padding = 8f;
            float targetX = winX - dialogW - padding;
            float targetY = winY;

            float stageW = _stage.GetWidth();
            float stageH = _stage.GetHeight();
            if (targetX < 0) targetX = 0;
            if (targetY < 0) targetY = 0;
            if (targetY + dialogH > stageH) targetY = stageH - dialogH;

            _confirmationDialog.SetPosition(targetX, targetY);

            if (_confirmationDialog.GetStage() == null)
                _stage.AddElement(_confirmationDialog);

            _confirmationDialog.SetVisible(true);
            _confirmationDialog.ToFront();
        }

        private void HideQuitConfirmation()
        {
            _confirmationDialog.SetVisible(false);
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

            float gearW = _gearButton.GetWidth();
            float gearH = _gearButton.GetHeight();
            float fastFW = _fastFUI.GetWidth();
            float heroW = _heroUI.GetWidth();
            
            // Calculate total width needed for all three buttons with padding
            float totalWidth = fastFW + gearW + heroW + (2 * GameConfig.UIButtonPadding);
            
            // Center all buttons as a group
            float startX = (stageW - totalWidth) * 0.5f;
            float buttonY = 2f;

            // Position FastF button to the left
            float fastFX = startX;
            _fastFUI.SetPosition(fastFX, buttonY);

            // Position gear button in the center
            float gearX = fastFX + fastFW + GameConfig.UIButtonPadding;
            _gearButton.SetPosition(gearX, buttonY);

            // Position hero button to the right
            float heroX = gearX + gearW + GameConfig.UIButtonPadding;
            _heroUI.SetPosition(heroX, buttonY);

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
        private void ToggleSettingsVisibility()
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
                case UIWindowManager.WindowSizeMode.Quarter:
                    _quarterSizeButton.IsChecked = true;
                    _desiredWindowSize = WindowSizeMode.Quarter;
                    break;
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

            // Update persistent size if window size changed externally (e.g., Shift+Mouse Wheel)
            if (!_isVisible) // Only update when settings are closed
            {
                UIWindowManager.UpdatePersistentWindowSizeIfChanged();
            }

            // Reposition only if any button size changed or stage dimensions changed
            bool needsReposition = _gearStyleChanged;
            if (_fastFUI != null && _fastFUI.ConsumeStyleChangedFlag()) needsReposition = true;
            if (_heroUI != null && _heroUI.ConsumeStyleChangedFlag()) needsReposition = true;

            if (_stage.GetWidth() != _lastStageW || _stage.GetHeight() != _lastStageH)
                needsReposition = true;

            if (needsReposition)
            {
                PositionUI();
                _gearStyleChanged = false;
            }
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
            _yOffsetLabel.SetText("Y Offset: 0");
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
            _yOffsetLabel.SetText("Y Offset: 0");
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
            _yOffsetLabel.SetText("Y Offset: 0");
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
                _zoomLabel.SetText($"Zoom: {currentZoom:F2}x");
                Debug.Log($"[SettingsUI] Updated zoom slider to current camera zoom: {currentZoom:F2}x");
            }
        }
    }
}