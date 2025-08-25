using Nez.Tiled;
using PitHero.AI.Interfaces;

namespace PitHero.Util
{
    /// <summary>
    /// Wrapper for Nez.Tiled.TmxLayer to implement ILayerData interface
    /// </summary>
    public class TmxLayerWrapper : ILayerData
    {
        private readonly TmxLayer _tmxLayer;

        public TmxLayerWrapper(TmxLayer tmxLayer)
        {
            _tmxLayer = tmxLayer;
        }

        public int Width => _tmxLayer.Width;
        public int Height => _tmxLayer.Height;

        public ITileData GetTile(int x, int y)
        {
            var tile = _tmxLayer.GetTile(x, y);
            return tile != null ? new TmxTileWrapper(tile) : null;
        }

        public void SetTile(int x, int y, int tileIndex)
        {
            _tmxLayer.SetTile(x, y, tileIndex);
        }

        public void RemoveTile(int x, int y)
        {
            _tmxLayer.RemoveTile(x, y);
        }
    }
}