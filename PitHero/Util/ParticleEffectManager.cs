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
        /// <summary>Green clump spreading outward while fading; plays on heal-spell targets.</summary>
        Heal,

        /// <summary>Green particles rising vertically while fading; plays on potion-heal targets.
        /// Density scales with potion strength via SpawnEffect's densityScale.</summary>
        HealPotion,

        /// <summary>Spinning ball of fire; launched from caster to target as a projectile
        /// (spawn via SpawnEffectAtPosition, move the entity, PauseEmission on impact).</summary>
        Fireball,

        /// <summary>Wide band of large fire particles raining down; spawned above the
        /// enemy group's center for the mage Firestorm AoE.</summary>
        Firestorm
    }

    /// <summary>
    /// Global registry of particle effects loaded from .pex files in Content/Particles.
    /// To add a new effect: drop a .pex in Content/Particles, add a ParticleEffectType member,
    /// load it in Init, then call SpawnEffect at the trigger site.
    /// Registry configs are shared instances — never mutate one at spawn time; a per-spawn
    /// variant needs its own enum member and .pex file, or a CloneConfig-based knob like
    /// SpawnEffect's densityScale.
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
                    { ParticleEffectType.Heal, content.LoadParticleEmitterConfig("Content/Particles/heal.pex") },
                    { ParticleEffectType.HealPotion, content.LoadParticleEmitterConfig("Content/Particles/heal_potion.pex") },
                    { ParticleEffectType.Fireball, content.LoadParticleEmitterConfig("Content/Particles/fireball.pex") },
                    { ParticleEffectType.Firestorm, content.LoadParticleEmitterConfig("Content/Particles/firestorm.pex") }
                };

                Initialized = true;
            }
        }

        /// <summary>
        /// Fire-and-forget effect attached to an entity; the emitter removes itself
        /// once all particles expire. Returns null if uninitialized or target is null.
        /// densityScale multiplies particle count and emission rate (1 = as authored);
        /// values != 1 spawn from a private clone so the shared registry config stays pristine.
        /// </summary>
        public ParticleEmitter SpawnEffect(ParticleEffectType type, Entity target,
            int renderLayer = GameConfig.RenderLayerLowest, float densityScale = 1f)
        {
            if (_configs == null || target == null || !_configs.TryGetValue(type, out var config))
                return null;

            if (densityScale != 1f)
            {
                config = CloneConfig(config);
                config.MaxParticles = (uint)(config.MaxParticles * densityScale);
                config.EmissionRate *= densityScale;
            }

            var emitter = target.AddComponent(new ParticleEmitter(config));
            emitter.SimulateInWorldSpace = true;
            emitter.RenderLayer = renderLayer;
            emitter.OnAllParticlesExpired += RemoveEmitterComponent;
            return emitter;
        }

        /// <summary>
        /// Spawns the potion-heal effect (rising green particles) on the target, denser
        /// for stronger potions: base potions 1x, mid potions (500+ HP) 2x, full-restore
        /// potions (negative amount) 3x. Shared by battle and out-of-battle potion paths.
        /// </summary>
        public ParticleEmitter SpawnPotionHealEffect(RolePlayingFramework.Equipment.Consumable consumable, Entity target)
        {
            if (consumable == null)
                return null;

            float densityScale = consumable.HPRestoreAmount < 0 ? 3f
                : consumable.HPRestoreAmount >= 500 ? 2f
                : 1f;
            return SpawnEffect(ParticleEffectType.HealPotion, target, densityScale: densityScale);
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

        /// <summary>
        /// Field-by-field copy so per-spawn tweaks never touch the shared registry config.
        /// The Sprite reference is shared intentionally — clones must never be disposed.
        /// </summary>
        private static ParticleEmitterConfig CloneConfig(ParticleEmitterConfig source)
        {
            return new ParticleEmitterConfig
            {
                Sprite = source.Sprite,
                SimulateInWorldSpace = source.SimulateInWorldSpace,
                BlendFuncSource = source.BlendFuncSource,
                BlendFuncDestination = source.BlendFuncDestination,
                SourcePosition = source.SourcePosition,
                SourcePositionVariance = source.SourcePositionVariance,
                Speed = source.Speed,
                SpeedVariance = source.SpeedVariance,
                ParticleLifespan = source.ParticleLifespan,
                ParticleLifespanVariance = source.ParticleLifespanVariance,
                Angle = source.Angle,
                AngleVariance = source.AngleVariance,
                Gravity = source.Gravity,
                RadialAcceleration = source.RadialAcceleration,
                RadialAccelVariance = source.RadialAccelVariance,
                TangentialAcceleration = source.TangentialAcceleration,
                TangentialAccelVariance = source.TangentialAccelVariance,
                StartColor = source.StartColor,
                StartColorVariance = source.StartColorVariance,
                FinishColor = source.FinishColor,
                FinishColorVariance = source.FinishColorVariance,
                MaxParticles = source.MaxParticles,
                StartParticleSize = source.StartParticleSize,
                StartParticleSizeVariance = source.StartParticleSizeVariance,
                FinishParticleSize = source.FinishParticleSize,
                FinishParticleSizeVariance = source.FinishParticleSizeVariance,
                Duration = source.Duration,
                EmitterType = source.EmitterType,
                RotationStart = source.RotationStart,
                RotationStartVariance = source.RotationStartVariance,
                RotationEnd = source.RotationEnd,
                RotationEndVariance = source.RotationEndVariance,
                EmissionRate = source.EmissionRate,
                MaxRadius = source.MaxRadius,
                MaxRadiusVariance = source.MaxRadiusVariance,
                MinRadius = source.MinRadius,
                MinRadiusVariance = source.MinRadiusVariance,
                RotatePerSecond = source.RotatePerSecond,
                RotatePerSecondVariance = source.RotatePerSecondVariance
            };
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
