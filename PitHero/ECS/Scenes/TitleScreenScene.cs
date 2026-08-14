using Microsoft.Xna.Framework;
using Nez;
using PitHero.UI;

namespace PitHero.ECS.Scenes
{
    /// <summary>
    /// Title screen scene that displays the logo and main menu
    /// </summary>
    public class TitleScreenScene : Scene
    {
        private TitleMenuUI _titleMenuUI;

        public override void Initialize()
        {
            base.Initialize();

            SetDesignResolution(GameConfig.VirtualWidth, GameConfig.VirtualHeight, SceneResolutionPolicy.FixedHeight);
            ClearColor = Color.CornflowerBlue;

            // Set up UI overlay using ScreenSpaceRenderer
            SetupTitleUI();
        }

        private void SetupTitleUI()
        {
            // Add ScreenSpaceRenderer for UI that uses its own camera which doesn't move
            var screenSpaceRenderer = new ScreenSpaceRenderer(100, 999);
            AddRenderer(screenSpaceRenderer);

            // Create UI entity with UICanvas component for screen-space UI
            var uiEntity = CreateEntity("title-ui");
            var uiCanvas = uiEntity.AddComponent(new UICanvas());
            // Stage in render-target space (like MainGameScene) so layout and mouse input stay
            // consistent under the FixedHeight policy on any aspect ratio
            uiCanvas.IsFullScreen = false;
            uiCanvas.RenderLayer = 999; // Render on screen space layer

            // Initialize title menu UI
            _titleMenuUI = new TitleMenuUI();
            _titleMenuUI.InitializeUI(uiCanvas.Stage);
        }

        public override void Update()
        {
            base.Update();
            _titleMenuUI?.Update();
        }
    }
}