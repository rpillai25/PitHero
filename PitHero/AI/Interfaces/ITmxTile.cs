namespace PitHero.AI.Interfaces
{
    /// <summary>
    /// Interface for TMX tile to abstract Nez.Tiled dependencies
    /// </summary>
    public interface ITileData
    {
        /// <summary>
        /// Global tile ID
        /// </summary>
        int Gid { get; }
    }
}