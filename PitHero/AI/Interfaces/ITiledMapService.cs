using PitHero.ECS.Components;

namespace PitHero.AI.Interfaces
{
    /// <summary>
    /// Interface for tiled map operations to abstract TMX dependencies
    /// </summary>
    public interface ITiledMapService
    {
        /// <summary>
        /// Current map interface
        /// </summary>
        IMapData CurrentMap { get; }

        /// <summary>
        /// Remove a tile from the specified layer at the given coordinates
        /// </summary>
        void RemoveTile(string layerName, int x, int y);

        /// <summary>
        /// Set a tile on the specified layer at the given coordinates
        /// </summary>
        void SetTile(string layerName, int x, int y, int tileIndex);

        /// <summary>
        /// Clear fog of war tile at specific coordinates
        /// </summary>
        /// <returns>True if fog was actually cleared</returns>
        bool ClearFogOfWarTile(int tileX, int tileY);

        /// <summary>
        /// Clear fog of war around a tile
        /// </summary>
        /// <param name="centerTileX">Center tile X coordinate</param>
        /// <param name="centerTileY">Center tile Y coordinate</param>
        /// <param name="radius">Radius for clearing fog (default 1)</param>
        /// <returns>True if any fog was actually cleared</returns>
        bool ClearFogOfWarAroundTile(int centerTileX, int centerTileY, HeroComponent heroComponent);
    }
}