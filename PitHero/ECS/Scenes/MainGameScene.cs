using Microsoft.Xna.Framework;
using Nez;
using Nez.AI.Pathfinding;
using Nez.Tiled;
using PitHero.AI;
using PitHero.ECS.Components;
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

            // Create AStarGridGraph using the collision layer
            var astarGraph = new AstarGridGraph(collisionLayer);
            
            // Register the pathfinding graph as a service
            Core.Services.AddService(astarGraph);
            
            Debug.Log("[MainGameScene] AStarGridGraph pathfinding service registered");
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
            Debug.Log($"[MainGameScene] Pit trigger collider bounds: X={pitWorldBounds.X}, " +
                $"Y={pitWorldBounds.Y}, Width={pitWorldBounds.Width}, Height={pitWorldBounds.Height}");
            
            // Add pit obstacles to pathfinding graph
            AddPitObstaclesToPathfinding();
        }

        private Rectangle CalculatePitWorldBounds()
        {
            // Convert tile coordinates to world coordinates
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

        private void AddPitObstaclesToPathfinding()
        {
            var astarGraph = Core.Services.GetService<AstarGridGraph>();
            if (astarGraph == null)
            {
                Debug.Warn("[MainGameScene] AStarGridGraph not found, cannot add pit obstacles");
                return;
            }

            // Add pit area tiles as obstacles for pathfinding
            // The pit area spans from (PitRectX, PitRectY) to (PitRectX + PitRectWidth - 1, PitRectY + PitRectHeight - 1)
            for (int x = GameConfig.PitRectX; x < GameConfig.PitRectX + GameConfig.PitRectWidth; x++)
            {
                for (int y = GameConfig.PitRectY; y < GameConfig.PitRectY + GameConfig.PitRectHeight; y++)
                {
                    // Add this tile as an obstacle in the pathfinding graph
                    astarGraph.Walls.Add(new Point(x, y));
                }
            }
            
            Debug.Log($"[MainGameScene] Added {GameConfig.PitRectWidth * GameConfig.PitRectHeight} pit obstacle tiles to pathfinding graph");
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
            var heroTileY = GameConfig.MapCenterTileY; // Keep same Y as pit entrance for simplicity
            
            var random = new System.Random();
            var heroTileX = random.Next(minHeroTileX, maxHeroTileX + 1);
            
            var heroStart = new Vector2(
                heroTileX * GameConfig.TileSize + GameConfig.TileSize / 2,
                heroTileY * GameConfig.TileSize + GameConfig.TileSize / 2
            );
            
            var hero = CreateEntity("hero").SetPosition(heroStart);

            Debug.Log($"[MainGameScene] Hero spawned at random position {heroStart.X},{heroStart.Y} , tile coordinates: " +
                      $"({heroTileX}, {heroTileY}) - {minHeroTileX - rightmostPitTile} tiles from pit edge");

            hero.AddComponent(new PrototypeSpriteRenderer(GameConfig.TileSize, GameConfig.TileSize));
            // Use centered collider constructor - this creates a collider centered on the entity
            var collider = hero.AddComponent(new BoxCollider(GameConfig.HeroWidth, GameConfig.HeroHeight));
            
            // Hero collides with both tilemap and pit layers
            Flags.SetFlag(ref collider.CollidesWithLayers, GameConfig.PhysicsTileMapLayer);
            Flags.SetFlag(ref collider.CollidesWithLayers, GameConfig.PhysicsPitLayer);
            Flags.SetFlagExclusive(ref collider.PhysicsLayer, GameConfig.PhysicsHeroWorldLayer);

            // Add TileByTileMover for tile-based movement with trigger detection
            hero.AddComponent(new TileByTileMover());
            var tileMover = hero.GetComponent<TileByTileMover>();
            tileMover.MovementSpeed = GameConfig.HeroMovementSpeed; // Set desired speed in tiles per second
            Debug.Log("[MainGameScene] Added TileByTileMover to hero for tile-based movement");

            hero.AddComponent(new HeroComponent
            {
                Health = 100,
                MaxHealth = 100,
                MoveSpeed = 140f,
                PitInitialized = true // Pit content has been generated
            });
            hero.AddComponent(new Historian());
            hero.AddComponent(new HeroGoapAgentComponent());
        }

        private void SetupUIOverlay()
        {
            var screenSpaceRenderer = new ScreenSpaceRenderer(100, 999);
            AddRenderer(screenSpaceRenderer);

            var uiEntity = CreateEntity("ui-overlay");
            var uiCanvas = uiEntity.AddComponent(new UICanvas());
            uiCanvas.IsFullScreen = true;
            uiCanvas.RenderLayer = 999;

            _settingsUI = new SettingsUI(Core.Instance);
            _settingsUI.InitializeUI(uiCanvas.Stage);
        }

        public override void Update()
        {
            base.Update();
            _settingsUI?.Update();
        }
    }
}