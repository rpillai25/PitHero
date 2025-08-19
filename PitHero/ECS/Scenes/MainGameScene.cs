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

        public override void Initialize()
        {
            base.Initialize();

            SetDesignResolution(GameConfig.VirtualWidth, GameConfig.VirtualHeight, SceneResolutionPolicy.BestFit);
            ClearColor = Color.Transparent;

            // Add camera controller for zoom and pan functionality
            var cameraEntity = CreateEntity("camera-controller");
            cameraEntity.AddComponent(Camera);
            cameraEntity.AddComponent(new CameraControllerComponent());

            _gameManager = new GameManager();
            _gameManager.StartNewGame();

            // --- Load TMX map and set up TiledMapRenderer ---
            var tmxMap = Core.Content.LoadTiledMap("Content/Tilemaps/PitHero.tmx");

            // Create the entity for the tilemap
            var tiledEntity = CreateEntity("tilemap");
            // Optionally set a tag if you want to query by tag later
            tiledEntity.SetTag(GameConfig.TAG_TILEMAP);

            // Add TiledMapRenderer, specifying the collision layer
            var tiledMapRenderer = tiledEntity.AddComponent(new TiledMapRenderer(tmxMap, "Collision"));
            // Only render the "Base" layer (do not render "Collision" layer)
            tiledMapRenderer.SetLayerToRender("Base");
            // Optionally set render layer, material, or effect if needed:
            // tiledMapRenderer.RenderLayer = 10;
            // tiledMapRenderer.Material = Material.StencilWrite(1);
            // tiledMapRenderer.Material.Effect = Core.Content.LoadNezEffect<SpriteAlphaTestEffect>();

            // Add other entities/components as needed
            CreateEntity("demo-entity")
                .SetPosition(new Vector2(500, 150))
                .AddComponent(new PrototypeSpriteRenderer(20, 20));

            // Set up UI overlay using ScreenSpaceRenderer
            SetupUIOverlay();
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