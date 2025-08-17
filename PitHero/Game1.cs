using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Nez;
using PitHero.ECS.Scenes;

namespace PitHero
{
    class Game1 : Core
    {
        public Game1() : base(GameConfig.VirtualWidth, GameConfig.VirtualHeight, false, "PitHero")
        {
            // Set up for pixel-perfect rendering - uncomment for scaled pixel art
            //System.Environment.SetEnvironmentVariable("FNA_OPENGL_BACKBUFFER_SCALE_NEAREST", "1");
        }

        protected override void Initialize()
        {
            base.Initialize();

            // Set the scene - this handles Update/Draw logic
            Scene = new MainGameScene();


#if DEBUG
            // Debug console setup if needed
            // System.Diagnostics.Debug.Listeners.Add(new System.Diagnostics.TextWriterTraceListener(System.Console.Out));
#endif
        }

        protected override void LoadContent()
        {
            base.LoadContent();

            // Configure window as horizontal strip docked at bottom
            //WindowManager.ConfigureHorizontalStrip(this,
            //    alwaysOnTop: GameConfig.AlwaysOnTop,
            //    clickThrough: GameConfig.ClickThrough);
            WindowManager.ConfigureHorizontalStrip(this,
                alwaysOnTop: GameConfig.AlwaysOnTop);
        }

    }
}

