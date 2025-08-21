using Microsoft.Xna.Framework;
using Nez;
using System;
using System.Collections.Generic;

namespace PitHero
{
    /// <summary>
    /// Generates pit content including obstacles, treasures, monsters, and wizard orbs
    /// </summary>
    public class PitGenerator
    {
        private Scene _scene;
        private System.Random _random;

        public PitGenerator(Scene scene)
        {
            _scene = scene;
            _random = new System.Random();
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
            // Valid interior: (2,3) to (11,9) in tiles  
            var validMinX = GameConfig.PitRectX + 1; // 2
            var validMinY = GameConfig.PitRectY + 1; // 3
            var validMaxX = GameConfig.PitRectX + GameConfig.PitRectWidth - 2; // 11
            var validMaxY = GameConfig.PitRectY + GameConfig.PitRectHeight - 2; // 9
            
            Debug.Log($"[PitGenerator] Valid placement area: tiles ({validMinX},{validMinY}) to ({validMaxX},{validMaxY})");

            // Keep track of used positions to avoid overlaps
            var usedPositions = new HashSet<Point>();

            // Generate 10 obstacles (gray)
            GenerateEntities(10, GameConfig.TAG_OBSTACLE, Color.Gray, validMinX, validMinY, validMaxX, validMaxY, usedPositions);

            // Generate 2 treasures (yellow)
            GenerateEntities(2, GameConfig.TAG_TREASURE, Color.Yellow, validMinX, validMinY, validMaxX, validMaxY, usedPositions);

            // Generate 2 monsters (red)
            GenerateEntities(2, GameConfig.TAG_MONSTER, Color.Red, validMinX, validMinY, validMaxX, validMaxY, usedPositions);

            // Generate 1 wizard orb (blue)
            GenerateEntities(1, GameConfig.TAG_WIZARD_ORB, Color.Blue, validMinX, validMinY, validMaxX, validMaxY, usedPositions);

            Debug.Log($"[PitGenerator] Generated {usedPositions.Count} entities total in pit");
        }

        private void GenerateEntities(int count, int tag, Color color, int minX, int minY, int maxX, int maxY, HashSet<Point> usedPositions)
        {
            var entityTypeName = GetEntityTypeName(tag);
            Debug.Log($"[PitGenerator] Generating {count} {entityTypeName} entities");

            for (int i = 0; i < count; i++)
            {
                Point tilePos = GetRandomUnusedPosition(minX, minY, maxX, maxY, usedPositions);
                if (tilePos.X == -1) // No valid position found
                {
                    Debug.Log($"[PitGenerator] Warning: Could not find valid position for {entityTypeName} {i + 1}");
                    continue;
                }

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

                Debug.Log($"[PitGenerator] Created {entityTypeName} at tile ({tilePos.X},{tilePos.Y}), world ({worldPos.X},{worldPos.Y})");

                usedPositions.Add(tilePos);
            }
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

                if (!usedPositions.Contains(pos))
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