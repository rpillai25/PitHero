using Microsoft.Xna.Framework;
using Nez;
using Nez.AI.Pathfinding;
using Nez.Tiled;
using PitHero.ECS.Components;
using PitHero.Services;
using PitHero.UI;
using PitHero.Util;
using System.Collections.Generic;

namespace PitHero.ECS.Scenes
{
    public class MainGameScene : Scene
    {
        private SettingsUI _settingsUI;
        private string _mapPath;
        private bool _isInitializationComplete;
        private CameraControllerComponent _cameraController;
        private TmxMap _tmxMap; // Store reference to the map
        private Entity _pauseOverlayEntity; // Pause overlay entity

        public MainGameScene() : this("Content/Tilemaps/PitHero.tmx") { }
        public MainGameScene(string mapPath) { _mapPath = mapPath; }

        public override void Initialize()
        {
            base.Initialize();
            SetDesignResolution(GameConfig.VirtualWidth, GameConfig.VirtualHeight, SceneResolutionPolicy.BestFit);
            ClearColor = Color.Transparent;

            var cameraEntity = CreateEntity("camera-controller");
            cameraEntity.AddComponent(Camera);
            _cameraController = cameraEntity.AddComponent(new CameraControllerComponent());

            SetupUIOverlay();
        }

        public override void Begin()
        {
            base.Begin();
            if (_isInitializationComplete)
                return;

            LoadMap();
            SpawnPit();

            // Set starting pit level to 9 (after pit exists to avoid early collider warnings)
            var pitWidthManager = Core.Services.GetService<PitWidthManager>();
            if (pitWidthManager != null)
                pitWidthManager.SetPitLevel(40);

            SpawnHero();
            AddPitLevelTestComponent();

            _isInitializationComplete = true;
        }

        private void LoadMap()
        {
            if (string.IsNullOrEmpty(_mapPath))
                return;

            _tmxMap = Core.Content.LoadTiledMap(_mapPath);
            Core.Services.AddService(new TiledMapService(_tmxMap));
            var tiledEntity = CreateEntity("tilemap").SetTag(GameConfig.TAG_TILEMAP);

            var baseLayerRenderer = tiledEntity.AddComponent(new TiledMapRenderer(_tmxMap, "Collision"));
            baseLayerRenderer.SetLayerToRender("Base");
            baseLayerRenderer.RenderLayer = GameConfig.RenderLayerBase;

            var fogLayerRenderer = tiledEntity.AddComponent(new TiledMapRenderer(_tmxMap));
            fogLayerRenderer.SetLayerToRender("FogOfWar");
            fogLayerRenderer.SetRenderLayer(GameConfig.RenderLayerFogOfWar);

            _cameraController?.ConfigureZoomForMap(_mapPath);
            
            // Set up pathfinding after map is loaded
            SetupPathfinding();
            
            // Initialize pit width manager after map and services are set up
            SetupPitWidthManager();
        }

        private void SetupPathfinding()
        {
            if (_tmxMap == null)
            {
                Debug.Warn("[MainGameScene] Cannot setup pathfinding without tilemap");
                return;
            }

            // Get the collision layer for pathfinding
            var collisionLayer = _tmxMap.GetLayer<TmxLayer>("Collision");
            if (collisionLayer == null)
            {
                Debug.Warn("[MainGameScene] No 'Collision' layer found in tilemap for pathfinding");
                return;
            }

            // Build graph from the entire Collision layer: any present tile is a wall
            var astarGraph = new AstarGridGraph(collisionLayer);
            Core.Services.AddService(astarGraph);
            Debug.Log($"[MainGameScene] AStarGridGraph pathfinding service registered with {astarGraph.Walls.Count} walls from Collision layer");
        }

        private void SetupPitWidthManager()
        {
            var pitWidthManager = new PitWidthManager();
            pitWidthManager.Initialize();
            Core.Services.AddService(pitWidthManager);
            Debug.Log("[MainGameScene] PitWidthManager initialized and registered as service");
        }

        private void SpawnPit()
        {
            var pitEntity = CreateEntity("pit");
            pitEntity.SetTag(GameConfig.TAG_PIT); // Make sure this is set!
            
            // Calculate pit bounds in world coordinates with padding
            var pitWorldBounds = CalculatePitWorldBounds();
            
            // Position the pit entity at the center of the bounds
            pitEntity.SetPosition(pitWorldBounds.Center.ToVector2());

            // Add logical pit component
            pitEntity.AddComponent(new PitComponent
            {
                CrystalPower = 1f,
                IsActive = true,
                EffectRadius = 100f
            });

            // Add trigger collider covering the pit area
            var pitCollider = pitEntity.AddComponent(new BoxCollider(pitWorldBounds.Width, pitWorldBounds.Height));
            pitCollider.IsTrigger = true; // Make it a trigger so it doesn't block movement
            Flags.SetFlagExclusive(ref pitCollider.PhysicsLayer, GameConfig.PhysicsPitLayer);

            Debug.Log($"[MainGameScene] Created pit entity with Tag={pitEntity.Tag} at position {pitEntity.Transform.Position.X},{pitEntity.Transform.Position.Y}");
            Debug.Log($"[MainGameScene] Pit trigger collider bounds: X={pitWorldBounds.X}, Y={pitWorldBounds.Y}, Width={pitWorldBounds.Width}, Height={pitWorldBounds.Height}");
            
            // Do NOT add synthetic pit walls here. Collision layer + generated obstacles will populate walls.
        }

        private Rectangle CalculatePitWorldBounds()
        {
            // Use dynamic pit bounds from PitWidthManager if available
            var pitWidthManager = Core.Services.GetService<PitWidthManager>();
            if (pitWidthManager != null)
            {
                return pitWidthManager.CalculateCurrentPitWorldBounds();
            }

            // Fallback to default static calculation
            var topLeftWorld = new Vector2(
                GameConfig.PitRectX * GameConfig.TileSize - GameConfig.PitColliderPadding,
                GameConfig.PitRectY * GameConfig.TileSize - GameConfig.PitColliderPadding
            );

            var bottomRightWorld = new Vector2(
                (GameConfig.PitRectX + GameConfig.PitRectWidth) * GameConfig.TileSize + GameConfig.PitColliderPadding,
                (GameConfig.PitRectY + GameConfig.PitRectHeight) * GameConfig.TileSize + GameConfig.PitColliderPadding
            );

            return new Rectangle(
                (int)topLeftWorld.X,
                (int)topLeftWorld.Y,
                (int)(bottomRightWorld.X - topLeftWorld.X),
                (int)(bottomRightWorld.Y - topLeftWorld.Y)
            );
        }

        private void SpawnHero()
        {
            // Calculate random position at least 8 tiles to the right of rightmost pit edge
            var pitWidthManager = Core.Services.GetService<PitWidthManager>();
            var rightmostPitTile = pitWidthManager?.CurrentPitRightEdge ?? (GameConfig.PitRectX + GameConfig.PitRectWidth - 1);

            var minHeroTileX = rightmostPitTile + 8; // 20
            var maxHeroTileX = 50; // Leave some space from map edge
            
            var heroTileX = Random.Range(minHeroTileX, maxHeroTileX + 1);
            var heroTileY = Random.Range(1, 8);
            
            var heroStart = new Vector2(
                heroTileX * GameConfig.TileSize + GameConfig.TileSize / 2,
                heroTileY * GameConfig.TileSize + GameConfig.TileSize / 2
            );
            
            var hero = CreateEntity("hero").SetPosition(heroStart);

            Debug.Log($"[MainGameScene] Hero spawned at random position {heroStart.X},{heroStart.Y} , tile coordinates: " +
                      $"({heroTileX}, {heroTileY}) - {minHeroTileX - rightmostPitTile} tiles from pit edge");

            var heroRenderer = hero.AddComponent(new PrototypeSpriteRenderer(GameConfig.TileSize, GameConfig.TileSize));
            heroRenderer.SetRenderLayer(GameConfig.RenderLayerHero);
            var collider = hero.AddComponent(new BoxCollider(GameConfig.HeroWidth, GameConfig.HeroHeight));
            
            Flags.SetFlag(ref collider.CollidesWithLayers, GameConfig.PhysicsTileMapLayer);
            Flags.SetFlag(ref collider.CollidesWithLayers, GameConfig.PhysicsPitLayer);
            Flags.SetFlagExclusive(ref collider.PhysicsLayer, GameConfig.PhysicsHeroWorldLayer);

            hero.AddComponent(new TileByTileMover());
            var tileMover = hero.GetComponent<TileByTileMover>();
            tileMover.MovementSpeed = GameConfig.HeroMovementSpeed;
            Debug.Log("[MainGameScene] Added TileByTileMover to hero for tile-based movement");

            hero.AddComponent(new HeroComponent
            {
                Health = 100,
                MaxHealth = 100,
                MoveSpeed = 140f,
                PitInitialized = true
            });
            hero.AddComponent(new Historian());
            hero.AddComponent(new HeroGoapAgentComponent());
        }

        private void SetupUIOverlay()
        {
            var screenSpaceRenderer = new ScreenSpaceRenderer(100, [GameConfig.TransparentPauseOverlay, GameConfig.RenderLayerUI]);
            AddRenderer(screenSpaceRenderer);

            // Create pause overlay entity
            _pauseOverlayEntity = CreateEntity("pause-overlay");
            _pauseOverlayEntity.SetPosition(0, 0); // Top-left corner

            // Size to backbuffer for ScreenSpaceRenderer and set origin to top-left
            var pauseOverlay = _pauseOverlayEntity.AddComponent(
                new PrototypeSpriteRenderer(Screen.Width * 2, Screen.Height * 2)
            );
            pauseOverlay.SetOrigin(Vector2.Zero); // or pauseOverlay.SetOriginNormalized(Vector2.Zero);
            pauseOverlay.SetColor(new Color(0, 0, 0, 100));
            pauseOverlay.SetRenderLayer(GameConfig.TransparentPauseOverlay);
            _pauseOverlayEntity.SetEnabled(false); // Initially hidden

            var uiEntity = CreateEntity("ui-overlay");
            var uiCanvas = uiEntity.AddComponent(new UICanvas());
            uiCanvas.IsFullScreen = true;
            uiCanvas.RenderLayer = GameConfig.RenderLayerUI;

            _settingsUI = new SettingsUI(Core.Instance);
            _settingsUI.InitializeUI(uiCanvas.Stage);
        }

        private void AddPitLevelTestComponent()
        {
#if DEBUG
            var testEntity = CreateEntity("pit-level-test");
            testEntity.AddComponent(new PitLevelTestComponent());
            Debug.Log("[MainGameScene] Added PitLevelTestComponent - Press number keys 0-9 to test pit level changes");
#endif
        }

        /// <summary>
        /// Update the pit collider bounds to match the current dynamic pit width
        /// </summary>
        public void UpdatePitColliderBounds()
        {
            var pitEntity = FindEntity("pit");
            if (pitEntity == null)
            {
                Debug.Error("[MainGameScene] Could not find pit entity to update collider bounds");
                return;
            }

            var pitCollider = pitEntity.GetComponent<BoxCollider>();
            if (pitCollider == null)
            {
                Debug.Error("[MainGameScene] Pit entity missing BoxCollider component");
                return;
            }

            // Calculate new pit bounds
            var newPitBounds = CalculatePitWorldBounds();

            // Update collider size
            pitCollider.SetWidth(newPitBounds.Width);
            pitCollider.SetHeight(newPitBounds.Height);

            // Update pit entity position to center of new bounds
            pitEntity.SetPosition(newPitBounds.Center.ToVector2());

            Debug.Log($"[MainGameScene] Updated pit collider bounds: X={newPitBounds.X}, Y={newPitBounds.Y}, Width={newPitBounds.Width}, Height={newPitBounds.Height}");
        }

        /// <summary>
        /// Reload the map from disk and regenerate pit content with a completely fresh slate
        /// This approach ensures identical behavior to initial generation
        /// </summary>
        public void ReloadMapAndRegeneratePit(int newPitLevel)
        {
            Debug.Log($"[MainGameScene] Starting fresh map reload and pit regeneration to level {newPitLevel}");
            
            // 1. Preserve hero state before reload
            var heroState = PreserveHeroStateForRegeneration();
            
            // 2. Clear all pit entities from cache (obstacles, chests, monsters)
            ClearAllPitEntities();
            
            // 3. Reload map from disk as if this was the first load
            ReloadMapFromDisk();
            
            // 4. Clear GOAP state but preserve EnteredPit status 
            ClearHeroGoapState(heroState);
            
            // 5. Regenerate pit on fresh map as if this was the first time
            RegeneratePitOnFreshMap(newPitLevel);
            
            // 6. Perform post-regeneration cleanup (FogOfWar clearing for heroes inside pit)
            PostRegenerationCleanup(heroState);
            
            Debug.Log($"[MainGameScene] Fresh map reload and pit regeneration to level {newPitLevel} complete");
        }

        /// <summary>
        /// Preserve hero state information before map reload
        /// </summary>
        private HeroRegenerationState PreserveHeroStateForRegeneration()
        {
            var hero = FindEntity("hero");
            if (hero == null)
            {
                Debug.Warn("[MainGameScene] No hero found for state preservation");
                return new HeroRegenerationState { IsHeroInPit = false };
            }

            var heroComponent = hero.GetComponent<HeroComponent>();
            if (heroComponent == null)
            {
                Debug.Warn("[MainGameScene] Hero missing HeroComponent for state preservation");
                return new HeroRegenerationState { IsHeroInPit = false };
            }

            // Check if hero is currently in the pit by checking EnteredPit status
            bool isInPit = heroComponent.EnteredPit;
            
            Debug.Log($"[MainGameScene] Preserved hero state: IsInPit={isInPit}");
            return new HeroRegenerationState { IsHeroInPit = isInPit };
        }

        /// <summary>
        /// Clear all pit entities (obstacles, chests, monsters, wizard orbs) from cache
        /// </summary>
        private void ClearAllPitEntities()
        {
            Debug.Log("[MainGameScene] Clearing all pit entities from cache");
            
            var entitiesToRemove = new List<Entity>();
            
            // Find all entities with pit-related tags
            var obstacles = FindEntitiesWithTag(GameConfig.TAG_OBSTACLE);
            var treasures = FindEntitiesWithTag(GameConfig.TAG_TREASURE);
            var monsters = FindEntitiesWithTag(GameConfig.TAG_MONSTER);
            var wizardOrbs = FindEntitiesWithTag(GameConfig.TAG_WIZARD_ORB);
            
            // Add all found entities to removal list
            entitiesToRemove.AddRange(obstacles);
            entitiesToRemove.AddRange(treasures);
            entitiesToRemove.AddRange(monsters);
            entitiesToRemove.AddRange(wizardOrbs);
            
            // Remove entities
            for (int i = 0; i < entitiesToRemove.Count; i++)
            {
                entitiesToRemove[i].Destroy();
            }
            
            Debug.Log($"[MainGameScene] Cleared {entitiesToRemove.Count} pit entities from cache");
        }

        /// <summary>
        /// Reload the map from disk, completely resetting all tilemap layers
        /// </summary>
        private void ReloadMapFromDisk()
        {
            Debug.Log("[MainGameScene] Reloading map from disk for fresh state");
            
            // Remove existing tilemap entities
            var existingTilemaps = FindEntitiesWithTag(GameConfig.TAG_TILEMAP);
            for (int i = 0; i < existingTilemaps.Count; i++)
            {
                existingTilemaps[i].Destroy();
            }
            
            // Load fresh map from disk
            _tmxMap = Core.Content.LoadTiledMap(_mapPath);
            
            // Update TiledMapService with fresh map
            Core.Services.RemoveService(typeof(TiledMapService));
            Core.Services.AddService(new TiledMapService(_tmxMap));
            
            // Recreate tilemap entity with fresh map
            var tiledEntity = CreateEntity("tilemap").SetTag(GameConfig.TAG_TILEMAP);

            var baseLayerRenderer = tiledEntity.AddComponent(new TiledMapRenderer(_tmxMap, "Collision"));
            baseLayerRenderer.SetLayerToRender("Base");
            baseLayerRenderer.RenderLayer = GameConfig.RenderLayerBase;

            var fogLayerRenderer = tiledEntity.AddComponent(new TiledMapRenderer(_tmxMap));
            fogLayerRenderer.SetLayerToRender("FogOfWar");
            fogLayerRenderer.SetRenderLayer(GameConfig.RenderLayerFogOfWar);
            
            // Rebuild pathfinding with fresh map
            SetupPathfinding();
            
            Debug.Log("[MainGameScene] Map reloaded from disk with fresh tilemap layers");
        }

        /// <summary>
        /// Clear hero GOAP state but preserve EnteredPit status based on hero position
        /// </summary>
        private void ClearHeroGoapState(HeroRegenerationState heroState)
        {
            Debug.Log("[MainGameScene] Clearing hero GOAP state while preserving pit status");
            
            var hero = FindEntity("hero");
            if (hero == null)
            {
                Debug.Warn("[MainGameScene] No hero found for GOAP state clearing");
                return;
            }

            // Reset GOAP agent
            var agentComponent = hero.GetComponent<HeroGoapAgentComponent>();
            if (agentComponent != null)
            {
                agentComponent.ResetActionPlan();
            }

            // Reset hero component state but preserve EnteredPit if hero was in pit
            var heroComponent = hero.GetComponent<HeroComponent>();
            if (heroComponent != null)
            {
                // Reset adjacency states
                heroComponent.AdjacentToPitBoundaryFromOutside = false;
                heroComponent.AdjacentToPitBoundaryFromInside = false;
                
                // Preserve EnteredPit status based on hero's position before regeneration
                heroComponent.EnteredPit = heroState.IsHeroInPit;
                
                Debug.Log($"[MainGameScene] Hero GOAP state cleared. EnteredPit preserved as: {heroComponent.EnteredPit}");
            }
        }

        /// <summary>
        /// Clear FogOfWar around the hero's current position when regenerating while inside pit
        /// </summary>
        private void ClearFogOfWarAroundHero(Entity hero)
        {
            Debug.Log("[MainGameScene] Clearing FogOfWar around hero's current position after regeneration");
            
            var tileByTileMover = hero.GetComponent<TileByTileMover>();
            if (tileByTileMover == null)
            {
                Debug.Warn("[MainGameScene] Hero missing TileByTileMover for FogOfWar clearing");
                return;
            }
            
            var tiledMapService = Core.Services.GetService<TiledMapService>();
            if (tiledMapService == null)
            {
                Debug.Warn("[MainGameScene] TiledMapService not available for FogOfWar clearing");
                return;
            }
            
            // Get hero's current tile coordinates
            var heroTileCoords = tileByTileMover.GetCurrentTileCoordinates();
            Debug.Log($"[MainGameScene] Clearing FogOfWar around hero at tile ({heroTileCoords.X}, {heroTileCoords.Y})");
            
            // Clear FogOfWar around the hero's position
            tiledMapService.ClearFogOfWarAroundTile(heroTileCoords.X, heroTileCoords.Y);
        }

        /// <summary>
        /// Regenerate pit on the fresh map as if this was the first time
        /// </summary>
        private void RegeneratePitOnFreshMap(int newPitLevel)
        {
            Debug.Log($"[MainGameScene] Regenerating pit on fresh map for level {newPitLevel}");
            
            // Remove and recreate PitWidthManager with fresh map
            Core.Services.RemoveService(typeof(PitWidthManager));
            SetupPitWidthManager();
            
            // Set pit level (this will trigger fresh pit generation on the clean map)
            var pitWidthManager = Core.Services.GetService<PitWidthManager>();
            if (pitWidthManager != null)
            {
                pitWidthManager.SetPitLevel(newPitLevel);
            }
            else
            {
                Debug.Error("[MainGameScene] Failed to get PitWidthManager for fresh pit regeneration");
            }
        }

        /// <summary>
        /// Clear FogOfWar around hero position after pit regeneration (for heroes inside pit)
        /// </summary>
        private void PostRegenerationCleanup(HeroRegenerationState heroState)
        {
            Debug.Log("[MainGameScene] Performing post-regeneration cleanup");
            
            // If hero was in pit, clear FogOfWar around his current position after regeneration
            if (heroState.IsHeroInPit)
            {
                var hero = FindEntity("hero");
                if (hero != null)
                {
                    ClearFogOfWarAroundHero(hero);
                }
            }
        }

        /// <summary>
        /// Helper struct to preserve hero state during regeneration
        /// </summary>
        private struct HeroRegenerationState
        {
            public bool IsHeroInPit;
        }

        public override void Update()
        {
            base.Update();
            _settingsUI?.Update();

            // Update pause overlay visibility based on pause state
            var pauseService = Core.Services.GetService<PauseService>();
            if (pauseService != null && _pauseOverlayEntity != null)
            {
                _pauseOverlayEntity.SetEnabled(pauseService.IsPaused);
            }
        }
    }
}