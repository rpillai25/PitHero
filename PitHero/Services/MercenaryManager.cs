using Microsoft.Xna.Framework;
using Nez;
using PitHero;
using PitHero.ECS.Components;
using RolePlayingFramework.Balance;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Jobs;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Mercenaries;
using RolePlayingFramework.Stats;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PitHero.Services
{
    /// <summary>
    /// Service that manages mercenary spawning, hiring, and tavern positions
    /// </summary>
    public class MercenaryManager
    {
        private const int MaxMercenariesInTavern = 9;
        private const int MaxHiredMercenaries = 2;

        private static readonly Point[] TavernPositions = new Point[]
        {
            new Point(97, 6), new Point(97, 4), new Point(93, 4),
            new Point(98, 7), new Point(98, 3), new Point(94, 3),
            new Point(96, 7), new Point(96, 3), new Point(92, 3)
        };

        private static readonly Point SpawnPosition = new Point(104, 11);

        // The tile mercenaries walk to before sliding south off the bottom of the map
        private static readonly Point TavernExitTile = new Point(103, 6);

        private readonly List<Entity> _mercenaryEntities;
        private readonly HashSet<Point> _occupiedTavernPositions;
        private float _timeSinceLastSpawn;
        private Scene _scene;
        private bool _hasSpawnedInitialMercenary;
        private int _nextSpawnId; // Global spawn ID counter
        private bool _isRemovingMercenary; // Flag to prevent overlapping removal/spawn
        private bool _hiringBlocked; // Flag to prevent hiring during hero death/respawn

        public MercenaryManager()
        {
            _mercenaryEntities = new List<Entity>();
            _occupiedTavernPositions = new HashSet<Point>();
            _timeSinceLastSpawn = 0f;
            _hasSpawnedInitialMercenary = false;
            _nextSpawnId = 1; // Start spawn IDs at 1
            _isRemovingMercenary = false;
            _hiringBlocked = false;
        }

        /// <summary>Initialize the manager with the scene reference</summary>
        public void Initialize(Scene scene)
        {
            _scene = scene;
            Debug.Log("[MercenaryManager] Initialized");
            
            // Spawn first mercenary immediately at game start
            TrySpawnMercenary();
            _hasSpawnedInitialMercenary = true;
        }

        /// <summary>Update the manager (called from scene update)</summary>
        public void Update()
        {
            if (_scene == null) return;

            // Check if game is paused
            var pauseService = Core.Services.GetService<PauseService>();
            if (pauseService?.IsPaused == true)
                return;

            // Use scaled time for spawn timer
            _timeSinceLastSpawn += Time.DeltaTime;

            if (_timeSinceLastSpawn >= GetSpawnInterval())
            {
                // Only reset the timer on a successful spawn — when the tavern is full the
                // timer holds at the threshold so a patron walks in as soon as a seat frees.
                if (TrySpawnMercenary())
                    _timeSinceLastSpawn = 0f;
            }
        }

        /// <summary>
        /// Spawn cadence: an empty tavern gets its first patron quickly; after that a new
        /// patron arrives on a flat interval whenever a seat is free.
        /// </summary>
        private float GetSpawnInterval()
        {
            return GetUnhiredMercenaries().Count == 0
                ? GameConfig.MercenaryMinSpawnIntervalSeconds
                : GameConfig.MercenarySpawnIntervalSeconds;
        }

        /// <summary>
        /// Attempts to spawn a new mercenary. Returns false (leaving the spawn timer pending)
        /// when the tavern has no free seat — patrons are never evicted to make room; seats
        /// free up when patrons finish dining, run out of patience, or get hired.
        /// </summary>
        private bool TrySpawnMercenary()
        {
            // Don't spawn if already in the process of removing a mercenary
            if (_isRemovingMercenary)
                return false;

            // Only spawn when there's still room at a table
            if (GetUnhiredMercenaries().Count >= MaxMercenariesInTavern)
                return false;

            // Find available tavern position
            var availablePosition = GetAvailableTavernPosition();
            if (!availablePosition.HasValue)
                return false;

            // Spawn new mercenary
            SpawnMercenary(availablePosition.Value);
            Debug.Log($"[MercenaryManager] Patron spawned. Next arrives in {GetSpawnInterval():F0}s when a seat is free");
            return true;
        }

        /// <summary>Spawns a mercenary at the spawn position and moves them to tavern</summary>
        private void SpawnMercenary(Point tavernPosition)
        {
            if (_scene == null) return;

            // Get hero level for mercenary level distribution
            var heroEntity = _scene.FindEntity("hero");
            var heroComponent = heroEntity?.GetComponent<HeroComponent>();
            var heroLevel = heroComponent?.LinkedHero?.Level ?? 1;

            // Tier ≥ 2: tavern mercenaries are floored at the tier base level so they stay
            // relevant for the hero's current progression loop.
            var pitWidthManagerForMerc = Core.Services.GetService<PitWidthManager>();
            int mercMinLevel = pitWidthManagerForMerc != null && pitWidthManagerForMerc.CurrentPitTier >= 2
                ? pitWidthManagerForMerc.TierBaseLevel
                : 1;

            // Determine mercenary level from distribution
            var mercLevel = DetermineMercenaryLevel(heroLevel, mercMinLevel);

            // Generate random job
            var job = GetRandomJob();

            // Generate random base stats (simplified for now - just use defaults)
            var baseStats = new StatBlock(strength: 4, agility: 3, vitality: 5, magic: 1);

            // Generate random name
            var name = GenerateRandomName();

            // Create mercenary
            var mercenary = new Mercenary(name, job, mercLevel, baseStats);
            mercenary.LearnAllJobSkills();

            Analytics.AnalyticsService.LogMercArrived(mercenary, BalanceConfig.CalculateMercenaryHireCost(mercLevel));

            // Create entity at spawn position
            var spawnWorldPos = new Vector2(
                SpawnPosition.X * GameConfig.TileSize + GameConfig.TileSize / 2,
                SpawnPosition.Y * GameConfig.TileSize + GameConfig.TileSize / 2
            );

            var mercEntity = _scene.CreateEntity($"mercenary_{name}");
            mercEntity.SetPosition(spawnWorldPos);
            mercEntity.SetTag(GameConfig.TAG_MERCENARY);

            // Add facing component
            mercEntity.AddComponent(new ActorFacingComponent());

            // Add animation components (similar to hero)
            var offset = new Vector2(0, -GameConfig.TileSize / 2);

            var bodyColor = GameConfig.SkinColors.RandomItem();
            var shirtColor = GameConfig.ShirtColors.RandomItem();
            var hairColor = GameConfig.HairColors.RandomItem();
            var hairstyleService = Core.Services.GetService<HairstyleQueueService>();
            var hairstyleIndex = hairstyleService.GetNextHairstyle();

            var bodyAnimator = mercEntity.AddComponent(new HeroBodyAnimationComponent(bodyColor));
            bodyAnimator.SetLocalOffset(offset);

            var hand2Animator = mercEntity.AddComponent(new HeroHand2AnimationComponent(bodyColor));
            hand2Animator.SetLocalOffset(offset);

            var pantsAnimator = mercEntity.AddComponent(new HeroPantsAnimationComponent(Color.White));
            pantsAnimator.SetLocalOffset(offset);

            var shirtAnimator = mercEntity.AddComponent(new HeroShirtAnimationComponent(shirtColor));
            shirtAnimator.SetLocalOffset(offset);

            var headAnimator = mercEntity.AddComponent(new HeroHeadAnimationComponent(bodyColor));
            headAnimator.SetLocalOffset(offset);

            var eyesAnimator = mercEntity.AddComponent(new HeroEyesAnimationComponent(Color.White));
            eyesAnimator.SetLocalOffset(offset);

            var hairAnimator = mercEntity.AddComponent(new HeroHairAnimationComponent(hairColor, hairstyleIndex));
            hairAnimator.SetLocalOffset(offset);

            var hand1Animator = mercEntity.AddComponent(new HeroHand1AnimationComponent(bodyColor));
            hand1Animator.SetLocalOffset(offset);

            // Composite all paperdoll layers into a single render target to prevent z-order artifacts
            var mercMultiAnimator = mercEntity.AddComponent(new MultiSpriteAnimator(
                hand2Animator, bodyAnimator, pantsAnimator, shirtAnimator,
                headAnimator, eyesAnimator, hairAnimator, hand1Animator));
            mercMultiAnimator.SetRenderLayer(GameConfig.RenderLayerActors);

            // Add collider (non-blocking - mercenaries should not hinder hero movement)
            var collider = mercEntity.AddComponent(new BoxCollider(GameConfig.HeroWidth, GameConfig.HeroHeight));
            collider.IsTrigger = true; // Make it a trigger so it doesn't block movement
            Flags.SetFlag(ref collider.CollidesWithLayers, GameConfig.PhysicsTileMapLayer);
            Flags.SetFlagExclusive(ref collider.PhysicsLayer, GameConfig.PhysicsMercenaryLayer);

            // Add tile-by-tile mover
            var tileMover = mercEntity.AddComponent(new TileByTileMover());
            tileMover.MovementSpeed = GameConfig.HeroMovementSpeed;

            // Add pathfinding component so mercenary can navigate around obstacles
            mercEntity.AddComponent(new PathfindingActorComponent());

            // Add mercenary component
            var mercComponent = mercEntity.AddComponent(new MercenaryComponent
            {
                LinkedMercenary = mercenary,
                IsHired = false,
                IsWaitingInTavern = false,
                TavernPosition = tavernPosition,
                SpawnTime = Time.TotalTime,
                SpawnId = _nextSpawnId, // Assign unique spawn ID
                LastTilePosition = SpawnPosition,
                SkinColor = bodyColor,
                HairColor = hairColor,
                HairstyleIndex = hairstyleIndex,
                ShirtColor = shirtColor
            });

            // Increment spawn ID counter for next mercenary
            _nextSpawnId++;

            _mercenaryEntities.Add(mercEntity);
            _occupiedTavernPositions.Add(tavernPosition);

            Debug.Log($"[MercenaryManager] Spawned mercenary {name} (Level {mercLevel} {job.Name}, SpawnId {mercComponent.SpawnId}) - moving to tavern position ({tavernPosition.X},{tavernPosition.Y})");

            // Start walking to tavern position
            Core.StartCoroutine(WalkToTavern(mercEntity, tavernPosition));
        }

        /// <summary>Coroutine to walk mercenary to tavern position</summary>
        private System.Collections.IEnumerator WalkToTavern(Entity mercEntity, Point tavernPosition)
        {
            var tileMover = mercEntity.GetComponent<TileByTileMover>();
            var mercComponent = mercEntity.GetComponent<MercenaryComponent>();
            var pathfinding = mercEntity.GetComponent<PathfindingActorComponent>();
            
            if (tileMover == null || mercComponent == null || pathfinding == null)
                yield break;

            // Wait for pathfinding to initialize (or until dismissed)
            while (!pathfinding.IsPathfindingInitialized)
            {
                if (mercComponent.IsBeingRemoved)
                    yield break;
                yield return null;
            }

            // Stop immediately if dismissed before we started walking
            if (mercComponent.IsBeingRemoved)
                yield break;

            // Calculate current tile position
            var currentPos = mercEntity.Transform.Position;
            var currentTile = new Point(
                (int)(currentPos.X / GameConfig.TileSize),
                (int)(currentPos.Y / GameConfig.TileSize)
            );

            // Calculate A* path to tavern
            var path = pathfinding.CalculatePath(currentTile, tavernPosition);
            
            if (path == null || path.Count == 0)
            {
                Debug.Warn($"[MercenaryManager] Could not find path to tavern for {mercComponent.LinkedMercenary.Name}");
                yield break;
            }

            Debug.Log($"[MercenaryManager] Mercenary {mercComponent.LinkedMercenary.Name} found path with {path.Count} steps");

            // Follow the path
            for (int i = 0; i < path.Count; i++)
            {
                // Check if mercenary was hired or dismissed during the walk
                if (mercComponent.IsHired || mercComponent.IsBeingRemoved)
                {
                    Debug.Log($"[MercenaryManager] Mercenary {mercComponent.LinkedMercenary.Name} was hired/dismissed during walk to tavern - stopping tavern walk");
                    yield break;
                }

                var targetTile = path[i];
                var currentTilePos = new Point(
                    (int)(mercEntity.Transform.Position.X / GameConfig.TileSize),
                    (int)(mercEntity.Transform.Position.Y / GameConfig.TileSize)
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
                        // Also check during movement if mercenary was hired or dismissed
                        if (mercComponent.IsHired || mercComponent.IsBeingRemoved)
                        {
                            Debug.Log($"[MercenaryManager] Mercenary {mercComponent.LinkedMercenary.Name} was hired/dismissed during movement - stopping tavern walk");
                            yield break;
                        }
                        yield return null;
                    }
                }

                // Small delay between moves
                yield return Coroutine.WaitForSeconds(0.05f);
            }

            // Arrived at tavern position
            mercComponent.IsWaitingInTavern = true;

            // Add patron component so the kitchen system can serve this merc as a dining customer.
            // Patience starts immediately since the merc is now seated.
            if (!mercEntity.HasComponent<ECS.Components.TavernPatronComponent>())
            {
                var patronComp = mercEntity.AddComponent(new ECS.Components.TavernPatronComponent());
                patronComp.SeatTile = tavernPosition;
            }

            Debug.Log($"[MercenaryManager] Mercenary {mercComponent.LinkedMercenary.Name} arrived at tavern");
        }

        /// <summary>Coroutine to walk mercenary offscreen (pathfind to exit point, then slide 64 pixels down) then remove them</summary>
        private System.Collections.IEnumerator WalkOffscreenAndRemove(Entity mercEntity)
        {
            var tileMover = mercEntity.GetComponent<TileByTileMover>();
            var mercComponent = mercEntity.GetComponent<MercenaryComponent>();
            var pathfinding = mercEntity.GetComponent<PathfindingActorComponent>();
            var facingComponent = mercEntity.GetComponent<ActorFacingComponent>();
            
            if (tileMover == null || mercComponent == null || pathfinding == null)
            {
                _isRemovingMercenary = false;
                yield break;
            }

            // Mark as being removed so it can't be hired during removal animation
            mercComponent.IsBeingRemoved = true;

            // Cancel any outstanding kitchen ticket for this patron
            Core.Services.GetService<KitchenTaskCoordinator>()?.CancelTicketForPatron(mercEntity);

            // Wait for pathfinding to initialize before calculating the exit path
            while (!pathfinding.IsPathfindingInitialized)
            {
                yield return null;
            }

            // Calculate current tile position
            var currentPos = mercEntity.Transform.Position;
            var currentTile = new Point(
                (int)(currentPos.X / GameConfig.TileSize),
                (int)(currentPos.Y / GameConfig.TileSize)
            );

            // Target is the southern exit of the tavern area
            var exitTile = TavernExitTile;

            Debug.Log($"[MercenaryManager] Mercenary {mercComponent.LinkedMercenary.Name} leaving tavern - walking to exit point ({exitTile.X},{exitTile.Y})");

            // Calculate A* path to exit point
            var path = pathfinding.CalculatePath(currentTile, exitTile);
            
            if (path == null || path.Count == 0)
            {
                Debug.Warn($"[MercenaryManager] Could not find path to exit for {mercComponent.LinkedMercenary.Name} - sliding offscreen from current position");
                // Fall through to slide-down animation so the mercenary still walks off visually
            }
            else
            {
            // Follow the path to walk to exit point
            for (int i = 0; i < path.Count; i++)
            {
                var targetTile = path[i];
                var currentTilePos = new Point(
                    (int)(mercEntity.Transform.Position.X / GameConfig.TileSize),
                    (int)(mercEntity.Transform.Position.Y / GameConfig.TileSize)
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

            Debug.Log($"[MercenaryManager] Mercenary {mercComponent.LinkedMercenary.Name} reached exit point - now sliding down offscreen (64 pixels)");

            // Now at exit point - set facing to down and switch animations
            if (facingComponent != null)
            {
                facingComponent.SetFacing(Direction.Down);
            }

            // Get all animation components and switch to walk down
            var bodyAnim = mercEntity.GetComponent<HeroBodyAnimationComponent>();
            var hand1Anim = mercEntity.GetComponent<HeroHand1AnimationComponent>();
            var hand2Anim = mercEntity.GetComponent<HeroHand2AnimationComponent>();
            var pantsAnim = mercEntity.GetComponent<HeroPantsAnimationComponent>();
            var shirtAnim = mercEntity.GetComponent<HeroShirtAnimationComponent>();
            var hairAnim = mercEntity.GetComponent<HeroHairAnimationComponent>();

            // Switch all animations to walk down
            if (bodyAnim != null) bodyAnim.UpdateAnimationForDirection(Direction.Down);
            if (hand1Anim != null) hand1Anim.UpdateAnimationForDirection(Direction.Down);
            if (hand2Anim != null) hand2Anim.UpdateAnimationForDirection(Direction.Down);
            if (pantsAnim != null) pantsAnim.UpdateAnimationForDirection(Direction.Down);
            if (shirtAnim != null) shirtAnim.UpdateAnimationForDirection(Direction.Down);
            if (hairAnim != null) hairAnim.UpdateAnimationForDirection(Direction.Down);

            // Smoothly slide down 64 pixels to go offscreen
            var startPosition = mercEntity.Transform.Position;
            var offscreenDistance = 64f; // 64 pixels down (2 tiles)
            var targetPosition = startPosition + new Vector2(0, offscreenDistance);
            var moveSpeed = GameConfig.HeroMovementSpeed; // Use same speed as normal movement
            var duration = offscreenDistance / moveSpeed;

            Debug.Log($"[MercenaryManager] Sliding {mercComponent.LinkedMercenary.Name} from ({startPosition.X},{startPosition.Y}) down {offscreenDistance} pixels over {duration:F2} seconds");

            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.DeltaTime;
                var progress = elapsed / duration;
                mercEntity.Transform.Position = Vector2.Lerp(startPosition, targetPosition, progress);
                yield return null;
            }

            // Ensure we're exactly at target
            mercEntity.Transform.Position = targetPosition;

            // Reached offscreen position - now remove the mercenary
            Debug.Log($"[MercenaryManager] Mercenary {mercComponent.LinkedMercenary.Name} has left offscreen - removing");
            RemoveMercenary(mercEntity);

            // Wait 2 seconds before spawning new mercenary to avoid immediate swap appearance
            Debug.Log("[MercenaryManager] Waiting 2 seconds before spawning replacement mercenary");
            yield return Coroutine.WaitForSeconds(2.0f);

            _isRemovingMercenary = false;

            // Now that removal is complete and delay has passed, spawn the new mercenary
            TrySpawnMercenary();
        }

        /// <summary>Removes a mercenary from the game</summary>
        private void RemoveMercenary(Entity mercEntity, string reason = "tavern_left")
        {
            var mercComponent = mercEntity.GetComponent<MercenaryComponent>();
            if (mercComponent != null)
            {
                _occupiedTavernPositions.Remove(mercComponent.TavernPosition);
                Debug.Log($"[MercenaryManager] Removed mercenary {mercComponent.LinkedMercenary.Name}");

                Analytics.AnalyticsService.LogMercLeft(mercComponent.LinkedMercenary?.Name, reason);
            }

            _mercenaryEntities.Remove(mercEntity);
            mercEntity.Destroy();

            // Clear any shortcut bar slots that referenced this mercenary's skills
            Core.Services.GetService<ShortcutBarService>()?.ShortcutBar?.RefreshItems();
        }

        /// <summary>
        /// Dismisses a hired party mercenary. Unequips all gear and returns it to the hero
        /// inventory; any overflow goes to the SecondChanceMerchantVault. The mercenary
        /// entity is immediately destroyed.
        /// </summary>
        public void DismissPartyMercenary(Entity mercEntity)
        {
            var mercComponent = mercEntity?.GetComponent<MercenaryComponent>();
            if (mercComponent == null || !mercComponent.IsHired)
            {
                Debug.Warn("[MercenaryManager] DismissPartyMercenary - entity is null or not hired");
                return;
            }

            var merc = mercComponent.LinkedMercenary;
            Debug.Log($"[MercenaryManager] Dismissing party mercenary {merc.Name}");

            // Return all equipped gear to hero inventory; overflow to vault
            var heroEntity = _scene?.FindEntity("hero");
            var heroComponent = heroEntity?.GetComponent<HeroComponent>();
            var vault = Core.Services.GetService<SecondChanceMerchantVault>();

            var slots = new EquipmentSlot[]
            {
                EquipmentSlot.WeaponShield1,
                EquipmentSlot.Armor,
                EquipmentSlot.Hat,
                EquipmentSlot.WeaponShield2,
                EquipmentSlot.Accessory1,
                EquipmentSlot.Accessory2
            };

            for (int i = 0; i < slots.Length; i++)
            {
                var gear = merc.Unequip(slots[i]);
                if (gear == null) continue;

                bool added = heroComponent != null && heroComponent.TryAddItem(gear);
                if (!added)
                {
                    vault?.AddItem(gear);
                    Debug.Log($"[MercenaryManager] Gear {gear.Name} sent to vault (inventory full)");
                }
                else
                {
                    Debug.Log($"[MercenaryManager] Gear {gear.Name} returned to hero inventory");
                }
            }

            // If this mercenary was the follow target for the second mercenary, reassign
            ReassignFollowTargetsAfterDismissal(mercEntity);

            // Remove and destroy
            RemoveMercenary(mercEntity, "dismissed");
        }

        /// <summary>
        /// Dismisses a tavern (unhired) mercenary, triggering the natural walk-off-screen
        /// animation so a new mercenary can arrive shortly after.
        /// </summary>
        public void DismissTavernMercenary(Entity mercEntity)
        {
            var mercComponent = mercEntity?.GetComponent<MercenaryComponent>();
            if (mercComponent == null || mercComponent.IsHired || mercComponent.IsBeingRemoved)
            {
                Debug.Warn("[MercenaryManager] DismissTavernMercenary - invalid state");
                return;
            }

            Debug.Log($"[MercenaryManager] Dismissing tavern mercenary {mercComponent.LinkedMercenary?.Name}");
            // Mark as being removed immediately so the mercenary is non-clickable from this frame onward
            mercComponent.IsBeingRemoved = true;
            _isRemovingMercenary = true;
            Core.StartCoroutine(WalkOffscreenAndRemove(mercEntity));
        }

        /// <summary>
        /// Called by TavernPatronComponent when an unhired merc finishes eating or patience expires.
        /// Triggers the natural walk-off-screen animation (same as eviction). Safe to call multiple
        /// times — guards against IsBeingRemoved flag.
        /// </summary>
        public void WalkOffPatron(Entity mercEntity)
        {
            if (mercEntity == null) return;
            var mercComponent = mercEntity.GetComponent<MercenaryComponent>();
            if (mercComponent == null || mercComponent.IsHired || mercComponent.IsBeingRemoved)
                return;

            Debug.Log($"[MercenaryManager] Patron {mercComponent.LinkedMercenary?.Name} leaving after meal");
            mercComponent.IsBeingRemoved = true;
            _isRemovingMercenary = true;
            Core.StartCoroutine(WalkOffscreenAndRemove(mercEntity));
        }

        /// <summary>
        /// Reassigns follow targets for any hired mercenaries that were following the
        /// dismissed entity, pointing them to the hero instead.
        /// </summary>
        private void ReassignFollowTargetsAfterDismissal(Entity dismissedEntity)
        {
            var heroEntity = _scene?.FindEntity("hero");
            if (heroEntity == null) return;

            for (int i = 0; i < _mercenaryEntities.Count; i++)
            {
                var entity = _mercenaryEntities[i];
                if (entity == dismissedEntity) continue;

                var comp = entity.GetComponent<MercenaryComponent>();
                if (comp != null && comp.IsHired && comp.FollowTarget == dismissedEntity)
                {
                    comp.FollowTarget = heroEntity;
                    Debug.Log($"[MercenaryManager] Reassigned {comp.LinkedMercenary?.Name} follow target to hero after dismissal");
                }
            }
        }

        /// <summary>Gets all unhired mercenaries</summary>
        public List<Entity> GetUnhiredMercenaries()
        {
            return _mercenaryEntities.Where(m =>
            {
                var comp = m.GetComponent<MercenaryComponent>();
                return comp != null && !comp.IsHired;
            }).ToList();
        }

        /// <summary>Gets an available tavern position</summary>
        private Point? GetAvailableTavernPosition()
        {
            for (int i = 0; i < TavernPositions.Length; i++)
            {
                if (!_occupiedTavernPositions.Contains(TavernPositions[i]))
                    return TavernPositions[i];
            }
            return null;
        }

        /// <summary>Determines mercenary level using a weighted distribution based on hero level.</summary>
        /// <param name="heroLevel">Current hero level (distribution anchor).</param>
        /// <param name="minLevel">Minimum level floor — tier base level when tier ≥ 2, otherwise 1.</param>
        public static int DetermineMercenaryLevel(int heroLevel, int minLevel = 1)
        {
            if (heroLevel < 1) heroLevel = 1;

            var roll = Nez.Random.NextFloat();
            int level;

            if (roll < 0.20f)
            {
                // 20% chance: level 1
                level = 1;
            }
            else if (roll < 0.50f)
            {
                // 30% chance: random [1, heroLevel/3] inclusive
                var max = heroLevel / 3;
                level = max < 1 ? 1 : Nez.Random.Range(1, max + 1);
            }
            else if (roll < 0.70f)
            {
                // 20% chance: random [heroLevel/3, heroLevel/2] inclusive
                var min = heroLevel / 3;
                var max = heroLevel / 2;
                if (min < 1) min = 1;
                if (max < min) max = min;
                level = Nez.Random.Range(min, max + 1);
            }
            else if (roll < 0.90f)
            {
                // 20% chance: random [heroLevel/2, heroLevel] inclusive
                var min = heroLevel / 2;
                if (min < 1) min = 1;
                level = Nez.Random.Range(min, heroLevel + 1);
            }
            else
            {
                // 10% chance: heroLevel
                level = heroLevel;
            }

            int raw = level < 1 ? 1 : level;
            return raw < minLevel ? minLevel : raw;
        }

        /// <summary>Hires a mercenary</summary>
        public bool HireMercenary(Entity mercEntity)
        {
            // Check if already at max hired mercenaries
            var hiredCount = GetHiredMercenaries().Count;
            if (hiredCount >= MaxHiredMercenaries)
            {
                Debug.Warn("[MercenaryManager] Already at max hired mercenaries");
                return false;
            }

            var mercComponent = mercEntity.GetComponent<MercenaryComponent>();
            if (mercComponent == null || mercComponent.IsHired)
            {
                Debug.Warn($"[MercenaryManager] Cannot hire - mercComponent null or already hired");
                return false;
            }

            // Deduct gold cost
            var hireCost = BalanceConfig.CalculateMercenaryHireCost(mercComponent.LinkedMercenary.Level);
            var gameState = Core.Services.GetService<GameStateService>();
            if (gameState == null || gameState.Funds < hireCost)
            {
                Debug.Warn("[MercenaryManager] Cannot hire - not enough gold");
                return false;
            }
            gameState.Funds -= hireCost;

            // Cancel any outstanding dining ticket and remove patron status before joining the party
            Core.Services.GetService<KitchenTaskCoordinator>()?.CancelTicketForPatron(mercEntity);
            mercEntity.RemoveComponent<ECS.Components.TavernPatronComponent>();

            // Determine follow target BEFORE marking as hired
            var heroEntity = _scene?.FindEntity("hero");
            Entity followTarget = null;

            if (hiredCount == 0)
            {
                // First mercenary follows hero
                followTarget = heroEntity;
                if (followTarget == null)
                {
                    Debug.Error("[MercenaryManager] Cannot hire first mercenary - hero entity not found!");
                    return false;
                }
                Debug.Log($"[MercenaryManager] First mercenary will follow hero");
            }
            else
            {
                // Second mercenary follows first mercenary — for-loop scan (no LINQ)
                Entity firstHired = null;
                for (int i = 0; i < _mercenaryEntities.Count; i++)
                {
                    var c = _mercenaryEntities[i].GetComponent<MercenaryComponent>();
                    if (c != null && c.IsHired)
                    {
                        firstHired = _mercenaryEntities[i];
                        break;
                    }
                }
                followTarget = firstHired;
                if (followTarget == null)
                {
                    Debug.Error("[MercenaryManager] Cannot hire second mercenary - first mercenary not found!");
                    return false;
                }
                var firstMercName = followTarget.GetComponent<MercenaryComponent>()?.LinkedMercenary?.Name ?? "Unknown";
                Debug.Log($"[MercenaryManager] Second mercenary will follow {firstMercName}");
            }

            // Now mark as hired and update state
            mercComponent.IsHired = true;
            mercComponent.IsWaitingInTavern = false;
            mercComponent.FollowTarget = followTarget;

            Debug.Log($"[MercenaryManager] Hired mercenary {mercComponent.LinkedMercenary.Name}, follow target set to: {followTarget.Name}");

            Core.Services.GetService<GameEventService>()?.EmitLocalized(UITextKey.ConsoleMercenaryHired,
                (mercComponent.LinkedMercenary.Job.Name, Color.White),
                (mercComponent.LinkedMercenary.Name, GameConfig.ConsoleColorHeroName));

            // If the hero is sleeping, defer AI initialization — merc stays at tavern chair until wake-up.
            // Keep the seat reserved in _occupiedTavernPositions so no other merc sits on top of them.
            var heroComponent = heroEntity?.GetComponent<HeroComponent>();
            if (heroComponent?.IsSleeping == true)
            {
                mercComponent.IsHiredDuringSleep = true;
                Debug.Log($"[MercenaryManager] Hero is sleeping — mercenary {mercComponent.LinkedMercenary.Name} will wait in tavern until party wakes");
                return true;
            }

            // Release the tavern seat and start the AI
            _occupiedTavernPositions.Remove(mercComponent.TavernPosition);

            if (!mercEntity.HasComponent<HeroJumpComponent>())
                mercEntity.AddComponent(new HeroJumpComponent());
            mercEntity.AddComponent(new AI.MercenaryStateMachine());

            return true;
        }

        /// <summary>
        /// Releases the reserved tavern seat and starts the AI for a mercenary that was hired while the party was asleep.
        /// Called by SleepInBedAction when the party wakes up.
        /// </summary>
        public void InitializeDeferredMercenary(Entity mercEntity)
        {
            var mercComp = mercEntity.GetComponent<MercenaryComponent>();
            if (mercComp == null || !mercComp.IsHiredDuringSleep) return;

            mercComp.IsHiredDuringSleep = false;
            _occupiedTavernPositions.Remove(mercComp.TavernPosition);

            if (!mercEntity.HasComponent<HeroJumpComponent>())
                mercEntity.AddComponent(new HeroJumpComponent());
            mercEntity.AddComponent(new AI.MercenaryStateMachine());

            Debug.Log($"[MercenaryManager] Initialized deferred mercenary {mercComp.LinkedMercenary.Name} — tavern seat released");
        }

        /// <summary>Spawns a hired mercenary from saved data and positions it near the hero.</summary>
        public Entity SpawnHiredMercenaryFromSave(
            SavedMercenary saved, Entity heroEntity, int hiredIndex)
        {
            if (_scene == null) return null;

            // Reconstruct job and RPG object
            var job = JobFactory.CreateJob(saved.JobName ?? "Knight");
            var baseStats = new StatBlock(
                saved.BaseStrength, saved.BaseAgility,
                saved.BaseVitality, saved.BaseMagic);
            var mercenary = new Mercenary(saved.Name, job, saved.Level, baseStats);
            mercenary.LearnAllJobSkills();

            // Restore partial experience toward next level (set directly to avoid re-triggering level-ups)
            if (saved.Experience > 0)
                mercenary.Experience = saved.Experience;

            // Restore equipment
            if (saved.EquipmentNames != null)
            {
                for (int i = 0; i < 6 && i < saved.EquipmentNames.Length; i++)
                {
                    if (!string.IsNullOrEmpty(saved.EquipmentNames[i]))
                    {
                        if (ItemRegistry.TryCreateItem(saved.EquipmentNames[i], out var item) && item is IGear gear)
                        {
                            mercenary.Equip(gear);
                        }
                    }
                }
            }

            // Adjust HP/MP to saved values
            int hpDiff = mercenary.MaxHP - saved.CurrentHP;
            if (hpDiff > 0) mercenary.TakeDamage(hpDiff);
            // Use SetCurrentMP (not UseMP/SpendMP) so MPCostReduction is NOT applied to the delta —
            // a state-restore must land at exactly saved.CurrentMP regardless of passives.
            mercenary.SetCurrentMP(saved.CurrentMP);

            // Position near hero
            var heroPos = heroEntity.Transform.Position;
            var spawnWorldPos = new Vector2(
                heroPos.X - ((hiredIndex + 1) * GameConfig.TileSize),
                heroPos.Y);

            var mercEntity = _scene.CreateEntity($"mercenary_{saved.Name}");
            mercEntity.SetPosition(spawnWorldPos);
            mercEntity.SetTag(GameConfig.TAG_MERCENARY);

            // Facing component
            mercEntity.AddComponent(new ActorFacingComponent());

            // Animation components using saved appearance
            var offset = new Vector2(0, -GameConfig.TileSize / 2);

            var bodyAnimator = mercEntity.AddComponent(new HeroBodyAnimationComponent(saved.SkinColor));
            bodyAnimator.SetLocalOffset(offset);

            var hand2Animator = mercEntity.AddComponent(new HeroHand2AnimationComponent(saved.SkinColor));
            hand2Animator.SetLocalOffset(offset);

            var pantsAnimator = mercEntity.AddComponent(new HeroPantsAnimationComponent(Color.White));
            pantsAnimator.SetLocalOffset(offset);

            var shirtAnimator = mercEntity.AddComponent(new HeroShirtAnimationComponent(saved.ShirtColor));
            shirtAnimator.SetLocalOffset(offset);

            var headAnimator = mercEntity.AddComponent(new HeroHeadAnimationComponent(saved.SkinColor));
            headAnimator.SetLocalOffset(offset);

            var eyesAnimator = mercEntity.AddComponent(new HeroEyesAnimationComponent(Color.White));
            eyesAnimator.SetLocalOffset(offset);

            var hairAnimator = mercEntity.AddComponent(new HeroHairAnimationComponent(saved.HairColor, saved.HairstyleIndex));
            hairAnimator.SetLocalOffset(offset);

            var hand1Animator = mercEntity.AddComponent(new HeroHand1AnimationComponent(saved.SkinColor));
            hand1Animator.SetLocalOffset(offset);

            // Composite all paperdoll layers into a single render target to prevent z-order artifacts
            var mercMultiAnimator = mercEntity.AddComponent(new MultiSpriteAnimator(
                hand2Animator, bodyAnimator, pantsAnimator, shirtAnimator,
                headAnimator, eyesAnimator, hairAnimator, hand1Animator));
            mercMultiAnimator.SetRenderLayer(GameConfig.RenderLayerActors);

            // Collider
            var collider = mercEntity.AddComponent(new BoxCollider(GameConfig.HeroWidth, GameConfig.HeroHeight));
            collider.IsTrigger = true;
            Flags.SetFlag(ref collider.CollidesWithLayers, GameConfig.PhysicsTileMapLayer);
            Flags.SetFlagExclusive(ref collider.PhysicsLayer, GameConfig.PhysicsMercenaryLayer);

            // Tile mover and pathfinding
            var tileMover = mercEntity.AddComponent(new TileByTileMover());
            tileMover.MovementSpeed = GameConfig.HeroMovementSpeed;
            mercEntity.AddComponent(new PathfindingActorComponent());

            // Determine follow target
            Entity followTarget;
            if (hiredIndex == 0)
            {
                followTarget = heroEntity;
            }
            else
            {
                var existingHired = GetHiredMercenaries();
                followTarget = existingHired.Count > 0 ? existingHired[0] : heroEntity;
            }

            // Mercenary component (already hired)
            var currentTile = new Point(
                (int)(spawnWorldPos.X / GameConfig.TileSize),
                (int)(spawnWorldPos.Y / GameConfig.TileSize));

            var mercComponent = mercEntity.AddComponent(new MercenaryComponent
            {
                LinkedMercenary = mercenary,
                IsHired = true,
                IsWaitingInTavern = false,
                TavernPosition = Point.Zero,
                SpawnTime = Time.TotalTime,
                SpawnId = _nextSpawnId,
                LastTilePosition = currentTile,
                FollowTarget = followTarget,
                SkinColor = saved.SkinColor,
                HairColor = saved.HairColor,
                HairstyleIndex = saved.HairstyleIndex,
                ShirtColor = saved.ShirtColor
            });
            _nextSpawnId++;

            _mercenaryEntities.Add(mercEntity);

            // Add state machine and jump component (same as HireMercenary)
            mercEntity.AddComponent(new HeroJumpComponent());
            mercEntity.AddComponent(new AI.MercenaryStateMachine());

            Debug.Log($"[MercenaryManager] Restored hired mercenary {saved.Name} (Level {saved.Level} {job.Name})");
            return mercEntity;
        }

        /// <summary>Gets all hired mercenaries</summary>
        public List<Entity> GetHiredMercenaries()
        {
            return _mercenaryEntities.Where(m =>
            {
                var comp = m.GetComponent<MercenaryComponent>();
                return comp != null && comp.IsHired;
            }).ToList();
        }

        /// <summary>
        /// Returns true if any hired mercenary has the TrapSense passive.
        /// Non-allocating: iterates the internal list with a for loop and early-exits.
        /// Use this instead of calling GetHiredMercenaries() in hot paths (e.g. fog-clear step).
        /// </summary>
        public bool AnyHiredMercenaryHasTrapSense()
        {
            for (int i = 0; i < _mercenaryEntities.Count; i++)
            {
                var comp = _mercenaryEntities[i].GetComponent<MercenaryComponent>();
                if (comp == null || !comp.IsHired) continue;
                var merc = comp.LinkedMercenary;
                if (merc != null && merc.TrapSense)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Returns true when every hired mercenary has exited the pit (or there are none hired).
        /// </summary>
        public bool AreAllHiredMercenariesOutOfPit()
        {
            var hired = GetHiredMercenaries();
            for (int i = 0; i < hired.Count; i++)
            {
                var merc = hired[i].GetComponent<MercenaryComponent>();
                if (merc != null && merc.InsidePit)
                    return false;
            }
            return true;
        }

        /// <summary>Gets all mercenary entities (hired and unhired)</summary>
        public List<Entity> GetAllMercenaries()
        {
            return new List<Entity>(_mercenaryEntities);
        }

        /// <summary>
        /// Removes a mercenary entity from tracking without tavern bookkeeping or analytics.
        /// Used when a mercenary dies in battle (death is logged separately as char_killed).
        /// </summary>
        public void UntrackMercenary(Entity mercEntity)
        {
            _mercenaryEntities.Remove(mercEntity);

            // Clear any shortcut bar slots that referenced this mercenary's skills
            Core.Services.GetService<ShortcutBarService>()?.ShortcutBar?.RefreshItems();
        }

        /// <summary>Gets a random job for mercenary generation</summary>
        private IJob GetRandomJob()
        {
            var jobTypes = new Type[]
            {
                typeof(Knight), typeof(Monk), typeof(Thief),
                typeof(Archer), typeof(Mage), typeof(Priest)
            };

            var randomType = jobTypes[global::Nez.Random.Range(0, jobTypes.Length)];
            return (IJob)Activator.CreateInstance(randomType);
        }

        /// <summary>Generates a random name for mercenary using shared NameGenerator</summary>
        private string GenerateRandomName()
        {
            return Util.NameGenerator.GenerateRandomName();
        }

        /// <summary>Checks if player can hire more mercenaries</summary>
        public bool CanHireMore()
        {
            // Cannot hire if hiring is blocked (during hero death/respawn)
            if (_hiringBlocked)
                return false;

            return GetHiredMercenaries().Count < MaxHiredMercenaries;
        }


        /// <summary>Blocks mercenary hiring (called when hero dies)</summary>
        public void BlockHiring()
        {
            _hiringBlocked = true;
            Debug.Log("[MercenaryManager] Hiring blocked - hero is dead");
        }

        /// <summary>Unblocks mercenary hiring (called when new hero is promoted)</summary>
        public void UnblockHiring()
        {
            _hiringBlocked = false;
            Debug.Log("[MercenaryManager] Hiring unblocked - new hero ready");
        }

        /// <summary>Freezes all hired mercenaries in place (called when hero dies)</summary>
        public void FreezeAllHiredMercenaries()
        {
            var hiredMercenaries = GetHiredMercenaries();
            
            if (hiredMercenaries.Count == 0)
            {
                Debug.Log("[MercenaryManager] No hired mercenaries to freeze");
                return;
            }

            Debug.Log($"[MercenaryManager] Freezing {hiredMercenaries.Count} hired mercenaries due to hero death");

            for (int i = 0; i < hiredMercenaries.Count; i++)
            {
                var mercEntity = hiredMercenaries[i];
                var mercComponent = mercEntity.GetComponent<MercenaryComponent>();
                if (mercComponent == null)
                    continue;

                Debug.Log($"[MercenaryManager] Freezing hired mercenary {mercComponent.LinkedMercenary.Name} in place");

                // Disable state machine and follow component to freeze mercenary
                var stateMachine = mercEntity.GetComponent<AI.MercenaryStateMachine>();
                var followComponent = mercEntity.GetComponent<MercenaryFollowComponent>();
                var tileMover = mercEntity.GetComponent<TileByTileMover>();
                
                if (stateMachine != null)
                {
                    stateMachine.SetEnabled(false);
                }
                if (followComponent != null)
                {
                    followComponent.SetEnabled(false);
                }
                if (tileMover != null)
                {
                    tileMover.SetEnabled(false);
                }

                // Clear the follow target since hero is dead
                mercComponent.FollowTarget = null;
            }

            Debug.Log("[MercenaryManager] All hired mercenaries frozen");
        }

        /// <summary>Unfreezes all hired mercenaries and reassigns follow targets to new hero (called when new hero is promoted)</summary>
        public void UnfreezeAndReassignMercenaries(Entity newHeroEntity)
        {
            var hiredMercenaries = GetHiredMercenaries();
            
            if (hiredMercenaries.Count == 0)
            {
                Debug.Log("[MercenaryManager] No hired mercenaries to unfreeze");
                return;
            }

            Debug.Log($"[MercenaryManager] Unfreezing {hiredMercenaries.Count} hired mercenaries and reassigning to new hero");

            // Reassign follow targets
            for (int i = 0; i < hiredMercenaries.Count; i++)
            {
                var mercEntity = hiredMercenaries[i];
                var mercComponent = mercEntity.GetComponent<MercenaryComponent>();
                if (mercComponent == null)
                    continue;

                // Determine follow target based on position in chain
                Entity followTarget;
                if (i == 0)
                {
                    // First mercenary follows new hero
                    followTarget = newHeroEntity;
                    Debug.Log($"[MercenaryManager] First mercenary {mercComponent.LinkedMercenary.Name} will follow new hero");
                }
                else
                {
                    // Second mercenary follows first mercenary
                    followTarget = hiredMercenaries[0];
                    var firstMercName = followTarget.GetComponent<MercenaryComponent>()?.LinkedMercenary?.Name ?? "Unknown";
                    Debug.Log($"[MercenaryManager] Second mercenary {mercComponent.LinkedMercenary.Name} will follow {firstMercName}");
                }

                mercComponent.FollowTarget = followTarget;

                // Re-enable state machine and follow component
                var stateMachine = mercEntity.GetComponent<AI.MercenaryStateMachine>();
                var followComponent = mercEntity.GetComponent<MercenaryFollowComponent>();
                var tileMover = mercEntity.GetComponent<TileByTileMover>();
                
                if (stateMachine != null)
                {
                    stateMachine.SetEnabled(true);
                }
                if (followComponent != null)
                {
                    followComponent.SetEnabled(true);
                }
                if (tileMover != null)
                {
                    tileMover.SetEnabled(true);
                }

                Debug.Log($"[MercenaryManager] Unfroze and reassigned {mercComponent.LinkedMercenary.Name} to follow {followTarget.Name}");
            }

            Debug.Log("[MercenaryManager] All hired mercenaries unfrozen and reassigned");
        }

    }
}
