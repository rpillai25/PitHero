using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nez;
using Nez.Sprites;
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

            // Set design resolution for the scene
            SetDesignResolution(GameConfig.VirtualWidth, GameConfig.VirtualHeight, SceneResolutionPolicy.None);

            // Initialize the event-driven game manager
            _gameManager = new GameManager();
            _gameManager.StartNewGame();

            // Add a simple demo entity with a colored rectangle (similar to original DefaultScene.cs)
            CreateEntity("demo-entity")
                .SetPosition(new Vector2(150, 150))
                .AddComponent(new PrototypeSpriteRenderer(20, 20));
        }


        public override void Update()
        {
            base.Update();

            // Update the game manager and all systems using Nez.Time
            float deltaTime = Time.DeltaTime;
            _gameManager.Update(deltaTime);
        }

        // Note: Nez handles rendering automatically through RenderableComponents
        // Custom rendering logic should be implemented through Components or Renderers
        // rather than overriding a Render method
    }
}