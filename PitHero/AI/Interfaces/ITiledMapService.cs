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
        void ClearFogOfWarTile(int tileX, int tileY);

        /// <summary>
        /// Clear fog of war around a tile in cardinal directions
        /// </summary>
        void ClearFogOfWarAroundTile(int centerTileX, int centerTileY);
    }
}