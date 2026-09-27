using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nez;
using Nez.Particles;
using PitHero.Services.Replay.Frames;
using PitHero.Util;

namespace PitHero.Rendering
{
    /// <summary>
    /// The replay frame viewer's particle effects (issue #431). The frame stream does not store
    /// particles; a <see cref="ParticleOp"/> names the effect, its emitter's root and its age in ticks.
    /// This pool owns one re-simulated effect per recorded emitter and drives it with Nez's own
    /// <see cref="Particle"/> code at the fixed step, from a <see cref="System.Random"/> seeded by the
    /// effect key and the start tick, so the same emitter looks the same at a tick however the playhead
    /// got there: forward play steps each effect once per tick, a seek or a rewind re-simulates it from
    /// its start. Approximate by design (the emitter's path before the current tick is unknown, so a
    /// rebuilt effect spawns at the current root), and cosmetic: nothing reads it back.
    /// Headless-safe apart from <see cref="Draw"/>; the effect configs come from
    /// <see cref="ParticleEffectManager.Current"/> or from <see cref="ConfigSource"/> in tests.
    /// </summary>
    public sealed class RecordedParticlePool
    {
        /// <summary>One recorded emitter being re-simulated.</summary>
        public sealed class Effect
        {
            public ushort EntityId;
            public long StartTick;
            public byte Key, Density;
            public ParticleEmitterConfig Config;
            public Material Material;
            public readonly List<Particle> Particles = new List<Particle>(64);
            public System.Random Rng;
            public float EmitCounter, ElapsedSeconds;
            public bool Emitting;
            /// <summary>Ticks simulated since the start (-1 before the first step).</summary>
            public int SimulatedTicks = -1;
            public Vector2 Root;
            internal int TouchedFrame;
        }

        private readonly List<Effect> _effects = new List<Effect>(16);
        private readonly Dictionary<int, ParticleEmitterConfig> _configs = new Dictionary<int, ParticleEmitterConfig>(8);
        private readonly Dictionary<byte, Material> _materials = new Dictionary<byte, Material>(8);
        private ParticleCollisionConfig _noCollision; // default: collisions off, no physics queries
        private int _frameStamp;

        /// <summary>Supplies the as-authored config for an effect key; defaults to the global effect manager.</summary>
        public Func<byte, ParticleEmitterConfig> ConfigSource { get; set; }

        /// <summary>Effects alive in the pool.</summary>
        public int Count => _effects.Count;

        /// <summary>The effect for an entity id, or null.</summary>
        public Effect Find(ushort entityId)
        {
            for (int i = 0; i < _effects.Count; i++)
            {
                if (_effects[i].EntityId == entityId)
                    return _effects[i];
            }
            return null;
        }

        /// <summary>Starts a frame: effects not touched before <see cref="EndFrame"/> are dropped there.</summary>
        public void BeginFrame()
        {
            _frameStamp++;
        }

        /// <summary>Drops the effects the frame walk did not touch (their emitters are gone at this tick).</summary>
        public void EndFrame()
        {
            for (int i = _effects.Count - 1; i >= 0; i--)
            {
                if (_effects[i].TouchedFrame == _frameStamp)
                    continue;
                Release(_effects[i]);
                _effects.RemoveAt(i);
            }
        }

        /// <summary>Frees every particle and forgets every effect.</summary>
        public void Clear()
        {
            for (int i = 0; i < _effects.Count; i++)
                Release(_effects[i]);
            _effects.Clear();
        }

        /// <summary>
        /// Brings the effect behind a recorded op to the op's age and returns it for drawing (null when
        /// the effect is unknown). One tick past the last simulated state is one step; anything else is a
        /// rebuild from the effect's start with the same seed.
        /// </summary>
        public Effect Touch(ushort entityId, in ParticleOp op, long frameTick)
        {
            var config = ConfigFor(op.EffectKey, op.Density);
            if (config == null)
                return null;
            long startTick = frameTick - op.ElapsedTicks;
            var fx = Find(entityId);
            if (fx == null)
            {
                fx = new Effect { EntityId = entityId };
                _effects.Add(fx);
            }
            if (fx.SimulatedTicks < 0 || fx.StartTick != startTick || fx.Key != op.EffectKey || fx.Density != op.Density)
                Reset(fx, op.EffectKey, op.Density, startTick, config);
            fx.TouchedFrame = _frameStamp;
            fx.Root = new Vector2(op.X, op.Y);

            int target = op.ElapsedTicks;
            if (target > GameConfig.ReplayFrameViewParticleRebuildMaxTicks)
                target = GameConfig.ReplayFrameViewParticleRebuildMaxTicks;
            if (target < fx.SimulatedTicks || target > fx.SimulatedTicks + 1)
                Reset(fx, op.EffectKey, op.Density, startTick, config);
            while (fx.SimulatedTicks < target)
                Step(fx);
            // The recording knows when emission stopped (an impact pauses the fireball's trail); the
            // rebuild does not, so the flag only ends emission from here on
            if (!op.IsEmitting)
                fx.Emitting = false;
            return fx;
        }

        private static int Seed(byte key, long startTick)
        {
            unchecked
            {
                return (int)(startTick * 31L + key * 7919L + 17L);
            }
        }

        private void Reset(Effect fx, byte key, byte density, long startTick, ParticleEmitterConfig config)
        {
            Release(fx);
            fx.Key = key;
            fx.Density = density;
            fx.StartTick = startTick;
            fx.Config = config;
            fx.Material = MaterialFor(key, config);
            fx.Rng = new System.Random(Seed(key, startTick));
            fx.EmitCounter = 0f;
            fx.ElapsedSeconds = 0f;
            fx.Emitting = true;
            fx.SimulatedTicks = 0;
        }

        private static void Release(Effect fx)
        {
            for (int i = 0; i < fx.Particles.Count; i++)
                Pool<Particle>.Free(fx.Particles[i]);
            fx.Particles.Clear();
        }

        /// <summary>
        /// One fixed step of the effect, the way <see cref="ParticleEmitter.Update"/> runs it: emit at
        /// the rate while emitting and within the duration, then update every particle. Nez's particle
        /// code reads <see cref="Time.DeltaTime"/> and <see cref="ParticleRandom.Rng"/>, so both are
        /// pointed at the step and the effect's own generator for the call and restored after.
        /// </summary>
        private void Step(Effect fx)
        {
            const float dt = GameConfig.SimulationFixedStepSeconds;
            float savedDelta = Time.DeltaTime;
            var savedRng = ParticleRandom.Rng;
            Time.DeltaTime = dt;
            ParticleRandom.Rng = fx.Rng;
            try
            {
                var cfg = fx.Config;
                var root = fx.Root;
                if (fx.Emitting && cfg.EmissionRate > 0f)
                {
                    float rate = 1f / cfg.EmissionRate;
                    if (fx.Particles.Count < cfg.MaxParticles)
                        fx.EmitCounter += dt;
                    while (fx.Particles.Count < cfg.MaxParticles && fx.EmitCounter > rate)
                    {
                        var particle = Pool<Particle>.Obtain();
                        particle.Initialize(cfg, root);
                        fx.Particles.Add(particle);
                        fx.EmitCounter -= rate;
                    }
                    fx.ElapsedSeconds += dt;
                    if (cfg.Duration != -1f && cfg.Duration < fx.ElapsedSeconds)
                        fx.Emitting = false;
                }
                for (int i = fx.Particles.Count - 1; i >= 0; i--)
                {
                    var particle = fx.Particles[i];
                    if (particle.Update(cfg, ref _noCollision, root))
                    {
                        Pool<Particle>.Free(particle);
                        fx.Particles.RemoveAt(i);
                    }
                }
                fx.SimulatedTicks++;
            }
            finally
            {
                Time.DeltaTime = savedDelta;
                ParticleRandom.Rng = savedRng;
            }
        }

        /// <summary>
        /// The config for an effect at a density: a private clone of the as-authored one (the game's
        /// spawn path clones too), simulating in world space as every spawned emitter does.
        /// </summary>
        private ParticleEmitterConfig ConfigFor(byte key, byte density)
        {
            int cacheKey = key << 8 | density;
            if (_configs.TryGetValue(cacheKey, out var config))
                return config;
            var source = ConfigSource != null ? ConfigSource(key) : ParticleEffectManager.Current?.GetConfig((ParticleEffectType)key);
            if (source == null)
            {
                _configs[cacheKey] = null;
                return null;
            }
            config = ParticleEffectManager.CloneConfig(source);
            float scale = density / 10f;
            if (scale > 0f && scale != 1f)
            {
                config.MaxParticles = (uint)(config.MaxParticles * scale);
                config.EmissionRate *= scale;
            }
            config.SimulateInWorldSpace = true;
            _configs[cacheKey] = config;
            return config;
        }

        /// <summary>The blend material the live emitter draws with (Nez builds the same one in ParticleEmitter.Init).</summary>
        private Material MaterialFor(byte key, ParticleEmitterConfig config)
        {
            if (_materials.TryGetValue(key, out var material))
                return material;
            if (Core.Instance == null)
            {
                _materials[key] = null;
                return null;
            }
            var blendState = new BlendState();
            blendState.ColorSourceBlend = blendState.AlphaSourceBlend = config.BlendFuncSource;
            blendState.ColorDestinationBlend = blendState.AlphaDestinationBlend = config.BlendFuncDestination;
            material = new Material(blendState);
            _materials[key] = material;
            return material;
        }

        /// <summary>Draws an effect's particles as <see cref="ParticleEmitter.Render"/> does (the batch material must already be the effect's).</summary>
        public void Draw(Batcher batcher, Effect fx)
        {
            var cfg = fx.Config;
            var particles = fx.Particles;
            for (int i = 0; i < particles.Count; i++)
            {
                var p = particles[i];
                var pos = cfg.SimulateInWorldSpace ? p.SpawnPosition : fx.Root;
                if (cfg.Sprite == null)
                    batcher.Draw(Graphics.Instance.PixelTexture, pos + p.Position, p.Color, p.Rotation, Vector2.One, p.ParticleSize * 0.5f, SpriteEffects.None, 0f);
                else
                    batcher.Draw(cfg.Sprite, pos + p.Position, p.Color, p.Rotation, cfg.Sprite.Center, p.ParticleSize / cfg.Sprite.SourceRect.Width, SpriteEffects.None, 0f);
            }
        }
    }
}
