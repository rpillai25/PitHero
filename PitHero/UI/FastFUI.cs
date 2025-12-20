using Nez;
using Nez.UI;

namespace PitHero.UI
{
    /// <summary>
    /// UI for FastF speed toggle button that controls game time scale
    /// </summary>
    public class FastFUI
    {
        private Stage _stage;
        private HoverableImageButton _fastFButton;

        private ImageButtonStyle _fastFNormalStyle;
        private ImageButtonStyle _fastFHalfStyle;

        //Button stays in pressed state
        private ImageButtonStyle _fastFNormalPressedStyle;
        private ImageButtonStyle _fastFHalfPressedStyle;

        private enum FastFMode { Normal, NormalPressed, Half, HalfPressed }
        private FastFMode _currentFastFMode = FastFMode.Normal;

        private bool _isSpeedUp = false; // Track current speed state
        private bool _styleChanged = false; // tracks when style (and thus size) changed

        public FastFUI()
        {
        }

        /// <summary>
        /// Initializes the FastF button and adds it to the stage
        /// </summary>
        public void InitializeUI(Stage stage)
        {
            _stage = stage;

            // Use default skin
            var skin = Skin.CreateDefaultSkin();

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
            var fastFSprite2x = uiAtlas.GetSprite("UIFastF2x");
            var fastFHighlight = uiAtlas.GetSprite("UIFastFHighlight");
            var fastFHighlight2x = uiAtlas.GetSprite("UIFastFHighlight2x");
            var fastFInverse = uiAtlas.GetSprite("UIFastFInverse");
            var fastFInverse2x = uiAtlas.GetSprite("UIFastFInverse2x");

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

            _fastFHalfStyle = new ImageButtonStyle
            {
                ImageUp = new SpriteDrawable(fastFSprite2x),
                ImageDown = new SpriteDrawable(fastFInverse2x),
                ImageOver = new SpriteDrawable(fastFHighlight2x)
            };

            _fastFHalfPressedStyle = new ImageButtonStyle
            {
                ImageUp = new SpriteDrawable(fastFInverse2x),
                ImageDown = new SpriteDrawable(fastFSprite2x),
                ImageOver = new SpriteDrawable(fastFHighlight2x)
            };

            _fastFButton = new HoverableImageButton(_fastFNormalStyle, "Fast Forward");
            // Explicitly size to the image
            _fastFButton.SetSize(fastFSprite.SourceRect.Width, fastFSprite.SourceRect.Height);

            // Handle click to toggle speed
            _fastFButton.OnClicked += (button) => ToggleSpeed();
        }

        private void ToggleSpeed()
        {
            if (_isSpeedUp)
            {
                // Set back to normal speed
                Time.TimeScale = 1f;
                _isSpeedUp = false;
            }
            else
            {
                // Speed up to 2x
                Time.TimeScale = 2.5f;
                _isSpeedUp = true;
            }
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
        }
    }
}