using Nez;
using Nez.UI;
using PitHero.AI;
using PitHero.ECS.Components;
using PitHero.Services;

namespace PitHero.UI
{
    /// <summary>
    /// UI for Stop/Continue Adventuring toggle button.
    /// </summary>
    public class StopAdventuringUI
    {
        private Stage _stage;
        private HoverableImageButton _button;
        private TextService _textService;

        // Stop Adventuring styles
        private ImageButtonStyle _stopNormalStyle;
        private ImageButtonStyle _stopHalfStyle;

        // Continue Adventuring styles
        private ImageButtonStyle _continueNormalStyle;
        private ImageButtonStyle _continueHalfStyle;

        private enum ButtonMode { StopNormal, StopHalf, ContinueNormal, ContinueHalf }
        private ButtonMode _currentMode = ButtonMode.StopNormal;

        private bool _isStoppedAdventuring = false;
        private bool _styleChanged = false;
        private bool _isHiddenForPromotion = false;
        private bool _barTouchDisabled = false;   // true while the top UI bar is hidden off-screen (issue #335)
        private bool _isHiddenForSleep = false;

        public StopAdventuringUI()
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

            // Stop Adventuring sprites
            var stopSprite = uiAtlas.GetSprite("UIStop");
            var stopHighlight = uiAtlas.GetSprite("UIStopHighlight");
            var stopInverse = uiAtlas.GetSprite("UIStopInverse");

            // Continue Adventuring sprites
            var continueSprite = uiAtlas.GetSprite("UIContinue");
            var continueHighlight = uiAtlas.GetSprite("UIContinueHighlight");
            var continueInverse = uiAtlas.GetSprite("UIContinueInverse");

            _stopNormalStyle = new ImageButtonStyle
            {
                ImageUp = new SpriteDrawable(stopSprite),
                ImageDown = new SpriteDrawable(stopInverse),
                ImageOver = new SpriteDrawable(stopHighlight)
            };

            _stopHalfStyle = ButtonSprite2xFactory.CreateHalfStyle(uiAtlas, "UIStop");

            _continueNormalStyle = new ImageButtonStyle
            {
                ImageUp = new SpriteDrawable(continueSprite),
                ImageDown = new SpriteDrawable(continueInverse),
                ImageOver = new SpriteDrawable(continueHighlight)
            };

            _continueHalfStyle = ButtonSprite2xFactory.CreateHalfStyle(uiAtlas, "UIContinue");

            _button = new HoverableImageButton(_stopNormalStyle, GetText(TextType.UI, UITextKey.ButtonStopAdventuring));
            _button.ClickSoundCategory = ButtonClickCategory.TopBar;
            _button.SetSize(stopSprite.SourceRect.Width, stopSprite.SourceRect.Height);

            _button.OnClicked += (button) => TriggerToggle();
        }

        /// <summary>
        /// Toggle between Stop and Continue Adventuring
        /// </summary>
        public void TriggerToggle() => SetStopped(!_isStoppedAdventuring);

        /// <summary>
        /// Sets Stop mode directly (idempotent). Used by the toggle button and by
        /// PartyDiningService for auto-dine trips and auto-resume after breakfast.
        /// </summary>
        public void SetStopped(bool stopped)
        {
            if (_isStoppedAdventuring == stopped)
                return;

            _isStoppedAdventuring = stopped;

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

            // Party dining reacts to Stop-mode edges (order eligibility, early-leave fast-track)
            var diningService = Core.Services.GetService<Services.PartyDiningService>();
            if (_isStoppedAdventuring)
                diningService?.OnStopped();
            else
                diningService?.OnResumed();

            // Force style update by setting _currentMode to the opposite of the desired state
            // so UpdateButtonStyleIfNeeded() detects a mismatch and applies the new style
            _currentMode = _isStoppedAdventuring ? ButtonMode.StopNormal : ButtonMode.ContinueNormal;
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
                    tooltip = GetText(TextType.UI, UITextKey.ButtonStopAdventuring);
                    break;
                case ButtonMode.StopHalf:
                    style = _stopHalfStyle;
                    tooltip = GetText(TextType.UI, UITextKey.ButtonStopAdventuring);
                    break;
                case ButtonMode.ContinueNormal:
                    style = _continueNormalStyle;
                    tooltip = GetText(TextType.UI, UITextKey.ButtonContinueAdventuring);
                    break;
                case ButtonMode.ContinueHalf:
                    style = _continueHalfStyle;
                    tooltip = GetText(TextType.UI, UITextKey.ButtonContinueAdventuring);
                    break;
                default:
                    style = _stopNormalStyle;
                    tooltip = GetText(TextType.UI, UITextKey.ButtonStopAdventuring);
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
            if (_isHiddenForPromotion)
                return 0f;
            return _button?.GetWidth() ?? 0f;
        }

        /// <summary>
        /// Get the button height
        /// </summary>
        public float GetHeight()
        {
            if (_isHiddenForPromotion)
                return 0f;
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

        /// <summary>Enables/disables hit-testing; disabled while the top UI bar is hidden off-screen.</summary>
        public void SetTouchable(Touchable touchable)
        {
            _barTouchDisabled = touchable == Touchable.Disabled;
            ApplyEffectiveTouchable();
        }

        /// <summary>
        /// The button is touchable only when neither the bar-hide nor the promotion/sleep hides want it disabled.
        /// </summary>
        private void ApplyEffectiveTouchable()
        {
            if (_button == null)
                return;
            bool enabled = !_barTouchDisabled && !_isHiddenForPromotion && !_isHiddenForSleep;
            _button.SetTouchable(enabled ? Touchable.Enabled : Touchable.Disabled);
        }

        /// <summary>
        /// The button is visible only when no hide state (promotion, sleep) wants it hidden.
        /// Hide states MUST combine here rather than call SetVisible directly — clearing one
        /// state while the other is still active previously re-showed the button while
        /// GetWidth() still reported 0, overlapping it with the Fast Forward button (issue #335
        /// lesson, applied to visibility). Also sets _styleChanged so SettingsUI reflows.
        /// </summary>
        private void ApplyEffectiveVisibility()
        {
            if (_button == null)
                return;
            _button.SetVisible(!_isHiddenForPromotion && !_isHiddenForSleep);
            ApplyEffectiveTouchable();
            _styleChanged = true; // Triggers SettingsUI layout reflow via ConsumeStyleChangedFlag
        }

        private void UpdateSleepVisibilityIfNeeded()
        {
            if (_button == null || Core.Scene == null)
                return;

            var heroComponent = Core.Scene.FindEntity("hero")?.GetComponent<HeroComponent>();
            bool shouldHide = heroComponent != null && heroComponent.IsSleeping;

            if (shouldHide == _isHiddenForSleep)
                return;

            _isHiddenForSleep = shouldHide;
            ApplyEffectiveVisibility();
        }

        /// <summary>
        /// Checks if the hero is pending crystal promotion and hides/shows the button accordingly.
        /// Skips entity lookup entirely when the button is not yet initialized or state is unchanged.
        /// When scene or hero is absent, shouldHide defaults to false (button shown).
        /// </summary>
        private void UpdatePromotionVisibilityIfNeeded()
        {
            if (_button == null || Core.Scene == null)
                return;

            var heroEntity = Core.Scene.FindEntity("hero");
            var heroComponent = heroEntity?.GetComponent<HeroComponent>();
            bool shouldHide = heroComponent != null && heroComponent.NeedsCrystal;

            if (shouldHide == _isHiddenForPromotion)
                return;

            _isHiddenForPromotion = shouldHide;
            ApplyEffectiveVisibility();
        }

        /// <summary>
        /// Update method called each frame
        /// </summary>
        public void Update()
        {
            UpdateSleepVisibilityIfNeeded();
            UpdatePromotionVisibilityIfNeeded();
            UpdateButtonStyleIfNeeded();
        }
    }
}
