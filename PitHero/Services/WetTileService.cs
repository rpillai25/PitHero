using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Nez.Tiled;
using PitHero.Farming;
using PitHero.Util;

namespace PitHero.Services
{
    /// <summary>
    /// Writes watered-soil bitmask tiles to the tilemap "Detail" layer when farming monsters water
    /// a crop tile. Uses the same bitmask transition logic as tilled tiles but on the Detail layer
    /// at ZeroTile index 154. Clears each morning and on tile-state restore.
    /// </summary>
    public class WetTileService
    {
        private readonly TmxLayer _detailLayer;
        private readonly TileStateService _tileState;
        private readonly System.Func<int, int, bool> _isWet;

        public WetTileService(TmxMap map, TileStateService tileState)
        {
            _detailLayer = map.GetLayer<TmxLayer>("Detail");
            _tileState = tileState;
            _isWet = IsWet;
        }

        private bool IsWet(int x, int y) => _tileState.HasFlag(new Point(x, y), TileStateFlag.Wet);

        /// <summary>
        /// Marks the tile Wet and CropGrowing, writes the Detail-layer bitmask tile, and
        /// recomputes cardinal neighbors.
        /// </summary>
        public void SetWet(Point tile)
        {
            _tileState.SetFlag(tile, TileStateFlag.Wet);
            _tileState.SetFlag(tile, TileStateFlag.CropGrowing);
            SetWetGid(tile.X, tile.Y);
            RecalculateNeighbors(tile);
        }

        /// <summary>
        /// Clears the Wet flag (CropGrowing stays), removes the Detail-layer tile, and
        /// recomputes cardinal neighbors.
        /// </summary>
        public void ClearWet(Point tile)
        {
            _tileState.ClearFlag(tile, TileStateFlag.Wet);
            RemoveDetailTile(tile.X, tile.Y);
            RecalculateNeighbors(tile);
        }

        /// <summary>Clears the Wet flag from all wet tiles. Collect list first to avoid mutation during iteration.</summary>
        public void ClearAllWet()
        {
            var wetTiles = new List<Point>();
            foreach (var tile in _tileState.GetTilesWithFlag(TileStateFlag.Wet))
                wetTiles.Add(tile);
            for (int i = 0; i < wetTiles.Count; i++)
                ClearWet(wetTiles[i]);
        }

        /// <summary>Re-derives all Detail-layer wet tiles from Wet flags after a save is loaded.</summary>
        public void RestoreAllWetTiles()
        {
            var enumerator = _tileState.GetAllStates().GetEnumerator();
            while (enumerator.MoveNext())
            {
                if ((enumerator.Current.Value & TileStateFlag.Wet) != 0)
                    SetWetGid(enumerator.Current.Key.X, enumerator.Current.Key.Y);
            }
            enumerator.Dispose();
        }

        private void SetWetGid(int x, int y)
        {
            if (_detailLayer == null)
                return;
            int gid = TileBitmask.GetTileIndex(x, y, GameConfig.WetZeroTileIndex, _isWet);
            _detailLayer.SetTile(x, y, gid);
        }

        private void RemoveDetailTile(int x, int y)
        {
            if (_detailLayer == null)
                return;
            _detailLayer.RemoveTile(x, y);
        }

        private void RecalculateNeighbors(Point center)
        {
            RecalculateIfWet(center.X, center.Y - 1);
            RecalculateIfWet(center.X - 1, center.Y);
            RecalculateIfWet(center.X + 1, center.Y);
            RecalculateIfWet(center.X, center.Y + 1);
        }

        private void RecalculateIfWet(int x, int y)
        {
            if (_detailLayer == null || x < 0 || y < 0 || x >= _detailLayer.Width || y >= _detailLayer.Height)
                return;
            if (IsWet(x, y))
                SetWetGid(x, y);
            else
                RemoveDetailTile(x, y);
        }
    }
}
