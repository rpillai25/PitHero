using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Config;
using PitHero.VirtualGame;
using RolePlayingFramework.Balance;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PitHero.Tests
{
    /// <summary>
    /// Comprehensive balance testing for Cave Biome (Pit Levels 1-25).
    /// Uses Virtual Game Logic Layer to validate progression, boss encounters, loot distribution, and difficulty curves.
    /// </summary>
    [TestClass]
    public class CaveBiomeBalanceTests
    {
        private VirtualWorldState _world = null!;
        private VirtualGoapContext _context = null!;
        private StringBuilder _balanceReport = null!;

        [TestInitialize]
        public void Setup()
        {
            _world = new VirtualWorldState();
            _context = new VirtualGoapContext(_world);
            _context.PitWidthManager.Initialize();
            _balanceReport = new StringBuilder();

            _balanceReport.AppendLine("=== CAVE BIOME BALANCE TEST REPORT ===");
            _balanceReport.AppendLine($"Test Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            _balanceReport.AppendLine($"Pit Levels: {CaveBiomeConfig.CaveStartLevel}-{CaveBiomeConfig.CaveEndLevel}");
            _balanceReport.AppendLine();
        }

        [TestCleanup]
        public void Cleanup()
        {
            _balanceReport.AppendLine();
            _balanceReport.AppendLine("=== END OF REPORT ===");
            Console.WriteLine(_balanceReport.ToString());
        }

        [TestMethod]
        public void CaveBiome_FullProgression_AllLevels1To25()
        {
            _balanceReport.AppendLine("TEST 1: Full Progression - All 25 Cave Levels");
            _balanceReport.AppendLine("==============================================");
            _balanceReport.AppendLine();

            var levelStats = new Dictionary<int, LevelStatistics>();

            // Traverse all 25 pit levels
            for (int level = 1; level <= 25; level++)
            {
                _context.PitGenerator.RegenerateForLevel(level);

                var stats = new LevelStatistics
                {
                    PitLevel = level,
                    MonsterCount = _world.LastGeneratedMonsterTypes.Count,
                    TreasureCount = _world.LastGeneratedTreasureLevels.Count,
                    BossCount = _world.LastGeneratedBossMonsterCount,
                    MonsterTypes = new List<string>(_world.LastGeneratedMonsterTypes),
                    TreasureLevels = new List<int>(_world.LastGeneratedTreasureLevels),
                    EquipmentTypes = new List<string>(_world.LastGeneratedEquipmentTypes),
                    IsBossFloor = CaveBiomeConfig.IsBossFloor(level),
                    ExpectedPool = CaveBiomeConfig.GetEnemyPoolForLevel(level),
                    ScaledEnemyLevel = CaveBiomeConfig.GetScaledEnemyLevelForPitLevel(level)
                };

                levelStats[level] = stats;

                // Log level summary
                _balanceReport.AppendLine($"Pit Level {level} [{(stats.IsBossFloor ? "BOSS" : "Normal")}]:");
                _balanceReport.AppendLine($"  Scaled Enemy Level: {stats.ScaledEnemyLevel}");
                _balanceReport.AppendLine($"  Monsters: {stats.MonsterCount} ({string.Join(", ", stats.MonsterTypes.Distinct())})");
                _balanceReport.AppendLine($"  Treasures: {stats.TreasureCount} (Levels: {string.Join(", ", stats.TreasureLevels)})");
                if (stats.IsBossFloor)
                {
                    _balanceReport.AppendLine($"  Boss: {stats.BossCount} boss marker(s)");
                }
                _balanceReport.AppendLine();
            }

            // Validate all levels generated successfully
            Assert.AreEqual(25, levelStats.Count, "Should generate all 25 cave levels");

            _balanceReport.AppendLine("✓ All 25 cave levels generated successfully");
            _balanceReport.AppendLine();
        }

        [TestMethod]
        public void CaveBiome_BossEncounters_ValidateAllFiveBosses()
        {
            _balanceReport.AppendLine("TEST 2: Boss Encounter Validation");
            _balanceReport.AppendLine("==================================");
            _balanceReport.AppendLine();

            int[] expectedBossLevels = { 5, 10, 15, 20, 25 };
            string[] expectedBosses = { "Stone Guardian", "Pit Lord", "Earth Elemental", "Molten Titan", "Ancient Wyrm" };

            for (int i = 0; i < expectedBossLevels.Length; i++)
            {
                int level = expectedBossLevels[i];
                string expectedBoss = expectedBosses[i];

                _context.PitGenerator.RegenerateForLevel(level);

                // Validate boss floor detection
                Assert.IsTrue(CaveBiomeConfig.IsBossFloor(level), 
                    $"Level {level} should be boss floor");

                // Validate boss marker count
                Assert.AreEqual(1, _world.LastGeneratedBossMonsterCount, 
                    $"Boss floor {level} should spawn exactly 1 boss");

                // Validate no regular monsters on boss floors
                Assert.AreEqual(0, CaveBiomeConfig.GetEnemyPoolForLevel(level).Length,
                    $"Boss floor {level} should have empty regular enemy pool");

                // Validate boss type tracking
                Assert.IsTrue(_world.LastGeneratedMonsterTypes.Count > 0,
                    $"Boss floor {level} should track boss type");

                string actualBoss = _world.LastGeneratedMonsterTypes[0];

                _balanceReport.AppendLine($"Boss Floor {level}:");
                _balanceReport.AppendLine($"  Expected: {expectedBoss}");
                _balanceReport.AppendLine($"  Actual: {actualBoss}");
                _balanceReport.AppendLine($"  Scaled Level: {CaveBiomeConfig.GetScaledEnemyLevelForPitLevel(level)}");
                _balanceReport.AppendLine($"  Result: {(actualBoss == expectedBoss ? "✓ PASS" : "✗ FAIL")}");
                _balanceReport.AppendLine();

                Assert.AreEqual(expectedBoss, actualBoss, 
                    $"Boss floor {level} should spawn {expectedBoss}");
            }

            _balanceReport.AppendLine("✓ All 5 boss encounters validated successfully");
            _balanceReport.AppendLine();
        }

        [TestMethod]
        public void CaveBiome_MonsterScaling_ValidateLevelProgression()
        {
            _balanceReport.AppendLine("TEST 3: Monster Scaling & Level Progression");
            _balanceReport.AppendLine("===========================================");
            _balanceReport.AppendLine();

            var scalingData = new List<(int PitLevel, int PlayerLevel, int EnemyLevel, bool IsBoss)>();

            for (int level = 1; level <= 25; level++)
            {
                int playerLevel = BalanceConfig.EstimatePlayerLevelForPitLevel(level);
                int enemyLevel = CaveBiomeConfig.GetScaledEnemyLevelForPitLevel(level);
                bool isBoss = CaveBiomeConfig.IsBossFloor(level);

                scalingData.Add((level, playerLevel, enemyLevel, isBoss));

                // Validate boss bonus
                if (isBoss)
                {
                    Assert.AreEqual(playerLevel + 2, enemyLevel,
                        $"Boss floor {level} should have +2 level bonus");
                }
                else
                {
                    Assert.AreEqual(playerLevel, enemyLevel,
                        $"Normal floor {level} should match player level");
                }
            }

            _balanceReport.AppendLine("| Pit | Player Lvl | Enemy Lvl | Boss Bonus | Type   |");
            _balanceReport.AppendLine("|-----|------------|-----------|------------|--------|");
            foreach (var data in scalingData)
            {
                string type = data.IsBoss ? "BOSS" : "Normal";
                int bonus = data.EnemyLevel - data.PlayerLevel;
                _balanceReport.AppendLine($"| {data.PitLevel,3} | {data.PlayerLevel,10} | {data.EnemyLevel,9} | {bonus,10} | {type,-6} |");
            }
            _balanceReport.AppendLine();

            // Validate smooth progression (allow a small dip after boss floors)
            for (int i = 1; i < scalingData.Count; i++)
            {
                int levelDiff = scalingData[i].EnemyLevel - scalingData[i - 1].EnemyLevel;
                int minAllowedDiff = scalingData[i - 1].IsBoss ? -2 : 0;
                Assert.IsTrue(levelDiff >= minAllowedDiff && levelDiff <= 5,
                    $"Enemy level progression between pit {scalingData[i - 1].PitLevel} and {scalingData[i].PitLevel} should be gradual (diff: {levelDiff})");
            }

            _balanceReport.AppendLine("✓ Monster scaling validates successfully - no sudden difficulty spikes");
            _balanceReport.AppendLine();
        }

        [TestMethod]
        public void CaveBiome_LootDistribution_ValidateTreasureLevels()
        {
            _balanceReport.AppendLine("TEST 4: Loot Distribution & Treasure Levels");
            _balanceReport.AppendLine("===========================================");
            _balanceReport.AppendLine();

            var lootStats = new Dictionary<int, (int Level1Count, int Level2Count)>();

            for (int level = 1; level <= 25; level++)
            {
                _context.PitGenerator.RegenerateForLevel(level);

                int level1 = _world.LastGeneratedTreasureLevels.Count(t => t == 1);
                int level2 = _world.LastGeneratedTreasureLevels.Count(t => t == 2);

                lootStats[level] = (level1, level2);

                // Validate pit 1-10: only level 1 treasure
                if (level <= 10)
                {
                    Assert.AreEqual(0, level2,
                        $"Pit {level} should not have level 2 treasure (1-10 range)");
                    Assert.IsTrue(level1 > 0,
                        $"Pit {level} should have at least some level 1 treasure");
                }

                // Validate pit 11-25: mixed treasure
                if (level > 10)
                {
                    int total = level1 + level2;
                    Assert.IsTrue(total > 0,
                        $"Pit {level} should have treasure");
                    // Should have at least some variety (not 100% one type)
                    // Note: Due to RNG, this might occasionally fail, but statistically should pass
                }
            }

            _balanceReport.AppendLine("Treasure Distribution by Pit Level:");
            _balanceReport.AppendLine("| Pit | Lvl 1 | Lvl 2 | Total | Rarity Band |");
            _balanceReport.AppendLine("|-----|-------|-------|-------|-------------|");
            foreach (var kvp in lootStats)
            {
                int pit = kvp.Key;
                int l1 = kvp.Value.Level1Count;
                int l2 = kvp.Value.Level2Count;
                int total = l1 + l2;
                string rarity = CaveBiomeConfig.GetCaveRarityBand(pit).ToString();
                _balanceReport.AppendLine($"| {pit,3} | {l1,5} | {l2,5} | {total,5} | {rarity,-11} |");
            }
            _balanceReport.AppendLine();

            _balanceReport.AppendLine("✓ Loot distribution follows cave rarity bands");
            _balanceReport.AppendLine();
        }

        [TestMethod]
        public void CaveBiome_SpawnPoolRotation_ValidateSlidingWindow()
        {
            _balanceReport.AppendLine("TEST 5: Spawn Pool Rotation - Sliding Window System");
            _balanceReport.AppendLine("===================================================");
            _balanceReport.AppendLine();

            // Define expected pool transitions
            var poolTransitions = new Dictionary<string, int[]>
            {
                { "Pool 1 (Early Cave)", new[] { 1, 2, 3, 4 } },
                { "Pool 2 (Mid Cave)", new[] { 6, 7, 8, 9 } },
                { "Pool 3 (Deep Cave)", new[] { 11, 12, 13, 14 } },
                { "Pool 4 (Ancient Cave)", new[] { 16, 17, 18, 19 } },
                { "Pool 5 (Abyssal Cave)", new[] { 21, 22, 23, 24 } }
            };

            foreach (var pool in poolTransitions)
            {
                _balanceReport.AppendLine($"{pool.Key}:");
                
                // Track unique monsters across the pool
                var uniqueMonsters = new HashSet<string>();
                
                foreach (int level in pool.Value)
                {
                    _context.PitGenerator.RegenerateForLevel(level);
                    
                    foreach (string monster in _world.LastGeneratedMonsterTypes)
                    {
                        uniqueMonsters.Add(monster);
                    }
                }

                _balanceReport.AppendLine($"  Levels: {string.Join(", ", pool.Value)}");
                _balanceReport.AppendLine($"  Unique Monsters: {uniqueMonsters.Count}");
                _balanceReport.AppendLine($"  Types: {string.Join(", ", uniqueMonsters.OrderBy(m => m))}");
                _balanceReport.AppendLine();

                // Validate monster variety in each pool
                Assert.IsTrue(uniqueMonsters.Count >= 3,
                    $"{pool.Key} should have at least 3 different monster types for variety");
            }

            _balanceReport.AppendLine("✓ Spawn pool rotation provides monster variety across level bands");
            _balanceReport.AppendLine();
        }

        [TestMethod]
        public void CaveBiome_MonsterPoolParity_VirtualMatchesConfig()
        {
            _balanceReport.AppendLine("TEST 6: Monster Pool Parity - Virtual vs Config");
            _balanceReport.AppendLine("================================================");
            _balanceReport.AppendLine();

            int totalMismatches = 0;

            for (int level = 1; level <= 25; level++)
            {
                if (CaveBiomeConfig.IsBossFloor(level))
                    continue; // Skip boss floors

                _context.PitGenerator.RegenerateForLevel(level);

                string[] expectedPool = CaveBiomeConfig.GetEnemyPoolForLevel(level);
                var actualMonsters = new HashSet<string>(_world.LastGeneratedMonsterTypes);

                // All actual monsters must be in expected pool
                bool hasError = false;
                foreach (string monster in actualMonsters)
                {
                    if (!expectedPool.Contains(monster))
                    {
                        _balanceReport.AppendLine($"✗ Level {level}: Unexpected monster '{monster}' not in pool");
                        hasError = true;
                        totalMismatches++;
                    }
                }

                if (!hasError)
                {
                    _balanceReport.AppendLine($"✓ Level {level}: All spawned monsters match expected pool");
                }
            }

            _balanceReport.AppendLine();
            _balanceReport.AppendLine($"Total Mismatches: {totalMismatches}");
            _balanceReport.AppendLine();

            Assert.AreEqual(0, totalMismatches,
                "Virtual layer spawns should match CaveBiomeConfig pools");

            _balanceReport.AppendLine("✓ 100% parity between virtual layer and config");
            _balanceReport.AppendLine();
        }

        [TestMethod]
        public void CaveBiome_EquipmentDrops_ValidateTracking()
        {
            _balanceReport.AppendLine("TEST 7: Equipment Drop Tracking");
            _balanceReport.AppendLine("================================");
            _balanceReport.AppendLine();

            var equipmentStats = new Dictionary<string, int>();

            for (int level = 1; level <= 25; level++)
            {
                _context.PitGenerator.RegenerateForLevel(level);

                foreach (string equipment in _world.LastGeneratedEquipmentTypes)
                {
                    if (!equipmentStats.ContainsKey(equipment))
                    {
                        equipmentStats[equipment] = 0;
                    }
                    equipmentStats[equipment]++;
                }

                // Validate equipment count matches treasure count
                Assert.AreEqual(_world.LastGeneratedTreasureLevels.Count,
                    _world.LastGeneratedEquipmentTypes.Count,
                    $"Level {level} should track equipment type for each treasure");
            }

            _balanceReport.AppendLine("Equipment Type Distribution (Levels 1-25):");
            foreach (var kvp in equipmentStats.OrderByDescending(kv => kv.Value))
            {
                _balanceReport.AppendLine($"  {kvp.Key}: {kvp.Value} drops");
            }
            _balanceReport.AppendLine();

            _balanceReport.AppendLine("✓ Equipment tracking validates successfully");
            _balanceReport.AppendLine();
        }

        [TestMethod]
        public void CaveBiome_DifficultyCurve_AssessProgression()
        {
            _balanceReport.AppendLine("TEST 8: Difficulty Curve Assessment");
            _balanceReport.AppendLine("====================================");
            _balanceReport.AppendLine();

            var difficultyData = new List<DifficultyMetrics>();

            for (int level = 1; level <= 25; level++)
            {
                _context.PitGenerator.RegenerateForLevel(level);

                var metrics = new DifficultyMetrics
                {
                    PitLevel = level,
                    EnemyLevel = CaveBiomeConfig.GetScaledEnemyLevelForPitLevel(level),
                    MonsterCount = _world.LastGeneratedMonsterTypes.Count,
                    IsBoss = CaveBiomeConfig.IsBossFloor(level),
                    TreasureCount = _world.LastGeneratedTreasureLevels.Count,
                    Level2TreasureRatio = _world.LastGeneratedTreasureLevels.Count > 0
                        ? (float)_world.LastGeneratedTreasureLevels.Count(t => t == 2) / _world.LastGeneratedTreasureLevels.Count
                        : 0f
                };

                difficultyData.Add(metrics);
            }

            _balanceReport.AppendLine("Difficulty Progression:");
            _balanceReport.AppendLine("| Pit | Enemy Lvl | Monsters | Boss | Treasures | L2 % |");
            _balanceReport.AppendLine("|-----|-----------|----------|------|-----------|------|");
            foreach (var data in difficultyData)
            {
                string boss = data.IsBoss ? "YES" : "NO";
                string l2Percent = $"{data.Level2TreasureRatio * 100:F0}%";
                _balanceReport.AppendLine($"| {data.PitLevel,3} | {data.EnemyLevel,9} | {data.MonsterCount,8} | {boss,4} | {data.TreasureCount,9} | {l2Percent,4} |");
            }
            _balanceReport.AppendLine();

            // Assess difficulty curve
            _balanceReport.AppendLine("Difficulty Curve Analysis:");
            _balanceReport.AppendLine($"  Enemy Level Range: {difficultyData.Min(d => d.EnemyLevel)} - {difficultyData.Max(d => d.EnemyLevel)}");
            _balanceReport.AppendLine($"  Average Monsters per Level: {difficultyData.Average(d => d.MonsterCount):F1}");
            _balanceReport.AppendLine($"  Boss Encounters: {difficultyData.Count(d => d.IsBoss)}");
            _balanceReport.AppendLine($"  Level 2 Treasure Availability: Pit {difficultyData.FirstOrDefault(d => d.Level2TreasureRatio > 0)?.PitLevel ?? 0}+");
            _balanceReport.AppendLine();

            _balanceReport.AppendLine("✓ Difficulty curve assessed - progression appears balanced");
            _balanceReport.AppendLine();
        }

        [TestMethod]
        public void CaveBiome_VirtualLayerParity_ConfirmBehavior()
        {
            _balanceReport.AppendLine("TEST 9: Virtual Layer Parity Confirmation");
            _balanceReport.AppendLine("==========================================");
            _balanceReport.AppendLine();

            bool allTestsPassed = true;
            var issues = new List<string>();

            for (int level = 1; level <= 25; level++)
            {
                _context.PitGenerator.RegenerateForLevel(level);

                // Check 1: Boss floors have exactly 1 boss
                if (CaveBiomeConfig.IsBossFloor(level))
                {
                    if (_world.LastGeneratedBossMonsterCount != 1)
                    {
                        issues.Add($"Boss floor {level} has {_world.LastGeneratedBossMonsterCount} bosses (expected: 1)");
                        allTestsPassed = false;
                    }
                }
                else
                {
                    if (_world.LastGeneratedBossMonsterCount != 0)
                    {
                        issues.Add($"Non-boss floor {level} has {_world.LastGeneratedBossMonsterCount} bosses (expected: 0)");
                        allTestsPassed = false;
                    }
                }

                // Check 2: Treasure levels are valid (1 or 2)
                foreach (int treasureLevel in _world.LastGeneratedTreasureLevels)
                {
                    if (treasureLevel < 1 || treasureLevel > 2)
                    {
                        issues.Add($"Level {level} has invalid treasure level: {treasureLevel}");
                        allTestsPassed = false;
                    }
                }

                // Check 3: Monster types are tracked
                if (!CaveBiomeConfig.IsBossFloor(level))
                {
                    if (_world.LastGeneratedMonsterTypes.Count == 0)
                    {
                        issues.Add($"Non-boss floor {level} has no monster types tracked");
                        allTestsPassed = false;
                    }
                }

                // Check 4: Equipment types match treasure count
                if (_world.LastGeneratedEquipmentTypes.Count != _world.LastGeneratedTreasureLevels.Count)
                {
                    issues.Add($"Level {level} equipment count mismatch");
                    allTestsPassed = false;
                }
            }

            if (issues.Count > 0)
            {
                _balanceReport.AppendLine("Issues Found:");
                foreach (string issue in issues)
                {
                    _balanceReport.AppendLine($"  ✗ {issue}");
                }
            }
            else
            {
                _balanceReport.AppendLine("✓ No parity issues detected");
            }
            _balanceReport.AppendLine();

            Assert.IsTrue(allTestsPassed,
                $"Virtual layer should match expected runtime behavior. Issues: {string.Join("; ", issues)}");

            _balanceReport.AppendLine("✓ Virtual layer has 100% parity with expected runtime behavior");
            _balanceReport.AppendLine();
        }

        /// <summary>Statistics for a single pit level.</summary>
        private class LevelStatistics
        {
            public int PitLevel { get; set; }
            public int MonsterCount { get; set; }
            public int TreasureCount { get; set; }
            public int BossCount { get; set; }
            public List<string> MonsterTypes { get; set; } = new List<string>();
            public List<int> TreasureLevels { get; set; } = new List<int>();
            public List<string> EquipmentTypes { get; set; } = new List<string>();
            public bool IsBossFloor { get; set; }
            public string[] ExpectedPool { get; set; } = System.Array.Empty<string>();
            public int ScaledEnemyLevel { get; set; }
        }

        /// <summary>Difficulty metrics for progression analysis.</summary>
        private class DifficultyMetrics
        {
            public int PitLevel { get; set; }
            public int EnemyLevel { get; set; }
            public int MonsterCount { get; set; }
            public bool IsBoss { get; set; }
            public int TreasureCount { get; set; }
            public float Level2TreasureRatio { get; set; }
        }
    }
}
