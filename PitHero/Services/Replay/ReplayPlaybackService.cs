using System;
using Nez;
using Nez.Systems;
using PitHero.AI;
using PitHero.ECS.Scenes;
using PitHero.UI;

namespace PitHero.Services.Replay
{
    /// <summary>Playback states.</summary>
    public enum ReplayPlaybackState
    {
        Idle,
        Starting,
        Playing,
        Paused,
        Seeking,
        AtEnd,
    }

    /// <summary>How a replay is shown.</summary>
    public enum ReplayPlaybackMode
    {
        /// <summary>Re-simulation through the live scene (saved replays, and every resume path).</summary>
        Simulated,
        /// <summary>Recorded frames drawn over the untouched live scene; nothing simulates (Replay Current Session, issue #428).</summary>
        FrameView,
    }

    /// <summary>
    /// Drives a recorded session. In <see cref="ReplayPlaybackMode.FrameView"/> (Replay Current Session
    /// with a complete frame stream, or a saved replay with a valid <c>.frames</c> cache, issue #429)
    /// the live scene stays as it is, its simulation is suspended and a
    /// <see cref="Frames.ReplayFrameViewer"/> draws the recorded tick at the playhead: scrubbing either
    /// way is instant, playback runs no simulation and Exit is instant. The timeline ends at the
    /// recorded session end; there is no simulated future (issue #438).
    /// In <see cref="ReplayPlaybackMode.Simulated"/> (saved replays without a cache, and every resume
    /// path) the game scene is restarted from the recording's start state and seed, the recorded
    /// commands are injected on their ticks and the fixed-step clock is stepped (Core.SimulationSpeed /
    /// SimulationSuspended / PendingExtraSteps) according to play, pause and seek requests. Backward
    /// seeks restart from tick 0 and fast-forward; forward seeks fast-forward in wall-budgeted bursts.
    /// Recorded tripwire hashes are compared as the replay runs and the first mismatch is reported.
    /// Time Travel Here (always to the past) from FrameView re-simulates to the playhead behind the
    /// frozen recorded frame. Global service; <see cref="Current"/> for scene code.
    /// </summary>
    public sealed class ReplayPlaybackService
    {
        /// <summary>The global instance, or null before Game1 registers it.</summary>
        public static ReplayPlaybackService Current { get; private set; }

        /// <summary>True while a replay is starting, playing, paused, seeking or at its end.</summary>
        public static bool IsPlaybackActive => Current != null && Current.IsActive;

        /// <summary>The recording being played, or null.</summary>
        public ReplayData Data { get; private set; }

        /// <summary>Current playback state.</summary>
        public ReplayPlaybackState State { get; private set; } = ReplayPlaybackState.Idle;

        /// <summary>How the replay is shown right now (a resume path switches FrameView to Simulated).</summary>
        public ReplayPlaybackMode Mode { get; private set; } = ReplayPlaybackMode.Simulated;

        /// <summary>The frame viewer while one is on screen (FrameView, or the frozen picture over a resume rebuild), else null.</summary>
        public Frames.ReplayFrameViewer Viewer => _viewer;

        /// <summary>True in any state other than Idle.</summary>
        public bool IsActive => State != ReplayPlaybackState.Idle;

        /// <summary>Length of the recording in ticks.</summary>
        public long TotalTicks { get; private set; }

        /// <summary>Tick the current seek is heading for (valid while Seeking or Starting).</summary>
        public long SeekTarget { get; private set; }

        /// <summary>Index into GameConfig.SpeedSteps.</summary>
        public int SpeedIndex { get; private set; }

        /// <summary>Tick of the first detected divergence, or -1.</summary>
        public long DivergenceTick { get; private set; } = -1;

        /// <summary>Diagnostic description of the first divergence (log only), or null.</summary>
        public string DivergenceKind { get; private set; }

        /// <summary>True when the first divergence was a hero decision (GOAP plan) rather than a state sample.</summary>
        public bool DivergenceIsDecision { get; private set; }

        /// <summary>The playhead: the viewer's cursor in FrameView, else the simulation tick the replayed scene is at.</summary>
        public long CurrentTick => Mode == ReplayPlaybackMode.FrameView && _viewer != null ? _viewer.Cursor.Cursor : SimulationClock.CurrentTick;

        /// <summary>The speed ladder of the current mode: the view-only ladder in FrameView (nothing simulates, so 16X/32X cost nothing), the simulation ladder otherwise.</summary>
        private float[] SpeedLadder => Mode == ReplayPlaybackMode.FrameView ? GameConfig.ReplayFrameViewSpeedSteps : GameConfig.SpeedSteps;

        /// <summary>Playback speed multiplier.</summary>
        public float Speed => SpeedLadder[SpeedIndex];

        /// <summary>Player-facing rendering of <see cref="Speed"/> (1X / 2X / 4X / 8X, plus 16X / 32X in FrameView).</summary>
        public string SpeedLabel => Mode == ReplayPlaybackMode.FrameView ? GameConfig.ReplayFrameViewSpeedStepLabels[SpeedIndex] : GameConfig.SpeedStepLabels[SpeedIndex];

        /// <summary>True while the FrameView playhead runs backwards (the rewind button or the held left arrow).</summary>
        public bool IsRewinding => Mode == ReplayPlaybackMode.FrameView && _viewer != null && _viewer.Cursor.Direction < 0;

        /// <summary>True when rewind is offered: recorded frames can be read in any order; a re-simulation cannot run backwards.</summary>
        public bool RewindAvailable => Mode == ReplayPlaybackMode.FrameView && _viewer != null && !_timeTravelInFlight;

        /// <summary>Whether time travel is unlocked at all: owning the Chronos Timepiece artifact.</summary>
        public static bool TimeTravelUnlocked => ArtifactService.Current != null && ArtifactService.Current.Owns(PitHero.Artifacts.ArtifactType.ChronosTimepiece);

        /// <summary>
        /// Whether Time Travel Here may be used: the Chronos Timepiece must be owned, and the recording
        /// must be the current session or belong to the hero being played right now (a different hero's
        /// world would silently replace the player's). Unknown ids (old recordings) never qualify.
        /// </summary>
        public bool TimeTravelAllowed => TimeTravelUnlocked
            && (_isCurrentSession || Data != null && Data.HeroId != 0 && Data.HeroId == _liveHeroId);

        /// <summary>
        /// True while playing a recording made by a build whose simulation logic differs from this one
        /// (GameConfig.SimulationVersion). It still plays and can still be time-travelled into — the
        /// world on screen is a valid state computed by this build — but the recorded commands may have
        /// led somewhere else than the original session, so the scrubber warns and the Time Travel
        /// confirmation spells out whether the replay matched the original.
        /// </summary>
        public bool IsOlderSimulation => Data != null && !_isCurrentSession && !Data.IsCurrentSimulation;

        private int _commandCursor;
        private int _decisionCursor;
        private int _hashCursor;
        private System.Collections.Generic.List<ReplayPauseSpan> _pauseSpans = new System.Collections.Generic.List<ReplayPauseSpan>();
        private bool _analyticsWasEnabled;
        private ReplayData _returnSession; // the live session set aside while a saved replay plays
        private PitHero.ECS.Components.CameraViewState? _returnView; // the player's view when the replay started
        private PitHero.ECS.Components.CameraViewState? _pendingView; // view to apply to the next rebuilt scene
        private UIWindowManager.WindowSizeMode? _returnWindowSize; // the player's window-size preference before the replay
        private readonly System.Diagnostics.Stopwatch _seekStopwatch = new System.Diagnostics.Stopwatch();
        private long _startAtTick;
        private bool _isCurrentSession;
        private int _liveHeroId; // hero of the session that was live when playback started
        private ReplayPlaybackState _stateAfterSeek = ReplayPlaybackState.Playing;
        private Action _afterSeek;
        private long _seekStartedAtTick;
        private Frames.ReplayFrameViewer _viewer;
        private bool _timeTravelInFlight; // a Time Travel rebuild runs behind the frozen frame: the playhead is locked
        private string _savedFileName; // the saved replay being played (null for Replay Current Session): the self-cache target
        private bool _holdRewind; // the left arrow is held: releasing it restores the state before the hold
        private ReplayPlaybackState _stateBeforeHoldRewind = ReplayPlaybackState.Paused;

        /// <summary>Creates the service and makes it the global instance.</summary>
        public ReplayPlaybackService()
        {
            Current = this;
        }

        /// <summary>Clears the global instance (tests).</summary>
        public void Detach()
        {
            if (Current == this)
                Current = null;
        }

        /// <summary>
        /// Points the service at a recording without a scene or a start (headless tests): only the
        /// injection and tripwire hooks are meaningful afterwards. Playback state stays Idle.
        /// </summary>
        public void AttachRecordingForTest(ReplayData data)
        {
            Data = data;
            TotalTicks = data != null ? data.TotalTicks : 0;
            _commandCursor = 0;
            _decisionCursor = 0;
            _hashCursor = 0;
        }

        private static string _tracePath;

        /// <summary>
        /// One line per playback state transition in replays/replay_playback.log, in every build
        /// (Debug.Log is compiled out in Release, and a Release freeze otherwise leaves no trail).
        /// </summary>
        private void Trace(string what)
        {
            if (!GameConfig.ReplayPlaybackTraceLog)
                return;
            try
            {
                if (_tracePath == null)
                {
                    string dir = Core.Services.GetService<ReplayFileService>()?.Directory_;
                    if (string.IsNullOrEmpty(dir))
                        return;
                    _tracePath = System.IO.Path.Combine(dir, GameConfig.ReplayPlaybackTraceLogFileName);
                }
                System.IO.File.AppendAllText(_tracePath,
                    $"{DateTime.Now:HH:mm:ss.fff} {what} | mode={Mode} state={State} sim={SimulationClock.CurrentTick} cursor={(_viewer != null ? _viewer.Cursor.Cursor : -1)} total={TotalTicks} viewer={(_viewer != null ? (_viewer.IsFrozen ? "frozen" : "on") : "none")}{Environment.NewLine}");
            }
            catch (Exception)
            {
                // A trace is a courtesy; never let it interrupt play
            }
        }

        // ── Lifecycle ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Starts playing <paramref name="data"/> from its beginning (or seeks to
        /// <paramref name="startAtTick"/> first). Interrupts the current live session. For a saved
        /// replay <paramref name="fileName"/> names its file, so a valid frame cache beside it opens
        /// the replay in the frame viewer instead of re-simulating.
        /// </summary>
        public void Start(ReplayData data, bool isCurrentSession, long startAtTick = 0, string fileName = null)
        {
            if (data == null || data.StateBlob == null && data.Kind == ReplayKind.Load)
            {
                Debug.Warn("[ReplayPlayback] Cannot start: recording has no start state");
                return;
            }

            // Remember where the live session was so Exit can bring the player back to it exactly:
            // for a saved replay that is a snapshot of the live recording; for "Replay Current
            // Session" the replay IS that snapshot
            if (isCurrentSession)
                _returnSession = data;
            else
                _returnSession = ReplayRecorder.Current?.Snapshot(SimulationClock.CurrentTick);

            // The view is the player's, not the recording's: keep it across every scene rebuild
            _returnView = CaptureView();
            _pendingView = _returnView;

            // A replay is there to be watched, so it plays at the full window height even if the
            // player was working in half-height mode; FinishExit puts their preference back. The
            // PERSISTENT preference has to change, not just the current size: the rebuild runs
            // UIWindowManager.ResetForNewScene, which re-applies whatever the preference says.
            // Only the first Start captures it — picking another replay from inside replay mode
            // would otherwise record the Normal we just set as the player's preference.
            if (!_returnWindowSize.HasValue)
            {
                _returnWindowSize = UIWindowManager.PersistentWindowSize;
                UIWindowManager.SetPersistentWindowSize(UIWindowManager.WindowSizeMode.Normal);
                UIWindowManager.ApplyPersistentWindowSize();
            }
            _isCurrentSession = isCurrentSession;
            _savedFileName = isCurrentSession ? null : fileName;
            _holdRewind = false;
            _liveHeroId = Core.Services.GetService<GameStateService>()?.HeroId ?? 0;
            // Captured once per replay (a rebuild re-enters replay presentation with analytics already off)
            _analyticsWasEnabled = Services.Analytics.AnalyticsService.Enabled;
            _timeTravelInFlight = false;

            Data = data;
            TotalTicks = data.TotalTicks;
            _pauseSpans = ReplayPauseSpans.Build(data.Commands, data.TotalTicks, GameConfig.ReplayPauseSkipMinTicks);
            SpeedIndex = 0;
            DivergenceTick = -1;
            DivergenceKind = null;
            DivergenceIsDecision = false;
            _stateAfterSeek = ReplayPlaybackState.Playing;
            _afterSeek = null;

            if (!string.IsNullOrEmpty(data.BuildId) && data.BuildId != BuildIdentity.Current)
                Debug.Warn($"[ReplayPlayback] Recording was made with build {data.BuildId}; this is {BuildIdentity.Current}. Divergence is possible.");
            if (!isCurrentSession && !data.IsCurrentSimulation)
                Debug.Warn($"[ReplayPlayback] Recording was made with simulation version {data.SimulationVersion}; this build is {GameConfig.SimulationVersion}. It plays with a warning; time travel asks for confirmation.");

            ExitViewer();
            if (isCurrentSession && TryStartFrameView(startAtTick))
                return;
            if (!isCurrentSession && TryStartSavedFrameView(fileName, startAtTick))
                return;
            RestartScene(startAtTick);
        }

        // ── Frame view (issue #428) ──────────────────────────────────────────────────

        /// <summary>
        /// Enters FrameView over the live scene when the session's frame stream is complete: the scene is
        /// not rebuilt, its simulation is suspended, and the viewer draws recorded ticks. False (fall back
        /// to re-simulation) with the kill switches off, without a recorder, or with a gap in the stream.
        /// </summary>
        private bool TryStartFrameView(long startAtTick)
        {
            if (!GameConfig.ReplayFrameCaptureEnabled || !GameConfig.ReplayFrameViewEnabled)
                return false;
            var recorder = Frames.FrameRecorder.Current;
            var scene = Core.Scene as MainGameScene;
            if (recorder == null || !recorder.IsInitialized || scene == null)
                return false;
            if (SimulationClock.CurrentTick != TotalTicks)
            {
                Debug.Warn($"[ReplayPlayback] Frame view skipped: the live world is at tick {SimulationClock.CurrentTick}, the recording ends at {TotalTicks}");
                return false;
            }
            // The ticks still in the recorder's builder become frames now, so the whole session is in the store
            recorder.FlushPending();
            if (recorder.Store.EndTick < TotalTicks - 1)
            {
                Debug.Warn($"[ReplayPlayback] Frame view skipped: frames end at tick {recorder.Store.EndTick}, the recording at {TotalTicks - 1}; re-simulating instead");
                return false;
            }

            Mode = ReplayPlaybackMode.FrameView;
            _viewer = new Frames.ReplayFrameViewer(recorder, TotalTicks);
            BeginFrameView(scene, startAtTick);
            Debug.Log($"[ReplayPlayback] Frame view over {TotalTicks} recorded ticks ({recorder.Store.ChunkCount} chunks); no simulation while watching");
            Trace($"Start FrameView chunks={recorder.Store.ChunkCount} startAt={startAtTick}");
            return true;
        }

        /// <summary>
        /// Enters FrameView for a saved replay whose <c>.frames</c> cache matches it (issue #429): the
        /// file's tables and console lines are read once, chunks are inflated on demand under the memory
        /// budget, and the live scene stays untouched underneath. False (re-simulate as before) with
        /// the kill switches off, without a file name, or when the cache is missing, stale or damaged.
        /// </summary>
        private bool TryStartSavedFrameView(string fileName, long startAtTick)
        {
            if (!GameConfig.ReplayFrameCaptureEnabled || !GameConfig.ReplayFrameViewEnabled || string.IsNullOrEmpty(fileName))
                return false;
            var scene = Core.Scene as MainGameScene;
            var files = Core.Services.GetService<ReplayFileService>();
            if (scene == null || files == null)
                return false;
            var result = files.TryOpenFrameCache(fileName, Data.MasterSeed, Data.RecordedAtUtcTicks, Data.SimulationVersion, Data.TotalTicks, out var reader);
            if (result != Frames.FrameSidecarFile.OpenResult.Ok)
            {
                Debug.Log($"[ReplayPlayback] No frame cache for {fileName} ({result}); re-simulating");
                return false;
            }
            var registry = new Frames.SpriteKeyRegistry();
            var consoleLog = new Frames.RecordedConsoleLog();
            try
            {
                reader.RebuildRegistry(registry);
                reader.ReadConsoleLog(consoleLog);
            }
            catch (Exception ex)
            {
                Debug.Warn($"[ReplayPlayback] Frame cache for {fileName} is unreadable ({ex.Message}); re-simulating");
                reader.Dispose();
                return false;
            }
            var store = new Frames.FrameStore(reader.ChunkTicks, GameConfig.ReplayFrameMemoryBudgetBytes);
            store.Preload(reader);

            Mode = ReplayPlaybackMode.FrameView;
            _viewer = new Frames.ReplayFrameViewer(store, registry, consoleLog, null, TotalTicks) { OwnedSource = reader };
            BeginFrameView(scene, startAtTick);
            Debug.Log($"[ReplayPlayback] Frame view over the saved replay {fileName}: {TotalTicks} ticks, {reader.ChunkCount} chunks on disk, {consoleLog.Count} console lines; the live world waits underneath");
            Trace($"Start SavedFrameView file={fileName} chunks={reader.ChunkCount} startAt={startAtTick}");
            return true;
        }

        /// <summary>The common tail of both FrameView starts: viewer on the scene, UI closed, simulation held, playhead placed.</summary>
        private void BeginFrameView(MainGameScene scene, long startAtTick)
        {
            _viewer.AttachToScene(scene);
            EnterReplayPresentation();

            // Everything recorded already happened and nothing simulates while watching: nothing is
            // injected or verified (a Time Travel rebuild resets these)
            _commandCursor = Data.Commands.Count;
            _decisionCursor = Data.Decisions.Count;
            _hashCursor = Data.StateHashes.Count;

            Core.SimulationSuspended = true;
            Core.SimulationSpeed = 1f;
            Core.PendingExtraSteps = 0;
            _viewer.Cursor.Seek(startAtTick);
            State = startAtTick >= TotalTicks ? ReplayPlaybackState.AtEnd : ReplayPlaybackState.Playing;
        }

        /// <summary>Closes the live-input doorway and the UI for a replay (both modes).</summary>
        private void EnterReplayPresentation()
        {
            var commands = PlayerCommandService.Current;
            if (commands != null)
                commands.RejectLiveEnqueues = true;

            // Replayed events already happened once: keep them out of the analytics session log and
            // the event console history
            Services.Analytics.AnalyticsService.Enabled = false;
            var events = Core.Services.GetService<GameEventService>();
            if (events != null)
                events.Suppressed = true;

            Core.Services.GetService<SettingsUI>()?.EnterReplayMode();
        }

        /// <summary>Removes the viewer from its scene (closing a saved replay's file) and brings the console and the portrait back to the live state.</summary>
        private void ExitViewer()
        {
            var viewer = _viewer;
            if (viewer == null)
                return;
            _viewer = null;
            Mode = ReplayPlaybackMode.Simulated;
            if (SpeedIndex >= GameConfig.SpeedSteps.Length)
                SpeedIndex = GameConfig.SpeedSteps.Length - 1; // the view-only rungs do not exist in Simulated mode
            viewer.Dispose();
            // The console showed the recording at the playhead; live play continues at the clock's tick
            // from the live session's own lines (a saved replay's lines belong to another session)
            var scene = Core.Scene as MainGameScene;
            var liveLog = Frames.FrameRecorder.Current?.ConsoleLog ?? (viewer.IsLiveStream ? viewer.ConsoleLog : null);
            scene?.EventConsole?.ShowRecorded(liveLog, SimulationClock.CurrentTick);
            scene?.ClearRecordedPortrait();
        }

        /// <summary>The per-frame drive of FrameView: the playhead moves over recorded frames, either way; the live world never runs while watching.</summary>
        private void UpdateFrameView()
        {
            var cursor = _viewer.Cursor;
            // Recorded sounds only accompany forward play at the low rungs: a scrub, a rewind, a pause
            // or a fast run would turn them into noise
            _viewer.SoundsEnabled = State == ReplayPlaybackState.Playing && cursor.Direction > 0
                && SpeedIndex <= GameConfig.ReplayFrameViewSoundMaxSpeedIndex;
            switch (State)
            {
                case ReplayPlaybackState.Playing:
                {
                    Core.SimulationSuspended = true;
                    if (cursor.Direction < 0)
                    {
                        // Rewind: backwards at the same speed, stopping at the start
                        cursor.Advance(Time.UnscaledDeltaTime, Speed, _pauseSpans);
                        if (cursor.Cursor <= 0)
                            StopRewind(ReplayPlaybackState.Paused);
                        break;
                    }
                    if (cursor.Cursor >= TotalTicks)
                    {
                        State = ReplayPlaybackState.AtEnd;
                        break;
                    }
                    cursor.Advance(Time.UnscaledDeltaTime, Speed, _pauseSpans);
                    if (cursor.Cursor >= TotalTicks)
                        State = ReplayPlaybackState.AtEnd;
                    break;
                }

                case ReplayPlaybackState.Paused:
                case ReplayPlaybackState.AtEnd:
                    Core.SimulationSuspended = true;
                    Core.PendingExtraSteps = 0;
                    break;

                // Seeking never happens in FrameView (a Time Travel rebuild switches to Simulated first)
                case ReplayPlaybackState.Seeking:
                case ReplayPlaybackState.Starting:
                case ReplayPlaybackState.Idle:
                    break;
            }
        }

        /// <summary>Tells the player once, on the console, that a resume rebuild drifted from the recording.</summary>
        private static void NotifyDivergence(long tick, bool decision)
        {
            var text = Core.Services.GetService<TextService>();
            var events = Core.Services.GetService<GameEventService>();
            if (text == null || events == null)
                return;
            string kind = text.DisplayText(TextType.UI, decision ? UITextKey.ReplayDivergenceDecision : UITextKey.ReplayDivergenceState);
            events.Emit(string.Format(text.DisplayText(TextType.UI, UITextKey.ReplayDivergenceAt), ReplayTimeFormatter.FormatTicks(tick), kind), EventPriority.High);
        }

        /// <summary>Tears the current scene down and rebuilds it from the recording's start state, then seeks to <paramref name="startAtTick"/>.</summary>
        private void RestartScene(long startAtTick)
        {
            Trace($"RestartScene startAt={startAtTick}");
            Mode = ReplayPlaybackMode.Simulated; // a frozen viewer, if any, keeps drawing over the rebuild
            if (SpeedIndex >= GameConfig.SpeedSteps.Length)
                SpeedIndex = GameConfig.SpeedSteps.Length - 1; // the view-only rungs cannot be simulated
            _holdRewind = false;
            Debug.QuietMode = false; // scene rebuild logs are worth keeping; the seek that follows re-arms quiet mode
            Core.CosmeticUpdatesSuspended = false;
            PitHero.Util.SoundEffectManager.Muted = true; // silent through the rebuild and any seek; playback unmutes
            _startAtTick = startAtTick;
            _commandCursor = 0;
            _decisionCursor = 0;
            _hashCursor = 0;
            _lastMatchTick = -1;
            _lastMatchDescription = null;
            ReplayBattleTrace.Clear(); // the trace is process-global; a rebuilt scene starts its own history
            State = ReplayPlaybackState.Starting;

            // Live-input doorway closes now; the new scene's service inherits the flag in OnSceneStarted
            var commands = PlayerCommandService.Current;
            if (commands != null)
                commands.RejectLiveEnqueues = true;

            ResetEngineForSceneSwap();

            var blob = ReplayIO.DeserializeSaveData(Data.StateBlob);
            var bootstrap = new ReplaySessionBootstrap(Data.MasterSeed, Data);
            if (blob != null)
            {
                // Restores hero design, funds and stencils on the global services
                SaveLoadService.ApplyLoadedState(blob);
                if (Data.Kind == ReplayKind.NewGame)
                {
                    // The scene must run the new-game path; the vault/defeated monsters come from the blob
                    SaveLoadService.PendingLoadData = null;
                    bootstrap.NewGameGlobals = blob;
                }
            }
            else
            {
                SaveLoadService.PendingLoadData = null;
            }

            ReplaySessionBootstrap.SetPending(bootstrap);
            // The running MainGameScene still owns its scene-scoped services; a trampoline scene lets
            // it unload before the replayed MainGameScene is constructed
            _viewer?.DetachFromScene();
            var boot = new ReplayBootScene(MainGameScene.DefaultMapPath);
            if (_viewer != null)
            {
                // The frozen frame covers the transition frame too, from the player's viewpoint
                if (_pendingView.HasValue)
                {
                    boot.Camera.RawZoom = _pendingView.Value.RawZoom;
                    boot.Camera.Position = _pendingView.Value.Position;
                }
                _viewer.AttachToScene(boot);
            }
            Core.Scene = boot;
        }

        /// <summary>The quit-to-title reset list: nothing from the old scene may leak into the replayed one.</summary>
        private static void ResetEngineForSceneSwap()
        {
            Core.SimulationSpeed = 1f;
            Core.SimulationSuspended = false;
            Core.PendingExtraSteps = 0;
            Core.MaxStepsPerFrame = GameConfig.SimulationMaxStepsPerFrame;
            Time.TimeScale = 1f;
            Core.GetGlobalManager<CoroutineManager>()?.StopAllCoroutines();
            HeroStateMachine.IsBattleInProgress = false;
            HeroStateMachine.CurrentThreatTarget = null;
            Core.Services.GetService<TileStateService>()?.Clear();
            Core.Services.GetService<PauseService>()?.ResetImmediate();
            // Global, lazily created and never removed: a level queued by the debug keys must not
            // leak into the replayed world (the replay re-queues it through its recorded command)
            Core.Services.GetService<PitLevelQueueService>()?.DequeueLevel();
        }

        /// <summary>
        /// Called at the end of MainGameScene.Begin for a scene started from a replay bootstrap.
        /// Installs the tripwire checks, closes the live-input doorway and enters replay mode.
        /// </summary>
        public void OnSceneStarted(MainGameScene scene)
        {
            if (Data == null)
                return;

            _viewer?.AttachToScene(scene); // a resume rebuild: the frozen frame stays on screen through the seek
            Trace($"OnSceneStarted startAt={_startAtTick} afterSeek={(_afterSeek != null)}");

            ReplayTripwire.PlaybackDecisionCheck = CheckDecision;
            ReplayTripwire.PlaybackStateHashCheck = CheckStateHash;
            EnterReplayPresentation();

            if (_pendingView.HasValue)
            {
                scene.CameraController?.RestoreView(_pendingView.Value);
                _pendingView = null;
            }

            if (_startAtTick > 0)
                BeginSeek(_startAtTick);
            else if (_afterSeek != null)
            {
                // Nothing to seek (a session that had not ticked yet): run the continuation now
                var after = _afterSeek;
                _afterSeek = null;
                after();
            }
            else
            {
                State = _stateAfterSeek == ReplayPlaybackState.Paused ? ReplayPlaybackState.Paused : ReplayPlaybackState.Playing;
                PitHero.Util.SoundEffectManager.Muted = false;
            }
        }

        // ── Per-frame driving (presentation pass) ────────────────────────────────────

        /// <summary>Applies the current state to the engine clock. Called every rendered frame by the scene.</summary>
        public void Update()
        {
            // Recruits re-happen during playback; their popups must not pile up for after the exit
            Core.Services.GetService<AlliedMonsterManager>()?.ClearNotifications();

            if (Mode == ReplayPlaybackMode.FrameView && _viewer != null)
            {
                UpdateFrameView();
                return;
            }

            switch (State)
            {
                case ReplayPlaybackState.Playing:
                {
                    // Playback stops at the session end (a fast frame may overshoot it by a few ticks;
                    // InjectDue and the tripwire checks record those so the recording stays gap-free)
                    if (CurrentTick >= TotalTicks)
                    {
                        State = ReplayPlaybackState.AtEnd;
                        Core.SimulationSuspended = true;
                        break;
                    }
                    // Nothing to watch while the recorded session sat in a menu: skip the stretch
                    long skipTo = ReplayPauseSpans.FindSkipTarget(_pauseSpans, CurrentTick);
                    if (skipTo > CurrentTick)
                    {
                        _stateAfterSeek = ReplayPlaybackState.Playing;
                        BeginSeek(skipTo > TotalTicks ? TotalTicks : skipTo);
                        break;
                    }
                    Core.SimulationSuspended = false;
                    Core.SimulationSpeed = Speed;
                    Core.MaxStepsPerFrame = GameConfig.HighSpeedMaxStepsPerFrame;
                    break;
                }

                case ReplayPlaybackState.Paused:
                case ReplayPlaybackState.AtEnd:
                    Core.SimulationSuspended = true;
                    Core.PendingExtraSteps = 0;
                    break;

                case ReplayPlaybackState.Seeking:
                {
                    Core.SimulationSuspended = true;
                    long remaining = SeekTarget - CurrentTick;
                    if (remaining <= 0)
                    {
                        Core.PendingExtraSteps = 0;
                        FinishSeek();
                    }
                    else
                    {
                        // Throttled (issue #432): a burst per frame, then a rest, so a long rebuild warms one
                        // core to the duty cycle instead of pegging it. Every re-simulation goes through here:
                        // Time Travel Here, Exit from an uncached saved replay, scrubs in Simulated mode
                        Core.ExtraStepWallBudgetSeconds = GameConfig.ReplaySeekWallBudgetSeconds;
                        Core.ExtraStepDutyCycle = GameConfig.ReplaySeekDutyCycle;
                        Core.PendingExtraSteps = remaining;
                    }
                    break;
                }

                case ReplayPlaybackState.Starting:
                case ReplayPlaybackState.Idle:
                    break;
            }
        }

        /// <summary>Progress of the current seek in [0,1] for the seek indicator.</summary>
        public float SeekProgress
        {
            get
            {
                if (State != ReplayPlaybackState.Seeking && State != ReplayPlaybackState.Starting)
                    return 1f;
                long span = SeekTarget - _seekStartedAtTick;
                if (span <= 0)
                    return 1f;
                long done = CurrentTick - _seekStartedAtTick;
                return done <= 0 ? 0f : (done >= span ? 1f : (float)done / span);
            }
        }

        // ── Player controls ──────────────────────────────────────────────────────────

        /// <summary>Pauses or resumes playback (no effect while seeking or at the end). Resuming always plays forward.</summary>
        public void TogglePause()
        {
            if (State == ReplayPlaybackState.Playing)
            {
                State = ReplayPlaybackState.Paused;
            }
            else if (State == ReplayPlaybackState.Paused)
            {
                if (_viewer != null)
                    _viewer.Cursor.Direction = 1;
                _holdRewind = false;
                State = ReplayPlaybackState.Playing;
            }
        }

        /// <summary>Cycles to the next playback speed of the current mode's ladder.</summary>
        public void CycleSpeed()
        {
            SpeedIndex = (SpeedIndex + 1) % SpeedLadder.Length;
        }

        /// <summary>
        /// The rewind button (FrameView only): plays the recorded frames backwards at the current speed
        /// from wherever the playhead is, the end included; pressing it while rewinding pauses.
        /// </summary>
        public void ToggleRewind()
        {
            if (!RewindAvailable || State == ReplayPlaybackState.Starting || State == ReplayPlaybackState.Seeking)
                return;
            if (IsRewinding)
            {
                StopRewind(ReplayPlaybackState.Paused);
                return;
            }
            _holdRewind = false;
            StartRewind();
        }

        /// <summary>The left arrow went down while the scrubber had focus: rewind for as long as it is held.</summary>
        public void BeginHoldRewind()
        {
            if (!RewindAvailable || _holdRewind || State == ReplayPlaybackState.Starting || State == ReplayPlaybackState.Seeking)
                return;
            _holdRewind = true;
            _stateBeforeHoldRewind = IsRewinding ? ReplayPlaybackState.Paused : State;
            StartRewind();
        }

        /// <summary>The left arrow came up: back to what the playhead was doing before the hold (paused, playing forward, or at the end).</summary>
        public void EndHoldRewind()
        {
            if (!_holdRewind)
                return;
            _holdRewind = false;
            if (!IsRewinding)
                return;
            var resume = _stateBeforeHoldRewind == ReplayPlaybackState.Playing ? ReplayPlaybackState.Playing : ReplayPlaybackState.Paused;
            StopRewind(resume);
        }

        private void StartRewind()
        {
            if (_viewer.Cursor.Cursor <= 0)
                return;
            _viewer.Cursor.Direction = -1;
            State = ReplayPlaybackState.Playing;
            Trace("Rewind");
        }

        /// <summary>Leaves reverse play; the playhead stays where it is and takes <paramref name="then"/> (AtEnd when it sits at the end).</summary>
        private void StopRewind(ReplayPlaybackState then)
        {
            if (_viewer != null)
                _viewer.Cursor.Direction = 1;
            _holdRewind = false;
            State = CurrentTick >= TotalTicks ? ReplayPlaybackState.AtEnd : then;
        }

        /// <summary>
        /// Moves playback to <paramref name="targetTick"/>, clamped to the recording. FrameView: a
        /// cursor move over recorded frames either way (live or saved stream). Simulated: forward by
        /// fast-forwarding, backward by restarting from tick 0.
        /// </summary>
        public void Seek(long targetTick)
        {
            if (!IsActive || _timeTravelInFlight)
                return;
            if (targetTick < 0) targetTick = 0;
            if (targetTick > TotalTicks) targetTick = TotalTicks;

            if (State == ReplayPlaybackState.Playing || State == ReplayPlaybackState.Paused || State == ReplayPlaybackState.AtEnd)
                _stateAfterSeek = State == ReplayPlaybackState.Paused ? ReplayPlaybackState.Paused : ReplayPlaybackState.Playing;

            if (Mode == ReplayPlaybackMode.FrameView && _viewer != null)
            {
                if (State == ReplayPlaybackState.Starting)
                    return;
                var cursor = _viewer.Cursor;
                cursor.Seek(targetTick);
                cursor.Direction = 1; // a scrub ends a rewind; play resumes forward from the new spot
                _holdRewind = false;
                State = cursor.Cursor >= TotalTicks ? ReplayPlaybackState.AtEnd : _stateAfterSeek;
                return;
            }

            if (targetTick < CurrentTick)
            {
                // A backward seek rebuilds the scene: carry the current view over to the new one
                _pendingView = CaptureView();
                RestartScene(targetTick);
                return;
            }
            BeginSeek(targetTick);
        }

        private static PitHero.ECS.Components.CameraViewState? CaptureView()
        {
            var scene = Core.Scene as MainGameScene;
            var camera = scene?.CameraController;
            return camera != null ? camera.CaptureView() : (PitHero.ECS.Components.CameraViewState?)null;
        }

        private void BeginSeek(long targetTick)
        {
            Trace($"BeginSeek target={targetTick}");
            SeekTarget = targetTick;
            _seekStartedAtTick = CurrentTick;
            State = ReplayPlaybackState.Seeking;
            Core.SimulationSuspended = true;
            // Thousands of simulated steps per second: keep Debug.WriteLine off the hot path and
            // skip purely visual per-step work nobody will see
            Debug.QuietMode = GameConfig.ReplaySeekQuietLogging;
            Core.CosmeticUpdatesSuspended = GameConfig.ReplaySeekSkipsCosmetics;
            PitHero.Util.SoundEffectManager.Muted = true; // no burst of every sound the seek passes through
            _seekStopwatch.Restart();
        }

        private void FinishSeek()
        {
            Debug.QuietMode = false;
            Core.CosmeticUpdatesSuspended = false;
            PitHero.Util.SoundEffectManager.Muted = false;
            _seekStopwatch.Stop();
            long steps = CurrentTick - _seekStartedAtTick;
            double seconds = _seekStopwatch.Elapsed.TotalSeconds;
            if (steps > 0 && seconds > 0.0)
                Debug.Log($"[ReplayPlayback] Seek ran {steps} steps in {seconds:0.00}s ({steps / seconds:0} steps/s, {seconds * 1000.0 / steps:0.000} ms/step)");
            var after = _afterSeek;
            _afterSeek = null;
            Trace($"FinishSeek steps={steps} continuation={(after != null)}");
            if (after != null)
            {
                after();
                return;
            }
            State = CurrentTick >= TotalTicks ? ReplayPlaybackState.AtEnd : _stateAfterSeek;
            if (State == ReplayPlaybackState.AtEnd)
                Core.SimulationSuspended = true;
        }

        /// <summary>
        /// Leaves replay mode. The world is first brought to the end of the recorded timeline (seeking
        /// if needed) so live play continues exactly where the recorded session ended.
        /// </summary>
        public void Exit()
        {
            if (!IsActive)
                return;
            Trace("Exit");

            if (Mode == ReplayPlaybackMode.FrameView && _viewer != null)
            {
                // Watching never moves the live world: the current session sits exactly where it was,
                // and a saved replay's cache never touched it. The exit is the removal of the viewer
                FinishExit();
                return;
            }

            // Watching a saved replay: the live session was set aside, not abandoned. Bring the
            // world back to exactly where it was by re-simulating the live recording to its end.
            if (_returnSession != null && !ReferenceEquals(_returnSession, Data))
            {
                if (State == ReplayPlaybackState.Seeking || State == ReplayPlaybackState.Starting)
                {
                    // Let the in-flight seek/start land, then return
                    _afterSeek = ReturnToLiveSession;
                    return;
                }
                ReturnToLiveSession();
                return;
            }

            if (State == ReplayPlaybackState.Starting)
            {
                // Scene not ready yet: finish the exit once it starts
                _afterSeek = FinishExit;
                _startAtTick = TotalTicks;
                return;
            }
            if (CurrentTick < TotalTicks)
            {
                _afterSeek = FinishExit;
                BeginSeek(TotalTicks);
                return;
            }
            FinishExit();
        }

        /// <summary>Restarts the scene from the live session's recording and seeks to its last tick, then hands control back.</summary>
        private void ReturnToLiveSession()
        {
            TrySelfCacheSimulatedReplay();
            var session = _returnSession;
            _returnSession = null;
            Data = session;
            TotalTicks = session.TotalTicks;
            _pauseSpans.Clear();
            _afterSeek = FinishExit;
            _pendingView = _returnView; // back to the view the player had before the replay
            Debug.Log($"[ReplayPlayback] Returning to the live session at tick {TotalTicks}");
            RestartScene(TotalTicks);
        }

        /// <summary>
        /// Self-caching of an uncached saved replay (issue #431): the Simulated playback's scene recorded
        /// every tick it simulated into a session file under the replay's identity. When the stream reaches
        /// the recording's end, that file is finished (console log in the footer) and moved beside the
        /// recording as its <c>.frames</c> cache, so the next play opens in FrameView; a replay left before
        /// its end stays uncached (the session file keeps the partial stream for a later pass). Runs
        /// before the return rebuild tears the scene down.
        /// </summary>
        private void TrySelfCacheSimulatedReplay()
        {
            if (Mode != ReplayPlaybackMode.Simulated || string.IsNullOrEmpty(_savedFileName) || Data == null)
                return;
            var frames = Frames.FrameRecorder.Current;
            var files = Core.Services.GetService<ReplayFileService>();
            if (frames == null || !frames.IsInitialized || files == null)
                return;
            frames.FlushPending();
            if (frames.Store.EndTick < TotalTicks - 1)
            {
                // Left before the end: the session file keeps what was watched for a later pass
                Trace($"SelfCache skipped file={_savedFileName} endTick={frames.Store.EndTick} total={TotalTicks}");
                return;
            }
            string path = files.FrameCachePath(_savedFileName);
            bool cached = frames.ExportSidecar(path, TotalTicks, endSession: true);
            Trace($"SelfCache file={_savedFileName} cached={cached} endTick={frames.Store.EndTick}");
            if (!cached)
                return;
            files.EnforceFrameCacheBudget(GameConfig.ReplayFrameCacheDiskBudgetBytes);
            Debug.Log($"[ReplayPlayback] {_savedFileName} is cached: the next play opens in the frame viewer");
        }

        /// <summary>
        /// Time travel: abandon the set-aside live session and continue playing from the replay's
        /// CURRENT position (always a past tick). Everything recorded after this tick is discarded so
        /// the recording stays a straight line for later replays.
        /// </summary>
        public void ContinueFromHere()
        {
            if (!IsActive || State == ReplayPlaybackState.Starting || State == ReplayPlaybackState.Seeking)
                return;
            Trace("ContinueFromHere");
            if (Mode == ReplayPlaybackMode.FrameView && _viewer != null)
            {
                long tick = _viewer.Cursor.Cursor;
                if (tick != SimulationClock.CurrentTick || !_viewer.IsLiveStream)
                {
                    // The live world is elsewhere (or another timeline's, for a saved replay): rebuild it
                    // at the playhead behind the frozen picture (design §3.3), then commit exactly as a
                    // landed seek would. A cursor at the live tick of the current session commits as is
                    _returnSession = null;
                    _viewer.Freeze(tick);
                    _afterSeek = CommitHere;
                    _timeTravelInFlight = true;
                    _stateAfterSeek = ReplayPlaybackState.Paused;
                    _pendingView = CaptureView();
                    Debug.Log($"[ReplayPlayback] Time travel to tick {tick}: rebuilding the world behind the recorded frame");
                    RestartScene(tick);
                    return;
                }
            }
            CommitHere();
        }

        /// <summary>Makes the simulation's current tick the new end of the recording and hands the world back to live play.</summary>
        private void CommitHere()
        {
            _returnSession = null;
            long tick = SimulationClock.CurrentTick;
            Trace($"CommitHere tick={tick}");
            // Ticks 0..tick-1 are the new timeline's past; tick itself is simulated live next. Everything
            // the old timeline recorded at or after it goes: a command drained at exactly this tick, a
            // tripwire sample, the frame. (Keeping tick itself left the old frame in the stream, so the
            // frame recorder refused the live capture of that tick with a "does not follow" warning.)
            // A fast playback may have overshot the recorded end by a few ticks; the recorder appended
            // those, so the recording already ends at tick-1 there and the truncation is a no-op
            long lastKept = tick - 1;
            ReplayRecorder.Current?.TruncateAfter(lastKept);
            Frames.FrameRecorder.Current?.TruncateAfter(lastKept);
            TotalTicks = tick;
            long divergence = DivergenceTick;
            bool decision = DivergenceIsDecision;
            Debug.Log($"[ReplayPlayback] Continuing live play from replay tick {tick}");
            FinishExit();
            if (divergence >= 0)
                NotifyDivergence(divergence, decision);
        }

        private void FinishExit()
        {
            Trace("FinishExit");
            ExitViewer();
            _timeTravelInFlight = false;
            _holdRewind = false;
            _savedFileName = null;
            State = ReplayPlaybackState.Idle;
            Data = null;
            _returnSession = null;
            Debug.QuietMode = false;
            Core.CosmeticUpdatesSuspended = false;
            PitHero.Util.SoundEffectManager.Muted = false;

            Services.Analytics.AnalyticsService.Enabled = _analyticsWasEnabled;
            var events = Core.Services.GetService<GameEventService>();
            if (events != null)
                events.Suppressed = false;

            Core.SimulationSuspended = false;
            Core.SimulationSpeed = 1f;
            Core.PendingExtraSteps = 0;
            Core.MaxStepsPerFrame = GameConfig.SimulationMaxStepsPerFrame;

            ReplayTripwire.PlaybackDecisionCheck = null;
            ReplayTripwire.PlaybackStateHashCheck = null;

            var commands = PlayerCommandService.Current;
            if (commands != null)
                commands.RejectLiveEnqueues = false;
            var recorder = ReplayRecorder.Current;
            if (recorder != null)
                recorder.IsRecording = true;
            var frames = Frames.FrameRecorder.Current;
            if (frames != null)
                frames.IsRecording = true;

            // Every window is closed in replay mode: bring the simulation's pause flags in line, ON
            // THE RECORD, so a later replay of this continued session releases them at the same tick
            if (commands != null)
            {
                commands.ApplyNow(PlayerCommand.Flag(PlayerCommandType.SetManualPause, false));
                commands.ApplyNow(PlayerCommand.Flag(PlayerCommandType.SetFarmModePause, false));
            }
            else
            {
                Core.Services.GetService<PauseService>()?.ResetImmediate();
            }

            var settings = Core.Services.GetService<SettingsUI>();
            settings?.FastFUI?.SetSpeedUp(false);
            settings?.ExitReplayMode();

            // Hand the player's window-size preference back last, so every UI element re-lays out
            // against the size they end up at rather than the full height the replay was watched in.
            if (_returnWindowSize.HasValue)
            {
                UIWindowManager.SetPersistentWindowSize(_returnWindowSize.Value);
                _returnWindowSize = null;
                UIWindowManager.ApplyPersistentWindowSize();
            }

            Debug.Log("[ReplayPlayback] Exited replay; live play resumes");
        }

        // ── Injection and tripwires (simulation side) ────────────────────────────────

        /// <summary>Queues every recorded command for <paramref name="tick"/> (and any missed earlier ones). Called before the drain.</summary>
        public void InjectDue(long tick, PlayerCommandService service)
        {
            if (Data == null || service == null)
                return;
            if (tick > TotalTicks)
                BeginRecordingPastEnd();
            var commands = Data.Commands;
            while (_commandCursor < commands.Count && commands[_commandCursor].Tick <= tick)
            {
                var rec = commands[_commandCursor];
                if (rec.Tick < tick)
                    Debug.Warn($"[ReplayPlayback] Command {rec.Command.Type} recorded for tick {rec.Tick} injected late at {tick}");
                service.Inject(in rec.Command);
                _commandCursor++;
            }
        }

        /// <summary>
        /// Ticks past the recorded end have nothing to verify against, so the recorder takes over and
        /// appends them (the few ticks a fast Simulated playback overshoots the end by inside one
        /// rendered frame). Continuing live play from there then leaves a gap-free recording. A scene
        /// restart re-preloads the recorder from the recording, so a backward seek drops these and
        /// re-records them.
        /// </summary>
        private static bool BeginRecordingPastEnd()
        {
            var recorder = ReplayRecorder.Current;
            if (recorder == null)
                return false;
            if (!recorder.IsRecording)
                recorder.IsRecording = true;
            var frames = Frames.FrameRecorder.Current;
            if (frames != null && !frames.IsRecording)
                frames.IsRecording = true;
            return true;
        }

        private void CheckDecision(long tick, ulong hash)
        {
            if (Data == null)
                return;
            if (tick > TotalTicks)
            {
                if (BeginRecordingPastEnd())
                    ReplayRecorder.Current.RecordDecision(tick, hash);
                return;
            }
            Compare(Data.Decisions, ref _decisionCursor, tick, hash, "decision");
        }

        private void CheckStateHash(ReplayHashSample actual)
        {
            if (Data == null)
                return;
            if (actual.Tick > TotalTicks)
            {
                if (BeginRecordingPastEnd())
                    ReplayRecorder.Current.RecordStateHash(in actual);
                return;
            }
            Compare(Data.StateHashes, ref _hashCursor, actual.Tick, actual.Hash, "state", actual);
        }

        private void Compare(System.Collections.Generic.List<ReplayHashSample> samples, ref int cursor, long tick, ulong hash, string kind)
        {
            Compare(samples, ref cursor, tick, hash, kind, default);
        }

        /// <summary>Names the parts of a state sample that differ from the recording (diagnostic string).</summary>
        private static string DescribeStateMismatch(in ReplayHashSample recorded, in ReplayHashSample actual)
        {
            string parts = string.Empty;
            if (recorded.Rng != actual.Rng) parts += " rng";
            if (recorded.Hero != actual.Hero) parts += " hero";
            if (recorded.Party != actual.Party) parts += " party";
            if (recorded.World != actual.World) parts += " world";
            return parts.Length == 0 ? " (combined only)" : parts;
        }

        private void Compare(System.Collections.Generic.List<ReplayHashSample> samples, ref int cursor, long tick, ulong hash, string kind, ReplayHashSample actual)
        {
            // Samples past the recording's end are simply unverified (live play resumed)
            if (tick > TotalTicks)
                return;
            while (cursor < samples.Count && samples[cursor].Tick < tick)
            {
                ReportDivergence(samples[cursor].Tick, kind + " (recorded sample missing in replay)");
                cursor++;
            }
            if (cursor >= samples.Count)
                return;
            var s = samples[cursor];
            if (s.Tick != tick)
            {
                ReportDivergence(tick, kind + " (extra sample in replay)");
                return;
            }
            cursor++;
            if (s.Hash != hash)
            {
                if (kind == "state")
                    ReportDivergence(tick, kind + " mismatch in:" + DescribeStateMismatch(in s, in actual));
                else
                    ReportDivergence(tick, kind);
            }
            else if (kind == "state" && GameConfig.ReplayDivergenceSnapshots && DivergenceTick < 0)
            {
                // Still in sync: this IS the recorded state at this tick, so remember it for the report
                _lastMatchTick = tick;
                _lastMatchDescription = ReplayStateDescriber.Describe();
            }
        }

        private long _lastMatchTick = -1;
        private string _lastMatchDescription;

        private void ReportDivergence(long tick, string kind)
        {
            if (DivergenceTick >= 0)
                return;
            DivergenceTick = tick;
            DivergenceKind = kind;
            DivergenceIsDecision = kind.StartsWith("decision");
            string line = $"[ReplayPlayback] DIVERGENCE at tick {tick} ({tick * GameConfig.SimulationFixedStepSeconds:0.0}s): {kind}";
            Debug.Warn(line);
            WriteDivergenceReport(tick, line);
        }

        /// <summary>
        /// Appends a diagnostic block to replay_divergence.log next to the replay files: what differed,
        /// the recording's identity, the live state at the tick and the commands injected just before it.
        /// </summary>
        private void WriteDivergenceReport(long tick, string headline)
        {
            try
            {
                var files = Core.Services.GetService<ReplayFileService>();
                if (files == null)
                    return;
                var sb = new System.Text.StringBuilder(1024);
                sb.Append(System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append("  ").AppendLine(headline);
                sb.Append("  recording: kind=").Append(Data.Kind).Append(" seed=").Append(Data.MasterSeed)
                  .Append(" totalTicks=").Append(TotalTicks).Append(" build=").Append(Data.BuildId)
                  .Append(" thisBuild=").Append(BuildIdentity.Current).AppendLine();
                sb.Append("  seek state: ").Append(State).Append(" seekTarget=").Append(SeekTarget)
                  .Append(" cosmeticsSkipped=").Append(GameConfig.ReplaySeekSkipsCosmetics).AppendLine();

                if (_hashCursor > 0 && _hashCursor - 1 < Data.StateHashes.Count)
                {
                    var rec = Data.StateHashes[_hashCursor - 1];
                    var now = SimulationStateHasher.Sample(tick);
                    sb.Append("  recorded @").Append(rec.Tick).Append(": rng=").Append(rec.Rng.ToString("X16"))
                      .Append(" hero=").Append(rec.Hero.ToString("X16")).Append(" party=").Append(rec.Party.ToString("X16"))
                      .Append(" world=").Append(rec.World.ToString("X16")).AppendLine();
                    sb.Append("  actual   @").Append(now.Tick).Append(": rng=").Append(now.Rng.ToString("X16"))
                      .Append(" hero=").Append(now.Hero.ToString("X16")).Append(" party=").Append(now.Party.ToString("X16"))
                      .Append(" world=").Append(now.World.ToString("X16")).AppendLine();
                }

                var heroComp = Core.Scene?.FindEntity("hero")?.GetComponent<PitHero.ECS.Components.HeroComponent>();
                if (heroComp?.LinkedHero != null)
                {
                    var t = heroComp.GetCurrentTilePosition();
                    sb.Append("  hero: tile=").Append(t.X).Append(',').Append(t.Y)
                      .Append(" hp=").Append(heroComp.LinkedHero.CurrentHP).Append(" mp=").Append(heroComp.LinkedHero.CurrentMP)
                      .Append(" lvl=").Append(heroComp.LinkedHero.Level).Append(" insidePit=").Append(heroComp.InsidePit)
                      .Append(" stopped=").Append(heroComp.StoppedAdventure).Append(" battle=").Append(HeroStateMachine.IsBattleInProgress)
                      .AppendLine();
                }
                var gameState = Core.Services.GetService<GameStateService>();
                var pause = Core.Services.GetService<PauseService>();
                var pit = Core.Services.GetService<PitWidthManager>();
                sb.Append("  world: funds=").Append(gameState?.Funds ?? -1).Append(" paused=").Append(pause?.IsPaused ?? false)
                  .Append(" pit=").Append(pit?.CurrentPitLevel ?? -1).AppendLine();

                sb.AppendLine("  last commands injected (tick type A B C D L S):");
                int from = _commandCursor - 8; if (from < 0) from = 0;
                for (int i = from; i < _commandCursor && i < Data.Commands.Count; i++)
                {
                    var c = Data.Commands[i];
                    sb.Append("    ").Append(c.Tick).Append(' ').Append(c.Command.Type).Append(' ').Append(c.Command.A).Append(' ')
                      .Append(c.Command.B).Append(' ').Append(c.Command.C).Append(' ').Append(c.Command.D).Append(' ')
                      .Append(c.Command.L).Append(' ').Append(c.Command.S ?? "-").AppendLine();
                }
                if (GameConfig.ReplayDivergenceSnapshots)
                {
                    sb.Append("  last in-sync sample @").Append(_lastMatchTick).AppendLine(" (this is the recorded state at that tick):");
                    sb.Append(_lastMatchDescription ?? "    (none)\n");
                    sb.Append("  drifted state @").Append(tick).AppendLine(":");
                    sb.Append(ReplayStateDescriber.Describe());
                }
                sb.AppendLine();
                System.IO.File.AppendAllText(System.IO.Path.Combine(files.Directory_, "replay_divergence.log"), sb.ToString());
            }
            catch (System.Exception ex)
            {
                Debug.Warn($"[ReplayPlayback] Could not write divergence report: {ex.Message}");
            }
        }
    }
}
