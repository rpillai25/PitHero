using Microsoft.Xna.Framework;
using Nez;
using Nez.Particles;
using Nez.Systems;
using System.Collections.Generic;

namespace PitHero.Util
{
    /// <summary>Particle effects that can be spawned via ParticleEffectManager.</summary>
    public enum ParticleEffectType
    {
        /// <summary>Green clump spreading outward while fading; plays on heal targets.</summary>
        Heal
    }

    /// <summary>
    /// Global registry of particle effects loaded from .pex files in Content/Particles.
    /// To add a new effect: drop a .pex in Content/Particles, add a ParticleEffectType member,
    /// load it in Init, then call SpawnEffect at the trigger site.
    /// Registry configs are shared instances — never mutate one at spawn time; a per-spawn
    /// variant needs its own enum member and .pex file.
    /// Only finite-duration configs belong here: a Duration of -1 never fires
    /// OnAllParticlesExpired, so the emitter would never clean itself up.
    /// </summary>
    public class ParticleEffectManager : GlobalManager
    {
        public bool Initialized = false;

        private Dictionary<ParticleEffectType, ParticleEmitterConfig> _configs;

        public void Init(NezContentManager content)
        {
            if (!Initialized)
            {
                _configs = new Dictionary<ParticleEffectType, ParticleEmitterConfig>
                {
                    { ParticleEffectType.Heal, content.LoadParticleEmitterConfig("Content/Particles/heal.pex") }
                };

                Initialized = true;
            }
        }

        /// <summary>
        /// Fire-and-forget effect attached to an entity; the emitter removes itself
        /// once all particles expire. Returns null if uninitialized or target is null.
        /// </summary>
        public ParticleEmitter SpawnEffect(ParticleEffectType type, Entity target,
            int renderLayer = GameConfig.RenderLayerLowest)
        {
            if (_configs == null || target == null || !_configs.TryGetValue(type, out var config))
                return null;

            var emitter = target.AddComponent(new ParticleEmitter(config));
            emitter.SimulateInWorldSpace = true;
            emitter.RenderLayer = renderLayer;
            emitter.OnAllParticlesExpired += RemoveEmitterComponent;
            return emitter;
        }

        /// <summary>
        /// Fire-and-forget effect at a world position for effects not tied to an actor
        /// (e.g. dust when jumping into the pit). Creates a throwaway entity that is
        /// destroyed once all particles expire.
        /// </summary>
        public ParticleEmitter SpawnEffectAtPosition(ParticleEffectType type, Vector2 worldPosition,
            Scene scene, int renderLayer = GameConfig.RenderLayerLowest)
        {
            if (_configs == null || scene == null || !_configs.TryGetValue(type, out var config))
                return null;

            var entity = scene.CreateEntity("particle-effect");
            entity.SetPosition(worldPosition);
            var emitter = entity.AddComponent(new ParticleEmitter(config));
            emitter.SimulateInWorldSpace = true;
            emitter.RenderLayer = renderLayer;
            emitter.OnAllParticlesExpired += DestroyEmitterEntity;
            return emitter;
        }

        private static void RemoveEmitterComponent(ParticleEmitter emitter)
        {
            emitter.Clear();
            emitter.Entity?.RemoveComponent(emitter);
        }

        private static void DestroyEmitterEntity(ParticleEmitter emitter)
        {
            emitter.Clear();
            emitter.Entity?.Destroy();
        }
    }
}
