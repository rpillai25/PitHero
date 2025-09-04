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
        public const int VirtualHeight = 360;
        public const int InternalWorldWidth = 1920;
        public const int InternalWorldHeight = 800;
        
        // Window Configuration
        public const bool AlwaysOnTop = true;
        public const bool ClickThrough = false;
        public const bool BorderlessWindow = true;
        
        // Hero Configuration
        public const int HeroWidth = 32;
        public const int HeroHeight = 32;
        
        // Hero movement speed
        public const float HeroMovementSpeed = 64f;  //Move speed in pixels per second (64 pixels = 2 tiles)
        public const float HeroPitMovementSpeed = 32f; //Move speed in pixels per second when in pit (32 pixels = 1 tile)
        public const float HeroJumpSpeed = 4f; //Jump speed in tiles per second
        
        // Fog of war movement speed configuration
        public const float HeroFogCooldownDuration = 1f; // Duration in seconds for fog cooldown after clearing fog

        // Pit Configuration
        public const int PitWidth = 64;
        public const int PitHeight = 64;
        public const float PitSpawnInterval = 10f; // seconds
        
        // Building Configuration
        public const int TownBuildingWidth = 48;
        public const int TownBuildingHeight = 48;
        
        // Camera Configuration
        public const float CameraDefaultZoom = 1f; // default zoom level
        public const float CameraMinimumZoom = 0.5f; // can't zoom out past default for normal maps
        public const float CameraMaximumZoom = 10f; // can zoom in really close
        public const float CameraMinimumZoomLargeMap = 0.25f; // can zoom out to 0.5x for large maps (clean divisor)
        public const float CameraZoomSpeed = 0.001f; // zoom sensitivity per mouse wheel notch
        public const float CameraPanSpeed = 1f; // pan speed multiplier
        
        // Game Timing
        public const float GameTickInterval = 1f / 60f; // 60 FPS
        public const float EventProcessingInterval = 1f / 120f; // 120 Hz event processing
        
        // World Bounds
        public static readonly Rectangle WorldBounds = new Rectangle(0, 0, InternalWorldWidth, InternalWorldHeight);
        public const int TileSize = 32;

        // Pit rectangle (adjust as needed)
        public const int PitRectX = 1;
        public const int PitRectY = 2;
        public const int PitRectWidth = 12;   // tile width span
        public const int PitRectHeight = 9;   // tile height span
        public const int PitCenterTileX = 6;
        public const int PitCenterTileY = 6;

        // Map "center" (MUST be outside pit)
        public const int MapCenterTileX = 33;
        public const int MapCenterTileY = 6;

        // Sensor radii (in pixels)
        public const float CenterRadiusPixels = 14f;

        // Adjacency ring radius in tiles (outside pit)
        public const int PitAdjacencyRadiusTiles = 2;

        // Pit collider padding (pixels around tile boundaries)
        public const int PitColliderPadding = 4;

        // Jump movement configuration
        public const float JumpMovementSpeed = 4.0f; // tiles per second for pit jumping (faster than normal movement)

        // Colors
        public static readonly Color HeroColor = Color.Blue;
        public static readonly Color PitColor = Color.Red;
        public static readonly Color TownColor = Color.Green;
        public static readonly Color BackgroundColor = Color.Black;

        // Tags
        public const int TAG_TILEMAP = 1; // Tag for tilemap entities
        public const int TAG_HERO = 2; // Tag for hero entity
        public const int TAG_PIT = 3; // Tag for pit entity
        public const int TAG_OBSTACLE = 4; // Tag for obstacle entities
        public const int TAG_TREASURE = 5; // Tag for treasure entities
        public const int TAG_MONSTER = 6; // Tag for monster entities
        public const int TAG_WIZARD_ORB = 7; // Tag for wizard orb entity

        // Render Layers (the lower the number, the higher the layer)

        public const int RenderLayerHero = 5; // Hero always on top of other actors
        public const int RenderLayerFogOfWar = 40;   // Fog of war layer above most things
        public const int RenderLayerActors = 50; // Actors and entities layer
        public const int RenderLayerBase = 100; // Background layer

        public const int RenderLayerUI = 998; // UI layer (always on top)
        public const int TransparentPauseOverlay = 999; // Transparent overlay for paused action when UI is active

        // Physics Layers (determines which layer an entity is on for collision)
        public const int PhysicsTileMapLayer = 0;   // Tilemap "Collision" layer
        public const int PhysicsHeroWorldLayer = 1; // Hero layer for collision
        public const int PhysicsPitLayer = 2;       // Pit trigger layer

        // Entity names
        public const string EntityHero = "Hero";
    }
}