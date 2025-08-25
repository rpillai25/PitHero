using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace PitHero.AI.Interfaces
{
    /// <summary>
    /// Interface for pathfinding operations
    /// </summary>
    public interface IPathfinder
    {
        /// <summary>
        /// Calculate path from start to end position
        /// </summary>
        List<Point> CalculatePath(Point start, Point end);

        /// <summary>
        /// Check if a tile is passable
        /// </summary>
        bool IsPassable(Point tilePosition);

        /// <summary>
        /// Check if pathfinding is initialized and ready
        /// </summary>
        bool IsInitialized { get; }
    }
}