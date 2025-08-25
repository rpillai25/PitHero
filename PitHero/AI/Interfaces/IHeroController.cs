using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace PitHero.AI.Interfaces
{
    /// <summary>
    /// Interface for hero movement and state management
    /// </summary>
    public interface IHeroController
    {
        /// <summary>
        /// Get current hero position in tile coordinates
        /// </summary>
        Point CurrentTilePosition { get; }

        /// <summary>
        /// Get current hero world position
        /// </summary>
        Vector2 CurrentWorldPosition { get; }

        /// <summary>
        /// Check if hero is currently moving
        /// </summary>
        bool IsMoving { get; }

        /// <summary>
        /// Start moving in a specific direction
        /// </summary>
        bool StartMoving(Direction direction);

        /// <summary>
        /// Move hero to a specific tile position
        /// </summary>
        void MoveTo(Point tilePosition);

        /// <summary>
        /// Move hero to a specific world position
        /// </summary>
        void MoveTo(Vector2 worldPosition);

        /// <summary>
        /// Set movement path for pathfinding
        /// </summary>
        void SetMovementPath(List<Point> path);

        /// <summary>
        /// Check if hero has reached a target position
        /// </summary>
        bool HasReachedTarget(Point targetTile);

        /// <summary>
        /// Get GOAP state flags
        /// </summary>
        bool PitInitialized { get; set; }
        bool AdjacentToPitBoundaryFromOutside { get; set; }
        bool AdjacentToPitBoundaryFromInside { get; set; }
        bool InsidePit { get; set; }
        bool ActivatedWizardOrb { get; set; }
        bool MovingToInsidePitEdge { get; set; }
        bool ReadyToJumpOutOfPit { get; set; }
        bool MovingToPitGenPoint { get; set; }
        Direction? PitApproachDirection { get; set; }
    }
}