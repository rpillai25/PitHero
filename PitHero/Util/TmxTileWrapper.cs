using Nez.Tiled;
using PitHero.AI.Interfaces;

namespace PitHero.Util
{
    /// <summary>
    /// Wrapper for Nez.Tiled.TmxLayerTile to implement ITileData interface
    /// </summary>
    public class TmxTileWrapper : ITileData
    {
        private readonly TmxLayerTile _tmxTile;

        public TmxTileWrapper(TmxLayerTile tmxTile)
        {
            _tmxTile = tmxTile;
        }

        public int Gid => _tmxTile.Gid;
    }
}