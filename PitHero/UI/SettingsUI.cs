using Microsoft.Xna.Framework;
using Nez;
using Nez.UI;

namespace PitHero.UI
{
    /// <summary>
    /// Modular settings UI that creates a gear button and settings TabPane using Nez.UI
    /// </summary>
    public class SettingsUI
    {
        private Stage _stage;
        private Table _mainTable;
        private Button _gearButton;
        private Window _settingsWindow;
        private bool _isVisible = false;

        // Tab management
        private Table _tabButtonsTable;
        private Table _tabContentTable;
        private TextButton _windowTabButton;
        private TextButton _sessionTabButton;
        private Table _windowTabContent;
        private Table _sessionTabContent;
        private bool _isWindowTabActive = true;

        // Window positioning controls
        private Slider _yOffsetSlider;
        private TextButton _dockTopButton;
        private TextButton _dockBottomButton;
        private TextButton _dockCenterButton;
        private Label _yOffsetLabel;
        
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

        // Add these fields
        private float _lastStageW = -1f;
        private float _lastStageH = -1f;

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

            // Use default skin
            var skin = Skin.CreateDefaultSkin();

            // Create main table for layout (kept to avoid breaking other UI expectations)
            _mainTable = new Table();
            _mainTable.SetFillParent(true);
            _stage.AddElement(_mainTable);

            // Create gear button with UIGear sprite
            CreateGearButton(skin);

            // Create settings TabPane (initially hidden)
            CreateSettingsWindow(skin);

            // Ensure both elements are added directly to the stage for absolute positioning
            _stage.AddElement(_gearButton);
            _stage.AddElement(_settingsWindow);

            // Set up initial layout
            LayoutUI();
        }

        private void CreateGearButton(Skin skin)
        {
            // Load the UI atlas and get the UIGear sprite
            var uiAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/UI.atlas");
            var gearSprite = uiAtlas.GetSprite("UIGear");

            // Create button with gear image
            var buttonStyle = new ImageButtonStyle();
            buttonStyle.ImageUp = new SpriteDrawable(gearSprite);
            buttonStyle.ImageDown = new SpriteDrawable(gearSprite);
            buttonStyle.ImageOver = new SpriteDrawable(gearSprite);

            _gearButton = new ImageButton(buttonStyle);
            _gearButton.SetSize(32, 32);

            // Handle click to toggle settings visibility
            _gearButton.OnClicked += (button) => ToggleSettingsVisibility();
        }

        private void CreateSettingsWindow(Skin skin)
        {
            // Create settings window with TabPane-style interface
            var windowStyle = skin.Get<WindowStyle>();
            _settingsWindow = new Window("Settings", windowStyle);
            _settingsWindow.SetSize(450, 350);
            
            var mainContainer = new Table();
            
            // Create tab buttons
            _tabButtonsTable = new Table();
            _windowTabButton = new TextButton("Window", skin);
            _sessionTabButton = new TextButton("Session", skin);
            
            _windowTabButton.OnClicked += (button) => SwitchToTab(true);
            _sessionTabButton.OnClicked += (button) => SwitchToTab(false);
            
            _tabButtonsTable.Add(_windowTabButton).Width(100).SetPadRight(5);
            _tabButtonsTable.Add(_sessionTabButton).Width(100);
            
            mainContainer.Add(_tabButtonsTable).SetExpandX().SetFillX().SetPadBottom(10);
            mainContainer.Row();
            
            // Create content area
            _tabContentTable = new Table();
            
            // Create tab contents
            _windowTabContent = CreateWindowTab(skin);
            _sessionTabContent = CreateSessionTab(skin);
            
            mainContainer.Add(_tabContentTable).Expand().Fill();
            
            _settingsWindow.Add(mainContainer).Expand().Fill();

            // Initially show window tab
            SwitchToTab(true);

            // Initially hidden
            _settingsWindow.SetVisible(false);
            
            // Create confirmation dialog (initially hidden)
            CreateConfirmationDialog(skin);
        }

        private Table CreateWindowTab(Skin skin)
        {
            var windowTable = new Table();
            windowTable.Pad(20);

            // Always On Top checkbox
            _alwaysOnTopCheckBox = new CheckBox("Always On Top", skin);
            _alwaysOnTopCheckBox.IsChecked = _alwaysOnTop;
            _alwaysOnTopCheckBox.OnChanged += (isChecked) => {
                _alwaysOnTop = isChecked;
                WindowManager.SetAlwaysOnTop(_game, _alwaysOnTop);
            };
            windowTable.Add(_alwaysOnTopCheckBox).Left().SetPadBottom(15);
            windowTable.Row();

            // Swap Monitor button
            _swapMonitorButton = new TextButton("Swap Monitor", skin);
            _swapMonitorButton.OnClicked += (button) => {
                WindowManager.SwapToNextMonitor(_game);
                // Reapply current docking after monitor swap
                ApplyCurrentWindowPosition();
            };
            windowTable.Add(_swapMonitorButton).Width(200).SetPadBottom(15);
            windowTable.Row();

            // Separator
            windowTable.Add(new Label("Window Position:", skin)).Left().SetPadBottom(10);
            windowTable.Row();

            // Y Offset slider
            _yOffsetLabel = new Label("Y Offset: 0", skin);
            windowTable.Add(_yOffsetLabel).SetPadBottom(10);
            windowTable.Row();

            // Create slider with initial range for bottom dock
            _yOffsetSlider = new Slider(-200, 0, 1, false, skin);
            _yOffsetSlider.SetValue(0);
            _yOffsetSlider.OnChanged += (value) => {
                _currentYOffset = (int)value;
                _yOffsetLabel.SetText($"Y Offset: {_currentYOffset}");
                ApplyCurrentWindowPosition();
            };
            windowTable.Add(_yOffsetSlider).Width(300).SetPadBottom(20);
            windowTable.Row();

            // Dock buttons
            _dockTopButton = new TextButton("Dock Top", skin);
            _dockTopButton.OnClicked += (button) => DockTop();
            windowTable.Add(_dockTopButton).Width(150).SetPadBottom(10);
            windowTable.Row();

            _dockBottomButton = new TextButton("Dock Bottom", skin);
            _dockBottomButton.OnClicked += (button) => DockBottom();
            windowTable.Add(_dockBottomButton).Width(150).SetPadBottom(10);
            windowTable.Row();

            _dockCenterButton = new TextButton("Dock Center", skin);
            _dockCenterButton.OnClicked += (button) => DockCenter();
            windowTable.Add(_dockCenterButton).Width(150);

            return windowTable;
        }

        private Table CreateSessionTab(Skin skin)
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
                // Placeholder - will eventually save game state
                Debug.Log("Save functionality not yet implemented");
            };
            sessionTable.Add(_saveButton).Width(150).SetPadBottom(15);
            sessionTable.Row();

            // Quit button
            _quitButton = new TextButton("Quit", skin);
            _quitButton.OnClicked += (button) => ShowQuitConfirmation();
            sessionTable.Add(_quitButton).Width(150);

            return sessionTable;
        }

        private void CreateConfirmationDialog(Skin skin)
        {
            var windowStyle = skin.Get<WindowStyle>();
            _confirmationDialog = new Window("Really Quit?", windowStyle);
            _confirmationDialog.SetSize(300, 150);
            
            var dialogTable = new Table();
            dialogTable.Pad(20);
            
            // Message
            dialogTable.Add(new Label("Are you sure you want to quit?", skin)).SetPadBottom(20);
            dialogTable.Row();
            
            // Button row
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
            
            // Center the dialog when shown
            _confirmationDialog.SetPosition(
                (_stage.GetWidth() - _confirmationDialog.GetWidth()) / 2,
                (_stage.GetHeight() - _confirmationDialog.GetHeight()) / 2
            );
        }

        private void ShowQuitConfirmation()
        {
            // Ensure layout is up to date so GetX/GetY/GetWidth/GetHeight are valid
            _settingsWindow.Validate();
            _sessionTabButton.Validate();
            _confirmationDialog.Validate();

            // Base window (Settings) position in stage coordinates
            float winX = _settingsWindow.GetX();
            float winY = _settingsWindow.GetY();

            // Session tab position relative to window
            float tabX = _sessionTabButton.GetX();
            float tabY = _sessionTabButton.GetY();
            float tabH = _sessionTabButton.GetHeight();

            // Convert to stage coordinates
            float sessionTabStageX = winX + tabX;
            float sessionTabStageY = winY + tabY;

            // Desired dialog position: directly left of the Session tab
            const float padding = 8f;
            float dialogW = _confirmationDialog.GetWidth();
            float dialogH = _confirmationDialog.GetHeight();

            float targetX = sessionTabStageX - dialogW - padding;
            float targetY = sessionTabStageY + (tabH - dialogH) / 2f;

            // Clamp inside stage bounds
            if (targetX < 0) targetX = 0;
            if (targetY < 0) targetY = 0;
            float stageW = _stage.GetWidth();
            float stageH = _stage.GetHeight();
            if (targetX + dialogW > stageW) targetX = stageW - dialogW;
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

        private void SwitchToTab(bool showWindowTab)
        {
            _isWindowTabActive = showWindowTab;
            
            // Clear current content
            _tabContentTable.Clear();
            
            // Show appropriate content
            if (showWindowTab)
            {
                _tabContentTable.Add(_windowTabContent).Expand().Fill();
                // Update button states to show active tab
                _windowTabButton.SetDisabled(true);
                _sessionTabButton.SetDisabled(false);
            }
            else
            {
                _tabContentTable.Add(_sessionTabContent).Expand().Fill();
                // Update button states to show active tab
                _windowTabButton.SetDisabled(false);
                _sessionTabButton.SetDisabled(true);
            }
        }

        /// <summary>Applies visibility and positions gear/settings window</summary>
        private void LayoutUI()
        {
            // Keep elements on the stage
            if (_gearButton.GetStage() == null)
                _stage.AddElement(_gearButton);

            if (_settingsWindow.GetStage() == null)
                _stage.AddElement(_settingsWindow);

            // Apply visibility and position
            _settingsWindow.SetVisible(_isVisible);
            PositionUI();
        }

        private void PositionUI()
        {
            float stageW = _stage.GetWidth();
            float stageH = _stage.GetHeight();

            // Gear size
            float gearW = _gearButton.GetWidth();
            float gearH = _gearButton.GetHeight();

            // Top middle: (middleX, 4px down from top)
            float gearX = (stageW - gearW) * 0.5f;
            float gearY = 2f;
            _gearButton.SetPosition(gearX, gearY);

            if (_isVisible)
            {
                // Settings window directly to the right of the gear
                const float padding = 4f;
                float winW = _settingsWindow.GetWidth();
                float winH = _settingsWindow.GetHeight();

                float winX = gearX + gearW + padding;
                float winY = gearY + padding;

                // If it overflows to the right, show it to the left of the gear
                if (winX + winW > stageW)
                    winX = gearX - padding - winW;

                // Clamp inside stage vertically and horizontally
                if (winX < 0) winX = 0;
                if (winY < 0) winY = 0;
                if (winY + winH > stageH) winY = stageH - winH;

                _settingsWindow.SetPosition(winX, winY);
            }

            _lastStageW = stageW;
            _lastStageH = stageH;
        }

        /// <summary>Toggles the settings window visibility and repositions UI</summary>
        private void ToggleSettingsVisibility()
        {
            _isVisible = !_isVisible;
            _settingsWindow.SetVisible(_isVisible);
            if (_isVisible)
                _settingsWindow.ToFront();

            LayoutUI();
        }

        private void DockTop()
        {
            _isDockedTop = true;
            _isDockedBottom = false;
            _isDockedCenter = false;
            
            // Reset Y offset and update slider range for top dock (0 to 200)
            _currentYOffset = 0;
            UpdateSliderRange(0, 200);
            _yOffsetSlider.SetValue(0);
            _yOffsetLabel.SetText("Y Offset: 0");
            
            ApplyCurrentWindowPosition();
        }

        private void DockBottom()
        {
            _isDockedTop = false;
            _isDockedBottom = true;
            _isDockedCenter = false;
            
            // Reset Y offset and update slider range for bottom dock (-200 to 0)
            _currentYOffset = 0;
            UpdateSliderRange(-200, 0);
            _yOffsetSlider.SetValue(0);
            _yOffsetLabel.SetText("Y Offset: 0");
            
            ApplyCurrentWindowPosition();
        }

        private void DockCenter()
        {
            _isDockedTop = false;
            _isDockedBottom = false;
            _isDockedCenter = true;
            
            // Reset Y offset and update slider range for center dock (-200 to 200)
            _currentYOffset = 0;
            UpdateSliderRange(-200, 200);
            _yOffsetSlider.SetValue(0);
            _yOffsetLabel.SetText("Y Offset: 0");
            
            ApplyCurrentWindowPosition();
        }

        private void UpdateSliderRange(float min, float max)
        {
            // Update slider range using SetMinMax method
            _yOffsetSlider.SetMinMax(min, max);
        }

        private void ApplyCurrentWindowPosition()
        {
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
        /// Updates the UI (called from scene update if needed)
        /// Leaving this here but doing nothing, because for now windows size never changes
        /// </summary>
        public void Update()
        {
            // Re-center on window resize
            //float stageW = _stage.GetWidth();
            //float stageH = _stage.GetHeight();
            //if (stageW != _lastStageW || stageH != _lastStageH)
            //    PositionUI();
        }
    }
}