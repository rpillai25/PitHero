using Microsoft.Xna.Framework;
using Nez;
using PitHero.AI.Interfaces;
using PitHero.ECS.Components;
using PitHero.Services;
using System.Collections;
using System.Collections.Generic;

namespace PitHero.AI
{
    /// <summary>
    /// Action that causes the hero to sleep in bed and restore HP and MP to full
    /// Requires 10 gold to pay the innkeeper
    /// </summary>
    public class SleepInBedAction : HeroActionBase
    {
        private ICoroutine _sleepCoroutine;
        private bool _sleepCompleted;
        private bool _isSleeping;
        private bool _hasReachedPaymentTile;
        private bool _hasPaidInnkeeper;

        public SleepInBedAction() : base(GoapConstants.SleepInBedAction, 5)
        {
            SetPrecondition(GoapConstants.OutsidePit, true);
            SetPrecondition(GoapConstants.HPCritical, true);
            SetPrecondition(GoapConstants.HasEnoughInnGold, true);

            SetPostcondition(GoapConstants.HPCritical, false);
            
            _isSleeping = false;
            _hasReachedPaymentTile = false;
            _hasPaidInnkeeper = false;
        }

        /// <summary>
        /// Execute the sleep action - walk to payment tile, pay innkeeper, sleep for 10 seconds and restore full HP and MP
        /// NOTE: Gold check happens here since we can't add dynamic preconditions
        /// </summary>
        public override bool Execute(HeroComponent hero)
        {
            // If we've already completed sleeping, return true
            if (_sleepCompleted)
            {
                Debug.Log("[SleepInBedAction] Sleep previously completed, resetting for next use");
                _sleepCompleted = false; // Reset for next time this action is used
                _isSleeping = false;
                _hasReachedPaymentTile = false;
                _hasPaidInnkeeper = false;
                return true;
            }

            // If coroutine is still running, return false (not complete)
            if (_sleepCoroutine != null)
            {
                Debug.Log("[SleepInBedAction] Sleep in progress...");
                return false;
            }

            // Check if hero has enough gold to pay for the inn (only before starting the action)
            var gameState = Core.Services.GetService<GameStateService>();
            if (gameState == null || gameState.Funds < GameConfig.InnCostGold)
            {
                Debug.Log($"[SleepInBedAction] Not enough gold to sleep at inn. Have {gameState?.Funds ?? 0}, need {GameConfig.InnCostGold}");
                return true; // Return true to mark action as "complete" so hero can try other actions
            }

            // Start the sleep coroutine
            Debug.Log("[SleepInBedAction] Starting sleep action");
            _isSleeping = true;
            _sleepCoroutine = Core.StartCoroutine(SleepCoroutine(hero));
            return false; // Not complete yet
        }

        /// <summary>
        /// Coroutine that walks to payment tile, pays innkeeper, then sleeps for 10 seconds and heals the hero and hired mercenaries to full HP and MP
        /// </summary>
        private IEnumerator SleepCoroutine(HeroComponent hero)
        {
            var heroEntity = hero.Entity;
            var tileMover = heroEntity.GetComponent<TileByTileMover>();
            var facingComponent = heroEntity.GetComponent<ActorFacingComponent>();

            if (tileMover == null)
            {
                Debug.Error("[SleepInBedAction] Hero entity missing TileByTileMover");
                yield break;
            }




            // Step 1: Hero should already be at payment tile (67, 3) from HeroStateMachine GoTo state
            // Just verify we're there and face the innkeeper
            var paymentTile = new Point(GameConfig.InnPaymentTileX, GameConfig.InnPaymentTileY);
            var currentTile = tileMover.GetCurrentTileCoordinates();

            Debug.Log($"[SleepInBedAction] Starting sleep action at ({currentTile.X},{currentTile.Y})");

            // If not at payment tile, walk there directly (shouldn't normally happen)
            if (currentTile != paymentTile)
            {
                Debug.Warn($"[SleepInBedAction] Hero not at payment tile, walking from ({currentTile.X},{currentTile.Y}) to ({paymentTile.X},{paymentTile.Y})");
                
                var pathfinding = heroEntity.GetComponent<PathfindingActorComponent>();
                if (pathfinding != null && pathfinding.IsPathfindingInitialized)
                {
                    var path = pathfinding.CalculatePath(currentTile, paymentTile);
                    if (path != null && path.Count > 0)
                    {
                        // Follow the path to payment tile
                        for (int i = 0; i < path.Count; i++)
                        {
                            var targetTile = path[i];
                            var currentTilePos = new Point(
                                (int)(heroEntity.Transform.Position.X / GameConfig.TileSize),
                                (int)(heroEntity.Transform.Position.Y / GameConfig.TileSize)
                            );

                            // Determine direction to move
                            var dx = targetTile.X - currentTilePos.X;
                            var dy = targetTile.Y - currentTilePos.Y;

                            Direction? direction = null;
                            if (dx > 0) direction = Direction.Right;
                            else if (dx < 0) direction = Direction.Left;
                            else if (dy > 0) direction = Direction.Down;
                            else if (dy < 0) direction = Direction.Up;

                            if (direction.HasValue)
                            {
                                tileMover.StartMoving(direction.Value);

                                // Wait for movement to complete
                                while (tileMover.IsMoving)
                                {
                                    yield return null;
                                }
                            }

                            // Small delay between moves
                            yield return Coroutine.WaitForSeconds(0.05f);
                        }
                    }
                    else
                    {
                        // No path found - teleport to payment tile
                        Debug.Warn("[SleepInBedAction] No path to payment tile, teleporting");
                        var paymentWorldPos = new Vector2(
                            paymentTile.X * GameConfig.TileSize + GameConfig.TileSize / 2,
                            paymentTile.Y * GameConfig.TileSize + GameConfig.TileSize / 2
                        );
                        heroEntity.Transform.Position = paymentWorldPos;
                        tileMover.SnapToTileGrid();
                    }
                }
                else
                {
                    // No pathfinding - teleport to payment tile
                    Debug.Warn("[SleepInBedAction] No pathfinding available, teleporting to payment tile");
                    var paymentWorldPos = new Vector2(
                        paymentTile.X * GameConfig.TileSize + GameConfig.TileSize / 2,
                        paymentTile.Y * GameConfig.TileSize + GameConfig.TileSize / 2
                    );
                    heroEntity.Transform.Position = paymentWorldPos;
                    tileMover.SnapToTileGrid();
                }
            }


            _hasReachedPaymentTile = true;

            // Step 2: Face right (towards innkeeper)
            if (facingComponent != null)
            {
                facingComponent.SetFacing(Direction.Right);
                Debug.Log("[SleepInBedAction] Hero facing right towards innkeeper");
            }

            // Wait a brief moment (payment animation would go here)
            yield return Coroutine.WaitForSeconds(0.5f);

            // Step 3: Pay the innkeeper (deduct 10 gold)
            var gameState = Core.Services.GetService<GameStateService>();
            if (gameState != null && gameState.Funds >= GameConfig.InnCostGold)
            {
                gameState.Funds -= GameConfig.InnCostGold;
                _hasPaidInnkeeper = true;
                Debug.Log($"[SleepInBedAction] Paid {GameConfig.InnCostGold} gold to innkeeper. Remaining funds: {gameState.Funds}");
            }
            else
            {
                Debug.Error("[SleepInBedAction] Not enough gold to pay innkeeper!");
                _sleepCompleted = true;
                _sleepCoroutine = null;
                _isSleeping = false;
                yield break;
            }

            // Step 4: Walk to bed (73, 3)
            var bedTile = new Point(73, 3);
            currentTile = tileMover.GetCurrentTileCoordinates();

            Debug.Log($"[SleepInBedAction] Walking to bed ({bedTile.X},{bedTile.Y}) from ({currentTile.X},{currentTile.Y})");

            if (currentTile != bedTile)
            {
                // Use pathfinding to walk to bed
                var pathfinding = heroEntity.GetComponent<PathfindingActorComponent>();
                if (pathfinding != null && pathfinding.IsPathfindingInitialized)
                {
                    var path = pathfinding.CalculatePath(currentTile, bedTile);
                    if (path != null && path.Count > 0)
                    {
                        Debug.Log($"[SleepInBedAction] Found path to bed with {path.Count} steps");
                        
                        // Follow the path
                        for (int i = 0; i < path.Count; i++)
                        {
                            var targetTile = path[i];
                            var currentTilePos = new Point(
                                (int)(heroEntity.Transform.Position.X / GameConfig.TileSize),
                                (int)(heroEntity.Transform.Position.Y / GameConfig.TileSize)
                            );

                            // Determine direction to move
                            var dx = targetTile.X - currentTilePos.X;
                            var dy = targetTile.Y - currentTilePos.Y;

                            Direction? direction = null;
                            if (dx > 0) direction = Direction.Right;
                            else if (dx < 0) direction = Direction.Left;
                            else if (dy > 0) direction = Direction.Down;
                            else if (dy < 0) direction = Direction.Up;

                            if (direction.HasValue)
                            {
                                tileMover.StartMoving(direction.Value);

                                // Wait for movement to complete
                                while (tileMover.IsMoving)
                                {
                                    yield return null;
                                }
                            }

                            // Small delay between moves
                            yield return Coroutine.WaitForSeconds(0.05f);
                        }
                    }
                    else
                    {
                        // No path found - just teleport
                        Debug.Warn("[SleepInBedAction] No path to bed, teleporting");
                        var bedWorldPos = new Vector2(
                            bedTile.X * GameConfig.TileSize + GameConfig.TileSize / 2,
                            bedTile.Y * GameConfig.TileSize + GameConfig.TileSize / 2
                        );
                        heroEntity.Transform.Position = bedWorldPos;
                        tileMover.SnapToTileGrid();
                    }
                }
                else
                {
                    // No pathfinding - just teleport
                    Debug.Warn("[SleepInBedAction] No pathfinding available, teleporting to bed");
                    var bedWorldPos = new Vector2(
                        bedTile.X * GameConfig.TileSize + GameConfig.TileSize / 2,
                        bedTile.Y * GameConfig.TileSize + GameConfig.TileSize / 2
                    );
                    heroEntity.Transform.Position = bedWorldPos;
                    tileMover.SnapToTileGrid();
                }
            }

            Debug.Log("[SleepInBedAction] Hero is now in bed, starting sleep...");

            // Teleport hired mercenaries to their beds (simple approach - no pathfinding needed)
            var mercenaryManager = Core.Services.GetService<MercenaryManager>();
            var hiredMercenaries = mercenaryManager?.GetHiredMercenaries() ?? new List<Entity>();
            
            Debug.Log($"[SleepInBedAction] Found {hiredMercenaries.Count} hired mercenaries to teleport to beds");
            
            var mercBedPositions = new Point[] { new Point(76, 3), new Point(73, 7) };
            
            for (int i = 0; i < hiredMercenaries.Count && i < 2; i++)
            {
                var merc = hiredMercenaries[i];
                var mercTileMover = merc.GetComponent<TileByTileMover>();
                var mercFollowComp = merc.GetComponent<MercenaryFollowComponent>();
                var mercComp = merc.GetComponent<MercenaryComponent>();
                
                if (mercTileMover != null)
                {
                    // Disable following component to prevent interference during sleep
                    if (mercFollowComp != null)
                    {
                        mercFollowComp.Enabled = false;
                        Debug.Log($"[SleepInBedAction] Disabled following for mercenary {i + 1}");
                    }
                    
                    // Stop any current movement
                    if (mercTileMover.IsMoving)
                    {
                        mercTileMover.StopMoving();
                    }
                    
                    // Teleport mercenary to bed position
                    var bedPos = mercBedPositions[i];
                    var bedWorldPos = new Vector2(
                        bedPos.X * GameConfig.TileSize + GameConfig.TileSize / 2,
                        bedPos.Y * GameConfig.TileSize + GameConfig.TileSize / 2
                    );
                    merc.Transform.Position = bedWorldPos;
                    mercTileMover.SnapToTileGrid();
                    
                    // Update LastTilePosition so mercenary knows where it is
                    if (mercComp != null)
                    {
                        mercComp.LastTilePosition = bedPos;
                    }
                    
                    Debug.Log($"[SleepInBedAction] Teleported mercenary {i + 1} to bed at ({bedPos.X},{bedPos.Y})");
                }
            }

            // Wait for 10 seconds (sleep duration)
            float elapsed = 0f;
            while (elapsed < 10f)
            {
                elapsed += Time.DeltaTime;
                yield return null;
            }

            Debug.Log("[SleepInBedAction] Sleep complete, restoring HP and MP to full for hero and mercenaries");

            // Heal hero to full HP and MP
            if (hero.LinkedHero != null)
            {
                int hpBefore = hero.LinkedHero.CurrentHP;
                int mpBefore = hero.LinkedHero.CurrentMP;

                // Restore HP to full
                bool hpRestored = hero.LinkedHero.RestoreHP(hero.LinkedHero.MaxHP - hero.LinkedHero.CurrentHP);
                
                // Restore MP to full (negative amount = full restore)
                bool mpRestored = hero.LinkedHero.RestoreMP(-1);

                if (hpRestored)
                {
                    int healAmount = hero.LinkedHero.CurrentHP - hpBefore;
                    Debug.Log($"[SleepInBedAction] Restored {healAmount} HP to hero. Current HP: {hero.LinkedHero.CurrentHP}/{hero.LinkedHero.MaxHP}");
                }
                else
                {
                    Debug.Log("[SleepInBedAction] Hero already at full HP");
                }

                if (mpRestored)
                {
                    int mpRestoreAmount = hero.LinkedHero.CurrentMP - mpBefore;
                    Debug.Log($"[SleepInBedAction] Restored {mpRestoreAmount} MP to hero. Current MP: {hero.LinkedHero.CurrentMP}/{hero.LinkedHero.MaxMP}");
                }
                else
                {
                    Debug.Log("[SleepInBedAction] Hero already at full MP");
                }
            }

            // Reset healing exhausted flags so items and skills can be tried again
            hero.HealingItemExhausted = false;
            hero.HealingSkillExhausted = false;
            Debug.Log("[SleepInBedAction] Reset HealingItemExhausted and HealingSkillExhausted flags");

            // Heal hired mercenaries to full HP and MP
            for (int i = 0; i < hiredMercenaries.Count; i++)
            {
                var merc = hiredMercenaries[i];
                var mercComp = merc.GetComponent<MercenaryComponent>();
                
                if (mercComp?.LinkedMercenary != null)
                {
                    int hpBefore = mercComp.LinkedMercenary.CurrentHP;
                    int mpBefore = mercComp.LinkedMercenary.CurrentMP;
                    
                    // Calculate how much HP/MP to restore
                    int hpToRestore = mercComp.LinkedMercenary.MaxHP - mercComp.LinkedMercenary.CurrentHP;
                    int mpToRestore = mercComp.LinkedMercenary.MaxMP - mercComp.LinkedMercenary.CurrentMP;
                    
                    // Restore HP and MP to full
                    if (hpToRestore > 0)
                    {
                        mercComp.LinkedMercenary.Heal(hpToRestore);
                        Debug.Log($"[SleepInBedAction] Restored {hpToRestore} HP to mercenary {mercComp.LinkedMercenary.Name}. Current HP: {mercComp.LinkedMercenary.CurrentHP}/{mercComp.LinkedMercenary.MaxHP}");
                    }
                    else
                    {
                        Debug.Log($"[SleepInBedAction] Mercenary {mercComp.LinkedMercenary.Name} already at full HP");
                    }
                    
                    if (mpToRestore > 0)
                    {
                        mercComp.LinkedMercenary.RestoreMP(mpToRestore);
                        Debug.Log($"[SleepInBedAction] Restored {mpToRestore} MP to mercenary {mercComp.LinkedMercenary.Name}. Current MP: {mercComp.LinkedMercenary.CurrentMP}/{mercComp.LinkedMercenary.MaxMP}");
                    }
                    else
                    {
                        Debug.Log($"[SleepInBedAction] Mercenary {mercComp.LinkedMercenary.Name} already at full MP");
                    }
                }
            }

            // Wait a brief moment before waking up
            yield return Coroutine.WaitForSeconds(0.5f);

            // Re-enable mercenary following BEFORE hero exits (like old working code)
            // This allows mercenaries to pathfind out of beds naturally
            for (int i = 0; i < hiredMercenaries.Count; i++)
            {
                var merc = hiredMercenaries[i];
                var mercFollowComp = merc.GetComponent<MercenaryFollowComponent>();
                
                if (mercFollowComp != null)
                {
                    mercFollowComp.ResetPathfinding();
                    mercFollowComp.Enabled = true;
                    Debug.Log($"[SleepInBedAction] Re-enabled following for mercenary {i + 1}");
                }
            }

            // Step 5: Walk hero out of bed to exit tile (71, 3) - between payment tile and bed
            var exitTile = new Point(71, 3);
            currentTile = tileMover.GetCurrentTileCoordinates();

            Debug.Log($"[SleepInBedAction] Waking up - walking to exit tile ({exitTile.X},{exitTile.Y}) from ({currentTile.X},{currentTile.Y})");

            if (currentTile != exitTile)
            {
                // Use pathfinding to walk to exit tile
                var pathfinding = heroEntity.GetComponent<PathfindingActorComponent>();
                if (pathfinding != null && pathfinding.IsPathfindingInitialized)
                {
                    var path = pathfinding.CalculatePath(currentTile, exitTile);
                    if (path != null && path.Count > 0)
                    {
                        Debug.Log($"[SleepInBedAction] Found path to exit with {path.Count} steps");
                        
                        // Follow the path
                        for (int i = 0; i < path.Count; i++)
                        {
                            var targetTile = path[i];
                            var currentTilePos = new Point(
                                (int)(heroEntity.Transform.Position.X / GameConfig.TileSize),
                                (int)(heroEntity.Transform.Position.Y / GameConfig.TileSize)
                            );

                            // Determine direction to move
                            var dx = targetTile.X - currentTilePos.X;
                            var dy = targetTile.Y - currentTilePos.Y;

                            Direction? direction = null;
                            if (dx > 0) direction = Direction.Right;
                            else if (dx < 0) direction = Direction.Left;
                            else if (dy > 0) direction = Direction.Down;
                            else if (dy < 0) direction = Direction.Up;

                            if (direction.HasValue)
                            {
                                tileMover.StartMoving(direction.Value);

                                // Wait for movement to complete
                                while (tileMover.IsMoving)
                                {
                                    yield return null;
                                }
                            }

                            // Small delay between moves
                            yield return Coroutine.WaitForSeconds(0.05f);
                        }
                    }
                    else
                    {
                        Debug.Warn("[SleepInBedAction] No path to exit, teleporting");
                        var exitWorldPos = new Vector2(
                            exitTile.X * GameConfig.TileSize + GameConfig.TileSize / 2,
                            exitTile.Y * GameConfig.TileSize + GameConfig.TileSize / 2
                        );
                        heroEntity.Transform.Position = exitWorldPos;
                        tileMover.SnapToTileGrid();
                    }
                }
                else
                {
                    Debug.Warn("[SleepInBedAction] No pathfinding available, teleporting to exit");
                    var exitWorldPos = new Vector2(
                        exitTile.X * GameConfig.TileSize + GameConfig.TileSize / 2,
                        exitTile.Y * GameConfig.TileSize + GameConfig.TileSize / 2
                    );
                    heroEntity.Transform.Position = exitWorldPos;
                    tileMover.SnapToTileGrid();
                }
            }

            Debug.Log("[SleepInBedAction] Hero has exited the bed, mercenaries will pathfind out naturally");

            // Mark sleep as completed and clear the coroutine reference
            _sleepCompleted = true;
            _sleepCoroutine = null;
            _isSleeping = false;

            Debug.Log("[SleepInBedAction] Sleep action completed, hero has left the inn");
        }

        /// <summary>
        /// Execute action using interface-based context (new approach)
        /// </summary>
        public override bool Execute(IGoapContext context)
        {
            context.LogDebug("[SleepInBedAction] Sleep action executed (interface-based context)");
            // For virtual game context, just restore HP immediately
            // In real game, the coroutine handles the delay
            return true;
        }

        /// <summary>
        /// Check if the sleep action is still in progress
        /// </summary>
        public bool IsSleeping => _isSleeping;
    }
}
