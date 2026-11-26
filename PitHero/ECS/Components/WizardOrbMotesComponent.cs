using Microsoft.Xna.Framework;
using Nez;
using Nez.Particles;
using PitHero.Util;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Blue motes rising out of the wizard orb while it is usable (issue #469). The orb's tint says which
    /// it is: blue (untinted) once uncovered on an ordinary floor or after the floor's boss falls, red
    /// while the boss lives and again once the orb has been touched. The motes emit only while the orb is
    /// shown (its fog tile is uncovered) and not red; turning red pauses the emitter, so the motes already
    /// rising finish and fade, and the emitter then removes itself (<see cref="ParticleEffectManager"/>).
    /// <para>
    /// Purely cosmetic: particles roll Nez's <c>ParticleRandom</c>, never the simulation's RNG, and the
    /// emitter is recorded into replay frames like any other managed effect.
    /// </para>
    /// </summary>
    public sealed class WizardOrbMotesComponent : Component, IUpdatable
    {
        private readonly RenderableComponent _orb;
        private ParticleEmitter _emitter;

        /// <summary>Watches <paramref name="orbRenderer"/>'s visibility and tint.</summary>
        public WizardOrbMotesComponent(RenderableComponent orbRenderer)
        {
            _orb = orbRenderer;
        }

        /// <summary>True when the orb should be giving off motes: shown and not red.</summary>
        public static bool ShouldEmit(bool orbShown, Color orbTint) => orbShown && orbTint != Color.Red;

        /// <inheritdoc />
        public void Update()
        {
            if (Core.CosmeticUpdatesSuspended || _orb == null)
                return;

            // The emitter removed itself after its last mote faded
            if (_emitter != null && (_emitter.Entity == null || _emitter.Entity.IsDestroyed))
                _emitter = null;

            if (ShouldEmit(_orb.Enabled && Entity.Enabled, _orb.Color))
            {
                if (_emitter == null)
                {
                    _emitter = ParticleEffectManager.Current?.SpawnEffect(ParticleEffectType.OrbMotes, Entity);
                    if (_emitter != null)
                        _emitter.LocalOffset = new Vector2(0f, GameConfig.WizardOrbMoteOffsetY);
                }
                else if (!_emitter.IsEmitting)
                {
                    _emitter.ResumeEmission();
                }
            }
            else if (_emitter != null && _emitter.IsEmitting)
            {
                _emitter.PauseEmission();
            }
        }
    }
}
