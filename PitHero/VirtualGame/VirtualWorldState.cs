using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PitHero.AI.Interfaces;
using PitHero.AI;

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

        public Point WorldSizeTiles { get; } = new Point(WORLD_WIDTH_TILES, WORLD_HEIGHT_TILES);
        public Point HeroPosition { get; private set; }
        public Point? WizardOrbPosition { get; private set; }
        public Rectangle PitBounds { get; private set; }
        public int PitLevel { get; private set; }
        public bool IsWizardOrbActivated { get; private set; }

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
            if (!_entities.ContainsKey("Treasures"))
                _entities["Treasures"] = new List<Point>();
            
            _entities["Treasures"].Add(position);
            Console.WriteLine($"[VirtualWorld] Added treasure at ({position.X},{position.Y})");
        }
        
        public void AddMonster(Point position)
        {
            if (!_entities.ContainsKey("Monsters"))
                _entities["Monsters"] = new List<Point>();
            
            _entities["Monsters"].Add(position);
            Console.WriteLine($"[VirtualWorld] Added monster at ({position.X},{position.Y})");
        }
        
        public void ClearAllEntities()
        {
            _entities.Clear();
            WizardOrbPosition = null;
            
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
            sb.AppendLine($"Fog tiles remaining in pit: {fogCount}");
            
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