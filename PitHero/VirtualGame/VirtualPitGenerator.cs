using Microsoft.Xna.Framework;
using PitHero.AI.Interfaces;
using PitHero.Config;
using PitHero.ECS.Components;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Equipment;
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

        /// <summary>
        /// Party job context used to bias chest gear toward equipable kinds, mirroring
        /// the live PitGenerator.BuildLootJobContext weighting.  Leave default (empty)
        /// for flat pool selection (matches live behavior with no party present).
        /// </summary>
        public LootJobContext LootContext { get; set; }

        public VirtualPitGenerator(VirtualWorldState worldState, VirtualTiledMapService tiledMapService, VirtualPitWidthManager pitWidthManager)
        {
            _worldState = worldState;
            _tiledMapService = tiledMapService;
            _pitWidthManager = pitWidthManager;
        }

        public void RegenerateForCurrentLevel()
        {
            // Convert displayed level + tier to cumulative depth so RegenerateForLevel
            // receives the same contract as RunPitLevel does.
            int displayedLevel = _pitWidthManager.CurrentPitLevel;
            int tier           = _pitWidthManager.CurrentPitTier;
            int depth          = BiomeProgressionConfig.GetEffectiveDepth(displayedLevel, tier);
            RegenerateForLevel(depth);
        }

        public void RegenerateForLevel(int level)
        {
            // level is the CUMULATIVE DEPTH (1-N across all tiers).
            // Decompose into biome-local displayed level and tier for content selection;
            // retain the original depth for seeding and entity-count formulas.
            int displayedLevel = BiomeProgressionConfig.GetDisplayedLevelForDepth(level);
            int tier           = BiomeProgressionConfig.GetTierForDepth(level);

            Console.WriteLine($"[VirtualPitGenerator] Regenerating pit content for depth {level} (displayed={displayedLevel}, tier={tier})");

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
            GenerateEntitiesForLevel(level, displayedLevel, tier, validMinX, validMinY, validMaxX, validMaxY);
        }

        private void GenerateEntitiesForLevel(int depth, int displayedLevel, int tier, int minX, int minY, int maxX, int maxY)
        {
            // Entity count formulas scale with cumulative depth so tier-2+ floors have more content.
            int maxMonsters = Math.Clamp((int)Math.Round(2 + 8 * Math.Max(depth - 10, 0) / 90.0), 2, 10);
            int maxChests = Math.Clamp((int)Math.Round(2 + 8 * Math.Max(depth - 10, 0) / 90.0), 2, 10);
            int minObstacles = Math.Clamp((int)Math.Round(5 + 35 * Math.Max(depth - 10, 0) / 90.0), 5, 40);
            int maxObstacles = Math.Clamp((int)Math.Round(10 + 40 * Math.Max(depth - 10, 0) / 90.0), 10, 50);

            // Seed with cumulative depth so tier-2 floors differ from tier-1 at same displayed level.
            var random = new Random(depth); // Deterministic based on cumulative depth
            int obstacleCount = random.Next(minObstacles, maxObstacles + 1);
            int chestCount = random.Next(maxChests / 2, maxChests + 1);
            int monsterCount = random.Next(maxMonsters / 2, maxMonsters + 1);

            Console.WriteLine($"[VirtualPitGenerator] Level {depth} calculated amounts:");
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

            // Generate treasures with Cave Biome progression.
            // Biome keying uses displayedLevel; tier scaling is applied after item generation.
            // Live code uses Nez.Random (global); here we use the local deterministic
            // Random(depth) so pit layout and loot are both reproducible per cumulative depth,
            // independent of the combat RNG seed passed to VirtualGameSimulation.
            for (int i = 0; i < chestCount; i++)
            {
                var pos = GetRandomPosition(minX, minY, maxX, maxY, usedPositions, random);
                if (pos.HasValue)
                {
                    usedPositions.Add(pos.Value);
                    IItem item;
                    if (CaveBiomeConfig.IsCaveLevel(displayedLevel))
                    {
                        float roll = (float)random.NextDouble();
                        int treasureLevel = CaveBiomeConfig.DetermineCaveTreasureLevel(displayedLevel, roll);
                        var lootCtx = LootContext;
                        item = TreasureComponent.GenerateCaveItemForTreasureLevelDeterministic(treasureLevel, in lootCtx, random);
                    }
                    else
                    {
                        float roll = (float)random.NextDouble();
                        int treasureLevel = DetermineNonCaveTreasureLevelDeterministic(displayedLevel, roll);
                        item = TreasureComponent.GenerateItemForTreasureLevelDeterministic(treasureLevel, random);
                    }

                    // Apply tier scaling to gear drops.  Potions pass through unchanged.
                    if (tier >= 2 && item is RolePlayingFramework.Equipment.Gear gearItem)
                    {
                        int depthDelta = (tier - 1) * BiomeProgressionConfig.MaxBiomeLevel;
                        item = RolePlayingFramework.Equipment.Gear.CreateTierScaledCopy(gearItem, tier, depthDelta);
                    }

                    // AddTreasure(Point, IItem) stores the instance and keeps parity lists in sync.
                    _worldState.AddTreasure(pos.Value, item);
                }
            }

            // Enemy level uses effective depth so tier-2+ enemies are stronger than tier-1 at the same displayed level.
            int caveScaledEnemyLevel = CaveBiomeConfig.GetScaledEnemyLevelForPitLevel(displayedLevel, tier);
            bool isCaveBossFloor = CaveBiomeConfig.IsBossFloor(displayedLevel);

            // Generate boss marker first on cave boss floors
            if (isCaveBossFloor && monsterCount > 0)
            {
                var bossPos = GetRandomPosition(minX, minY, maxX, maxY, usedPositions, random);
                if (bossPos.HasValue)
                {
                    usedPositions.Add(bossPos.Value);
                    EnemyId bossId = GetBossEnemyIdForLevel(displayedLevel);
                    IEnemy bossEnemy = EnemyFactory.Create(bossId, caveScaledEnemyLevel);
                    // AddMonster(Point, IEnemy) routes to AddBossMonster internally when IsBoss=true
                    _worldState.AddMonster(bossPos.Value, bossEnemy);
                    monsterCount--;
                    Console.WriteLine($"[VirtualPitGenerator] Cave boss floor at displayed level {displayedLevel} (depth={depth}) with {bossEnemy.Name} (scaled level {caveScaledEnemyLevel})");
                }
            }

            // Generate monsters from Cave Biome pool if in cave range
            if (CaveBiomeConfig.IsCaveLevel(displayedLevel) && !isCaveBossFloor)
            {
                EnemyId[] enemyPool = CaveBiomeConfig.GetEnemyPoolForLevel(displayedLevel);
                for (int i = 0; i < monsterCount; i++)
                {
                    var pos = GetRandomPosition(minX, minY, maxX, maxY, usedPositions, random);
                    if (pos.HasValue)
                    {
                        usedPositions.Add(pos.Value);
                        EnemyId pickedId = enemyPool.Length > 0
                            ? enemyPool[random.Next(enemyPool.Length)]
                            : EnemyId.Slime;
                        IEnemy enemy = EnemyFactory.Create(pickedId, caveScaledEnemyLevel);
                        _worldState.AddMonster(pos.Value, enemy);
                    }
                }
            }
            else if (!isCaveBossFloor)
            {
                // Non-cave fallback (unreachable when all displayed levels are 1-25, kept for safety)
                for (int i = 0; i < monsterCount; i++)
                {
                    var pos = GetRandomPosition(minX, minY, maxX, maxY, usedPositions, random);
                    if (pos.HasValue)
                    {
                        usedPositions.Add(pos.Value);
                        IEnemy enemy = EnemyFactory.Create(EnemyId.Slime, caveScaledEnemyLevel);
                        _worldState.AddMonster(pos.Value, enemy);
                    }
                }
            }

            // Spawn traps per GameConfig.TrapMin/MaxPerFloor
            // Boss floors never have traps (the boss is the hazard)
            if (!isCaveBossFloor)
            {
                int trapCount = random.Next(GameConfig.TrapMinPerFloor, GameConfig.TrapMaxPerFloor + 1);
                for (int i = 0; i < trapCount; i++)
                {
                    var pos = GetRandomPosition(minX, minY, maxX, maxY, usedPositions, random);
                    if (pos.HasValue)
                    {
                        usedPositions.Add(pos.Value);
                        _worldState.AddTrapTile(pos.Value);
                    }
                }
                Console.WriteLine($"[VirtualPitGenerator] Spawned {trapCount} trap(s) for depth {depth}");
            }

            Console.WriteLine($"[VirtualPitGenerator] Generated {obstacles.Count} obstacles, {chestCount} treasures, {monsterCount} monsters, and 1 wizard orb");
        }

        /// <summary>
        /// Returns the <see cref="EnemyId"/> for the Cave Biome boss at the given pit level.
        /// Mirrors the live <c>PitGenerator</c> boss mapping exactly:
        /// 5 = StoneGuardian, 10 = EarthElemental, 15 = AncientWyrm, 20 = PitLord, 25 = MoltenTitan.
        /// </summary>
        private EnemyId GetBossEnemyIdForLevel(int level)
        {
            switch (level)
            {
                case 5:  return EnemyId.StoneGuardian;
                case 10: return EnemyId.EarthElemental;
                case 15: return EnemyId.AncientWyrm;
                case 20: return EnemyId.PitLord;
                case 25: return EnemyId.MoltenTitan;
                default: return EnemyId.PitLord;
            }
        }

        /// <summary>
        /// Determines a treasure level for non-cave pit levels using the same probability
        /// distribution as <see cref="TreasureComponent.DetermineTreasureLevel"/> but with
        /// the caller-supplied <see cref="System.Random"/> instead of <c>Nez.Random</c>.
        /// Mirrors the live switch table exactly so balance matches the live game.
        /// </summary>
        private static int DetermineNonCaveTreasureLevelDeterministic(int pitLevel, float roll)
        {
            if (pitLevel <= 10) return 1;
            if (pitLevel <= 30) return roll < 0.8f ? 1 : 2;
            if (pitLevel <= 60)
            {
                if (roll < 0.7f) return 1;
                if (roll < 0.9f) return 2;
                return 3;
            }
            if (pitLevel <= 90)
            {
                if (roll < 0.55f) return 1;
                if (roll < 0.8f)  return 2;
                if (roll < 0.95f) return 3;
                return 4;
            }
            if (roll < 0.4f)  return 1;
            if (roll < 0.7f)  return 2;
            if (roll < 0.9f)  return 3;
            if (roll < 0.99f) return 4;
            return 5;
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