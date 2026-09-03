namespace PitHero.Services.Replay
{
    /// <summary>
    /// Hand-off slot for starting a MainGameScene as a replay: the playback service stores the master
    /// seed and the recording here before swapping scenes, and MainGameScene.Begin consumes it before
    /// generating any world content. Mirrors the SaveLoadService.PendingLoadData pattern.
    /// </summary>
    public sealed class ReplaySessionBootstrap
    {
        private static ReplaySessionBootstrap _pending;

        /// <summary>Master seed the new session must be seeded with.</summary>
        public int MasterSeed { get; }

        /// <summary>The recording being played back (null for a plain seeded start).</summary>
        public ReplayData Data { get; }

        /// <summary>
        /// For NewGame-kind replays: the global-services snapshot (vault contents, defeated monsters)
        /// captured when the original session began, restored before the new-game path runs.
        /// </summary>
        public SaveData NewGameGlobals { get; set; }

        /// <summary>Creates a bootstrap for the given seed and (optional) recording.</summary>
        public ReplaySessionBootstrap(int masterSeed, ReplayData data = null)
        {
            MasterSeed = masterSeed;
            Data = data;
        }

        /// <summary>Queues a bootstrap for the next MainGameScene.Begin.</summary>
        public static void SetPending(ReplaySessionBootstrap bootstrap)
        {
            _pending = bootstrap;
        }

        /// <summary>True when a bootstrap is waiting to be consumed.</summary>
        public static bool HasPending => _pending != null;

        /// <summary>Returns and clears the pending bootstrap, or null.</summary>
        public static ReplaySessionBootstrap Consume()
        {
            var b = _pending;
            _pending = null;
            return b;
        }
    }
}
