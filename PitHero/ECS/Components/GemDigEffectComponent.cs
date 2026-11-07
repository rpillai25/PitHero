using Microsoft.Xna.Framework;
using Nez;
using Nez.Sprites;
using PitHero.Services;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Drives a dug-up gem (issue #452): the gem sprite and its "Agate (+1)" label rise quickly out of
    /// the gem dig site, fade, and the entity is destroyed. Cosmetic only: it finishes instantly
    /// during replay seeks and never touches simulation state. Paused steps do not advance it.
    /// </summary>
    public class GemDigEffectComponent : Component, IUpdatable
    {
        private readonly SpriteRenderer _gem;
        private readonly GemValueTextComponent _text;
        private Vector2 _start;
        private float _elapsed;
        private PauseService _pauseService;

        /// <summary>Creates the driver for a find's renderer (null for an empty dig) and its label on the same entity.</summary>
        public GemDigEffectComponent(SpriteRenderer gem, GemValueTextComponent text)
        {
            _gem = gem;
            _text = text;
        }

        /// <summary>Captures the start position and the pause service.</summary>
        public override void OnAddedToEntity()
        {
            _start = Entity.Position;
            _pauseService = Core.Services.GetService<PauseService>();
        }

        /// <summary>Advances the rise and fade on simulation time.</summary>
        public void Update()
        {
            if (Core.CosmeticUpdatesSuspended)
            {
                Entity.Destroy(); // replay seek: finish instantly so the gem does not rise when the seek ends
                return;
            }
            if (_pauseService?.IsPaused == true)
                return;

            _elapsed += Time.DeltaTime;
            float t = _elapsed / GameConfig.GemRiseDurationSeconds;
            if (t >= 1f)
            {
                Entity.Destroy();
                return;
            }

            // Ease out: fast pop, slowing near the top
            float eased = 1f - (1f - t) * (1f - t);
            Entity.SetPosition(_start.X, Mathf.Round(_start.Y - GameConfig.GemRiseDistance * eased));

            float fadeStart = GameConfig.GemRiseFadeStartFraction;
            float alpha = t <= fadeStart ? 1f : 1f - (t - fadeStart) / (1f - fadeStart);
            _gem?.SetColor(Color.White * alpha); // null for an empty dig (label only)
            _text.SetAlpha(alpha);
        }
    }
}
