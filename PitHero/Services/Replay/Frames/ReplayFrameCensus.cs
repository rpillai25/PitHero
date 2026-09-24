using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Xna.Framework;
using Nez;
using Nez.Particles;
using Nez.Sprites;
using Nez.Textures;
using Nez.Tiled;
using PitHero.AI;
using PitHero.ECS.Components;
using PitHero.UI;
using PitHero.Util;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace PitHero.Services.Replay.Frames
{
    /// <summary>
    /// Measurement spike for issue #425 (behind <see cref="GameConfig.ReplayFrameCensus"/>): walks the
    /// scene's renderables once per simulation tick and reports, once per
    /// <see cref="GameConfig.ReplayFrameCensusWindowTicks"/>, what a Braid-style frame stream (design doc
    /// features/feature_replay_frame_recording_424.md §3.1) would have to store. Five encoding variants
    /// are written as real byte streams and deflated per chunk, so their compressed sizes are measured
    /// rather than estimated. Output goes to frame_census.log next to the replay files, so Release
    /// builds report too.
    /// Strictly read-only over the scene: never rolls Nez.Random, never reads Input, never mutates a
    /// component. The Stopwatch only times the census itself and feeds nothing back into the sim.
    /// </summary>
    public sealed class ReplayFrameCensus
    {
        /// <summary>The census for the running scene; null when the const is off or outside a game session.</summary>
        public static ReplayFrameCensus Current { get; private set; }

        // Concrete renderable kinds, classified by a hand-written type-check list (no reflection)
        private const byte KSpriteRenderer = 0, KYSortSprite = 1, KPrototypeSprite = 2, KSpriteAnimator = 3,
            KPausableAnimator = 4, KEnemyAnimation = 5, KHeroLayer = 6, KMultiSprite = 7, KStaticCompositor = 8,
            KTextRender = 9, KRisingText = 10, KBouncyText = 11, KBouncyDigit = 12, KSpeechBubble = 13,
            KMonsterHpBar = 14, KBuildingOutline = 15, KSelectBox = 16, KActionQueueViz = 17, KCloudOverlay = 18,
            KTreeBand = 19, KGraphicalHud = 20, KTiledMap = 21, KParticleEmitter = 22, KUiCanvas = 23, KOther = 24;
        private const int KindCount = 25;
        private static readonly string[] KindNames =
        {
            "SpriteRenderer", "YSortSpriteRenderer", "PrototypeSpriteRenderer", "SpriteAnimator",
            "PausableSpriteAnimator", "EnemyAnimationComponent", "HeroAnimationComponent(layer)", "MultiSpriteAnimator",
            "StaticSpriteCompositor", "TextRenderComponent", "RisingTextComponent", "BouncyTextComponent",
            "BouncyDigitComponent", "SpeechBubbleComponent", "MonsterHPBarComponent", "BuildingOutlineRenderComponent",
            "SelectBoxRenderComponent", "ActionQueueVisualizationComponent", "CloudOverlayComponent", "TreeBandComponent",
            "GraphicalHUD", "TiledMapRenderer", "ParticleEmitter", "UICanvas", "other",
        };

        // Op classes and the issue #425 op sizes (used for the "naive" estimate only; the streams use the §3.1 layout)
        private const int OpNone = 0, OpSprite = 1, OpComposite = 2, OpText = 3, OpRect = 4, OpHpBar = 5, OpSpeech = 6;
        private const int SpriteOpBytes = 20, CompositeOpBytes = 8, CompositeLayerBytes = 13, TextOpBytes = 16,
            RectOpBytes = 21, NinePatchOpBytes = 22, EntityHeaderBytes = 4, TombstoneBytes = 4;
        private const int HudRecordBytes = 40, TileEventBytes = 11, ConsoleLineBytes = 4, ConsoleSegmentBytes = 8;

        // Encoding variants written as real byte streams
        private const int SA = 0, SB = 1, SC = 2, SD = 3, SE = 4, StreamCount = 5;
        private static readonly string[] StreamNames =
        {
            "A design: delta vs chunk base, f32 positions",
            "B delta vs previous tick, f32 positions",
            "C delta vs previous tick, i16 pixel positions",
            "D = C + composite 'moved' op (position/depth only)",
            "E = C sampled every 2nd tick (30 Hz)",
        };

        // Tick buckets
        private const int BOutOfPit = 0, BInPit = 1, BBattle = 2, BPaused = 3, BucketCount = 4;
        private static readonly string[] BucketNames = { "hero out of pit", "hero in pit", "battle", "paused" };

        private const int ChunkTicks = 120;          // Braid GOP the estimate models (design default)
        private const int RingCapacity = 1024;       // Per-tick samples awaiting the presentation pass
        private const string LogFileName = "frame_census.log";

        private struct Snap
        {
            public ushort Id;
            public byte Kind;
            public bool Enabled, Captured;
            public float X, Y, Depth;
            public int Layer;
            public uint Color;
            public byte Fx;
            public Sprite Sprite;
            public int Content;
        }

        private struct TickSample
        {
            public byte Bucket;
            public bool BaseFrame, Sampled30;
            public int Renderables, Captured, ChangedAll, ChangedCaptured, ChangedCapturedQ, ChangedVsBase, Bytes, TileOps, ConsoleLines;
            public int Bytes0, Bytes1, Bytes2, Bytes3, Bytes4;
            public int Emit0, Emit1, Emit2, Emit3, Emit4;
            public long CostTicks;
        }

        private sealed class StreamBuf
        {
            public byte[] Buf = new byte[256 * 1024];
            public byte[] Pending = new byte[256 * 1024];
            public int Len, PendingLen, TickBytes, TickEmitted;

            public void Ensure(int extra)
            {
                if (Len + extra <= Buf.Length)
                    return;
                // Warm-up growth only
                var grown = new byte[Buf.Length * 2];
                System.Buffer.BlockCopy(Buf, 0, grown, 0, Len);
                Buf = grown;
            }
            public void U8(byte v) { Ensure(1); Buf[Len++] = v; TickBytes++; }
            public void U16(ushort v) { Ensure(2); Buf[Len++] = (byte)v; Buf[Len++] = (byte)(v >> 8); TickBytes += 2; }
            public void I16(int v) => U16((ushort)(short)v);
            public void U32(uint v)
            {
                Ensure(4);
                Buf[Len++] = (byte)v; Buf[Len++] = (byte)(v >> 8);
                Buf[Len++] = (byte)(v >> 16); Buf[Len++] = (byte)(v >> 24);
                TickBytes += 4;
            }
            public void F32(float v) => U32((uint)System.BitConverter.SingleToInt32Bits(v));

            public void EndChunk()
            {
                if (PendingLen == 0)
                {
                    var tmp = Pending; Pending = Buf; Buf = tmp;
                    PendingLen = Len;
                }
                // else: the presentation pass has not caught up; this chunk is not measured for compression
                Len = 0;
            }
        }

        private sealed class Stat
        {
            public long N, Min = long.MaxValue, Max = long.MinValue;
            public double Sum;
            public void Add(long v) { N++; Sum += v; if (v < Min) Min = v; if (v > Max) Max = v; }
            public void Reset() { N = 0; Sum = 0; Min = long.MaxValue; Max = long.MinValue; }
            public void Merge(Stat o) { if (o.N == 0) return; N += o.N; Sum += o.Sum; if (o.Min < Min) Min = o.Min; if (o.Max > Max) Max = o.Max; }
            public string Format(double scale = 1.0)
                => N == 0 ? "n/a" : (Min * scale).ToString("0.#") + " / " + (Sum / N * scale).ToString("0.#") + " / " + (Max * scale).ToString("0.#");
        }

        private sealed class Window
        {
            public long Ticks, RawBytes, BaseFrames;
            public long TileOps, TileNoOps, ConsoleLines, ConsoleSegments, ConsoleChars, DroppedSamples;
            public readonly long[] TileOpsByLayer = new long[LayerNames.Length];
            public readonly long[] BucketTicks = new long[BucketCount];
            public readonly Stat[] ChangedAll = NewStats(), ChangedCaptured = NewStats(), ChangedCapturedQ = NewStats(), ChangedVsBase = NewStats(), Bytes = NewStats();
            public readonly Stat Captured = new Stat(), Renderables = new Stat(), Cost = new Stat(), DeflateMicros = new Stat();
            public readonly long[] StreamRaw = new long[StreamCount], StreamDeflated = new long[StreamCount], StreamChunks = new long[StreamCount],
                StreamEmitted = new long[StreamCount], StreamDeltaTicks = new long[StreamCount];

            private static Stat[] NewStats() { var s = new Stat[BucketCount]; for (int i = 0; i < BucketCount; i++) s[i] = new Stat(); return s; }

            public void Reset()
            {
                Ticks = RawBytes = BaseFrames = 0;
                TileOps = TileNoOps = ConsoleLines = ConsoleSegments = ConsoleChars = DroppedSamples = 0;
                for (int i = 0; i < TileOpsByLayer.Length; i++) TileOpsByLayer[i] = 0;
                for (int i = 0; i < BucketCount; i++)
                {
                    BucketTicks[i] = 0; ChangedAll[i].Reset(); ChangedCaptured[i].Reset(); ChangedCapturedQ[i].Reset(); ChangedVsBase[i].Reset(); Bytes[i].Reset();
                }
                for (int i = 0; i < StreamCount; i++)
                    StreamRaw[i] = StreamDeflated[i] = StreamChunks[i] = StreamEmitted[i] = StreamDeltaTicks[i] = 0;
                Captured.Reset(); Renderables.Reset(); Cost.Reset(); DeflateMicros.Reset();
            }

            public void Merge(Window o)
            {
                Ticks += o.Ticks; RawBytes += o.RawBytes; BaseFrames += o.BaseFrames;
                TileOps += o.TileOps; TileNoOps += o.TileNoOps; ConsoleLines += o.ConsoleLines; ConsoleSegments += o.ConsoleSegments;
                ConsoleChars += o.ConsoleChars; DroppedSamples += o.DroppedSamples;
                for (int i = 0; i < TileOpsByLayer.Length; i++) TileOpsByLayer[i] += o.TileOpsByLayer[i];
                for (int i = 0; i < BucketCount; i++)
                {
                    BucketTicks[i] += o.BucketTicks[i]; ChangedAll[i].Merge(o.ChangedAll[i]); ChangedCaptured[i].Merge(o.ChangedCaptured[i]);
                    ChangedCapturedQ[i].Merge(o.ChangedCapturedQ[i]); ChangedVsBase[i].Merge(o.ChangedVsBase[i]); Bytes[i].Merge(o.Bytes[i]);
                }
                for (int i = 0; i < StreamCount; i++)
                {
                    StreamRaw[i] += o.StreamRaw[i]; StreamDeflated[i] += o.StreamDeflated[i]; StreamChunks[i] += o.StreamChunks[i];
                    StreamEmitted[i] += o.StreamEmitted[i]; StreamDeltaTicks[i] += o.StreamDeltaTicks[i];
                }
                Captured.Merge(o.Captured); Renderables.Merge(o.Renderables); Cost.Merge(o.Cost); DeflateMicros.Merge(o.DeflateMicros);
            }
        }

        private readonly struct SpriteKey : System.IEquatable<SpriteKey>
        {
            public readonly string Texture;
            public readonly Rectangle Rect;
            public SpriteKey(string texture, Rectangle rect) { Texture = texture; Rect = rect; }
            public bool Equals(SpriteKey o) => Rect == o.Rect && string.Equals(Texture, o.Texture);
            public override bool Equals(object obj) => obj is SpriteKey k && Equals(k);
            public override int GetHashCode() => (Texture?.GetHashCode() ?? 0) * 31 + Rect.GetHashCode();
        }

        private static readonly string[] LayerNames = { "Base", "Detail", "FogOfWar", "Collision", "Top", "(other)" };

        // Per-tick state (sim side; read-only over the scene). Three snapshot dictionaries rotate each tick:
        // cur (this tick), prev (last tick), prev2 (two ticks ago, for the 30 Hz variant).
        private Dictionary<RenderableComponent, Snap> _prev2 = new Dictionary<RenderableComponent, Snap>(1024);
        private Dictionary<RenderableComponent, Snap> _prev = new Dictionary<RenderableComponent, Snap>(1024);
        private Dictionary<RenderableComponent, Snap> _cur = new Dictionary<RenderableComponent, Snap>(1024);
        private int _prev2Captured, _prevCaptured, _curCaptured;
        private readonly Dictionary<RenderableComponent, Snap> _base = new Dictionary<RenderableComponent, Snap>(1024);
        private readonly Dictionary<Sprite, ushort> _spriteIds = new Dictionary<Sprite, ushort>(2048);
        private ushort _nextEntityId = 1;
        private int _chunkTick;
        private int _tileOpsThisTick, _consoleLinesThisTick, _consoleBytesThisTick;
        private Entity _heroEntity;
        private HeroComponent _heroComponent;

        private readonly StreamBuf[] _streams = new StreamBuf[StreamCount];
        private readonly MemoryStream _deflateOut = new MemoryStream(256 * 1024);

        // Sim → presentation handoff
        private readonly TickSample[] _ring = new TickSample[RingCapacity];
        private int _ringHead, _ringCount;
        private long _droppedSamples;

        // Sprite-key census (first sight of each Sprite reference)
        private readonly HashSet<SpriteKey> _spriteKeys = new HashSet<SpriteKey>();
        private readonly HashSet<string> _textureNames = new HashSet<string>();
        private readonly HashSet<object> _unnamedTextures = new HashSet<object>();
        private readonly long[] _unnamedSpritesByKind = new long[KindCount];
        private readonly long[] _spritesByKind = new long[KindCount];

        // Presentation-side aggregation
        private readonly Window _window = new Window();
        private readonly Window _session = new Window();
        private readonly int[] _kindCounts = new int[KindCount];
        private readonly int[] _kindCountsMax = new int[KindCount];
        private readonly string _logPath;
        private readonly System.DateTime _startedUtc;
        private int _windowIndex;
        private long _tileKeyframeRaw, _tileKeyframeDeflated;
        private byte[] _tileBuf;

        private ReplayFrameCensus(string logPath)
        {
            _logPath = logPath;
            _startedUtc = System.DateTime.UtcNow;
            for (int i = 0; i < StreamCount; i++)
                _streams[i] = new StreamBuf();
        }

        /// <summary>Creates the census for a new scene (called from MainGameScene.Begin when the const is on).</summary>
        public static void Start()
        {
            Detach();
            var files = Core.Services.GetService<ReplayFileService>();
            string dir = files != null ? files.Directory_ : Path.GetTempPath();
            Current = new ReplayFrameCensus(Path.Combine(dir, LogFileName));
#if DEBUG
            const string build = "Debug";
#else
            const string build = "Release";
#endif
            Current.Append("=== frame census started " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " build=" + build
                + " window=" + GameConfig.ReplayFrameCensusWindowTicks + " ticks, chunk model=" + ChunkTicks + " ticks ===\n");
        }

        /// <summary>Flushes a final partial report and detaches (called from MainGameScene.Unload).</summary>
        public static void Detach()
        {
            if (Current == null)
                return;
            Current.Flush(final: true);
            Current = null;
        }

        private static bool LiveOnly
        {
            get
            {
                var playback = ReplayPlaybackService.Current;
                return playback == null || !playback.IsActive;
            }
        }

        /// <summary>TiledMapService hook: one runtime tile mutation (gid 0 = removal).</summary>
        public void OnTileMutation(string layerName, uint oldRawGid, int newGid)
        {
            if (!LiveOnly)
                return;
            _tileOpsThisTick++;
            int li = LayerIndex(layerName);
            _window.TileOpsByLayer[li]++;
            if ((int)(oldRawGid & 0x1FFFFFFFu) == newGid)
                _window.TileNoOps++;
        }

        /// <summary>GameEventService hook: one console line with its segment count and text length.</summary>
        public void OnConsoleLine(int segments, int chars)
        {
            if (!LiveOnly)
                return;
            _consoleLinesThisTick++;
            _consoleBytesThisTick += ConsoleLineBytes + segments * ConsoleSegmentBytes;
            _window.ConsoleSegments += segments;
            _window.ConsoleChars += chars;
        }

        /// <summary>
        /// Per-tick sample at the tail of MainGameScene.Update, before the clock advances. Read-only walk of
        /// Scene.RenderableComponents; no allocation after warm-up (dictionaries and buffers are pre-sized).
        /// </summary>
        public void SampleTick(Scene scene, long tick)
        {
            if (!LiveOnly || scene == null)
            {
                _tileOpsThisTick = _consoleLinesThisTick = _consoleBytesThisTick = 0;
                return;
            }

            long t0 = Stopwatch.GetTimestamp();

            bool baseFrame = _chunkTick == 0;
            bool sampled30 = (_chunkTick & 1) == 0;
            var rot = _prev2; _prev2 = _prev; _prev = _cur; _cur = rot;
            _prev2Captured = _prevCaptured; _prevCaptured = _curCaptured; _curCaptured = 0;
            _cur.Clear();
            if (baseFrame)
                _base.Clear();
            for (int i = 0; i < StreamCount; i++)
            {
                _streams[i].TickBytes = 0;
                _streams[i].TickEmitted = 0;
            }

            var list = scene.RenderableComponents;
            int n = list.Count;
            int captured = 0, changedAll = 0, changedCaptured = 0, changedCapturedQ = 0, changedVsBase = 0;
            int matchedPrev = 0, matchedPrevCaptured = 0, matchedPrev2Captured = 0, matchedBase = 0, bytes = 0;

            for (int i = 0; i < n; i++)
            {
                var rc = list[i] as RenderableComponent;
                if (rc == null || rc.Entity == null)
                    continue;

                bool had = _prev.TryGetValue(rc, out var p);
                byte kind = had ? p.Kind : Classify(rc);
                var s = Build(rc, kind);
                s.Id = had ? p.Id : _nextEntityId++;
                if (_nextEntityId == 0) _nextEntityId = 1;

                bool differsFromPrev = !had || !Same(ref p, ref s);
                if (had) matchedPrev++;
                if (differsFromPrev) changedAll++;

                int opClass = s.Enabled ? OpClassOf(rc, kind) : OpNone;
                s.Captured = opClass != OpNone;
                _cur[rc] = s;
                if (!s.Captured)
                    continue;

                captured++;
                _curCaptured++;
                bool hadCaptured = had && p.Captured;
                if (hadCaptured) matchedPrevCaptured++;
                bool diffPrev = !hadCaptured || differsFromPrev;
                bool diffPrevQ = !hadCaptured || !SameQ(ref p, ref s);
                if (diffPrev) changedCaptured++;
                if (diffPrevQ) changedCapturedQ++;
                if (!had) NoteSprites(rc, kind);

                // A: the design as written, entity deltas vs the chunk base
                bool emitA;
                if (baseFrame)
                {
                    _base[rc] = s;
                    emitA = true;
                }
                else if (_base.TryGetValue(rc, out var b))
                {
                    matchedBase++;
                    emitA = !Same(ref b, ref s);
                }
                else
                {
                    emitA = true;
                }
                if (emitA)
                {
                    if (!baseFrame) changedVsBase++;
                    bytes += EntityHeaderBytes + OpBytesOf(rc, opClass);
                    WriteOps(_streams[SA], rc, ref s, opClass, quantized: false);
                }

                // B: deltas vs the previous tick
                if (baseFrame || diffPrev)
                    WriteOps(_streams[SB], rc, ref s, opClass, quantized: false);

                // C: B with integer-pixel positions
                if (baseFrame || diffPrevQ)
                    WriteOps(_streams[SC], rc, ref s, opClass, quantized: true);

                // D: C plus a composite "moved" op when only position/depth changed
                if (baseFrame || diffPrevQ)
                {
                    if (!baseFrame && hadCaptured && opClass == OpComposite && MovedOnly(ref p, ref s))
                        WriteMoved(_streams[SD], ref s);
                    else
                        WriteOps(_streams[SD], rc, ref s, opClass, quantized: true);
                }

                // E: C sampled every second tick (deltas vs the snapshot two ticks ago)
                if (sampled30)
                {
                    bool had2 = _prev2.TryGetValue(rc, out var p2) && p2.Captured;
                    if (had2) matchedPrev2Captured++;
                    if (baseFrame || !had2 || !SameQ(ref p2, ref s))
                        WriteOps(_streams[SE], rc, ref s, opClass, quantized: true);
                }
            }

            // Renderables gone since the previous tick count as changes; captured ones gone are tombstones
            changedAll += _prev.Count - matchedPrev;
            if (!baseFrame)
            {
                int tombstonesA = _base.Count - matchedBase;
                changedVsBase += tombstonesA;
                bytes += tombstonesA * TombstoneBytes;
                WriteTombstones(_streams[SA], tombstonesA);
                int tombstonesPrev = _prevCaptured - matchedPrevCaptured;
                changedCaptured += tombstonesPrev;
                changedCapturedQ += tombstonesPrev;
                WriteTombstones(_streams[SB], tombstonesPrev);
                WriteTombstones(_streams[SC], tombstonesPrev);
                WriteTombstones(_streams[SD], tombstonesPrev);
                if (sampled30)
                    WriteTombstones(_streams[SE], _prev2Captured - matchedPrev2Captured);
            }

            // HUD record: full in the base, a flag byte per delta plus a full record once per sim second (clock)
            bytes += baseFrame ? HudRecordBytes : 1 + (tick % 60 == 0 ? HudRecordBytes : 0);
            bytes += _tileOpsThisTick * TileEventBytes + _consoleBytesThisTick;

            long cost = Stopwatch.GetTimestamp() - t0;

            if (_ringCount < RingCapacity)
            {
                int slot = (_ringHead + _ringCount) % RingCapacity;
                _ring[slot] = new TickSample
                {
                    Bucket = (byte)ClassifyTick(scene),
                    BaseFrame = baseFrame,
                    Sampled30 = sampled30,
                    Renderables = n,
                    Captured = captured,
                    ChangedAll = changedAll,
                    ChangedCaptured = changedCaptured,
                    ChangedCapturedQ = changedCapturedQ,
                    ChangedVsBase = changedVsBase,
                    Bytes = bytes,
                    TileOps = _tileOpsThisTick,
                    ConsoleLines = _consoleLinesThisTick,
                    Bytes0 = _streams[0].TickBytes, Bytes1 = _streams[1].TickBytes, Bytes2 = _streams[2].TickBytes,
                    Bytes3 = _streams[3].TickBytes, Bytes4 = _streams[4].TickBytes,
                    Emit0 = _streams[0].TickEmitted, Emit1 = _streams[1].TickEmitted, Emit2 = _streams[2].TickEmitted,
                    Emit3 = _streams[3].TickEmitted, Emit4 = _streams[4].TickEmitted,
                    CostTicks = cost,
                };
                _ringCount++;
            }
            else
            {
                _droppedSamples++;
            }
            _tileOpsThisTick = _consoleLinesThisTick = _consoleBytesThisTick = 0;

            if (++_chunkTick >= ChunkTicks)
            {
                _chunkTick = 0;
                for (int i = 0; i < StreamCount; i++)
                    _streams[i].EndChunk();
            }
        }

        /// <summary>Presentation-side aggregation: drains per-tick samples, deflates finished chunks, logs each window.</summary>
        public void PresentationUpdate(Scene scene)
        {
            while (_ringCount > 0)
            {
                ref var s = ref _ring[_ringHead];
                _ringHead = (_ringHead + 1) % RingCapacity;
                _ringCount--;

                var w = _window;
                w.Ticks++;
                w.BucketTicks[s.Bucket]++;
                w.ChangedAll[s.Bucket].Add(s.ChangedAll);
                w.ChangedCaptured[s.Bucket].Add(s.ChangedCaptured);
                w.ChangedCapturedQ[s.Bucket].Add(s.ChangedCapturedQ);
                if (!s.BaseFrame)
                {
                    w.ChangedVsBase[s.Bucket].Add(s.ChangedVsBase);
                    w.Bytes[s.Bucket].Add(s.Bytes);
                }
                else
                {
                    w.BaseFrames++;
                }
                w.RawBytes += s.Bytes;
                w.StreamRaw[0] += s.Bytes0; w.StreamRaw[1] += s.Bytes1; w.StreamRaw[2] += s.Bytes2; w.StreamRaw[3] += s.Bytes3; w.StreamRaw[4] += s.Bytes4;
                if (!s.BaseFrame)
                {
                    w.StreamEmitted[0] += s.Emit0; w.StreamEmitted[1] += s.Emit1; w.StreamEmitted[2] += s.Emit2; w.StreamEmitted[3] += s.Emit3;
                    for (int i = 0; i < SE; i++) w.StreamDeltaTicks[i]++;
                    if (s.Sampled30) { w.StreamEmitted[4] += s.Emit4; w.StreamDeltaTicks[4]++; }
                }
                w.Captured.Add(s.Captured);
                w.Renderables.Add(s.Renderables);
                w.Cost.Add(s.CostTicks);
                w.TileOps += s.TileOps;
                w.ConsoleLines += s.ConsoleLines;

                if (w.Ticks >= GameConfig.ReplayFrameCensusWindowTicks)
                {
                    CountKinds(scene);
                    Flush(final: false);
                }
            }

            for (int i = 0; i < StreamCount; i++)
            {
                var sb = _streams[i];
                if (sb.PendingLen == 0)
                    continue;
                long t0 = Stopwatch.GetTimestamp();
                long deflated = Deflate(sb.Pending, sb.PendingLen, CompressionLevel.Optimal);
                long micros = (Stopwatch.GetTimestamp() - t0) * 1000000L / Stopwatch.Frequency;
                _window.StreamDeflated[i] += deflated;
                _window.StreamChunks[i]++;
                if (i == SA)
                    _window.DeflateMicros.Add(micros);
                sb.PendingLen = 0;
            }
        }

        // ───────────────────────────── classification ─────────────────────────────

        private static byte Classify(RenderableComponent rc)
        {
            // Most-derived first
            if (rc is HeroAnimationComponent) return KHeroLayer;
            if (rc is EnemyAnimationComponent) return KEnemyAnimation;
            if (rc is PausableSpriteAnimator) return KPausableAnimator;
            if (rc is SpriteAnimator) return KSpriteAnimator;
            if (rc is YSortSpriteRenderer) return KYSortSprite;
            if (rc is PrototypeSpriteRenderer) return KPrototypeSprite;
            if (rc is SpriteRenderer) return KSpriteRenderer;
            if (rc is MultiSpriteAnimator) return KMultiSprite;
            if (rc is StaticSpriteCompositor) return KStaticCompositor;
            if (rc is TextRenderComponent) return KTextRender;
            if (rc is RisingTextComponent) return KRisingText;
            if (rc is BouncyTextComponent) return KBouncyText;
            if (rc is BouncyDigitComponent) return KBouncyDigit;
            if (rc is SpeechBubbleComponent) return KSpeechBubble;
            if (rc is MonsterHPBarComponent) return KMonsterHpBar;
            if (rc is BuildingOutlineRenderComponent) return KBuildingOutline;
            if (rc is SelectBoxRenderComponent) return KSelectBox;
            if (rc is ActionQueueVisualizationComponent) return KActionQueueViz;
            if (rc is CloudOverlayComponent) return KCloudOverlay;
            if (rc is TreeBandComponent) return KTreeBand;
            if (rc is GraphicalHUD) return KGraphicalHud;
            if (rc is TiledMapRenderer) return KTiledMap;
            if (rc is ParticleEmitter) return KParticleEmitter;
            if (rc is UICanvas) return KUiCanvas;
            return KOther;
        }

        private static int OpClassOf(RenderableComponent rc, byte kind)
        {
            switch (kind)
            {
                case KHeroLayer:
                    // Owned layers are captured inside their MultiSpriteAnimator's Composite op
                    if (((ICompositeLayer)rc).OwnedByComposite) return OpNone;
                    return ((SpriteRenderer)rc).Sprite != null ? OpSprite : OpNone;
                case KSpriteRenderer:
                case KYSortSprite:
                case KPrototypeSprite:
                case KSpriteAnimator:
                case KPausableAnimator:
                case KEnemyAnimation:
                    return ((SpriteRenderer)rc).Sprite != null ? OpSprite : OpNone;
                case KMultiSprite:
                case KStaticCompositor:
                    return OpComposite;
                case KTextRender:
                case KRisingText:
                case KBouncyText:
                case KBouncyDigit:
                    return OpText;
                case KSpeechBubble:
                    return ((SpeechBubbleComponent)rc).IsShowing ? OpSpeech : OpNone;
                case KMonsterHpBar:
                    return OpHpBar;
                case KBuildingOutline:
                case KSelectBox:
                    return OpRect;
                default:
                    // Live-only (clouds, tree band, HUD, action queue, UI canvas), tile maps (tile events), particles (v1 skip), unknown
                    return OpNone;
            }
        }

        private static int OpBytesOf(RenderableComponent rc, int opClass)
        {
            switch (opClass)
            {
                case OpSprite: return SpriteOpBytes;
                case OpComposite: return CompositeOpBytes + CompositeLayerBytes * CompositeLayerCount(rc);
                case OpText: return TextOpBytes;
                case OpRect: return RectOpBytes;
                case OpHpBar: return TextOpBytes + 2 * RectOpBytes;
                case OpSpeech: return NinePatchOpBytes + TextOpBytes;
                default: return 0;
            }
        }

        private static int CompositeLayerCount(RenderableComponent rc)
        {
            if (rc is MultiSpriteAnimator m) return m.LayerCount;
            if (rc is StaticSpriteCompositor c) return c.LayerCount;
            return 0;
        }

        private int ClassifyTick(Scene scene)
        {
            var pause = Core.Services.GetService<PauseService>();
            if (pause != null && pause.IsPaused)
                return BPaused;
            if (HeroStateMachine.IsBattleInProgress)
                return BBattle;
            if (_heroEntity == null || _heroEntity.IsDestroyed || _heroEntity.Scene != scene)
            {
                _heroEntity = scene.FindEntity("hero");
                _heroComponent = _heroEntity?.GetComponent<HeroComponent>();
            }
            return _heroComponent != null && _heroComponent.InsidePit ? BInPit : BOutOfPit;
        }

        private static int LayerIndex(string layerName)
        {
            for (int i = 0; i < LayerNames.Length - 1; i++)
                if (layerName == LayerNames[i])
                    return i;
            return LayerNames.Length - 1;
        }

        // ───────────────────────────── snapshot ─────────────────────────────

        private static Snap Build(RenderableComponent rc, byte kind)
        {
            var s = new Snap { Kind = kind };
            s.Enabled = rc.Enabled;
            var pos = rc.Entity.Transform.Position + rc.LocalOffset;
            s.X = pos.X;
            s.Y = pos.Y;
            s.Depth = rc.LayerDepth;
            s.Layer = rc.RenderLayer;
            s.Color = rc.Color.PackedValue;

            if (rc is SpriteRenderer sr)
            {
                s.Sprite = sr.Sprite;
                s.Fx = (byte)sr.SpriteEffects;
            }
            else if (rc is MultiSpriteAnimator m)
            {
                int h = 17;
                for (int j = 0; j < m.LayerCount; j++)
                {
                    var layer = m.GetLayer(j);
                    if (layer == null) continue;
                    h = h * 31 + RuntimeHelpers.GetHashCode(layer.Sprite);
                    h = h * 31 + (int)layer.LayerColor.PackedValue;
                    h = h * 31 + (layer.FlipX ? 1 : 0);
                    h = h * 31 + layer.LocalOffset.GetHashCode();
                }
                s.Content = h;
            }
            else if (rc is StaticSpriteCompositor c)
            {
                int h = 19;
                for (int j = 0; j < c.LayerCount; j++)
                {
                    var layer = c.GetLayer(j);
                    if (layer == null) continue;
                    h = h * 31 + RuntimeHelpers.GetHashCode(layer.Sprite);
                    h = h * 31 + (int)layer.Color.PackedValue;
                    h = h * 31 + (int)layer.SpriteEffects;
                }
                s.Content = h;
            }
            else if (kind == KSpeechBubble)
            {
                s.Content = ((SpeechBubbleComponent)rc).IsShowing ? 1 : 0;
            }
            return s;
        }

        private static int Px(float v) => (int)System.MathF.Round(v);

        private static bool SameNonPositional(ref Snap a, ref Snap b)
            => a.Enabled == b.Enabled && a.Layer == b.Layer && a.Color == b.Color && a.Fx == b.Fx
               && ReferenceEquals(a.Sprite, b.Sprite) && a.Content == b.Content;

        private static bool Same(ref Snap a, ref Snap b)
            => SameNonPositional(ref a, ref b) && a.X == b.X && a.Y == b.Y && a.Depth == b.Depth;

        /// <summary>Equality with positions compared at integer-pixel precision.</summary>
        private static bool SameQ(ref Snap a, ref Snap b)
            => SameNonPositional(ref a, ref b) && Px(a.X) == Px(b.X) && Px(a.Y) == Px(b.Y) && a.Depth == b.Depth;

        private static bool MovedOnly(ref Snap a, ref Snap b) => SameNonPositional(ref a, ref b);

        // ───────────────────────────── sprite keys ─────────────────────────────

        private void NoteSprites(RenderableComponent rc, byte kind)
        {
            if (rc is SpriteRenderer sr)
                NoteSprite(sr.Sprite, kind);
            else if (rc is MultiSpriteAnimator m)
            {
                for (int j = 0; j < m.LayerCount; j++)
                    NoteSprite(m.GetLayer(j)?.Sprite, kind);
            }
            else if (rc is StaticSpriteCompositor c)
            {
                for (int j = 0; j < c.LayerCount; j++)
                    NoteSprite(c.GetLayer(j)?.Sprite, kind);
            }
        }

        private ushort NoteSprite(Sprite sprite, byte kind)
        {
            if (sprite == null)
                return 0;
            if (_spriteIds.TryGetValue(sprite, out ushort id))
                return id;

            id = (ushort)(_spriteIds.Count + 1);
            _spriteIds[sprite] = id;
            _spritesByKind[kind]++;
            string name = sprite.Texture2D?.Name;
            if (string.IsNullOrEmpty(name))
            {
                _unnamedSpritesByKind[kind]++;
                if (sprite.Texture2D != null)
                    _unnamedTextures.Add(sprite.Texture2D);
            }
            else
            {
                _textureNames.Add(name);
            }
            _spriteKeys.Add(new SpriteKey(name, sprite.SourceRect));
            return id;
        }

        // ───────────────────────────── synthetic op streams (§3.1 layout) ─────────────────────────────

        private static int StreamOpBytes(RenderableComponent rc, int opClass, bool q)
        {
            // Sprite: id u16, x, y, depth f32, layer u16, color u32, flags u8. Quantized: x,y as i16.
            int pos2 = q ? 4 : 8;
            switch (opClass)
            {
                case OpSprite: return 2 + pos2 + 4 + 2 + 4 + 1;
                case OpComposite: return 1 + CompositeLayerCount(rc) * (2 + pos2 + 4 + 1) + pos2 + 4 + 2;
                case OpText: return 2 + pos2 + 4 + 4 + 1 + 1;
                case OpRect: return pos2 * 2 + 4 + 1;
                case OpHpBar: return (2 + pos2 + 4 + 4 + 1 + 1) + 2 * (pos2 * 2 + 4 + 1);
                case OpSpeech: return (2 + pos2 * 2 + 4) + (2 + pos2 + 4 + 4 + 1 + 1);
                default: return 0;
            }
        }

        private static void Pos(StreamBuf b, float x, float y, bool q)
        {
            if (q) { b.I16(Px(x)); b.I16(Px(y)); }
            else { b.F32(x); b.F32(y); }
        }

        private void WriteOps(StreamBuf b, RenderableComponent rc, ref Snap s, int opClass, bool quantized)
        {
            bool q = quantized;
            b.TickEmitted++;
            b.U16(s.Id);
            b.U16((ushort)StreamOpBytes(rc, opClass, q));
            switch (opClass)
            {
                case OpSprite:
                    b.U16(NoteSprite(s.Sprite, s.Kind));
                    Pos(b, s.X, s.Y, q); b.F32(s.Depth);
                    b.U16((ushort)s.Layer); b.U32(s.Color); b.U8(s.Fx);
                    break;
                case OpComposite:
                    if (rc is MultiSpriteAnimator m)
                    {
                        b.U8((byte)m.LayerCount);
                        for (int j = 0; j < m.LayerCount; j++)
                        {
                            var layer = m.GetLayer(j);
                            b.U16(NoteSprite(layer?.Sprite, s.Kind));
                            Pos(b, layer != null ? layer.LocalOffset.X : 0f, layer != null ? layer.LocalOffset.Y : 0f, q);
                            b.U32(layer != null ? layer.LayerColor.PackedValue : 0u); b.U8((byte)(layer != null && layer.FlipX ? 1 : 0));
                        }
                    }
                    else if (rc is StaticSpriteCompositor c)
                    {
                        b.U8((byte)c.LayerCount);
                        for (int j = 0; j < c.LayerCount; j++)
                        {
                            var layer = c.GetLayer(j);
                            b.U16(NoteSprite(layer?.Sprite, s.Kind));
                            Pos(b, layer != null ? layer.LocalOffset.X : 0f, layer != null ? layer.LocalOffset.Y : 0f, q);
                            b.U32(layer != null ? layer.Color.PackedValue : 0u); b.U8(layer != null ? (byte)layer.SpriteEffects : (byte)0);
                        }
                    }
                    Pos(b, s.X, s.Y, q); b.F32(s.Depth); b.U16((ushort)s.Layer);
                    break;
                case OpText:
                    b.U16((ushort)s.Kind); Pos(b, s.X, s.Y, q); b.U32(s.Color); b.F32(1f); b.U8(0); b.U8(0);
                    break;
                case OpRect:
                    Pos(b, s.X, s.Y, q); Pos(b, 0f, 0f, q); b.U32(s.Color); b.U8(0);
                    break;
                case OpHpBar:
                    b.U16((ushort)s.Kind); Pos(b, s.X, s.Y, q); b.U32(s.Color); b.F32(1f); b.U8(0); b.U8(0);
                    for (int r = 0; r < 2; r++) { Pos(b, s.X, s.Y, q); Pos(b, 0f, 0f, q); b.U32(s.Color); b.U8(0); }
                    break;
                case OpSpeech:
                    b.U16(1); Pos(b, s.X, s.Y, q); Pos(b, 0f, 0f, q); b.U32(s.Color);
                    b.U16((ushort)s.Kind); Pos(b, s.X, s.Y, q); b.U32(s.Color); b.F32(1f); b.U8(0); b.U8(0);
                    break;
            }
        }

        /// <summary>Composite "moved" op: id, opsLen, x i16, y i16, depth f32 (12 B).</summary>
        private static void WriteMoved(StreamBuf b, ref Snap s)
        {
            b.TickEmitted++;
            b.U16(s.Id);
            b.U16(8);
            b.I16(Px(s.X)); b.I16(Px(s.Y)); b.F32(s.Depth);
        }

        private static void WriteTombstones(StreamBuf b, int count)
        {
            for (int t = 0; t < count; t++) { b.U16(0); b.U16(0xFFFF); }
        }

        private long Deflate(byte[] buf, int len, CompressionLevel level)
        {
            _deflateOut.SetLength(0);
            using (var ds = new DeflateStream(_deflateOut, level, leaveOpen: true))
                ds.Write(buf, 0, len);
            return _deflateOut.Length;
        }

        // ───────────────────────────── reporting (presentation side) ─────────────────────────────

        private void CountKinds(Scene scene)
        {
            for (int k = 0; k < KindCount; k++)
                _kindCounts[k] = 0;
            if (scene == null)
                return;
            var list = scene.RenderableComponents;
            for (int i = 0; i < list.Count; i++)
            {
                var rc = list[i] as RenderableComponent;
                if (rc == null) continue;
                _kindCounts[Classify(rc)]++;
            }
            for (int k = 0; k < KindCount; k++)
                if (_kindCounts[k] > _kindCountsMax[k])
                    _kindCountsMax[k] = _kindCounts[k];
        }

        private void MeasureTileKeyframe()
        {
            var map = Core.Services.GetService<TiledMapService>()?.CurrentMap;
            if (map == null)
                return;
            // Base, Detail, FogOfWar gid arrays as the design's tile keyframe would store them (once per window)
            int len = 0;
            for (int li = 0; li < 3; li++)
            {
                var layer = map.GetLayer(LayerNames[li]) as TmxLayer;
                if (layer == null || layer.Grid == null) continue;
                if (_tileBuf == null || _tileBuf.Length < len + layer.Grid.Length * 4)
                {
                    var grown = new byte[(len + layer.Grid.Length * 4) * 3];
                    if (_tileBuf != null) System.Buffer.BlockCopy(_tileBuf, 0, grown, 0, len);
                    _tileBuf = grown;
                }
                for (int g = 0; g < layer.Grid.Length; g++)
                {
                    uint v = layer.Grid[g];
                    _tileBuf[len++] = (byte)v; _tileBuf[len++] = (byte)(v >> 8);
                    _tileBuf[len++] = (byte)(v >> 16); _tileBuf[len++] = (byte)(v >> 24);
                }
            }
            _tileKeyframeRaw = len;
            _tileKeyframeDeflated = len > 0 ? Deflate(_tileBuf, len, CompressionLevel.Optimal) : 0;
        }

        private void Flush(bool final)
        {
            var w = _window;
            if (w.Ticks == 0 && !final)
                return;
            w.DroppedSamples = _droppedSamples;
            _droppedSamples = 0;
            MeasureTileKeyframe();

            var sb = new StringBuilder(4096);
            _windowIndex++;
            sb.Append("--- window ").Append(_windowIndex).Append(final ? " (final, partial)" : "")
              .Append(" @ ").Append(System.DateTime.Now.ToString("HH:mm:ss")).Append(" ---\n");
            AppendWindow(sb, w);
            _session.Merge(w);
            sb.Append("--- session so far (").Append((System.DateTime.UtcNow - _startedUtc).TotalMinutes.ToString("0.0")).Append(" wall min) ---\n");
            AppendWindow(sb, _session);
            AppendSessionOnly(sb);
            Append(sb.ToString());
            w.Reset();
        }

        private void AppendWindow(StringBuilder sb, Window w)
        {
            double ticks = System.Math.Max(1, w.Ticks);
            double minutes = ticks / 3600.0;
            double usPerTick = 1000000.0 / Stopwatch.Frequency;

            sb.Append("  ticks=").Append(w.Ticks).Append(" (").Append(minutes.ToString("0.00")).Append(" sim min)  buckets:");
            for (int b = 0; b < BucketCount; b++)
                sb.Append(' ').Append(BucketNames[b]).Append('=').Append(w.BucketTicks[b]);
            sb.Append("  dropped samples=").Append(w.DroppedSamples).Append('\n');

            sb.Append("  [1] renderables min/mean/max ").Append(w.Renderables.Format())
              .Append("; captured (enabled, drawn from a frame) ").Append(w.Captured.Format()).Append('\n');
            sb.Append("  [2] changed per tick, min/mean/max: all renderables vs prev | captured vs prev | captured vs prev at pixel precision | captured vs chunk base\n");
            for (int b = 0; b < BucketCount; b++)
            {
                if (w.BucketTicks[b] == 0) continue;
                sb.Append("      ").Append(BucketNames[b]).Append(": ").Append(w.ChangedAll[b].Format())
                  .Append(" | ").Append(w.ChangedCaptured[b].Format()).Append(" | ").Append(w.ChangedCapturedQ[b].Format())
                  .Append(" | ").Append(w.ChangedVsBase[b].Format()).Append('\n');
            }
            sb.Append("  [3] naive delta-frame bytes/tick with the issue's op sizes (vs base, incl. header/tombstones/HUD/events), min/mean/max:\n");
            for (int b = 0; b < BucketCount; b++)
            {
                if (w.BucketTicks[b] == 0) continue;
                sb.Append("      ").Append(BucketNames[b]).Append(": ").Append(w.Bytes[b].Format()).Append('\n');
            }
            sb.Append("      all ticks incl. base frames: ").Append((w.RawBytes / ticks).ToString("0")).Append(" B/tick mean, ")
              .Append(w.BaseFrames).Append(" base frames, raw ").Append(Mb(w.RawBytes / ticks * 216000.0)).Append(" MB/h\n");
            sb.Append("  [3b] encoding variants, real §3.1-layout streams, deflate optimal per 120-tick chunk (per hour of play):\n");
            for (int i = 0; i < StreamCount; i++)
            {
                double rawPerHour = w.StreamRaw[i] / ticks * 216000.0;
                double deflPerChunk = w.StreamChunks[i] > 0 ? (double)w.StreamDeflated[i] / w.StreamChunks[i] : 0;
                double deflPerHour = deflPerChunk * 1800.0;
                double ratio = deflPerHour > 0 ? rawPerHour / deflPerHour : 0;
                double emitted = w.StreamDeltaTicks[i] > 0 ? (double)w.StreamEmitted[i] / w.StreamDeltaTicks[i] : 0;
                sb.Append("      ").Append(StreamNames[i]).Append(": raw ").Append(Mb(rawPerHour)).Append(" MB/h, deflated ")
                  .Append(Mb(deflPerHour)).Append(" MB/h (").Append(ratio.ToString("0.0")).Append("x), ")
                  .Append(emitted.ToString("0.0")).Append(" entities per delta frame, ").Append(w.StreamChunks[i]).Append(" chunks\n");
            }
            sb.Append("      deflate us per chunk (stream A) min/mean/max ").Append(w.DeflateMicros.Format()).Append('\n');
            sb.Append("  [4] tile mutations: ").Append(w.TileOps).Append(" (").Append((w.TileOps / minutes).ToString("0")).Append("/min), same-gid no-ops ")
              .Append(w.TileNoOps).Append("; by layer:");
            for (int l = 0; l < LayerNames.Length; l++)
                if (w.TileOpsByLayer[l] > 0) sb.Append(' ').Append(LayerNames[l]).Append('=').Append(w.TileOpsByLayer[l]);
            sb.Append('\n');
            sb.Append("  [5] console lines: ").Append(w.ConsoleLines).Append(" (").Append((w.ConsoleLines / minutes).ToString("0.0"))
              .Append("/min), segments ").Append(w.ConsoleSegments).Append(", chars ").Append(w.ConsoleChars).Append('\n');
            sb.Append("  [7] census cost us/tick (walk + all five streams) min/mean/max ").Append(w.Cost.Format(usPerTick)).Append('\n');
        }

        private void AppendSessionOnly(StringBuilder sb)
        {
            sb.Append("  [1] renderables by type now (max seen):");
            for (int k = 0; k < KindCount; k++)
                if (_kindCountsMax[k] > 0)
                    sb.Append(' ').Append(KindNames[k]).Append('=').Append(_kindCounts[k]).Append('(').Append(_kindCountsMax[k]).Append(')');
            sb.Append('\n');
            sb.Append("  [6] distinct Sprite refs=").Append(_spriteIds.Count).Append(", distinct (texture, rect) keys=").Append(_spriteKeys.Count)
              .Append(", named textures=").Append(_textureNames.Count).Append(", unnamed textures=").Append(_unnamedTextures.Count).Append('\n');
            sb.Append("      sprites first seen / with unnamed Texture2D by owner type:");
            for (int k = 0; k < KindCount; k++)
                if (_spritesByKind[k] > 0)
                    sb.Append(' ').Append(KindNames[k]).Append('=').Append(_spritesByKind[k]).Append('/').Append(_unnamedSpritesByKind[k]);
            sb.Append('\n');
            sb.Append("      tile keyframe (Base+Detail+FogOfWar gids): raw ").Append(_tileKeyframeRaw).Append(" B, deflated ")
              .Append(_tileKeyframeDeflated).Append(" B\n");
        }

        private static string Mb(double bytes) => (bytes / (1024.0 * 1024.0)).ToString("0.0");

        private void Append(string text)
        {
            Debug.Log(text);
            try
            {
                File.AppendAllText(_logPath, text);
            }
            catch (System.Exception ex)
            {
                Debug.Warn("Frame census log write failed: " + ex.Message);
            }
        }
    }
}
