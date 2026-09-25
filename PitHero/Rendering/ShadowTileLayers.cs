using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Nez;
using Nez.Tiled;
using PitHero.Services.Replay.Frames;

namespace PitHero.Rendering
{
    /// <summary>
    /// Copies of the three mutable tile layers (Base, Detail, FogOfWar) rebuilt from the recorded tile
    /// keyframes and events (design §3.2), plus a reference to the live Top layer, which never changes.
    /// The copies share the map (tileset lookups) but own their grids, so the live layers are never
    /// touched. Drawn with the same <see cref="TiledRendering.RenderLayer"/> call the live
    /// <c>TiledMapRenderer</c> uses.
    /// </summary>
    public sealed class ShadowTileLayers
    {
        public const int LayerCount = 3;
        private static readonly string[] LayerNames = { "Base", "Detail", "FogOfWar" };
        private const string TopLayerName = "Top";

        private readonly ShadowTileGrid _grid = new ShadowTileGrid();
        private readonly TmxLayer[] _layers = new TmxLayer[LayerCount];
        private readonly int[] _copiedVersion = new int[LayerCount];
        private TmxLayer _top;
        private int _gridVersion = -1;

        /// <summary>True once <see cref="Bind"/> found the map's layers.</summary>
        public bool IsBound { get; private set; }
        /// <summary>The live Top layer (constant), or null.</summary>
        public TmxLayer Top => _top;
        /// <summary>The tick the copies currently show, or -1.</summary>
        public long AppliedTick => _grid.AppliedTick;

        /// <summary>Creates the copies from the map's live layers. Safe to call again with the same map.</summary>
        public void Bind(TmxMap map)
        {
            if (map == null)
                return;
            for (int i = 0; i < LayerCount; i++)
            {
                var live = map.GetLayer<TmxLayer>(LayerNames[i]);
                if (live == null || live.Grid == null)
                {
                    _layers[i] = null;
                    continue;
                }
                var copy = new TmxLayer
                {
                    Map = live.Map,
                    Name = live.Name,
                    Opacity = live.Opacity,
                    Visible = true,
                    OffsetX = live.OffsetX,
                    OffsetY = live.OffsetY,
                    ParallaxFactorX = live.ParallaxFactorX,
                    ParallaxFactorY = live.ParallaxFactorY,
                    Properties = live.Properties,
                    Width = live.Width,
                    Height = live.Height,
                    Grid = (uint[])live.Grid.Clone(),
                    Tiles = new Dictionary<uint, TmxLayerTile>(64),
                };
                _layers[i] = copy;
                _copiedVersion[i] = -1;
            }
            _top = map.GetLayer<TmxLayer>(TopLayerName);
            _grid.Invalidate();
            IsBound = _layers[0] != null || _layers[1] != null || _layers[2] != null;
        }

        /// <summary>Brings the copies to <paramref name="tick"/>; false when the store has no keyframe for it (copies keep their last state).</summary>
        public bool SyncTo(FrameStore store, long tick)
        {
            if (!IsBound || !_grid.SyncTo(store, tick))
                return false;
            if (_grid.Version == _gridVersion)
                return true;
            _gridVersion = _grid.Version;
            for (int i = 0; i < LayerCount; i++)
                CopyLayer(i);
            return true;
        }

        private void CopyLayer(int i)
        {
            var layer = _layers[i];
            var gids = _grid.Gids(i);
            if (layer == null || gids == null)
                return;
            int count = _grid.Width(i) * _grid.Height(i);
            if (count > layer.Grid.Length)
                count = layer.Grid.Length;
            var grid = layer.Grid;
            var tiles = layer.Tiles;
            for (int g = 0; g < count; g++)
            {
                uint gid = gids[g];
                if (grid[g] == gid)
                    continue;
                grid[g] = gid;
                if (gid != 0 && !tiles.ContainsKey(gid))
                    tiles.Add(gid, new TmxLayerTile(layer.Map, gid));
            }
        }

        /// <summary>A copied layer by index (0 Base, 1 Detail, 2 FogOfWar), or null.</summary>
        public TmxLayer GetLayer(int index) => index >= 0 && index < LayerCount ? _layers[index] : null;

        /// <summary>Draws a copied layer (or the Top layer for <paramref name="index"/> = -1) with the batcher's current material.</summary>
        public void Draw(int index, Batcher batcher, Camera camera, Vector2 position)
        {
            var layer = index < 0 ? _top : GetLayer(index);
            if (layer == null)
                return;
            TiledRendering.RenderLayer(layer, batcher, position, Vector2.One, 0f, camera.Bounds);
        }

        /// <summary>Forgets the applied tick so the next sync rebuilds from a keyframe (after a stream truncation).</summary>
        public void Invalidate()
        {
            _grid.Invalidate();
            _gridVersion = -1;
        }
    }
}
