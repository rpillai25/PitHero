using Microsoft.Xna.Framework;

namespace PitHero.AI.Interfaces
{
    /// <summary>
    /// Interface for pit level management and queuing
    /// </summary>
    public interface IPitLevelManager
    {
        /// <summary>
        /// Queue a new pit level for regeneration
        /// </summary>
        void QueueLevel(int level);

        /// <summary>
        /// Check if there's a queued level
        /// </summary>
        bool HasQueuedLevel { get; }

        /// <summary>
        /// Dequeue the next pit level
        /// </summary>
        int? DequeueLevel();

        /// <summary>
        /// Get current pit level
        /// </summary>
        int CurrentLevel { get; }

        /// <summary>
        /// Regenerate pit at specified level
        /// </summary>
        void RegeneratePit(int level, Point? heroPosition = null);
    }
}