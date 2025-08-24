using Microsoft.Xna.Framework;
using Nez;
using Nez.Tiled;
using PitHero;
using PitHero.Util;
using PitHero.ECS.Components;
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
        private HashSet<Point> _collisionTiles;

        public PitGenerator(Scene scene)
        {
            _scene = scene;
            _collisionTiles = new HashSet<Point>(64);
        }

        /// <summary>
        /// Calculate maximum number of monsters based on pit level
        /// </summary>
        private int MaxMonsters(int level)
        {
            return Math.Clamp(
                (int)Math.Round(2 + 8 * Math.Max(level - 10, 0) / 90.0), 2, 10);
        }

        /// <summary>
        /// Calculate maximum number of chests/treasures based on pit level
        /// </summary>
        private int MaxChests(int level)
        {
            return Math.Clamp(
                (int)Math.Round(2 + 8 * Math.Max(level - 10, 0) / 90.0), 2, 10);
        }

        /// <summary>
        /// Calculate minimum number of obstacles based on pit level
        /// </summary>
        private int MinObstacles(int level)
        {
            return Math.Clamp(
                (int)Math.Round(5 + 35 * Math.Max(level - 10, 0) / 90.0), 5, 40);
        }

        /// <summary>
        /// Calculate maximum number of obstacles based on pit level
        /// </summary>
        private int MaxObstacles(int level)
        {
            return Math.Clamp(
                (int)Math.Round(10 + 40 * Math.Max(level - 10, 0) / 90.0), 10, 50);
        }

        /// <summary>
        /// Clear all existing pit entities and regenerate content for the current pit level
        /// </summary>
        public void RegenerateForCurrentLevel()
        {
            // Get current pit level from PitWidthManager
            var pitWidthManager = Core.Services.GetService<PitWidthManager>();
            if (pitWidthManager == null)
            {
                Debug.Warn("[PitGenerator] PitWidthManager service not found, using level 1");
                RegenerateForLevel(1);
                return;
            }

            int currentLevel = pitWidthManager.CurrentPitLevel;
            RegenerateForLevel(currentLevel);
        }

        /// <summary>
        /// Clear all existing pit entities and regenerate content for the specified level
        /// </summary>
        public void RegenerateForLevel(int level)
        {
            Debug.Log($"[PitGenerator] Regenerating pit content for level {level}");
            
            // Clear existing pit entities
            ClearExistingPitEntities();
            
            // Clear obstacle walls from A* graph
            ClearObstacleWallsFromAstar();
            
            // Generate new content
            GenerateForLevel(level);
            
            // Update hero pathfinding target if hero exists
            UpdateHeroPathfindingTarget();
        }

        /// <summary>
        /// Clear all existing pit entities (obstacles, treasures, monsters, wizard orbs)
        /// </summary>
        private void ClearExistingPitEntities()
        {
            Debug.Log("[PitGenerator] Clearing existing pit entities");
            
            var entitiesToRemove = new List<Entity>();
            
            // Find all entities with pit-related tags using FindEntitiesWithTag
            var obstacles = _scene.FindEntitiesWithTag(GameConfig.TAG_OBSTACLE);
            var treasures = _scene.FindEntitiesWithTag(GameConfig.TAG_TREASURE);
            var monsters = _scene.FindEntitiesWithTag(GameConfig.TAG_MONSTER);
            var wizardOrbs = _scene.FindEntitiesWithTag(GameConfig.TAG_WIZARD_ORB);
            
            // Add all found entities to removal list
            entitiesToRemove.AddRange(obstacles);
            entitiesToRemove.AddRange(treasures);
            entitiesToRemove.AddRange(monsters);
            entitiesToRemove.AddRange(wizardOrbs);
            
            // Remove entities by calling Destroy on each
            for (int i = 0; i < entitiesToRemove.Count; i++)
            {
                entitiesToRemove[i].Destroy();
            }
            
            Debug.Log($"[PitGenerator] Cleared {entitiesToRemove.Count} existing pit entities");
        }

        /// <summary>
        /// Clear obstacle walls from hero's A* graph
        /// </summary>
        private void ClearObstacleWallsFromAstar()
        {
            // Find hero entity to access its pathfinding component
            var hero = _scene.FindEntity("hero");
            if (hero == null)
            {
                Debug.Warn("[PitGenerator] No hero found when clearing obstacle walls");
                return;
            }

            var heroComponent = hero.GetComponent<HeroComponent>();
            if (heroComponent == null || !heroComponent.IsPathfindingInitialized)
            {
                Debug.Warn("[PitGenerator] Hero pathfinding not initialized when clearing obstacle walls");
                return;
            }
            
            // Refresh the hero's pathfinding to clear dynamically added obstacles
            heroComponent.RefreshPathfinding();
            
            Debug.Log($"[PitGenerator] Hero pathfinding refreshed with {heroComponent.PathfindingGraph.Walls.Count} walls from collision layer");
        }

        /// <summary>
        /// Update hero pathfinding target after regeneration
        /// </summary>
        private void UpdateHeroPathfindingTarget()
        {
            // Find hero entity
            var hero = _scene.FindEntity("hero");
            if (hero == null)
            {
                Debug.Log("[PitGenerator] No hero found to update pathfinding target");
                return;
            }

            // Force refresh of A* graph to ensure hero has up-to-date pathfinding information
            //RefreshAstarGraph();

            // Get HeroGoapAgentComponent to potentially reset current action
            var agentComponent = hero.GetComponent<HeroGoapAgentComponent>();
            if (agentComponent != null)
            {
                Debug.Log("[PitGenerator] Hero agent component found - pathfinding will be updated on next action plan");
            }
            
            Debug.Log("[PitGenerator] Hero pathfinding target update complete");
        }

        /// <summary>
        /// Generate pit content for any level using dynamic calculations
        /// </summary>
        private void GenerateForLevel(int level)
        {
            // Calculate pit bounds using PitWidthManager if available
            var pitWidthManager = Core.Services.GetService<PitWidthManager>();
            int validMinX, validMinY, validMaxX, validMaxY;
            
            if (pitWidthManager != null && pitWidthManager.CurrentPitRightEdge > 0)
            {
                // Use dynamic pit bounds
                validMinX = GameConfig.PitRectX + 1; // 2
                validMinY = GameConfig.PitRectY + 1; // 3
                validMaxX = pitWidthManager.CurrentPitRightEdge - 3; // 2 tiles from right edge
                validMaxY = GameConfig.PitRectHeight - 2; // 9
            }
            else
            {
                // Use default pit bounds
                validMinX = GameConfig.PitRectX + 1; // 2
                validMinY = GameConfig.PitRectY + 1; // 3
                validMaxX = GameConfig.PitRectX + GameConfig.PitRectWidth - 3; // 10
                validMaxY = GameConfig.PitRectY + GameConfig.PitRectHeight - 2; // 9
            }
            
            Debug.Log($"[PitGenerator] Valid placement area for level {level}: tiles ({validMinX},{validMinY}) to ({validMaxX},{validMaxY})");

            // Calculate entity counts based on level
            int maxMonsters = MaxMonsters(level);
            int maxChests = MaxChests(level);
            int minObstacles = MinObstacles(level);
            int maxObstacles = MaxObstacles(level);
            
            // Calculate actual entity counts with variance
            int obstacleCount = Nez.Random.Range(minObstacles, maxObstacles + 1);
            int chestCount = Nez.Random.Range(maxChests / 2, maxChests + 1);
            int monsterCount = Nez.Random.Range(maxMonsters / 2, maxMonsters + 1);
            
            Debug.Log($"[PitGenerator] Level {level} calculated amounts:");
            Debug.Log($"[PitGenerator]   Max Monsters: {maxMonsters}, Actual: {monsterCount}");
            Debug.Log($"[PitGenerator]   Max Chests: {maxChests}, Actual: {chestCount}");
            Debug.Log($"[PitGenerator]   Min Obstacles: {minObstacles}");
            Debug.Log($"[PitGenerator]   Max Obstacles: {maxObstacles}, Actual: {obstacleCount}");

            InitializeCollisionTiles(validMinX, validMinY, validMaxX, validMaxY);

            var maxAttempts = 10;
            bool validLayoutGenerated = false;

            for (int attempt = 1; attempt <= maxAttempts && !validLayoutGenerated; attempt++)
            {
                Debug.Log($"[PitGenerator] Generation attempt {attempt}");
                
                var usedPositions = new HashSet<Point>(64);
                var obstaclePositions = new HashSet<Point>(16);
                var targetPositions = new List<Point>(8);

                var obstacles = GenerateEntityPositions(obstacleCount, validMinX, validMinY, validMaxX, validMaxY, usedPositions, "obstacles");
                obstaclePositions.UnionWith(obstacles);
                usedPositions.UnionWith(obstacles);

                var treasures = GenerateEntityPositions(chestCount, validMinX, validMinY, validMaxX, validMaxY, usedPositions, "treasures");
                var monsters = GenerateEntityPositions(monsterCount, validMinX, validMinY, validMaxX, validMaxY, usedPositions, "monsters");
                var wizardOrbs = GenerateEntityPositions(1, validMinX, validMinY, validMaxX, validMaxY, usedPositions, "wizard orbs"); // Always 1 wizard orb

                // Manual AddRange without LINQ
                for (int i = 0; i < treasures.Count; i++) targetPositions.Add(treasures[i]);
                for (int i = 0; i < monsters.Count; i++) targetPositions.Add(monsters[i]);
                for (int i = 0; i < wizardOrbs.Count; i++) targetPositions.Add(wizardOrbs[i]);

                // Remove problematic obstacles to ensure all targets are reachable
                var validObstacles = EnsureTargetsReachable(obstaclePositions, targetPositions, validMinX, validMinY, validMaxX, validMaxY);

                if (validObstacles.Count > 0 || targetPositions.Count == 0)
                {
                    CreateEntitiesAtPositions(validObstacles, GameConfig.TAG_OBSTACLE, Color.Gray, "obstacle");
                    CreateEntitiesAtPositions(treasures, GameConfig.TAG_TREASURE, Color.Yellow, "treasure");
                    CreateEntitiesAtPositions(monsters, GameConfig.TAG_MONSTER, Color.Red, "monster");
                    CreateEntitiesAtPositions(wizardOrbs, GameConfig.TAG_WIZARD_ORB, Color.Blue, "wizard_orb");

                    validLayoutGenerated = true;
                    Debug.Log($"[PitGenerator] Valid layout generated on attempt {attempt}");
                    Debug.Log($"[PitGenerator] Generated {validObstacles.Count + targetPositions.Count} entities total in pit");
                    Debug.Log($"[PitGenerator] Final obstacle count: {validObstacles.Count} (removed {obstacles.Count - validObstacles.Count} problematic obstacles)");
                }
                else
                {
                    Debug.Log($"[PitGenerator] Attempt {attempt} failed - could not create valid layout");
                }
            }

            if (!validLayoutGenerated)
            {
                Debug.Log($"[PitGenerator] Warning: Could not generate valid layout after {maxAttempts} attempts");
                GenerateFallbackLayout(validMinX, validMinY, validMaxX, validMaxY);
            }
        }

        /// <summary>
        /// Remove obstacles that block access to targets or create enclosed areas
        /// </summary>
        private List<Point> EnsureTargetsReachable(HashSet<Point> obstaclePositions, List<Point> targetPositions, int minX, int minY, int maxX, int maxY)
        {
            if (targetPositions.Count == 0)
            {
                Debug.Log("[PitGenerator] No targets to validate, keeping all obstacles");
                return new List<Point>(obstaclePositions);
            }

            var workingObstacles = new HashSet<Point>(obstaclePositions);
            var removedObstacles = new List<Point>(16);
            int maxRemovalAttempts = obstaclePositions.Count;
            
            Debug.Log($"[PitGenerator] Starting obstacle removal validation with {workingObstacles.Count} obstacles and {targetPositions.Count} targets");

            for (int attempt = 0; attempt < maxRemovalAttempts; attempt++)
            {
                var pitEntryPoint = FindPitEntryPoint(workingObstacles, minX, minY, maxX, maxY);
                if (!pitEntryPoint.HasValue)
                {
                    Debug.Log("[PitGenerator] No pit entry point found, removing random obstacle");
                    var randomObstacle = GetRandomObstacle(workingObstacles);
                    if (randomObstacle.HasValue)
                    {
                        workingObstacles.Remove(randomObstacle.Value);
                        removedObstacles.Add(randomObstacle.Value);
                    }
                    continue;
                }

                // Find unreachable targets
                var unreachableTargets = new List<Point>(8);
                for (int i = 0; i < targetPositions.Count; i++)
                {
                    var target = targetPositions[i];
                    if (!IsPathExists(pitEntryPoint.Value, target, workingObstacles, minX, minY, maxX, maxY))
                    {
                        unreachableTargets.Add(target);
                    }
                }

                // Find enclosed areas (areas with no path to pit entry)
                var enclosedAreas = FindEnclosedAreas(workingObstacles, pitEntryPoint.Value, minX, minY, maxX, maxY);

                if (unreachableTargets.Count == 0 && enclosedAreas.Count == 0)
                {
                    Debug.Log($"[PitGenerator] All targets reachable and no enclosed areas found after {attempt} obstacle removals");
                    break;
                }

                // Remove the most problematic obstacle
                var obstacleToRemove = FindMostProblematicObstacle(workingObstacles, unreachableTargets, enclosedAreas, pitEntryPoint.Value, minX, minY, maxX, maxY);
                if (obstacleToRemove.HasValue)
                {
                    workingObstacles.Remove(obstacleToRemove.Value);
                    removedObstacles.Add(obstacleToRemove.Value);
                    Debug.Log($"[PitGenerator] Removed problematic obstacle at ({obstacleToRemove.Value.X},{obstacleToRemove.Value.Y})");
                }
                else
                {
                    Debug.Log("[PitGenerator] Could not identify problematic obstacle to remove");
                    break;
                }
            }

            Debug.Log($"[PitGenerator] Obstacle removal complete. Removed {removedObstacles.Count} obstacles");
            return new List<Point>(workingObstacles);
        }

        /// <summary>
        /// Find areas that are completely enclosed by obstacles
        /// </summary>
        private List<Point> FindEnclosedAreas(HashSet<Point> obstaclePositions, Point entryPoint, int minX, int minY, int maxX, int maxY)
        {
            var reachableAreas = new HashSet<Point>(64);
            var queue = new Queue<Point>(32);
            
            queue.Enqueue(entryPoint);
            reachableAreas.Add(entryPoint);

            var directions = new Point[]
            {
                new Point(0, 1), new Point(0, -1), new Point(1, 0), new Point(-1, 0)
            };

            // Flood fill from entry point to find all reachable areas
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                
                for (int i = 0; i < 4; i++)
                {
                    var dir = directions[i];
                    var next = new Point(current.X + dir.X, current.Y + dir.Y);
                    
                    if (next.X < minX || next.X > maxX || next.Y < minY || next.Y > maxY)
                        continue;
                    
                    if (reachableAreas.Contains(next))
                        continue;
                    
                    if (IsBlocked(next, obstaclePositions))
                        continue;
                    
                    queue.Enqueue(next);
                    reachableAreas.Add(next);
                }
            }

            // Find enclosed areas (open spaces not reachable from entry point)
            var enclosedAreas = new List<Point>(16);
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    var point = new Point(x, y);
                    if (!IsBlocked(point, obstaclePositions) && !reachableAreas.Contains(point))
                    {
                        enclosedAreas.Add(point);
                    }
                }
            }

            if (enclosedAreas.Count > 0)
            {
                Debug.Log($"[PitGenerator] Found {enclosedAreas.Count} enclosed area tiles");
            }

            return enclosedAreas;
        }

        /// <summary>
        /// Find the obstacle that is blocking the most targets or contributing to enclosed areas
        /// </summary>
        private Point? FindMostProblematicObstacle(HashSet<Point> obstaclePositions, List<Point> unreachableTargets, List<Point> enclosedAreas, Point entryPoint, int minX, int minY, int maxX, int maxY)
        {
            Point? bestObstacle = null;
            int bestScore = 0;

            foreach (var obstacle in obstaclePositions)
            {
                // Temporarily remove this obstacle
                var tempObstacles = new HashSet<Point>(obstaclePositions);
                tempObstacles.Remove(obstacle);

                int score = 0;

                // Count how many previously unreachable targets become reachable
                for (int i = 0; i < unreachableTargets.Count; i++)
                {
                    var target = unreachableTargets[i];
                    if (IsPathExists(entryPoint, target, tempObstacles, minX, minY, maxX, maxY))
                    {
                        score += 10; // High weight for making targets reachable
                    }
                }

                // Count how many previously enclosed areas become reachable
                for (int i = 0; i < enclosedAreas.Count; i++)
                {
                    var area = enclosedAreas[i];
                    if (IsPathExists(entryPoint, area, tempObstacles, minX, minY, maxX, maxY))
                    {
                        score += 1; // Lower weight for opening enclosed areas
                    }
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestObstacle = obstacle;
                }
            }

            return bestObstacle;
        }

        /// <summary>
        /// Get a random obstacle from the set
        /// </summary>
        private Point? GetRandomObstacle(HashSet<Point> obstaclePositions)
        {
            if (obstaclePositions.Count == 0)
                return null;

            int randomIndex = Nez.Random.Range(0, obstaclePositions.Count);
            int currentIndex = 0;
            
            foreach (var obstacle in obstaclePositions)
            {
                if (currentIndex == randomIndex)
                    return obstacle;
                currentIndex++;
            }

            return null;
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

                var entity = _scene.CreateEntity(entityTypeName);
                entity.SetTag(tag);
                entity.SetPosition(worldPos);

                var renderer = entity.AddComponent(new PrototypeSpriteRenderer(GameConfig.TileSize, GameConfig.TileSize));
                renderer.Color = color;
                renderer.SetRenderLayer(GameConfig.RenderLayerActors);

                var collider = entity.AddComponent(new BoxCollider(GameConfig.TileSize, GameConfig.TileSize));

                if (tag == GameConfig.TAG_OBSTACLE)
                {
                    // Obstacles block both physics and pathfinding
                    // Find hero entity to add wall to its pathfinding graph
                    var hero = _scene.FindEntity("hero");
                    if (hero != null)
                    {
                        var heroComponent = hero.GetComponent<HeroComponent>();
                        if (heroComponent != null && heroComponent.IsPathfindingInitialized)
                        {
                            heroComponent.AddWall(tilePos);
                            Debug.Log($"[PitGenerator] Added obstacle tile to hero pathfinding at ({tilePos.X},{tilePos.Y})");
                        }
                        else
                        {
                            Debug.Log($"[PitGenerator] Hero found but pathfinding not initialized for obstacle at ({tilePos.X},{tilePos.Y})");
                        }
                    }
                    else
                    {
                        Debug.Log($"[PitGenerator] No hero found when creating obstacle at ({tilePos.X},{tilePos.Y}) - will be added to pathfinding when hero spawns");
                    }
                    // Leave collider defaults so hero collides with obstacle (physics layer 0)
                }
                else
                {
                    // Non-obstacles should NOT block movement. Make them triggers and put them on a layer the hero doesn't collide with.
                    collider.IsTrigger = true;
                    Flags.SetFlagExclusive(ref collider.PhysicsLayer, GameConfig.PhysicsHeroWorldLayer);
                }

                Debug.Log($"[PitGenerator] Created {entityTypeName} at tile ({tilePos.X},{tilePos.Y}), world ({worldPos.X},{worldPos.Y})");
            }
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
                int x = Nez.Random.Range(minX, maxX + 1);
                int y = Nez.Random.Range(minY, maxY + 1);
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