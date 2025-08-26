using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace PitHero.VirtualGame
{
    /// <summary>
    /// Interface for virtual world representation that can simulate game state without graphics
    /// </summary>
    public interface IVirtualWorld
    {
        /// <summary>
        /// Get the size of the world in tiles
        /// </summary>
        Point WorldSizeTiles { get; }

        /// <summary>
        /// Get hero position in tiles
        /// </summary>
        Point HeroPosition { get; }

        /// <summary>
        /// Get wizard orb position in tiles (null if not spawned)
        /// </summary>
        Point? WizardOrbPosition { get; }

        /// <summary>
        /// Get pit bounds in tiles
        /// </summary>
        Rectangle PitBounds { get; }

        /// <summary>
        /// Get pit level
        /// </summary>
        int PitLevel { get; }

        /// <summary>
        /// Check if a tile has fog of war
        /// </summary>
        bool HasFogOfWar(Point tilePos);

        /// <summary>
        /// Check if a tile is a collision tile
        /// </summary>
        bool IsCollisionTile(Point tilePos);

        /// <summary>
        /// Check if wizard orb is activated (purple)
        /// </summary>
        bool IsWizardOrbActivated { get; }

        /// <summary>
        /// Get all entity positions by type
        /// </summary>
        Dictionary<string, List<Point>> GetEntityPositions();

        /// <summary>
        /// Move hero to a tile position
        /// </summary>
        void MoveHeroTo(Point tilePos);

        /// <summary>
        /// Clear fog of war around a position
        /// </summary>
        void ClearFogOfWar(Point centerPos, int radius = 2);

        /// <summary>
        /// Activate wizard orb
        /// </summary>
        void ActivateWizardOrb();

        /// <summary>
        /// Regenerate pit at specified level
        /// </summary>
        void RegeneratePit(int level);

        /// <summary>
        /// Get a visual representation of the world for console display
        /// </summary>
        string GetVisualRepresentation();
    }
}