using Microsoft.Xna.Framework;

namespace PitHero.AI.Interfaces
{
    /// <summary>
    /// Interface for pit width management operations
    /// </summary>
    public interface IPitWidthManager
    {
        /// <summary>
        /// Current pit level
        /// </summary>
        int CurrentPitLevel { get; }

        /// <summary>
        /// Current pit right edge X coordinate
        /// </summary>
        int CurrentPitRightEdge { get; }

        /// <summary>
        /// Current pit width in tiles
        /// </summary>
        int CurrentPitRectWidthTiles { get; }

        /// <summary>
        /// Current pit center X tile coordinate
        /// </summary>
        int CurrentPitCenterTileX { get; }

        /// <summary>
        /// Initialize the pit width manager
        /// </summary>
        void Initialize();

        /// <summary>
        /// Set the pit level and regenerate width accordingly
        /// </summary>
        void SetPitLevel(int newLevel);

        /// <summary>
        /// Regenerate pit width based on current level
        /// </summary>
        void RegeneratePitWidth();

        /// <summary>
        /// Get current pit candidate targets for MoveToPitAction
        /// </summary>
        Point[] GetCurrentPitCandidateTargets();

        /// <summary>
        /// Calculate current pit bounds in world coordinates
        /// </summary>
        Rectangle CalculateCurrentPitWorldBounds();
    }
}