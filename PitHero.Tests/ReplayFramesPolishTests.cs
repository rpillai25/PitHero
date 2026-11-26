using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nez.Particles;
using PitHero.Rendering;
using PitHero.Services.Replay.Frames;

namespace PitHero.Tests
{
    /// <summary>
    /// Issue #431: the Particle op round-trips and skips like every other op; the view-only speed ladder
    /// extends the simulation ladder with aligned labels; the viewer's re-simulated particle effects are
    /// the same at a tick whether the playhead stepped there or jumped there, and effects absent from a
    /// frame are dropped.
    /// </summary>
    [TestClass]
    public class ReplayFramesPolishTests
    {
        [TestMethod]
        public void ParticleOp_RoundTrips_AndSkipOpWalksIt()
        {
            var w = new FrameWriter(new byte[8]);
            w.WriteSprite(1, 10f, 10f, 0f, 0, 0xFFFFFFFF, 0);
            w.WriteParticle(effectKey: 2, densityScale: 3f, x: 100.4f, y: -20.6f, elapsedTicks: 77, renderLayer: 60, layerDepth: 0.5f, flags: FrameOpFlags.Emitting);
            w.WriteParticle(effectKey: 0, densityScale: 1f, x: 0f, y: 0f, elapsedTicks: 70000, renderLayer: 0, layerDepth: 0f, flags: FrameOpFlags.None);
            Assert.AreEqual(1 + FrameOpCode.SpritePayload + 2 * (1 + FrameOpCode.ParticlePayload), w.Length);

            var r = new FrameReader(w.Buffer, 0, w.Length);
            r.SkipOp();
            Assert.AreEqual(FrameOpCode.Particle, r.ReadOpCode());
            r.ReadParticle(out var op);
            Assert.AreEqual((byte)2, op.EffectKey);
            Assert.AreEqual(3f, op.DensityScale);
            Assert.AreEqual((short)100, op.X);
            Assert.AreEqual((short)-21, op.Y);
            Assert.AreEqual((ushort)77, op.ElapsedTicks);
            Assert.AreEqual((short)60, op.RenderLayer);
            Assert.AreEqual(0.5f, op.LayerDepth);
            Assert.IsTrue(op.IsEmitting);
            Assert.AreEqual(FrameOpCode.Particle, r.ReadOpCode());
            r.ReadParticle(out var clamped);
            Assert.AreEqual(FrameWriter.EncodeParticleAge(70000), clamped.ElapsedTicks, "ages beyond u16 wrap back above the rebuild cap (format v22)");
            Assert.IsTrue(clamped.ElapsedTicks >= GameConfig.ReplayFrameViewParticleRebuildMaxTicks);
            Assert.IsFalse(clamped.IsEmitting);
            Assert.IsTrue(r.AtEnd);

            // A decoded record writes back byte-identical
            var w2 = new FrameWriter(new byte[8]);
            w2.WriteParticle(in op);
            var r2 = new FrameReader(w2.Buffer, 0, w2.Length);
            r2.SkipOp();
            Assert.IsTrue(r2.AtEnd);
            Assert.IsTrue(new System.ReadOnlySpan<byte>(w.Buffer, 1 + FrameOpCode.SpritePayload, 1 + FrameOpCode.ParticlePayload).SequenceEqual(w2.Written));
        }

        [TestMethod]
        public void FrameViewSpeedLadder_ExtendsTheSimulationLadder_WithAlignedLabels()
        {
            var sim = GameConfig.SpeedSteps;
            var view = GameConfig.ReplayFrameViewSpeedSteps;
            Assert.AreEqual(GameConfig.SpeedStepLabels.Length, sim.Length);
            Assert.AreEqual(GameConfig.ReplayFrameViewSpeedStepLabels.Length, view.Length);
            Assert.IsTrue(view.Length > sim.Length, "the view ladder adds rungs nothing simulated could reach");
            for (int i = 0; i < sim.Length; i++)
            {
                Assert.AreEqual(sim[i], view[i], "rung " + i + " is shared");
                Assert.AreEqual(GameConfig.SpeedStepLabels[i], GameConfig.ReplayFrameViewSpeedStepLabels[i]);
            }
            for (int i = 1; i < view.Length; i++)
                Assert.IsTrue(view[i] > view[i - 1], "the ladder climbs");
            Assert.IsTrue(GameConfig.ReplayFrameViewSoundMaxSpeedIndex < view.Length);
        }

        private static ParticleEmitterConfig TestConfig()
        {
            return new ParticleEmitterConfig
            {
                MaxParticles = 40,
                EmissionRate = 30f,
                Duration = 1f,
                ParticleLifespan = 0.8f,
                ParticleLifespanVariance = 0.3f,
                Speed = 20f,
                SpeedVariance = 10f,
                Angle = 90f,
                AngleVariance = 180f,
                SourcePositionVariance = new Vector2(4f, 4f),
                StartParticleSize = 4f,
                FinishParticleSize = 1f,
                StartColor = Color.White,
                FinishColor = Color.Transparent,
                Gravity = new Vector2(0f, -10f),
                EmitterType = ParticleEmitterType.Gravity,
                BlendFuncSource = Blend.SourceAlpha,
                BlendFuncDestination = Blend.One,
            };
        }

        private static RecordedParticlePool NewPool()
        {
            var config = TestConfig();
            return new RecordedParticlePool { ConfigSource = _ => config };
        }

        private static ParticleOp OpAt(long frameTick, long startTick, bool emitting = true)
        {
            return new ParticleOp
            {
                EffectKey = 1,
                Density = 10,
                X = 320,
                Y = 240,
                ElapsedTicks = (ushort)(frameTick - startTick),
                RenderLayer = 0,
                LayerDepth = 0f,
                Flags = emitting ? FrameOpFlags.Emitting : FrameOpFlags.None,
            };
        }

        private static List<(Vector2 pos, float size)> Snapshot(RecordedParticlePool.Effect fx)
        {
            var list = new List<(Vector2, float)>(fx.Particles.Count);
            for (int i = 0; i < fx.Particles.Count; i++)
                list.Add((fx.Particles[i].SpawnPosition + fx.Particles[i].Position, fx.Particles[i].ParticleSize));
            return list;
        }

        [TestMethod]
        public void ParticlePool_SteppingAndJumpingReachTheSamePicture_AndSeedsAreStable()
        {
            const long start = 1000;
            const long target = 1030;
            var stepped = NewPool();
            stepped.BeginFrame();
            RecordedParticlePool.Effect fx = null;
            for (long t = start; t <= target; t++)
                fx = stepped.Touch(7, OpAt(t, start), t);
            Assert.IsNotNull(fx);
            Assert.AreEqual(30, fx.SimulatedTicks);
            Assert.IsTrue(fx.Particles.Count > 0, "half a second in, the effect has particles");
            var byStepping = Snapshot(fx);

            var jumped = NewPool();
            jumped.BeginFrame();
            var fx2 = jumped.Touch(7, OpAt(target, start), target);
            Assert.AreEqual(30, fx2.SimulatedTicks);
            CollectionAssert.AreEqual(byStepping, Snapshot(fx2), "a seek lands on the same particles as forward play");

            // Rewinding one tick rebuilds from the seed and matches a fresh pool at that tick
            var back = stepped.Touch(7, OpAt(target - 1, start), target - 1);
            var fresh = NewPool();
            fresh.BeginFrame();
            var freshBack = fresh.Touch(7, OpAt(target - 1, start), target - 1);
            CollectionAssert.AreEqual(Snapshot(freshBack), Snapshot(back), "a rewind is a rebuild with the same seed");

            // A different start tick is a different effect (another seed), even at the same age
            var other = NewPool();
            other.BeginFrame();
            var fxOther = other.Touch(7, OpAt(target + 5, start + 5), target + 5);
            CollectionAssert.AreNotEqual(byStepping, Snapshot(fxOther));
        }

        [TestMethod]
        public void ParticlePool_DropsEffectsMissingFromTheFrame_AndStopsEmittingWhenTheOpSaysSo()
        {
            var pool = NewPool();
            pool.BeginFrame();
            pool.Touch(1, OpAt(10, 0), 10);
            pool.Touch(2, OpAt(10, 4), 10);
            pool.EndFrame();
            Assert.AreEqual(2, pool.Count);

            pool.BeginFrame();
            var fx = pool.Touch(1, OpAt(11, 0, emitting: false), 11);
            pool.EndFrame();
            Assert.AreEqual(1, pool.Count, "the emitter absent from the frame is gone");
            Assert.IsNull(pool.Find(2));
            Assert.IsFalse(fx.Emitting, "the recording says emission stopped");
            int count = fx.Particles.Count;
            pool.BeginFrame();
            pool.Touch(1, OpAt(12, 0, emitting: false), 12);
            pool.EndFrame();
            Assert.IsTrue(fx.Particles.Count <= count, "no new particles once emission stopped");

            pool.Clear();
            Assert.AreEqual(0, pool.Count);
        }

        [TestMethod]
        public void ParticlePool_AnEffectOlderThanTheRebuildCap_KeepsSteppingOneTickPerFrame()
        {
            // Issue #469: the wizard orb's motes emit for as long as the orb sits uncovered
            const long start = 0;
            long age = GameConfig.ReplayFrameViewParticleRebuildMaxTicks + 3000;
            var continuous = TestConfig();
            continuous.Duration = -1f;
            var pool = new RecordedParticlePool { ConfigSource = _ => continuous };
            pool.BeginFrame();
            var fx = pool.Touch(3, OpAt(start + age, start), start + age);
            Assert.AreEqual(GameConfig.ReplayFrameViewParticleRebuildMaxTicks, fx.SimulatedTicks, "a seek simulates at most the cap");
            Assert.IsTrue(fx.Particles.Count > 0);

            for (int i = 1; i <= 5; i++)
            {
                var next = pool.Touch(3, OpAt(start + age + i, start), start + age + i);
                Assert.AreSame(fx, next);
                Assert.AreEqual(GameConfig.ReplayFrameViewParticleRebuildMaxTicks + i, fx.SimulatedTicks, "forward play steps once, it does not rebuild");
            }
        }

        [TestMethod]
        public void ParticleAge_WrapsPastU16AboveTheRebuildCap_InsteadOfSticking()
        {
            Assert.AreEqual(0, FrameWriter.EncodeParticleAge(-4));
            Assert.AreEqual(1234, FrameWriter.EncodeParticleAge(1234));
            Assert.AreEqual(ushort.MaxValue - 1, FrameWriter.EncodeParticleAge(ushort.MaxValue - 1));
            int cap = GameConfig.ReplayFrameViewParticleRebuildMaxTicks;
            Assert.AreEqual(cap, FrameWriter.EncodeParticleAge(ushort.MaxValue));
            Assert.AreEqual(cap + 1, FrameWriter.EncodeParticleAge(ushort.MaxValue + 1L));
            for (long t = 60_000; t < 200_000; t += 997)
            {
                ushort a = FrameWriter.EncodeParticleAge(t), b = FrameWriter.EncodeParticleAge(t + 1);
                Assert.IsTrue(b == a + 1 || b == cap, $"one tick older is one more, or a wrap back to the cap (t={t})");
                if (t >= ushort.MaxValue)
                    Assert.IsTrue(a >= cap, "a wrapped age never drops below the cap, so the rebuild refills a steady effect");
            }
        }
    }
}
