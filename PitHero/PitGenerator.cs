using Microsoft.Xna.Framework;
using Nez;
using Nez.AI.Pathfinding;
using Nez.Tiled;
using PitHero.Util;
using System.Collections.Generic;
using System.Linq;

namespace PitHero
{
    /// <summary>
    /// Generates pit content including obstacles, treasures, monsters, and wizard orbs
    /// </summary>
    public class PitGenerator
    {
        private Scene _scene;
        private System.Random _random;
        private HashSet<Point> _collisionTiles;

        public PitGenerator(Scene scene)
        {
            _scene = scene;
            _random = new System.Random();
            _collisionTiles = new HashSet<Point>();
        }

        /// <summary>
        /// Generates pit content for the specified level
        /// </summary>
        /// <param name="level">The pit level to generate</param>
        public void Generate(int level)
        {
            Debug.Log($"[PitGenerator] Generating pit content for level {level}");

            // For level 1, generate fixed entities
            if (level == 1)
            {
                GenerateLevel1();
            }
        }

        private void GenerateLevel1()
        {
            // Calculate valid placement area (excluding 1-tile perimeter)
            // Pit area: (1,2) to (12,10) in tiles
            // Valid interior: (2,3) to (10,9) in tiles  (excludes rightmost edge)
            var validMinX = GameConfig.PitRectX + 1; // 2
            var validMinY = GameConfig.PitRectY + 1; // 3
            var validMaxX = GameConfig.PitRectX + GameConfig.PitRectWidth - 3; // 10
            var validMaxY = GameConfig.PitRectY + GameConfig.PitRectHeight - 2; // 9
            
            Debug.Log($"[PitGenerator] Valid placement area: tiles ({validMinX},{validMinY}) to ({validMaxX},{validMaxY})");

            // Initialize collision tracking with tilemap collision tiles
            InitializeCollisionTiles(validMinX, validMinY, validMaxX, validMaxY);

            // Generate entities with pathfinding validation
            var maxAttempts = 10;
            bool validLayoutGenerated = false;

            for (int attempt = 1; attempt <= maxAttempts && !validLayoutGenerated; attempt++)
            {
                Debug.Log($"[PitGenerator] Generation attempt {attempt}");
                
                // Keep track of used positions to avoid overlaps
                var usedPositions = new HashSet<Point>();
                var obstaclePositions = new HashSet<Point>();
                var targetPositions = new List<Point>(); // Treasures, monsters, wizard orbs

                // Generate obstacles first
                var obstacles = GenerateEntityPositions(10, validMinX, validMinY, validMaxX, validMaxY, usedPositions, "obstacles");
                obstaclePositions.UnionWith(obstacles);
                usedPositions.UnionWith(obstacles);

                // Generate target entities
                var treasures = GenerateEntityPositions(2, validMinX, validMinY, validMaxX, validMaxY, usedPositions, "treasures");
                var monsters = GenerateEntityPositions(2, validMinX, validMinY, validMaxX, validMaxY, usedPositions, "monsters");
                var wizardOrbs = GenerateEntityPositions(1, validMinX, validMinY, validMaxX, validMaxY, usedPositions, "wizard orbs");

                targetPositions.AddRange(treasures);
                targetPositions.AddRange(monsters);
                targetPositions.AddRange(wizardOrbs);

                // Validate that all targets are reachable
                if (ValidateAllTargetsReachable(obstaclePositions, targetPositions, validMinX, validMinY, validMaxX, validMaxY))
                {
                    // Valid layout found - create the actual entities
                    CreateEntitiesAtPositions(obstacles, GameConfig.TAG_OBSTACLE, Color.Gray, "obstacle");
                    CreateEntitiesAtPositions(treasures, GameConfig.TAG_TREASURE, Color.Yellow, "treasure");
                    CreateEntitiesAtPositions(monsters, GameConfig.TAG_MONSTER, Color.Red, "monster");
                    CreateEntitiesAtPositions(wizardOrbs, GameConfig.TAG_WIZARD_ORB, Color.Blue, "wizard_orb");

                    validLayoutGenerated = true;
                    Debug.Log($"[PitGenerator] Valid layout generated on attempt {attempt}");
                    Debug.Log($"[PitGenerator] Generated {usedPositions.Count} entities total in pit");
                }
                else
                {
                    Debug.Log($"[PitGenerator] Attempt {attempt} failed - some targets unreachable");
                }
            }

            if (!validLayoutGenerated)
            {
                Debug.Log($"[PitGenerator] Warning: Could not generate valid layout after {maxAttempts} attempts");
                // Fallback to a simple safe layout
                GenerateFallbackLayout(validMinX, validMinY, validMaxX, validMaxY);
            }
        }

        private void InitializeCollisionTiles(int minX, int minY, int maxX, int maxY)
        {
            _collisionTiles.Clear();

            // Get tilemap service to check for collision tiles
            var tiledMapService = Core.Services.GetService<TiledMapService>();
            if (tiledMapService?.CurrentMap == null)
            {
                Debug.Log("[PitGenerator] No tilemap service found - assuming no tilemap collisions");
                return;
            }

            var collisionLayer = tiledMapService.CurrentMap.GetLayer<TmxLayer>("Collision");
            if (collisionLayer == null)
            {
                Debug.Log("[PitGenerator] No collision layer found in tilemap");
                return;
            }

            // Check each tile in the valid area for collisions
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    var tile = collisionLayer.GetTile(x, y);
                    if (tile != null && tile.Gid != 0) // Non-empty tile indicates collision
                    {
                        _collisionTiles.Add(new Point(x, y));
                        Debug.Log($"[PitGenerator] Found collision tile at ({x},{y})");
                    }
                }
            }

            Debug.Log($"[PitGenerator] Found {_collisionTiles.Count} collision tiles from tilemap");
        }

        private List<Point> GenerateEntityPositions(int count, int minX, int minY, int maxX, int maxY, HashSet<Point> usedPositions, string entityType)
        {
            var positions = new List<Point>();
            
            for (int i = 0; i < count; i++)
            {
                Point tilePos = GetRandomUnusedPosition(minX, minY, maxX, maxY, usedPositions);
                if (tilePos.X == -1) // No valid position found
                {
                    Debug.Log($"[PitGenerator] Warning: Could not find valid position for {entityType} {i + 1}");
                    continue;
                }

                positions.Add(tilePos);
                usedPositions.Add(tilePos);
            }

            return positions;
        }

        private void CreateEntitiesAtPositions(List<Point> positions, int tag, Color color, string entityTypeName)
        {
            for (int i = 0; i < positions.Count; i++)
            {
                var tilePos = positions[i];
                
                // Convert tile position to world position (center of tile)
                var worldPos = new Vector2(
                    tilePos.X * GameConfig.TileSize + GameConfig.TileSize / 2,
                    tilePos.Y * GameConfig.TileSize + GameConfig.TileSize / 2
                );

                // Create entity
                var entity = _scene.CreateEntity($"{entityTypeName}_{i + 1}");
                entity.SetTag(tag);
                entity.SetPosition(worldPos);

                // Add visual component with specified color
                var renderer = entity.AddComponent(new PrototypeSpriteRenderer(GameConfig.TileSize, GameConfig.TileSize));
                renderer.Color = color;

                // Add collision component
                var collider = entity.AddComponent(new BoxCollider(GameConfig.TileSize, GameConfig.TileSize));

                // Register obstacle tiles as A* walls so pathfinding avoids them
                if (tag == GameConfig.TAG_OBSTACLE)
                {
                    var astarGraph = Core.Services.GetService<AstarGridGraph>();
                    if (astarGraph != null)
                    {
                        astarGraph.Walls.Add(tilePos);
                        Debug.Log($"[PitGenerator] Added obstacle tile to A* walls at ({tilePos.X},{tilePos.Y})");
                    }
                    else
                    {
                        Debug.Warn("[PitGenerator] A* graph not found when adding obstacle walls");
                    }
                }

                Debug.Log($"[PitGenerator] Created {entityTypeName} at tile ({tilePos.X},{tilePos.Y}), world ({worldPos.X},{worldPos.Y})");
            }
        }

        private bool ValidateAllTargetsReachable(HashSet<Point> obstaclePositions, List<Point> targetPositions, int minX, int minY, int maxX, int maxY)
        {
            if (targetPositions.Count == 0)
            {
                Debug.Log("[PitGenerator] No targets to validate");
                return true;
            }

            // Hero starts at map center, which should be outside the pit
            var heroStartTile = new Point(GameConfig.MapCenterTileX, GameConfig.MapCenterTileY);
            
            // Find the closest accessible tile to the pit boundary as our starting point for pathfinding
            var pitEntryPoint = FindPitEntryPoint(obstaclePositions, minX, minY, maxX, maxY);
            if (!pitEntryPoint.HasValue)
            {
                Debug.Log("[PitGenerator] No accessible entry point to pit found");
                return false;
            }

            Debug.Log($"[PitGenerator] Using pit entry point ({pitEntryPoint.Value.X},{pitEntryPoint.Value.Y}) for pathfinding validation");

            // Check if each target is reachable from the pit entry point
            foreach (var target in targetPositions)
            {
                if (!IsPathExists(pitEntryPoint.Value, target, obstaclePositions, minX, minY, maxX, maxY))
                {
                    Debug.Log($"[PitGenerator] Target at ({target.X},{target.Y}) is not reachable");
                    return false;
                }
            }

            Debug.Log($"[PitGenerator] All {targetPositions.Count} targets are reachable");
            return true;
        }

        private Point? FindPitEntryPoint(HashSet<Point> obstaclePositions, int minX, int minY, int maxX, int maxY)
        {
            // Try to find an accessible tile near the pit boundary
            // Check the perimeter of the valid area first
            var potentialEntries = new List<Point>();

            // Top edge
            for (int x = minX; x <= maxX; x++)
            {
                var point = new Point(x, minY);
                if (!IsBlocked(point, obstaclePositions))
                    potentialEntries.Add(point);
            }

            // Bottom edge
            for (int x = minX; x <= maxX; x++)
            {
                var point = new Point(x, maxY);
                if (!IsBlocked(point, obstaclePositions))
                    potentialEntries.Add(point);
            }

            // Left edge
            for (int y = minY; y <= maxY; y++)
            {
                var point = new Point(minX, y);
                if (!IsBlocked(point, obstaclePositions))
                    potentialEntries.Add(point);
            }

            // Right edge
            for (int y = minY; y <= maxY; y++)
            {
                var point = new Point(maxX, y);
                if (!IsBlocked(point, obstaclePositions))
                    potentialEntries.Add(point);
            }

            return potentialEntries.FirstOrDefault();
        }

        private bool IsPathExists(Point start, Point target, HashSet<Point> obstaclePositions, int minX, int minY, int maxX, int maxY)
        {
            // Simple BFS pathfinding
            var queue = new Queue<Point>();
            var visited = new HashSet<Point>();
            
            queue.Enqueue(start);
            visited.Add(start);

            var directions = new Point[]
            {
                new Point(0, 1),   // Down
                new Point(0, -1),  // Up
                new Point(1, 0),   // Right
                new Point(-1, 0)   // Left
            };

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                
                if (current == target)
                {
                    return true; // Path found
                }

                foreach (var dir in directions)
                {
                    var next = new Point(current.X + dir.X, current.Y + dir.Y);
                    
                    // Check bounds
                    if (next.X < minX || next.X > maxX || next.Y < minY || next.Y > maxY)
                        continue;
                    
                    // Check if already visited
                    if (visited.Contains(next))
                        continue;
                    
                    // Check if blocked
                    if (IsBlocked(next, obstaclePositions))
                        continue;
                    
                    queue.Enqueue(next);
                    visited.Add(next);
                }
            }

            return false; // No path found
        }

        private bool IsBlocked(Point position, HashSet<Point> obstaclePositions)
        {
            // Check if position is blocked by tilemap collision or obstacles
            return _collisionTiles.Contains(position) || obstaclePositions.Contains(position);
        }

        private void GenerateFallbackLayout(int minX, int minY, int maxX, int maxY)
        {
            Debug.Log("[PitGenerator] Generating fallback safe layout");
            
            // Create a simple layout with guaranteed paths
            // Place obstacles only in corners and edges, leaving center areas clear
            var safeObstacles = new List<Point>
            {
                new Point(minX, minY),       // Top-left
                new Point(maxX, minY),       // Top-right
                new Point(minX, maxY),       // Bottom-left
                new Point(maxX, maxY),       // Bottom-right
                new Point(minX + 1, minY),   // Additional safe obstacles
                new Point(maxX - 1, minY),
                new Point(minX, minY + 1),
                new Point(maxX, minY + 1),
                new Point(minX + 2, maxY),
                new Point(maxX - 2, maxY)
            };

            // Place targets in easily accessible positions
            var centerX = (minX + maxX) / 2;
            var centerY = (minY + maxY) / 2;
            
            var safeTargets = new List<(Point pos, int tag, Color color, string name)>
            {
                (new Point(centerX, centerY), GameConfig.TAG_TREASURE, Color.Yellow, "treasure"),
                (new Point(centerX + 1, centerY), GameConfig.TAG_TREASURE, Color.Yellow, "treasure"),
                (new Point(centerX, centerY + 1), GameConfig.TAG_MONSTER, Color.Red, "monster"),
                (new Point(centerX - 1, centerY), GameConfig.TAG_MONSTER, Color.Red, "monster"),
                (new Point(centerX, centerY - 1), GameConfig.TAG_WIZARD_ORB, Color.Blue, "wizard_orb")
            };

            // Create obstacle entities
            CreateEntitiesAtPositions(safeObstacles, GameConfig.TAG_OBSTACLE, Color.Gray, "obstacle");

            // Create target entities
            foreach (var (pos, tag, color, name) in safeTargets)
            {
                CreateEntitiesAtPositions(new List<Point> { pos }, tag, color, name);
            }

            Debug.Log("[PitGenerator] Fallback layout created with guaranteed paths");
        }

        private Point GetRandomUnusedPosition(int minX, int minY, int maxX, int maxY, HashSet<Point> usedPositions)
        {
            // Calculate total available spots
            int totalSpots = (maxX - minX + 1) * (maxY - minY + 1);
            int attempts = 0;
            int maxAttempts = totalSpots * 2; // Allow extra attempts for safety

            while (attempts < maxAttempts)
            {
                int x = _random.Next(minX, maxX + 1);
                int y = _random.Next(minY, maxY + 1);
                var pos = new Point(x, y);

                if (!usedPositions.Contains(pos) && !_collisionTiles.Contains(pos))
                {
                    return pos;
                }

                attempts++;
            }

            // If we can't find a spot, return invalid position
            return new Point(-1, -1);
        }

        private string GetEntityTypeName(int tag)
        {
            switch (tag)
            {
                case GameConfig.TAG_OBSTACLE: return "obstacle";
                case GameConfig.TAG_TREASURE: return "treasure";
                case GameConfig.TAG_MONSTER: return "monster";
                case GameConfig.TAG_WIZARD_ORB: return "wizard_orb";
                default: return "unknown";
            }
        }
    }
}