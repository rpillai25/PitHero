using System.Collections.Generic;
using Microsoft.Xna.Framework;
using PitHero.Farming;

namespace PitHero.Services
{
    /// <summary>Global service storing per-tile state flags (ReadyToTill, Tilled, Wet, etc.).</summary>
    public class TileStateService
    {
        private readonly Dictionary<Point, TileStateFlag> _tileStates = new Dictionary<Point, TileStateFlag>();

        /// <summary>Fired when a tile's ReadyToTill bit transitions from unset to set.</summary>
        public System.Action<Point> OnReadyToTillSet;

        /// <summary>Fired when a tile's ReadyToTill bit transitions from set to unset.</summary>
        public System.Action<Point> OnReadyToTillCleared;

        /// <summary>Adds the given flag(s) to the tile, merging with any existing flags.</summary>
        public void SetFlag(Point tile, TileStateFlag flag)
        {
            if (_tileStates.TryGetValue(tile, out var existing))
                _tileStates[tile] = existing | flag;
            else
            {
                existing = TileStateFlag.None;
                _tileStates[tile] = flag;
            }

            if ((flag & TileStateFlag.ReadyToTill) != 0 && (existing & TileStateFlag.ReadyToTill) == 0)
                OnReadyToTillSet?.Invoke(tile);
        }

        /// <summary>Removes the given flag(s) from the tile. Removes the entry entirely when no flags remain.</summary>
        public void ClearFlag(Point tile, TileStateFlag flag)
        {
            if (_tileStates.TryGetValue(tile, out var existing))
            {
                var result = existing & ~flag;
                if (result == TileStateFlag.None)
                    _tileStates.Remove(tile);
                else
                    _tileStates[tile] = result;

                if ((existing & TileStateFlag.ReadyToTill) != 0 && (result & TileStateFlag.ReadyToTill) == 0)
                    OnReadyToTillCleared?.Invoke(tile);
            }
        }

        /// <summary>Returns true if the tile has ALL of the specified flags set.</summary>
        public bool HasFlag(Point tile, TileStateFlag flag)
        {
            return _tileStates.TryGetValue(tile, out var state) && (state & flag) != 0;
        }

        /// <summary>Returns the full flag state for the tile, or None if the tile has no recorded state.</summary>
        public TileStateFlag GetState(Point tile)
        {
            return _tileStates.TryGetValue(tile, out var state) ? state : TileStateFlag.None;
        }

        /// <summary>Enumerates all tile positions that have at least one of the specified flags set.</summary>
        public IEnumerable<Point> GetTilesWithFlag(TileStateFlag flag)
        {
            foreach (var kvp in _tileStates)
                if ((kvp.Value & flag) != 0)
                    yield return kvp.Key;
        }

        /// <summary>Returns a snapshot of all entries for serialization.</summary>
        public IEnumerable<KeyValuePair<Point, TileStateFlag>> GetAllStates() => _tileStates;

        /// <summary>Removes all tile state. Called when quitting to title so stale data cannot bleed into a new save.</summary>
        public void Clear() => _tileStates.Clear();
    }
}
