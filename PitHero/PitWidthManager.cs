using Microsoft.Xna.Framework;
using Nez;
using Nez.Tiled;
using PitHero.Util;
using PitHero.ECS.Scenes;
using System.Collections.Generic;

namespace PitHero
{
    /// <summary>
    /// Manages dynamic pit width generation based on pit level
    /// Every 10 levels, the pit width extends by 2 tiles to the right
    /// </summary>
    public class PitWidthManager
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
        
        // Store original tilemap state for restoration during regeneration
        private Dictionary<Point, int> _originalBaseTiles; // Original Base layer tiles from x=13 to x=33
        private Dictionary<Point, int> _originalCollisionTiles; // Original Collision layer tiles from x=13 to x=33
        private Dictionary<Point, int> _originalFogOfWarTiles; // Original FogOfWar layer tiles from x=13 to x=33
        
        // Track current pit state
        private int _currentPitLevel = 1;
        private int _currentPitRightEdge; // The rightmost x coordinate of the current pit
        private bool _isInitialized = false;

        public int CurrentPitLevel => _currentPitLevel;
        public int CurrentPitRightEdge => _currentPitRightEdge;

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

            var tiledMapService = Core.Services.GetService<TiledMapService>();
            if (tiledMapService?.CurrentMap == null)
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

            // Initialize dictionaries for storing original tilemap state
            _originalBaseTiles = new Dictionary<Point, int>();
            _originalCollisionTiles = new Dictionary<Point, int>();
            _originalFogOfWarTiles = new Dictionary<Point, int>();
            // Get layer references
            var baseLayer = tiledMapService.CurrentMap.GetLayer<TmxLayer>("Base");
            var collisionLayer = tiledMapService.CurrentMap.GetLayer<TmxLayer>("Collision");
            var fogOfWarLayer = tiledMapService.CurrentMap.GetLayer<TmxLayer>("FogOfWar");

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

            // Initialize baseOuterFloor and collisionOuterFloor from coordinates (13,1) to (13,11)
            InitializeTilePattern(_baseOuterFloor, baseLayer, 13, 1, 11, "baseOuterFloor");
            InitializeTilePattern(_collisionOuterFloor, collisionLayer, 13, 1, 11, "collisionOuterFloor");

            // Initialize baseInnerWall and collisionInnerWall from coordinates (12,1) to (12,11)
            InitializeTilePattern(_baseInnerWall, baseLayer, 12, 1, 11, "baseInnerWall");
            InitializeTilePattern(_collisionInnerWall, collisionLayer, 12, 1, 11, "collisionInnerWall");

            // Initialize baseInnerFloor and collisionInnerFloor from coordinates (11,1) to (11,11)
            InitializeTilePattern(_baseInnerFloor, baseLayer, 11, 1, 11, "baseInnerFloor");
            InitializeTilePattern(_collisionInnerFloor, collisionLayer, 11, 1, 11, "collisionInnerFloor");

            // Store original tilemap state for regeneration purposes (x=13 to x=33, y=1 to y=11)
            StoreOriginalTilemapState(baseLayer, collisionLayer, fogOfWarLayer);

            // Set initial pit right edge (default pit goes from x=1 to x=12, so rightmost is 12)
            _currentPitRightEdge = GameConfig.PitRectX + GameConfig.PitRectWidth - 1; // 1 + 12 - 1 = 12

            _isInitialized = true;
            Debug.Log($"[PitWidthManager] Initialization complete. Current pit right edge: {_currentPitRightEdge}");
        }

        /// <summary>
        /// Helper method to initialize a tile pattern dictionary from a specific x column
        /// </summary>
        private void InitializeTilePattern(Dictionary<int, int> dictionary, TmxLayer layer, int x, int startY, int endY, string patternName)
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
        /// Store the original state of the tilemap from x=13 to x=33 for restoration during regeneration
        /// This allows regeneration to start from a clean slate like initial generation
        /// </summary>
        private void StoreOriginalTilemapState(TmxLayer baseLayer, TmxLayer collisionLayer, TmxLayer fogOfWarLayer)
        {
            Debug.Log("[PitWidthManager] Storing original tilemap state for regeneration");
            
            // Store original tiles from x=13 to x=33 (area that may be modified during pit extension)
            for (int x = 13; x <= 33; x++)
            {
                for (int y = 1; y <= 11; y++)
                {
                    var point = new Point(x, y);
                    
                    // Store Base layer tile
                    var baseTile = baseLayer.GetTile(x, y);
                    _originalBaseTiles[point] = baseTile?.Gid ?? 0;
                    
                    // Store Collision layer tile  
                    var collisionTile = collisionLayer.GetTile(x, y);
                    _originalCollisionTiles[point] = collisionTile?.Gid ?? 0;
                    
                    // Store FogOfWar layer tile
                    var fogTile = fogOfWarLayer.GetTile(x, y);
                    _originalFogOfWarTiles[point] = fogTile?.Gid ?? 0;
                }
            }
            
            Debug.Log($"[PitWidthManager] Stored {_originalBaseTiles.Count} original tile states for restoration");
        }

        /// <summary>
        /// Set the pit level and regenerate the pit width accordingly
        /// This method ensures regeneration starts from a clean slate like initial generation
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
            
            // ALWAYS restore tilemap to original state first to ensure clean slate regeneration
            // This makes regeneration work exactly like initial generation
            RestoreTilemapToOriginalState();
            
            // Reset pit right edge to default before regenerating
            _currentPitRightEdge = GameConfig.PitRectX + GameConfig.PitRectWidth - 1; // Reset to 12
            
            // Now regenerate from clean state (like initial generation)
            RegeneratePitWidth();
        }

        /// <summary>
        /// Restore the tilemap to its original state from x=13 to x=33
        /// This ensures regeneration starts from a clean slate like initial generation
        /// </summary>
        private void RestoreTilemapToOriginalState()
        {
            if (!_isInitialized)
            {
                Debug.Error("[PitWidthManager] Cannot restore tilemap - manager not initialized");
                return;
            }

            var tiledMapService = Core.Services.GetService<TiledMapService>();
            if (tiledMapService == null)
            {
                Debug.Error("[PitWidthManager] TiledMapService not available for tilemap restoration");
                return;
            }

            Debug.Log("[PitWidthManager] Restoring tilemap to original state for clean slate regeneration");

            // Restore all tiles from x=13 to x=33 to their original state
            foreach (var kvp in _originalBaseTiles)
            {
                var point = kvp.Key;
                var originalTileIndex = kvp.Value;
                
                // Restore Base layer
                if (originalTileIndex != 0)
                {
                    tiledMapService.SetTile("Base", point.X, point.Y, originalTileIndex);
                }
                else
                {
                    tiledMapService.RemoveTile("Base", point.X, point.Y);
                }
                
                // Restore Collision layer
                if (_originalCollisionTiles.TryGetValue(point, out int originalCollisionIndex))
                {
                    if (originalCollisionIndex != 0)
                    {
                        tiledMapService.SetTile("Collision", point.X, point.Y, originalCollisionIndex);
                    }
                    else
                    {
                        tiledMapService.RemoveTile("Collision", point.X, point.Y);
                    }
                }
                
                // Restore FogOfWar layer
                if (_originalFogOfWarTiles.TryGetValue(point, out int originalFogIndex))
                {
                    if (originalFogIndex != 0)
                    {
                        tiledMapService.SetTile("FogOfWar", point.X, point.Y, originalFogIndex);
                    }
                    else
                    {
                        tiledMapService.RemoveTile("FogOfWar", point.X, point.Y);
                    }
                }
            }

            Debug.Log("[PitWidthManager] Tilemap restoration complete - ready for clean pit generation");
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
                return;
            }

            var tiledMapService = Core.Services.GetService<TiledMapService>();
            if (tiledMapService == null)
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
                ExtendColumn(tiledMapService, currentX, _baseInnerFloor, _collisionInnerFloor, "inner floor");
                lastXCoordinate = currentX;
                currentX++;
            }

            // Add inner wall column after all inner floor columns
            if (innerFloorTilesToExtend > 0)
            {
                currentX = lastXCoordinate + 1;
                ExtendColumn(tiledMapService, currentX, _baseInnerWall, _collisionInnerWall, "inner wall");
                lastXCoordinate = currentX;

                // Add outer floor column after inner wall
                currentX = lastXCoordinate + 1;
                ExtendColumn(tiledMapService, currentX, _baseOuterFloor, _collisionOuterFloor, "outer floor");
                lastXCoordinate = currentX;
            }

            // Update the current pit right edge
            _currentPitRightEdge = lastXCoordinate;
            Debug.Log($"[PitWidthManager] Pit extension complete. New right edge: {_currentPitRightEdge}");

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
        /// Regenerate pit content after pit width changes
        /// </summary>
        private void RegeneratePitContent()
        {
            Debug.Log("[PitWidthManager] Regenerating pit content after width change");
            
            // Regenerate FogOfWar for the entire current pit area
            RegenerateFogOfWar();
            
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

            var tiledMapService = Core.Services.GetService<TiledMapService>();
            if (tiledMapService == null)
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
                    tiledMapService.SetTile("FogOfWar", x, y, _fogOfWarIndex);
                }
            }

            Debug.Log($"[PitWidthManager] FogOfWar regeneration complete");
        }

        /// <summary>
        /// Extend a single column using the provided tile patterns
        /// </summary>
        private void ExtendColumn(TiledMapService tiledMapService, int x, Dictionary<int, int> basePattern, Dictionary<int, int> collisionPattern, string columnType)
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

                // Handle collision tiles with special logic for explorable area
                bool isExplorableArea = (y >= 3 && y <= 9);
                bool isInnerFloorColumn = columnType.Contains("inner floor");
                
                if (collisionPattern.TryGetValue(y, out int collisionTileIndex))
                {
                    // For inner floor columns in explorable area, never set collision tiles to ensure pathfinding works
                    if (isInnerFloorColumn && isExplorableArea)
                    {
                        Debug.Log($"[PitWidthManager] Clearing collision for inner floor at ({x},{y}) to ensure pathfinding");
                        tiledMapService.RemoveTile("Collision", x, y);
                    }
                    else if (collisionTileIndex != 0)
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
                bool shouldSetFogOfWar = isExplorableArea && 
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

            // Calculate the rightmost accessible x coordinate (right edge + 1)
            int targetX = _currentPitRightEdge + 1;

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

            // Calculate dynamic width based on current right edge
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