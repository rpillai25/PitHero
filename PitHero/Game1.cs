using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace PitHero
{
    class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private GameManager _gameManager;

        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
            
            // Set up for pixel-perfect rendering
            _graphics.GraphicsProfile = GraphicsProfile.Reach;
            
            // Set virtual resolution
            _graphics.PreferredBackBufferWidth = GameConfig.VirtualWidth;
            _graphics.PreferredBackBufferHeight = GameConfig.VirtualHeight;
        }

        protected override void Initialize()
        {
            base.Initialize();
            
            // Initialize the event-driven game manager
            _gameManager = new GameManager();
            _gameManager.StartNewGame();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
        }

        protected override void Update(GameTime gameTime)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            
            // Update the game manager and all systems
            _gameManager.Update(deltaTime);

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(GameConfig.BackgroundColor);

            _spriteBatch.Begin(samplerState: SamplerState.PointClamp);

            // Render the current world state
            RenderWorldState(_gameManager.WorldState);
            
            // If in replay mode, render the replay overlay
            if (_gameManager.IsReplaying())
            {
                var replayWorldState = _gameManager.GetReplayWorldState();
                if (replayWorldState != null)
                {
                    RenderReplayOverlay(replayWorldState);
                }
            }

            _spriteBatch.End();

            base.Draw(gameTime);
        }
        
        private void RenderWorldState(ECS.WorldState worldState)
        {
            var entities = worldState.GetAllEntities();
            
            foreach (var entity in entities)
            {
                var renderComponent = entity.GetComponent<Components.RenderComponent>();
                if (renderComponent != null && entity.Enabled)
                {
                    // Create a simple colored rectangle for each entity
                    var texture = CreateColorTexture(renderComponent.Color);
                    var bounds = renderComponent.Bounds;
                    
                    _spriteBatch.Draw(texture, bounds, Color.White);
                }
            }
        }
        
        private void RenderReplayOverlay(ECS.WorldState replayWorldState)
        {
            // Render replay entities with some transparency or different styling
            var entities = replayWorldState.GetAllEntities();
            
            foreach (var entity in entities)
            {
                var renderComponent = entity.GetComponent<Components.RenderComponent>();
                if (renderComponent != null && entity.Enabled)
                {
                    var texture = CreateColorTexture(renderComponent.Color);
                    var bounds = renderComponent.Bounds;
                    
                    // Render with transparency to show it's a replay
                    _spriteBatch.Draw(texture, bounds, Color.White * 0.5f);
                }
            }
        }
        
        private Texture2D CreateColorTexture(Color color)
        {
            // Simple method to create a 1x1 colored texture
            // In a real implementation, you'd cache these textures
            var texture = new Texture2D(GraphicsDevice, 1, 1);
            texture.SetData(new[] { color });
            return texture;
        }
    }
}

