using Microsoft.Xna.Framework;
using Nez;
using Nez.Tiled;
using Nez.UI;
using PitHero.ECS.Components;
using PitHero.UI;

namespace PitHero.ECS.Scenes
{
    /// <summary>
    /// Main game scene that handles game logic following Nez architecture
    /// </summary>
    public class MainGameScene : Scene
    {
        private GameManager _gameManager;
        private SettingsUI _settingsUI;
        private string _mapPath;
        private bool _isInitializationComplete = false;
        private CameraControllerComponent _cameraController;

        public MainGameScene() : this("Content/Tilemaps/PitHero.tmx")
        {
        }

        public MainGameScene(string mapPath)
        {
            _mapPath = mapPath;
        }

        public override void Initialize()
        {
            base.Initialize();

            SetDesignResolution(GameConfig.VirtualWidth, GameConfig.VirtualHeight, SceneResolutionPolicy.BestFit);
            ClearColor = Color.Transparent;

            // Add camera controller for zoom and pan functionality
            var cameraEntity = CreateEntity("camera-controller");
            cameraEntity.AddComponent(Camera);
            _cameraController = cameraEntity.AddComponent(new CameraControllerComponent());

            _gameManager = new GameManager();
            _gameManager.StartNewGame();

            // Set up UI overlay using ScreenSpaceRenderer first
            SetupUIOverlay();
        }

        public override void Begin()
        {
            base.Begin();
            
            // Load the map after the scene is fully constructed and ready
            if (!_isInitializationComplete)
            {
                LoadMap();
                
                // Add other entities/components after map is loaded
                var hero = CreateEntity(GameConfig.EntityHero)
                    .SetPosition(new Vector2(500, 150))
                    .AddComponent(new PrototypeSpriteRenderer(20, 20));
                var heroCollider = hero.AddComponent<BoxCollider>();
                //Meant for colliders that hero can move into
                Flags.SetFlagExclusive(ref heroCollider.CollidesWithLayers, GameConfig.PhysicsTileMapLayer);
                Flags.SetFlagExclusive(ref heroCollider.PhysicsLayer, GameConfig.PhysicsHeroWorldLayer);


                _isInitializationComplete = true;
            }
        }

        private void LoadMap()
        {
            if (string.IsNullOrEmpty(_mapPath))
                return;

            // --- Load TMX map and set up TiledMapRenderer ---
            var tmxMap = Core.Content.LoadTiledMap(_mapPath);

            // Create the entity for the tilemap
            var tiledEntity = CreateEntity("tilemap");
            // Optionally set a tag if you want to query by tag later
            tiledEntity.SetTag(GameConfig.TAG_TILEMAP);

            // Add TiledMapRenderer, specifying the collision layer
            var baseLayerRenderer = tiledEntity.AddComponent(new TiledMapRenderer(tmxMap, "Collision"));
            // Only render the "Base" layer (do not render "Collision" layer)
            baseLayerRenderer.SetLayerToRender("Base");
            // Optionally set render layer, material, or effect if needed:
            baseLayerRenderer.RenderLayer = GameConfig.RenderLayerBase;
            // baseLayerRenderer.Material = Material.StencilWrite(1);
            // baseLayerRenderer.Material.Effect = Core.Content.LoadNezEffect<SpriteAlphaTestEffect>();

            var fogOfWarLayerRenderer = tiledEntity.AddComponent(new TiledMapRenderer(tmxMap));
            // Set the fog of war layer to render the "FogOfWar" layer
            fogOfWarLayerRenderer.SetLayerToRender("FogOfWar");
            baseLayerRenderer.SetRenderLayer(GameConfig.RenderLayerFogOfWar);

            // Add FogOfWar helper component
            tiledEntity.AddComponent<FogOfWarHelper>();

            // Configure camera zoom limits based on the loaded map
            _cameraController?.ConfigureZoomForMap(_mapPath);
        }

        private void SetupUIOverlay()
        {
            // Add ScreenSpaceRenderer for UI that uses its own camera which doesn't move
            var screenSpaceRenderer = new ScreenSpaceRenderer(100, 999);
            AddRenderer(screenSpaceRenderer);

            // Create UI entity with UICanvas component for screen-space UI
            var uiEntity = CreateEntity("ui-overlay");
            var uiCanvas = uiEntity.AddComponent(new UICanvas());
            uiCanvas.IsFullScreen = true;
            uiCanvas.RenderLayer = 999; // Render on screen space layer

            // Initialize settings UI
            _settingsUI = new SettingsUI(Core.Instance);
            _settingsUI.InitializeUI(uiCanvas.Stage);
        }

        public override void Update()
        {
            base.Update();
            float deltaTime = Time.DeltaTime;
            _gameManager.Update(deltaTime);
            
            // Update settings UI
            _settingsUI?.Update();
        }
    }
}