using Microsoft.Xna.Framework;
using Nez;
using Nez.Tiled;
using PitHero.ECS.Components;
using PitHero.UI;

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
            SpawnHero();

            _isInitializationComplete = true;
        }

        private void LoadMap()
        {
            if (string.IsNullOrEmpty(_mapPath))
                return;

            _tmxMap = Core.Content.LoadTiledMap(_mapPath);
            var tiledEntity = CreateEntity("tilemap").SetTag(GameConfig.TAG_TILEMAP);

            var baseLayerRenderer = tiledEntity.AddComponent(new TiledMapRenderer(_tmxMap, "Collision"));
            baseLayerRenderer.SetLayerToRender("Base");
            baseLayerRenderer.RenderLayer = GameConfig.RenderLayerBase;

            var fogLayerRenderer = tiledEntity.AddComponent(new TiledMapRenderer(_tmxMap));
            fogLayerRenderer.SetLayerToRender("FogOfWar");
            fogLayerRenderer.SetRenderLayer(GameConfig.RenderLayerFogOfWar);

            tiledEntity.AddComponent<FogOfWarHelper>();

            _cameraController?.ConfigureZoomForMap(_mapPath);
        }

        private void SpawnPit()
        {
            var pitEntity = CreateEntity("pit");
            
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

            // Add controller component
            pitEntity.AddComponent(new PitControllerComponent());

            Debug.Log($"[MainGameScene] Created logical pit with trigger collider at X: {pitWorldBounds.X}, " +
                $"Y: {pitWorldBounds.Y}, Width: {pitWorldBounds.Width}, Height: {pitWorldBounds.Height}");
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

        private void SpawnHero()
        {
            var heroStart = HeroActionBase.GetMapCenterWorldPosition();
            var hero = CreateEntity("hero").SetPosition(heroStart);

            hero.AddComponent(new PrototypeSpriteRenderer(20, 20));
            var collider = hero.AddComponent(new BoxCollider(GameConfig.HeroWidth, GameConfig.HeroHeight));
            
            // Hero collides with both tilemap and pit layers
            Flags.SetFlag(ref collider.CollidesWithLayers, GameConfig.PhysicsTileMapLayer);
            Flags.SetFlag(ref collider.CollidesWithLayers, GameConfig.PhysicsPitLayer);
            Flags.SetFlagExclusive(ref collider.PhysicsLayer, GameConfig.PhysicsHeroWorldLayer);

            // Add TileByTileMover for tile-based movement with trigger detection
            hero.AddComponent(new TileByTileMover());
            Debug.Log("[MainGameScene] Added TileByTileMover to hero for tile-based movement");

            hero.AddComponent(new HeroComponent
            {
                Health = 100,
                MaxHealth = 100,
                MoveSpeed = 140f,
                IsAtCenter = true,
                IsInsidePit = false,
                IsAdjacentToPit = false,
                JustJumpedOutOfPit = false
            });
            hero.AddComponent(new Historian());
            hero.AddComponent(new HeroSensorComponent()).SetUpdateOrder(-10);
            hero.AddComponent(new HeroGoapAgent());
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