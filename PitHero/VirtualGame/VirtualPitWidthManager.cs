using Microsoft.Xna.Framework;
using PitHero.AI.Interfaces;
using PitHero.Config;
using PitHero.Util;
using System;
using System.Collections.Generic;

namespace PitHero.VirtualGame
{
    /// <summary>
    /// Virtual implementation of IPitWidthManager for virtual layer
    /// Uses actual PitWidthManager logic but operates on virtual tile data
    /// </summary>
    public class VirtualPitWidthManager : IPitWidthManager
    {
        private readonly VirtualTiledMapService _tiledMapService;

        // State tracking (same as real PitWidthManager)
        private int _currentPitLevel = 1;
        private int _currentPitTier = 1;
        private int _tierBaseLevel = 1;
        private int _currentPitRightEdge;
        private bool _isInitialized = false;

        // Tile patterns (same as real PitWidthManager)
        private Dictionary<int, int> _baseOuterFloor;
        private Dictionary<int, int> _collisionOuterFloor;
        private Dictionary<int, int> _baseInnerWall;
        private Dictionary<int, int> _collisionInnerWall;
        private Dictionary<int, int> _baseInnerFloor;
        private Dictionary<int, int> _collisionInnerFloor;
        private int _fogOfWarIndex;
        private int _groundTileIndex;

        public VirtualPitWidthManager(VirtualTiledMapService tiledMapService)
        {
            _tiledMapService = tiledMapService;
        }

        public int CurrentPitLevel => _currentPitLevel;
        public int CurrentPitTier => _currentPitTier;
        public int TierBaseLevel => _tierBaseLevel;
        public int CurrentPitRightEdge => _currentPitRightEdge;

        public int CurrentPitRectWidthTiles => _isInitialized
            ? (_currentPitRightEdge - GameConfig.PitRectX + 1)
            : GameConfig.PitRectWidth;

        public int CurrentPitCenterTileX
        {
            get
            {
                if (!_isInitialized)
                    return GameConfig.PitCenterTileX;

                int leftInteriorX = GameConfig.PitRectX + 1;
                int rightInteriorX = _currentPitRightEdge - 2;
                return leftInteriorX + ((rightInteriorX - leftInteriorX) / 2);
            }
        }

        public void Initialize()
        {
            if (_isInitialized)
            {
                Console.WriteLine("[VirtualPitWidthManager] Already initialized, skipping");
                return;
            }

            if (_tiledMapService?.CurrentMap == null)
            {
                Console.WriteLine("[VirtualPitWidthManager] Cannot initialize - TiledMapService or CurrentMap is null");
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
                Console.WriteLine("[VirtualPitWidthManager] Required layers (Base, Collision, FogOfWar) not found in map");
                return;
            }

            // Record FogOfWar tile index at (2,3)
            var fogTile = fogOfWarLayer.GetTile(2, 3);
            _fogOfWarIndex = fogTile?.Gid ?? 301; // Default fog tile index
            Console.WriteLine($"[VirtualPitWidthManager] Recorded FogOfWar index: {_fogOfWarIndex}");

            // Record ground tile at (19,1)
            var groundTile = baseLayer.GetTile(19, 1);
            _groundTileIndex = groundTile?.Gid ?? 104; // Default ground tile index
            Console.WriteLine($"[VirtualPitWidthManager] Recorded ground tile index: {_groundTileIndex}");

            // Initialize tile patterns from the virtual map data
            InitializeTilePattern(_baseOuterFloor, baseLayer, 13, 1, 11, "baseOuterFloor");
            InitializeTilePattern(_collisionOuterFloor, collisionLayer, 13, 1, 11, "collisionOuterFloor");
            InitializeTilePattern(_baseInnerWall, baseLayer, 12, 1, 11, "baseInnerWall");
            InitializeTilePattern(_collisionInnerWall, collisionLayer, 12, 1, 11, "collisionInnerWall");
            InitializeTilePattern(_baseInnerFloor, baseLayer, 11, 1, 11, "baseInnerFloor");
            InitializeTilePattern(_collisionInnerFloor, collisionLayer, 11, 1, 11, "collisionInnerFloor");

            // Set initial pit right edge
            _currentPitRightEdge = GameConfig.PitRectX + GameConfig.PitRectWidth;

            _isInitialized = true;
            Console.WriteLine($"[VirtualPitWidthManager] Initialization complete. Current pit right edge: {_currentPitRightEdge}");
        }

        private void InitializeTilePattern(Dictionary<int, int> dictionary, ILayerData layer, int x, int startY, int endY, string patternName)
        {
            int nonZeroCount = 0;
            for (int y = startY; y <= endY; y++)
            {
                var tile = layer.GetTile(x, y);
                int tileIndex = tile?.Gid ?? 0;
                dictionary[y] = tileIndex;
                Console.WriteLine($"[VirtualPitWidthManager] {patternName}[{y}] = {tileIndex}");
                if (tileIndex != 0) nonZeroCount++;
            }
            Console.WriteLine($"[VirtualPitWidthManager] {patternName} total: {dictionary.Count} tiles, {nonZeroCount} non-zero");
        }

        /// <summary>
        /// Sets the pit tier, clamped to [1, MaxPitTier].
        /// </summary>
        public void SetPitTier(int tier)
        {
            int clamped = tier < 1 ? 1 : (tier > BiomeProgressionConfig.MaxPitTier ? BiomeProgressionConfig.MaxPitTier : tier);
            _currentPitTier = clamped;
            Console.WriteLine($"[VirtualPitWidthManager] PitTier set to {_currentPitTier}");
        }

        /// <summary>
        /// Sets the tier base level (clamped to ≥1, monotonic non-decreasing — lower values are ignored).
        /// </summary>
        public void SetTierBaseLevel(int heroLevel)
        {
            int clamped = heroLevel < 1 ? 1 : heroLevel;
            if (clamped > _tierBaseLevel)
            {
                _tierBaseLevel = clamped;
                Console.WriteLine($"[VirtualPitWidthManager] TierBaseLevel updated to {_tierBaseLevel}");
            }
        }

        /// <summary>
        /// Resets tier progression to the first cycle after permadeath. Bypasses the monotonic
        /// guard in SetTierBaseLevel, which stays monotonic for normal progression.
        /// </summary>
        public void ResetTierForNewCycle()
        {
            _currentPitTier = 1;
            _tierBaseLevel = 1;
            Console.WriteLine("[VirtualPitWidthManager] Tier reset to 1 for new cycle (hero death)");
        }

        /// <summary>
        /// Increments the pit tier by 1 (clamped at MaxPitTier) and records the hero level as the new tier base level.
        /// </summary>
        public void IncrementPitTier(int heroLevelAtEntry)
        {
            int newTier = _currentPitTier + 1;
            if (newTier > BiomeProgressionConfig.MaxPitTier)
                newTier = BiomeProgressionConfig.MaxPitTier;
            _currentPitTier = newTier;
            SetTierBaseLevel(heroLevelAtEntry);
            Console.WriteLine($"[VirtualPitWidthManager] PitTier incremented to {_currentPitTier}, TierBaseLevel={_tierBaseLevel}");
        }

        public void SetPitLevel(int newLevel)
        {
            if (!_isInitialized)
            {
                Console.WriteLine("[VirtualPitWidthManager] Cannot set pit level - manager not initialized");
                return;
            }

            if (newLevel < 1)
            {
                Console.WriteLine($"[VirtualPitWidthManager] Invalid pit level {newLevel}, minimum is 1");
                return;
            }

            var previousLevel = _currentPitLevel;
            var previousRightEdge = _currentPitRightEdge;

            Console.WriteLine($"[VirtualPitWidthManager] Setting pit level from {_currentPitLevel} to {newLevel}");
            _currentPitLevel = newLevel;

            // Mirror the real PitWidthManager: when sizing down, restore the old extension
            // region first so no stale inner-wall collision column survives the shrink.
            int newRightEdge = PitWidthManager.CalculateRightEdgeForDepth(BiomeProgressionConfig.GetEffectiveDepth(_currentPitLevel, _currentPitTier));
            if (newRightEdge < previousRightEdge)
            {
                ClearTilesFromXToEnd(newRightEdge);
            }

            RegeneratePitWidth();
        }

        /// <summary>
        /// Clear tiles from a given x coordinate to x=33 to clean up when sizing down.
        /// Mirrors the real PitWidthManager: the base pit's inner-wall (x=12) and outer-floor (x=13)
        /// columns are restored from their recorded patterns; everything further right becomes
        /// plain ground with no collision or fog.
        /// </summary>
        private void ClearTilesFromXToEnd(int startX)
        {
            Console.WriteLine($"[VirtualPitWidthManager] Clearing tiles from x={startX} to x=33, y=1 to y=11");

            for (int x = startX - 1; x <= 33; x++)
            {
                for (int y = 1; y <= 11; y++)
                {
                    if (x == 12 || x == 13)
                    {
                        // Restore the base pit's own columns from their recorded patterns
                        var basePattern = x == 12 ? _baseInnerWall : _baseOuterFloor;
                        var collisionPattern = x == 12 ? _collisionInnerWall : _collisionOuterFloor;

                        if (basePattern.TryGetValue(y, out int baseGid) && baseGid != 0)
                            _tiledMapService.SetTile("Base", x, y, baseGid);

                        if (collisionPattern.TryGetValue(y, out int colGid) && colGid != 0)
                            _tiledMapService.SetTile("Collision", x, y, colGid);
                        else
                            _tiledMapService.RemoveTile("Collision", x, y);
                    }
                    else
                    {
                        if (_groundTileIndex != 0)
                            _tiledMapService.SetTile("Base", x, y, _groundTileIndex);
                        _tiledMapService.RemoveTile("Collision", x, y);
                    }

                    _tiledMapService.RemoveTile("FogOfWar", x, y);
                }
            }

            Console.WriteLine($"[VirtualPitWidthManager] Cleared tiles from x={startX} to x=33");
        }

        public void RegeneratePitWidth()
        {
            if (!_isInitialized)
            {
                Console.WriteLine("[VirtualPitWidthManager] Cannot regenerate pit width - manager not initialized");
                return;
            }

            // Calculate how many inner floor tiles to extend (same logic as real PitWidthManager).
            // Use effective depth so expansion continues across tiers, capped at depth 100.
            int effectiveDepth = BiomeProgressionConfig.GetEffectiveDepth(_currentPitLevel, _currentPitTier);
            int expansionLevel = Math.Min(effectiveDepth, 100);
            int innerFloorTilesToExtend = ((int)(expansionLevel / 10)) * 2;
            Console.WriteLine($"[VirtualPitWidthManager] Level {_currentPitLevel} Tier {_currentPitTier} (effectiveDepth={effectiveDepth}): extending pit by {innerFloorTilesToExtend} inner floor tiles");

            if (innerFloorTilesToExtend <= 0)
            {
                // No extension tiles — reset right edge to the base pit boundary (mirrors real manager)
                _currentPitRightEdge = GameConfig.PitRectX + GameConfig.PitRectWidth;
                Console.WriteLine($"[VirtualPitWidthManager] No extension needed for current level. Reset right edge to {_currentPitRightEdge}");
                return;
            }

            // Start extending from the original pit boundary 
            int currentX = 12; // Start at x=12 for inner floor extension
            int lastXCoordinate = currentX;

            // Loop to extend inner floor tiles
            for (int i = 0; i < innerFloorTilesToExtend; i++)
            {
                ExtendColumn(currentX, _baseInnerFloor, _collisionInnerFloor, "inner floor");
                lastXCoordinate = currentX;
                currentX++;
            }

            // Add inner wall column after all inner floor columns
            if (innerFloorTilesToExtend > 0)
            {
                currentX = lastXCoordinate + 1;
                ExtendColumn(currentX, _baseInnerWall, _collisionInnerWall, "inner wall");
                lastXCoordinate = currentX;

                // Add outer floor column after inner wall
                currentX = lastXCoordinate + 1;
                ExtendColumn(currentX, _baseOuterFloor, _collisionOuterFloor, "outer floor");
                lastXCoordinate = currentX;
            }

            // Update the current pit right edge
            _currentPitRightEdge = lastXCoordinate;
            Console.WriteLine($"[VirtualPitWidthManager] Pit extension complete. New right edge: {_currentPitRightEdge}");

            // Regenerate FogOfWar for the entire current pit area
            RegenerateFogOfWar();
        }

        private void ExtendColumn(int x, Dictionary<int, int> basePattern, Dictionary<int, int> collisionPattern, string columnType)
        {
            Console.WriteLine($"[VirtualPitWidthManager] Extending {columnType} column at x={x}");

            // Set tiles from y=1 to y=11 (same as real PitWidthManager)
            for (int y = 1; y <= 11; y++)
            {
                // Set Base layer tile
                if (basePattern.TryGetValue(y, out int baseTileIndex) && baseTileIndex != 0)
                {
                    _tiledMapService.SetTile("Base", x, y, baseTileIndex);
                }

                // Set Collision layer tile (or remove if 0/null)
                if (collisionPattern.TryGetValue(y, out int collisionTileIndex))
                {
                    if (collisionTileIndex != 0)
                    {
                        _tiledMapService.SetTile("Collision", x, y, collisionTileIndex);
                    }
                    else
                    {
                        _tiledMapService.RemoveTile("Collision", x, y);
                    }
                }
                else
                {
                    _tiledMapService.RemoveTile("Collision", x, y);
                }

                // Set FogOfWar layer tile - only for y=3 to y=9 and not for inner wall or outer floor columns
                bool shouldSetFogOfWar = (y >= 3 && y <= 9) &&
                                        !columnType.Contains("inner wall") &&
                                        !columnType.Contains("outer floor");

                if (shouldSetFogOfWar && _fogOfWarIndex != 0)
                {
                    _tiledMapService.SetTile("FogOfWar", x, y, GameConfig.FogOfWarZeroTileIndex + 15);
                }
            }
        }

        private void RegenerateFogOfWar()
        {
            if (!_isInitialized)
            {
                Console.WriteLine("[VirtualPitWidthManager] Cannot regenerate FogOfWar - manager not initialized");
                return;
            }

            if (_fogOfWarIndex == 0)
            {
                Console.WriteLine("[VirtualPitWidthManager] No FogOfWar tile index recorded, skipping FogOfWar regeneration");
                return;
            }

            Console.WriteLine($"[VirtualPitWidthManager] Regenerating FogOfWar for entire pit area from x=2 to x={_currentPitRightEdge}, y=3 to y=9");

            // Add FogOfWar tiles for the entire current pit area (same as real PitWidthManager)
            for (int x = 2; x <= _currentPitRightEdge - 2; x++)
            {
                for (int y = 3; y <= 9; y++)
                {
                    _tiledMapService.SetTile("FogOfWar", x, y, GameConfig.FogOfWarZeroTileIndex + 15);
                }
            }

            // Second pass: correct bitmask for perimeter tiles that border non-fog areas
            for (int x = 2; x <= _currentPitRightEdge - 2; x++)
            {
                for (int y = 3; y <= 9; y++)
                {
                    int idx = TileBitmask.GetTileIndex(x, y, GameConfig.FogOfWarZeroTileIndex, _tiledMapService.IsFogOfWarTile);
                    _tiledMapService.SetTile("FogOfWar", x, y, idx);
                }
            }

            Console.WriteLine($"[VirtualPitWidthManager] FogOfWar regeneration complete");
        }

        public Point[] GetCurrentPitCandidateTargets()
        {
            if (!_isInitialized)
            {
                Console.WriteLine("[VirtualPitWidthManager] Manager not initialized, returning default targets");
                return new Point[]
                {
                    new Point(13, 3), new Point(13, 4), new Point(13, 5), new Point(13, 6),
                    new Point(13, 7), new Point(13, 8), new Point(13, 9)
                };
            }

            // Calculate the rightmost accessible x coordinate (same as real PitWidthManager)
            int targetX = _currentPitRightEdge;

            var candidates = new Point[]
            {
                new Point(targetX, 3), new Point(targetX, 4), new Point(targetX, 5), new Point(targetX, 6),
                new Point(targetX, 7), new Point(targetX, 8), new Point(targetX, 9)
            };

            Console.WriteLine($"[VirtualPitWidthManager] Generated {candidates.Length} candidate targets at x={targetX}");
            return candidates;
        }

        public Rectangle CalculateCurrentPitWorldBounds()
        {
            if (!_isInitialized)
            {
                Console.WriteLine("[VirtualPitWidthManager] Manager not initialized, returning default pit bounds");
                return new Rectangle(GameConfig.PitRectX * GameConfig.TileSize, GameConfig.PitRectY * GameConfig.TileSize,
                                   GameConfig.PitRectWidth * GameConfig.TileSize, GameConfig.PitRectHeight * GameConfig.TileSize);
            }

            // Calculate dynamic width (same as real PitWidthManager)
            int dynamicPitWidth = _currentPitRightEdge - GameConfig.PitRectX;

            return new Rectangle(
                GameConfig.PitRectX * GameConfig.TileSize,
                GameConfig.PitRectY * GameConfig.TileSize,
                dynamicPitWidth * GameConfig.TileSize,
                GameConfig.PitRectHeight * GameConfig.TileSize
            );
        }
    }
}