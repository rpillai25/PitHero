using Nez;
using Nez.Tiled;

namespace PitHero.Util
{
    /// <summary>
    /// Use Core.Services.GetService<TiledMapService>() to get the current TiledMapService from anywhere
    /// </summary>
    public class TiledMapService
    {
        public TmxMap CurrentMap;

        public TiledMapService(TmxMap tmxMap)
        {
            CurrentMap = tmxMap;
        }

        public void RemoveTile(string layerName, int x, int y)
        {
            if (IsOutOfBounds(x, y))
            {
                Debug.Log("WARNING: RemoveTile out of bounds!!!");
                return;
            }

            var layer = (TmxLayer)CurrentMap.GetLayer(layerName);
            if (layer == null)
            {
                Debug.Log("WARNING: Layer null for RemoveTile!!!");
                return;
            }
            layer.RemoveTile(x, y);
        }

        public void SetTile(string layerName, int x, int y, int tileIndex)
        {
            if (IsOutOfBounds(x, y))
            {
                Debug.Log("WARNING: SetTile out of bounds!!!");
                return;
            }

            var layer = (TmxLayer)CurrentMap.GetLayer(layerName);
            if (layer == null)
            {
                Debug.Log("WARNING: Layer null for SetTile!!!");
                return;
            }
            //var newTile = new TmxLayerTile(tiledMap, tileIndex, x, y);
            //layer.SetTile(newTile);
            layer.SetTile(x, y, tileIndex);
        }

        private bool IsOutOfBounds(int x, int y)
        {
            return (y * CurrentMap.Width + x < 0 || y * CurrentMap.Width + x >= CurrentMap.Width * CurrentMap.Height);
        }

        // Pseudocode:
        // 1. If CurrentMap is null, return (nothing to clear).
        // 2. Get FogOfWar layer from CurrentMap (typed as TmxLayer). If null, return.
        // 3. Bounds check tileX/tileY against layer Width/Height. If OOB, return.
        // 4. Log that we are checking this tile.
        // 5. Retrieve tile via fogLayer.GetTile(tileX, tileY). If null (already cleared), return.
        // 6. Call RemoveTile helper (uses CurrentMap) to remove tile.
        // 7. Log removal.
        public void ClearFogOfWarTile(int tileX, int tileY)
        {
            if (CurrentMap == null)
                return;

            var fogLayer = CurrentMap.GetLayer<TmxLayer>("FogOfWar");
            if (fogLayer == null)
            {
                Debug.Log("WARNING: FogOfWar layer not found for ClearFogOfWarTile");
                return;
            }

            if (tileX < 0 || tileY < 0 || tileX >= fogLayer.Width || tileY >= fogLayer.Height)
            {
                Debug.Log($"WARNING: ClearFogOfWarTile out of bounds tileX={tileX} tileY={tileY} width={fogLayer.Width} height={fogLayer.Height}");
                return;
            }

            Debug.Log($"Checking to clear FogOfWar tile at ({tileX}, {tileY})");

            var existingTile = fogLayer.GetTile(tileX, tileY);
            if (existingTile == null)
            {
                Debug.Log($"FogOfWar tile already clear at ({tileX}, {tileY})");
                return;
            }

            RemoveTile("FogOfWar", tileX, tileY);
            Debug.Log($"FogOfWar tile removed at {tileX}, {tileY}");
        }

        /// <summary>
        /// Clear FogOfWar in the 4 cardinal directions around a center tile
        /// </summary>
        /// <param name="centerTileX">Center tile X coordinate</param>
        /// <param name="centerTileY">Center tile Y coordinate</param>
        public void ClearFogOfWarAroundTile(int centerTileX, int centerTileY)
        {
            // Clear tile at center position
            ClearFogOfWarTile(centerTileX, centerTileY);
            // Clear tiles in 4 cardinal directions
            ClearFogOfWarTile(centerTileX - 1, centerTileY); // Left
            ClearFogOfWarTile(centerTileX + 1, centerTileY); // Right
            ClearFogOfWarTile(centerTileX, centerTileY - 1); // Up
            ClearFogOfWarTile(centerTileX, centerTileY + 1); // Down
        }
    }
}
