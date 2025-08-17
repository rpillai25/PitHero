using Microsoft.Xna.Framework;

namespace PitHero
{
    /// <summary>
    /// Central configuration for all game constants
    /// </summary>
    public static class GameConfig
    {
        // Screen and Resolution
        public const int VirtualWidth = 1920;
        public const int VirtualHeight = 1080;
        public const int InternalWorldWidth = 1920;
        public const int InternalWorldHeight = 360;  // Game world matches playable area height
        
        // Playable Area (bottom section that's initially clickable)
        public const int PlayableAreaWidth = 1920;
        public const int PlayableAreaHeight = 360;
        public const int PlayableAreaX = 0;
        public const int PlayableAreaY = VirtualHeight - PlayableAreaHeight; // 720
        
        // Window Configuration
        public const bool AlwaysOnTop = true;
        public const bool ClickThrough = false;
        public const bool BorderlessWindow = true;
        
        // Hero Configuration
        public const int HeroWidth = 32;
        public const int HeroHeight = 32;
        public const float HeroMoveSpeed = 100f; // pixels per second
        
        // Pit Configuration
        public const int PitWidth = 64;
        public const int PitHeight = 64;
        public const float PitSpawnInterval = 10f; // seconds
        
        // Building Configuration
        public const int TownBuildingWidth = 48;
        public const int TownBuildingHeight = 48;
        
        // Game Timing
        public const float GameTickInterval = 1f / 60f; // 60 FPS
        public const float EventProcessingInterval = 1f / 120f; // 120 Hz event processing
        
        // World Bounds
        public static readonly Rectangle WorldBounds = new Rectangle(0, 0, InternalWorldWidth, InternalWorldHeight);
        
        // Colors
        public static readonly Color HeroColor = Color.Blue;
        public static readonly Color PitColor = Color.Red;
        public static readonly Color TownColor = Color.Green;
        public static readonly Color BackgroundColor = Color.Black;
    }
}