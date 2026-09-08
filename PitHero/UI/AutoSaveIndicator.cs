using Microsoft.Xna.Framework;
using Nez;
using Nez.Textures;
using Nez.UI;

namespace PitHero.UI
{
    /// <summary>
    /// SaveIcon shown in the lower-right corner of the UI stage while an autosave is in flight
    /// (issue #409). Presentation-only: it reads the wall clock for its alpha pulse and never touches
    /// simulation state. Uses the 1x atlas sprite in the normal window and a 2x render-texture sprite
    /// in half-height mode, re-checked every frame because opening any window temporarily restores
    /// the normal size.
    /// </summary>
    public class AutoSaveIndicator : Image
    {
        private const string SpriteName = "SaveIcon";

        private readonly SpriteDrawable _drawable1x;
        private readonly SpriteDrawable _drawable2x;
        private bool _usingHalfMode;
        private float _lingerSeconds;
        private bool _wasSaving;

        /// <summary>Creates the indicator, hidden, with both scale variants ready.</summary>
        public AutoSaveIndicator()
        {
            var uiAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/UI.atlas");
            var sprite1x = uiAtlas.GetSprite(SpriteName);
            _drawable1x = new SpriteDrawable(sprite1x);
            _drawable2x = new SpriteDrawable(ButtonSprite2xFactory.GetOrCreate2x(uiAtlas, SpriteName));

            SetDrawable(_drawable1x);
            SetScaling(Scaling.None);
            SetSize(sprite1x.SourceRect.Width, sprite1x.SourceRect.Height);
            SetTouchable(Touchable.Disabled);
            SetVisible(false);
            _usingHalfMode = false;
        }

        /// <summary>
        /// Per-frame presentation update: swaps 1x/2x art with the window mode, keeps the icon anchored
        /// to the lower-right corner, and shows it while saving plus a short linger afterwards.
        /// </summary>
        public void Update(bool saving)
        {
            bool halfMode = WindowManager.IsHalfHeightMode();
            if (halfMode != _usingHalfMode)
            {
                _usingHalfMode = halfMode;
                var drawable = halfMode ? _drawable2x : _drawable1x;
                SetDrawable(drawable);
                SetSize(drawable.Sprite.SourceRect.Width, drawable.Sprite.SourceRect.Height);
                Reposition();
            }

            if (saving)
            {
                _lingerSeconds = GameConfig.AutoSaveIconMinVisibleSeconds;
                if (!_wasSaving)
                    Reposition();
            }
            else if (_lingerSeconds > 0f)
            {
                _lingerSeconds -= Time.UnscaledDeltaTime;
            }
            _wasSaving = saving;

            bool visible = saving || _lingerSeconds > 0f;
            if (IsVisible() != visible)
                SetVisible(visible);
            if (!visible)
                return;

            // Wall-clock alpha pulse (UI only — see ReplaySystem.md on presentation-side timers)
            float t = 0.5f + 0.5f * Mathf.Sin(Time.TotalTime * GameConfig.AutoSaveIconPulseSpeed);
            int alpha = (int)Mathf.Lerp(GameConfig.AutoSaveIconAlphaMin, GameConfig.AutoSaveIconAlphaMax, t);
            SetColor(new Color(255, 255, 255, alpha));
        }

        /// <summary>Anchors the icon to the lower-right corner of its stage with the configured margin.</summary>
        public void Reposition()
        {
            var stage = GetStage();
            if (stage == null)
                return;
            SetPosition(
                stage.GetWidth() - GetWidth() - GameConfig.AutoSaveIconMargin,
                stage.GetHeight() - GetHeight() - GameConfig.AutoSaveIconMargin);
        }
    }
}
