using Microsoft.Xna.Framework;
using Nez;
using PitHero.ECS.Scenes;

namespace PitHero
{
    class Game1 : Core
    {
        public Game1() : base(GameConfig.VirtualWidth, GameConfig.VirtualHeight, false, "PitHero")
        {
            // Set up for pixel-perfect rendering - uncomment for scaled pixel art
            System.Environment.SetEnvironmentVariable("FNA_OPENGL_BACKBUFFER_SCALE_NEAREST", "1");
        }

        protected override void Initialize()
        {
            base.Initialize();

            var scene = new TitleScreenScene();
            scene.ClearColor = Color.CornflowerBlue;   // Set background
            // Optional: letterbox bars (if any) also blue
            scene.LetterboxColor = Color.CornflowerBlue;

            Scene = scene;
        }

        protected override void LoadContent()
        {
            base.LoadContent();

            // Configure window as horizontal strip docked at bottom
            //WindowManager.ConfigureHorizontalStrip(this,
            //    alwaysOnTop: GameConfig.AlwaysOnTop,
            //    clickThrough: GameConfig.ClickThrough);
            WindowManager.ConfigureHorizontalStripOneThird(this,
                alwaysOnTop: GameConfig.AlwaysOnTop);
        }

    }
}

