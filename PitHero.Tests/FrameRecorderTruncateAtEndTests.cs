using System;
using System.IO;
using Nez;
using PitHero.ECS.Components;
using PitHero.Services;
using PitHero.Services.Replay.Frames;

namespace PitHero.Tests
{
    /// <summary>
    /// A truncation at or past the stream end (a Time Travel commit at the live tick in the simulated
    /// future) drops nothing but must leave the recorder able to continue its partial chunk: the first
    /// live check froze here because the next tick started a chunk off its boundary and the store
    /// rejected it.
    /// </summary>
    [TestClass]
    public class FrameRecorderTruncateAtEndTests
    {
        private const int Seed = 777;
        private const long RecordedAt = 638200001000000000L;

        private static bool Capture(FrameRecorder rec, SimulationClock clock, long tick, Entity entity, RenderableComponent rc)
        {
            clock.SetTick(tick);
            entity.SetPosition(tick, 0f);
            if (!rec.BeginTickCapture(tick))
                return false;
            rec.CaptureRenderable(rc);
            rec.EndTickCapture(new HudRecord { Gold = tick });
            return true;
        }

        private static void RunCase(bool withSidecar)
        {
            int chunkTicks = GameConfig.ReplayFrameChunkTicks;
            string dir = withSidecar ? Path.Combine(Path.GetTempPath(), "pithero_trunc_" + Guid.NewGuid().ToString("N")) : null;
            if (dir != null)
                Directory.CreateDirectory(dir);
            var clock = new SimulationClock();
            var rec = new FrameRecorder();
            try
            {
                rec.Initialize(Seed, RecordedAt, null, dir);
                var entity = new Entity("box");
                var outline = entity.AddComponent(new BuildingOutlineRenderComponent());

                long end = chunkTicks + 9; // one full chunk plus a partial one
                for (long t = 0; t <= end; t++)
                    Assert.IsTrue(Capture(rec, clock, t, entity, outline));

                // Commit at the live tick: nothing to drop
                rec.TruncateAfter(end);
                Assert.AreEqual(end, rec.Store.EndTick, "every captured tick is in the store after the flush");

                // Recording continues: the partial chunk must be continued, then new chunks appended on boundaries
                long last = 3 * chunkTicks + 5;
                for (long t = end + 1; t <= last; t++)
                    Assert.IsTrue(Capture(rec, clock, t, entity, outline), "tick " + t);
                rec.TruncateAfter(last); // a second commit, on the way back
                for (int spin = 0; spin < 200 && rec.Store.EndTick < last; spin++)
                {
                    System.Threading.Thread.Sleep(10);
                    rec.PresentationUpdate(0f);
                }
                Assert.AreEqual(last, rec.Store.EndTick);
                Assert.AreEqual(4, rec.Store.ChunkCount);
                long[] probes = { 0, end, end + 1, chunkTicks * 2, last };
                for (int i = 0; i < probes.Length; i++)
                {
                    Assert.IsTrue(rec.Store.TryGetFrame(probes[i], out var frame), "frame " + probes[i]);
                    Assert.AreEqual(probes[i], frame.Hud.Gold);
                    Assert.IsTrue(frame.TryGetEntity(1, out var e));
                    var r = frame.ReadOps(e);
                    r.ReadOpCode();
                    r.ReadRect(out var rect);
                    Assert.AreEqual((short)probes[i], rect.X);
                }
            }
            finally
            {
                rec.Detach(handoffToNextScene: false);
                if (dir != null)
                {
                    try { Directory.Delete(dir, true); } catch (IOException) { }
                }
            }
        }

        [TestMethod]
        public void TruncateAtEnd_ContinuesThePartialChunk_MemoryOnly() => RunCase(withSidecar: false);

        [TestMethod]
        public void TruncateAtEnd_ContinuesThePartialChunk_WithSidecar() => RunCase(withSidecar: true);
    }
}
