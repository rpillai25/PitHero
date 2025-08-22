using Microsoft.Xna.Framework;
using Nez;
using Nez.Tiled;
using PitHero;
using PitHero.Util;
using System;
using System.Collections.Generic;
using Nez.AI.Pathfinding;

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
            _collisionTiles = new HashSet<Point>(64);
        }

        public void Generate(int level)
        {
            Debug.Log($"[PitGenerator] Generating pit content for level {level}");

            if (level == 1)
            {
                GenerateLevel1();
            }
        }

        private void GenerateLevel1()
        {
            var validMinX = GameConfig.PitRectX + 1; // 2
            var validMinY = GameConfig.PitRectY + 1; // 3
            var validMaxX = GameConfig.PitRectX + GameConfig.PitRectWidth - 3; // 10
            var validMaxY = GameConfig.PitRectY + GameConfig.PitRectHeight - 2; // 9
            
            Debug.Log($"[PitGenerator] Valid placement area: tiles ({validMinX},{validMinY}) to ({validMaxX},{validMaxY})");

            InitializeCollisionTiles(validMinX, validMinY, validMaxX, validMaxY);

            var maxAttempts = 10;
            bool validLayoutGenerated = false;

            for (int attempt = 1; attempt <= maxAttempts && !validLayoutGenerated; attempt++)
            {
                Debug.Log($"[PitGenerator] Generation attempt {attempt}");
                
                var usedPositions = new HashSet<Point>(64);
                var obstaclePositions = new HashSet<Point>(16);
                var targetPositions = new List<Point>(8);

                var obstacles = GenerateEntityPositions(10, validMinX, validMinY, validMaxX, validMaxY, usedPositions, "obstacles");
                obstaclePositions.UnionWith(obstacles);
                usedPositions.UnionWith(obstacles);

                var treasures = GenerateEntityPositions(2, validMinX, validMinY, validMaxX, validMaxY, usedPositions, "treasures");
                var monsters = GenerateEntityPositions(2, validMinX, validMinY, validMaxX, validMaxY, usedPositions, "monsters");
                var wizardOrbs = GenerateEntityPositions(1, validMinX, validMinY, validMaxX, validMaxY, usedPositions, "wizard orbs");

                // Manual AddRange without LINQ
                for (int i = 0; i < treasures.Count; i++) targetPositions.Add(treasures[i]);
                for (int i = 0; i < monsters.Count; i++) targetPositions.Add(monsters[i]);
                for (int i = 0; i < wizardOrbs.Count; i++) targetPositions.Add(wizardOrbs[i]);

                if (ValidateAllTargetsReachable(obstaclePositions, targetPositions, validMinX, validMinY, validMaxX, validMaxY))
                {
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
                GenerateFallbackLayout(validMinX, validMinY, validMaxX, validMaxY);
            }
        }

        private void InitializeCollisionTiles(int minX, int minY, int maxX, int maxY)
        {
            _collisionTiles.Clear();

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

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    var tile = collisionLayer.GetTile(x, y);
                    if (tile != null && tile.Gid != 0)
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
            var positions = new List<Point>(count);
            
            for (int i = 0; i < count; i++)
            {
                Point tilePos = GetRandomUnusedPosition(minX, minY, maxX, maxY, usedPositions);
                if (tilePos.X == -1)
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
                
                var worldPos = new Vector2(
                    tilePos.X * GameConfig.TileSize + GameConfig.TileSize / 2,
                    tilePos.Y * GameConfig.TileSize + GameConfig.TileSize / 2
                );

                // Avoid dynamic strings for entity names; duplicate names are acceptable
                var entity = _scene.CreateEntity(entityTypeName);
                entity.SetTag(tag);
                entity.SetPosition(worldPos);

                var renderer = entity.AddComponent(new PrototypeSpriteRenderer(GameConfig.TileSize, GameConfig.TileSize));
                renderer.Color = color;
                renderer.SetRenderLayer(GameConfig.RenderLayerActors);

                entity.AddComponent(new BoxCollider(GameConfig.TileSize, GameConfig.TileSize));

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

            var pitEntryPoint = FindPitEntryPoint(obstaclePositions, minX, minY, maxX, maxY);
            if (!pitEntryPoint.HasValue)
            {
                Debug.Log("[PitGenerator] No accessible entry point to pit found");
                return false;
            }

            Debug.Log($"[PitGenerator] Using pit entry point ({pitEntryPoint.Value.X},{pitEntryPoint.Value.Y}) for pathfinding validation");

            for (int i = 0; i < targetPositions.Count; i++)
            {
                var target = targetPositions[i];
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
            // Scan perimeter and return first open tile without allocating intermediate lists

            // Top edge
            for (int x = minX; x <= maxX; x++)
            {
                var point = new Point(x, minY);
                if (!IsBlocked(point, obstaclePositions))
                    return point;
            }

            // Bottom edge
            for (int x = minX; x <= maxX; x++)
            {
                var point = new Point(x, maxY);
                if (!IsBlocked(point, obstaclePositions))
                    return point;
            }

            // Left edge
            for (int y = minY; y <= maxY; y++)
            {
                var point = new Point(minX, y);
                if (!IsBlocked(point, obstaclePositions))
                    return point;
            }

            // Right edge
            for (int y = minY; y <= maxY; y++)
            {
                var point = new Point(maxX, y);
                if (!IsBlocked(point, obstaclePositions))
                    return point;
            }

            return null;
        }

        private bool IsPathExists(Point start, Point target, HashSet<Point> obstaclePositions, int minX, int minY, int maxX, int maxY)
        {
            var queue = new Queue<Point>(32);
            var visited = new HashSet<Point>(64);
            
            queue.Enqueue(start);
            visited.Add(start);

            var directions = new Point[]
            {
                new Point(0, 1),
                new Point(0, -1),
                new Point(1, 0),
                new Point(-1, 0)
            };

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                
                if (current == target)
                {
                    return true;
                }

                for (int i = 0; i < 4; i++)
                {
                    var dir = directions[i];
                    var next = new Point(current.X + dir.X, current.Y + dir.Y);
                    
                    if (next.X < minX || next.X > maxX || next.Y < minY || next.Y > maxY)
                        continue;
                    
                    if (visited.Contains(next))
                        continue;
                    
                    if (IsBlocked(next, obstaclePositions))
                        continue;
                    
                    queue.Enqueue(next);
                    visited.Add(next);
                }
            }

            return false;
        }

        private bool IsBlocked(Point position, HashSet<Point> obstaclePositions)
        {
            return _collisionTiles.Contains(position) || obstaclePositions.Contains(position);
        }

        private void GenerateFallbackLayout(int minX, int minY, int maxX, int maxY)
        {
            Debug.Log("[PitGenerator] Generating fallback safe layout");
            
            var safeObstacles = new List<Point>(10)
            {
                new Point(minX, minY),
                new Point(maxX, minY),
                new Point(minX, maxY),
                new Point(maxX, maxY),
                new Point(minX + 1, minY),
                new Point(maxX - 1, minY),
                new Point(minX, minY + 1),
                new Point(maxX, minY + 1),
                new Point(minX + 2, maxY),
                new Point(maxX - 2, maxY)
            };

            var centerX = (minX + maxX) / 2;
            var centerY = (minY + maxY) / 2;
            
            var safeTargets = new List<(Point pos, int tag, Color color, string name)>(5)
            {
                (new Point(centerX, centerY), GameConfig.TAG_TREASURE, Color.Yellow, "treasure"),
                (new Point(centerX + 1, centerY), GameConfig.TAG_TREASURE, Color.Yellow, "treasure"),
                (new Point(centerX, centerY + 1), GameConfig.TAG_MONSTER, Color.Red, "monster"),
                (new Point(centerX - 1, centerY), GameConfig.TAG_MONSTER, Color.Red, "monster"),
                (new Point(centerX, centerY - 1), GameConfig.TAG_WIZARD_ORB, Color.Blue, "wizard_orb")
            };

            CreateEntitiesAtPositions(safeObstacles, GameConfig.TAG_OBSTACLE, Color.Gray, "obstacle");

            for (int i = 0; i < safeTargets.Count; i++)
            {
                var t = safeTargets[i];
                CreateEntitiesAtPositions(new List<Point>(1) { t.pos }, t.tag, t.color, t.name);
            }

            Debug.Log("[PitGenerator] Fallback layout created with guaranteed paths");
        }

        private Point GetRandomUnusedPosition(int minX, int minY, int maxX, int maxY, HashSet<Point> usedPositions)
        {
            int totalSpots = (maxX - minX + 1) * (maxY - minY + 1);
            int attempts = 0;
            int maxAttempts = totalSpots * 2;

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

            return new Point(-1, -1);
        }
    }
}