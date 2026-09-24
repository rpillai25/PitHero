using System;
using System.IO;
using Nez;
using Nez.Tiled;
using PitHero.ECS.Components;
using PitHero.Util;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace PitHero.Services.Replay.Frames
{
    /// <summary>
    /// Scene-scoped recorder of the presentation frame stream (design doc
    /// features/feature_replay_frame_recording_424.md §3.1): once per simulation tick it walks
    /// <c>Scene.RenderableComponents</c>, turns every drawn renderable into ops through
    /// <see cref="FrameCaptureAdapters"/>, adds the HUD record, and hands finished chunks to the session
    /// sidecar worker. Tile mutations and console lines arrive through hooks. Strictly read-only over
    /// the scene: never rolls Nez.Random, reads Input or mutates a component.
    /// Lifecycle mirrors <see cref="ReplayRecorder"/>: created in MainGameScene.Begin, detached in
    /// Unload, <see cref="IsRecording"/> off during playback (ticks already recorded are skipped; ticks
    /// past the recorded end are captured), <see cref="TruncateAfter"/> for Time Travel. A scene
    /// rebuild for a replay hands the stream to the next scene (<see cref="Detach"/> with handoff);
    /// a session that ends closes its sidecar with a footer.
    /// </summary>
    public sealed class FrameRecorder
    {
        /// <summary>Tile layer indices used in tile events and keyframes.</summary>
        public const byte TileLayerBase = 0, TileLayerDetail = 1, TileLayerFogOfWar = 2;
        private const int TileLayerCount = 3;
        private static readonly string[] TileLayerNames = { "Base", "Detail", "FogOfWar" };
        private const string HeroEntityName = "hero";
        private const uint GidMask = 0x1FFFFFFFu;

        private sealed class Handoff
        {
            public FrameSidecarIdentity Identity;
            public FrameStore Store;
            public SpriteKeyRegistry Registry;
            public FrameSessionSidecar Sidecar;
        }

        private static Handoff _pendingHandoff;

        /// <summary>The scene's recorder, or null outside a game session (or with the kill switch off).</summary>
        public static FrameRecorder Current { get; private set; }

        /// <summary>False while a replay plays back: ticks that already have a frame are skipped; ticks past the recorded end are still captured.</summary>
        public bool IsRecording { get; set; } = true;
        public bool IsInitialized { get; private set; }
        /// <summary>Every recorded chunk (memory ring + session file).</summary>
        public FrameStore Store { get; private set; }
        public SpriteKeyRegistry Registry { get; private set; }
        public FrameCaptureContext Context { get; private set; }
        /// <summary>The session file, or null when capture is memory-only.</summary>
        public string SessionFilePath => _sidecar?.Path;
        /// <summary>Ticks captured by this recorder instance.</summary>
        public long CapturedTicks => _captureCount;
        /// <summary>Mean capture cost since the last log line, microseconds.</summary>
        public double MeanCaptureMicros => _captureCount > 0 ? _captureTicksSum * 1_000_000.0 / Stopwatch.Frequency / _captureCount : 0.0;

        private FrameSidecarIdentity _identity;
        private FrameSessionSidecar _sidecar;
        private readonly FrameChunkBuilder _builder = new FrameChunkBuilder(GameConfig.ReplayFrameChunkTicks);
        private readonly FrameEntityIdPool _ids = new FrameEntityIdPool();
        private byte[] _ops = new byte[4096];
        private ConsoleSegmentRecord[] _segments = new ConsoleSegmentRecord[32];
        private readonly DecodedFrame _scratchFrame = new DecodedFrame();
        private readonly DecodedChunk _scratchChunk = new DecodedChunk();
        private bool _builderDirty;      // the builder holds ticks the store/file do not have yet
        private int _rewindPendingTo = -1; // registry flushes to rewind when the loaded chunk gets its first new tick
        private bool _gapWarned, _eventRangeWarned;

        private TiledMapService _map;
        private readonly TmxLayer[] _tileLayers = new TmxLayer[TileLayerCount];
        private GameEventService _events;
        private PauseService _pause;
        private InGameTimeService _time;
        private GameStateService _state;
        private PitWidthManager _pit;
        private MercenaryManager _mercs;
        private Entity _hero;
        private HeroComponent _heroComponent;

        private long _captureTicksSum, _captureTicksMax, _captureCount, _captureStart;
        private float _logTimer;

        private static int ChunkTicks => GameConfig.ReplayFrameChunkTicks;

        /// <summary>Creates the recorder and makes it the current instance.</summary>
        public FrameRecorder()
        {
            Current = this;
        }

        /// <summary>
        /// Sets the stream up for this session: adopts the previous scene's stream when it is the same
        /// session (replay rebuild), else continues or creates the session file under
        /// <paramref name="directory"/>. With <paramref name="preload"/> (a replay) frames after its
        /// recorded end are dropped, as the command recorder drops commands. Stale session files of
        /// other sessions are deleted when a genuinely new session starts.
        /// </summary>
        public void Initialize(int masterSeed, long recordedAtUtcTicks, ReplayData preload, string directory)
        {
            _identity = new FrameSidecarIdentity(masterSeed, recordedAtUtcTicks, GameConfig.SimulationVersion, GameConfig.ReplayFrameFormatVersion, -1);
            long keepThroughTick = preload != null ? preload.TotalTicks - 1 : long.MaxValue;

            var handoff = _pendingHandoff;
            _pendingHandoff = null;
            bool adopted = false;
            if (handoff != null)
            {
                if (handoff.Identity.MatchesRecording(masterSeed, recordedAtUtcTicks, GameConfig.SimulationVersion))
                {
                    Store = handoff.Store;
                    Registry = handoff.Registry;
                    _sidecar = handoff.Sidecar;
                    adopted = true;
                }
                else
                {
                    // Another session's stream (a saved replay set the live one aside, or the way back): close it cleanly
                    handoff.Sidecar?.Finish(handoff.Store, handoff.Store.EndTick + 1);
                    handoff.Store.Clear();
                }
            }

            if (!adopted)
            {
                Registry = new SpriteKeyRegistry();
                Store = new FrameStore(ChunkTicks, GameConfig.ReplayFrameMemoryBudgetBytes);
                if (!string.IsNullOrEmpty(directory))
                {
                    string path = Path.Combine(directory, GameConfig.ReplayFrameSessionFilePrefix + recordedAtUtcTicks + GameConfig.ReplayFrameFileExtension);
                    _sidecar = FrameSessionSidecar.OpenOrCreate(path, _identity, ChunkTicks, Registry);
                    if (preload == null && handoff == null)
                        DeleteStaleSessionFiles(directory, path);
                }
                if (_sidecar != null)
                    Store.Preload(_sidecar);
            }
            if (_sidecar != null)
                Store.IsSpilled = IsChunkSpilled;
            Context = new FrameCaptureContext(Registry);
            _ids.Clear();
            IsInitialized = true;

            if (keepThroughTick < Store.EndTick)
                TruncateStream(keepThroughTick);
            else
                ResumeBuilderFromStore(fileHasLastChunk: true);

            _events = Service<GameEventService>();
            if (_events != null)
                _events.OnEmitAny += OnConsoleEmitted;

            Debug.Log($"[FrameRecorder] Session {recordedAtUtcTicks}: {(adopted ? "adopted" : _sidecar != null && _sidecar.WasReopened ? "reopened" : "new")} stream, {Store.ChunkCount} chunks, end tick {Store.EndTick}, file {(_sidecar != null ? Path.GetFileName(_sidecar.Path) : "none")}");
        }

        /// <summary>Subscribes to the map's tile mutations (call right after the TiledMapService is registered, before the pit is generated).</summary>
        public void AttachMap(TiledMapService map)
        {
            DetachMap();
            _map = map;
            if (map == null)
                return;
            var tmx = map.CurrentMap;
            AttachTileLayers(tmx?.GetLayer<TmxLayer>(TileLayerNames[TileLayerBase]),
                tmx?.GetLayer<TmxLayer>(TileLayerNames[TileLayerDetail]),
                tmx?.GetLayer<TmxLayer>(TileLayerNames[TileLayerFogOfWar]));
            map.TileChanging += OnTileChanging;
        }

        /// <summary>The three mutable layers the keyframes and tile events cover (any may be null).</summary>
        public void AttachTileLayers(TmxLayer baseLayer, TmxLayer detailLayer, TmxLayer fogLayer)
        {
            _tileLayers[TileLayerBase] = baseLayer;
            _tileLayers[TileLayerDetail] = detailLayer;
            _tileLayers[TileLayerFogOfWar] = fogLayer;
        }

        private void DetachMap()
        {
            if (_map != null)
                _map.TileChanging -= OnTileChanging;
            _map = null;
            for (int i = 0; i < TileLayerCount; i++)
                _tileLayers[i] = null;
        }

        /// <summary>A global service, or null when no engine is running (headless tests).</summary>
        private static T Service<T>() where T : class => Core.Instance != null ? Core.Services.GetService<T>() : null;

        /// <summary>
        /// Ends this scene's recording. With <paramref name="handoffToNextScene"/> (a replay rebuild of
        /// the same session) the store, tables and open session file pass to the next recorder; otherwise
        /// the session file gets its footer and the stream is dropped.
        /// </summary>
        public void Detach(bool handoffToNextScene)
        {
            if (Current == this)
                Current = null;
            DetachMap();
            if (_events != null)
            {
                _events.OnEmitAny -= OnConsoleEmitted;
                _events = null;
            }
            if (!IsInitialized)
                return;
            IsInitialized = false;

            FlushBuilder();
            _sidecar?.Drain(Store);
            if (handoffToNextScene)
            {
                _pendingHandoff = new Handoff { Identity = _identity, Store = Store, Registry = Registry, Sidecar = _sidecar };
            }
            else
            {
                _sidecar?.Finish(Store, Store.EndTick + 1);
                Store.Clear();
            }
            _sidecar = null;
            Store = null;
            Registry = null;
            Context = null;
        }

        /// <summary>Drops a stream left behind by a scene that never got a successor (tests, aborted rebuilds).</summary>
        public static void DiscardPendingHandoff()
        {
            var h = _pendingHandoff;
            _pendingHandoff = null;
            if (h == null)
                return;
            h.Sidecar?.Finish(h.Store, h.Store.EndTick + 1);
            h.Store.Clear();
        }

        /// <summary>True when a rebuilt scene will inherit the current stream.</summary>
        public static bool HasPendingHandoff => _pendingHandoff != null;

        // ───────────────────────────── per-tick capture ─────────────────────────────

        /// <summary>
        /// Captures the frame for <paramref name="tick"/>: the tail of MainGameScene.Update, right before
        /// the clock advances (Y-sort depths are final there). Skipped for ticks that already have a
        /// frame while a replay plays back.
        /// </summary>
        public void CaptureTick(Scene scene, long tick)
        {
            if (scene == null || !BeginTickCapture(tick))
                return;
            var list = scene.RenderableComponents;
            int n = list.Count;
            for (int i = 0; i < n; i++)
                CaptureRenderable(list[i] as RenderableComponent);
            EndTickCapture(BuildHud(scene));
        }

        /// <summary>
        /// Opens the capture of a tick (the pieces of <see cref="CaptureTick"/>, for headless tests):
        /// false when the tick is skipped (already recorded during playback, or not the next tick).
        /// </summary>
        public bool BeginTickCapture(long tick)
        {
            if (!IsInitialized)
                return false;
            if (!IsRecording && tick <= Store.EndTick)
                return false;
            long expected = _builder.NextTick;
            if (expected >= 0 && tick != expected)
            {
                if (!_gapWarned)
                {
                    _gapWarned = true;
                    Debug.Warn($"[FrameRecorder] Tick {tick} does not follow the stream end {expected - 1}; frames are not captured until the stream is rebuilt");
                }
                return false;
            }
            _captureStart = Stopwatch.GetTimestamp();
            if (_builder.IsFull)
                FinishChunk();
            if (_rewindPendingTo >= 0)
            {
                // The loaded partial chunk gets new ticks: it will be written again, with its table entries
                Registry.RewindFlushes(_rewindPendingTo);
                _rewindPendingTo = -1;
            }
            EnsureTileKeyframe();
            _ids.BeginTick();
            _builder.BeginTick(tick);
            return true;
        }

        /// <summary>Records one renderable of the open tick (null and entity-less ones are ignored).</summary>
        public void CaptureRenderable(RenderableComponent rc)
        {
            if (rc == null || rc.Entity == null)
                return;
            ushort id = _ids.Acquire(rc);
            if (id == 0 || !rc.Enabled)
                return;
            var w = new FrameWriter(_ops);
            FrameCaptureAdapters.Capture(rc, ref w, Context);
            _ops = w.Buffer;
            if (w.Length > 0)
                _builder.AddEntity(id, _ops, 0, w.Length);
        }

        /// <summary>Closes the open tick with its HUD record; finishes the chunk when it is full.</summary>
        public void EndTickCapture(in HudRecord hud)
        {
            _ids.ReleaseUnseen();
            _builder.EndTick(hud);
            _builderDirty = true;
            if (_builder.IsFull)
                FinishChunk();

            long cost = Stopwatch.GetTimestamp() - _captureStart;
            _captureTicksSum += cost;
            if (cost > _captureTicksMax) _captureTicksMax = cost;
            _captureCount++;
        }

        /// <summary>Main-thread housekeeping once per rendered frame: collects finished chunks from the worker, logs stats in Debug builds.</summary>
        public void PresentationUpdate(float wallDeltaSeconds)
        {
            if (!IsInitialized)
                return;
            _sidecar?.Poll(Store);
#if DEBUG
            _logTimer += wallDeltaSeconds;
            if (_logTimer >= GameConfig.ReplayFrameDebugLogIntervalSeconds)
            {
                _logTimer = 0f;
                double usPerTick = 1_000_000.0 / Stopwatch.Frequency;
                Debug.Log($"[FrameRecorder] chunks {Store.ChunkCount} ({Store.MemoryChunkCount} in RAM, {_sidecar?.SpilledChunkCount ?? 0} on disk), RAM {Store.MemoryBytes / 1024} KB, disk {(_sidecar?.BytesOnDisk ?? 0) / 1024} KB, capture {MeanCaptureMicros:0.0} us/tick mean, {_captureTicksMax * usPerTick:0} us max over {_captureCount} ticks, sprites {Registry.SpriteCount}, strings {Registry.StringCount}, ids {_ids.LiveCount}");
                _captureTicksSum = 0;
                _captureTicksMax = 0;
                _captureCount = 0;
            }
#endif
        }

        /// <summary>
        /// Drops every frame after <paramref name="tick"/> (Time Travel), exactly as the command
        /// recorder drops its records, and continues the chunk that contains the tick.
        /// </summary>
        public void TruncateAfter(long tick)
        {
            if (!IsInitialized)
                return;
            FlushBuilder();
            _sidecar?.Drain(Store);
            if (tick >= Store.EndTick)
                return;
            TruncateStream(tick);
        }

        private void TruncateStream(long tick)
        {
            Store.TruncateAfter(tick);
            int completeChunks = (int)((Store.EndTick + 1) / ChunkTicks);
            _sidecar?.TruncateChunks(completeChunks);
            bool endsOnBoundary = (Store.EndTick + 1) % ChunkTicks == 0;
            ResumeBuilderFromStore(fileHasLastChunk: endsOnBoundary);
        }

        // ───────────────────────────── chunk plumbing ─────────────────────────────

        private void FinishChunk()
        {
            var raw = _builder.Finish(Registry);
            _builderDirty = false;
            _rewindPendingTo = -1;
            if (raw == null)
                return;
            if (_sidecar != null)
                _sidecar.Enqueue(raw);
            else
                Store.Add(FrameChunkCodec.Compress(raw));
        }

        /// <summary>Writes the in-progress chunk out when it holds ticks the store does not have.</summary>
        private void FlushBuilder()
        {
            if (_builder.InTick)
                return;
            if (_builderDirty && !_builder.IsEmpty)
                FinishChunk();
            else
                _builder.Reset();
        }

        /// <summary>
        /// Points the builder at the tick after the store's end; when the store ends mid-chunk the
        /// partial chunk is decoded back into the builder so the next tick continues it.
        /// </summary>
        private void ResumeBuilderFromStore(bool fileHasLastChunk)
        {
            _builder.Reset();
            _builderDirty = false;
            _rewindPendingTo = -1;
            long end = Store.EndTick;
            if (end < 0)
            {
                _builder.Begin(0);
                return;
            }
            if ((end + 1) % ChunkTicks == 0)
            {
                _builder.Begin(end + 1);
                return;
            }
            int index = Store.ChunkIndexOf(end);
            if (Store.TryGetChunk(index, out var chunk))
            {
                FrameChunkCodec.Decode(chunk, _scratchChunk);
                _builder.LoadFrom(_scratchChunk, _scratchFrame, end);
                if (fileHasLastChunk)
                {
                    _rewindPendingTo = index; // only if a new tick lands in it
                }
                else
                {
                    Registry.RewindFlushes(index);
                    _builderDirty = true;     // the file lacks this chunk: it must be written even without new ticks
                }
                return;
            }
            // Unreadable partial chunk: fall back to the last chunk boundary
            Debug.Warn($"[FrameRecorder] Partial chunk {index} could not be reloaded; dropping ticks after {index * (long)ChunkTicks - 1}");
            Store.TruncateAfter(index * (long)ChunkTicks - 1);
            _sidecar?.TruncateChunks(index);
            _builder.Begin(index * (long)ChunkTicks);
        }

        private bool IsChunkSpilled(int index) => _sidecar != null && index < _sidecar.SpilledChunkCount;

        private void EnsureTileKeyframe()
        {
            if (_builder.HasTileKeyframe || _builder.FirstTick < 0)
                return;
            long chunkIndex = _builder.FirstTick / ChunkTicks;
            if (GameConfig.ReplayTileKeyframeIntervalChunks > 1 && chunkIndex % GameConfig.ReplayTileKeyframeIntervalChunks != 0)
                return;
            for (int i = 0; i < TileLayerCount; i++)
            {
                var layer = _tileLayers[i];
                if (layer?.Grid == null)
                    continue;
                _builder.SetTileKeyframeLayer((byte)i, layer.Width, layer.Height, layer.Grid);
            }
        }

        private bool EventTickInChunk(long tick)
        {
            long first = _builder.FirstTick;
            if (first >= 0 && tick >= first && tick < first + ChunkTicks)
                return true;
            if (!_eventRangeWarned)
            {
                _eventRangeWarned = true;
                Debug.Warn($"[FrameRecorder] Event at tick {tick} falls outside the chunk being built (first tick {first}); dropped");
            }
            return false;
        }

        // ───────────────────────────── hooks ─────────────────────────────

        /// <summary>TiledMapService hook, raised before a tile is written: records the change, skipping same-gid writes and undrawn layers.</summary>
        public void OnTileChanging(TmxLayer layer, int x, int y, int newGid)
        {
            if (!IsInitialized || layer == null)
                return;
            long tick = SimulationClock.CurrentTick;
            if (!IsRecording && tick <= Store.EndTick)
                return;
            int li = -1;
            for (int i = 0; i < TileLayerCount; i++)
            {
                if (ReferenceEquals(_tileLayers[i], layer))
                {
                    li = i;
                    break;
                }
            }
            if (li < 0 || layer.Grid == null)
                return;
            int idx = x + y * layer.Width;
            if (x < 0 || y < 0 || x >= layer.Width || idx < 0 || idx >= layer.Grid.Length)
                return;
            if ((int)(layer.Grid[idx] & GidMask) == newGid)
                return;
            EnsureTileKeyframe(); // the snapshot is the state before this chunk's first event
            if (!EventTickInChunk(tick))
                return;
            _builder.AddTileEvent(tick, (byte)li, (ushort)x, (ushort)y, newGid);
            _builderDirty = true;
        }

        /// <summary>GameEventService hook: records a console line with its segments interned.</summary>
        public void OnConsoleEmitted(ConsoleSegment[] segments)
        {
            if (!IsInitialized || segments == null)
                return;
            long tick = SimulationClock.CurrentTick;
            if (!IsRecording && tick <= Store.EndTick)
                return;
            if (!EventTickInChunk(tick))
                return;
            int count = Math.Min(segments.Length, byte.MaxValue);
            if (_segments.Length < count)
                _segments = new ConsoleSegmentRecord[Math.Max(count, _segments.Length * 2)];
            for (int i = 0; i < count; i++)
            {
                var s = segments[i];
                _segments[i] = new ConsoleSegmentRecord(Context.StringId(s.Text), s.Color.PackedValue, Context.StringId(s.ItemName));
            }
            _builder.AddConsoleEvent(tick, new ReadOnlySpan<ConsoleSegmentRecord>(_segments, 0, count));
            _builderDirty = true;
        }

        // ───────────────────────────── HUD record ─────────────────────────────

        private HudRecord BuildHud(Scene scene)
        {
            var hud = new HudRecord();
            if (_hero == null || _hero.IsDestroyed || _hero.Scene != scene)
            {
                _hero = scene.FindEntity(HeroEntityName);
                _heroComponent = _hero?.GetComponent<HeroComponent>();
            }
            var linked = _heroComponent?.LinkedHero;
            if (linked != null && _hero != null && !_hero.HasComponent<HeroDeathComponent>())
                hud.Hero = new HudMember { Present = true, Hp = linked.CurrentHP, MaxHp = linked.MaxHP, Mp = linked.CurrentMP, MaxMp = linked.MaxMP, Level = linked.Level };

            _mercs ??= Service<MercenaryManager>();
            if (_mercs != null)
            {
                hud.Merc1 = MercMember(_mercs.GetHiredMercenary(0));
                hud.Merc2 = MercMember(_mercs.GetHiredMercenary(1));
            }

            _state ??= Service<GameStateService>();
            if (_state != null)
                hud.Gold = _state.Funds;
            _pit ??= Service<PitWidthManager>();
            if (_pit != null)
            {
                hud.PitLevel = _pit.CurrentPitLevel;
                hud.PitTier = _pit.CurrentPitTier;
            }
            _time ??= Service<InGameTimeService>();
            if (_time != null)
                hud.InGameSeconds = MathF.Floor(_time.AccumulatedSeconds); // the clock label shows minutes; whole seconds keep the record from changing every tick
            _pause ??= Service<PauseService>();
            hud.Paused = _pause != null && _pause.IsPaused;
            return hud;
        }

        private static HudMember MercMember(Entity entity)
        {
            var comp = entity?.GetComponent<MercenaryComponent>();
            var merc = comp?.LinkedMercenary;
            if (merc == null || merc.CurrentHP <= 0)
                return default;
            return new HudMember { Present = true, Hp = merc.CurrentHP, MaxHp = merc.MaxHP, Mp = merc.CurrentMP, MaxMp = merc.MaxMP, Level = merc.Level };
        }

        // ───────────────────────────── files ─────────────────────────────

        private static void DeleteStaleSessionFiles(string directory, string keepPath)
        {
            try
            {
                var files = Directory.GetFiles(directory, GameConfig.ReplayFrameSessionFilePrefix + "*" + GameConfig.ReplayFrameFileExtension);
                for (int i = 0; i < files.Length; i++)
                {
                    if (string.Equals(files[i], keepPath, StringComparison.OrdinalIgnoreCase))
                        continue;
                    File.Delete(files[i]);
                }
            }
            catch (IOException ex)
            {
                Debug.Warn("[FrameRecorder] Could not clean stale session files: " + ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.Warn("[FrameRecorder] Could not clean stale session files: " + ex.Message);
            }
        }
    }
}
