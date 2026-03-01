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

            SetDesignResolution(GameConfig.VirtualWidth, GameConfig.VirtualHeight, SceneResolutionPolicy.BestFit);
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
            var screenSpaceRenderer = new ScreenSpaceRenderer(100, 999);
            AddRenderer(screenSpaceRenderer);

            // Create UI entity with UICanvas
            var uiEntity = CreateEntity("hero-creation-ui");
            var uiCanvas = uiEntity.AddComponent(new UICanvas());
            uiCanvas.IsFullScreen = true;
            uiCanvas.RenderLayer = 999;

            // Create the hero preview entity centered in the left panel (left of the controls window at 60%)
            var previewEntity = CreateEntity("hero-preview");
            previewEntity.SetPosition(GameConfig.VirtualWidth * 0.30f, GameConfig.VirtualHeight * 0.55f);

            // Initialize the hero creation UI
            _heroCreationUI = new HeroCreationUI(_mapPath);
            _heroCreationUI.InitializeUI(uiCanvas.Stage, previewEntity);
        }

        /// <summary>Updates the hero creation UI each frame</summary>
        public override void Update()
        {
            base.Update();
            _heroCreationUI?.Update();
        }
    }
}
