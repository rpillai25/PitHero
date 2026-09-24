using System;
using System.Collections.Generic;
using PitHero.Services.Replay.Frames;

namespace PitHero.Tests
{
    /// <summary>
    /// A synthetic scene for the frame-stream tests: static sprites, walking 8-layer composites,
    /// animated sprites, short-lived rising texts, plus spawns and despawns, all driven by a seeded
    /// System.Random so every run is identical. <see cref="Capture"/> advances one tick and returns the
    /// reference record the codec must reproduce; <see cref="Step"/> advances and feeds a builder
    /// directly without allocating (for the hour-long budget test). Ids of despawned actors are reused,
    /// as the live recorder's id pool will. The proportions default to the #425 census (design doc
    /// §6.1–6.2) and can be scaled down for small tests.
    /// </summary>
    internal sealed class FrameTestWorld
    {
        public sealed class TickRecord
        {
            public long Tick;
            public readonly List<KeyValuePair<ushort, byte[]>> Entities = new List<KeyValuePair<ushort, byte[]>>(64);
            public HudRecord Hud;
        }

        private const byte KindStatic = 0, KindWalker = 1, KindAnimated = 2, KindText = 3;

        private struct Actor
        {
            public ushort Id;
            public byte Kind;
            public float X, Y;
            public ushort SpriteBase;
            public int Phase, Period, Life;
            public bool Alive;
        }

        private readonly Random _rng;
        private readonly List<Actor> _actors = new List<Actor>(512);
        private readonly List<int> _deadSlots = new List<int>(64);
        private readonly int _staticCount;
        private ushort _nextId = 1;
        private byte[] _scratch = new byte[4096];
        private long _tick;
        private float _gold = 100f;

        /// <summary>Probability per tick that an animated sprite despawns and a new one spawns elsewhere.</summary>
        public double RespawnChance = 0.01;
        /// <summary>Probability per tick that a rising text spawns.</summary>
        public double TextSpawnChance = 0.05;

        public FrameTestWorld(int seed, int statics, int walkers, int animated)
        {
            _rng = new Random(seed);
            for (int i = 0; i < statics; i++)
                Spawn(KindStatic);
            _staticCount = statics; // statics never move or die and sit first in the list, so the update loop skips them
            for (int i = 0; i < walkers; i++)
                Spawn(KindWalker);
            for (int i = 0; i < animated; i++)
                Spawn(KindAnimated);
        }

        /// <summary>The census mix: ~300 static, 4 walking composites, ~60 animated sprites.</summary>
        public static FrameTestWorld CensusMix(int seed) => new FrameTestWorld(seed, 300, 4, 60);

        /// <summary>A small mix for codec tests.</summary>
        public static FrameTestWorld Small(int seed) => new FrameTestWorld(seed, 6, 2, 6);

        public int LiveCount => _actors.Count - _deadSlots.Count;

        private void Spawn(byte kind)
        {
            var a = new Actor
            {
                Kind = kind,
                X = _rng.Next(0, 1900),
                Y = _rng.Next(0, 260),
                SpriteBase = (ushort)_rng.Next(1, 500),
                Phase = _rng.Next(0, 16),
                Period = kind == KindWalker ? 8 : _rng.Next(6, 16),
                Life = kind == KindText ? _rng.Next(20, 60) : int.MaxValue,
                Alive = true,
            };
            // Reuse a dead actor's id most of the time (an id that vanished comes back with new content);
            // otherwise mint a new id while the id space lasts
            if (_deadSlots.Count > 0 && (_nextId >= 60000 || _rng.NextDouble() < 0.7))
            {
                int slot = _deadSlots[_deadSlots.Count - 1];
                _deadSlots.RemoveAt(_deadSlots.Count - 1);
                a.Id = _actors[slot].Id;
                _actors[slot] = a;
                return;
            }
            a.Id = _nextId++;
            _actors.Add(a);
        }

        private void Kill(int slot)
        {
            var a = _actors[slot];
            a.Alive = false;
            _actors[slot] = a;
            _deadSlots.Add(slot);
        }

        private long Advance()
        {
            long tick = _tick++;
            int count = _actors.Count; // spawns during the loop append or reuse dead slots; either is fine
            for (int i = _staticCount; i < count; i++)
            {
                var a = _actors[i];
                if (!a.Alive)
                    continue;
                switch (a.Kind)
                {
                    case KindWalker:
                        if (_rng.NextDouble() < 0.5) a.X += _rng.Next(0, 2) == 0 ? 1f : -1f;
                        if (_rng.NextDouble() < 0.25) a.Y += _rng.Next(0, 2) == 0 ? 1f : -1f;
                        break;
                    case KindAnimated:
                        if (_rng.NextDouble() < 0.25) a.X += 0.5f;
                        if (_rng.NextDouble() < RespawnChance)
                        {
                            Kill(i);
                            Spawn(KindAnimated);
                            continue;
                        }
                        break;
                    case KindText:
                        a.Y -= 0.5f;
                        if (--a.Life <= 0)
                        {
                            Kill(i);
                            continue;
                        }
                        break;
                }
                _actors[i] = a;
            }
            if (_rng.NextDouble() < TextSpawnChance)
                Spawn(KindText);
            _gold += 0.01f;
            return tick;
        }

        private int EmitOps(in Actor a, long tick)
        {
            var w = new FrameWriter(_scratch);
            int frame = (int)((tick + a.Phase) / a.Period) & 3;
            switch (a.Kind)
            {
                case KindStatic:
                    w.WriteSprite(a.SpriteBase, a.X, a.Y, 0.5f, 20, 0xFFFFFFFF, FrameOpFlags.None);
                    break;
                case KindWalker:
                    w.WriteCompositeHeader(a.X, a.Y, a.Y / 300f, 30, 8);
                    for (int l = 0; l < 8; l++)
                        w.WriteCompositeLayer((ushort)(a.SpriteBase + l * 4 + frame), 0f, -l, 0xFFFFFFFF, FrameOpFlags.None);
                    break;
                case KindAnimated:
                    w.WriteSprite((ushort)(a.SpriteBase + frame), a.X, a.Y, a.Y / 300f, 30, 0xFFFFFFFF, FrameOpFlags.FlipX);
                    break;
                case KindText:
                    w.WriteText((ushort)(a.SpriteBase % 50 + 1), a.X, a.Y, 0xFF0000FF, 1f, 0, FrameOpFlags.Centered);
                    break;
            }
            _scratch = w.Buffer;
            return w.Length;
        }

        /// <summary>HUD numbers at label granularity: the clock and HP move once per sim second, gold as it accrues.</summary>
        private HudRecord HudAt(long tick) => new HudRecord
        {
            Hero = new HudMember { Present = true, Hp = 80 + (int)(tick / 60 % 7), MaxHp = 95, Mp = 20, MaxMp = 22, Level = 3 },
            Merc1 = new HudMember { Present = true, Hp = 40, MaxHp = 50, Mp = 5, MaxMp = 8, Level = 2 },
            Gold = (long)_gold,
            PitLevel = 4,
            PitTier = 1,
            InGameSeconds = tick / 60,
            Paused = false,
        };

        /// <summary>Advances the world one tick and returns what the capture would record.</summary>
        public TickRecord Capture()
        {
            long tick = Advance();
            var rec = new TickRecord { Tick = tick };
            for (int i = 0; i < _actors.Count; i++)
            {
                var a = _actors[i];
                if (!a.Alive)
                    continue;
                int len = EmitOps(a, tick);
                var copy = new byte[len];
                Buffer.BlockCopy(_scratch, 0, copy, 0, len);
                rec.Entities.Add(new KeyValuePair<ushort, byte[]>(a.Id, copy));
            }
            rec.Hud = HudAt(tick);
            return rec;
        }

        /// <summary>Advances one tick and feeds it straight into a builder (no reference kept, no allocation).</summary>
        public void Step(FrameChunkBuilder builder)
        {
            long tick = Advance();
            builder.BeginTick(tick);
            for (int i = 0; i < _actors.Count; i++)
            {
                var a = _actors[i];
                if (!a.Alive)
                    continue;
                int len = EmitOps(a, tick);
                builder.AddEntity(a.Id, _scratch, 0, len);
            }
            builder.EndTick(HudAt(tick));
        }

        /// <summary>Feeds a reference record into a builder as the capture would.</summary>
        public static void Feed(FrameChunkBuilder builder, TickRecord rec)
        {
            builder.BeginTick(rec.Tick);
            for (int i = 0; i < rec.Entities.Count; i++)
                builder.AddEntity(rec.Entities[i].Key, rec.Entities[i].Value, 0, rec.Entities[i].Value.Length);
            builder.EndTick(rec.Hud);
        }

        /// <summary>Asserts a decoded frame holds exactly the reference entities, bytes and HUD.</summary>
        public static void AssertFrameEquals(TickRecord expected, DecodedFrame actual, string context = null)
        {
            Assert.IsNotNull(actual, context);
            Assert.AreEqual(expected.Tick, actual.Tick, "tick " + context);
            Assert.AreEqual(expected.Entities.Count, actual.LiveCount, "entity count at tick " + expected.Tick + " " + context);
            for (int i = 0; i < expected.Entities.Count; i++)
            {
                var e = expected.Entities[i];
                Assert.IsTrue(actual.TryGetEntity(e.Key, out var got), "entity " + e.Key + " missing at tick " + expected.Tick + " " + context);
                Assert.AreEqual(e.Value.Length, got.Length, "entity " + e.Key + " length at tick " + expected.Tick);
                Assert.IsTrue(new ReadOnlySpan<byte>(e.Value).SequenceEqual(new ReadOnlySpan<byte>(actual.OpsBuffer, got.Offset, got.Length)),
                    "entity " + e.Key + " bytes at tick " + expected.Tick + " " + context);
            }
            Assert.AreEqual(expected.Hud, actual.Hud, "hud at tick " + expected.Tick + " " + context);
        }

        /// <summary>Encodes <paramref name="chunkCount"/> chunks of <paramref name="chunkTicks"/> ticks each, keeping every reference record.</summary>
        public static List<FrameChunk> EncodeChunks(FrameTestWorld world, int chunkTicks, int chunkCount, List<TickRecord> records, SpriteKeyRegistry registry = null)
        {
            var builder = new FrameChunkBuilder(chunkTicks);
            var chunks = new List<FrameChunk>(chunkCount);
            for (int c = 0; c < chunkCount; c++)
            {
                for (int t = 0; t < chunkTicks; t++)
                {
                    var rec = world.Capture();
                    records.Add(rec);
                    Feed(builder, rec);
                }
                chunks.Add(FrameChunkCodec.Compress(builder.Finish(registry)));
            }
            return chunks;
        }
    }
}
