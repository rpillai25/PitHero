using Microsoft.Xna.Framework;
using Nez.Tiled;
using PitHero.Farming;
using PitHero.Util;

namespace PitHero.Services
{
    /// <summary>
    /// Writes real tilled tiles to the tilemap "Base" layer when farming monsters complete a till
    /// action. Uses the same bitmask transition logic as the Till Mode overlay, but considers only
    /// Tilled neighbors so real tiles connect to other real tiles.
    /// </summary>
    public class TilledTileService
    {
        private readonly TmxLayer _baseLayer;
        private readonly TileStateService _tileState;
        private readonly System.Func<int, int, bool> _isTilled;

        /// <summary>Fired after a tile becomes Tilled so overlays can remove their grayscale sprite.</summary>
        public System.Action<Point> OnTileTilled;

        public TilledTileService(TmxMap map, TileStateService tileState)
        {
            _baseLayer = map.GetLayer<TmxLayer>("Base");
            _tileState = tileState;
            _isTilled = IsTilled;
        }

        private bool IsTilled(int x, int y)
        {
            return _tileState.HasFlag(new Point(x, y), TileStateFlag.Tilled);
        }

        /// <summary>
        /// Marks the tile Tilled (clearing ReadyToTill), writes the real Detail-layer tile, and
        /// recomputes the bitmask GIDs of already-tilled cardinal neighbors.
        /// </summary>
        public void TillTile(Point tile)
        {
            _tileState.ClearFlag(tile, TileStateFlag.ReadyToTill);
            _tileState.SetFlag(tile, TileStateFlag.Tilled);
            SetTilledGid(tile.X, tile.Y);
            RecalculateNeighbors(tile);
            OnTileTilled?.Invoke(tile);
        }

        /// <summary>Re-derives all real Base-layer tiles from Tilled flags after a save is loaded.</summary>
        public void RestoreAllTilledTiles()
        {
            var enumerator = _tileState.GetAllStates().GetEnumerator();
            while (enumerator.MoveNext())
            {
                if ((enumerator.Current.Value & TileStateFlag.Tilled) != 0)
                    SetTilledGid(enumerator.Current.Key.X, enumerator.Current.Key.Y);
            }
            enumerator.Dispose();
        }

        private void SetTilledGid(int x, int y)
        {
            if (_baseLayer == null)
                return;
            int gid = TileBitmask.GetTileIndex(x, y, GameConfig.TillZerothGid, _isTilled);
            _baseLayer.SetTile(x, y, gid);
        }

        // Only the 4 cardinal neighbors participate in the bitmask, so only they can change.
        private void RecalculateNeighbors(Point center)
        {
            SetIfTilled(center.X, center.Y - 1);
            SetIfTilled(center.X - 1, center.Y);
            SetIfTilled(center.X + 1, center.Y);
            SetIfTilled(center.X, center.Y + 1);
        }

        private void SetIfTilled(int x, int y)
        {
            if (_baseLayer == null || x < 0 || y < 0 || x >= _baseLayer.Width || y >= _baseLayer.Height)
                return;
            if (IsTilled(x, y))
                SetTilledGid(x, y);
        }
    }
}
