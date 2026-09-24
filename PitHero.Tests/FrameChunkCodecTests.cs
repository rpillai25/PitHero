using System;
using System.Collections.Generic;
using PitHero.Services.Replay.Frames;

namespace PitHero.Tests
{
    /// <summary>
    /// Pins the chunk codec: every decoded tick equals the captured input across spawns, moves and
    /// despawns (tombstones), the HUD change flag, table deltas across chunks, the deflate round-trip,
    /// events and tile keyframes, truncation and mid-chunk resume.
    /// </summary>
    [TestClass]
    public class FrameChunkCodecTests
    {
        private const int ChunkTicks = 120;

        [TestMethod]
        public void SyntheticTicks_EveryDecodedTickEqualsInput_SequentialAndRandomAccess()
        {
            var world = FrameTestWorld.Small(1);
            world.RespawnChance = 0.05;
            world.TextSpawnChance = 0.2;
            var records = new List<FrameTestWorld.TickRecord>();
            var chunks = FrameTestWorld.EncodeChunks(world, ChunkTicks, 3, records);
            Assert.AreEqual(3, chunks.Count);
            Assert.AreEqual(0, chunks[0].FirstTick);
            Assert.AreEqual(ChunkTicks, chunks[1].FirstTick);
            Assert.AreEqual(ChunkTicks, chunks[2].TickCount);

            var frame = new DecodedFrame();
            for (int c = 0; c < chunks.Count; c++)
            {
                var decoded = FrameChunkCodec.Decode(chunks[c]);
                Assert.AreEqual(chunks[c].FirstTick, decoded.FirstTick);
                // Sequential: each step is one delta
                for (int k = 0; k < ChunkTicks; k++)
                {
                    decoded.DecodeFrame(k, frame);
                    FrameTestWorld.AssertFrameEquals(records[c * ChunkTicks + k], frame, "sequential");
                }
                // Random access: rebuilds from the base, or steps forward when already ahead of the base
                var rng = new Random(7 + c);
                for (int n = 0; n < 40; n++)
                {
                    int k = rng.Next(ChunkTicks);
                    decoded.DecodeFrame(k, frame);
                    FrameTestWorld.AssertFrameEquals(records[c * ChunkTicks + k], frame, "random");
                }
            }
        }

        [TestMethod]
        public void Tombstones_EntityGoneSincePreviousTick_IsAbsent_AndReturnsWhenRespawned()
        {
            var builder = new FrameChunkBuilder(ChunkTicks);
            var ops = new byte[32];
            var w = new FrameWriter(ops);
            w.WriteSprite(1, 10, 10, 0f, 0, 0, 0);
            int len = w.Length;
            var hud = new HudRecord();

            // Ticks 0-4: entities 1 and 2; ticks 5-9: only 1; tick 10: 2 returns with different bytes; tick 11: nothing
            for (int t = 0; t < 12; t++)
            {
                builder.BeginTick(t);
                if (t < 11)
                    builder.AddEntity(1, ops, 0, len);
                if (t < 5)
                    builder.AddEntity(2, ops, 0, len);
                if (t == 10)
                {
                    var w2 = new FrameWriter(new byte[32]);
                    w2.WriteText(3, 1, 2, 0, 1f, 0, 0);
                    builder.AddEntity(2, w2.Buffer, 0, w2.Length);
                }
                builder.EndTick(hud);
            }
            var chunk = FrameChunkCodec.Compress(builder.Finish((SpriteKeyRegistry)null));
            var decoded = FrameChunkCodec.Decode(chunk);
            var frame = new DecodedFrame();

            decoded.DecodeFrame(4, frame);
            Assert.AreEqual(2, frame.LiveCount);
            decoded.DecodeFrame(5, frame);
            Assert.AreEqual(1, frame.LiveCount);
            Assert.IsFalse(frame.TryGetEntity(2, out _));
            Assert.AreEqual(4 + 4 + 1, decoded.GetSectionLength(5), "delta 5 = count u32 + one tombstone (id u16, 0xFFFF) + hud flag");
            Assert.AreEqual(4 + 1, decoded.GetSectionLength(6), "nothing changed at tick 6");
            decoded.DecodeFrame(10, frame);
            Assert.IsTrue(frame.TryGetEntity(2, out var e2));
            Assert.AreEqual(1 + FrameOpCode.TextPayload, e2.Length);
            var r = frame.ReadOps(e2);
            Assert.AreEqual(FrameOpCode.Text, r.ReadOpCode());
            decoded.DecodeFrame(11, frame);
            Assert.AreEqual(0, frame.LiveCount);
            // Jumping backwards rebuilds from the base
            decoded.DecodeFrame(0, frame);
            Assert.AreEqual(2, frame.LiveCount);
        }

        [TestMethod]
        public void HudFlag_HudReEmittedOnlyWhenChanged()
        {
            var builder = new FrameChunkBuilder(ChunkTicks);
            var hud = new HudRecord { Gold = 10, Hero = new HudMember { Present = true, Hp = 5 } };
            for (int t = 0; t < 4; t++)
            {
                builder.BeginTick(t);
                if (t == 2)
                    hud.Gold = 11;
                builder.EndTick(hud);
            }
            var chunk = FrameChunkCodec.Compress(builder.Finish((SpriteKeyRegistry)null));
            var decoded = FrameChunkCodec.Decode(chunk);
            Assert.AreEqual(4 + HudRecord.Size, decoded.GetSectionLength(0), "base: count u32 + hud");
            Assert.AreEqual(4 + 1, decoded.GetSectionLength(1), "unchanged: count + flag 0");
            Assert.AreEqual(4 + 1 + HudRecord.Size, decoded.GetSectionLength(2), "changed: count + flag 1 + hud");
            Assert.AreEqual(4 + 1, decoded.GetSectionLength(3));

            var frame = new DecodedFrame();
            decoded.DecodeFrame(1, frame);
            Assert.AreEqual(10, frame.Hud.Gold);
            decoded.DecodeFrame(3, frame);
            Assert.AreEqual(11, frame.Hud.Gold);
            Assert.AreEqual(5, frame.Hud.Hero.Hp);
        }

        [TestMethod]
        public void TableDeltas_AcrossChunks_RebuildTheRegistry()
        {
            var registry = new SpriteKeyRegistry();
            var builder = new FrameChunkBuilder(ChunkTicks);
            var chunks = new List<FrameChunk>();
            var keysByChunk = new List<SpriteKey>();
            for (int c = 0; c < 3; c++)
            {
                // Two new sprites, one string and (in chunk 1) a nine-patch per chunk; chunk 2 re-interns old keys only
                if (c < 2)
                {
                    var k1 = new SpriteKey("Atlases/Actors.png", 32 * c, 0, 32, 32);
                    var k2 = new SpriteKey("Atlases/Actors.png", 32 * c, 32, 32, 32);
                    keysByChunk.Add(k1);
                    keysByChunk.Add(k2);
                    Assert.AreEqual((ushort)(2 * c + 1), registry.InternSprite(k1));
                    Assert.AreEqual((ushort)(2 * c + 2), registry.InternSprite(k2));
                    Assert.AreEqual((ushort)(c + 1), registry.InternString("line " + c));
                }
                else
                {
                    Assert.AreEqual((ushort)1, registry.InternSprite(keysByChunk[0]), "re-interning returns the same id");
                    Assert.AreEqual((ushort)1, registry.InternString("line 0"));
                }
                if (c == 1)
                    Assert.AreEqual((ushort)1, registry.InternNinePatch("bubble"));
                builder.BeginTick(c); // one tick per chunk: Finish advances the next first tick by one
                builder.EndTick(default);
                chunks.Add(FrameChunkCodec.Compress(builder.Finish(registry)));
                Assert.AreEqual(0, registry.PendingCount, "everything flushed by Finish");
            }
            Assert.AreEqual(3, registry.FlushCount);
            Assert.AreEqual(6, chunks[2].TableDeltaLength, "no new entries: three zero counts");
            Assert.IsTrue(chunks[0].TableDeltaLength > 6);

            var rebuilt = new SpriteKeyRegistry();
            for (int c = 0; c < chunks.Count; c++)
            {
                var r = new FrameReader(chunks[c].Bytes, chunks[c].TableDeltaOffset, chunks[c].TableDeltaLength);
                rebuilt.ReadTableDelta(ref r);
                Assert.IsTrue(r.AtEnd, "table delta fully consumed");
            }
            Assert.AreEqual(4, rebuilt.SpriteCount);
            Assert.AreEqual(2, rebuilt.StringCount);
            Assert.AreEqual(1, rebuilt.NinePatchCount);
            Assert.AreEqual(3, rebuilt.FlushCount);
            for (int i = 0; i < keysByChunk.Count; i++)
            {
                Assert.IsTrue(rebuilt.TryGetSpriteKey((ushort)(i + 1), out var key));
                Assert.AreEqual(keysByChunk[i], key);
                Assert.AreEqual((ushort)(i + 1), rebuilt.InternSprite(keysByChunk[i]), "rebuilt table resolves the same ids");
            }
            Assert.AreEqual("line 1", rebuilt.GetString(2));
            Assert.AreEqual("bubble", rebuilt.GetNinePatch(1));
            Assert.IsNull(rebuilt.GetString(SpriteKeyRegistry.None));
            Assert.IsFalse(rebuilt.TryGetSpriteKey(99, out _));
        }

        [TestMethod]
        public void RewindFlushes_ReDeclaresEntriesFirstWrittenInDroppedChunks()
        {
            var registry = new SpriteKeyRegistry();
            registry.InternSprite(new SpriteKey("a", 0, 0, 1, 1));
            var w = new FrameWriter(new byte[256]);
            registry.WriteTableDelta(ref w);                  // flush 1: sprite 1
            registry.InternSprite(new SpriteKey("b", 0, 0, 1, 1));
            registry.InternString("s");
            w.Reset();
            registry.WriteTableDelta(ref w);                  // flush 2: sprite 2, string 1
            Assert.AreEqual(0, registry.PendingCount);

            registry.RewindFlushes(1);                        // chunk 2 was dropped by a truncation
            Assert.AreEqual(1, registry.FlushCount);
            Assert.AreEqual(2, registry.PendingCount, "sprite 2 and string 1 go out again with the next chunk");
            w.Reset();
            registry.WriteTableDelta(ref w);
            var r = new FrameReader(w.Buffer, 0, w.Length);
            Assert.AreEqual(1, r.ReadU16(), "one sprite re-declared");
            Assert.AreEqual("b", r.ReadString());

            registry.RewindFlushes(0);
            Assert.AreEqual(3, registry.PendingCount);
            registry.RewindFlushes(99);
            Assert.AreEqual(3, registry.PendingCount, "keeping more than exist changes nothing");
        }

        [TestMethod]
        public void DeflateRoundTrip_PayloadShrinks_PrefixParsesBack()
        {
            var world = FrameTestWorld.Small(3);
            var records = new List<FrameTestWorld.TickRecord>();
            var builder = new FrameChunkBuilder(ChunkTicks);
            for (int t = 0; t < ChunkTicks; t++)
            {
                var rec = world.Capture();
                records.Add(rec);
                FrameTestWorld.Feed(builder, rec);
            }
            var raw = builder.Finish((SpriteKeyRegistry)null);
            int rawLength = raw.PayloadRawLength;
            var chunk = FrameChunkCodec.Compress(raw);
            Assert.IsNull(raw.Buffer, "raw buffer released after compression");
            Assert.AreEqual(rawLength, chunk.PayloadRawLength);
            Assert.IsTrue(chunk.PayloadLength < rawLength / 2, "deflate must shrink a synthetic chunk at least 2x: " + chunk.PayloadLength + " of " + rawLength);
            Assert.AreEqual(FrameChunk.PrefixSize + chunk.TableDeltaLength + chunk.PayloadLength, chunk.Length);
            Assert.AreEqual(GameConfig.ReplayFrameFormatVersion, chunk.FormatVersion);

            Assert.IsTrue(FrameChunk.TryReadPrefix(chunk.Bytes, 0, chunk.Bytes.Length, out var prefix));
            Assert.AreEqual(chunk.FirstTick, prefix.FirstTick);
            Assert.AreEqual(chunk.TickCount, prefix.TickCount);
            Assert.AreEqual(chunk.Length, prefix.TotalLength);
            Assert.IsTrue(FrameChunk.TryParse(chunk.Bytes, 0, chunk.Bytes.Length, out var parsed));
            Assert.AreSame(chunk.Bytes, parsed.Bytes, "whole-array parse wraps without copying");
            Assert.IsFalse(FrameChunk.TryParse(chunk.Bytes, 0, chunk.Bytes.Length - 1, out _), "a short buffer is rejected");

            var frame = new DecodedFrame();
            var decoded = FrameChunkCodec.Decode(parsed);
            for (int k = 0; k < ChunkTicks; k++)
            {
                decoded.DecodeFrame(k, frame);
                FrameTestWorld.AssertFrameEquals(records[k], frame);
            }
        }

        [TestMethod]
        public void Events_AndTileKeyframe_RoundTrip()
        {
            var builder = new FrameChunkBuilder(ChunkTicks);
            builder.Begin(240);
            builder.AddTileEvent(240, 2, 5, 6, 0);          // event before the first frame of the chunk
            var gids = new uint[6 * 4];
            for (int i = 0; i < gids.Length; i++) gids[i] = (uint)(i + 1);
            builder.SetTileKeyframeLayer(0, 6, 4, gids);
            builder.SetTileKeyframeLayer(2, 6, 4, gids);
            gids[0] = 99;
            builder.SetTileKeyframeLayer(0, 6, 4, gids);       // replaces layer 0
            var segments = new[]
            {
                new ConsoleSegmentRecord(1, 0xFFFFFFFF, 0),
                new ConsoleSegmentRecord(2, 0xFF00FF00, 7),
            };
            for (int t = 0; t < 10; t++)
            {
                if (t == 3)
                    builder.AddTileEvent(243, 0, 1, 2, 13);
                if (t == 7)
                    builder.AddConsoleEvent(247, segments);
                builder.BeginTick(240 + t);
                builder.EndTick(default);
            }
            builder.AddConsoleEvent(249, ReadOnlySpan<ConsoleSegmentRecord>.Empty);
            Assert.AreEqual(2, builder.TileEventCount);
            Assert.AreEqual(2, builder.ConsoleEventCount);
            Assert.IsTrue(builder.HasTileKeyframe);
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => builder.AddTileEvent(240 + ChunkTicks, 0, 0, 0, 0));

            var decoded = FrameChunkCodec.Decode(FrameChunkCodec.Compress(builder.Finish((SpriteKeyRegistry)null)));
            Assert.AreEqual(240, decoded.FirstTick);
            Assert.AreEqual(10, decoded.TickCount);
            Assert.AreEqual(2, decoded.TileEvents.Count);
            Assert.AreEqual(240, decoded.TileEvents[0].Tick);
            Assert.AreEqual((byte)2, decoded.TileEvents[0].Layer);
            Assert.AreEqual(243, decoded.TileEvents[1].Tick);
            Assert.AreEqual(13, decoded.TileEvents[1].Gid);
            Assert.AreEqual((ushort)2, decoded.TileEvents[1].Y);
            Assert.AreEqual(2, decoded.ConsoleEvents.Count);
            Assert.AreEqual(247, decoded.ConsoleEvents[0].Tick);
            Assert.AreEqual((byte)2, decoded.ConsoleEvents[0].SegmentCount);
            var seg = decoded.ConsoleSegments[decoded.ConsoleEvents[0].SegmentStart + 1];
            Assert.AreEqual((ushort)2, seg.StringId);
            Assert.AreEqual(0xFF00FF00u, seg.Color);
            Assert.AreEqual((ushort)7, seg.ItemStringId);
            Assert.AreEqual((byte)0, decoded.ConsoleEvents[1].SegmentCount);
            Assert.IsTrue(decoded.HasTileKeyframe);
            Assert.AreEqual(2, decoded.TileKeyframeLayerCount);
            var l0 = decoded.GetTileKeyframeLayer(0);
            Assert.AreEqual((byte)0, l0.LayerIndex);
            Assert.AreEqual((ushort)6, l0.Width);
            Assert.AreEqual(99u, l0.Gids[0]);
            Assert.AreEqual(24u, l0.Gids[23]);
            Assert.AreEqual((byte)2, decoded.GetTileKeyframeLayer(1).LayerIndex);
            Assert.AreEqual(1u, decoded.GetTileKeyframeLayer(1).Gids[0]);

            // Builder is ready for the next chunk right after this one
            Assert.AreEqual(250, builder.NextTick);
            Assert.IsFalse(builder.HasTileKeyframe);
            Assert.AreEqual(0, builder.TileEventCount);
        }

        [TestMethod]
        public void Truncate_KeepsTicksEventsAndTableDeltaUpToTheCut()
        {
            var registry = new SpriteKeyRegistry();
            registry.InternSprite(new SpriteKey("tex", 1, 2, 3, 4));
            var world = FrameTestWorld.Small(5);
            var records = new List<FrameTestWorld.TickRecord>();
            var builder = new FrameChunkBuilder(ChunkTicks);
            for (int t = 0; t < ChunkTicks; t++)
            {
                if (t % 10 == 0)
                    builder.AddTileEvent(t, 1, (ushort)t, 0, t);
                var rec = world.Capture();
                records.Add(rec);
                FrameTestWorld.Feed(builder, rec);
            }
            var full = FrameChunkCodec.Compress(builder.Finish(registry));

            Assert.AreSame(full, FrameChunkCodec.Truncate(full, full.LastTick));
            Assert.AreSame(full, FrameChunkCodec.Truncate(full, 10_000));
            Assert.IsNull(FrameChunkCodec.Truncate(full, -1));

            var cut = FrameChunkCodec.Truncate(full, 45);
            Assert.AreEqual(0, cut.FirstTick);
            Assert.AreEqual(46, cut.TickCount);
            Assert.AreEqual(45, cut.LastTick);
            Assert.AreEqual(full.TableDeltaLength, cut.TableDeltaLength);
            Assert.IsTrue(new ReadOnlySpan<byte>(full.Bytes, full.TableDeltaOffset, full.TableDeltaLength)
                .SequenceEqual(new ReadOnlySpan<byte>(cut.Bytes, cut.TableDeltaOffset, cut.TableDeltaLength)), "table delta carried over verbatim");
            var decoded = FrameChunkCodec.Decode(cut);
            Assert.AreEqual(5, decoded.TileEvents.Count, "events at ticks 0,10,20,30,40 stay; 50+ dropped");
            var frame = new DecodedFrame();
            for (int k = 0; k <= 45; k++)
            {
                decoded.DecodeFrame(k, frame);
                FrameTestWorld.AssertFrameEquals(records[k], frame, "truncated");
            }
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => decoded.DecodeFrame(46, frame));
        }

        [TestMethod]
        public void LoadFrom_ResumesRecordingInTheMiddleOfAChunk()
        {
            var world = FrameTestWorld.Small(9);
            var records = new List<FrameTestWorld.TickRecord>();
            var builder = new FrameChunkBuilder(ChunkTicks);
            for (int t = 0; t < 30; t++)
            {
                var rec = world.Capture();
                records.Add(rec);
                FrameTestWorld.Feed(builder, rec);
            }
            var partial = FrameChunkCodec.Compress(builder.Finish((SpriteKeyRegistry)null));
            Assert.AreEqual(30, partial.TickCount);

            // A new builder (new scene) resumes from the partial chunk and records ticks 30..119
            var resumed = new FrameChunkBuilder(ChunkTicks);
            resumed.LoadFrom(FrameChunkCodec.Decode(partial), new DecodedFrame(), long.MaxValue);
            Assert.AreEqual(30, resumed.TickCount);
            Assert.AreEqual(30, resumed.NextTick);
            for (int t = 30; t < ChunkTicks; t++)
            {
                var rec = world.Capture();
                records.Add(rec);
                FrameTestWorld.Feed(resumed, rec);
            }
            Assert.IsTrue(resumed.IsFull);
            var whole = FrameChunkCodec.Compress(resumed.Finish((SpriteKeyRegistry)null));
            Assert.AreEqual(ChunkTicks, whole.TickCount);
            var decoded = FrameChunkCodec.Decode(whole);
            var frame = new DecodedFrame();
            for (int k = 0; k < ChunkTicks; k++)
            {
                decoded.DecodeFrame(k, frame);
                FrameTestWorld.AssertFrameEquals(records[k], frame, "resumed");
            }
        }

        [TestMethod]
        public void Builder_RejectsMisuse()
        {
            var builder = new FrameChunkBuilder(4);
            Assert.IsNull(builder.Finish((SpriteKeyRegistry)null), "nothing captured");
            Assert.ThrowsException<InvalidOperationException>(() => builder.BeginEntity(), "outside a tick");
            builder.BeginTick(0);
            Assert.ThrowsException<InvalidOperationException>(() => builder.BeginTick(1));
            builder.AddEntity(5, new byte[] { 1, 2 });
            Assert.ThrowsException<ArgumentException>(() => builder.AddEntity(5, new byte[] { 1, 2 }), "duplicate id in one tick");
            // Direct emission into the arena: an empty write records nothing, a real one records the entity
            var direct = builder.BeginEntity();
            builder.EndEntity(6, ref direct);
            direct = builder.BeginEntity();
            direct.WriteSprite(1, 2, 3, 0f, 0, 0, 0);
            builder.EndEntity(6, ref direct);
            Assert.ThrowsException<ArgumentException>(() =>
            {
                var again = builder.BeginEntity();
                again.WriteU8(1);
                builder.EndEntity(6, ref again);
            }, "duplicate id through the direct path");
            Assert.ThrowsException<InvalidOperationException>(() => builder.Finish((SpriteKeyRegistry)null), "inside a tick");
            builder.EndTick(default);
            Assert.ThrowsException<ArgumentException>(() => builder.BeginTick(5), "ticks must be consecutive");
            Assert.ThrowsException<InvalidOperationException>(() => builder.EndTick(default));
            for (int t = 1; t < 4; t++)
            {
                builder.BeginTick(t);
                builder.EndTick(default);
            }
            Assert.IsTrue(builder.IsFull);
            Assert.ThrowsException<InvalidOperationException>(() => builder.BeginTick(4), "full chunk must be finished first");
            Assert.ThrowsException<InvalidOperationException>(() => builder.Begin(0), "Begin on a chunk in progress");
            var chunk = FrameChunkCodec.Compress(builder.Finish((SpriteKeyRegistry)null));
            Assert.AreEqual(4, chunk.TickCount);
            Assert.AreEqual(4, builder.NextTick);
            var decoded = FrameChunkCodec.Decode(chunk);
            var frame = new DecodedFrame();
            decoded.DecodeFrame(0, frame);
            Assert.AreEqual(2, frame.LiveCount, "entities 5 (copied) and 6 (direct) in the base frame");
            Assert.IsTrue(frame.TryGetEntity(6, out var e6) && e6.Length == 1 + FrameOpCode.SpritePayload);
            decoded.DecodeFrame(3, frame);
            Assert.AreEqual(0, frame.LiveCount, "both tombstoned at tick 1");
            builder.Reset();
            Assert.AreEqual(-1, builder.NextTick);
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new FrameChunkBuilder(0));
        }

        [TestMethod]
        public void DecodedFrame_CompactsDeadSlots_KeepsLiveEntitiesReadable()
        {
            // Many spawns and despawns inside one chunk force slot and ops compaction in the decoded frame
            var builder = new FrameChunkBuilder(ChunkTicks);
            var ops = new byte[64];
            var w = new FrameWriter(ops);
            w.WriteSprite(1, 0, 0, 0f, 0, 0, 0);
            int len = w.Length;
            var expected = new List<HashSet<ushort>>();
            for (int t = 0; t < ChunkTicks; t++)
            {
                builder.BeginTick(t);
                var ids = new HashSet<ushort>();
                // Entities 1..400 each live for exactly one tick, in turn; entity 0 lives always
                builder.AddEntity(0, ops, 0, len);
                ids.Add(0);
                for (int n = 0; n < 5; n++)
                {
                    ushort id = (ushort)(1 + (t * 5 + n) % 400);
                    builder.AddEntity(id, ops, 0, len);
                    ids.Add(id);
                }
                builder.EndTick(default);
                expected.Add(ids);
            }
            var decoded = FrameChunkCodec.Decode(FrameChunkCodec.Compress(builder.Finish((SpriteKeyRegistry)null)));
            var frame = new DecodedFrame();
            for (int k = 0; k < ChunkTicks; k++)
            {
                decoded.DecodeFrame(k, frame);
                Assert.AreEqual(6, frame.LiveCount, "tick " + k);
                int seen = 0;
                for (int s = 0; s < frame.SlotCount; s++)
                {
                    if (!frame.TryGetSlot(s, out var e))
                        continue;
                    seen++;
                    Assert.IsTrue(expected[k].Contains(e.Id), "unexpected entity " + e.Id + " at tick " + k);
                    var r = frame.ReadOps(e);
                    Assert.AreEqual(FrameOpCode.Sprite, r.ReadOpCode());
                }
                Assert.AreEqual(6, seen);
            }
            Assert.IsTrue(frame.SlotCount < 400, "dead slots were compacted: " + frame.SlotCount);
        }
    }
}
