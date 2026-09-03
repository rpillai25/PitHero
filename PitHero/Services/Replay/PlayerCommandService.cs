using System;
using Nez;

namespace PitHero.Services.Replay
{
    /// <summary>
    /// The single doorway from player input into the simulation. UI code enqueues
    /// <see cref="PlayerCommand"/>s during the presentation pass; <see cref="Drain"/> applies them at
    /// the end of the simulation tick (always the same point, live or replay) and raises
    /// <see cref="OnCommandApplied"/> so the recorder can log them with their tick. During replay
    /// playback live enqueues are rejected and the playback service injects the recorded commands
    /// for the current tick instead. Scene-scoped; <see cref="Current"/> mirrors SimulationClock.
    /// </summary>
    public sealed class PlayerCommandService
    {
        private const int InitialCapacity = 64;

        private PlayerCommand[] _queue = new PlayerCommand[InitialCapacity];
        private int _count;
        private PlayerCommand[] _draining = new PlayerCommand[InitialCapacity];

        /// <summary>The scene's service, or null outside a game session.</summary>
        public static PlayerCommandService Current { get; private set; }

        /// <summary>When true (replay playback) live enqueues are dropped with a warning.</summary>
        public bool RejectLiveEnqueues { get; set; }

        /// <summary>True while a command handler is running. Lets guarded setters bypass the queue.</summary>
        public bool IsApplying { get; private set; }

        /// <summary>Raised after each command is applied: (tick, command).</summary>
        public event Action<long, PlayerCommand> OnCommandApplied;

        /// <summary>Number of commands waiting for the next drain.</summary>
        public int PendingCount => _count;

        /// <summary>Creates the service and makes it the current instance.</summary>
        public PlayerCommandService()
        {
            Current = this;
        }

        /// <summary>Clears the static instance when the owning scene unloads.</summary>
        public void Detach()
        {
            if (Current == this)
                Current = null;
        }

        /// <summary>
        /// Queues a command for the end of the current/next simulation tick. Returns false when live
        /// input is being rejected (replay playback) or the command type is None.
        /// </summary>
        public bool Enqueue(in PlayerCommand command)
        {
            if (command.Type == PlayerCommandType.None)
                return false;
            if (RejectLiveEnqueues)
            {
                Debug.Warn($"[PlayerCommandService] Rejected live command {command.Type} during replay playback");
                return false;
            }
            Push(command);
            return true;
        }

        /// <summary>Queues a recorded command during replay playback (bypasses the live-input rejection).</summary>
        public void Inject(in PlayerCommand command)
        {
            if (command.Type == PlayerCommandType.None)
                return;
            Push(command);
        }

        private void Push(in PlayerCommand command)
        {
            if (_count == _queue.Length)
                Array.Resize(ref _queue, _queue.Length * 2);
            _queue[_count++] = command;
        }

        /// <summary>
        /// Applies every queued command in order and reports each with <paramref name="tick"/>.
        /// Commands enqueued by handlers while draining run on the next drain, keeping the per-tick
        /// sequence identical between live play and replay.
        /// </summary>
        public void Drain(long tick)
        {
            if (_count == 0)
                return;

            if (_draining.Length < _count)
                _draining = new PlayerCommand[_queue.Length];
            int n = _count;
            Array.Copy(_queue, _draining, n);
            Array.Clear(_queue, 0, n);
            _count = 0;

            for (int i = 0; i < n; i++)
            {
                var cmd = _draining[i];
                IsApplying = true;
                try
                {
                    PlayerCommandHandlers.Apply(in cmd);
                }
                catch (Exception ex)
                {
                    Debug.Error($"[PlayerCommandService] {cmd.Type} threw: {ex}");
                }
                finally
                {
                    IsApplying = false;
                }
                OnCommandApplied?.Invoke(tick, cmd);
            }
            Array.Clear(_draining, 0, n);
        }

        /// <summary>
        /// Applies a command immediately (outside the drain) and records it at <paramref name="tick"/>.
        /// For the rare moments when the normal drain will never run before the scene is torn down,
        /// e.g. releasing the settings pause right before a replay restarts the scene: the recording
        /// must still contain the release or a later replay would freeze at that point.
        /// </summary>
        public void ApplyNow(in PlayerCommand command, long tick)
        {
            if (command.Type == PlayerCommandType.None)
                return;
            IsApplying = true;
            try
            {
                PlayerCommandHandlers.Apply(in command);
            }
            finally
            {
                IsApplying = false;
            }
            OnCommandApplied?.Invoke(tick, command);
        }

        /// <summary>Convenience: enqueue on the current service if one exists. Returns false otherwise.</summary>
        public static bool TryEnqueue(in PlayerCommand command)
        {
            var svc = Current;
            return svc != null && svc.Enqueue(in command);
        }

        /// <summary>
        /// True when a caller should apply a mutation directly instead of enqueuing: no service exists
        /// (title/creation scenes, headless tests) or we are already inside a command handler.
        /// </summary>
        public static bool ShouldApplyDirectly => Current == null || Current.IsApplying;

        /// <summary>
        /// The one call UI code makes: queues the command for the next tick during a session, or
        /// applies it immediately when no session/service exists. Returns false if it was rejected.
        /// </summary>
        public static bool Dispatch(in PlayerCommand command)
        {
            if (ShouldApplyDirectly)
            {
                PlayerCommandHandlers.Apply(in command);
                return true;
            }
            return Current.Enqueue(in command);
        }
    }
}
