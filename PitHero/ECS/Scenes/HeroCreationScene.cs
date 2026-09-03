using Microsoft.Xna.Framework;
using Nez;
using PitHero.UI;

namespace PitHero.ECS.Scenes
{
    /// <summary>
    /// Scene for hero creation, displaying a paperdoll preview and appearance controls
    /// </summary>
    public class HeroCreationScene : Scene
    {
        private HeroCreationUI _heroCreationUI;
        private string _mapPath;

        /// <summary>Creates a new HeroCreationScene that will transition to MainGameScene with the given map</summary>
        public HeroCreationScene(string mapPath)
        {
            _mapPath = mapPath;
        }

        /// <summary>Initializes the scene with design resolution</summary>
        public override void Initialize()
        {
            base.Initialize();

            SetDesignResolution(GameConfig.VirtualWidth, GameConfig.VirtualHeight, SceneResolutionPolicy.FixedHeight);
            ClearColor = Color.CornflowerBlue;
        }

        /// <summary>Sets up the hero creation UI after constructor has fully completed</summary>
        public override void Begin()
        {
            base.Begin();
            SetupHeroCreationUI();
        }

        /// <summary>Sets up the screen-space renderer, UI canvas, preview entity, and HeroCreationUI</summary>
        private void SetupHeroCreationUI()
        {
            // Add ScreenSpaceRenderer for UI
            var screenSpaceRenderer = new ScreenSpaceRenderer(100, new int[]{999,998});
            AddRenderer(screenSpaceRenderer);

            // Create UI entity with UICanvas
            var uiEntity = CreateEntity("hero-creation-ui");
            var uiCanvas = uiEntity.AddComponent(new UICanvas());
            // Stage in render-target space (like MainGameScene) so layout and mouse input stay
            // consistent under the FixedHeight policy on any aspect ratio
            uiCanvas.IsFullScreen = false;
            uiCanvas.RenderLayer = 999;

            // Compute preview entity position to appear above the direction buttons in the Appearance window
            // Window layout: totalWidth = 560 + 10 + 350 = 920, centered on the stage width
            // Controls table is ~290px wide; preview centered above the direction arrows to its right
            const float windowWidth = 560f;
            const float jobInfoWidth = 350f;
            const float gap = 10f;
            float totalWidth = windowWidth + gap + jobInfoWidth;
            float startX = (uiCanvas.Stage.GetWidth() - totalWidth) / 2f;
            float previewX = startX + windowWidth - 128f;
            float previewY = uiCanvas.Stage.GetHeight() * 0.48f;

            var previewEntity = CreateEntity("hero-preview");
            previewEntity.SetPosition(previewX, previewY);

            // Initialize the hero creation UI
            _heroCreationUI = new HeroCreationUI(_mapPath);
            _heroCreationUI.InitializeUI(uiCanvas.Stage, previewEntity);
        }

        /// <summary>Updates the hero creation UI once per rendered frame (presentation pass, never inside a simulation step)</summary>
        public override void PresentationUpdate()
        {
            base.PresentationUpdate();
            _heroCreationUI?.Update();
        }
    }
}
