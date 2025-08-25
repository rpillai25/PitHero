using Microsoft.Xna.Framework;
using PitHero.AI.Interfaces;
using System;
using System.Collections.Generic;

namespace PitHero.VirtualGame
{
    /// <summary>
    /// Virtual implementation of IPitGenerator for virtual layer
    /// Uses actual PitGenerator logic but operates on virtual world state
    /// </summary>
    public class VirtualPitGenerator : IPitGenerator
    {
        private readonly VirtualWorldState _worldState;
        private readonly VirtualTiledMapService _tiledMapService;
        private readonly VirtualPitWidthManager _pitWidthManager;

        public VirtualPitGenerator(VirtualWorldState worldState, VirtualTiledMapService tiledMapService, VirtualPitWidthManager pitWidthManager)
        {
            _worldState = worldState;
            _tiledMapService = tiledMapService;
            _pitWidthManager = pitWidthManager;
        }

        public void RegenerateForCurrentLevel()
        {
            var currentLevel = _pitWidthManager.CurrentPitLevel;
            RegenerateForLevel(currentLevel);
        }

        public void RegenerateForLevel(int level)
        {
            Console.WriteLine($"[VirtualPitGenerator] Regenerating pit content for level {level}");
            
            // Clear existing entities
            _worldState.ClearAllEntities();
            
            // Use PitWidthManager for dynamic bounds
            int validMinX, validMinY, validMaxX, validMaxY;
            
            if (_pitWidthManager.CurrentPitRightEdge > 0)
            {
                validMinX = GameConfig.PitRectX + 1; // 2
                validMinY = GameConfig.PitRectY + 1; // 3
                validMaxX = _pitWidthManager.CurrentPitRightEdge - 3; // 3 tiles from right edge
                validMaxY = GameConfig.PitRectY + GameConfig.PitRectHeight - 2; // 9
            }
            else
            {
                validMinX = GameConfig.PitRectX + 1; // 2
                validMinY = GameConfig.PitRectY + 1; // 3
                validMaxX = GameConfig.PitRectX + GameConfig.PitRectWidth - 3; // 10
                validMaxY = GameConfig.PitRectY + GameConfig.PitRectHeight - 2; // 9
            }
            
            Console.WriteLine($"[VirtualPitGenerator] Valid placement area for level {level}: tiles ({validMinX},{validMinY}) to ({validMaxX},{validMaxY})");

            // Generate entities using same logic as real PitGenerator
            GenerateEntitiesForLevel(level, validMinX, validMinY, validMaxX, validMaxY);
        }

        private void GenerateEntitiesForLevel(int level, int minX, int minY, int maxX, int maxY)
        {
            // Calculate entity counts based on level (same as real PitGenerator)
            int maxMonsters = Math.Clamp((int)Math.Round(2 + 8 * Math.Max(level - 10, 0) / 90.0), 2, 10);
            int maxChests = Math.Clamp((int)Math.Round(2 + 8 * Math.Max(level - 10, 0) / 90.0), 2, 10);
            int minObstacles = Math.Clamp((int)Math.Round(5 + 35 * Math.Max(level - 10, 0) / 90.0), 5, 40);
            int maxObstacles = Math.Clamp((int)Math.Round(10 + 40 * Math.Max(level - 10, 0) / 90.0), 10, 50);
            
            // Calculate actual entity counts with variance
            var random = new Random(level); // Deterministic based on level
            int obstacleCount = random.Next(minObstacles, maxObstacles + 1);
            int chestCount = random.Next(maxChests / 2, maxChests + 1);
            int monsterCount = random.Next(maxMonsters / 2, maxMonsters + 1);
            
            Console.WriteLine($"[VirtualPitGenerator] Level {level} calculated amounts:");
            Console.WriteLine($"[VirtualPitGenerator]   Max Monsters: {maxMonsters}, Actual: {monsterCount}");
            Console.WriteLine($"[VirtualPitGenerator]   Max Chests: {maxChests}, Actual: {chestCount}");
            Console.WriteLine($"[VirtualPitGenerator]   Min Obstacles: {minObstacles}");
            Console.WriteLine($"[VirtualPitGenerator]   Max Obstacles: {maxObstacles}, Actual: {obstacleCount}");

            var usedPositions = new HashSet<Point>();
            
            // Generate wizard orb (always 1)
            var wizardOrbPos = GetRandomPosition(minX, minY, maxX, maxY, usedPositions, random);
            if (wizardOrbPos.HasValue)
            {
                _worldState.SetWizardOrbPosition(wizardOrbPos.Value);
                usedPositions.Add(wizardOrbPos.Value);
                Console.WriteLine($"[VirtualPitGenerator] Generated wizard orb at ({wizardOrbPos.Value.X},{wizardOrbPos.Value.Y})");
            }
            
            // Generate obstacles
            var obstacles = new List<Point>();
            for (int i = 0; i < obstacleCount; i++)
            {
                var pos = GetRandomPosition(minX, minY, maxX, maxY, usedPositions, random);
                if (pos.HasValue)
                {
                    obstacles.Add(pos.Value);
                    usedPositions.Add(pos.Value);
                    _worldState.AddObstacle(pos.Value);
                }
            }
            
            // Generate treasures
            for (int i = 0; i < chestCount; i++)
            {
                var pos = GetRandomPosition(minX, minY, maxX, maxY, usedPositions, random);
                if (pos.HasValue)
                {
                    usedPositions.Add(pos.Value);
                    _worldState.AddTreasure(pos.Value);
                }
            }
            
            // Generate monsters
            for (int i = 0; i < monsterCount; i++)
            {
                var pos = GetRandomPosition(minX, minY, maxX, maxY, usedPositions, random);
                if (pos.HasValue)
                {
                    usedPositions.Add(pos.Value);
                    _worldState.AddMonster(pos.Value);
                }
            }
            
            Console.WriteLine($"[VirtualPitGenerator] Generated {obstacles.Count} obstacles, {chestCount} treasures, {monsterCount} monsters, and 1 wizard orb");
        }

        private Point? GetRandomPosition(int minX, int minY, int maxX, int maxY, HashSet<Point> usedPositions, Random random)
        {
            for (int attempts = 0; attempts < 100; attempts++)
            {
                var x = random.Next(minX, maxX + 1);
                var y = random.Next(minY, maxY + 1);
                var pos = new Point(x, y);
                
                if (!usedPositions.Contains(pos) && _worldState.IsPassable(pos))
                {
                    return pos;
                }
            }
            return null;
        }
    }
}