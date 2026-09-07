using Nez;
using Nez.UI;
using PitHero.Services;

namespace PitHero.UI
{
    /// <summary>
    /// UI for FastF speed toggle button that controls game time scale
    /// </summary>
    public class FastFUI
    {
        private Stage _stage;
        private SpeedOverlayImageButton _fastFButton;
        private TextService _textService;

        private ImageButtonStyle _fastFNormalStyle;
        private ImageButtonStyle _fastFHalfStyle;

        //Button stays in pressed state
        private ImageButtonStyle _fastFNormalPressedStyle;
        private ImageButtonStyle _fastFHalfPressedStyle;

        private enum FastFMode { Normal, NormalPressed, Half, HalfPressed }
        private FastFMode _currentFastFMode = FastFMode.Normal;

        private bool _isSpeedUp = false; // Track current speed state
        private int _speedIndex = 0; // Rung into GameConfig.SpeedSteps; starts at 1X so no label shows until a speed is picked
        private bool _showLabel = false; // The rung is remembered while disengaged, but its label is not drawn
        private bool _styleChanged = false; // tracks when style (and thus size) changed

        public FastFUI()
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

        /// <summary>
        /// Initializes the FastF button and adds it to the stage
        /// </summary>
        public void InitializeUI(Stage stage)
        {
            _stage = stage;

            // Use PitHero skin
            var skin = PitHeroSkin.CreateSkin();

            // Create FastF button
            CreateFastFButton(skin);

            // Add button to stage
            _stage.AddElement(_fastFButton);
        }

        private void CreateFastFButton(Skin skin)
        {
            // Load the UI atlas and get the FastF sprites
            var uiAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/UI.atlas");
            var fastFSprite = uiAtlas.GetSprite("UIFastF");
            var fastFHighlight = uiAtlas.GetSprite("UIFastFHighlight");
            var fastFInverse = uiAtlas.GetSprite("UIFastFInverse");

            // Base styles for each sprite with proper ImageDown and ImageOver
            _fastFNormalStyle = new ImageButtonStyle
            {
                ImageUp = new SpriteDrawable(fastFSprite),
                ImageDown = new SpriteDrawable(fastFInverse),
                ImageOver = new SpriteDrawable(fastFHighlight)
            };

            _fastFNormalPressedStyle = new ImageButtonStyle
            {
                ImageUp = new SpriteDrawable(fastFInverse),
                ImageDown = new SpriteDrawable(fastFSprite),
                ImageOver = new SpriteDrawable(fastFHighlight)
            };

            _fastFHalfStyle = ButtonSprite2xFactory.CreateHalfStyle(uiAtlas, "UIFastF");

            // Pressed-half: Up/Down swapped relative to normal half style.
            var fastF2x      = ButtonSprite2xFactory.GetOrCreate2x(uiAtlas, "UIFastF");
            var fastFInv2x   = ButtonSprite2xFactory.GetOrCreate2x(uiAtlas, "UIFastFInverse");
            var fastFHigh2x  = ButtonSprite2xFactory.GetOrCreate2x(uiAtlas, "UIFastFHighlight");
            _fastFHalfPressedStyle = new ImageButtonStyle
            {
                ImageUp   = new SpriteDrawable(fastFInv2x),
                ImageDown = new SpriteDrawable(fastF2x),
                ImageOver = new SpriteDrawable(fastFHigh2x)
            };

            _fastFButton = new SpeedOverlayImageButton(_fastFNormalStyle, GetText(TextType.UI, UITextKey.ButtonFastForward));
            _fastFButton.ClickSoundCategory = ButtonClickCategory.TopBar;
            // Explicitly size to the image
            _fastFButton.SetSize(fastFSprite.SourceRect.Width, fastFSprite.SourceRect.Height);

            // Plain click only engages/disengages; SHIFT+click only changes the speed rung.
            // Read the keyboard directly rather than through Nez Input so the modifier is sampled at
            // click time regardless of where in the frame the stage dispatches the click.
            _fastFButton.OnClicked += (button) =>
            {
                var kb = Microsoft.Xna.Framework.Input.Keyboard.GetState();
                if (kb.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.LeftShift)
                    || kb.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.RightShift))
                    CycleSpeed();
                else
                    TriggerToggle();
            };
        }

        /// <summary>
        /// Toggles game speed between normal and fast forward. Fast forward runs more fixed simulation
        /// steps per rendered frame (Core.SimulationSpeed) rather than scaling Time.DeltaTime, so the
        /// simulation follows the exact same trajectory at either speed (replay determinism). The
        /// per-frame step cap is raised while engaged so the top rung is not silently clamped.
        /// </summary>
        public void TriggerToggle()
        {
            SetSpeedUp(!_isSpeedUp);
        }

        /// <summary>
        /// True once the Kairos Metronome is owned, which unlocks the 4X and 8X rungs. Without it the
        /// ladder tops out at 2X.
        /// </summary>
        public static bool HighSpeedRungsUnlocked
            => ArtifactService.Current != null && ArtifactService.Current.Owns(Artifacts.ArtifactType.KairosMetronome);

        /// <summary>
        /// Steps to the next speed rung: 2X -> 4X -> 8X -> 2X, or stays on 2X until the Kairos
        /// Metronome unlocks the higher rungs. This only picks the speed fast forward will run at;
        /// engaging and disengaging is the plain click's job alone. Rung 0 (1X, normal speed) is the
        /// untouched start state and is never cycled back into — every rung the player can select is
        /// an actual speed-up.
        /// </summary>
        public void CycleSpeed()
        {
            int topRung = HighSpeedRungsUnlocked
                ? GameConfig.SpeedSteps.Length - 1
                : GameConfig.SimulationDefaultSpeedIndex;

            _speedIndex = _speedIndex >= topRung
                ? GameConfig.SimulationDefaultSpeedIndex
                : _speedIndex + 1;
            _showLabel = true; // picking a speed shows it, engaged or not, so SHIFT+click has feedback
            ApplySpeed();
        }

        /// <summary>True while fast forward is engaged.</summary>
        public bool IsSpeedUp => _isSpeedUp;

        /// <summary>Current speed rung index into GameConfig.SpeedSteps.</summary>
        public int SpeedIndex => _speedIndex;

        /// <summary>
        /// Label drawn on the button face (2X / 4X / 8X), or null when nothing should be shown. It is
        /// hidden whenever the game drops back to normal speed, and comes back when fast forward is
        /// re-engaged or SHIFT+click picks a rung.
        /// </summary>
        public string SpeedLabel => _showLabel && _speedIndex > 0 ? GameConfig.SpeedStepLabels[_speedIndex] : null;

        /// <summary>Sets fast forward on or off explicitly (replay playback forces it off).</summary>
        public void SetSpeedUp(bool speedUp)
        {
            // Rung 0 is normal speed, so engaging there would leave the button latched doing nothing.
            // Engaging from 1X therefore picks the default rung; SHIFT+click is still the only way to
            // change which rung that is.
            if (speedUp && _speedIndex == 0)
                _speedIndex = GameConfig.SimulationDefaultSpeedIndex;

            _isSpeedUp = speedUp;
            _showLabel = speedUp; // back to normal speed: drop the label until a speed is chosen again
            ApplySpeed();
        }

        /// <summary>
        /// Pushes the current rung to the engine. Speed is extra fixed steps per frame, never a scaled
        /// delta; the step cap is raised alongside it so 8x is not clamped by the normal catch-up cap.
        /// </summary>
        private void ApplySpeed()
        {
            var speed = _isSpeedUp ? GameConfig.SpeedSteps[_speedIndex] : 1f;
            Core.SimulationSpeed = speed;
            Core.MaxStepsPerFrame = speed > 1f ? GameConfig.HighSpeedMaxStepsPerFrame : GameConfig.SimulationMaxStepsPerFrame;
        }

        /// <summary>
        /// Update button style based on current window shrink mode
        /// </summary>
        public void UpdateButtonStyleIfNeeded()
        {
            // Determine desired mode based on current shrink mode
            FastFMode desired;
            if (WindowManager.IsHalfHeightMode() && !_isSpeedUp)
                desired = FastFMode.Half;
            else if (WindowManager.IsHalfHeightMode() && _isSpeedUp)
                desired = FastFMode.HalfPressed;
            else if (!_isSpeedUp)
                desired = FastFMode.Normal;
            else
                desired = FastFMode.NormalPressed;

            if (desired == _currentFastFMode)
                return; // no change needed

            switch (desired)
            {
                case FastFMode.Normal:
                    _fastFButton.SetStyle(_fastFNormalStyle);
                    _fastFButton.SetSize(((SpriteDrawable)_fastFNormalStyle.ImageUp).Sprite.SourceRect.Width, ((SpriteDrawable)_fastFNormalStyle.ImageUp).Sprite.SourceRect.Height);
                    break;
                case FastFMode.NormalPressed:
                    _fastFButton.SetStyle(_fastFNormalPressedStyle);
                    _fastFButton.SetSize(((SpriteDrawable)_fastFNormalPressedStyle.ImageUp).Sprite.SourceRect.Width, ((SpriteDrawable)_fastFNormalPressedStyle.ImageUp).Sprite.SourceRect.Height);
                    break;
                case FastFMode.Half:
                    _fastFButton.SetStyle(_fastFHalfStyle);
                    _fastFButton.SetSize(((SpriteDrawable)_fastFHalfStyle.ImageUp).Sprite.SourceRect.Width, ((SpriteDrawable)_fastFHalfStyle.ImageUp).Sprite.SourceRect.Height);
                    break;
                case FastFMode.HalfPressed:
                    _fastFButton.SetStyle(_fastFHalfPressedStyle);
                    _fastFButton.SetSize(((SpriteDrawable)_fastFHalfPressedStyle.ImageUp).Sprite.SourceRect.Width, ((SpriteDrawable)_fastFHalfPressedStyle.ImageUp).Sprite.SourceRect.Height);
                    break;
            }

            _currentFastFMode = desired;
            _styleChanged = true; // mark for layout reposition
        }

        /// <summary>
        /// Position the button at the specified coordinates
        /// </summary>
        public void SetPosition(float x, float y)
        {
            _fastFButton?.SetPosition(x, y);
        }

        /// <summary>Enables/disables hit-testing; disabled while the top UI bar is hidden off-screen.</summary>
        public void SetTouchable(Touchable touchable)
        {
            _fastFButton?.SetTouchable(touchable);
        }

        /// <summary>
        /// Get the button width
        /// </summary>
        public float GetWidth()
        {
            return _fastFButton?.GetWidth() ?? 0f;
        }

        /// <summary>
        /// Get the button height
        /// </summary>
        public float GetHeight()
        {
            return _fastFButton?.GetHeight() ?? 0f;
        }

        /// <summary>
        /// Consume style changed flag (returns true if a style change occurred this frame)
        /// </summary>
        public bool ConsumeStyleChangedFlag()
        {
            if (_styleChanged)
            {
                _styleChanged = false;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Update method (can be called from main update loop if needed)
        /// </summary>
        public void Update()
        {
            UpdateButtonStyleIfNeeded();

            if (_fastFButton != null)
                _fastFButton.OverlayText = SpeedLabel;
        }
    }
}