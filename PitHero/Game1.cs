using Microsoft.Xna.Framework;
using Nez;
using PitHero.ECS.Scenes;

namespace PitHero
{
    class Game1 : Core
    {
        public Game1() : base()
        {
            // Set up for pixel-perfect rendering - uncomment for scaled pixel art
            // System.Environment.SetEnvironmentVariable("FNA_OPENGL_BACKBUFFER_SCALE_NEAREST", "1");
        }

        protected override void Initialize()
        {
            base.Initialize();

            // Window configuration will be handled by WindowManager
            
            // Configure window as horizontal strip docked at bottom
            WindowManager.ConfigureHorizontalStrip(this, 
                alwaysOnTop: GameConfig.AlwaysOnTop, 
                clickThrough: GameConfig.ClickThrough);

            // Set the scene - this handles Update/Draw logic
            Scene = new MainGameScene();

#if DEBUG
            // Debug console setup if needed
            // System.Diagnostics.Debug.Listeners.Add(new System.Diagnostics.TextWriterTraceListener(System.Console.Out));
#endif
        }
    }
}

