using Microsoft.Xna.Framework;
using Nez;
using Nez.AI.GOAP;
using PitHero.AI;
using PitHero.Util;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Component for heroes in the game - simplified to only contain the 7 required state properties
    /// </summary>
    public class HeroComponent : PathfindingActorComponent
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

        private PitWidthManager _pitWidthManager;

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
        /// Apply movement speed to the TileByTileMover based on whether hero is inside or outside the pit
        /// </summary>
        private void ApplyMovementSpeedForPitState()
        {
            var mover = Entity?.GetComponent<TileByTileMover>();
            if (mover == null)
                return;

            var newSpeed = _insidePit ? GameConfig.HeroPitMovementSpeed : GameConfig.HeroMovementSpeed;
            mover.MovementSpeed = newSpeed;

            Debug.Log($"[HeroComponent] Movement speed set based on pit state. InsidePit={_insidePit}, Speed={newSpeed}");
        }

        /// <summary>
        /// Apply movement speed to the TileByTileMover based on whether the target tile has fog of war
        /// </summary>
        public void ApplyMovementSpeedForFogStatus(Point targetTilePosition)
        {
            var mover = Entity?.GetComponent<TileByTileMover>();
            if (mover == null)
                return;

            var tms = Core.Services.GetService<TiledMapService>();
            if (tms == null)
                return;

            var hasFog = tms.HasFogOfWar(targetTilePosition);
            var newSpeed = hasFog ? GameConfig.HeroPitMovementSpeed : GameConfig.HeroMovementSpeed;
            mover.MovementSpeed = newSpeed;

            Debug.Log($"[HeroComponent] Movement speed set based on fog status. TargetTile=({targetTilePosition.X},{targetTilePosition.Y}), HasFog={hasFog}, Speed={newSpeed}");
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
        }

        /// <summary>
        /// Override to set goal state based on hero's desired state
        /// </summary>
        public override void SetGoalState(ref WorldState goalState)
        {
            // 2 main goals for the hero so far. The planner should always plan the optimal path of actions to these goals.
            
            if (PitInitialized && !ActivatedWizardOrb)
            {
                goalState.Set(GoapConstants.ActivatedWizardOrb, true);
            }
            else if (!PitInitialized && ActivatedWizardOrb)
            {
                goalState.Set(GoapConstants.PitInitialized, true);
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
        private Point GetCurrentTilePosition()
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
    }
}