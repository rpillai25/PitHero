using Microsoft.Xna.Framework;
using Nez;
using Nez.Tiled;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Helper component for managing FogOfWar clearing
    /// </summary>
    public class FogOfWarHelper : Component
    {
        private TiledMapRenderer _fogOfWarRenderer;
        private TmxMap _tiledMap;

        public override void OnAddedToEntity()
        {
            base.OnAddedToEntity();
            
            // Find the tilemap entity and get the FogOfWar renderer
            var tilemapEntity = Entity.Scene.FindEntity("tilemap");
            if (tilemapEntity != null)
            {
                // Get all TiledMapRenderer components and find the FogOfWar one
                var renderers = tilemapEntity.GetComponents<TiledMapRenderer>();
                foreach (var renderer in renderers)
                {
                    // Check if this renderer is for the FogOfWar layer
                    // This is a simplified check - in practice you might need to track this differently
                    if (renderer != null)
                    {
                        _tiledMap = renderer.TiledMap;
                        // For now, assume the second renderer is the FogOfWar renderer
                        // In a full implementation, you'd want a more robust way to identify it
                        if (_fogOfWarRenderer == null)
                        {
                            _fogOfWarRenderer = renderer;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Clear FogOfWar tile at specific coordinates
        /// </summary>
        /// <param name="tileX">Tile X coordinate</param>
        /// <param name="tileY">Tile Y coordinate</param>
        public void ClearFogOfWarTile(int tileX, int tileY)
        {
            if (_tiledMap == null)
                return;

            // Find the FogOfWar layer
            var fogLayer = _tiledMap.GetLayer<TmxLayer>("FogOfWar");
            if (fogLayer != null && tileX >= 0 && tileY >= 0 && tileX < fogLayer.Width && tileY < fogLayer.Height)
            {
                // For now, just log that we would clear the fog tile
                // In a full implementation, you'd modify the layer's Grid array
                Debug.Log($"Clearing FogOfWar tile at ({tileX}, {tileY})");
                
                // Example of how you might clear a tile (modify the Grid array):
                // var tileIndex = tileY * fogLayer.Width + tileX;
                // if (tileIndex < fogLayer.Grid.Length)
                // {
                //     fogLayer.Grid[tileIndex] = 0; // Set to empty tile
                // }
            }
        }

        /// <summary>
        /// Clear FogOfWar in the 4 cardinal directions around a center tile
        /// </summary>
        /// <param name="centerTileX">Center tile X coordinate</param>
        /// <param name="centerTileY">Center tile Y coordinate</param>
        public void ClearFogOfWarAroundTile(int centerTileX, int centerTileY)
        {
            // Clear tiles in 4 cardinal directions
            ClearFogOfWarTile(centerTileX - 1, centerTileY); // Left
            ClearFogOfWarTile(centerTileX + 1, centerTileY); // Right
            ClearFogOfWarTile(centerTileX, centerTileY - 1); // Up
            ClearFogOfWarTile(centerTileX, centerTileY + 1); // Down
        }
    }
}