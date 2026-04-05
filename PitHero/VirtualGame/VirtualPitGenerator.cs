using Microsoft.Xna.Framework;
using PitHero.AI.Interfaces;
using PitHero.Config;
using PitHero.ECS.Components;
using RolePlayingFramework.Enemies;
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

            // Generate treasures with Cave Biome progression
            for (int i = 0; i < chestCount; i++)
            {
                var pos = GetRandomPosition(minX, minY, maxX, maxY, usedPositions, random);
                if (pos.HasValue)
                {
                    usedPositions.Add(pos.Value);
                    
                    // Use Cave Biome treasure level if in cave range, otherwise use default
                    int treasureLevel;
                    string equipmentType;
                    if (CaveBiomeConfig.IsCaveLevel(level))
                    {
                        float roll = (float)random.NextDouble();
                        treasureLevel = CaveBiomeConfig.DetermineCaveTreasureLevel(level, roll);
                        equipmentType = GetRandomEquipmentType(level, treasureLevel, random);
                    }
                    else
                    {
                        treasureLevel = TreasureComponent.DetermineTreasureLevel(level);
                        equipmentType = "GenericEquipment";
                    }
                    
                    _worldState.AddTreasure(pos.Value, equipmentType, treasureLevel);
                }
            }

            int caveScaledEnemyLevel = CaveBiomeConfig.GetScaledEnemyLevelForPitLevel(level);
            bool isCaveBossFloor = CaveBiomeConfig.IsBossFloor(level);

            // Generate boss marker first on cave boss floors
            if (isCaveBossFloor && monsterCount > 0)
            {
                var bossPos = GetRandomPosition(minX, minY, maxX, maxY, usedPositions, random);
                if (bossPos.HasValue)
                {
                    usedPositions.Add(bossPos.Value);
                    string bossType = GetBossTypeForLevel(level);
                    _worldState.AddBossMonster(bossPos.Value, bossType);
                    monsterCount--;
                    Console.WriteLine($"[VirtualPitGenerator] Cave boss floor at level {level} with {bossType} (scaled level {caveScaledEnemyLevel})");
                }
            }

            // Generate monsters from Cave Biome pool if in cave range
            if (CaveBiomeConfig.IsCaveLevel(level) && !isCaveBossFloor)
            {
                EnemyId[] enemyPool = CaveBiomeConfig.GetEnemyPoolForLevel(level);
                for (int i = 0; i < monsterCount; i++)
                {
                    var pos = GetRandomPosition(minX, minY, maxX, maxY, usedPositions, random);
                    if (pos.HasValue)
                    {
                        usedPositions.Add(pos.Value);
                        string monsterType = enemyPool.Length > 0 ? enemyPool[random.Next(enemyPool.Length)].ToString() : "GenericMonster";
                        _worldState.AddMonster(pos.Value, monsterType);
                    }
                }
            }
            else if (!isCaveBossFloor)
            {
                // Non-cave levels use generic monster spawning
                for (int i = 0; i < monsterCount; i++)
                {
                    var pos = GetRandomPosition(minX, minY, maxX, maxY, usedPositions, random);
                    if (pos.HasValue)
                    {
                        usedPositions.Add(pos.Value);
                        _worldState.AddMonster(pos.Value, "GenericMonster");
                    }
                }
            }

            Console.WriteLine($"[VirtualPitGenerator] Generated {obstacles.Count} obstacles, {chestCount} treasures, {monsterCount} monsters, and 1 wizard orb");
        }

        /// <summary>
        /// Gets the boss type for a specific Cave Biome boss floor.
        /// </summary>
        private string GetBossTypeForLevel(int level)
        {
            // Cave biome boss progression: unique bosses at each major milestone
            switch (level)
            {
                case 5:
                    return "Stone Guardian";
                case 10:
                    return "Pit Lord";
                case 15:
                    return "Earth Elemental";
                case 20:
                    return "Molten Titan";
                case 25:
                    return "Ancient Wyrm";
                default:
                    return "Pit Lord";
            }
        }

        /// <summary>
        /// Gets a random equipment type from appropriate Cave Biome spawn pool.
        /// Virtual stub - actual equipment pool logic will be implemented by Principal Game Engineer.
        /// </summary>
        private string GetRandomEquipmentType(int level, int treasureLevel, Random random)
        {
            // Simplified equipment pool logic - stub implementation for virtual layer
            // Actual implementation will have 135 equipment pieces with spawn windows
            
            // Equipment categories
            string[] categories = { "Sword", "Axe", "Dagger", "Spear", "Hammer", "Staff", 
                                   "Armor", "Shield", "Helm" };
            
            // Select random category
            string category = categories[random.Next(categories.Length)];
            
            // Determine rarity suffix based on treasure level
            string raritySuffix = treasureLevel == 1 ? "Normal" : "Uncommon";
            
            // Determine pit tier for equipment naming
            string tierPrefix;
            if (level <= 5)
                tierPrefix = "Early";
            else if (level <= 10)
                tierPrefix = "Mid";
            else if (level <= 15)
                tierPrefix = "Late";
            else if (level <= 20)
                tierPrefix = "Advanced";
            else
                tierPrefix = "Elite";
            
            return $"{tierPrefix}{category}_{raritySuffix}";
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