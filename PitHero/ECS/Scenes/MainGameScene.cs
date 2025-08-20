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

            var tmxMap = Core.Content.LoadTiledMap(_mapPath);
            var tiledEntity = CreateEntity("tilemap").SetTag(GameConfig.TAG_TILEMAP);

            var baseLayerRenderer = tiledEntity.AddComponent(new TiledMapRenderer(tmxMap, "Collision"));
            baseLayerRenderer.SetLayerToRender("Base");
            baseLayerRenderer.RenderLayer = GameConfig.RenderLayerBase;

            var fogLayerRenderer = tiledEntity.AddComponent(new TiledMapRenderer(tmxMap));
            fogLayerRenderer.SetLayerToRender("FogOfWar");
            fogLayerRenderer.SetRenderLayer(GameConfig.RenderLayerFogOfWar);

            tiledEntity.AddComponent<FogOfWarHelper>();

            _cameraController?.ConfigureZoomForMap(_mapPath);
        }

        private void SpawnPit()
        {
            var pitEntity = CreateEntity("pit");
            var pitPos = HeroActionBase.GetPitCenterWorldPosition();
            pitEntity.SetPosition(pitPos);

            pitEntity.AddComponent(new PitComponent
            {
                CrystalPower = 1f,
                IsActive = true,
                EffectRadius = 100f
            });

            pitEntity.AddComponent(new BasicRenderableComponent
            {
                Color = GameConfig.PitColor,
                RenderWidth = GameConfig.PitWidth,
                RenderHeight = GameConfig.PitHeight
            });

            pitEntity.AddComponent(new PitControllerComponent());
        }

        private void SpawnHero()
        {
            var heroStart = HeroActionBase.GetMapCenterWorldPosition();
            var hero = CreateEntity("hero").SetPosition(heroStart);

            hero.AddComponent(new PrototypeSpriteRenderer(20, 20));
            var collider = hero.AddComponent(new BoxCollider(GameConfig.HeroWidth, GameConfig.HeroHeight));
            Flags.SetFlagExclusive(ref collider.CollidesWithLayers, GameConfig.PhysicsTileMapLayer);
            Flags.SetFlagExclusive(ref collider.PhysicsLayer, GameConfig.PhysicsHeroWorldLayer);

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