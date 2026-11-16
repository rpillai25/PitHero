using Microsoft.Xna.Framework;
using Nez;
using Nez.Sprites;
using PitHero.Services;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// A hooked catch on its way to the fisher: pulled straight up out of the water, arcing through the
    /// air to the fisher's hands (<see cref="FishingCatchPath"/>, the same path the line follows), then
    /// lingering there with its "Name (+N G)" label before fading. Cosmetic only: the gold is paid by
    /// the simulation when the catch is drawn; this finishes instantly during replay seeks and never
    /// touches simulation state. Paused steps do not advance it.
    /// </summary>
    public class FishCatchEffectComponent : Component, IUpdatable
    {
        private readonly SpriteRenderer _sprite;
        private readonly GemValueTextComponent _label;
        private readonly Vector2 _bobber;
        private readonly Vector2 _target;
        private readonly float _flightSeconds;
        private float _elapsed;
        private PauseService _pauseService;

        /// <summary>Creates the driver for the catch sprite and its label on the same entity.</summary>
        public FishCatchEffectComponent(SpriteRenderer sprite, GemValueTextComponent label, Vector2 bobber, Vector2 target)
        {
            _sprite = sprite;
            _label = label;
            _bobber = bobber;
            _target = target;
            _flightSeconds = GameConfig.FishingReelSeconds;
        }

        /// <summary>Places the catch at the bobber and hides the label until it lands.</summary>
        public override void OnAddedToEntity()
        {
            _pauseService = Core.Services.GetService<PauseService>();
            Entity.SetPosition(_bobber);
            _label?.SetAlpha(0f);
        }

        /// <summary>Advances the flight, then the linger and fade, on simulation time.</summary>
        public void Update()
        {
            if (Core.CosmeticUpdatesSuspended)
            {
                Entity.Destroy(); // replay seek: finish instantly so nothing flies when the seek ends
                return;
            }
            if (_pauseService?.IsPaused == true)
                return;

            _elapsed += Time.DeltaTime;
            float total = _flightSeconds + GameConfig.FishCatchLingerSeconds;
            if (_elapsed >= total)
            {
                Entity.Destroy();
                return;
            }

            float p = _elapsed / _flightSeconds;
            var pos = FishingCatchPath.Position(_bobber, _target, p > 1f ? 1f : p);
            Entity.SetPosition(Mathf.Round(pos.X), Mathf.Round(pos.Y));

            if (_elapsed < _flightSeconds)
                return;

            // Landed: show the label, then fade both out over the second half of the linger
            float linger = (_elapsed - _flightSeconds) / GameConfig.FishCatchLingerSeconds;
            float alpha = linger < 0.5f ? 1f : 1f - (linger - 0.5f) / 0.5f;
            _sprite?.SetColor(Color.White * alpha);
            _label?.SetAlpha(alpha);
        }
    }
}
