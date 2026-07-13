using Microsoft.Xna.Framework;
using Nez;
using Nez.Persistence.Binary;
using PitHero.Config;
using PitHero.ECS.Scenes;
using PitHero.Services;
using PitHero.Util;
using RolePlayingFramework.Equipment;

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

            // Synchronize ItemRegistry tier stride with the biome loop length so that
            // tier-scaled gear names ("Item+2", "Item+3", …) resolve correctly on load.
            ItemRegistry.TierDepthStride = BiomeProgressionConfig.MaxBiomeLevel;

            Graphics.Instance.BitmapFont = Content.LoadBitmapFont(GameConfig.FontMainUI);

            // Register global services
            Services.AddService(new TextService());
            Services.AddService(new PauseService());
            Services.AddService(new CrystalMerchantVault());
            Services.AddService(new PitMerchantVault());
            Services.AddService(new SecondChanceMerchantVault());
            Services.AddService(new GameStateService());
            Services.AddService(new DefeatedMonsterService());
            Services.AddService(new InGameTimeService());
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

            ParticleEffectManager particleEffectManager = new ParticleEffectManager();
            particleEffectManager.Init(Content);
            RegisterGlobalManager(particleEffectManager);

#if DEBUG
            // Analytics for game balancing (issue #289) - debug builds only
            RegisterGlobalManager(new Services.Analytics.AnalyticsManager());
#endif

            // Disable pausing when focus is lost - essential for idle game behavior
            PauseOnFocusLost = false;

            Core.ExitOnEscapeKeypress = false;
            Services.AddService(new TileStateService());

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
                PitHero.Services.Analytics.AnalyticsService.LogSessionEnd();
                PitHero.Services.Analytics.AnalyticsService.Shutdown();

                var soundEffectManager = GetGlobalManager<SoundEffectManager>();
                soundEffectManager?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

