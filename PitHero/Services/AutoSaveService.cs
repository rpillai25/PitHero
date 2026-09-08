using Nez;
using System;
using System.Threading.Tasks;

namespace PitHero.Services
{
    /// <summary>
    /// Periodically writes the running session to the dedicated autosave file (issue #409).
    /// The state snapshot (<see cref="SaveLoadService.GatherCurrentState"/>) is gathered on the
    /// calling (main) thread; only the serialize-and-write step runs on a worker. Completion is
    /// observed by polling from the main thread, so no callback ever runs off-thread.
    /// Presentation-only: the timer is wall-clock and the service never touches simulation state.
    /// </summary>
    public sealed class AutoSaveService
    {
        private readonly Func<SaveData> _gather;
        private readonly Action<SaveData> _write;
        private readonly Action<SaveData> _onSaved;

        private float _elapsed;
        private bool _isSaving;
        private Task _worker;
        private SaveData _inFlightData;
        private Exception _error;

        /// <summary>Wall seconds between autosaves.</summary>
        public float IntervalSeconds { get; set; }

        /// <summary>Wall seconds until the next autosave fires, assuming saving stays allowed.</summary>
        public float SecondsUntilNextSave => Math.Max(0f, IntervalSeconds - _elapsed);

        /// <summary>
        /// True while a write is in flight. The getter polls the worker first so a save that finished
        /// while no scene was ticking this service (title screen) is observed as complete.
        /// </summary>
        public bool IsSaving
        {
            get
            {
                PollCompletion();
                return _isSaving;
            }
        }

        /// <summary>Raised on the polling (main) thread when a write threw.</summary>
        public event Action<Exception> Failed;

        /// <summary>
        /// Creates the service. <paramref name="gather"/> runs on the caller's thread when a save starts,
        /// <paramref name="write"/> runs on a worker thread and must be thread-agnostic (no Core, no logging),
        /// <paramref name="onSaved"/> runs on the polling thread after a successful write.
        /// </summary>
        public AutoSaveService(Func<SaveData> gather, Action<SaveData> write, Action<SaveData> onSaved, float intervalSeconds)
        {
            _gather = gather;
            _write = write;
            _onSaved = onSaved;
            IntervalSeconds = intervalSeconds;
        }

        /// <summary>Restarts the countdown (session start, manual save).</summary>
        public void ResetTimer()
        {
            _elapsed = 0f;
        }

        /// <summary>
        /// Advances the wall-clock countdown. Call once per rendered frame from the presentation pass.
        /// While <paramref name="canSave"/> is false the countdown is held at zero so no save fires the
        /// instant a transitional state (death, ceremony, intro, replay) ends.
        /// </summary>
        public void Tick(float wallDeltaSeconds, bool canSave)
        {
            PollCompletion();

            if (!canSave)
            {
                _elapsed = 0f;
                return;
            }

            _elapsed += wallDeltaSeconds;
            if (_elapsed >= IntervalSeconds && !_isSaving)
            {
                TryStartAutoSave();
                _elapsed = 0f;
            }
        }

        /// <summary>
        /// Gathers the current state on the calling thread and starts the background write.
        /// Returns false when a write is already in flight or the gather produced nothing.
        /// </summary>
        public bool TryStartAutoSave()
        {
            PollCompletion();
            if (_isSaving)
                return false;

            var data = _gather();
            if (data == null)
                return false;

            _inFlightData = data;
            _error = null;
            _isSaving = true;
            var write = _write;
            _worker = Task.Run(() =>
            {
                try
                {
                    write(data);
                }
                catch (Exception ex)
                {
                    // Recorded for the main thread; never log from the worker
                    _error = ex;
                }
            });
            return true;
        }

        /// <summary>
        /// Observes a finished worker on the calling (main) thread: clears the in-flight flag, then
        /// reports the failure or publishes the saved snapshot.
        /// </summary>
        public void PollCompletion()
        {
            if (!_isSaving || _worker == null || !_worker.IsCompleted)
                return;

            var data = _inFlightData;
            var error = _error ?? _worker.Exception;
            _worker = null;
            _inFlightData = null;
            _error = null;
            _isSaving = false;

            if (error != null)
            {
                Debug.Warn("[AutoSaveService] Autosave failed: " + error.Message);
                Failed?.Invoke(error);
                return;
            }

            _onSaved?.Invoke(data);
        }

        /// <summary>
        /// Blocks until any in-flight write has finished and its completion has been observed.
        /// Used before reading the autosave file (load) and on process exit.
        /// </summary>
        public void WaitForCompletion()
        {
            var worker = _worker;
            if (worker != null)
            {
                try
                {
                    worker.Wait();
                }
                catch (AggregateException)
                {
                    // Surfaced through PollCompletion below
                }
            }
            PollCompletion();
        }
    }
}
