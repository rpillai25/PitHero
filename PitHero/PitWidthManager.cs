using Microsoft.Xna.Framework;
using Nez;
using PitHero.AI.Interfaces;
using PitHero.ECS.Scenes;
using PitHero.Util;
using System;
using System.Collections.Generic;

namespace PitHero
{
    /// <summary>
    /// Manages dynamic pit width generation based on pit level
    /// Every 10 levels, the pit width extends by 2 tiles to the right
    /// </summary>
    public class PitWidthManager : IPitWidthManager
    {
        // Dictionary to store tile patterns for pit expansion
        private Dictionary<int, int> _baseOuterFloor;
        private Dictionary<int, int> _collisionOuterFloor;
        private Dictionary<int, int> _baseInnerWall;
        private Dictionary<int, int> _collisionInnerWall;
        private Dictionary<int, int> _baseInnerFloor;
        private Dictionary<int, int> _collisionInnerFloor;
        private int _fogOfWarIndex;
        private int _groundTileIndex; // Ground tile recorded from (19,1)
        private int _regenTileIndex; // Regen tile recorded from "map center" (33, 6)

        // Track current pit state
        private int _currentPitLevel = 1;
        private int _currentPitRightEdge; // The rightmost x coordinate of the current pit
        private bool _isInitialized = false;
        
        // Interface dependencies
        private ITiledMapService _tiledMapService;

        public int CurrentPitLevel => _currentPitLevel;
        public int CurrentPitRightEdge => _currentPitRightEdge;

        public PitWidthManager(ITiledMapService tiledMapService = null)
        {
            try
            {
                _tiledMapService = tiledMapService ?? Core.Services?.GetService<TiledMapService>();
            }
            catch
            {
                // Core.Services may not be available during unit testing
                _tiledMapService = tiledMapService;
            }
        }

        /// <summary>
        /// Gets the current pit width in tiles (dynamic), or GameConfig default if not initialized
        /// </summary>
        public int CurrentPitRectWidthTiles => _isInitialized
            ? (_currentPitRightEdge - GameConfig.PitRectX + 1)
            : GameConfig.PitRectWidth;

        /// <summary>
        /// Gets the current pit center X tile (dynamic), or GameConfig default if not initialized
        /// </summary>
        public int CurrentPitCenterTileX
        {
            get
            {
                if (!_isInitialized)
                    return GameConfig.PitCenterTileX;

                // Interior spans from (left = PitRectX + 1) to (right = _currentPitRightEdge - 2)
                int leftInteriorX = GameConfig.PitRectX + 1;
                int rightInteriorX = _currentPitRightEdge - 2;
                return leftInteriorX + ((rightInteriorX - leftInteriorX) / 2);
            }
        }


        /// <summary>
        /// Initialize the tile pattern dictionaries from the map
        /// This should be called once when the map is loaded, before any pit manipulation
        /// </summary>
        public void Initialize()
        {
            if (_isInitialized)
            {
                Debug.Log("[PitWidthManager] Already initialized, skipping");
                return;
            }

            if (_tiledMapService?.CurrentMap == null)
            {
                Debug.Error("[PitWidthManager] Cannot initialize - TiledMapService or CurrentMap is null");
                return;
            }

            _baseOuterFloor = new Dictionary<int, int>();
            _collisionOuterFloor = new Dictionary<int, int>();
            _baseInnerWall = new Dictionary<int, int>();
            _collisionInnerWall = new Dictionary<int, int>();
            _baseInnerFloor = new Dictionary<int, int>();
            _collisionInnerFloor = new Dictionary<int, int>();

            // Get layer references
            var baseLayer = _tiledMapService.CurrentMap.GetLayer("Base");
            var collisionLayer = _tiledMapService.CurrentMap.GetLayer("Collision");
            var fogOfWarLayer = _tiledMapService.CurrentMap.GetLayer("FogOfWar");

            if (baseLayer == null || collisionLayer == null || fogOfWarLayer == null)
            {
                Debug.Error("[PitWidthManager] Required layers (Base, Collision, FogOfWar) not found in map");
                return;
            }

            // Record FogOfWar tile index at (2,3)
            var fogTile = fogOfWarLayer.GetTile(2, 3);
            _fogOfWarIndex = fogTile?.Gid ?? 0;
            Debug.Log($"[PitWidthManager] Recorded FogOfWar index: {_fogOfWarIndex}");

            // Record ground tile at (19,1)
            var groundTile = baseLayer.GetTile(19, 1);
            _groundTileIndex = groundTile?.Gid ?? 0;
            Debug.Log($"[PitWidthManager] Recorded ground tile index: {_groundTileIndex}");

            var regenTile = baseLayer.GetTile(GameConfig.MapCenterTileX, GameConfig.MapCenterTileY);
            _regenTileIndex = regenTile?.Gid ?? 16; // Default to 16 if not found
            Debug.Log($"[PitWidthManager] Recorded regen tile index: {_regenTileIndex}");

            // Initialize baseOuterFloor and collisionOuterFloor from coordinates (13,1) to (13,11)
            InitializeTilePattern(_baseOuterFloor, baseLayer, 13, 1, 11, "baseOuterFloor");
            InitializeTilePattern(_collisionOuterFloor, collisionLayer, 13, 1, 11, "collisionOuterFloor");

            // Initialize baseInnerWall and collisionInnerWall from coordinates (12,1) to (12,11)
            InitializeTilePattern(_baseInnerWall, baseLayer, 12, 1, 11, "baseInnerWall");
            InitializeTilePattern(_collisionInnerWall, collisionLayer, 12, 1, 11, "collisionInnerWall");

            // Initialize baseInnerFloor and collisionInnerFloor from coordinates (11,1) to (11,11)
            InitializeTilePattern(_baseInnerFloor, baseLayer, 11, 1, 11, "baseInnerFloor");
            InitializeTilePattern(_collisionInnerFloor, collisionLayer, 11, 1, 11, "collisionInnerFloor");
            
            // CRITICAL FIX: Ensure inner floor collision pattern has no collision in explorable area
            // This prevents movement blocking when pit expands, as collision tiles in y=3-9 would
            // be copied to extended columns and block hero movement
            for (int y = 3; y <= 9; y++)
            {
                _collisionInnerFloor[y] = 0; // Force explorable area to be passable
            }
            Debug.Log("[PitWidthManager] Fixed collision pattern: cleared y=3-9 for inner floor extension");

            // Set initial pit right edge (default pit goes from x=1 to x=12, so rightmost is 12)
            _currentPitRightEdge = GameConfig.PitRectX + GameConfig.PitRectWidth;

            _isInitialized = true;
            Debug.Log($"[PitWidthManager] Initialization complete. Current pit right edge: {_currentPitRightEdge}");
        }

        public void ReinitRightEdge()
        {
            _currentPitRightEdge = GameConfig.PitRectX + GameConfig.PitRectWidth;
        }

        /// <summary>
        /// Helper method to initialize a tile pattern dictionary from a specific x column
        /// </summary>
        private void InitializeTilePattern(Dictionary<int, int> dictionary, ILayerData layer, int x, int startY, int endY, string patternName)
        {
            int nonZeroCount = 0;
            for (int y = startY; y <= endY; y++)
            {
                var tile = layer.GetTile(x, y);
                int tileIndex = tile?.Gid ?? 0;
                dictionary[y] = tileIndex;
                Debug.Log($"[PitWidthManager] {patternName}[{y}] = {tileIndex}");
                if (tileIndex != 0) nonZeroCount++;
            }
            Debug.Log($"[PitWidthManager] {patternName} total: {dictionary.Count} tiles, {nonZeroCount} non-zero");
        }

        /// <summary>
        /// Set the pit level and regenerate the pit width accordingly
        /// </summary>
        public void SetPitLevel(int newLevel)
        {
            if (!_isInitialized)
            {
                Debug.Error("[PitWidthManager] Cannot set pit level - manager not initialized");
                return;
            }

            if (newLevel < 1)
            {
                Debug.Warn($"[PitWidthManager] Invalid pit level {newLevel}, minimum is 1");
                return;
            }

            var previousLevel = _currentPitLevel;
            var previousRightEdge = _currentPitRightEdge;
            
            Debug.Log($"[PitWidthManager] Setting pit level from {_currentPitLevel} to {newLevel}");
            _currentPitLevel = newLevel;
            
            // Calculate new right edge
            int initialRightEdge = GameConfig.PitRectX + GameConfig.PitRectWidth;
            // Cap expansion at level 100 - pit stops expanding beyond level 100
            int expansionLevel = Math.Min(_currentPitLevel, 100);
            int innerFloorTilesToExtend = ((int)(expansionLevel / 10)) * 2;
            int newRightEdge = initialRightEdge + innerFloorTilesToExtend + (innerFloorTilesToExtend > 0 ? 2 : 0); // +2 for inner wall and outer floor
            
            // If sizing down, clear tiles first
            if (newRightEdge < previousRightEdge)
            {
                ClearTilesFromXToEnd(newRightEdge);
            }
            
            RegeneratePitWidth();
        }

        /// <summary>
        /// Clear tiles from a given x coordinate to x=33 to clean up when sizing down
        /// </summary>
        private void ClearTilesFromXToEnd(int startX)
        {
        if (!_isInitialized)
            {
                Debug.Error("[PitWidthManager] Cannot clear tiles - manager not initialized");
                return;
            }

            if (_tiledMapService == null)
            {
                Debug.Error("[PitWidthManager] TiledMapService not available for clearing tiles");
                return;
            }

            Debug.Log($"[PitWidthManager] Clearing tiles from x={startX} to x=33, y=1 to y=11");

            for (int x = startX - 1; x <= 33; x++)
            {
                for (int y = 1; y <= 11; y++)
                {
                    // Set Base layer to ground tile
                    if (_groundTileIndex != 0)
                    {
                        _tiledMapService.SetTile("Base", x, y, _groundTileIndex);
                    }

                    // Remove Collision layer tiles
                    _tiledMapService.RemoveTile("Collision", x, y);

                    // Remove FogOfWar layer tiles
                    _tiledMapService.RemoveTile("FogOfWar", x, y);
                }
            }

            //Clear fog of war from inner wall and outer floor columns to the left of startX
            for (int x = startX - 3; x <= startX; x++)
            {
                for (int y = 1; y <= 11; y++)
                {
                    // Remove FogOfWar layer tiles
                    _tiledMapService.RemoveTile("FogOfWar", x, y);
                }
            }

            Debug.Log($"[PitWidthManager] Cleared tiles from x={startX} to x=33");
        }

        /// <summary>
        /// Regenerate the pit width based on the current pit level
        /// </summary>
        public void RegeneratePitWidth()
        {
            if (!_isInitialized)
            {
                Debug.Error("[PitWidthManager] Cannot regenerate pit width - manager not initialized");
                return;
            }

            // Calculate how many inner floor tiles to extend
            int innerFloorTilesToExtend = ((int)(_currentPitLevel / 10)) * 2;
            Debug.Log($"[PitWidthManager] Level {_currentPitLevel}: extending pit by {innerFloorTilesToExtend} inner floor tiles");

            if (innerFloorTilesToExtend <= 0)
            {
                Debug.Log("[PitWidthManager] No extension needed for current level");
                RegeneratePitContent();
                return;
            }

            if (_tiledMapService == null)
            {
                Debug.Error("[PitWidthManager] TiledMapService not available");
                return;
            }

            // Start extending from the original pit boundary 
            int currentX = 12; // Start at x=12 for inner floor extension
            int lastXCoordinate = currentX;

            // Loop to extend inner floor tiles
            for (int i = 0; i < innerFloorTilesToExtend; i++)
            {
                ExtendColumn(_tiledMapService, currentX, _baseInnerFloor, _collisionInnerFloor, "inner floor");
                lastXCoordinate = currentX;
                currentX++;
            }

            // Add inner wall column after all inner floor columns
            if (innerFloorTilesToExtend > 0)
            {
                currentX = lastXCoordinate + 1;
                ExtendColumn(_tiledMapService, currentX, _baseInnerWall, _collisionInnerWall, "inner wall");
                lastXCoordinate = currentX;

                // Add outer floor column after inner wall
                currentX = lastXCoordinate + 1;
                ExtendColumn(_tiledMapService, currentX, _baseOuterFloor, _collisionOuterFloor, "outer floor");
                lastXCoordinate = currentX;
            }

            // Update the current pit right edge
            _currentPitRightEdge = lastXCoordinate;
            Debug.Log($"[PitWidthManager] Pit extension complete. New right edge: {_currentPitRightEdge}");

            //Set the regen tile again
            _tiledMapService.SetTile("Base", GameConfig.MapCenterTileX, GameConfig.MapCenterTileY, _regenTileIndex);

            // Notify the scene to update pit collider bounds
            UpdatePitColliderBounds();
            
            // Regenerate pit content for the new size
            RegeneratePitContent();
        }

        /// <summary>
        /// Notify the main game scene to update pit collider bounds
        /// </summary>
        private void UpdatePitColliderBounds()
        {
            // Find the MainGameScene and call its update method
            var scene = Core.Scene as MainGameScene;
            if (scene != null)
            {
                scene.UpdatePitColliderBounds();
            }
            else
            {
                Debug.Warn("[PitWidthManager] Could not find MainGameScene to update pit collider bounds");
            }
        }

        /// <summary>
        /// Rebuild tilemap physics colliders from the current Collision layer tiles
        /// </summary>
        private void RebuildTilemapColliders()
        {
            var scene = Core.Scene;
            if (scene == null)
            {
                Debug.Warn("[PitWidthManager] No active scene found when rebuilding tilemap colliders");
                return;
            }

            var tilemapEntity = scene.FindEntity("tilemap");
            if (tilemapEntity == null)
            {
                Debug.Warn("[PitWidthManager] Tilemap entity not found when rebuilding colliders");
                return;
            }

            var renderers = tilemapEntity.GetComponents<TiledMapRenderer>();
            if (renderers == null || renderers.Count == 0)
            {
                Debug.Warn("[PitWidthManager] No TiledMapRenderer components found on tilemap entity");
                return;
            }

            int rebuiltCount = 0;
            for (int i = 0; i < renderers.Count; i++)
            {
                var r = renderers[i];
                if (r == null || r.CollisionLayer == null)
                    continue; // Only rebuild for the renderer that manages CollisionLayer colliders

                Debug.Log("[PitWidthManager] Rebuilding tilemap colliders from Collision layer");

                // Before rebuild: log current rectangle count
                var beforeRects = r.CollisionLayer.GetCollisionRectangles();
                Debug.Log($"[PitWidthManager] Collision rectangles BEFORE rebuild: {beforeRects.Count}");

                r.RemoveColliders();
                r.AddColliders();
                rebuiltCount++;

                // After rebuild, validate collision at expanded inner floor columns
                var collisionLayer = r.CollisionLayer;
                int tileSize = GameConfig.TileSize;

                // We expect x=12 and x=13 (new inner floors at level 10) to be passable for y=3..9
                for (int x = 12; x <= 13; x++)
                {
                    for (int y = 3; y <= 9; y++)
                    {
                        int gid = 0;
                        var t = collisionLayer.GetTile(x, y);
                        gid = t != null ? t.Gid : 0;

                        // Count how many collision rectangles intersect this tile area
                        var tileRect = new Rectangle(x * tileSize, y * tileSize, tileSize, tileSize);
                        int intersectCount = 0;
                        var rects = collisionLayer.GetCollisionRectangles();
                        for (int ri = 0; ri < rects.Count; ri++)
                        {
                            if (rects[ri].Intersects(tileRect))
                                intersectCount++;
                        }

                        Debug.Log($"[PitWidthManager] Post-rebuild check: Collision[{x},{y}] gid={gid}, intersectingRects={intersectCount}");
                    }
                }

                // Also log rectangle count after rebuild
                var afterRects = r.CollisionLayer.GetCollisionRectangles();
                Debug.Log($"[PitWidthManager] Collision rectangles AFTER rebuild: {afterRects.Count}");
            }

            if (rebuiltCount == 0)
            {
                Debug.Warn("[PitWidthManager] No colliders rebuilt (no renderer with Collision layer)");
            }
        }

        /// <summary>
        /// Regenerate pit content after pit width changes
        /// </summary>
        private void RegeneratePitContent()
        {
            Debug.Log("[PitWidthManager] Regenerating pit content after width change");
            
            // Regenerate FogOfWar for the entire current pit area
            RegenerateFogOfWar();

            // Rebuild physics colliders so movement matches updated Collision layer
            RebuildTilemapColliders();
            
            // Find the MainGameScene and regenerate pit content
            var scene = Core.Scene as MainGameScene;
            if (scene != null)
            {
                var pitGenerator = new PitGenerator(scene);
                pitGenerator.RegenerateForCurrentLevel();
                Debug.Log("[PitWidthManager] Pit content regeneration complete");
            }
            else
            {
                Debug.Warn("[PitWidthManager] Could not find MainGameScene to regenerate pit content");
            }
        }

        /// <summary>
        /// Regenerate FogOfWar tiles for the entire current pit area
        /// </summary>
        private void RegenerateFogOfWar()
        {
            if (!_isInitialized)
            {
                Debug.Error("[PitWidthManager] Cannot regenerate FogOfWar - manager not initialized");
                return;
            }

            if (_tiledMapService == null)
            {
                Debug.Error("[PitWidthManager] TiledMapService not available for regenerating FogOfWar");
                return;
            }

            if (_fogOfWarIndex == 0)
            {
                Debug.Warn("[PitWidthManager] No FogOfWar tile index recorded, skipping FogOfWar regeneration");
                return;
            }

            Debug.Log($"[PitWidthManager] Regenerating FogOfWar for entire pit area from x=2 to x={_currentPitRightEdge}, y=3 to y=9");

            // Add FogOfWar tiles for the entire current pit area (y=3 to y=9 for the explorable pit interior)
            for (int x = 2; x <= _currentPitRightEdge - 2; x++) // Start from x=2 (first explorable column)
            {
                for (int y = 3; y <= 9; y++) // y=3 to y=9 is the explorable pit interior
                {
                    _tiledMapService.SetTile("FogOfWar", x, y, _fogOfWarIndex);
                }
            }

            Debug.Log($"[PitWidthManager] FogOfWar regeneration complete");
        }

        /// <summary>
        /// Extend a single column using the provided tile patterns
        /// </summary>
        private void ExtendColumn(ITiledMapService tiledMapService, int x, Dictionary<int, int> basePattern, Dictionary<int, int> collisionPattern, string columnType)
        {
            Debug.Log($"[PitWidthManager] Extending {columnType} column at x={x}");

            // Set tiles from y=1 to y=11
            for (int y = 1; y <= 11; y++)
            {
                // Set Base layer tile
                if (basePattern.TryGetValue(y, out int baseTileIndex) && baseTileIndex != 0)
                {
                    tiledMapService.SetTile("Base", x, y, baseTileIndex);
                }

                // Set Collision layer tile (or remove if 0/null)
                if (collisionPattern.TryGetValue(y, out int collisionTileIndex))
                {
                    if (collisionTileIndex != 0)
                    {
                        tiledMapService.SetTile("Collision", x, y, collisionTileIndex);
                    }
                    else
                    {
                        tiledMapService.RemoveTile("Collision", x, y);
                    }
                }
                else
                {
                    tiledMapService.RemoveTile("Collision", x, y);
                }

                // Set FogOfWar layer tile - only for y=3 to y=9 and not for baseInnerWall or baseOuterFloor columns
                bool shouldSetFogOfWar = (y >= 3 && y <= 9) && 
                                        !columnType.Contains("inner wall") && 
                                        !columnType.Contains("outer floor");
                
                if (shouldSetFogOfWar && _fogOfWarIndex != 0)
                {
                    tiledMapService.SetTile("FogOfWar", x, y, _fogOfWarIndex);
                }
            }
        }

        /// <summary>
        /// Get the current pit candidate targets for MoveToPitAction based on current pit width
        /// </summary>
        public Point[] GetCurrentPitCandidateTargets()
        {
            if (!_isInitialized)
            {
                Debug.Warn("[PitWidthManager] Manager not initialized, returning default targets");
                // Return default targets as fallback
                return new Point[]
                {
                    new Point(13, 3),
                    new Point(13, 4),
                    new Point(13, 5),
                    new Point(13, 6),
                    new Point(13, 7),
                    new Point(13, 8),
                    new Point(13, 9)
                };
            }

            // Calculate the rightmost accessible x coordinate (right edge)
            int targetX = _currentPitRightEdge;

            // Generate candidate targets along the right edge
            var candidates = new Point[]
            {
                new Point(targetX, 3),
                new Point(targetX, 4),
                new Point(targetX, 5),
                new Point(targetX, 6),
                new Point(targetX, 7),
                new Point(targetX, 8),
                new Point(targetX, 9)
            };

            Debug.Log($"[PitWidthManager] Generated {candidates.Length} candidate targets at x={targetX}");
            return candidates;
        }

        /// <summary>
        /// Calculate the current dynamic pit bounds in world coordinates
        /// </summary>
        public Rectangle CalculateCurrentPitWorldBounds()
        {
            if (!_isInitialized)
            {
                Debug.Warn("[PitWidthManager] Manager not initialized, returning default pit bounds");
                return CalculateDefaultPitWorldBounds();
            }

            // Calculate dynamic width (inclusive of both edges)
            int dynamicPitWidth = _currentPitRightEdge - GameConfig.PitRectX;

            // Convert tile coordinates to world coordinates
            var topLeftWorld = new Vector2(
                GameConfig.PitRectX * GameConfig.TileSize - GameConfig.PitColliderPadding,
                GameConfig.PitRectY * GameConfig.TileSize - GameConfig.PitColliderPadding
            );

            var bottomRightWorld = new Vector2(
                (GameConfig.PitRectX + dynamicPitWidth) * GameConfig.TileSize + GameConfig.PitColliderPadding,
                (GameConfig.PitRectY + GameConfig.PitRectHeight) * GameConfig.TileSize + GameConfig.PitColliderPadding
            );

            return new Rectangle(
                (int)topLeftWorld.X,
                (int)topLeftWorld.Y,
                (int)(bottomRightWorld.X - topLeftWorld.X),
                (int)(bottomRightWorld.Y - topLeftWorld.Y)
            );
        }

        /// <summary>
        /// Calculate the default static pit bounds for fallback
        /// </summary>
        private Rectangle CalculateDefaultPitWorldBounds()
        {
            // Convert tile coordinates to world coordinates
            var topLeftWorld = new Vector2(
                GameConfig.PitRectX * GameConfig.TileSize - GameConfig.PitColliderPadding,
                GameConfig.PitRectY * GameConfig.TileSize - GameConfig.PitColliderPadding
            );

            var bottomRightWorld = new Vector2(
                (GameConfig.PitRectX + GameConfig.PitRectWidth) * GameConfig.TileSize + GameConfig.PitColliderPadding,
                (GameConfig.PitRectY + GameConfig.PitRectHeight) * GameConfig.TileSize + GameConfig.PitColliderPadding
            );

            return new Rectangle(
                (int)topLeftWorld.X,
                (int)topLeftWorld.Y,
                (int)(bottomRightWorld.X - topLeftWorld.X),
                (int)(bottomRightWorld.Y - topLeftWorld.Y)
            );
        }
    }
}