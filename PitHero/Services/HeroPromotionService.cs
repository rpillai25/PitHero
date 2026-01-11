using Microsoft.Xna.Framework;
using Nez;
using Nez.Sprites;
using PitHero.ECS.Components;
using PitHero.ECS.Scenes;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Inventory;
using RolePlayingFramework.Stats;
using System.Collections;
using System.Linq;

namespace PitHero.Services
{
    /// <summary>
    /// Service that manages promoting mercenaries to heroes when the current hero dies
    /// </summary>
    public class HeroPromotionService
    {
        private Scene _scene;
        private bool _isPromotingHero;
        private Entity _mercenaryBeingPromoted;

        public HeroPromotionService(Scene scene)
        {
            _scene = scene;
            _isPromotingHero = false;
        }

        /// <summary>
        /// Checks if there is a living hero, and if not, starts the promotion process
        /// </summary>
        public void CheckAndPromoteIfNeeded()
        {
            // Don't start new promotion if already in progress
            if (_isPromotingHero)
                return;

            // Don't start promotion if a battle is in progress - wait for battle to end first
            if (AI.HeroStateMachine.IsBattleInProgress)
            {
                Debug.Log("[HeroPromotionService] Battle in progress - delaying promotion check");
                return;
            }

            // Check if there's a living hero
            var heroEntity = _scene.FindEntity("hero");
            if (heroEntity != null)
            {
                var heroComponent = heroEntity.GetComponent<HeroComponent>();
                if (heroComponent?.LinkedHero != null && heroComponent.LinkedHero.CurrentHP > 0)
                {
                    // Living hero exists, nothing to do
                    return;
                }
            }

            // No living hero - try to promote a mercenary
            Debug.Log("[HeroPromotionService] No living hero detected - attempting to promote a mercenary");
            TryPromoteMercenary();
        }

        /// <summary>
        /// Attempts to select and promote a random unhired mercenary to hero
        /// </summary>
        private void TryPromoteMercenary()
        {
            var mercenaryManager = Core.Services.GetService<MercenaryManager>();
            if (mercenaryManager == null)
            {
                Debug.Warn("[HeroPromotionService] MercenaryManager service not found");
                return;
            }

            // Get all unhired mercenaries
            var unhiredMercenaries = mercenaryManager.GetUnhiredMercenaries();
            
            if (unhiredMercenaries.Count == 0)
            {
                Debug.Warn("[HeroPromotionService] No unhired mercenaries available for promotion - will retry later");
                return;
            }

            // Filter to only mercenaries who are waiting in the tavern (already arrived at their position)
            var mercenariesInTavern = unhiredMercenaries.Where(m =>
            {
                var comp = m.GetComponent<MercenaryComponent>();
                return comp != null && comp.IsWaitingInTavern;
            }).ToList();

            if (mercenariesInTavern.Count == 0)
            {
                Debug.Log("[HeroPromotionService] No mercenaries have arrived at tavern yet - will retry later");
                return;
            }

            // Select a random mercenary from those waiting in tavern
            var randomMercenary = mercenariesInTavern.RandomItem();
            var mercComponent = randomMercenary.GetComponent<MercenaryComponent>();
            
            if (mercComponent == null)
            {
                Debug.Error("[HeroPromotionService] Selected mercenary has no MercenaryComponent");
                return;
            }

            Debug.Log($"[HeroPromotionService] Selected {mercComponent.LinkedMercenary.Name} for promotion to hero (waiting in tavern at position ({mercComponent.TavernPosition.X},{mercComponent.TavernPosition.Y})");

            // Add state machine if not already present (for pathfinding and action execution)
            if (!randomMercenary.HasComponent<AI.MercenaryStateMachine>())
            {
                randomMercenary.AddComponent(new AI.MercenaryStateMachine());
                Debug.Log($"[HeroPromotionService] Added MercenaryStateMachine to {mercComponent.LinkedMercenary.Name}");
            }

            // Mark mercenary as being promoted
            mercComponent.IsBeingPromoted = true;
            _mercenaryBeingPromoted = randomMercenary;
            _isPromotingHero = true;

            // Start the promotion process
            Core.StartCoroutine(ExecutePromotionSequence(randomMercenary));
        }

        /// <summary>
        /// Executes the full promotion sequence: walk to statue, face it, lightning strike, convert to hero
        /// </summary>
        private IEnumerator ExecutePromotionSequence(Entity mercenary)
        {
            var mercComponent = mercenary.GetComponent<MercenaryComponent>();
            if (mercComponent == null)
            {
                _isPromotingHero = false;
                yield break;
            }

            Debug.Log($"[HeroPromotionService] Starting promotion sequence for {mercComponent.LinkedMercenary.Name}");

            // Wait for mercenary to arrive at statue (PathfindingActorComponent and state machine will handle walking)
            // The mercenary's state machine will handle walking to the statue when IsBeingPromoted is true
            while (!mercComponent.HasArrivedAtStatue)
            {
                yield return null;
            }

            // Log actual arrival position
            var arrivalPos = mercenary.Transform.Position;
            var arrivalTile = new Point(
                (int)(arrivalPos.X / GameConfig.TileSize),
                (int)(arrivalPos.Y / GameConfig.TileSize)
            );
            Debug.Log($"[HeroPromotionService] {mercComponent.LinkedMercenary.Name} has arrived at statue at tile ({arrivalTile.X},{arrivalTile.Y})");

            // Verify mercenary is actually at the statue location (112,6)
            if (arrivalTile.X != 112 || arrivalTile.Y != 6)
            {
                Debug.Warn($"[HeroPromotionService] WARNING: Mercenary arrived at wrong location! Expected (112,6) but got ({arrivalTile.X},{arrivalTile.Y})");
            }

            // Face the statue for 1 second
            var facingComponent = mercenary.GetComponent<ActorFacingComponent>();
            if (facingComponent != null)
            {
                facingComponent.SetFacing(Direction.Up);
            }

            yield return Coroutine.WaitForSeconds(1.0f);

            // Play lightning strike animation
            yield return PlayLightningStrike(mercenary);

            Debug.Log("[HeroPromotionService] Lightning strike complete, starting conversion to hero");

            // Get MercenaryManager service (used for tavern cleanup and unfreeze later)
            var mercenaryManager = Core.Services.GetService<MercenaryManager>();
            
            // Clean up tavern position BEFORE conversion (while MercenaryComponent still exists)
            if (mercenaryManager != null)
            {
                mercenaryManager.RemovePromotedMercenaryTavernPosition(mercenary);
            }
            else
            {
                Debug.Warn("[HeroPromotionService] MercenaryManager service not found for tavern position cleanup");
            }

            // Convert mercenary to hero (this is now a coroutine)
            yield return ConvertMercenaryToHero(mercenary);

            Debug.Log("[HeroPromotionService] Conversion complete, finishing promotion sequence");

            _isPromotingHero = false;
            _mercenaryBeingPromoted = null;

            Debug.Log("[HeroPromotionService] *** PROMOTION COMPLETE ***");

            // Unblock mercenary hiring and unfreeze/reassign hired mercenaries to new hero
            if (mercenaryManager != null)
            {
                mercenaryManager.UnblockHiring();
                mercenaryManager.UnfreezeAndReassignMercenaries(mercenary);
                Debug.Log("[HeroPromotionService] Unblocked mercenary hiring and reassigned frozen mercenaries to new hero");
            }
            else
            {
                Debug.Warn("[HeroPromotionService] MercenaryManager service not found");
            }
        }

        /// <summary>
        /// Plays the lightning strike animation at the mercenary's position
        /// </summary>
        private IEnumerator PlayLightningStrike(Entity mercenary)
        {
            Debug.Log("[HeroPromotionService] Playing lightning strike animation");

            // Disable mercenary's ability to move and AI during lightning strike
            var tileMover = mercenary.GetComponent<TileByTileMover>();
            var stateMachine = mercenary.GetComponent<AI.MercenaryStateMachine>();
            bool wasMoverEnabled = false;
            bool wasStateMachineEnabled = false;
            
            if (tileMover != null)
            {
                wasMoverEnabled = tileMover.Enabled;
                tileMover.SetEnabled(false);
                Debug.Log("[HeroPromotionService] Disabled TileByTileMover during promotion");
            }
            
            if (stateMachine != null)
            {
                wasStateMachineEnabled = stateMachine.Enabled;
                stateMachine.SetEnabled(false);
                Debug.Log("[HeroPromotionService] Disabled MercenaryStateMachine during promotion");
            }

            // Create temporary entity for lightning animation
            var lightningEntity = _scene.CreateEntity("lightning-strike");
            lightningEntity.SetPosition(mercenary.Transform.Position);

            // Load animation from Actors atlas
            var actorsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/Actors.atlas");
            if (actorsAtlas == null)
            {
                Debug.Error("[HeroPromotionService] Failed to load Actors.atlas for lightning strike");
                yield break;
            }

            var animator = lightningEntity.AddComponent<PausableSpriteAnimator>();
            animator.AddAnimationsFromAtlas(actorsAtlas);
            animator.SetRenderLayer(GameConfig.RenderLayerTop);

            // Play lightning strike animation (assume it plays once)
            animator.Play("LightningStrike", SpriteAnimator.LoopMode.Once);
            Debug.Log("[HeroPromotionService] Lightning animation started");

            // Wait for animation to complete (check IsRunning instead of IsAnimationActive)
            float timeout = 5.0f; // 5 second timeout as safety
            float elapsed = 0f;
            while (animator.IsRunning && elapsed < timeout)
            {
                yield return null;
                elapsed += Time.DeltaTime;
            }

            if (elapsed >= timeout)
            {
                Debug.Warn($"[HeroPromotionService] Lightning animation timed out after {timeout} seconds");
            }
            else
            {
                Debug.Log($"[HeroPromotionService] Lightning animation completed in {elapsed:F2} seconds");
            }

            // Clean up lightning entity
            lightningEntity.Destroy();
            Debug.Log("[HeroPromotionService] Lightning strike animation complete and entity destroyed");
        }

        /// <summary>
        /// Converts a mercenary entity into the new hero entity
        /// </summary>
        private IEnumerator ConvertMercenaryToHero(Entity mercenary)
        {
            Debug.Log("[HeroPromotionService] ConvertMercenaryToHero coroutine started");

            var mercComponent = mercenary.GetComponent<MercenaryComponent>();
            if (mercComponent == null)
            {
                Debug.Error("[HeroPromotionService] Cannot convert mercenary - MercenaryComponent missing");
                yield break;
            }

            Debug.Log($"[HeroPromotionService] Converting {mercComponent.LinkedMercenary.Name} to hero");

            // Remove mercenary-specific components
            mercenary.RemoveComponent<MercenaryComponent>();
            if (mercenary.HasComponent<AI.MercenaryStateMachine>())
            {
                mercenary.RemoveComponent<AI.MercenaryStateMachine>();
            }
            if (mercenary.HasComponent<MercenaryFollowComponent>())
            {
                mercenary.RemoveComponent<MercenaryFollowComponent>();
            }

            // Change entity name and tag
            mercenary.Name = "hero";
            mercenary.SetTag(GameConfig.TAG_HERO);

            // Update collider to hero layer
            var collider = mercenary.GetComponent<BoxCollider>();
            if (collider != null)
            {
                collider.IsTrigger = true;
                Flags.SetFlag(ref collider.CollidesWithLayers, GameConfig.PhysicsTileMapLayer);
                Flags.SetFlag(ref collider.CollidesWithLayers, GameConfig.PhysicsPitLayer);
                Flags.SetFlagExclusive(ref collider.PhysicsLayer, GameConfig.PhysicsHeroWorldLayer);
            }

            // Add hero-specific components
            var heroComponent = mercenary.AddComponent(new HeroComponent
            {
                Health = 25,
                MaxHealth = 25,
                PitInitialized = true
            });

            // CRITICAL: Manually call OnAddedToEntity if it wasn't called automatically
            // This ensures Bag and BattleActionQueue are initialized
            if (heroComponent.Bag == null)
            {
                Debug.Warn("[HeroPromotionService] OnAddedToEntity was not called automatically - calling manually");
                heroComponent.OnAddedToEntity();
            }

            // Generate a random hero crystal for the new hero
            var randomCrystal = GenerateRandomHeroCrystal();
            heroComponent.LinkedHero = new RolePlayingFramework.Heroes.Hero(
                mercComponent.LinkedMercenary.Name,
                randomCrystal.Job,
                randomCrystal.Level,
                randomCrystal.BaseStats,
                randomCrystal
            );

            Debug.Log($"[HeroPromotionService] Created new hero {heroComponent.LinkedHero.Name} with Level {heroComponent.LinkedHero.Level}, HP {heroComponent.LinkedHero.CurrentHP}/{heroComponent.LinkedHero.MaxHP}");

            // Add bouncy digit and text components for damage/miss display (if not already present)
            if (!mercenary.HasComponent<BouncyDigitComponent>())
            {
                var bouncyDigit = mercenary.AddComponent(new BouncyDigitComponent());
                bouncyDigit.SetRenderLayer(GameConfig.RenderLayerLowest);
                bouncyDigit.SetEnabled(false);
            }

            if (!mercenary.HasComponent<BouncyTextComponent>())
            {
                var bouncyText = mercenary.AddComponent(new BouncyTextComponent());
                bouncyText.SetRenderLayer(GameConfig.RenderLayerLowest);
                bouncyText.SetEnabled(false);
            }

            // Add action queue visualization
            var actionQueueViz = mercenary.AddComponent(new ActionQueueVisualizationComponent());
            actionQueueViz.SetRenderLayer(GameConfig.RenderLayerLowest);

            // Add HeroJumpComponent if not already present (needed for pit jumping animation)
            if (!mercenary.HasComponent<HeroJumpComponent>())
            {
                mercenary.AddComponent(new HeroJumpComponent());
                Debug.Log("[HeroPromotionService] Added HeroJumpComponent to promoted hero");
            }

            // Add Historian and HeroStateMachine
            mercenary.AddComponent(new Historian());
            var heroStateMachine = mercenary.AddComponent(new AI.HeroStateMachine());
            
            Debug.Log($"[HeroPromotionService] Added HeroStateMachine to promoted hero");

            Debug.Log($"[HeroPromotionService] Successfully promoted {heroComponent.LinkedHero.Name} to hero");

            // Wait one frame for all components to initialize properly
            Debug.Log("[HeroPromotionService] Waiting one frame for component initialization");
            yield return null;
            Debug.Log("[HeroPromotionService] Component initialization frame complete");

            // Verify Bag was initialized by OnAddedToEntity
            if (heroComponent.Bag == null)
            {
                Debug.Error("[HeroPromotionService] HeroComponent.Bag is STILL null after waiting one frame!");
            }
            else
            {
                Debug.Log($"[HeroPromotionService] HeroComponent.Bag initialized with capacity {heroComponent.Bag.Capacity}");
            }

            // Reconnect UI to the new hero
            Debug.Log("[HeroPromotionService] Reconnecting UI to new hero");
            ReconnectUIToHero(mercenary);
            Debug.Log("[HeroPromotionService] UI reconnection complete");
            
            // Ensure pathfinding is properly initialized
            Debug.Log("[HeroPromotionService] Waiting for pathfinding initialization");
            yield return WaitForPathfindingInitialization(mercenary);
            Debug.Log("[HeroPromotionService] Pathfinding initialization complete");

            Debug.Log("[HeroPromotionService] ConvertMercenaryToHero coroutine complete");
        }

        /// <summary>
        /// Waits for pathfinding to initialize and adds existing obstacles
        /// </summary>
        private IEnumerator WaitForPathfindingInitialization(Entity hero)
        {
            var heroComponent = hero.GetComponent<HeroComponent>();
            if (heroComponent == null)
            {
                Debug.Error("[HeroPromotionService] Cannot initialize pathfinding - HeroComponent missing");
                yield break;
            }

            // Wait until pathfinding is initialized
            while (!heroComponent.IsPathfindingInitialized)
            {
                yield return null;
            }

            Debug.Log("[HeroPromotionService] Pathfinding initialized for promoted hero");

            // Add existing obstacles to the pathfinding graph
            var obstacles = _scene.FindEntitiesWithTag(GameConfig.TAG_OBSTACLE);
            var addedWalls = 0;

            for (int i = 0; i < obstacles.Count; i++)
            {
                var obstacle = obstacles[i];
                var worldPos = obstacle.Transform.Position;
                var tileX = (int)(worldPos.X / GameConfig.TileSize);
                var tileY = (int)(worldPos.Y / GameConfig.TileSize);
                var tilePos = new Point(tileX, tileY);

                heroComponent.AddWall(tilePos);
                addedWalls++;
            }

            Debug.Log($"[HeroPromotionService] Added {addedWalls} obstacle walls to promoted hero's pathfinding");

            // Re-enable TileByTileMover now that promotion is complete
            var tileMover = hero.GetComponent<TileByTileMover>();
            if (tileMover != null)
            {
                tileMover.SetEnabled(true);
                Debug.Log("[HeroPromotionService] Re-enabled TileByTileMover after promotion complete");
            }
        }

        /// <summary>
        /// Reconnects the UI components to the newly promoted hero
        /// </summary>
        private void ReconnectUIToHero(Entity newHeroEntity)
        {
            // Cast scene to MainGameScene to access UI reconnection method
            if (_scene is MainGameScene mainGameScene)
            {
                mainGameScene.ReconnectUIToHero();
                Debug.Log("[HeroPromotionService] Reconnected UI to new hero");
            }
            else
            {
                Debug.Warn("[HeroPromotionService] Could not reconnect UI - scene is not MainGameScene");
            }
        }

        /// <summary>
        /// Generates a random hero crystal for the new hero
        /// In the future, this will use the crystal forge queue
        /// </summary>
        private HeroCrystal GenerateRandomHeroCrystal()
        {
            // For now, generate a random crystal with a random job and level 1
            var randomJob = GetRandomJob();
            var baseStats = new StatBlock(
                strength: Nez.Random.Range(2, 6),
                agility: Nez.Random.Range(2, 6),
                vitality: Nez.Random.Range(2, 6),
                magic: Nez.Random.Range(2, 6)
            );

            var crystal = new HeroCrystal("Generated Hero", randomJob, 1, baseStats);
            Debug.Log($"[HeroPromotionService] Generated random crystal: {randomJob.Name} Level 1");

            return crystal;
        }

        /// <summary>
        /// Gets a random job for hero crystal generation
        /// </summary>
        private RolePlayingFramework.Jobs.IJob GetRandomJob()
        {
            var jobs = new RolePlayingFramework.Jobs.IJob[]
            {
                new RolePlayingFramework.Jobs.Primary.Knight(),
                new RolePlayingFramework.Jobs.Primary.Monk(),
                new RolePlayingFramework.Jobs.Primary.Thief(),
                new RolePlayingFramework.Jobs.Primary.Archer(),
                new RolePlayingFramework.Jobs.Primary.Mage(),
                new RolePlayingFramework.Jobs.Primary.Priest()
            };

            return jobs.RandomItem();
        }
    }
}
