namespace PitHero.AI.Interfaces
{
    /// <summary>
    /// Interface for TMX map operations to abstract Nez.Tiled dependencies
    /// </summary>
    public interface IMapData
    {
        /// <summary>
        /// Map width in tiles
        /// </summary>
        int Width { get; }

        /// <summary>
        /// Map height in tiles
        /// </summary>
        int Height { get; }

        /// <summary>
        /// Get a layer by name
        /// </summary>
        ILayerData GetLayer(string layerName);
    }
}