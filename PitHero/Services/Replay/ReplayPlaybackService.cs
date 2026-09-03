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

        /// <summary>"decision" or "state" for the first divergence, or null.</summary>
        public string DivergenceKind { get; private set; }

        /// <summary>Simulation tick the replayed scene is at.</summary>
        public long CurrentTick => SimulationClock.CurrentTick;

        /// <summary>Playback speed multiplier.</summary>
        public float Speed => GameConfig.ReplaySpeedSteps[SpeedIndex];

        private int _commandCursor;
        private int _decisionCursor;
        private int _hashCursor;
        private long _startAtTick;
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
        public void Start(ReplayData data, long startAtTick = 0)
        {
            if (data == null || data.StateBlob == null && data.Kind == ReplayKind.Load)
            {
                Debug.Warn("[ReplayPlayback] Cannot start: recording has no start state");
                return;
            }

            Data = data;
            TotalTicks = data.TotalTicks;
            SpeedIndex = 0;
            DivergenceTick = -1;
            DivergenceKind = null;
            _stateAfterSeek = ReplayPlaybackState.Playing;
            _afterSeek = null;

            if (!string.IsNullOrEmpty(data.BuildId) && data.BuildId != BuildIdentity.Current)
                Debug.Warn($"[ReplayPlayback] Recording was made with build {data.BuildId}; this is {BuildIdentity.Current}. Divergence is possible.");

            RestartScene(startAtTick);
        }

        /// <summary>Tears the current scene down and rebuilds it from the recording's start state, then seeks to <paramref name="startAtTick"/>.</summary>
        private void RestartScene(long startAtTick)
        {
            _startAtTick = startAtTick;
            _commandCursor = 0;
            _decisionCursor = 0;
            _hashCursor = 0;
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

            Core.Services.GetService<SettingsUI>()?.EnterReplayMode();

            if (_startAtTick > 0)
                BeginSeek(_startAtTick);
            else
                State = _stateAfterSeek == ReplayPlaybackState.Paused ? ReplayPlaybackState.Paused : ReplayPlaybackState.Playing;
        }

        // ── Per-frame driving (presentation pass) ────────────────────────────────────

        /// <summary>Applies the current state to the engine clock. Called every rendered frame by the scene.</summary>
        public void Update()
        {
            switch (State)
            {
                case ReplayPlaybackState.Playing:
                    Core.SimulationSuspended = false;
                    Core.SimulationSpeed = Speed;
                    Core.MaxStepsPerFrame = GameConfig.ReplayMaxStepsPerFrame;
                    if (CurrentTick >= TotalTicks)
                    {
                        State = ReplayPlaybackState.AtEnd;
                        Core.SimulationSuspended = true;
                    }
                    break;

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

        /// <summary>Pauses or resumes playback (no effect while seeking).</summary>
        public void TogglePause()
        {
            if (State == ReplayPlaybackState.Playing)
                State = ReplayPlaybackState.Paused;
            else if (State == ReplayPlaybackState.Paused || State == ReplayPlaybackState.AtEnd && CurrentTick < TotalTicks)
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
            if (targetTick > TotalTicks) targetTick = TotalTicks;

            if (State == ReplayPlaybackState.Playing || State == ReplayPlaybackState.Paused || State == ReplayPlaybackState.AtEnd)
                _stateAfterSeek = State == ReplayPlaybackState.Paused ? ReplayPlaybackState.Paused : ReplayPlaybackState.Playing;

            if (targetTick < CurrentTick)
            {
                RestartScene(targetTick);
                return;
            }
            BeginSeek(targetTick);
        }

        private void BeginSeek(long targetTick)
        {
            SeekTarget = targetTick;
            _seekStartedAtTick = CurrentTick;
            State = ReplayPlaybackState.Seeking;
            Core.SimulationSuspended = true;
        }

        private void FinishSeek()
        {
            var after = _afterSeek;
            _afterSeek = null;
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

        private void FinishExit()
        {
            State = ReplayPlaybackState.Idle;
            Data = null;

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
                long tick = SimulationClock.CurrentTick;
                commands.ApplyNow(PlayerCommand.Flag(PlayerCommandType.SetManualPause, false), tick);
                commands.ApplyNow(PlayerCommand.Flag(PlayerCommandType.SetFarmModePause, false), tick);
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

        private void CheckDecision(long tick, ulong hash)
        {
            if (Data == null)
                return;
            Compare(Data.Decisions, ref _decisionCursor, tick, hash, "decision");
        }

        private void CheckStateHash(long tick, ulong hash)
        {
            if (Data == null)
                return;
            Compare(Data.StateHashes, ref _hashCursor, tick, hash, "state");
        }

        private void Compare(System.Collections.Generic.List<ReplayHashSample> samples, ref int cursor, long tick, ulong hash, string kind)
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
                ReportDivergence(tick, kind);
        }

        private void ReportDivergence(long tick, string kind)
        {
            if (DivergenceTick >= 0)
                return;
            DivergenceTick = tick;
            DivergenceKind = kind;
            Debug.Warn($"[ReplayPlayback] DIVERGENCE at tick {tick} ({tick * GameConfig.SimulationFixedStepSeconds:0.0}s): {kind}");
        }
    }
}
