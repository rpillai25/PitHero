using Nez.Tiled;
using PitHero.AI.Interfaces;

namespace PitHero.Util
{
    /// <summary>
    /// Wrapper for Nez.Tiled.TmxMap to implement IMapData interface
    /// </summary>
    public class TmxMapWrapper : IMapData
    {
        private readonly TmxMap _tmxMap;

        public TmxMapWrapper(TmxMap tmxMap)
        {
            _tmxMap = tmxMap;
        }

        public int Width => _tmxMap.Width;
        public int Height => _tmxMap.Height;

        public ILayerData GetLayer(string layerName)
        {
            var layer = _tmxMap.GetLayer<TmxLayer>(layerName);
            return layer != null ? new TmxLayerWrapper(layer) : null;
        }
    }
}