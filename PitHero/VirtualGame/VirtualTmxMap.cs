using System.Collections.Generic;
using PitHero.AI.Interfaces;

namespace PitHero.VirtualGame
{
    /// <summary>
    /// Virtual implementation of IMapData for virtual layer
    /// </summary>
    public class VirtualTmxMap : IMapData
    {
        private readonly Dictionary<string, VirtualTmxLayer> _layers;

        public int Width { get; }
        public int Height { get; }

        public VirtualTmxMap(int width, int height)
        {
            Width = width;
            Height = height;
            _layers = new Dictionary<string, VirtualTmxLayer>();
            
            // Initialize standard layers
            _layers["Base"] = new VirtualTmxLayer(width, height);
            _layers["Collision"] = new VirtualTmxLayer(width, height);
            _layers["FogOfWar"] = new VirtualTmxLayer(width, height);
        }

        public ILayerData GetLayer(string layerName)
        {
            return _layers.TryGetValue(layerName, out var layer) ? layer : null;
        }

        public VirtualTmxLayer GetVirtualLayer(string layerName)
        {
            return _layers.TryGetValue(layerName, out var layer) ? layer : null;
        }
    }
}