namespace PitHero
{
    /// <summary>
    /// Service for queuing pit level changes for regeneration
    /// </summary>
    public class PitLevelQueueService
    {
        private int? _queuedLevel;

        /// <summary>
        /// Gets whether a pit level is currently queued for regeneration
        /// </summary>
        public bool HasQueuedLevel => _queuedLevel.HasValue;

        /// <summary>
        /// Queue a pit level for regeneration
        /// </summary>
        /// <param name="level">The pit level to queue</param>
        public void QueueLevel(int level)
        {
            _queuedLevel = level;
        }

        /// <summary>
        /// Dequeue the next pit level for regeneration
        /// </summary>
        /// <returns>The queued pit level, or null if no level is queued</returns>
        public int? DequeueLevel()
        {
            var level = _queuedLevel;
            _queuedLevel = null;
            return level;
        }
    }
}