using PitHero.AI.Interfaces;

namespace PitHero.VirtualGame
{
    /// <summary>
    /// Virtual implementation of ILayerData for virtual layer
    /// </summary>
    public class VirtualTmxLayer : ILayerData
    {
        private readonly int[,] _tiles;

        public int Width { get; }
        public int Height { get; }

        public VirtualTmxLayer(int width, int height)
        {
            Width = width;
            Height = height;
            _tiles = new int[width, height];
        }

        public ITileData GetTile(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height)
                return null;

            var gid = _tiles[x, y];
            return gid != 0 ? new VirtualTmxTile(gid) : null;
        }

        public void SetTile(int x, int y, int tileIndex)
        {
            if (x >= 0 && y >= 0 && x < Width && y < Height)
            {
                _tiles[x, y] = tileIndex;
            }
        }

        public void RemoveTile(int x, int y)
        {
            if (x >= 0 && y >= 0 && x < Width && y < Height)
            {
                _tiles[x, y] = 0;
            }
        }

        public int GetTileGid(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height)
                return 0;

            return _tiles[x, y];
        }
    }
}