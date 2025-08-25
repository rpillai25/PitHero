using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace PitHero.AI.Interfaces
{
    /// <summary>
    /// Unified interface for GOAP actions to interact with game world
    /// Combines world state, hero control, pathfinding, and pit management
    /// </summary>
    public interface IGoapContext
    {
        /// <summary>
        /// World state information
        /// </summary>
        IWorldState WorldState { get; }

        /// <summary>
        /// Hero movement and state control
        /// </summary>
        IHeroController HeroController { get; }

        /// <summary>
        /// Pathfinding operations
        /// </summary>
        IPathfinder Pathfinder { get; }

        /// <summary>
        /// Pit level management
        /// </summary>
        IPitLevelManager PitLevelManager { get; }

        /// <summary>
        /// Get current GOAP world state for planning
        /// </summary>
        Dictionary<string, bool> GetGoapWorldState();

        /// <summary>
        /// Update hero position-based states
        /// </summary>
        void UpdateHeroPositionStates();

        /// <summary>
        /// Log debug information
        /// </summary>
        void LogDebug(string message);

        /// <summary>
        /// Log warning information
        /// </summary>
        void LogWarning(string message);
    }
}