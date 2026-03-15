using Microsoft.Xna.Framework;
using Nez;
using Nez.Persistence.Binary;
using PitHero.ECS.Scenes;
using PitHero.Services;
using PitHero.Util;

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

            Graphics.Instance.BitmapFont = Content.LoadBitmapFont(GameConfig.FontMainUI);

            // Register global services
            Services.AddService(new PauseService());
            Services.AddService(new CrystalMerchantVault());
            Services.AddService(new PitMerchantVault());
            Services.AddService(new SecondChanceMerchantVault());
            Services.AddService(new GameStateService());
            Services.AddService(new HairstyleQueueService(GameConfig.MaleHeroHairstyleCount));
            Services.AddService(new HeroDesignService());

            // Register persistence services
            var fileDataStore = new FileDataStore(null);
            Services.AddService(fileDataStore);
            Services.AddService(new SaveLoadService(fileDataStore));

            // Register global managers
            SoundEffectManager soundEffectManager = new SoundEffectManager();
            soundEffectManager.Init(Content);
            RegisterGlobalManager(soundEffectManager);

            // Disable pausing when focus is lost - essential for idle game behavior
            PauseOnFocusLost = false;

            var scene = new TitleScreenScene();
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                var soundEffectManager = GetGlobalManager<SoundEffectManager>();
                soundEffectManager?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

