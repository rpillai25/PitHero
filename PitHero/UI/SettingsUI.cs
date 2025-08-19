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

        // Window positioning controls
        private Slider _yOffsetSlider;
        private TextButton _dockTopButton;
        private TextButton _dockBottomButton;
        private Label _yOffsetLabel;

        private Game _game;
        private int _currentYOffset = 0;
        private bool _isDockedTop = false;
        private bool _isDockedBottom = true; // Default to bottom dock

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

            // Create main table for layout
            _mainTable = new Table();
            _mainTable.SetFillParent(true);
            _stage.AddElement(_mainTable);

            // Create gear button with UIGear sprite
            CreateGearButton(skin);

            // Create settings TabPane (initially hidden)
            CreateSettingsWindow(skin);

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
            _settingsWindow.SetSize(400, 300);
            
            // Create tabbed interface - we'll use buttons for tab switching in the future
            var mainContainer = new Table();
            
            // Tab header area (for future expansion)
            var tabHeader = new Table();
            tabHeader.Add(new Label("Window", skin)).SetPadBottom(10);
            mainContainer.Add(tabHeader).SetExpandX().SetFillX();
            mainContainer.Row();
            
            // Tab content area
            var windowContent = CreateWindowTab(skin);
            mainContainer.Add(windowContent).Expand().Fill();
            
            _settingsWindow.Add(mainContainer).Expand().Fill();

            // Initially hidden
            _settingsWindow.SetVisible(false);
        }

        private Table CreateWindowTab(Skin skin)
        {
            var windowTable = new Table();
            windowTable.Pad(20);

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
            windowTable.Add(_dockBottomButton).Width(150);

            return windowTable;
        }

        private void LayoutUI()
        {
            _mainTable.Clear();

            // Create a new row for the gear button and settings window
            _mainTable.Add().SetExpandX(); // Take up left space

            if (_isVisible)
            {
                // Add settings window to the left of gear button in the same row
                _mainTable.Add(_settingsWindow).Right().SetPadRight(0); // 32 for gear + 32 for window offset
            }

            // Always add gear button in the same cell, right edge
            _mainTable.Add(_gearButton).Right().SetPadRight(16);

            _mainTable.Row();
        }

        private void ToggleSettingsVisibility()
        {
            _isVisible = !_isVisible;
            _settingsWindow.SetVisible(_isVisible);
            
            // Re-layout to show/hide the TabPane
            LayoutUI();
        }

        private void DockTop()
        {
            _isDockedTop = true;
            _isDockedBottom = false;
            
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
            
            // Reset Y offset and update slider range for bottom dock (-200 to 0)
            _currentYOffset = 0;
            UpdateSliderRange(-200, 0);
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
        }

        /// <summary>
        /// Updates the UI (called from scene update if needed)
        /// </summary>
        public void Update()
        {
            // Handle any per-frame UI updates if needed
        }
    }
}