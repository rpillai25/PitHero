using Microsoft.Xna.Framework;
using Nez;
using PitHero;
using PitHero.AI.Interfaces;
using PitHero.Dining;
using PitHero.ECS.Components;
using PitHero.Services;
using PitHero.Util;
using PitHero.Util.SoundEffectTypes;
using System;
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
        private static readonly Direction[] SleepFacingDirections = new Direction[] { Direction.Left, Direction.Right, Direction.Up, Direction.Down };

        private ICoroutine _sleepCoroutine;
        private bool _sleepCompleted;
        private bool _isSleeping;
        private bool _hasReachedPaymentTile;
        private bool _hasPaidInnkeeper;        

        public SleepInBedAction() : base(GoapConstants.SleepInBedAction, 5)
        {
            SetPrecondition(GoapConstants.OutsidePit, true);
            // Note: No HPCritical precondition — this action can satisfy either HPCritical or MPCritical goals.
            // The Validate() method ensures at least one of these conditions is true.

            // Inn restores both HP and MP to full; night sleep satisfies the IsNighttime goal
            SetPostcondition(GoapConstants.HPCritical, false);
            SetPostcondition(GoapConstants.MPCritical, false);
            SetPostcondition(GoapConstants.IsNighttime, false);
            
            _isSleeping = false;
            _hasReachedPaymentTile = false;
            _hasPaidInnkeeper = false;
        }

        public override bool Validate()
        {
            var heroComponent = Game1.Scene.FindEntity("hero")?.GetComponent<HeroComponent>();

            // Night sleep is always valid — no HP/MP or gold requirement
            var timeService = Core.Services.GetService<InGameTimeService>();
            if (timeService?.IsNighttime == true)
                return true;

            var healPrioritiesInOrder = heroComponent?.GetHealPrioritiesInOrder();

            // Must have either HPCritical or MPCritical
            if (!heroComponent.HPCritical && !heroComponent.MPCritical)
            {
                return false;
            }
            if (!heroComponent.HasEnoughInnGold)
            {
                heroComponent.InnExhausted = true;
                return false;
            }

            if (healPrioritiesInOrder != null)
            {
                int innPriority = Array.IndexOf(healPrioritiesInOrder, HeroHealPriority.Inn);
                int skillPriority = Array.IndexOf(healPrioritiesInOrder, HeroHealPriority.HealingSkill);
                int itemPriority = Array.IndexOf(healPrioritiesInOrder, HeroHealPriority.HealingItem);

                // Check if we should wait for a higher-priority option
                // Note: HealingSkill can only address HPCritical, not MPCritical, so when only MP is low,
                // we should NOT wait for HealingSkill even if it has higher priority
                bool shouldWaitForSkill = innPriority > skillPriority && 
                                          !heroComponent.HealingSkillExhausted &&
                                          heroComponent.HPCritical; // Only wait if HP is critical (skill can help)
                
                bool shouldWaitForItem = innPriority > itemPriority && !heroComponent.HealingItemExhausted;

                if (shouldWaitForSkill || shouldWaitForItem)
                {
                    return false;
                }
            }

            return true;
        }

        public override bool ShouldNotOverride()
        {
            //Should not override this action if coroutine is still running
            return _sleepCoroutine != null;
        }

        /// <summary>
        /// Instantly poses an actor asleep in bed: random sleep facing, closed-eye sleep
        /// animation, paused. Used by the night-load spawn path so the party never renders
        /// awake (open eyes, walk-cycle bobbing) while waiting for the sleep plan to start.
        /// </summary>
        public static void ApplySleepPose(Entity entity)
        {
            var dir = SleepFacingDirections[Nez.Random.Range(0, 4)];
            var facing = entity.GetComponent<ActorFacingComponent>();
            if (facing != null)
            {
                facing.SetFacing(dir);
                facing.ConsumeDirtyFlag();
            }

            var animComps = entity.GetComponents<HeroAnimationComponent>();
            for (int i = 0; i < animComps.Count; i++)
                animComps[i].ForceSleepPose(dir);

            SetSleepRenderOffset(entity, true);
        }

        /// <summary>
        /// Shifts the actor's composited sprite down into the bed while asleep (render-only —
        /// entity position, tile coordinates and pathfinding are untouched). Idempotent.
        /// </summary>
        private static void SetSleepRenderOffset(Entity entity, bool asleep)
        {
            var compositor = entity.GetComponent<MultiSpriteAnimator>();
            if (compositor != null)
                compositor.SetLocalOffset(new Vector2(
                    compositor.LocalOffset.X,
                    asleep ? GameConfig.SleepInBedSpriteOffsetY : 0f));
        }

        /// <summary>Unpauses every animation layer on the hero and all hired mercenaries.</summary>
        private static void UnpausePartyAnimations(Entity heroEntity)
        {
            var heroAnims = heroEntity.GetComponents<HeroAnimationComponent>();
            for (int i = 0; i < heroAnims.Count; i++)
                heroAnims[i].UnpauseAnimation();

            SetSleepRenderOffset(heroEntity, false);

            var hired = Core.Services.GetService<MercenaryManager>()?.GetHiredMercenaries();
            for (int i = 0; hired != null && i < hired.Count; i++)
            {
                var mercAnims = hired[i].GetComponents<HeroAnimationComponent>();
                for (int j = 0; j < mercAnims.Count; j++)
                    mercAnims[j].UnpauseAnimation();
                SetSleepRenderOffset(hired[i], false);
            }
        }

        /// <summary>
        /// Execute the sleep action - walk to payment tile, pay innkeeper (if not night sleep), then sleep and restore HP/MP
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
                hero.IsSleeping = false;
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

            var timeService = Core.Services.GetService<InGameTimeService>();
            bool isNightSleep = timeService?.IsNighttime == true;

            // Night-time load spawned the party already in bed — skip the walk/pay steps.
            bool spawnAsleep = hero.SpawnedAsleepPending;
            hero.SpawnedAsleepPending = false;
            if (spawnAsleep && !isNightSleep)
            {
                // Clock crossed 6 AM between load and the first plan — undo the spawn-asleep
                // staging (including the paused sleep poses) and behave like a normal (paid) inn visit.
                hero.IsSleeping = false;
                UnpausePartyAnimations(hero.Entity);
                WalkToTavernForStopAction.ReenableMercenaryFollowing();
                spawnAsleep = false;
            }

            // Night sleep is free — skip gold check
            if (!isNightSleep)
            {
                var gameState = Core.Services.GetService<GameStateService>();
                int innCost = Services.InnCostCalculator.GetCurrentPartyCost(hero.LinkedHero);
                if (gameState == null || gameState.Funds < innCost)
                {
                    Debug.Log($"[SleepInBedAction] Not enough gold to sleep at inn. Have {gameState?.Funds ?? 0}, need {innCost}");
                    return true; // Return true to mark action as "complete" so hero can try other actions
                }
            }

            // Start the sleep coroutine
            Debug.Log($"[SleepInBedAction] Starting sleep action (isNightSleep={isNightSleep}, spawnAsleep={spawnAsleep})");
            _isSleeping = true;
            hero.IsSleeping = true;
            _sleepCoroutine = Core.StartCoroutine(SleepCoroutine(hero, isNightSleep, spawnAsleep));
            return false; // Not complete yet
        }

        /// <summary>
        /// Coroutine that walks to payment tile, optionally pays innkeeper (free for night sleep), then sleeps and heals the hero and hired mercenaries.
        /// When spawnAsleep is set (night-time load placed the party in the beds already), the innkeeper walk/pay steps are skipped.
        /// </summary>
        private IEnumerator SleepCoroutine(HeroComponent hero, bool isNightSleep, bool spawnAsleep)
        {
            var heroEntity = hero.Entity;
            var tileMover = heroEntity.GetComponent<TileByTileMover>();
            var facingComponent = heroEntity.GetComponent<ActorFacingComponent>();
            SoundEffectManager soundEffectManager = Core.GetGlobalManager<SoundEffectManager>();

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

            // If not at payment tile, walk there directly (shouldn't normally happen).
            // Skipped entirely when the party spawned asleep — they're already in the beds.
            if (!spawnAsleep && currentTile != paymentTile)
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

            // Step 2: Face right (towards innkeeper) — skipped when already in bed
            if (!spawnAsleep && facingComponent != null)
            {
                facingComponent.SetFacing(Direction.Right);
                Debug.Log("[SleepInBedAction] Hero facing right towards innkeeper");
            }

            // Wait a brief moment (payment animation would go here)
            if (!spawnAsleep)
                yield return Coroutine.WaitForSeconds(0.5f);

            // Step 3: Pay the innkeeper (skipped for free night sleep)
            if (!isNightSleep)
            {
                var gameState = Core.Services.GetService<GameStateService>();
                int innCost = Services.InnCostCalculator.GetCurrentPartyCost(hero.LinkedHero);
                if (gameState != null && gameState.Funds >= innCost)
                {
                    gameState.Funds -= innCost;
                    _hasPaidInnkeeper = true;
                    soundEffectManager.PlaySoundAt(SoundEffectType.PayGold, heroEntity.Transform.Position);
                    SpeechBubbleDialogue.SayInnkeeperGoodRest(Game1.Scene?.FindEntity("innkeeper"));
                    Debug.Log($"[SleepInBedAction] Paid {innCost} gold to innkeeper. Remaining funds: {gameState.Funds}");

                    Services.Analytics.AnalyticsService.LogInnSleep(innCost, gameState.Funds);
                }
                else
                {
                    Debug.Error("[SleepInBedAction] Not enough gold to pay innkeeper!");
                    _sleepCompleted = true;
                    _sleepCoroutine = null;
                    _isSleeping = false;
                    hero.IsSleeping = false;
                    yield break;
                }
            }
            else if (!spawnAsleep) // save-restore of an in-progress night sleep isn't a new inn stay
            {
                Debug.Log("[SleepInBedAction] Night sleep — innkeeper stay is free");

                Services.Analytics.AnalyticsService.LogInnSleep(0,
                    Core.Services.GetService<GameStateService>()?.Funds ?? 0);
            }

            // Step 4: Walk to bed (73, 3)
            var bedTile = new Point(GameConfig.InnHeroBedTileX, GameConfig.InnHeroBedTileY);
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
            
            var mercBedPositions = new Point[]
            {
                new Point(GameConfig.InnMercBed1TileX, GameConfig.InnMercBed1TileY),
                new Point(GameConfig.InnMercBed2TileX, GameConfig.InnMercBed2TileY)
            };
            
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

                        // Bed teleport can cross the pit boundary (safety net — mercs normally
                        // already exited on their own): sync the authoritative flag + graph
                        mercComp.InsidePit = false;
                        merc.GetComponent<PathfindingActorComponent>()?.RefreshPathfindingWithObstacles();
                    }

                    Debug.Log($"[SleepInBedAction] Teleported mercenary {i + 1} to bed at ({bedPos.X},{bedPos.Y})");
                }
            }

            // Set random facing and freeze everyone into their sleep pose. Skipped when the
            // party spawned asleep — the night-load path already posed them (ApplySleepPose),
            // and re-randomizing here would visibly flip facings mid-sleep.
            if (!spawnAsleep)
            {
                Direction heroSleepDir = SleepFacingDirections[Nez.Random.Range(0, 4)];
                if (facingComponent != null)
                    facingComponent.SetFacing(heroSleepDir);

                for (int i = 0; i < hiredMercenaries.Count && i < 2; i++)
                {
                    var mercFacing = hiredMercenaries[i].GetComponent<ActorFacingComponent>();
                    mercFacing?.SetFacing(SleepFacingDirections[Nez.Random.Range(0, 4)]);
                }

                // Wait one frame so HeroAnimationComponent.Update() picks up new facing direction before freeze
                yield return null;

                // Play sleep animations then pause all hero animation layers so hero looks still while sleeping
                var heroAnimComps = heroEntity.GetComponents<HeroAnimationComponent>();
                for (int i = 0; i < heroAnimComps.Count; i++)
                    heroAnimComps[i].PlaySleepAnimationForDirection(heroSleepDir);
                for (int i = 0; i < heroAnimComps.Count; i++)
                    heroAnimComps[i].PauseAnimation();
                SetSleepRenderOffset(heroEntity, true);

                // Play sleep animations then pause all mercenary animation layers
                for (int i = 0; i < hiredMercenaries.Count && i < 2; i++)
                {
                    Direction mercSleepDir = hiredMercenaries[i].GetComponent<ActorFacingComponent>()?.Facing ?? Direction.Down;
                    var mercAnimComps = hiredMercenaries[i].GetComponents<HeroAnimationComponent>();
                    for (int j = 0; j < mercAnimComps.Count; j++)
                        mercAnimComps[j].PlaySleepAnimationForDirection(mercSleepDir);
                    for (int j = 0; j < mercAnimComps.Count; j++)
                        mercAnimComps[j].PauseAnimation();
                    SetSleepRenderOffset(hiredMercenaries[i], true);
                }
            }

            // Night sleep: wait until 6 AM; healing sleep: wait 10 seconds
            if (isNightSleep)
            {
                var timeServiceForSleep = Core.Services.GetService<InGameTimeService>();
                Debug.Log("[SleepInBedAction] Night sleep — waiting until 6 AM");
                while (timeServiceForSleep?.IsNighttime == true)
                {
                    yield return null;
                }
            }
            else
            {
                float elapsed = 0f;
                while (elapsed < 10f)
                {
                    elapsed += Time.DeltaTime;
                    yield return null;
                }
            }

            Debug.Log("[SleepInBedAction] Sleep complete, restoring HP and MP to full for hero and mercenaries");
            soundEffectManager.PlaySoundAt(SoundEffectType.Restorative, heroEntity.Transform.Position);

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
                    
                    // Restore HP to full
                    bool hpRestored = mercComp.LinkedMercenary.RestoreHP(mercComp.LinkedMercenary.MaxHP);
                    if (hpRestored)
                    {
                        int hpRestoreAmount = mercComp.LinkedMercenary.CurrentHP - hpBefore;
                        Debug.Log($"[SleepInBedAction] Restored {hpRestoreAmount} HP to mercenary {mercComp.LinkedMercenary.Name}. Current HP: {mercComp.LinkedMercenary.CurrentHP}/{mercComp.LinkedMercenary.MaxHP}");
                    }
                    else
                    {
                        Debug.Log($"[SleepInBedAction] Mercenary {mercComp.LinkedMercenary.Name} already at full HP");
                    }
                    
                    // Restore MP to full
                    int mpToRestore = mercComp.LinkedMercenary.MaxMP - mercComp.LinkedMercenary.CurrentMP;
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

                    // Unpause mercenary animation layers
                    var mercAnimCompsWake = merc.GetComponents<HeroAnimationComponent>();
                    for (int j = 0; j < mercAnimCompsWake.Count; j++)
                        mercAnimCompsWake[j].UnpauseAnimation();
                    SetSleepRenderOffset(merc, false);

                    Debug.Log($"[SleepInBedAction] Re-enabled following for mercenary {i + 1}");
                }
            }

            // Initialize any mercenaries hired while party was asleep (releases reserved tavern seats)
            var allHiredMercs = mercenaryManager?.GetHiredMercenaries() ?? new List<Entity>();
            for (int i = 0; i < allHiredMercs.Count; i++)
            {
                var mercComp = allHiredMercs[i].GetComponent<MercenaryComponent>();
                if (mercComp != null && mercComp.IsHiredDuringSleep)
                    mercenaryManager.InitializeDeferredMercenary(allHiredMercs[i]);
            }

            // Unpause hero animation layers before walking out
            var heroAnimCompsWake = heroEntity.GetComponents<HeroAnimationComponent>();
            for (int i = 0; i < heroAnimCompsWake.Count; i++)
                heroAnimCompsWake[i].UnpauseAnimation();
            SetSleepRenderOffset(heroEntity, false);

            // Step 5: Walk hero out of bed to exit tile (71, 3) - between payment tile and bed
            var exitTile = new Point(GameConfig.InnExitTileX, GameConfig.InnExitTileY);
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
            hero.IsSleeping = false;
            hero.JustLeftInn = true; // arms the innkeeper farewell at the inn-farewell tile (issue #385)

            // Morning breakfast trip (issue #319): if "Eat at tavern" is enabled and any party
            // member can actually order, enter Stop mode and head to the tavern
            if (isNightSleep)
                Core.Services.GetService<Services.PartyDiningService>()?.BeginAutoDine(MealPeriod.Breakfast);

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
