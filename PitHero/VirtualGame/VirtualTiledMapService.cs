using Microsoft.Xna.Framework;
using PitHero.AI.Interfaces;
using System;

namespace PitHero.VirtualGame
{
    /// <summary>
    /// Virtual implementation of ITiledMapService for virtual layer
    /// </summary>
    public class VirtualTiledMapService : ITiledMapService
    {
        private readonly VirtualTmxMap _virtualMap;
        private readonly VirtualWorldState _worldState;

        public IMapData CurrentMap => _virtualMap;

        public VirtualTiledMapService(VirtualWorldState worldState, int width = 60, int height = 25)
        {
            _worldState = worldState;
            _virtualMap = new VirtualTmxMap(width, height);
            
            // Initialize the map with basic patterns that PitWidthManager expects
            InitializeBasicPatterns();
        }

        public void RemoveTile(string layerName, int x, int y)
        {
            var layer = _virtualMap.GetVirtualLayer(layerName);
            layer?.RemoveTile(x, y);
            
            Console.WriteLine($"[VirtualTiledMapService] Removed tile at {layerName}[{x},{y}]");
        }

        public void SetTile(string layerName, int x, int y, int tileIndex)
        {
            var layer = _virtualMap.GetVirtualLayer(layerName);
            layer?.SetTile(x, y, tileIndex);
            
            Console.WriteLine($"[VirtualTiledMapService] Set tile at {layerName}[{x},{y}] = {tileIndex}");
        }

        public bool ClearFogOfWarTile(int tileX, int tileY)
        {
            var fogLayer = _virtualMap.GetVirtualLayer("FogOfWar");
            if (fogLayer != null)
            {
                bool hadTile = fogLayer.GetTile(tileX, tileY) != null;
                if (hadTile)
                {
                    fogLayer.RemoveTile(tileX, tileY);
                    _worldState.ClearFogOfWar(new Point(tileX, tileY), 0); // Single tile clear
                    Console.WriteLine($"[VirtualTiledMapService] Cleared FogOfWar tile at ({tileX},{tileY})");
                    return true;
                }
            }
            return false;
        }

        public bool ClearFogOfWarAroundTile(int centerTileX, int centerTileY)
        {
            var fogLayer = _virtualMap.GetVirtualLayer("FogOfWar");
            if (fogLayer != null)
            {
                bool anyCleared = false;
                // Clear center and 4 cardinal directions
                anyCleared |= ClearFogOfWarTile(centerTileX, centerTileY);
                anyCleared |= ClearFogOfWarTile(centerTileX - 1, centerTileY);
                anyCleared |= ClearFogOfWarTile(centerTileX + 1, centerTileY);
                anyCleared |= ClearFogOfWarTile(centerTileX, centerTileY - 1);
                anyCleared |= ClearFogOfWarTile(centerTileX, centerTileY + 1);
                
                if (anyCleared)
                {
                    Console.WriteLine($"[VirtualTiledMapService] Cleared FogOfWar around ({centerTileX},{centerTileY})");
                }
                return anyCleared;
            }
            return false;
        }

        /// <summary>
        /// Initialize basic tile patterns that PitWidthManager expects to find
        /// </summary>
        private void InitializeBasicPatterns()
        {
            var baseLayer = _virtualMap.GetVirtualLayer("Base");
            var collisionLayer = _virtualMap.GetVirtualLayer("Collision");
            var fogLayer = _virtualMap.GetVirtualLayer("FogOfWar");

            // Initialize patterns from existing map data that PitWidthManager reads from
            // x=11: baseInnerFloor pattern (y=1 to y=11)
            for (int y = 1; y <= 11; y++)
            {
                baseLayer.SetTile(11, y, y == 1 || y == 11 ? 100 : 101); // Mock tile indices
                if (y > 2 && y < 10)
                    collisionLayer.SetTile(11, y, 0); // No collision in middle
            }

            // x=12: baseInnerWall pattern (y=1 to y=11)
            for (int y = 1; y <= 11; y++)
            {
                baseLayer.SetTile(12, y, 102); // Wall tile
                collisionLayer.SetTile(12, y, y > 1 && y < 11 ? 201 : 0);
            }

            // x=13: baseOuterFloor pattern (y=1 to y=11)
            for (int y = 1; y <= 11; y++)
            {
                baseLayer.SetTile(13, y, y == 1 || y == 11 ? 100 : 103); // Floor tile
                collisionLayer.SetTile(13, y, 0); // No collision
            }

            // Ground tile at (19,1)
            baseLayer.SetTile(19, 1, 104);

            // Fog tile pattern
            for (int x = 2; x <= 12; x++)
            {
                for (int y = 3; y <= 9; y++)
                {
                    fogLayer.SetTile(x, y, 301); // Fog tile index
                }
            }

            Console.WriteLine("[VirtualTiledMapService] Initialized basic tile patterns for PitWidthManager");
        }
    }
}