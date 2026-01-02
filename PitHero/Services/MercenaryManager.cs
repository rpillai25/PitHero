using Microsoft.Xna.Framework;
using Nez;
using PitHero.ECS.Components;
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
        private const int MaxMercenariesInTavern = 12;
        private const int MaxHiredMercenaries = 2;

        private static readonly Point[] TavernPositions = new Point[]
        {
            new Point(68, 5), new Point(69, 4), new Point(70, 5),
            new Point(72, 5), new Point(73, 4), new Point(74, 5),
            new Point(68, 7), new Point(69, 8), new Point(70, 7),
            new Point(72, 7), new Point(73, 8), new Point(74, 7)
        };

        private static readonly Point SpawnPosition = new Point(62, 11);

        private readonly List<Entity> _mercenaryEntities;
        private readonly HashSet<Point> _occupiedTavernPositions;
        private float _timeSinceLastSpawn;
        private Scene _scene;
        private bool _hasSpawnedInitialMercenary;
        private int _nextSpawnId; // Global spawn ID counter
        private bool _isRemovingMercenary; // Flag to prevent overlapping removal/spawn

        public MercenaryManager()
        {
            _mercenaryEntities = new List<Entity>();
            _occupiedTavernPositions = new HashSet<Point>();
            _timeSinceLastSpawn = 0f;
            _hasSpawnedInitialMercenary = false;
            _nextSpawnId = 1; // Start spawn IDs at 1
            _isRemovingMercenary = false;
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

            if (_timeSinceLastSpawn >= GameConfig.MercenarySpawnIntervalSeconds)
            {
                _timeSinceLastSpawn = 0f;
                TrySpawnMercenary();
            }
        }

        /// <summary>Attempts to spawn a new mercenary</summary>
        private void TrySpawnMercenary()
        {
            // Don't spawn if already in the process of removing a mercenary
            if (_isRemovingMercenary)
                return;

            // Count unhired mercenaries
            var unhiredMercenaries = GetUnhiredMercenaries();

            if (unhiredMercenaries.Count >= MaxMercenariesInTavern)
            {
                // Find the oldest unhired mercenary (lowest SpawnId)
                var oldestMercenary = unhiredMercenaries
                    .OrderBy(m => m.GetComponent<MercenaryComponent>()?.SpawnId ?? int.MaxValue)
                    .FirstOrDefault();
                    
                if (oldestMercenary != null)
                {
                    var mercName = oldestMercenary.GetComponent<MercenaryComponent>()?.LinkedMercenary?.Name ?? "Unknown";
                    Debug.Log($"[MercenaryManager] Tavern full - oldest mercenary {mercName} will leave to make room");
                    
                    // Start the walk-off-and-remove process
                    _isRemovingMercenary = true;
                    Core.StartCoroutine(WalkOffscreenAndRemove(oldestMercenary));
                    return; // Don't spawn yet - wait for removal to complete
                }
            }

            // Find available tavern position
            var availablePosition = GetAvailableTavernPosition();
            if (!availablePosition.HasValue)
            {
                Debug.Warn("[MercenaryManager] No available tavern positions");
                return;
            }

            // Spawn new mercenary
            SpawnMercenary(availablePosition.Value);
        }

        /// <summary>Spawns a mercenary at the spawn position and moves them to tavern</summary>
        private void SpawnMercenary(Point tavernPosition)
        {
            if (_scene == null) return;

            // Get hero level for mercenary level
            var heroEntity = _scene.FindEntity("hero");
            var heroComponent = heroEntity?.GetComponent<HeroComponent>();
            var heroLevel = heroComponent?.LinkedHero?.Level ?? 1;

            // Generate random job
            var job = GetRandomJob();

            // Generate random base stats (simplified for now - just use defaults)
            var baseStats = new StatBlock(strength: 4, agility: 3, vitality: 5, magic: 1);

            // Generate random name
            var name = GenerateRandomName();

            // Create mercenary
            var mercenary = new Mercenary(name, job, heroLevel, baseStats);

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
            var bodyAnimator = mercEntity.AddComponent(new HeroBodyAnimationComponent(bodyColor));
            bodyAnimator.SetRenderLayer(GameConfig.RenderLayerHeroBody);
            bodyAnimator.SetLocalOffset(offset);

            var hand2Animator = mercEntity.AddComponent(new HeroHand2AnimationComponent(bodyColor));
            hand2Animator.SetRenderLayer(GameConfig.RenderLayerHeroHand2);
            hand2Animator.SetLocalOffset(offset);

            var pantsAnimator = mercEntity.AddComponent(new HeroPantsAnimationComponent(Color.White));
            pantsAnimator.SetRenderLayer(GameConfig.RenderLayerHeroPants);
            pantsAnimator.SetLocalOffset(offset);

            var shirtAnimator = mercEntity.AddComponent(new HeroShirtAnimationComponent(GameConfig.ShirtColors.RandomItem()));
            shirtAnimator.SetRenderLayer(GameConfig.RenderLayerHeroShirt);
            shirtAnimator.SetLocalOffset(offset);

            var hairAnimator = mercEntity.AddComponent(new HeroHairAnimationComponent(GameConfig.HairColors.RandomItem()));
            hairAnimator.SetRenderLayer(GameConfig.RenderLayerHeroHair);
            hairAnimator.SetLocalOffset(offset);

            var hand1Animator = mercEntity.AddComponent(new HeroHand1AnimationComponent(bodyColor));
            hand1Animator.SetRenderLayer(GameConfig.RenderLayerHeroHand1);
            hand1Animator.SetLocalOffset(offset);

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
                LastTilePosition = SpawnPosition
            });

            // Increment spawn ID counter for next mercenary
            _nextSpawnId++;

            _mercenaryEntities.Add(mercEntity);
            _occupiedTavernPositions.Add(tavernPosition);

            Debug.Log($"[MercenaryManager] Spawned mercenary {name} (Level {heroLevel} {job.Name}, SpawnId {mercComponent.SpawnId}) - moving to tavern position ({tavernPosition.X},{tavernPosition.Y})");

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

            // Wait for pathfinding to initialize
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

            // Arrived at tavern position
            mercComponent.IsWaitingInTavern = true;
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

            // Calculate current tile position
            var currentPos = mercEntity.Transform.Position;
            var currentTile = new Point(
                (int)(currentPos.X / GameConfig.TileSize),
                (int)(currentPos.Y / GameConfig.TileSize)
            );

            // Target is 2 tiles to the left of spawn position (exit point)
            var exitTile = new Point(SpawnPosition.X - 2, SpawnPosition.Y);

            Debug.Log($"[MercenaryManager] Mercenary {mercComponent.LinkedMercenary.Name} leaving tavern - walking to exit point ({exitTile.X},{exitTile.Y})");

            // Calculate A* path to exit point
            var path = pathfinding.CalculatePath(currentTile, exitTile);
            
            if (path == null || path.Count == 0)
            {
                Debug.Warn($"[MercenaryManager] Could not find path to exit for {mercComponent.LinkedMercenary.Name} - removing anyway");
                RemoveMercenary(mercEntity);
                _isRemovingMercenary = false;
                
                // Immediately try to spawn replacement
                TrySpawnMercenary();
                yield break;
            }

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
        private void RemoveMercenary(Entity mercEntity)
        {
            var mercComponent = mercEntity.GetComponent<MercenaryComponent>();
            if (mercComponent != null)
            {
                _occupiedTavernPositions.Remove(mercComponent.TavernPosition);
                Debug.Log($"[MercenaryManager] Removed mercenary {mercComponent.LinkedMercenary.Name}");
            }

            _mercenaryEntities.Remove(mercEntity);
            mercEntity.Destroy();
        }

        /// <summary>Gets all unhired mercenaries</summary>
        private List<Entity> GetUnhiredMercenaries()
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
                // Second mercenary follows first mercenary
                // Get the list BEFORE marking this one as hired to avoid confusion
                followTarget = GetHiredMercenaries().FirstOrDefault();
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
            _occupiedTavernPositions.Remove(mercComponent.TavernPosition);
            mercComponent.FollowTarget = followTarget;

            Debug.Log($"[MercenaryManager] Hired mercenary {mercComponent.LinkedMercenary.Name}, follow target set to: {followTarget.Name}");

            // Add join component to make mercenary navigate to their target (handles pit detection and jumping)
            // This will automatically switch to follow mode once the mercenary reaches the target
            mercEntity.AddComponent(new MercenaryJoinComponent());

            return true;
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

        /// <summary>Gets all mercenary entities (hired and unhired)</summary>
        public List<Entity> GetAllMercenaries()
        {
            return new List<Entity>(_mercenaryEntities);
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

        /// <summary>Generates a random name for mercenary</summary>
        private string GenerateRandomName()
        {
            var firstNames = new[] { "Aldric", "Brynn", "Cedric", "Diana", "Elara", "Finn", "Gareth", "Helena", "Ivan", "Jade", "Kael", "Luna", "Marcus", "Nina", "Owen", "Petra", "Quinn", "Rowan", "Sasha", "Thane" };
            var lastNames = new[] { "Swift", "Strong", "Wise", "Brave", "Bold", "Quick", "Keen", "True", "Steel", "Bright" };
            
            return $"{firstNames[global::Nez.Random.Range(0, firstNames.Length)]} {lastNames[global::Nez.Random.Range(0, lastNames.Length)]}";
        }

        /// <summary>Checks if player can hire more mercenaries</summary>
        public bool CanHireMore()
        {
            return GetHiredMercenaries().Count < MaxHiredMercenaries;
        }
    }
}
