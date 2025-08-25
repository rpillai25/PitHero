using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace PitHero.AI.Interfaces
{
    /// <summary>
    /// Interface for providing world state information to GOAP actions
    /// </summary>
    public interface IWorldState
    {
        /// <summary>
        /// Get current hero position in tile coordinates
        /// </summary>
        Point HeroPosition { get; }

        /// <summary>
        /// Get pit bounds in tile coordinates
        /// </summary>
        Rectangle PitBounds { get; }

        /// <summary>
        /// Get current pit level
        /// </summary>
        int PitLevel { get; }

        /// <summary>
        /// Get wizard orb position if available
        /// </summary>
        Point? WizardOrbPosition { get; }

        /// <summary>
        /// Check if wizard orb is activated
        /// </summary>
        bool IsWizardOrbActivated { get; }

        /// <summary>
        /// Check if a tile has fog of war
        /// </summary>
        bool HasFogOfWar(Point tilePosition);

        /// <summary>
        /// Check if a tile is passable for pathfinding
        /// </summary>
        bool IsPassable(Point tilePosition);

        /// <summary>
        /// Clear fog of war around a tile
        /// </summary>
        void ClearFogOfWar(Point tilePosition, int radius);

        /// <summary>
        /// Activate wizard orb
        /// </summary>
        void ActivateWizardOrb();

        /// <summary>
        /// Check if map exploration is complete (no fog in pit)
        /// </summary>
        bool IsMapExplored { get; }

        /// <summary>
        /// Check if wizard orb has been found (fog cleared around it)
        /// </summary>
        bool IsWizardOrbFound { get; }
    }
}