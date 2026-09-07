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

    /// <summary>
    /// Drives a recorded session through the live simulation. Starting a replay restarts the game
    /// scene from the recording's start state and seed, injects the recorded commands on their ticks,
    /// and steps the fixed-step clock (Core.SimulationSpeed / SimulationSuspended / PendingExtraSteps)
    /// according to play, pause and seek requests. Backward seeks restart from tick 0 and fast-forward;
    /// forward seeks fast-forward in wall-budgeted bursts so the scrubber stays responsive. Recorded
    /// tripwire hashes are compared as the replay runs and the first mismatch is reported. Exiting
    /// seeks to the end of the timeline and hands the world back to live play with the recorder
    /// appending again. Global service; <see cref="Current"/> for scene code.
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

        /// <summary>True in any state other than Idle.</summary>
        public bool IsActive => State != ReplayPlaybackState.Idle;

        /// <summary>Length of the recording in ticks.</summary>
        public long TotalTicks { get; private set; }

        /// <summary>Tick the current seek is heading for (valid while Seeking or Starting).</summary>
        public long SeekTarget { get; private set; }

        /// <summary>Index into GameConfig.ReplaySpeedSteps.</summary>
        public int SpeedIndex { get; private set; }

        /// <summary>Tick of the first detected divergence, or -1.</summary>
        public long DivergenceTick { get; private set; } = -1;

        /// <summary>Diagnostic description of the first divergence (log only), or null.</summary>
        public string DivergenceKind { get; private set; }

        /// <summary>True when the first divergence was a hero decision (GOAP plan) rather than a state sample.</summary>
        public bool DivergenceIsDecision { get; private set; }

        /// <summary>Simulation tick the replayed scene is at.</summary>
        public long CurrentTick => SimulationClock.CurrentTick;

        /// <summary>Playback speed multiplier.</summary>
        public float Speed => GameConfig.ReplaySpeedSteps[SpeedIndex];

        /// <summary>
        /// Whether the player may drag the timeline past the recorded session end into a simulated
        /// future: owning the Sphere of Foresight artifact (system-level, shared by every hero).
        /// </summary>
        public static bool FutureSimulationUnlocked => ArtifactService.Current != null && ArtifactService.Current.Owns(PitHero.Artifacts.ArtifactType.SphereOfForesight);

        /// <summary>Whether time travel is unlocked at all: owning the Chronos Timepiece artifact.</summary>
        public static bool TimeTravelUnlocked => ArtifactService.Current != null && ArtifactService.Current.Owns(PitHero.Artifacts.ArtifactType.ChronosTimepiece);

        /// <summary>Last tick of the future-simulation region (session end + the configured allowance).</summary>
        public long FutureEndTick => TotalTicks + GameConfig.ReplayFutureSimulationMaxTicks;

        /// <summary>Furthest tick a seek may target: the session end, or the future cap when unlocked.</summary>
        public long MaxSeekTick => FutureSimulationUnlocked ? FutureEndTick : TotalTicks;

        /// <summary>
        /// True once the player has deliberately placed the playhead past the session end. Playback then
        /// runs on to <see cref="FutureEndTick"/> instead of stopping at the session end. Exit still
        /// returns to the normal session time; only Continue Here commits to the simulated future.
        /// </summary>
        public bool InFuture { get; private set; }

        /// <summary>
        /// Whether Time Travel Here may be used: the Chronos Timepiece must be owned, and the recording
        /// must be the current session or belong to the hero being played right now (a different hero's
        /// world would silently replace the player's). Unknown ids (old recordings) never qualify.
        /// </summary>
        public bool TimeTravelAllowed => TimeTravelUnlocked
            && (_isCurrentSession || Data != null && Data.HeroId != 0 && Data.HeroId == _liveHeroId);

        private int _commandCursor;
        private int _decisionCursor;
        private int _hashCursor;
        private System.Collections.Generic.List<ReplayPauseSpan> _pauseSpans = new System.Collections.Generic.List<ReplayPauseSpan>();
        private bool _analyticsWasEnabled;
        private ReplayData _returnSession; // the live session set aside while a saved replay plays
        private PitHero.ECS.Components.CameraViewState? _returnView; // the player's view when the replay started
        private PitHero.ECS.Components.CameraViewState? _pendingView; // view to apply to the next rebuilt scene
        private readonly System.Diagnostics.Stopwatch _seekStopwatch = new System.Diagnostics.Stopwatch();
        private long _startAtTick;
        private bool _pastEndUnpauseInjected; // per scene instance: the future's pause release has been injected
        private bool _isCurrentSession;
        private int _liveHeroId; // hero of the session that was live when playback started
        private ReplayPlaybackState _stateAfterSeek = ReplayPlaybackState.Playing;
        private Action _afterSeek;
        private long _seekStartedAtTick;

        /// <summary>Creates the service and makes it the global instance.</summary>
        public ReplayPlaybackService()
        {
            Current = this;
        }

        // ── Lifecycle ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Starts playing <paramref name="data"/> from its beginning (or seeks to
        /// <paramref name="startAtTick"/> first). Interrupts the current live session.
        /// </summary>
        public void Start(ReplayData data, bool isCurrentSession, long startAtTick = 0)
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
            _isCurrentSession = isCurrentSession;
            _liveHeroId = Core.Services.GetService<GameStateService>()?.HeroId ?? 0;

            Data = data;
            TotalTicks = data.TotalTicks;
            _pauseSpans = ReplayPauseSpans.Build(data.Commands, data.TotalTicks, GameConfig.ReplayPauseSkipMinTicks);
            SpeedIndex = 0;
            DivergenceTick = -1;
            DivergenceKind = null;
            DivergenceIsDecision = false;
            InFuture = false;
            _stateAfterSeek = ReplayPlaybackState.Playing;
            _afterSeek = null;

            if (!string.IsNullOrEmpty(data.BuildId) && data.BuildId != BuildIdentity.Current)
                Debug.Warn($"[ReplayPlayback] Recording was made with build {data.BuildId}; this is {BuildIdentity.Current}. Divergence is possible.");

            RestartScene(startAtTick);
        }

        /// <summary>Tears the current scene down and rebuilds it from the recording's start state, then seeks to <paramref name="startAtTick"/>.</summary>
        private void RestartScene(long startAtTick)
        {
            Debug.QuietMode = false; // scene rebuild logs are worth keeping; the seek that follows re-arms quiet mode
            Core.CosmeticUpdatesSuspended = false;
            PitHero.Util.SoundEffectManager.Muted = true; // silent through the rebuild and any seek; playback unmutes
            _startAtTick = startAtTick;
            _commandCursor = 0;
            _decisionCursor = 0;
            _hashCursor = 0;
            _pastEndUnpauseInjected = false;
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
            Core.Scene = new ReplayBootScene(MainGameScene.DefaultMapPath);
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

            var commands = PlayerCommandService.Current;
            if (commands != null)
                commands.RejectLiveEnqueues = true;

            ReplayTripwire.PlaybackDecisionCheck = CheckDecision;
            ReplayTripwire.PlaybackStateHashCheck = CheckStateHash;

            // Replayed events already happened once: keep them out of the analytics session log and
            // the event console history
            _analyticsWasEnabled = Services.Analytics.AnalyticsService.Enabled;
            Services.Analytics.AnalyticsService.Enabled = false;
            var events = Core.Services.GetService<GameEventService>();
            if (events != null)
                events.Suppressed = true;

            Core.Services.GetService<SettingsUI>()?.EnterReplayMode();

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

            switch (State)
            {
                case ReplayPlaybackState.Playing:
                {
                    // Natural playback stops at the session end; only a deliberate drag past it
                    // (InFuture) lets it run on to the future cap
                    if (CurrentTick >= PlayStopTick)
                    {
                        State = ReplayPlaybackState.AtEnd;
                        Core.SimulationSuspended = true;
                        break;
                    }
                    // Nothing to watch while the recorded session sat in a menu: skip the stretch
                    long skipTo = InFuture ? CurrentTick : ReplayPauseSpans.FindSkipTarget(_pauseSpans, CurrentTick);
                    if (skipTo > CurrentTick)
                    {
                        _stateAfterSeek = ReplayPlaybackState.Playing;
                        BeginSeek(skipTo > TotalTicks ? TotalTicks : skipTo);
                        break;
                    }
                    Core.SimulationSuspended = false;
                    Core.SimulationSpeed = Speed;
                    Core.MaxStepsPerFrame = GameConfig.ReplayMaxStepsPerFrame;
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
                        Core.ExtraStepWallBudgetSeconds = GameConfig.ReplaySeekWallBudgetSeconds;
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

        /// <summary>Tick at which natural playback stops: the session end, or the future cap once the player is in the future.</summary>
        private long PlayStopTick => InFuture ? FutureEndTick : TotalTicks;

        /// <summary>Pauses or resumes playback (no effect while seeking).</summary>
        public void TogglePause()
        {
            if (State == ReplayPlaybackState.Playing)
                State = ReplayPlaybackState.Paused;
            else if (State == ReplayPlaybackState.Paused || State == ReplayPlaybackState.AtEnd && CurrentTick < PlayStopTick)
                State = ReplayPlaybackState.Playing;
        }

        /// <summary>Cycles to the next playback speed.</summary>
        public void CycleSpeed()
        {
            SpeedIndex = (SpeedIndex + 1) % GameConfig.ReplaySpeedSteps.Length;
        }

        /// <summary>Moves playback to <paramref name="targetTick"/>: forward by fast-forwarding, backward by restarting from tick 0.</summary>
        public void Seek(long targetTick)
        {
            if (!IsActive)
                return;
            if (targetTick < 0) targetTick = 0;
            if (targetTick > MaxSeekTick) targetTick = MaxSeekTick;

            if (State == ReplayPlaybackState.Playing || State == ReplayPlaybackState.Paused || State == ReplayPlaybackState.AtEnd)
                _stateAfterSeek = State == ReplayPlaybackState.Paused ? ReplayPlaybackState.Paused : ReplayPlaybackState.Playing;

            // Past the session end the world keeps simulating with no player input: the future is a
            // pure function of the recording, so a backward seek inside it rebuilds the same future
            bool enteringFuture = targetTick > TotalTicks;
            if (enteringFuture && !InFuture)
                Debug.Log($"[ReplayPlayback] Entering future simulation (session end {TotalTicks}, cap {FutureEndTick})");
            InFuture = enteringFuture;

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
            SeekTarget = targetTick;
            _seekStartedAtTick = CurrentTick;
            State = ReplayPlaybackState.Seeking;
            Core.SimulationSuspended = true;
            // Thousands of simulated steps per second: keep Debug.WriteLine off the hot path and
            // skip purely visual per-step work nobody will see
            Debug.QuietMode = true;
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
            if (after != null)
            {
                after();
                return;
            }
            State = CurrentTick >= PlayStopTick ? ReplayPlaybackState.AtEnd : _stateAfterSeek;
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

            // The simulated future is only ever watched: exiting from it goes back to the normal
            // session time (the world is rebuilt to the session end, or to the set-aside live session)
            if (InFuture)
            {
                if (State == ReplayPlaybackState.Seeking || State == ReplayPlaybackState.Starting)
                {
                    _afterSeek = ReturnFromFuture;
                    return;
                }
                ReturnFromFuture();
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

        /// <summary>
        /// Leaves the simulated future without keeping it: a saved replay returns to the set-aside
        /// live session; the current session is rebuilt back to its recorded end.
        /// </summary>
        private void ReturnFromFuture()
        {
            InFuture = false;
            if (_returnSession != null && !ReferenceEquals(_returnSession, Data))
            {
                ReturnToLiveSession();
                return;
            }
            _pauseSpans.Clear();
            _afterSeek = FinishExit;
            _pendingView = CaptureView();
            Debug.Log($"[ReplayPlayback] Leaving the simulated future; returning to the session end at tick {TotalTicks}");
            RestartScene(TotalTicks);
        }

        /// <summary>Restarts the scene from the live session's recording and seeks to its last tick, then hands control back.</summary>
        private void ReturnToLiveSession()
        {
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
        /// Time travel: abandon the set-aside live session and continue playing from the replay's
        /// CURRENT position. Everything recorded after this tick is discarded so the recording stays
        /// a straight line for future replays.
        /// </summary>
        public void ContinueFromHere()
        {
            if (!IsActive || State == ReplayPlaybackState.Starting || State == ReplayPlaybackState.Seeking)
                return;
            _returnSession = null;
            long tick = CurrentTick;
            // In the future the recorder has been appending past the recorded end, so the recording
            // already runs up to this tick and the truncation is a no-op
            ReplayRecorder.Current?.TruncateAfter(tick);
            TotalTicks = tick;
            InFuture = false;
            Debug.Log($"[ReplayPlayback] Continuing live play from replay tick {tick}");
            FinishExit();
        }

        private void FinishExit()
        {
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
            Debug.Log("[ReplayPlayback] Exited replay; live play resumes");
        }

        // ── Injection and tripwires (simulation side) ────────────────────────────────

        /// <summary>Queues every recorded command for <paramref name="tick"/> (and any missed earlier ones). Called before the drain.</summary>
        public void InjectDue(long tick, PlayerCommandService service)
        {
            if (Data == null || service == null)
                return;
            if (tick > TotalTicks)
            {
                BeginRecordingPastEnd();
                if (!_pastEndUnpauseInjected)
                {
                    // A saved replay is snapshotted from inside the Settings window, so its recording
                    // ends with the menu pause still applied and nothing past the end releases it.
                    // Release both pause flags on the first future tick; they are recorded like any
                    // other past-end tick so a continued session stays consistent.
                    _pastEndUnpauseInjected = true;
                    service.Inject(PlayerCommand.Flag(PlayerCommandType.SetManualPause, false));
                    service.Inject(PlayerCommand.Flag(PlayerCommandType.SetFarmModePause, false));
                }
            }
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
        /// appends them (future simulation, or the few ticks a fast playback overshoots the end by).
        /// Continuing live play from there then leaves a gap-free recording. A scene restart re-preloads
        /// the recorder from the recording, so a backward seek drops these and re-records them.
        /// </summary>
        private static bool BeginRecordingPastEnd()
        {
            var recorder = ReplayRecorder.Current;
            if (recorder == null)
                return false;
            if (!recorder.IsRecording)
                recorder.IsRecording = true;
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
        }

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
