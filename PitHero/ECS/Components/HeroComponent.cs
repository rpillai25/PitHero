using Microsoft.Xna.Framework;
using Nez;
using Nez.AI.GOAP;
using PitHero.AI;
using System;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Component for heroes in the game - simplified to only contain the 7 required state properties
    /// </summary>
    public class HeroComponent : PathfindingActorComponent, IUpdatable
    {
        // The 7 required GOAP state properties
        public bool HeroInitialized { get; set; }                    // True after hero entity initialized, remains true
        public bool PitInitialized { get; set; }                     // True after pit generated, false after ActivateWizardOrbAction

        // Backing field so we can react to inside/outside transitions
        private bool _insidePit;
        /// <summary>
        /// True when hero is inside the pit. Adjusts movement speed automatically on change.
        /// </summary>
        public bool InsidePit
        {
            get => _insidePit;
            set
            {
                if (_insidePit == value)
                    return;

                _insidePit = value;
                ApplyMovementSpeedForPitState();
            }
        }
        public bool OutsidePit => !InsidePit;                        // Opposite of InsidePit (calculated)
        public bool ExploredPit { get; set; }                        // True after all reachable FogOfWar uncovered, false upon ActivatePitRegenAction
        public bool FoundWizardOrb { get; set; }                     // True after hero uncovered fog over wizard orb
        public bool ActivatedWizardOrb { get; set; }                 // True after ActivateWizardOrbAction, false upon ActivatePitRegenAction
        public bool AdjacentToMonster { get; set; }                  // True when monster exists in tile adjacent to hero
        public bool AdjacentToChest { get; set; }                    // True when chest exists in tile adjacent to hero

        /// <summary>
        /// Hero uncover radius for fog of war clearing (default 1 = 8 surrounding tiles)
        /// </summary>
        public int UncoverRadius { get; set; } = GameConfig.DefaultHeroUncoverRadius;

        /// <summary>
        /// Hero pit priority 1 (highest priority action)
        /// </summary>
        public HeroPitPriority Priority1 { get; set; } = HeroPitPriority.Treasure;

        /// <summary>
        /// Hero pit priority 2 (medium priority action)
        /// </summary>
        public HeroPitPriority Priority2 { get; set; } = HeroPitPriority.Battle;

        /// <summary>
        /// Hero pit priority 3 (lowest priority action)
        /// </summary>
        public HeroPitPriority Priority3 { get; set; } = HeroPitPriority.Advance;

        /// <summary>
        /// Link to the Hero class from RolePlayingFramework
        /// </summary>
        public RolePlayingFramework.Heroes.Hero LinkedHero { get; set; }

        private PitWidthManager _pitWidthManager;

        // Fog of war movement speed tracking
        private float _fogCooldown = 0f;

        // Dynamic pit collision rectangle computed from PitWidthManager (falls back to GameConfig)
        private Rectangle PitCollisionRect
        {
            get
            {
                var width = _pitWidthManager?.CurrentPitRectWidthTiles ?? GameConfig.PitRectWidth;
                var height = GameConfig.PitRectHeight;
                return new Rectangle(GameConfig.PitRectX, GameConfig.PitRectY, width, height);
            }
        }

        // Dynamic pit center point computed from PitWidthManager (falls back to GameConfig)
        private Point PitCenter
        {
            get
            {
                var centerX = _pitWidthManager?.CurrentPitCenterTileX ?? GameConfig.PitCenterTileX;
                var centerY = GameConfig.PitCenterTileY;
                return new Point(centerX, centerY);
            }
        }

        // Helper: distance in tiles
        private float DistanceTiles(Point a, Point b) =>
            Vector2.Distance(new Vector2(a.X, a.Y), new Vector2(b.X, b.Y));

        public override void OnAddedToEntity()
        {
            base.OnAddedToEntity();

            // Cache PitWidthManager service for dynamic pit sizing
            _pitWidthManager = Core.Services.GetService<PitWidthManager>();

            // Initialize state properties to clean state
            HeroInitialized = true;  // Set to true after hero entity and components initialized
            // Do not override PitInitialized here; it may be set by the spawner.
            _insidePit = false;
            ExploredPit = false;
            FoundWizardOrb = false;
            ActivatedWizardOrb = false;

            // Ensure initial movement speed matches starting pit state (outside by default)
            ApplyMovementSpeedForPitState();
        }

        /// <summary>
        /// Update fog cooldown timer
        /// </summary>
        public void Update()
        {
            if (_fogCooldown > 0f)
            {
                _fogCooldown -= Time.DeltaTime;
                if (_fogCooldown < 0f)
                {
                    _fogCooldown = 0f;
                }

                // Reapply movement speed when cooldown expires
                if (_fogCooldown == 0f && _insidePit)
                {
                    ApplyMovementSpeedForPitState();
                }
            }
        }

        /// <summary>
        /// Apply movement speed to the TileByTileMover based on whether hero is inside or outside the pit
        /// </summary>
        private void ApplyMovementSpeedForPitState()
        {
            var mover = Entity?.GetComponent<TileByTileMover>();
            if (mover == null)
                return;

            float newSpeed;
            if (_insidePit)
            {
                // Inside pit: use slow speed if fog cooldown is active, otherwise use normal pit speed
                newSpeed = _fogCooldown > 0f ? GameConfig.HeroPitMovementSpeed : GameConfig.HeroMovementSpeed;
            }
            else
            {
                // Outside pit: always use normal speed
                newSpeed = GameConfig.HeroMovementSpeed;
            }

            mover.MovementSpeed = newSpeed;

            Debug.Log($"[HeroComponent] Movement speed set based on pit state. InsidePit={_insidePit}, FogCooldown={_fogCooldown:F2}, Speed={newSpeed}");
        }

        /// <summary>
        /// Trigger fog cooldown when fog of war is cleared
        /// </summary>
        public void TriggerFogCooldown()
        {
            if (_insidePit)
            {
                _fogCooldown = GameConfig.HeroFogCooldownDuration;
                ApplyMovementSpeedForPitState();
                Debug.Log($"[HeroComponent] Fog cooldown triggered. Duration={_fogCooldown:F2}s");
            }
        }

        /// <summary>
        /// Override to set world state based on hero's current state
        /// </summary>
        public override void SetWorldState(ref WorldState worldState)
        {
            if (HeroInitialized)
            {
                worldState.Set(GoapConstants.HeroInitialized, true);
            }
            if (PitInitialized)
            {
                worldState.Set(GoapConstants.PitInitialized, true);
            }
            if (InsidePit)
            {
                worldState.Set(GoapConstants.InsidePit, true);
            }
            if (OutsidePit)
            {
                worldState.Set(GoapConstants.OutsidePit, true);
            }
            if (ExploredPit)
            {
                worldState.Set(GoapConstants.ExploredPit, true);
            }
            if (FoundWizardOrb)
            {
                worldState.Set(GoapConstants.FoundWizardOrb, true);
            }
            if (ActivatedWizardOrb)
            {
                worldState.Set(GoapConstants.ActivatedWizardOrb, true);
            }
            if (AdjacentToMonster)
            {
                worldState.Set(GoapConstants.AdjacentToMonster, true);
            }
            if (AdjacentToChest)
            {
                worldState.Set(GoapConstants.AdjacentToChest, true);
            }
        }

        /// <summary>
        /// Override to set goal state based on hero's desired state
        /// </summary>
        public override void SetGoalState(ref WorldState goalState)
        {
            // Main goals for the hero - planner should always plan the optimal path to these goals.

            if (PitInitialized && !ActivatedWizardOrb)
            {
                goalState.Set(GoapConstants.ActivatedWizardOrb, true);
            }
            else if (!PitInitialized && ActivatedWizardOrb)
            {
                goalState.Set(GoapConstants.PitInitialized, true);
            }            
            // Interactive entity goals - higher priority when inside pit
            if (InsidePit && AdjacentToMonster)
            {
                goalState.Set(GoapConstants.AdjacentToMonster, false);
            }
            if (InsidePit && AdjacentToChest)
            {
                goalState.Set(GoapConstants.AdjacentToChest, false);
            }
        }

        /// <summary>
        /// Called when hero enters a trigger collider
        /// </summary>
        public override void OnTriggerEnter(Collider other, Collider local)
        {
            base.OnTriggerEnter(other, local);
            
            Debug.Log($"[HeroComponent] OnTriggerEnter: other.Entity.Name={other.Entity.Name}, " +
                      $"other.Entity.Tag={other.Entity.Tag}, " +
                      $"other.PhysicsLayer={other.PhysicsLayer}, " +
                      $"HeroPos={Entity.Transform.Position.X},{Entity.Transform.Position.Y}");
            
            // Handle pit trigger separately from tilemap
            if (other.Entity.Tag == GameConfig.TAG_PIT)
            {
                Debug.Log("[HeroComponent] Detected pit trigger entry");
                HandlePitTriggerEnter();
                return;
            }
            
            // Handle tilemap triggers for FogOfWar clearing
            if (!IsTileMapCollision(other))
                return;

            Debug.Log("[HeroComponent] Detected tilemap trigger entry");
            var tileCoords = GetTileCoordinates(Entity.Transform.Position, GameConfig.TileSize);
            var pitBounds = PitCollisionRect;
            var inside = pitBounds.Contains(tileCoords);

            if (inside)
            {
                // milestone + fog clear when entering pit area via tilemap
                var historian = Entity.GetComponent<Historian>();
                historian?.RecordMilestone(MilestoneType.FirstJumpIntoPit, Time.TotalTime);
            }
        }

        /// <summary>
        /// Called when hero exits a trigger collider
        /// </summary>
        public override void OnTriggerExit(Collider other, Collider local)
        {
            base.OnTriggerExit(other, local);
            
            // Handle pit trigger separately
            if (other.PhysicsLayer == GameConfig.PhysicsPitLayer)
            {
                HandlePitTriggerExit();
                return;
            }
        }

        private void HandlePitTriggerEnter()
        {
            var currentTile = GetCurrentTilePosition();
            var pitBounds = PitCollisionRect;
            
            Debug.Log($"[HeroComponent] HandlePitTriggerEnter: currentTile={currentTile.X},{currentTile.Y}, " +
                      $"pitBounds=({pitBounds.X},{pitBounds.Y},{pitBounds.Width},{pitBounds.Height})");
            
            // Mark as inside pit when entering the pit trigger
            InsidePit = true;

            var historian = Entity.GetComponent<Historian>();
            historian?.RecordMilestone(MilestoneType.FirstJumpIntoPit, Time.TotalTime);
            
            var tileCoords = GetTileCoordinates(Entity.Transform.Position, GameConfig.TileSize);
        }

        private void HandlePitTriggerExit()
        {
            var currentTile = GetCurrentTilePosition();
            var pitBounds = PitCollisionRect;
            
            Debug.Log($"[HeroComponent] HandlePitTriggerExit: currentTile={currentTile.X},{currentTile.Y}, " +
                      $"pitBounds=({pitBounds.X},{pitBounds.Y},{pitBounds.Width},{pitBounds.Height})");
            
            // Only reset flags if hero is actually outside the pit area 
            // This prevents spurious trigger exits from resetting state during normal pit exploration
            if (!pitBounds.Contains(currentTile))
            {
                Debug.Log("[HeroComponent] Hero truly exited pit area - resetting GOAP flags");
                
                // Reset flags when actually leaving pit area
                InsidePit = false;
                
                // Reset wizard orb workflow flags when leaving pit
                ActivatedWizardOrb = false;
                
                var historian = Entity.GetComponent<Historian>();
                historian?.RecordMilestone(MilestoneType.FirstJumpOutOfPit, Time.TotalTime);
            }
            else
            {
                Debug.Log("[HeroComponent] Hero still inside pit area - ignoring spurious trigger exit");
            }
        }

        /// <summary>
        /// Get current tile position using TileByTileMover if available
        /// </summary>
        public Point GetCurrentTilePosition()
        {
            var tileMover = Entity.GetComponent<TileByTileMover>();
            if (tileMover != null)
            {
                return tileMover.GetCurrentTileCoordinates();
            }
            
            // Fallback to manual calculation
            return GetTileCoordinates(Entity.Transform.Position, GameConfig.TileSize);
        }

        /// <summary>
        /// Check if hero is adjacent to the pit (collision tile within pit rectangle)
        /// </summary>
        public bool CheckAdjacentToPit(Vector2 position)
        {
            var tile = GetTileCoordinates(position, GameConfig.TileSize);
            if (PitCollisionRect.Contains(tile))
                return false; // inside is not "adjacent"
            return DistanceTiles(tile, PitCenter) <= GameConfig.PitAdjacencyRadiusTiles;
        }

        /// <summary>
        /// Check if hero is inside the pit
        /// </summary>
        public bool CheckInsidePit(Vector2 position)
        {
            var tile = GetTileCoordinates(position, GameConfig.TileSize);
            return PitCollisionRect.Contains(tile);
        }

        /// <summary>
        /// Get the pit center coordinates
        /// </summary>
        public Point GetPitCenter()
        {
            return PitCenter;
        }

        /// <summary>
        /// Check if there are any undefeated monsters in adjacent tiles to the hero
        /// </summary>
        public bool CheckAdjacentToMonster()
        {
            var heroTile = GetCurrentTilePosition();
            var scene = Core.Scene;
            if (scene == null) return false;

            var monsterEntities = scene.FindEntitiesWithTag(GameConfig.TAG_MONSTER);
            foreach (var monster in monsterEntities)
            {
                var monsterTile = GetTileCoordinates(monster.Transform.Position, GameConfig.TileSize);
                if (IsAdjacent(heroTile, monsterTile))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Check if there are any unopened chests in adjacent tiles to the hero
        /// </summary>
        public bool CheckAdjacentToChest()
        {
            var heroTile = GetCurrentTilePosition();
            var scene = Core.Scene;
            if (scene == null) return false;

            var chestEntities = scene.FindEntitiesWithTag(GameConfig.TAG_TREASURE);
            foreach (var chest in chestEntities)
            {
                var chestTile = GetTileCoordinates(chest.Transform.Position, GameConfig.TileSize);
                var treasureComponent = chest.GetComponent<TreasureComponent>();
                if (IsAdjacent(heroTile, chestTile) && treasureComponent.State == TreasureComponent.TreasureState.CLOSED)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Check if two tile positions are adjacent in cardinal directions (N/S/E/W only)
        /// </summary>
        private bool IsAdjacent(Point tile1, Point tile2)
        {
            int dx = Math.Abs(tile1.X - tile2.X);
            int dy = Math.Abs(tile1.Y - tile2.Y);
            return (dx + dy) == 1; // cardinal adjacency only
        }

        /// <summary>
        /// Gets the priorities in order (Priority1, Priority2, Priority3)
        /// </summary>
        public HeroPitPriority[] GetPrioritiesInOrder()
        {
            return new HeroPitPriority[] { Priority1, Priority2, Priority3 };
        }

        /// <summary>
        /// Checks if a specific pit priority is satisfied
        /// </summary>
        public bool IsPrioritySatisfied(HeroPitPriority priority)
        {
            switch (priority)
            {
                case HeroPitPriority.Treasure:
                    return AreAllReachableTilesUncoveredAndAllTreasuresOpened();
                case HeroPitPriority.Battle:
                    return AreAllReachableTilesUncoveredAndAllMonstersDefeated();
                case HeroPitPriority.Advance:
                    return FoundWizardOrb; // Satisfied when wizard orb is uncovered
                default:
                    return false;
            }
        }

        /// <summary>
        /// Gets the next unsatisfied priority in the ordered list
        /// </summary>
        public HeroPitPriority? GetNextPriority()
        {
            var priorities = GetPrioritiesInOrder();
            foreach (var priority in priorities)
            {
                if (!IsPrioritySatisfied(priority))
                {
                    return priority;
                }
            }
            return null; // All priorities satisfied
        }

        /// <summary>
        /// Updates ExploredPit based on satisfied priorities
        /// </summary>
        public void UpdateExploredPitBasedOnPriorities()
        {
            var nextPriority = GetNextPriority();
            
            if (nextPriority == null)
            {
                // All priorities satisfied
                ExploredPit = true;
                return;
            }

            // Check if next priority is satisfied and there are no more priorities after it
            var priorities = GetPrioritiesInOrder();
            for (int i = 0; i < priorities.Length; i++)
            {
                if (priorities[i] == nextPriority.Value)
                {
                    if (IsPrioritySatisfied(nextPriority.Value))
                    {
                        // Check if this is the last priority or if it's Advance (special case)
                        if (nextPriority.Value == HeroPitPriority.Advance || i == priorities.Length - 1)
                        {
                            ExploredPit = true;
                        }
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// Check if all reachable tiles are uncovered and all treasures are opened
        /// </summary>
        private bool AreAllReachableTilesUncoveredAndAllTreasuresOpened()
        {
            // For now, return false to indicate this needs proper implementation
            // TODO: Implement proper logic to check all reachable tiles and treasures
            return false;
        }

        /// <summary>
        /// Check if all reachable tiles are uncovered and all monsters are defeated
        /// </summary>
        private bool AreAllReachableTilesUncoveredAndAllMonstersDefeated()
        {
            // For now, return false to indicate this needs proper implementation
            // TODO: Implement proper logic to check all reachable tiles and monsters
            return false;
        }
    }
}