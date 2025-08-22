using Microsoft.Xna.Framework;
using Nez;
using Nez.AI.Pathfinding;
using Nez.Tiled;
using PitHero.ECS.Components;
using PitHero.Services;
using PitHero.UI;
using PitHero.Util;

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
            GeneratePitContent();
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

        private void GeneratePitContent()
        {
            Debug.Log("[MainGameScene] Generating pit content");
            
            var pitGenerator = new PitGenerator(this);
            pitGenerator.Generate(1);
            
            Debug.Log("[MainGameScene] Pit content generation complete");
        }

        private void SpawnHero()
        {
            // Calculate random position at least 8 tiles to the right of rightmost pit edge
            var rightmostPitTile = GameConfig.PitRectX + GameConfig.PitRectWidth - 1; // 12
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
            heroRenderer.SetRenderLayer(GameConfig.RenderLayerActors);
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