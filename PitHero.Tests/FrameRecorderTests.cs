using System;
using System.IO;
using Microsoft.Xna.Framework;
using Nez;
using Nez.Tiled;
using PitHero.ECS.Components;
using PitHero.Services;
using PitHero.Services.Replay;
using PitHero.Services.Replay.Frames;

namespace PitHero.Tests
{
    /// <summary>
    /// The recorder's lifecycle without a scene: a fresh session writes its session file; a replay
    /// rebuild adopts the stream and skips recorded ticks but captures past the end; Time Travel
    /// truncates the stream, the file and the tables and continues the cut chunk; the session end
    /// closes the file with a footer that a reader (and the tables) come back from; a new session
    /// deletes stale session files; tile and console hooks land in the chunk with a keyframe.
    /// </summary>
    [TestClass]
    public class FrameRecorderTests
    {
        private const int Seed = 4242;
        private const long RecordedAt = 638200000000000000L;

        private static string NewTempDir()
        {
            var dir = Path.Combine(Path.GetTempPath(), "pithero_recorder_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static TmxLayer NewLayer(int w, int h, uint fill)
        {
            var layer = new TmxLayer { Width = w, Height = h, Grid = new uint[w * h] };
            for (int i = 0; i < layer.Grid.Length; i++)
                layer.Grid[i] = fill;
            return layer;
        }

        private static HudRecord HudAt(long tick) => new HudRecord { Gold = tick, Hero = new HudMember { Present = true, Hp = 10, MaxHp = 10, Level = 1 } };

        /// <summary>Captures one tick: the outline entity sits at x = tick.</summary>
        private static bool Capture(FrameRecorder rec, SimulationClock clock, long tick, Entity entity, RenderableComponent rc)
        {
            clock.SetTick(tick);
            entity.SetPosition(tick, 0f);
            if (!rec.BeginTickCapture(tick))
                return false;
            rec.CaptureRenderable(rc);
            rec.EndTickCapture(HudAt(tick));
            return true;
        }

        private static void AssertOutlineAt(FrameRecorder rec, long tick)
        {
            Assert.IsTrue(rec.Store.TryGetFrame(tick, out var frame), "frame " + tick);
            Assert.AreEqual(1, frame.LiveCount);
            Assert.IsTrue(frame.TryGetEntity(1, out var e));
            var r = frame.ReadOps(e);
            Assert.AreEqual(FrameOpCode.Rect, r.ReadOpCode());
            r.ReadRect(out var rect);
            Assert.AreEqual((short)tick, rect.X, "outline x at tick " + tick);
            Assert.AreEqual(tick, frame.Hud.Gold);
        }

        [TestMethod]
        public void FreshSession_Rebuild_TimeTravel_SessionEnd()
        {
            int chunkTicks = GameConfig.ReplayFrameChunkTicks;
            string dir = NewTempDir();
            var clock = new SimulationClock();
            FrameRecorder rec = null;
            try
            {
                var entity = new Entity("box");
                var outline = entity.AddComponent(new BuildingOutlineRenderComponent());
                outline.SetSize(10f, 10f);
                var fog = NewLayer(4, 3, 13);
                var baseLayer = NewLayer(4, 3, 1);

                // ── Fresh live session ──
                rec = new FrameRecorder();
                rec.Initialize(Seed, RecordedAt, null, dir);
                Assert.IsTrue(rec.IsInitialized);
                string sessionPath = Path.Combine(dir, GameConfig.ReplayFrameSessionFilePrefix + RecordedAt + GameConfig.ReplayFrameFileExtension);
                Assert.AreEqual(sessionPath, rec.SessionFilePath);
                Assert.IsTrue(File.Exists(sessionPath));
                rec.AttachTileLayers(baseLayer, null, fog);

                // Tick-0 tile write (pit generation) before the first capture: keyframe holds the state before it
                clock.SetTick(0);
                rec.OnTileChanging(fog, 1, 1, 0);
                fog.Grid[1 + 1 * 4] = 0;
                rec.OnTileChanging(fog, 1, 1, 0); // same-gid write: not recorded
                rec.OnTileChanging(baseLayer, 0, 0, 1); // same gid: not recorded

                int ticks = chunkTicks + 10;
                for (long t = 0; t < ticks; t++)
                {
                    if (t == 3)
                    {
                        clock.SetTick(3);
                        rec.OnConsoleEmitted(new[] { new ConsoleSegment("hello", Color.White), new ConsoleSegment("Rusty Blade", Color.Cyan, "RustyBlade") });
                    }
                    Assert.IsTrue(Capture(rec, clock, t, entity, outline));
                }
                rec.PresentationUpdate(0f);
                // The worker may still be compressing chunk 0; drain through a no-op truncate is not public, so poll until it lands
                for (int spin = 0; spin < 200 && rec.Store.ChunkCount == 0; spin++)
                {
                    System.Threading.Thread.Sleep(10);
                    rec.PresentationUpdate(0f);
                }
                Assert.AreEqual(1, rec.Store.ChunkCount, "chunk 0 collected from the worker");
                Assert.AreEqual(chunkTicks - 1, rec.Store.EndTick, "ticks 120..129 are still in the builder");
                Assert.AreEqual(ticks, rec.CapturedTicks);
                AssertOutlineAt(rec, 0);
                AssertOutlineAt(rec, 57);
                Assert.IsTrue(rec.Store.TryGetDecodedChunk(0, out var decoded0));
                Assert.IsTrue(decoded0.HasTileKeyframe);
                Assert.AreEqual(2, decoded0.TileKeyframeLayerCount, "base + fog (detail is null)");
                Assert.AreEqual(FrameRecorder.TileLayerFogOfWar, decoded0.GetTileKeyframeLayer(1).LayerIndex);
                Assert.AreEqual(13u, decoded0.GetTileKeyframeLayer(1).Gids[5], "keyframe = state before the tick-0 write");
                Assert.AreEqual(1, decoded0.TileEvents.Count, "same-gid writes are dropped");
                Assert.AreEqual(0, decoded0.TileEvents[0].Tick);
                Assert.AreEqual(FrameRecorder.TileLayerFogOfWar, decoded0.TileEvents[0].Layer);
                Assert.AreEqual((ushort)1, decoded0.TileEvents[0].X);
                Assert.AreEqual(0, decoded0.TileEvents[0].Gid);
                Assert.AreEqual(1, decoded0.ConsoleEvents.Count);
                Assert.AreEqual(3, decoded0.ConsoleEvents[0].Tick);
                Assert.AreEqual((byte)2, decoded0.ConsoleEvents[0].SegmentCount);
                var seg1 = decoded0.ConsoleSegments[decoded0.ConsoleEvents[0].SegmentStart + 1];
                Assert.AreEqual("Rusty Blade", rec.Registry.GetString(seg1.StringId));
                Assert.AreEqual("RustyBlade", rec.Registry.GetString(seg1.ItemStringId));
                Assert.AreEqual(Color.Cyan.PackedValue, seg1.Color);

                // ── Scene rebuild for "Replay Current Session": the stream is handed to the next recorder ──
                rec.Detach(handoffToNextScene: true);
                Assert.IsNull(FrameRecorder.Current);
                Assert.IsTrue(FrameRecorder.HasPendingHandoff);
                var preload = new ReplayData { MasterSeed = Seed, RecordedAtUtcTicks = RecordedAt, TotalTicks = ticks };
                rec = new FrameRecorder();
                rec.Initialize(Seed, RecordedAt, preload, dir);
                rec.IsRecording = false;
                rec.AttachTileLayers(baseLayer, null, fog);
                Assert.IsFalse(FrameRecorder.HasPendingHandoff);
                Assert.AreEqual(ticks - 1, rec.Store.EndTick, "the partial chunk was flushed at the handoff");
                Assert.AreEqual(2, rec.Store.ChunkCount);
                AssertOutlineAt(rec, ticks - 1);
                Assert.AreEqual(2, rec.Registry.FlushCount, "both chunks flushed their table deltas");

                // Playback re-simulates recorded ticks: nothing is captured; past the end it is
                var newEntity = new Entity("box2");
                var newOutline = newEntity.AddComponent(new BuildingOutlineRenderComponent());
                newOutline.SetSize(10f, 10f);
                Assert.IsFalse(Capture(rec, clock, 50, newEntity, newOutline));
                Assert.IsFalse(Capture(rec, clock, ticks - 1, newEntity, newOutline));
                Assert.IsTrue(Capture(rec, clock, ticks, newEntity, newOutline), "first tick past the recorded end");
                Assert.IsTrue(Capture(rec, clock, ticks + 1, newEntity, newOutline));
                Assert.AreEqual(ticks - 1, rec.Store.EndTick, "still in the builder");

                // ── Time Travel Here at tick 100: everything after is dropped and the cut chunk continues ──
                rec.IsRecording = true;
                rec.TruncateAfter(100);
                Assert.AreEqual(100, rec.Store.EndTick);
                Assert.AreEqual(1, rec.Store.ChunkCount);
                Assert.IsFalse(rec.Store.TryGetFrame(101, out _));
                AssertOutlineAt(rec, 100);
                Assert.AreEqual(0, rec.Registry.FlushCount, "no complete chunk remains: every table entry is pending again");
                Assert.IsFalse(Capture(rec, clock, 50, newEntity, newOutline), "not the next tick");
                for (long t = 101; t <= ticks; t++)
                    Assert.IsTrue(Capture(rec, clock, t, newEntity, newOutline), "tick " + t);
                for (int spin = 0; spin < 200 && rec.Store.EndTick < ticks - 1; spin++)
                {
                    System.Threading.Thread.Sleep(10);
                    rec.PresentationUpdate(0f);
                }
                Assert.AreEqual(chunkTicks - 1, rec.Store.EndTick, "chunk 0 re-finished with the continued ticks; 120..130 in the builder");
                AssertOutlineAt(rec, 100);
                AssertOutlineAt(rec, 110);
                Assert.IsTrue(rec.Store.TryGetDecodedChunk(0, out decoded0));
                Assert.AreEqual(1, decoded0.TileEvents.Count, "the tick-0 event survived the truncation and re-encode");
                Assert.IsTrue(decoded0.HasTileKeyframe);

                // ── Session end: footer, tables and frames readable from the file ──
                rec.Detach(handoffToNextScene: false);
                Assert.IsFalse(FrameRecorder.HasPendingHandoff);
                Assert.AreEqual(FrameSidecarFile.OpenResult.Ok, FrameSidecarReader.Open(sessionPath, Seed, RecordedAt, GameConfig.SimulationVersion, out var reader));
                using (reader)
                {
                    Assert.IsTrue(reader.HasFooter);
                    Assert.AreEqual(2, reader.ChunkCount);
                    Assert.AreEqual(ticks + 1, reader.TotalTicks, "ticks 0..130");
                    var registry = new SpriteKeyRegistry();
                    reader.RebuildRegistry(registry);
                    Assert.AreEqual((ushort)1, registry.InternString("hello"), "table entries re-declared in the re-finished chunk 0");
                    var store = new FrameStore(chunkTicks, long.MaxValue);
                    store.Preload(reader);
                    for (long t = 0; t <= ticks; t += 13)
                    {
                        Assert.IsTrue(store.TryGetFrame(t, out var frame), "tick " + t);
                        Assert.IsTrue(frame.TryGetEntity(1, out var e));
                        var r = frame.ReadOps(e);
                        Assert.AreEqual(FrameOpCode.Rect, r.ReadOpCode());
                        r.ReadRect(out var rect);
                        Assert.AreEqual((short)t, rect.X);
                    }
                }

                // ── A new session deletes the old session file ──
                rec = new FrameRecorder();
                rec.Initialize(Seed + 1, RecordedAt + 1, null, dir);
                Assert.IsFalse(File.Exists(sessionPath), "stale session file removed");
                Assert.IsTrue(File.Exists(rec.SessionFilePath));
                rec.Detach(handoffToNextScene: false);
                rec = null;
            }
            finally
            {
                rec?.Detach(handoffToNextScene: false);
                FrameRecorder.DiscardPendingHandoff();
                clock.Detach();
                try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
            }
        }

        /// <summary>
        /// Save Session Replay mid-session (issue #429): the frames so far, including the ticks still
        /// in the builder, become a finished cache beside the recording through the file service, the
        /// console lines ride in its footer, and recording carries on in the session file. Quit-time
        /// saves move the session file instead and capture continues in memory.
        /// </summary>
        [TestMethod]
        public void ExportSidecar_MidSessionCopies_EndSessionMoves()
        {
            int chunkTicks = GameConfig.ReplayFrameChunkTicks;
            string dir = NewTempDir();
            var clock = new SimulationClock();
            FrameRecorder rec = null;
            try
            {
                var entity = new Entity("box");
                var outline = entity.AddComponent(new BuildingOutlineRenderComponent());
                outline.SetSize(10f, 10f);
                var files = new ReplayFileService(dir);

                rec = new FrameRecorder();
                rec.Initialize(Seed, RecordedAt, null, dir);
                string sessionPath = rec.SessionFilePath;
                int ticks = chunkTicks + 7;
                for (long t = 0; t < ticks; t++)
                {
                    if (t == 5)
                    {
                        clock.SetTick(5);
                        rec.OnConsoleEmitted(new[] { new ConsoleSegment("line", Color.White) });
                    }
                    Assert.IsTrue(Capture(rec, clock, t, entity, outline));
                }

                // ── Mid-session save: a copy; the recorder keeps going ──
                var data = new ReplayData
                {
                    Kind = ReplayKind.Load, MasterSeed = Seed, HeroName = "Ann", JobName = "Knight", HeroId = 1,
                    RecordedAtUtcTicks = RecordedAt, TotalTicks = ticks, BuildId = "t", SimulationVersion = GameConfig.SimulationVersion,
                    StateBlob = new byte[] { 1 },
                };
                string fileName = files.SaveWithFrameCache(data, rec, endSession: false);
                Assert.IsNotNull(fileName);
                string cache = files.FrameCachePath(fileName);
                Assert.IsTrue(File.Exists(cache), "cache written beside the recording");
                Assert.IsTrue(File.Exists(sessionPath), "session file kept");
                Assert.AreEqual(ticks - 1, rec.Store.EndTick, "the builder's ticks were flushed into the stream");
                Assert.AreEqual(FrameSidecarFile.OpenResult.Ok, files.TryOpenFrameCache(fileName, Seed, RecordedAt, GameConfig.SimulationVersion, ticks, out var reader));
                using (reader)
                {
                    Assert.AreEqual(2, reader.ChunkCount);
                    Assert.AreEqual(ticks, reader.TotalTicks);
                    Assert.AreEqual(ticks - 1, reader.EndTick);
                    var log = new RecordedConsoleLog();
                    reader.ReadConsoleLog(log);
                    Assert.AreEqual(1, log.Count);
                    Assert.AreEqual(5, log[0].Tick);
                    Assert.AreEqual("line", log[0].Segments[0].Text);
                    var rebuilt = new SpriteKeyRegistry();
                    reader.RebuildRegistry(rebuilt);
                    var store = new FrameStore(reader.ChunkTicks, long.MaxValue);
                    store.Preload(reader);
                    for (long t = 0; t < ticks; t += 5)
                    {
                        Assert.IsTrue(store.TryGetFrame(t, out var frame), "tick " + t);
                        Assert.IsTrue(frame.TryGetEntity(1, out var e));
                        var r = frame.ReadOps(e);
                        Assert.AreEqual(FrameOpCode.Rect, r.ReadOpCode());
                        r.ReadRect(out var rect);
                        Assert.AreEqual((short)t, rect.X);
                    }
                }
                var listed = files.Enumerate();
                Assert.AreEqual(1, listed.Count);
                Assert.IsTrue(listed[0].HasFrameCache);

                // Recording continues the partial chunk in the session file
                for (long t = ticks; t < 2 * chunkTicks + 3; t++)
                    Assert.IsTrue(Capture(rec, clock, t, entity, outline), "tick " + t);
                rec.FlushPending();
                Assert.AreEqual(2 * chunkTicks + 2, rec.Store.EndTick);
                AssertOutlineAt(rec, ticks + 2);
                AssertOutlineAt(rec, 2 * chunkTicks + 1);

                // ── Quit-time save: the session file becomes the cache ──
                long total = 2 * chunkTicks + 3;
                data.TotalTicks = total;
                data.RecordedAtUtcTicks = RecordedAt; // same session, a later save
                string fileName2 = files.SaveWithFrameCache(data, rec, endSession: true);
                Assert.IsNotNull(fileName2);
                Assert.AreNotEqual(fileName, fileName2);
                string cache2 = files.FrameCachePath(fileName2);
                Assert.IsTrue(File.Exists(cache2));
                Assert.IsFalse(File.Exists(sessionPath), "session file moved");
                Assert.IsNull(rec.SessionFilePath);
                Assert.AreEqual(FrameSidecarFile.OpenResult.Ok, files.TryOpenFrameCache(fileName2, Seed, RecordedAt, GameConfig.SimulationVersion, total, out var reader2));
                using (reader2)
                {
                    Assert.AreEqual(3, reader2.ChunkCount);
                    Assert.AreEqual(total - 1, reader2.EndTick);
                }
                // Capture still works in memory after the move
                Assert.IsTrue(Capture(rec, clock, total, entity, outline));
                rec.FlushPending();
                AssertOutlineAt(rec, total);

                rec.Detach(handoffToNextScene: false);
                rec = null;
                Assert.IsTrue(File.Exists(cache), "the first cache is untouched");
                Assert.IsTrue(File.Exists(cache2));
            }
            finally
            {
                rec?.Detach(handoffToNextScene: false);
                FrameRecorder.DiscardPendingHandoff();
                clock.Detach();
                try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
            }
        }

        /// <summary>
        /// Self-caching of an uncached saved replay (issue #431): a recorder preloaded with the recording
        /// (a Simulated playback) captures past the stream end up to TotalTicks and exports a cache the
        /// file service accepts; a stream that stops short exports nothing and keeps its session file.
        /// </summary>
        [TestMethod]
        public void SelfCache_PreloadedReplayCapturedToTheEnd_ExportsAnAcceptedCache_ShortStreamExportsNothing()
        {
            int chunkTicks = GameConfig.ReplayFrameChunkTicks;
            string dir = NewTempDir();
            var clock = new SimulationClock();
            FrameRecorder rec = null;
            try
            {
                var entity = new Entity("box");
                var outline = entity.AddComponent(new BuildingOutlineRenderComponent());
                outline.SetSize(10f, 10f);
                var files = new ReplayFileService(dir);
                long total = chunkTicks + 30;
                var data = new ReplayData
                {
                    Kind = ReplayKind.Load, MasterSeed = Seed, HeroName = "Ann", JobName = "Knight", HeroId = 1,
                    RecordedAtUtcTicks = RecordedAt, TotalTicks = total, BuildId = "t", SimulationVersion = GameConfig.SimulationVersion,
                    StateBlob = new byte[] { 1 },
                };
                string fileName = files.Save(data);
                string cache = files.FrameCachePath(fileName);
                Assert.IsFalse(File.Exists(cache));

                // ── Left before the end: nothing is cached, the session file keeps the partial stream ──
                rec = new FrameRecorder();
                rec.Initialize(Seed, RecordedAt, data, dir);
                rec.IsRecording = false; // playback: already recorded ticks are skipped, ticks past the end captured
                string sessionPath = rec.SessionFilePath;
                Assert.IsTrue(File.Exists(sessionPath));
                for (long t = 0; t < 40; t++)
                    Assert.IsTrue(Capture(rec, clock, t, entity, outline), "tick " + t);
                rec.FlushPending();
                Assert.AreEqual(39, rec.Store.EndTick);
                Assert.IsFalse(rec.ExportSidecar(cache, total, endSession: true), "a short stream exports nothing");
                Assert.IsFalse(File.Exists(cache));
                Assert.IsTrue(File.Exists(sessionPath), "the partial stream stays for a later pass");
                rec.Detach(handoffToNextScene: false);

                // ── A later pass reopens the session file, skips what it has and captures to the end ──
                rec = new FrameRecorder();
                rec.Initialize(Seed, RecordedAt, data, dir);
                rec.IsRecording = false;
                Assert.AreEqual(39, rec.Store.EndTick, "the earlier pass's frames were reopened");
                for (long t = 0; t < 40; t++)
                    Assert.IsFalse(Capture(rec, clock, t, entity, outline), "recorded tick " + t + " is skipped");
                for (long t = 40; t < total; t++)
                    Assert.IsTrue(Capture(rec, clock, t, entity, outline), "tick " + t);
                Assert.IsTrue(rec.ExportSidecar(cache, total, endSession: true), "the complete stream becomes the cache");
                Assert.IsTrue(File.Exists(cache));
                Assert.IsFalse(File.Exists(sessionPath), "the session file was moved beside the recording");
                Assert.AreEqual(FrameSidecarFile.OpenResult.Ok, files.TryOpenFrameCache(fileName, Seed, RecordedAt, GameConfig.SimulationVersion, total, out var reader));
                using (reader)
                {
                    Assert.AreEqual(total, reader.TotalTicks);
                    Assert.AreEqual(total - 1, reader.EndTick);
                    var registry = new SpriteKeyRegistry();
                    reader.RebuildRegistry(registry);
                    var store = new FrameStore(reader.ChunkTicks, long.MaxValue);
                    store.Preload(reader);
                    for (long t = 0; t < total; t += 11)
                    {
                        Assert.IsTrue(store.TryGetFrame(t, out var frame), "tick " + t);
                        Assert.IsTrue(frame.TryGetEntity(1, out var e));
                        var r = frame.ReadOps(e);
                        Assert.AreEqual(FrameOpCode.Rect, r.ReadOpCode());
                        r.ReadRect(out var rect);
                        Assert.AreEqual((short)t, rect.X);
                    }
                }
                Assert.IsTrue(files.Enumerate()[0].HasFrameCache, "the row gains its Cached mark");
                rec.Detach(handoffToNextScene: false);
                rec = null;
            }
            finally
            {
                rec?.Detach(handoffToNextScene: false);
                FrameRecorder.DiscardPendingHandoff();
                clock.Detach();
                try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
            }
        }

        /// <summary>The sound hook (issue #431) lands in the open chunk with the resolved variant; UI clicks and already-recorded ticks are ignored.</summary>
        [TestMethod]
        public void SoundHook_RecordsSimulationSounds_SkipsUiClicks_AndRecordedTicks()
        {
            string dir = NewTempDir();
            var clock = new SimulationClock();
            FrameRecorder rec = null;
            try
            {
                var entity = new Entity("box");
                var outline = entity.AddComponent(new BuildingOutlineRenderComponent());
                outline.SetSize(10f, 10f);
                rec = new FrameRecorder();
                rec.Initialize(Seed, RecordedAt, null, dir);
                for (long t = 0; t < 10; t++)
                {
                    Assert.IsTrue(Capture(rec, clock, t, entity, outline));
                    if (t == 3)
                    {
                        rec.OnSoundPlayed(PitHero.Util.SoundEffectTypes.SoundEffectType.Restorative, 2, new Vector2(100.4f, -7.6f), positional: true);
                        rec.OnSoundPlayed(PitHero.Util.SoundEffectTypes.SoundEffectType.NormalButtonClick, 0, Vector2.Zero, positional: false);
                    }
                    if (t == 7)
                        rec.OnSoundPlayed(PitHero.Util.SoundEffectTypes.SoundEffectType.PayGold, 0, Vector2.Zero, positional: false);
                }
                rec.FlushPending();
                Assert.IsTrue(rec.Store.TryGetDecodedChunk(0, out var chunk));
                Assert.AreEqual(2, chunk.SoundEvents.Count, "the UI click is not a simulation sound");
                Assert.AreEqual(3, chunk.SoundEvents[0].Tick);
                Assert.AreEqual((byte)PitHero.Util.SoundEffectTypes.SoundEffectType.Restorative, chunk.SoundEvents[0].Type);
                Assert.AreEqual((byte)2, chunk.SoundEvents[0].Variant);
                Assert.AreEqual((short)100, chunk.SoundEvents[0].X);
                Assert.AreEqual((short)-8, chunk.SoundEvents[0].Y);
                Assert.IsTrue(chunk.SoundEvents[0].IsPositional);
                Assert.AreEqual(7, chunk.SoundEvents[1].Tick);
                Assert.IsFalse(chunk.SoundEvents[1].IsPositional);

                // Playback over recorded ticks records nothing more
                rec.IsRecording = false;
                clock.SetTick(5);
                rec.OnSoundPlayed(PitHero.Util.SoundEffectTypes.SoundEffectType.Jump, 0, Vector2.Zero, positional: false);
                rec.FlushPending();
                Assert.IsTrue(rec.Store.TryGetDecodedChunk(0, out chunk));
                Assert.AreEqual(2, chunk.SoundEvents.Count);
            }
            finally
            {
                rec?.Detach(handoffToNextScene: false);
                FrameRecorder.DiscardPendingHandoff();
                clock.Detach();
                try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
            }
        }

        [TestMethod]
        public void Handoff_ToAnotherSession_ClosesTheOldStream_AndTheNewSessionReopensItsOwnFileLater()
        {
            int chunkTicks = GameConfig.ReplayFrameChunkTicks;
            string dir = NewTempDir();
            var clock = new SimulationClock();
            FrameRecorder rec = null;
            try
            {
                var entity = new Entity("box");
                var outline = entity.AddComponent(new BuildingOutlineRenderComponent());

                rec = new FrameRecorder();
                rec.Initialize(Seed, RecordedAt, null, dir);
                string livePath = rec.SessionFilePath;
                for (long t = 0; t < chunkTicks + 5; t++)
                    Capture(rec, clock, t, entity, outline);
                rec.Detach(handoffToNextScene: true);

                // A saved replay of another session takes over: the live stream is closed with a footer
                var other = new ReplayData { MasterSeed = 9, RecordedAtUtcTicks = RecordedAt - 5, TotalTicks = 1000 };
                rec = new FrameRecorder();
                rec.Initialize(9, RecordedAt - 5, other, dir);
                rec.IsRecording = false;
                Assert.IsTrue(rec.Store.IsEmpty, "the other session has no frames yet");
                Assert.IsTrue(File.Exists(livePath), "the set-aside live session's file is kept");
                Assert.AreEqual(FrameSidecarFile.OpenResult.Ok, FrameSidecarReader.Open(livePath, Seed, RecordedAt, GameConfig.SimulationVersion, out var reader));
                using (reader)
                {
                    Assert.IsTrue(reader.HasFooter, "closed cleanly");
                    Assert.AreEqual(2, reader.ChunkCount);
                    Assert.AreEqual(chunkTicks + 5, reader.TotalTicks);
                }
                rec.Detach(handoffToNextScene: true);

                // The way back (ReturnToLiveSession): the live session's file is reopened and continued
                var live = new ReplayData { MasterSeed = Seed, RecordedAtUtcTicks = RecordedAt, TotalTicks = chunkTicks + 5 };
                rec = new FrameRecorder();
                rec.Initialize(Seed, RecordedAt, live, dir);
                Assert.AreEqual(chunkTicks + 4, rec.Store.EndTick);
                Assert.AreEqual(2, rec.Store.ChunkCount);
                AssertOutlineAt(rec, chunkTicks + 2);
                rec.IsRecording = true;
                Assert.IsTrue(Capture(rec, clock, chunkTicks + 5, entity, outline), "continues after the recorded end");
                rec.Detach(handoffToNextScene: false);
                rec = null;
            }
            finally
            {
                rec?.Detach(handoffToNextScene: false);
                FrameRecorder.DiscardPendingHandoff();
                clock.Detach();
                try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
            }
        }
    }
}
