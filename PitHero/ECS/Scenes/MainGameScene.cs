using Microsoft.Xna.Framework;
using Nez;
using Nez.Tiled;
using PitHero.ECS.Components;

namespace PitHero.ECS.Scenes
{
    /// <summary>
    /// Main game scene that handles game logic following Nez architecture
    /// </summary>
    public class MainGameScene : Scene
    {
        private GameManager _gameManager;

        public override void Initialize()
        {
            base.Initialize();

            SetDesignResolution(GameConfig.VirtualWidth, GameConfig.VirtualHeight, SceneResolutionPolicy.None);
            ClearColor = Color.Transparent;

            _gameManager = new GameManager();
            _gameManager.StartNewGame();

            // --- Load TMX map and set up TiledMapRenderer ---
            var tmxMap = Core.Content.LoadTiledMap("Content/Tilemaps/PitHero.tmx");

            // Create the entity for the tilemap
            var tiledEntity = CreateEntity("tilemap");
            // Optionally set a tag if you want to query by tag later
            // tiledEntity.SetTag(GameConfig.TAG_TILEMAP);

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
        }

        public override void Update()
        {
            base.Update();
            float deltaTime = Time.DeltaTime;
            _gameManager.Update(deltaTime);
        }
    }
}