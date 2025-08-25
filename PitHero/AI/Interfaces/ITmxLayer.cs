namespace PitHero.AI.Interfaces
{
    /// <summary>
    /// Interface for TMX layer operations to abstract Nez.Tiled dependencies
    /// </summary>
    public interface ILayerData
    {
        /// <summary>
        /// Layer width in tiles
        /// </summary>
        int Width { get; }

        /// <summary>
        /// Layer height in tiles
        /// </summary>
        int Height { get; }

        /// <summary>
        /// Get tile at coordinates
        /// </summary>
        ITileData GetTile(int x, int y);

        /// <summary>
        /// Set tile at coordinates
        /// </summary>
        void SetTile(int x, int y, int tileIndex);

        /// <summary>
        /// Remove tile at coordinates
        /// </summary>
        void RemoveTile(int x, int y);
    }
}