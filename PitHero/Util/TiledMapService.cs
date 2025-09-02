using Nez;
using Nez.Tiled;
using Microsoft.Xna.Framework;
using Nez.AI.Pathfinding;
using PitHero.ECS.Components;
using PitHero.AI.Interfaces;

namespace PitHero.Util
{
    /// <summary>
    /// Use Core.Services.GetService<TiledMapService>() to get the current TiledMapService from anywhere
    /// </summary>
    public class TiledMapService : ITiledMapService
    {
        public TmxMap CurrentMap;
        
        // Interface implementation
        IMapData ITiledMapService.CurrentMap => CurrentMap != null ? new TmxMapWrapper(CurrentMap) : null;

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
            layer.SetTile(x, y, tileIndex);
        }

        private bool IsOutOfBounds(int x, int y)
        {
            return (y * CurrentMap.Width + x < 0 || y * CurrentMap.Width + x >= CurrentMap.Width * CurrentMap.Height);
        }

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
        /// Clear FogOfWar in the 4 cardinal directions around a center tile and diagonals if those diagonal tiles are obstacles
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

            // Clear diagonal fog only if that diagonal tile is an obstacle
            // Find the hero to access its pathfinding graph
            var heroEntity = Core.Scene?.FindEntity("hero");
            if (heroEntity != null)
            {
                var heroComponent = heroEntity.GetComponent<HeroComponent>();
                if (heroComponent != null && heroComponent.IsPathfindingInitialized)
                {
                    var astarGraph = heroComponent.PathfindingGraph;
                    
                    // Upper-left
                    TryClearDiagonalIfObstacle(astarGraph, centerTileX - 1, centerTileY - 1);
                    // Upper-right
                    TryClearDiagonalIfObstacle(astarGraph, centerTileX + 1, centerTileY - 1);
                    // Lower-left
                    TryClearDiagonalIfObstacle(astarGraph, centerTileX - 1, centerTileY + 1);
                    // Lower-right
                    TryClearDiagonalIfObstacle(astarGraph, centerTileX + 1, centerTileY + 1);
                }
            }
        }

        /// <summary>
        /// Clears the fog at (x,y) if the A* graph marks that tile as a wall/obstacle
        /// </summary>
        private void TryClearDiagonalIfObstacle(AstarGridGraph astarGraph, int x, int y)
        {
            // AstarGridGraph.Walls contains both Collision tiles and spawned obstacles.
            // Treasures/monsters are NOT added to Walls, so they won’t trigger diagonal clearing.
            if (astarGraph.Walls.Contains(new Point(x, y)))
            {
                ClearFogOfWarTile(x, y);
            }
        }

        /// <summary>
        /// Check if a tile has fog of war (returns true if fog exists, false if cleared)
        /// </summary>
        public bool HasFogOfWar(int tileX, int tileY)
        {
            if (CurrentMap == null)
                return false;

            var fogLayer = CurrentMap.GetLayer<TmxLayer>("FogOfWar");
            if (fogLayer == null)
                return false;

            if (tileX < 0 || tileY < 0 || tileX >= fogLayer.Width || tileY >= fogLayer.Height)
                return false;

            var existingTile = fogLayer.GetTile(tileX, tileY);
            return existingTile != null; // Fog exists if tile is not null
        }
    }
}
