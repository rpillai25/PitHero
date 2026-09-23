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
    /// features/feature_replay_frame_recording_424.md §3.1) would have to store. Output goes to
    /// frame_census.log next to the replay files, so Release builds report too.
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
            KTreeBand = 19, KGraphicalHud = 20, KTiledMap = 21, KParticleEmitter = 22, KOther = 23;
        private const int KindCount = 24;
        private static readonly string[] KindNames =
        {
            "SpriteRenderer", "YSortSpriteRenderer", "PrototypeSpriteRenderer", "SpriteAnimator",
            "PausableSpriteAnimator", "EnemyAnimationComponent", "HeroAnimationComponent(layer)", "MultiSpriteAnimator",
            "StaticSpriteCompositor", "TextRenderComponent", "RisingTextComponent", "BouncyTextComponent",
            "BouncyDigitComponent", "SpeechBubbleComponent", "MonsterHPBarComponent", "BuildingOutlineRenderComponent",
            "SelectBoxRenderComponent", "ActionQueueVisualizationComponent", "CloudOverlayComponent", "TreeBandComponent",
            "GraphicalHUD", "TiledMapRenderer", "ParticleEmitter", "other",
        };

        // Op sizes from issue #425 / design §3.1, plus the per-entity header (id u16 + opsLen u16)
        private const int OpNone = 0, OpSprite = 1, OpComposite = 2, OpText = 3, OpRect = 4, OpHpBar = 5, OpSpeech = 6;
        private const int SpriteOpBytes = 20, CompositeOpBytes = 8, CompositeLayerBytes = 13, TextOpBytes = 16,
            RectOpBytes = 21, NinePatchOpBytes = 22, EntityHeaderBytes = 4, TombstoneBytes = 4;
        private const int HudRecordBytes = 40, TileEventBytes = 11, ConsoleLineBytes = 4, ConsoleSegmentBytes = 8;

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
            public bool Enabled;
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
            public bool BaseFrame;
            public int Renderables, Captured, ChangedAll, ChangedCaptured, ChangedVsBase, Bytes, TileOps, ConsoleLines;
            public long CostTicks;
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
            public long Ticks, RawBytes, BaseFrames, ChunkRaw, ChunkDeflateOptimal, ChunkDeflateFastest, ChunksCompressed;
            public long TileOps, TileNoOps, ConsoleLines, ConsoleSegments, ConsoleChars, DroppedSamples;
            public readonly long[] TileOpsByLayer = new long[LayerNames.Length];
            public readonly long[] BucketTicks = new long[BucketCount];
            public readonly Stat[] ChangedAll = NewStats(), ChangedCaptured = NewStats(), ChangedVsBase = NewStats(), Bytes = NewStats();
            public readonly Stat Captured = new Stat(), Renderables = new Stat(), Cost = new Stat(), CompressMicros = new Stat();

            private static Stat[] NewStats() { var s = new Stat[BucketCount]; for (int i = 0; i < BucketCount; i++) s[i] = new Stat(); return s; }

            public void Reset()
            {
                Ticks = RawBytes = BaseFrames = ChunkRaw = ChunkDeflateOptimal = ChunkDeflateFastest = ChunksCompressed = 0;
                TileOps = TileNoOps = ConsoleLines = ConsoleSegments = ConsoleChars = DroppedSamples = 0;
                for (int i = 0; i < TileOpsByLayer.Length; i++) TileOpsByLayer[i] = 0;
                for (int i = 0; i < BucketCount; i++)
                {
                    BucketTicks[i] = 0; ChangedAll[i].Reset(); ChangedCaptured[i].Reset(); ChangedVsBase[i].Reset(); Bytes[i].Reset();
                }
                Captured.Reset(); Renderables.Reset(); Cost.Reset(); CompressMicros.Reset();
            }

            public void Merge(Window o)
            {
                Ticks += o.Ticks; RawBytes += o.RawBytes; BaseFrames += o.BaseFrames; ChunkRaw += o.ChunkRaw;
                ChunkDeflateOptimal += o.ChunkDeflateOptimal; ChunkDeflateFastest += o.ChunkDeflateFastest; ChunksCompressed += o.ChunksCompressed;
                TileOps += o.TileOps; TileNoOps += o.TileNoOps; ConsoleLines += o.ConsoleLines; ConsoleSegments += o.ConsoleSegments;
                ConsoleChars += o.ConsoleChars; DroppedSamples += o.DroppedSamples;
                for (int i = 0; i < TileOpsByLayer.Length; i++) TileOpsByLayer[i] += o.TileOpsByLayer[i];
                for (int i = 0; i < BucketCount; i++)
                {
                    BucketTicks[i] += o.BucketTicks[i]; ChangedAll[i].Merge(o.ChangedAll[i]); ChangedCaptured[i].Merge(o.ChangedCaptured[i]);
                    ChangedVsBase[i].Merge(o.ChangedVsBase[i]); Bytes[i].Merge(o.Bytes[i]);
                }
                Captured.Merge(o.Captured); Renderables.Merge(o.Renderables); Cost.Merge(o.Cost); CompressMicros.Merge(o.CompressMicros);
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

        // Per-tick state (sim side; read-only over the scene)
        private Dictionary<RenderableComponent, Snap> _prev = new Dictionary<RenderableComponent, Snap>(1024);
        private Dictionary<RenderableComponent, Snap> _cur = new Dictionary<RenderableComponent, Snap>(1024);
        private readonly Dictionary<RenderableComponent, Snap> _base = new Dictionary<RenderableComponent, Snap>(1024);
        private readonly Dictionary<Sprite, ushort> _spriteIds = new Dictionary<Sprite, ushort>(2048);
        private ushort _nextEntityId = 1;
        private int _chunkTick;
        private int _tileOpsThisTick, _consoleLinesThisTick, _consoleBytesThisTick;
        private Entity _heroEntity;
        private HeroComponent _heroComponent;

        // Synthetic op stream, double-buffered: the sim fills one chunk, the presentation pass deflates the other
        private byte[] _chunkBuf = new byte[256 * 1024];
        private byte[] _pendingBuf = new byte[256 * 1024];
        private int _chunkLen, _pendingLen;
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
        }

        /// <summary>Creates the census for a new scene (called from MainGameScene.Begin when the const is on).</summary>
        public static void Start()
        {
            Detach();
            var files = Core.Services.GetService<ReplayFileService>();
            string dir = files != null ? files.Directory_ : Path.GetTempPath();
            Current = new ReplayFrameCensus(Path.Combine(dir, LogFileName));
            Current.Append("=== frame census started " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
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
            var swap = _prev; _prev = _cur; _cur = swap;
            _cur.Clear();
            if (baseFrame)
                _base.Clear();

            var list = scene.RenderableComponents;
            int n = list.Count;
            int captured = 0, changedAll = 0, changedCaptured = 0, changedVsBase = 0, matchedPrev = 0, matchedBase = 0, bytes = 0;

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
                _cur[rc] = s;

                bool differsFromPrev = !had || !Same(ref p, ref s);
                if (had) matchedPrev++;
                if (differsFromPrev) changedAll++;

                int opClass = s.Enabled ? OpClassOf(rc, kind) : OpNone;
                if (opClass == OpNone)
                    continue;

                captured++;
                if (differsFromPrev) changedCaptured++;
                if (!had) NoteSprites(rc, kind);

                bool emit;
                if (baseFrame)
                {
                    _base[rc] = s;
                    emit = true;
                }
                else if (_base.TryGetValue(rc, out var b))
                {
                    matchedBase++;
                    emit = !Same(ref b, ref s);
                }
                else
                {
                    emit = true;
                }

                if (emit)
                {
                    if (!baseFrame) changedVsBase++;
                    bytes += EntityHeaderBytes + OpBytesOf(rc, opClass);
                    WriteOps(rc, ref s, opClass);
                }
            }

            // Renderables gone since the previous tick count as changes; captured ones gone since the base are tombstones
            changedAll += _prev.Count - matchedPrev;
            if (!baseFrame)
            {
                int tombstones = _base.Count - matchedBase;
                changedVsBase += tombstones;
                bytes += tombstones * TombstoneBytes;
                for (int t = 0; t < tombstones; t++) { WriteU16(0); WriteU16(0xFFFF); }
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
                    Renderables = n,
                    Captured = captured,
                    ChangedAll = changedAll,
                    ChangedCaptured = changedCaptured,
                    ChangedVsBase = changedVsBase,
                    Bytes = bytes,
                    TileOps = _tileOpsThisTick,
                    ConsoleLines = _consoleLinesThisTick,
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
                if (_pendingLen == 0)
                {
                    var tmp = _pendingBuf; _pendingBuf = _chunkBuf; _chunkBuf = tmp;
                    _pendingLen = _chunkLen;
                }
                // else: the presentation pass has not caught up; this chunk is not measured for compression
                _chunkLen = 0;
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

            if (_pendingLen > 0)
            {
                long t0 = Stopwatch.GetTimestamp();
                long optimal = Deflate(_pendingBuf, _pendingLen, CompressionLevel.Optimal);
                long micros = (Stopwatch.GetTimestamp() - t0) * 1000000L / Stopwatch.Frequency;
                long fastest = Deflate(_pendingBuf, _pendingLen, CompressionLevel.Fastest);
                _window.ChunkRaw += _pendingLen;
                _window.ChunkDeflateOptimal += optimal;
                _window.ChunkDeflateFastest += fastest;
                _window.ChunksCompressed++;
                _window.CompressMicros.Add(micros);
                _pendingLen = 0;
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
                    // Live-only (clouds, tree band, HUD, action queue), tile maps (tile events), particles (v1 skip), unknown
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

        private static bool Same(ref Snap a, ref Snap b)
            => a.Enabled == b.Enabled && a.X == b.X && a.Y == b.Y && a.Depth == b.Depth && a.Layer == b.Layer
               && a.Color == b.Color && a.Fx == b.Fx && ReferenceEquals(a.Sprite, b.Sprite) && a.Content == b.Content;

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

        // ───────────────────────────── synthetic op stream ─────────────────────────────

        private void WriteOps(RenderableComponent rc, ref Snap s, int opClass)
        {
            WriteU16(s.Id);
            WriteU16((ushort)OpBytesOf(rc, opClass));
            switch (opClass)
            {
                case OpSprite:
                    WriteU16(NoteSprite(s.Sprite, s.Kind));
                    WriteF32(s.X); WriteF32(s.Y); WriteF32(s.Depth);
                    WriteU16((ushort)s.Layer); WriteU32(s.Color); WriteU8(s.Fx);
                    break;
                case OpComposite:
                    if (rc is MultiSpriteAnimator m)
                    {
                        WriteU8((byte)m.LayerCount);
                        for (int j = 0; j < m.LayerCount; j++)
                        {
                            var layer = m.GetLayer(j);
                            WriteU16(NoteSprite(layer?.Sprite, s.Kind));
                            WriteF32(layer != null ? layer.LocalOffset.X : 0f); WriteF32(layer != null ? layer.LocalOffset.Y : 0f);
                            WriteU32(layer != null ? layer.LayerColor.PackedValue : 0u); WriteU8((byte)(layer != null && layer.FlipX ? 1 : 0));
                        }
                    }
                    else if (rc is StaticSpriteCompositor c)
                    {
                        WriteU8((byte)c.LayerCount);
                        for (int j = 0; j < c.LayerCount; j++)
                        {
                            var layer = c.GetLayer(j);
                            WriteU16(NoteSprite(layer?.Sprite, s.Kind));
                            WriteF32(layer != null ? layer.LocalOffset.X : 0f); WriteF32(layer != null ? layer.LocalOffset.Y : 0f);
                            WriteU32(layer != null ? layer.Color.PackedValue : 0u); WriteU8(layer != null ? (byte)layer.SpriteEffects : (byte)0);
                        }
                    }
                    WriteF32(s.X); WriteF32(s.Y); WriteF32(s.Depth); WriteU16((ushort)s.Layer);
                    break;
                case OpText:
                    WriteU16((ushort)s.Kind); WriteF32(s.X); WriteF32(s.Y); WriteU32(s.Color); WriteU8(0); WriteU8(0);
                    break;
                case OpRect:
                    WriteF32(s.X); WriteF32(s.Y); WriteF32(0f); WriteF32(0f); WriteU32(s.Color); WriteU8(0);
                    break;
                case OpHpBar:
                    WriteU16((ushort)s.Kind); WriteF32(s.X); WriteF32(s.Y); WriteU32(s.Color); WriteU8(0); WriteU8(0);
                    for (int r = 0; r < 2; r++) { WriteF32(s.X); WriteF32(s.Y); WriteF32(0f); WriteF32(0f); WriteU32(s.Color); WriteU8(0); }
                    break;
                case OpSpeech:
                    WriteU16(1); WriteF32(s.X); WriteF32(s.Y); WriteF32(0f); WriteF32(0f); WriteU32(s.Color);
                    WriteU16((ushort)s.Kind); WriteF32(s.X); WriteF32(s.Y); WriteU32(s.Color); WriteU8(0); WriteU8(0);
                    break;
            }
        }

        private void Ensure(int extra)
        {
            if (_chunkLen + extra <= _chunkBuf.Length)
                return;
            // Warm-up growth only: a chunk that outgrows the buffer doubles both halves once
            var grown = new byte[_chunkBuf.Length * 2];
            System.Buffer.BlockCopy(_chunkBuf, 0, grown, 0, _chunkLen);
            _chunkBuf = grown;
        }

        private void WriteU8(byte v) { Ensure(1); _chunkBuf[_chunkLen++] = v; }
        private void WriteU16(ushort v) { Ensure(2); _chunkBuf[_chunkLen++] = (byte)v; _chunkBuf[_chunkLen++] = (byte)(v >> 8); }
        private void WriteU32(uint v)
        {
            Ensure(4);
            _chunkBuf[_chunkLen++] = (byte)v; _chunkBuf[_chunkLen++] = (byte)(v >> 8);
            _chunkBuf[_chunkLen++] = (byte)(v >> 16); _chunkBuf[_chunkLen++] = (byte)(v >> 24);
        }
        private void WriteF32(float v) => WriteU32((uint)System.BitConverter.SingleToInt32Bits(v));

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
            sb.Append("  [2] changed per tick vs previous tick (all renderables | captured only | captured vs chunk base), min/mean/max:\n");
            for (int b = 0; b < BucketCount; b++)
            {
                if (w.BucketTicks[b] == 0) continue;
                sb.Append("      ").Append(BucketNames[b]).Append(": ").Append(w.ChangedAll[b].Format())
                  .Append(" | ").Append(w.ChangedCaptured[b].Format()).Append(" | ").Append(w.ChangedVsBase[b].Format()).Append('\n');
            }
            sb.Append("  [3] naive delta-frame bytes/tick (vs base, incl. header/tombstones/HUD/events), min/mean/max:\n");
            for (int b = 0; b < BucketCount; b++)
            {
                if (w.BucketTicks[b] == 0) continue;
                sb.Append("      ").Append(BucketNames[b]).Append(": ").Append(w.Bytes[b].Format()).Append('\n');
            }
            double rawPerHour = w.RawBytes / ticks * 216000.0;
            sb.Append("      all ticks incl. base frames: ").Append((w.RawBytes / ticks).ToString("0")).Append(" B/tick mean, ")
              .Append(w.BaseFrames).Append(" base frames, raw ").Append(Mb(rawPerHour)).Append(" MB/h\n");
            if (w.ChunkRaw > 0)
            {
                double ro = (double)w.ChunkRaw / System.Math.Max(1, w.ChunkDeflateOptimal);
                double rf = (double)w.ChunkRaw / System.Math.Max(1, w.ChunkDeflateFastest);
                sb.Append("      synthetic op stream deflate (").Append(w.ChunksCompressed).Append(" chunks): optimal ")
                  .Append(ro.ToString("0.0")).Append("x -> ").Append(Mb(rawPerHour / ro)).Append(" MB/h, fastest ")
                  .Append(rf.ToString("0.0")).Append("x -> ").Append(Mb(rawPerHour / rf)).Append(" MB/h; optimal deflate us/chunk ")
                  .Append(w.CompressMicros.Format()).Append('\n');
            }
            sb.Append("  [4] tile mutations: ").Append(w.TileOps).Append(" (").Append((w.TileOps / minutes).ToString("0")).Append("/min), same-gid no-ops ")
              .Append(w.TileNoOps).Append("; by layer:");
            for (int l = 0; l < LayerNames.Length; l++)
                if (w.TileOpsByLayer[l] > 0) sb.Append(' ').Append(LayerNames[l]).Append('=').Append(w.TileOpsByLayer[l]);
            sb.Append('\n');
            sb.Append("  [5] console lines: ").Append(w.ConsoleLines).Append(" (").Append((w.ConsoleLines / minutes).ToString("0.0"))
              .Append("/min), segments ").Append(w.ConsoleSegments).Append(", chars ").Append(w.ConsoleChars).Append('\n');
            sb.Append("  [7] census cost us/tick min/mean/max ").Append(w.Cost.Format(usPerTick)).Append('\n');
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
