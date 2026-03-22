using Nez;
using Nez.UI;
using PitHero.AI;
using PitHero.ECS.Components;

namespace PitHero.UI
{
    /// <summary>
    /// UI for Stop/Continue Adventuring toggle button.
    /// Uses FastForward sprite for Stop Adventuring and Settings (Gear) sprite for Continue Adventuring.
    /// </summary>
    public class StopAdventuringUI
    {
        private Stage _stage;
        private HoverableImageButton _button;

        // Stop Adventuring styles (uses FastForward sprites)
        private ImageButtonStyle _stopNormalStyle;
        private ImageButtonStyle _stopHalfStyle;

        // Continue Adventuring styles (uses Gear sprites)
        private ImageButtonStyle _continueNormalStyle;
        private ImageButtonStyle _continueHalfStyle;

        private enum ButtonMode { StopNormal, StopHalf, ContinueNormal, ContinueHalf }
        private ButtonMode _currentMode = ButtonMode.StopNormal;

        private bool _isStoppedAdventuring = false;
        private bool _styleChanged = false;

        public StopAdventuringUI()
        {
        }

        /// <summary>
        /// Initializes the button and adds it to the stage
        /// </summary>
        public void InitializeUI(Stage stage)
        {
            _stage = stage;

            var skin = PitHeroSkin.CreateSkin();

            CreateButton(skin);

            _stage.AddElement(_button);
        }

        private void CreateButton(Skin skin)
        {
            var uiAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/UI.atlas");

            // Stop Adventuring sprites (same as FastForward)
            var fastFSprite = uiAtlas.GetSprite("UIFastF");
            var fastFSprite2x = uiAtlas.GetSprite("UIFastF2x");
            var fastFHighlight = uiAtlas.GetSprite("UIFastFHighlight");
            var fastFHighlight2x = uiAtlas.GetSprite("UIFastFHighlight2x");
            var fastFInverse = uiAtlas.GetSprite("UIFastFInverse");
            var fastFInverse2x = uiAtlas.GetSprite("UIFastFInverse2x");

            // Continue Adventuring sprites (same as Gear/Settings)
            var gearSprite = uiAtlas.GetSprite("UIGear");
            var gearSprite2x = uiAtlas.GetSprite("UIGear2x");
            var gearHighlight = uiAtlas.GetSprite("UIGearHighlight");
            var gearHighlight2x = uiAtlas.GetSprite("UIGearHighlight2x");
            var gearInverse = uiAtlas.GetSprite("UIGearInverse");
            var gearInverse2x = uiAtlas.GetSprite("UIGearInverse2x");

            _stopNormalStyle = new ImageButtonStyle
            {
                ImageUp = new SpriteDrawable(fastFSprite),
                ImageDown = new SpriteDrawable(fastFInverse),
                ImageOver = new SpriteDrawable(fastFHighlight)
            };

            _stopHalfStyle = new ImageButtonStyle
            {
                ImageUp = new SpriteDrawable(fastFSprite2x),
                ImageDown = new SpriteDrawable(fastFInverse2x),
                ImageOver = new SpriteDrawable(fastFHighlight2x)
            };

            _continueNormalStyle = new ImageButtonStyle
            {
                ImageUp = new SpriteDrawable(gearSprite),
                ImageDown = new SpriteDrawable(gearInverse),
                ImageOver = new SpriteDrawable(gearHighlight)
            };

            _continueHalfStyle = new ImageButtonStyle
            {
                ImageUp = new SpriteDrawable(gearSprite2x),
                ImageDown = new SpriteDrawable(gearInverse2x),
                ImageOver = new SpriteDrawable(gearHighlight2x)
            };

            _button = new HoverableImageButton(_stopNormalStyle, "Stop Adventuring");
            _button.SetSize(fastFSprite.SourceRect.Width, fastFSprite.SourceRect.Height);

            _button.OnClicked += (button) => ToggleAdventuring();
        }

        /// <summary>
        /// Toggle between Stop and Continue Adventuring
        /// </summary>
        private void ToggleAdventuring()
        {
            _isStoppedAdventuring = !_isStoppedAdventuring;

            // Find the hero and update StoppedAdventure state
            var heroEntity = Core.Scene?.FindEntity("hero");
            var heroComponent = heroEntity?.GetComponent<HeroComponent>();

            if (heroComponent != null)
            {
                heroComponent.StoppedAdventure = _isStoppedAdventuring;

                if (_isStoppedAdventuring)
                {
                    // Reset SeatedInTavern so the planner creates a new plan to get to the tavern
                    heroComponent.SeatedInTavern = false;
                    Debug.Log("[StopAdventuringUI] Player stopped adventuring");
                }
                else
                {
                    // Clear seated state so hero can resume
                    heroComponent.SeatedInTavern = false;

                    // Re-enable mercenary following so they resume movement
                    WalkToTavernForStopAction.ReenableMercenaryFollowing();

                    Debug.Log("[StopAdventuringUI] Player resumed adventuring");
                }
            }

            // Force style update
            _currentMode = ButtonMode.StopNormal; // Reset to force recalculation
            _styleChanged = true;
        }

        /// <summary>
        /// Update button style based on current state and window mode
        /// </summary>
        public void UpdateButtonStyleIfNeeded()
        {
            ButtonMode desired;
            bool isHalf = WindowManager.IsHalfHeightMode();

            if (_isStoppedAdventuring)
            {
                desired = isHalf ? ButtonMode.ContinueHalf : ButtonMode.ContinueNormal;
            }
            else
            {
                desired = isHalf ? ButtonMode.StopHalf : ButtonMode.StopNormal;
            }

            if (desired == _currentMode)
                return;

            ImageButtonStyle style;
            string tooltip;

            switch (desired)
            {
                case ButtonMode.StopNormal:
                    style = _stopNormalStyle;
                    tooltip = "Stop Adventuring";
                    break;
                case ButtonMode.StopHalf:
                    style = _stopHalfStyle;
                    tooltip = "Stop Adventuring";
                    break;
                case ButtonMode.ContinueNormal:
                    style = _continueNormalStyle;
                    tooltip = "Continue Adventuring";
                    break;
                case ButtonMode.ContinueHalf:
                    style = _continueHalfStyle;
                    tooltip = "Continue Adventuring";
                    break;
                default:
                    style = _stopNormalStyle;
                    tooltip = "Stop Adventuring";
                    break;
            }

            _button.SetStyle(style);
            _button.SetHoverText(tooltip);
            _button.SetSize(
                ((SpriteDrawable)style.ImageUp).Sprite.SourceRect.Width,
                ((SpriteDrawable)style.ImageUp).Sprite.SourceRect.Height
            );

            _currentMode = desired;
            _styleChanged = true;
        }

        /// <summary>
        /// Position the button at the specified coordinates
        /// </summary>
        public void SetPosition(float x, float y)
        {
            _button?.SetPosition(x, y);
        }

        /// <summary>
        /// Get the button width
        /// </summary>
        public float GetWidth()
        {
            return _button?.GetWidth() ?? 0f;
        }

        /// <summary>
        /// Get the button height
        /// </summary>
        public float GetHeight()
        {
            return _button?.GetHeight() ?? 0f;
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
        /// Update method called each frame
        /// </summary>
        public void Update()
        {
            UpdateButtonStyleIfNeeded();
        }
    }
}
