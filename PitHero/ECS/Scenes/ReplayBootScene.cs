using Nez;

namespace PitHero.ECS.Scenes
{
    /// <summary>
    /// One-step trampoline used to restart the game scene while another game scene is running.
    /// MainGameScene registers its scene-scoped services in its constructor (Scene.Initialize), so
    /// it cannot be constructed until the current MainGameScene has unloaded and removed them.
    /// Swapping to this empty scene first lets that teardown happen; its Begin then constructs the
    /// real scene, which Nez swaps in on the following step.
    /// </summary>
    public class ReplayBootScene : Scene
    {
        private readonly string _mapPath;

        /// <summary>Creates the trampoline for the given gameplay map.</summary>
        public ReplayBootScene(string mapPath)
        {
            _mapPath = mapPath;
        }

        /// <summary>A renderer so the single frame this scene may be drawn for is not an error.</summary>
        public override void Initialize()
        {
            base.Initialize();
            AddRenderer(new DefaultRenderer());
            // Same ground color as gameplay so the single transition frame does not flash
            ClearColor = new Microsoft.Xna.Framework.Color(71, 114, 56);
            LetterboxColor = ClearColor;
        }

        /// <summary>The previous scene has ended: build the real gameplay scene now.</summary>
        public override void Begin()
        {
            base.Begin();
            Core.Scene = MainGameScene.CreateForGameplay(_mapPath);
        }
    }
}
