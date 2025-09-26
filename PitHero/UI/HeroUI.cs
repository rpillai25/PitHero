using Microsoft.Xna.Framework;
using Nez;
using Nez.UI;
using PitHero.Services;

namespace PitHero.UI
{
    /// <summary>
    /// UI for Hero button (placeholder functionality for now)
    /// </summary>
    public class HeroUI
    {
        private Stage _stage;
        private ImageButton _heroButton;
        
        private ImageButtonStyle _heroNormalStyle;
        private ImageButtonStyle _heroHalfStyle;
        private ImageButtonStyle _heroQuarterStyle;
        private enum HeroMode { Normal, Half, Quarter }
        private HeroMode _currentHeroMode = HeroMode.Normal;
        private bool _styleChanged = false;

        public HeroUI()
        {
        }

        /// <summary>
        /// Initializes the Hero button and adds it to the stage
        /// </summary>
        public void InitializeUI(Stage stage)
        {
            _stage = stage;

            // Use default skin
            var skin = Skin.CreateDefaultSkin();

            // Create Hero button
            CreateHeroButton(skin);

            // Add button to stage
            _stage.AddElement(_heroButton);
        }

        private void CreateHeroButton(Skin skin)
        {
            // Load the UI atlas and get the Hero sprites
            var uiAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/UI.atlas");
            var heroSprite = uiAtlas.GetSprite("UIHero");
            var heroSprite2x = uiAtlas.GetSprite("UIHero2x");
            var heroSprite4x = uiAtlas.GetSprite("UIHero4x");
            var heroHighlight = uiAtlas.GetSprite("UIHeroHighlight");
            var heroHighlight2x = uiAtlas.GetSprite("UIHeroHighlight2x");
            var heroHighlight4x = uiAtlas.GetSprite("UIHeroHighlight4x");
            var heroInverse = uiAtlas.GetSprite("UIHeroInverse");
            var heroInverse2x = uiAtlas.GetSprite("UIHeroInverse2x");
            var heroInverse4x = uiAtlas.GetSprite("UIHeroInverse4x");

            // Base styles for each sprite with proper ImageDown and ImageOver
            _heroNormalStyle = new ImageButtonStyle
            {
                ImageUp = new SpriteDrawable(heroSprite),
                ImageDown = new SpriteDrawable(heroInverse),
                ImageOver = new SpriteDrawable(heroHighlight)
            };

            _heroHalfStyle = new ImageButtonStyle
            {
                ImageUp = new SpriteDrawable(heroSprite2x),
                ImageDown = new SpriteDrawable(heroInverse2x),
                ImageOver = new SpriteDrawable(heroHighlight2x)
            };

            _heroQuarterStyle = new ImageButtonStyle
            {
                ImageUp = new SpriteDrawable(heroSprite4x),
                ImageDown = new SpriteDrawable(heroInverse4x),
                ImageOver = new SpriteDrawable(heroHighlight4x)
            };

            _heroButton = new ImageButton(_heroNormalStyle);
            // Explicitly size to the image
            _heroButton.SetSize(heroSprite.SourceRect.Width, heroSprite.SourceRect.Height);

            // Handle click (placeholder - does nothing for now)
            _heroButton.OnClicked += (button) => HandleHeroButtonClick();
        }

        private void HandleHeroButtonClick()
        {
            // Placeholder - button does nothing yet as specified
            Debug.Log("Hero button clicked - no functionality implemented yet");
        }

        /// <summary>
        /// Update button style based on current window shrink mode
        /// </summary>
        public void UpdateButtonStyleIfNeeded()
        {
            // Determine desired mode based on current shrink mode
            HeroMode desired;
            if (WindowManager.IsQuarterHeightMode())
                desired = HeroMode.Quarter;
            else if (WindowManager.IsHalfHeightMode())
                desired = HeroMode.Half;
            else
                desired = HeroMode.Normal;

            if (desired == _currentHeroMode)
                return; // no change needed

            switch (desired)
            {
                case HeroMode.Normal:
                    _heroButton.SetStyle(_heroNormalStyle);
                    _heroButton.SetSize(((SpriteDrawable)_heroNormalStyle.ImageUp).Sprite.SourceRect.Width, ((SpriteDrawable)_heroNormalStyle.ImageUp).Sprite.SourceRect.Height);
                    break;
                case HeroMode.Half:
                    _heroButton.SetStyle(_heroHalfStyle);
                    _heroButton.SetSize(((SpriteDrawable)_heroHalfStyle.ImageUp).Sprite.SourceRect.Width, ((SpriteDrawable)_heroHalfStyle.ImageUp).Sprite.SourceRect.Height);
                    break;
                case HeroMode.Quarter:
                    _heroButton.SetStyle(_heroQuarterStyle);
                    _heroButton.SetSize(((SpriteDrawable)_heroQuarterStyle.ImageUp).Sprite.SourceRect.Width, ((SpriteDrawable)_heroQuarterStyle.ImageUp).Sprite.SourceRect.Height);
                    break;
            }

            _currentHeroMode = desired;
            _styleChanged = true;
        }

        /// <summary>
        /// Position the button at the specified coordinates
        /// </summary>
        public void SetPosition(float x, float y)
        {
            _heroButton?.SetPosition(x, y);
        }

        /// <summary>
        /// Get the button width
        /// </summary>
        public float GetWidth()
        {
            return _heroButton?.GetWidth() ?? 0f;
        }

        /// <summary>
        /// Get the button height
        /// </summary>
        public float GetHeight()
        {
            return _heroButton?.GetHeight() ?? 0f;
        }

        /// <summary>
        /// Consume style changed flag
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