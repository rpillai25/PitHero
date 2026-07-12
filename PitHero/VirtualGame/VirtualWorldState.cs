using Microsoft.Xna.Framework;
using PitHero.AI;
using PitHero.AI.Interfaces;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Equipment;
using System;
using System.Collections.Generic;
using System.Text;

namespace PitHero.VirtualGame
{
    /// <summary>
    /// Virtual world state that simulates the game world without graphics
    /// </summary>
    public class VirtualWorldState : IVirtualWorld, IWorldState
    {
        private const int WORLD_WIDTH_TILES = 60; // Based on GameConfig.InternalWorldWidth / TileSize
        private const int WORLD_HEIGHT_TILES = 25; // Based on GameConfig.InternalWorldHeight / TileSize

        private readonly bool[,] _fogOfWar;
        private readonly bool[,] _collisionMap;
        private readonly Dictionary<string, List<Point>> _entities;

        // ── Phase B: real IEnemy instance tracking ─────────────────────────────────
        // Parallel to the string-list tracking in _entities["Monsters"];
        // both are kept in sync so parity tests still pass.
        private readonly Dictionary<Point, IEnemy> _monsterInstances = new Dictionary<Point, IEnemy>(16);
        private readonly Dictionary<IEnemy, Point> _monsterPositions  = new Dictionary<IEnemy, Point>(16);

        // ── Phase C: real IItem treasure instance tracking ─────────────────────────
        // Parallel to the position list in _entities["Treasures"] and the parity lists
        // LastGeneratedTreasureLevels / LastGeneratedEquipmentTypes.
        // AddTreasure(Point, IItem) keeps all four in sync automatically.
        private readonly Dictionary<Point, IItem> _treasureInstances = new Dictionary<Point, IItem>(16);

        // ── Phase B: trap tile set ─────────────────────────────────────────────────
        /// <summary>
        /// Set of tile positions that contain hidden traps.  Populated by
        /// <see cref="VirtualPitGenerator"/> according to
        /// <see cref="GameConfig.TrapMinPerFloor"/> / <see cref="GameConfig.TrapMaxPerFloor"/>.
        /// </summary>
        public HashSet<Point> TrapTiles { get; } = new HashSet<Point>();

        public Point WorldSizeTiles { get; } = new Point(WORLD_WIDTH_TILES, WORLD_HEIGHT_TILES);
        public Point HeroPosition { get; private set; }
        public Point? WizardOrbPosition { get; private set; }
        public Rectangle PitBounds { get; private set; }
        public int PitLevel { get; private set; }
        public bool IsWizardOrbActivated { get; private set; }
        public int LastGeneratedBossMonsterCount { get; private set; }
        public List<int> LastGeneratedTreasureLevels { get; } = new List<int>(16);
        public List<string> LastGeneratedMonsterTypes { get; } = new List<string>(16);
        public List<string> LastGeneratedEquipmentTypes { get; } = new List<string>(16);

        public VirtualWorldState()
        {
            _fogOfWar = new bool[WORLD_WIDTH_TILES, WORLD_HEIGHT_TILES];
            _collisionMap = new bool[WORLD_WIDTH_TILES, WORLD_HEIGHT_TILES];
            _entities = new Dictionary<string, List<Point>>();

            // Initialize hero at map center
            HeroPosition = new Point(GameConfig.MapCenterTileX, GameConfig.MapCenterTileY);

            // Initialize pit bounds
            PitBounds = new Rectangle(GameConfig.PitRectX, GameConfig.PitRectY,
                                    GameConfig.PitRectWidth, GameConfig.PitRectHeight);

            // Initialize with pit level 10
            RegeneratePit(10);
        }

        public bool HasFogOfWar(Point tilePos)
        {
            if (tilePos.X < 0 || tilePos.Y < 0 || tilePos.X >= WORLD_WIDTH_TILES || tilePos.Y >= WORLD_HEIGHT_TILES)
                return false;

            return _fogOfWar[tilePos.X, tilePos.Y];
        }

        public bool IsCollisionTile(Point tilePos)
        {
            if (tilePos.X < 0 || tilePos.Y < 0 || tilePos.X >= WORLD_WIDTH_TILES || tilePos.Y >= WORLD_HEIGHT_TILES)
                return true; // Out of bounds is collision

            return _collisionMap[tilePos.X, tilePos.Y];
        }

        public Dictionary<string, List<Point>> GetEntityPositions()
        {
            var result = new Dictionary<string, List<Point>>();
            foreach (var kvp in _entities)
            {
                result[kvp.Key] = new List<Point>(kvp.Value);
            }
            return result;
        }

        public void MoveHeroTo(Point tilePos)
        {
            var oldPos = HeroPosition;
            HeroPosition = tilePos;

            // Clear fog of war around new position if inside pit
            if (PitBounds.Contains(tilePos))
            {
                ClearFogOfWar(tilePos, 2);
            }

            Console.WriteLine($"[VirtualWorld] Hero moved from ({oldPos.X},{oldPos.Y}) to ({tilePos.X},{tilePos.Y})");
        }

        public void ClearFogOfWar(Point centerPos, int radius = 2)
        {
            int clearedCount = 0;
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    var pos = new Point(centerPos.X + dx, centerPos.Y + dy);
                    if (pos.X >= 0 && pos.Y >= 0 && pos.X < WORLD_WIDTH_TILES && pos.Y < WORLD_HEIGHT_TILES)
                    {
                        if (_fogOfWar[pos.X, pos.Y])
                        {
                            _fogOfWar[pos.X, pos.Y] = false;
                            clearedCount++;
                        }
                    }
                }
            }

            if (clearedCount > 0)
            {
                Console.WriteLine($"[VirtualWorld] Cleared {clearedCount} fog tiles around ({centerPos.X},{centerPos.Y})");
            }
        }

        public void ActivateWizardOrb()
        {
            IsWizardOrbActivated = true;
            Console.WriteLine($"[VirtualWorld] Wizard orb activated at ({WizardOrbPosition?.X},{WizardOrbPosition?.Y})");
        }

        public void SetWizardOrbPosition(Point position)
        {
            WizardOrbPosition = position;
            _entities["WizardOrb"] = new List<Point> { position };
            Console.WriteLine($"[VirtualWorld] Wizard orb positioned at ({position.X},{position.Y})");
        }

        public void AddObstacle(Point position)
        {
            if (!_entities.ContainsKey("Obstacles"))
                _entities["Obstacles"] = new List<Point>();

            _entities["Obstacles"].Add(position);
            _collisionMap[position.X, position.Y] = true;
            Console.WriteLine($"[VirtualWorld] Added obstacle at ({position.X},{position.Y})");
        }

        public void AddTreasure(Point position)
        {
            AddTreasure(position, "Unknown", 1);
        }

        /// <summary>
        /// Adds a treasure and records the generated treasure level for parity tests.
        /// </summary>
        public void AddTreasure(Point position, int treasureLevel)
        {
            AddTreasure(position, "Unknown", treasureLevel);
        }

        /// <summary>
        /// Adds a treasure and records both equipment type and treasure level for parity tests.
        /// </summary>
        public void AddTreasure(Point position, string equipmentType, int treasureLevel)
        {
            if (!_entities.ContainsKey("Treasures"))
                _entities["Treasures"] = new List<Point>();

            _entities["Treasures"].Add(position);
            LastGeneratedTreasureLevels.Add(treasureLevel);
            LastGeneratedEquipmentTypes.Add(equipmentType);
            Console.WriteLine($"[VirtualWorld] Added {equipmentType} treasure (level {treasureLevel}) at ({position.X},{position.Y})");
        }

        /// <summary>
        /// Adds a real <see cref="IItem"/> instance at the given tile position,
        /// updating both the instance dictionary and the string-based parity tracking lists.
        /// The treasure level is inferred from the item's rarity (Normal→1, Uncommon→2,
        /// Rare→3, Epic→4, Legendary→5), which is accurate for all Cave-biome items.
        /// </summary>
        public void AddTreasure(Point position, IItem item)
        {
            if (item == null) return;
            _treasureInstances[position] = item;
            int inferredLevel = GetTreasureLevelFromRarity(item.Rarity);
            AddTreasure(position, item.Name, inferredLevel);
        }

        /// <summary>
        /// Returns the <see cref="IItem"/> at the given tile without removing it,
        /// or false when no unopened treasure exists there.
        /// </summary>
        public bool TryGetTreasureAt(Point position, out IItem item)
        {
            return _treasureInstances.TryGetValue(position, out item) && item != null;
        }

        /// <summary>
        /// Removes a collected treasure from both the instance dictionary and the
        /// <c>_entities["Treasures"]</c> position list so the world state stays consistent.
        /// </summary>
        public void RemoveTreasure(Point position)
        {
            _treasureInstances.Remove(position);
            if (_entities.TryGetValue("Treasures", out var list))
                list.Remove(position);
        }

        /// <summary>
        /// Read-only view of all treasure items currently placed in the world, keyed by tile position.
        /// Use this in tests to inspect item types and tier-scaling without consuming treasures.
        /// </summary>
        public IReadOnlyDictionary<Point, IItem> TreasureInstances => _treasureInstances;

        /// <summary>Returns true when at least one unopened treasure chest remains.</summary>
        public bool HasUnopenedTreasures()
        {
            return _treasureInstances.Count > 0;
        }

        /// <summary>
        /// Returns the tile position of the nearest unopened treasure to
        /// <paramref name="heroPos"/>, or null when no treasures remain.
        /// Uses Manhattan distance as the proximity metric.
        /// </summary>
        public Point? GetNearestTreasurePosition(Point heroPos)
        {
            Point? best     = null;
            int    bestDist = int.MaxValue;
            foreach (var kvp in _treasureInstances)
            {
                int dist = System.Math.Abs(kvp.Key.X - heroPos.X) +
                           System.Math.Abs(kvp.Key.Y - heroPos.Y);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best     = kvp.Key;
                }
            }
            return best;
        }

        /// <summary>Maps item rarity to the corresponding treasure level (1–5).</summary>
        private static int GetTreasureLevelFromRarity(RolePlayingFramework.Equipment.ItemRarity rarity)
        {
            switch (rarity)
            {
                case RolePlayingFramework.Equipment.ItemRarity.Uncommon:  return 2;
                case RolePlayingFramework.Equipment.ItemRarity.Rare:      return 3;
                case RolePlayingFramework.Equipment.ItemRarity.Epic:      return 4;
                case RolePlayingFramework.Equipment.ItemRarity.Legendary: return 5;
                default:                                                   return 1;
            }
        }

        public void AddMonster(Point position)
        {
            AddMonster(position, "Unknown");
        }

        /// <summary>
        /// Adds a monster and records the monster type for parity tests.
        /// </summary>
        public void AddMonster(Point position, string monsterType)
        {
            if (!_entities.ContainsKey("Monsters"))
                _entities["Monsters"] = new List<Point>();

            _entities["Monsters"].Add(position);
            LastGeneratedMonsterTypes.Add(monsterType);
            Console.WriteLine($"[VirtualWorld] Added {monsterType} monster at ({position.X},{position.Y})");
        }

        /// <summary>
        /// Adds a boss monster marker for virtual cave boss floor parity.
        /// </summary>
        public void AddBossMonster(Point position)
        {
            AddBossMonster(position, "BossMonster");
        }

        /// <summary>
        /// Adds a boss monster with type tracking for virtual cave boss floor parity.
        /// </summary>
        public void AddBossMonster(Point position, string monsterType)
        {
            if (!_entities.ContainsKey("BossMonsters"))
            {
                _entities["BossMonsters"] = new List<Point>();
            }

            _entities["BossMonsters"].Add(position);
            LastGeneratedBossMonsterCount++;
            AddMonster(position, monsterType);
        }

        // ── Phase B: IEnemy-backed monster methods ─────────────────────────────────

        /// <summary>
        /// Adds a real <see cref="IEnemy"/> instance at the given tile position,
        /// updating both the string-list tracking (for parity tests) and the
        /// instance dictionaries (for combat simulation).
        /// </summary>
        public void AddMonster(Point position, IEnemy enemy)
        {
            if (enemy == null) return;
            _monsterInstances[position] = enemy;
            _monsterPositions[enemy]    = position;

            // Route through the correct string-based method so that
            // LastGeneratedBossMonsterCount and the "BossMonsters" entity key
            // are populated when the enemy is a boss.
            //
            // Key point: parity tests compare monster-type strings against
            // EnemyId.ToString() (e.g. "Bat", "Slime") for regular monsters,
            // and MonsterTextKey strings (e.g. "Monster_StoneGuardian") for bosses.
            // We must use different string sources:
            //   boss    → enemy.Name  (the MonsterTextKey)
            //   regular → enemy.GetType().Name  (matches EnemyId.ToString() exactly)
            if (enemy.IsBoss)
                AddBossMonster(position, enemy.Name);
            else
                AddMonster(position, enemy.GetType().Name);
        }

        /// <summary>
        /// Fills <paramref name="buffer"/> with all living monsters whose tile position
        /// is within Chebyshev distance 1 (8-adjacency) of <paramref name="heroPos"/>.
        /// </summary>
        public void GetLivingMonstersAdjacentTo(Point heroPos, List<IEnemy> buffer)
        {
            foreach (var kvp in _monsterInstances)
            {
                var pos = kvp.Key;
                if (System.Math.Abs(pos.X - heroPos.X) <= 1 &&
                    System.Math.Abs(pos.Y - heroPos.Y) <= 1 &&
                    kvp.Value.CurrentHP > 0)
                {
                    buffer.Add(kvp.Value);
                }
            }
        }

        /// <summary>
        /// Tries to get the monster instance at the given tile, returning false when
        /// the tile has no monster or the monster is dead.
        /// </summary>
        public bool TryGetMonsterAt(Point position, out IEnemy enemy)
        {
            if (_monsterInstances.TryGetValue(position, out enemy))
                return enemy != null && enemy.CurrentHP > 0;
            enemy = null;
            return false;
        }

        /// <summary>
        /// Removes a defeated monster from both the instance dictionaries and the
        /// <c>_entities["Monsters"]</c> position list so the world state stays consistent.
        /// </summary>
        public void RemoveMonster(IEnemy enemy)
        {
            if (enemy == null) return;
            if (!_monsterPositions.TryGetValue(enemy, out Point pos)) return;

            _monsterPositions.Remove(enemy);
            _monsterInstances.Remove(pos);

            // Remove from the string-based position list
            if (_entities.TryGetValue("Monsters", out var monsterPosList))
                monsterPosList.Remove(pos);
        }

        /// <summary>
        /// Returns true when at least one monster in the instance dictionary is alive
        /// and flagged as a boss (<see cref="IEnemy.IsBoss"/>).
        /// </summary>
        public bool HasLivingBoss()
        {
            foreach (var enemy in _monsterInstances.Values)
            {
                if (enemy.IsBoss && enemy.CurrentHP > 0) return true;
            }
            return false;
        }

        /// <summary>
        /// Returns true when at least one monster in the instance dictionary is alive.
        /// </summary>
        public bool HasLivingMonsters()
        {
            foreach (var enemy in _monsterInstances.Values)
            {
                if (enemy.CurrentHP > 0) return true;
            }
            return false;
        }

        /// <summary>
        /// Returns the tile position of the nearest living monster to
        /// <paramref name="heroPos"/>, or null when no monsters remain.
        /// Uses Manhattan distance as the proximity metric.
        /// </summary>
        public Point? GetNearestLivingMonsterPosition(Point heroPos)
        {
            Point? best    = null;
            int    bestDist = int.MaxValue;
            foreach (var kvp in _monsterInstances)
            {
                if (kvp.Value.CurrentHP <= 0) continue;
                int dist = System.Math.Abs(kvp.Key.X - heroPos.X) +
                           System.Math.Abs(kvp.Key.Y - heroPos.Y);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best     = kvp.Key;
                }
            }
            return best;
        }

        // ── Phase B: trap methods ──────────────────────────────────────────────────

        /// <summary>
        /// Registers a trap at <paramref name="tile"/>.
        /// Called by <see cref="VirtualPitGenerator"/> during pit generation.
        /// </summary>
        public void AddTrapTile(Point tile)
        {
            TrapTiles.Add(tile);
        }

        /// <summary>
        /// Triggers a trap: removes it from <see cref="TrapTiles"/> and returns the
        /// raw damage that should be applied to the hero.
        /// Formula mirrors <c>TrapComponent.Damage</c>: <c>5 + PitLevel * 2</c>.
        /// Clamped to at least 1 for safety.
        /// The caller is responsible for clamping damage so the hero survives with ≥ 1 HP
        /// (matching <c>TrapComponent.Trigger</c> behaviour).
        /// Returns 0 when no trap exists at <paramref name="tile"/>.
        /// </summary>
        public int TriggerTrap(Point tile)
        {
            if (!TrapTiles.Contains(tile)) return 0;
            TrapTiles.Remove(tile);
            return System.Math.Max(1, 5 + PitLevel * 2);
        }

        /// <summary>
        /// Disarms a trap without dealing damage.
        /// Mirrors <c>TrapComponent.Disarm()</c> triggered by TrapSense.
        /// </summary>
        public void DisarmTrap(Point tile)
        {
            TrapTiles.Remove(tile);
        }

        public void ClearAllEntities()
        {
            _entities.Clear();
            WizardOrbPosition = null;
            LastGeneratedBossMonsterCount = 0;
            LastGeneratedTreasureLevels.Clear();
            LastGeneratedMonsterTypes.Clear();
            LastGeneratedEquipmentTypes.Clear();
            _monsterInstances.Clear();
            _monsterPositions.Clear();
            TrapTiles.Clear();
            _treasureInstances.Clear();

            // Clear collision map except for pit boundaries
            for (int x = 0; x < WORLD_WIDTH_TILES; x++)
            {
                for (int y = 0; y < WORLD_HEIGHT_TILES; y++)
                {
                    // Keep pit boundary collisions, clear interior collisions
                    if (PitBounds.Contains(new Point(x, y)) &&
                        (x == PitBounds.X || x == PitBounds.Right - 1 ||
                         y == PitBounds.Y || y == PitBounds.Bottom - 1))
                    {
                        // Keep boundary collision
                    }
                    else if (PitBounds.Contains(new Point(x, y)))
                    {
                        // Clear interior collision
                        _collisionMap[x, y] = false;
                    }
                }
            }

            Console.WriteLine("[VirtualWorld] Cleared all entities and interior collisions");
        }

        public void RegeneratePit(int level)
        {
            PitLevel = level;
            IsWizardOrbActivated = false;

            // Calculate dynamic pit width based on level
            var pitWidthManager = CalculatePitWidth(level);
            PitBounds = new Rectangle(GameConfig.PitRectX, GameConfig.PitRectY,
                                    pitWidthManager.width, GameConfig.PitRectHeight);

            // Clear all fog in world first
            for (int x = 0; x < WORLD_WIDTH_TILES; x++)
            {
                for (int y = 0; y < WORLD_HEIGHT_TILES; y++)
                {
                    _fogOfWar[x, y] = false;
                    _collisionMap[x, y] = false;
                }
            }

            // Add fog of war to pit interior
            for (int x = PitBounds.X + 1; x < PitBounds.Right - 1; x++)
            {
                for (int y = PitBounds.Y + 1; y < PitBounds.Bottom - 1; y++)
                {
                    _fogOfWar[x, y] = true;
                }
            }

            // Add collision tiles around pit boundary
            for (int x = PitBounds.X; x < PitBounds.Right; x++)
            {
                _collisionMap[x, PitBounds.Y] = true; // Top edge
                _collisionMap[x, PitBounds.Bottom - 1] = true; // Bottom edge
            }
            for (int y = PitBounds.Y; y < PitBounds.Bottom; y++)
            {
                _collisionMap[PitBounds.X, y] = true; // Left edge
                _collisionMap[PitBounds.Right - 1, y] = true; // Right edge
            }

            // Generate entities in pit
            GeneratePitEntities(level);

            Console.WriteLine($"[VirtualWorld] Regenerated pit at level {level}, bounds: ({PitBounds.X},{PitBounds.Y},{PitBounds.Width},{PitBounds.Height})");
        }

        private (int width, int centerX) CalculatePitWidth(int level)
        {
            // Use the actual PitWidthManager logic: ((int)(level / 10)) * 2
            int extensionTiles = ((int)(level / 10)) * 2;
            int width = GameConfig.PitRectWidth + extensionTiles;

            int centerX = GameConfig.PitRectX + (width / 2);
            return (width, centerX);
        }

        private void GeneratePitEntities(int level)
        {
            _entities.Clear();
            LastGeneratedBossMonsterCount = 0;
            LastGeneratedTreasureLevels.Clear();
            LastGeneratedMonsterTypes.Clear();
            LastGeneratedEquipmentTypes.Clear();

            // Generate wizard orb at center-ish position
            var pitCenter = new Point(PitBounds.X + PitBounds.Width / 2, PitBounds.Y + PitBounds.Height / 2);
            WizardOrbPosition = pitCenter;

            _entities["WizardOrb"] = new List<Point> { pitCenter };

            // Generate some obstacles
            var obstacles = new List<Point>();
            var random = new Random(level); // Deterministic based on level

            for (int i = 0; i < 5; i++)
            {
                var x = random.Next(PitBounds.X + 1, PitBounds.Right - 1);
                var y = random.Next(PitBounds.Y + 1, PitBounds.Bottom - 1);
                var pos = new Point(x, y);

                if (pos != pitCenter) // Don't overlap wizard orb
                {
                    obstacles.Add(pos);
                    _collisionMap[x, y] = true;
                }
            }

            _entities["Obstacles"] = obstacles;

            Console.WriteLine($"[VirtualWorld] Generated {obstacles.Count} obstacles and 1 wizard orb in pit");
        }

        public string GetVisualRepresentation()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== Virtual World - Pit Level {PitLevel} ===");
            sb.AppendLine($"Hero: ({HeroPosition.X},{HeroPosition.Y}), Wizard Orb: ({WizardOrbPosition?.X},{WizardOrbPosition?.Y}){(IsWizardOrbActivated ? " [ACTIVATED]" : "")}");
            sb.AppendLine($"Pit Bounds: ({PitBounds.X},{PitBounds.Y},{PitBounds.Width},{PitBounds.Height})");
            sb.AppendLine();

            // Show a focused view around the pit
            int minX = Math.Max(0, PitBounds.X - 3);
            int maxX = Math.Min(WORLD_WIDTH_TILES - 1, PitBounds.Right + 2);
            int minY = Math.Max(0, PitBounds.Y - 2);
            int maxY = Math.Min(WORLD_HEIGHT_TILES - 1, PitBounds.Bottom + 1);

            // Header with X coordinates
            sb.Append("   ");
            for (int x = minX; x <= maxX; x++)
            {
                sb.Append($"{x % 10}");
            }
            sb.AppendLine();

            for (int y = minY; y <= maxY; y++)
            {
                sb.Append($"{y:D2} ");
                for (int x = minX; x <= maxX; x++)
                {
                    var pos = new Point(x, y);
                    char ch = '.';

                    if (pos == HeroPosition)
                        ch = 'H';
                    else if (pos == WizardOrbPosition)
                        ch = IsWizardOrbActivated ? 'W' : 'w';
                    else if (_entities.ContainsKey("Obstacles") && _entities["Obstacles"].Contains(pos))
                        ch = '#';
                    else if (_entities.ContainsKey("Treasures") && _entities["Treasures"].Contains(pos))
                        ch = '$';
                    else if (_entities.ContainsKey("Monsters") && _entities["Monsters"].Contains(pos))
                        ch = 'M';
                    else if (IsCollisionTile(pos))
                        ch = '█';
                    else if (HasFogOfWar(pos))
                        ch = '?';

                    sb.Append(ch);
                }
                sb.AppendLine();
            }

            sb.AppendLine();
            sb.AppendLine("Legend: H=Hero, w=Wizard Orb, W=Activated Orb, #=Obstacle, $=Treasure, M=Monster, █=Wall, ?=Fog, .=Empty");

            // Count fog tiles
            int fogCount = 0;
            for (int x = PitBounds.X + 1; x < PitBounds.Right - 1; x++)
            {
                for (int y = PitBounds.Y + 1; y < PitBounds.Bottom - 1; y++)
                {
                    if (HasFogOfWar(new Point(x, y)))
                        fogCount++;
                }
            }
            sb.AppendLine($"fog remaining: {fogCount}");

            return sb.ToString();
        }

        // IWorldState implementation - required methods
        public bool IsPassable(Point tilePosition)
        {
            // Check bounds
            if (tilePosition.X < 0 || tilePosition.Y < 0 ||
                tilePosition.X >= WORLD_WIDTH_TILES || tilePosition.Y >= WORLD_HEIGHT_TILES)
                return false;

            // Check collision map
            return !_collisionMap[tilePosition.X, tilePosition.Y];
        }

        public bool IsMapExplored
        {
            get
            {
                // Check if all tiles in the pit (explorable area) have no fog
                for (int x = PitBounds.X + 1; x < PitBounds.Right - 1; x++)
                {
                    for (int y = PitBounds.Y + 1; y < PitBounds.Bottom - 1; y++)
                    {
                        if (HasFogOfWar(new Point(x, y)))
                            return false; // Still has fog
                    }
                }
                return true; // All fog cleared
            }
        }

        public bool IsWizardOrbFound
        {
            get
            {
                var orbPos = WizardOrbPosition;
                if (!orbPos.HasValue)
                    return false;

                return !HasFogOfWar(orbPos.Value);
            }
        }

        /// <summary>
        /// Clear all fog tiles in the pit area (for testing complete exploration)
        /// </summary>
        public void ClearAllFogInPit()
        {
            for (int x = PitBounds.X + 1; x < PitBounds.Right - 1; x++)
            {
                for (int y = PitBounds.Y + 1; y < PitBounds.Bottom - 1; y++)
                {
                    _fogOfWar[x, y] = false;
                }
            }
            Console.WriteLine("[VirtualWorld] Cleared all fog in pit area");
        }

        /// <summary>
        /// Discover wizard orb at specific position (for testing)
        /// </summary>
        public void DiscoverWizardOrb(Point position)
        {
            WizardOrbPosition = position;
            // Clear fog around wizard orb
            _fogOfWar[position.X, position.Y] = false;
            Console.WriteLine($"[VirtualWorld] Discovered wizard orb at {position.X},{position.Y}");
        }

        /// <summary>
        /// Get collection of fog tiles in pit for testing
        /// </summary>
        public List<Point> FogTilesInPit
        {
            get
            {
                var fogTiles = new List<Point>();
                for (int x = PitBounds.X + 1; x < PitBounds.Right - 1; x++)
                {
                    for (int y = PitBounds.Y + 1; y < PitBounds.Bottom - 1; y++)
                    {
                        if (_fogOfWar[x, y])
                        {
                            fogTiles.Add(new Point(x, y));
                        }
                    }
                }
                return fogTiles;
            }
        }

        /// <summary>
        /// Get current pit width in tiles
        /// </summary>
        public int PitWidthTiles => PitBounds.Width;

        /// <summary>
        /// Get current world state as dictionary (for testing)
        /// </summary>
        public Dictionary<string, bool> GetCurrentState()
        {
            var state = new Dictionary<string, bool>();

            // Always true states
            state[GoapConstants.HeroInitialized] = true;
            state[GoapConstants.PitInitialized] = true;

            // Check exploration status - MapExplored is now ExploredPit
            state[GoapConstants.ExploredPit] = IsMapExplored;

            // Check wizard orb status
            state[GoapConstants.FoundWizardOrb] = IsWizardOrbFound;
            state[GoapConstants.ActivatedWizardOrb] = IsWizardOrbActivated;

            // Check hero position states (would come from hero object in real scenario)
            // For testing, we'll use reasonable defaults
            state[GoapConstants.InsidePit] = PitBounds.Contains(HeroPosition);
            state[GoapConstants.OutsidePit] = !PitBounds.Contains(HeroPosition);

            // Note: AtWizardOrb is no longer a state in simplified GOAP - position checking is done in actions
            // if (WizardOrbPosition.HasValue)
            // {
            //     state[GoapConstants.AtWizardOrb] = HeroPosition == WizardOrbPosition.Value;
            // }

            // Note: AtPitGenPoint is no longer a state in simplified GOAP - position checking is done in actions
            // state[GoapConstants.AtPitGenPoint] = HeroPosition.X == 34 && HeroPosition.Y == 6;

            return state;
        }
    }
}