using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace PitHero
{
    /// <summary>
    /// Central configuration for all game constants
    /// </summary>
    public static class GameConfig
    {
        // Screen and Resolution
        // Scenes use SceneResolutionPolicy.FixedHeight: VirtualHeight is the fixed design height,
        // while the render-target width grows with the window aspect (ultrawide sees more world).
        // VirtualWidth is only a reference width (initial backbuffer, minimum-layout reference) —
        // runtime layout/camera code must use Stage.GetWidth()/Scene.SceneRenderTargetSize instead.
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
        public const float HeroCriticalHPPercent = 0.4f;
        public const float HeroCriticalMPPercent = 0.5f;

        // --- Burst damage thresholds for Strategic/Blitz tactics ---
        /// <summary>
        /// Burst damage detection threshold for Strategic and Blitz tactics. If a character loses this fraction
        /// of their max HP in a single hit, the burst-critical flag is triggered.
        /// </summary>
        public static float BurstDamageThresholdPercent = 0.20f; // 20% of MaxHP lost triggers burst flag

        /// <summary>
        /// Burst damage recovery threshold for Strategic and Blitz tactics. Once the burst-critical flag is set,
        /// it clears when HP recovers to or above this fraction of max HP.
        /// </summary>
        public static float BurstDamageRecoveryPercent = 0.60f; // Burst flag clears once HP recovers to 60% of MaxHP

        // --- Burst damage thresholds for Defensive tactic (more careful) ---
        /// <summary>
        /// Burst damage detection threshold for Defensive tactic. Lower threshold means smaller hits trigger
        /// the burst flag, making the AI more cautious.
        /// </summary>
        public static float BurstDamageThresholdPercentDefensive = 0.15f; // 15% of MaxHP lost triggers burst flag

        /// <summary>
        /// Burst damage recovery threshold for Defensive tactic. Higher recovery requirement means characters
        /// must heal more before the burst flag clears, keeping them safer.
        /// </summary>
        public static float BurstDamageRecoveryPercentDefensive = 0.80f; // Burst flag clears once HP recovers to 80% of MaxHP
        public const float ReplenishThresholdDefault = 0.9f;
        public const int MaleHeroHairstyleCount = 5; // Number of available hairstyles for male heroes

        // Action Configuration
        public const float BattleDigitBounceWait = 0.5f;
        public const float BattleTurnWait = 0.7f;
        public const float TreasureOpenWait = 0.5f;

        // Monster animation
        public const float MonsterWobbleFrequency = 10f; // radians/sec oscillation for 1-frame move animations
        public const float MonsterWobbleAmplitude = 0.12f; // radians (~7°) max rotation during wobble
        public const float MonsterAttackPlaceholderDuration = 0.6f; // seconds for the placeholder attack animation
        public const float MonsterAttackJumpHeight = 6f; // pixels to offset upward during placeholder attack

        // Sound configuration (can be updated in UI)
        public static float MasterVolume = 0.5f;

        /// <summary>World sounds farther than this many tiles past the nearest horizontal camera edge are inaudible and skipped</summary>
        public const int MaxAudibleDistanceTiles = 10;

        // Hero movement speed
        public const float HeroMovementSpeed = 64f;  //Move speed in pixels per second (64 pixels = 2 tiles)
        public const float HeroPitMovementSpeed = 32f; //Move speed in pixels per second when in pit (32 pixels = 1 tile)
        public const float HeroJumpSpeed = 4f; //Jump speed in tiles per second

        // Mercenary configuration
        public const float MercenaryMinSpawnIntervalSeconds = 5f; // First mercenary (empty tavern) spawns after 5 seconds
        public const float BaseMonsterJoinChance = 0.10f;
        public const float MercenarySpawnIntervalMinSeconds = 60f;  // New patrons arrive every 1-2 scaled minutes...
        public const float MercenarySpawnIntervalMaxSeconds = 120f; // ...rolled randomly per arrival

        // Monster house configuration
        public const int MonsterHouseCapacity = 16; // Max allied monsters that can live in one Monster House
        public const int DaytimeMonsterAddCostGold = 500;  // Gold to manually add a Daytime monster (issue #283)
        public const int NocturnalMonsterAddCostGold = 700; // Gold to manually add a Nocturnal monster (issue #283)

        // Analytics configuration (issue #289; debug builds only)
        public const bool AnalyticsEnabled = true; // Master switch for analytics logging in debug builds
        public const float AnalyticsFlushIntervalSeconds = 15f; // How often buffered analytics events are written to disk
        public const int AnalyticsFlushThresholdChars = 65536; // Buffer size that forces an immediate analytics flush
        public const string AnalyticsDirectoryName = "analytics"; // Subfolder under the exe-named %LOCALAPPDATA% folder (same root as save files)

        // Inn configuration
        public const int InnkeeperTileX = 69; // Innkeeper stands at (69, 3)
        public const int InnkeeperTileY = 3;
        public const int InnPaymentTileX = 67; // Hero pays at (67, 3) facing right
        public const int InnPaymentTileY = 3;
        public const int InnHeroBedTileX = 73; // Hero sleeps at (73, 3)
        public const int InnHeroBedTileY = 3;
        public const int InnMercBed1TileX = 76; // First hired merc sleeps at (76, 3)
        public const int InnMercBed1TileY = 3;
        public const int InnMercBed2TileX = 73; // Second hired merc sleeps at (73, 7)
        public const int InnMercBed2TileY = 7;
        public const int InnExitTileX = 71; // Hero walks here after waking, between payment tile and bed
        public const int InnExitTileY = 3;
        public const int InnFarewellTileX = 63; // Innkeeper waves goodbye when the hero crosses (63,6) heading to the pit
        public const int InnFarewellTileY = 6;
        // Render-only Y offset applied to a sleeping actor's composite sprite so they nestle
        // into the bed instead of standing on top of it
        public const float SleepInBedSpriteOffsetY = 8f;
        // Inn nap cost scales with the party: each member costs the base fee plus a surcharge
        // per full 10 levels (level 30 -> 10 + 30 = 40g). Night sleep stays free.
        public const int InnCostBaseGoldPerMember = 10;
        public const int InnCostGoldPerTenLevels = 10;

        /// <summary>Inn nap cost for one party member of the given level.</summary>
        public static int GetInnCostForMember(int level)
            => InnCostBaseGoldPerMember + InnCostGoldPerTenLevels * (level / 10);
        public const int CrystalBuyBackBasePrice = 100; // Base gold cost per crystal level for Second Chance Shop
        public const float CrystalJPToGoldRate = 0.5f;  // Gold per JP invested in learned job skills (buy-back premium)
        public const int CrystalSynergySkillFee = 150;  // Flat gold per learned synergy skill (buy-back premium)
        public const int CrystalCreationFee = 100;      // Gold cost to create a brand-new crystal
        public const int CrystalForgeFeeMultiplier = 2; // Forge fee = combined crystal buy-back price x multiplier

        // Minimum height for dialog buttons (Yes/No/OK) so they present a comfortable click target
        public const float DialogButtonMinHeight = 16f;

        // Tavern seat configuration (for Stop Adventuring)
        public const int TavernHeroSeatTileX = 93;
        public const int TavernHeroSeatTileY = 6;
        public const int TavernMercenary1SeatTileX = 92;
        public const int TavernMercenary1SeatTileY = 7;
        public const int TavernMercenary2SeatTileX = 94;
        public const int TavernMercenary2SeatTileY = 7;

        // Fog of war movement speed configuration
        public const float HeroFogCooldownDuration = 1f; // Duration in seconds for fog cooldown after clearing fog

        // Hero uncover radius configuration
        public const int DefaultHeroUncoverRadius = 1; // Default radius for hero fog clearing
        public const int FogOfWarZeroTileIndex = 138; // Base GID for fog; GIDs 137-152 cover the 16 bitmask variants

        // Building Configuration
        public const int TownBuildingWidth = 48;
        public const int TownBuildingHeight = 48;

        // Farming Configuration
        /// <summary>Fraction of total growth below which any crop is eligible for early destroy/swap (e.g. 0.5 = 50%).</summary>
        public const float CropSwapDestroyProgressThreshold = 0.5f;
        /// <summary>Alpha value (0–255) for translucent plan-preview sprites shown in farm mode.</summary>
        public const int CropPlanPreviewAlpha = 153;
        /// <summary>Maximum seed quantity purchasable in one Second Chance Shop transaction.</summary>
        public const int SeedShopMaxPurchaseQuantity = 99;
        /// <summary>Maximum seeds of a single crop type the player can hold.</summary>
        public const int SeedInventoryMaxPerCrop = 999;
        /// <summary>Scale amplitude of the attention pulse on seed shop slots with unmet planned demand (0.1 = ±10%).</summary>
        public const float SeedShopPulseAmplitude = 0.1f;
        /// <summary>Angular speed (radians/sec) of the seed shop attention pulse.</summary>
        public const float SeedShopPulseSpeed = 4f;
        public const int TillZerothGid = 122;            // GIDs 122-137 are the 16 tilled-tile bitmask variants
        /// <summary>Base-layer GID of untilled grass used when restoring a tilled tile back to grass.</summary>
        public const int FarmGrassTileGid = 13;
        public const int WetZeroTileIndex = 154;         // GIDs 154-169 are the 16 wet-tile bitmask variants
        public const float WaterBaseDurationSeconds = 3f;  // watering time at FarmingProficiency 1
        public const float PlantBaseDurationSeconds = 2f;  // planting time at FarmingProficiency 1
        public const int WateringCanMaxCharges = 3;         // tiles watered per can-full before refill
        public const float WateringCanFillDurationSeconds = 1f; // seconds to display can while filling at pond
        public const int FarmMinTillTileX = 120;         // tiles at x >= this can be marked for tilling
        public const int FarmMinTillTileY = 1;           // tiles at y >= this can be marked for tilling
        public const int FarmMinWanderTileX = 118;       // farming monsters wander at x >= this
        public const float TillBaseDurationSeconds = 3f;       // hoe time at FarmingProficiency 1
        public const float TillProficiencySpeedStep = 0.06f;   // till duration reduced 6% per proficiency point above 1
        public const float FarmMonsterIdlePollInterval = 0.25f; // seconds between queue checks while idle
        public const int FarmWanderRadiusTiles = 4;             // idle wander stays within this radius of the nearest field tile
        public const int FarmWanderMaxEastOffsetTiles = 5;      // idle wander goes at most this many tiles east of the rightmost farm object (building or tilled tile)
        public const float HarvestWaitSeconds = 5f;             // worker waits this long on the crop tile before harvesting
        public const float AppleHarvestWaitSeconds = 2f;        // worker waits this long under an apple tree before jumping
        public const float AppleHarvestJumpDurationSeconds = 0.6f; // duration of the apple-picking jump arc
        public const float AppleTreeTopHarvestOffsetPx = 26f;   // worker sprite centre rises to this many px below the apple-tree top
        public const float HarvestDepositSeconds = 2f;          // worker stays hidden "inside" the storage building this long after delivering

        // Kitchen / Tavern Dining (issue #319)
        public const int KitchenStove1TileX = 83;
        public const int KitchenStove2TileX = 84;
        public const int KitchenStove3TileX = 85;
        public const int KitchenStoveTileY = 2;
        public const int KitchenSinkTileX = 86;
        public const int KitchenSinkTileY = 2;
        public const int MaxKitchenCooks = 3;
        public const int MaxKitchenServers = 2;
        public const int MaxKitchenRunners = 3;         // runners also bus plates, so peak hours want a third
        public const float KitchenHatOverlapPixels = 6f;        // how far the job hat's brim overlaps the head top
        public const float KitchenHatCheckIntervalSeconds = 5f; // how often the coordinator re-checks that workers wear hats
        public const int KitchenTicketBoardTileX = 82;          // servers post orders / cooks read them here
        public const int KitchenTicketBoardTileY = 2;
        public const int KitchenFridgeTileX = 87;               // cooks grab ingredients here; runners restock it
        public const int KitchenFridgeTileY = 2;
        public const int KitchenServingTableTileX = 87;         // serving tables at (87,3),(87,4),(87,5)
        public const int KitchenServingTableFirstTileY = 3;
        public const int KitchenServingSlotCount = 3;
        // Runners wander this area (kitchen south corridor) while waiting for a fetch job
        public const int KitchenRunnerWanderMinTileX = 83;
        public const int KitchenRunnerWanderMinTileY = 6;
        public const int KitchenRunnerWanderMaxTileX = 88;
        public const int KitchenRunnerWanderMaxTileY = 8;
        // Cooks wander this area (around the ticket board and the first two stoves) between tickets
        public const int KitchenCookWanderMinTileX = 82;
        public const int KitchenCookWanderMinTileY = 2;
        public const int KitchenCookWanderMaxTileX = 84;
        public const int KitchenCookWanderMaxTileY = 3;
        public const int RunnerMaxStorageStops = 3;             // storages a runner tours in one ingredient trip
        public const int ServerOrderMemoryLimit = 3;            // orders a server can hold before posting at the board
        public const int ServerCarryDishLimit = 2;              // cooked dishes a server can carry at once
        public const float ServerBusPlateMaxWaitSeconds = 90f;  // fallback bussing only (no runner on shift): a plate waiting this long jumps ahead of deliveries/orders
        public const int RunnerCarryPlateLimit = 3;             // empty plates a runner carries to the sink in one trip
        public const float TicketBoardPauseSeconds = 1f;        // pause at the board to post/read a ticket
        public const int KitchenFridgeStackSize = 10;           // flat fridge stack size for every crop (issue #386)
        public const int KitchenPreStockStackSizeMin = 1;       // Pre-Stock Stack Size slider range
        public const int KitchenPreStockStackSizeMax = 4;
        public const int KitchenRunnerCarryCropTypes = 3;       // distinct crop types a runner can hold (one per hand slot)
        // Staff exits (issue #386): collision tiles only kitchen runners may pass through, so
        // crop runs skip the tavern's main entryway. Solid for everyone else.
        public const int KitchenRunnerStaffExitAX = 91;
        public const int KitchenRunnerStaffExitAY = 10;
        public const int KitchenRunnerStaffExitBX = 101;
        public const int KitchenRunnerStaffExitBY = 10;
        public const int KitchenRunnerCarryLevelMin = 1;        // global runner carry level range; raised by
        public const int KitchenRunnerCarryLevelMax = 3;        // one-of-a-kind items (future feature)

        /// <summary>Units of each crop type a runner can hold per trip at the given carry level (1→1, 2→5, 3→10).</summary>
        public static int GetRunnerCarryUnits(int carryLevel)
        {
            switch (carryLevel)
            {
                case 3:  return 10;
                case 2:  return 5;
                default: return 1;
            }
        }
        public const float KitchenPreStockCheckIntervalSeconds = 2f; // throttle for pre-stock deficit recompute
        public const int KitchenFridgeArtTopTileY = 1;          // fridge art spans (87,1)-(87,2); hover outline top
        public const float KitchenRunnerSprintMultiplier = 3f;  // runner speed multiplier while fetching ingredients
        public const float ServerWanderPauseSeconds = 2.5f;     // idle pause between server wander hops
        // A patron whose assigned seat still has an un-bussed plate waits here until it's cleared
        public const int TavernDoorWaitTileX = 100;
        public const int TavernDoorWaitTileY = 6;

        // Tavern dining area bounds (server zones and wandering)
        public const int TavernAreaMinTileX = 91;
        public const int TavernAreaMaxTileX = 99;
        public const int TavernTopZoneMinTileY = 2;             // top tables (93,3)/(97,3) and their seats
        public const int TavernTopZoneMaxTileY = 4;
        public const int TavernBottomZoneMinTileY = 5;          // bottom tables (93,7)/(97,7) and their seats
        public const int TavernBottomZoneMaxTileY = 8;
        public const float DishPriceMarkup = 1.25f;             // menu price = ingredient sell value x markup + effect premium
        // Effect premium (gold per buff point) so dishes with better effects always cost more
        public const int DishBuffStatGoldPerPoint = 15;         // ATK / DEF / AGI
        public const int DishBuffMagicGoldPerPoint = 10;        // MAG (magnitudes run higher than physical stats)
        public const int DishBuffEvasionGoldPerPoint = 3;       // EVA (magnitudes ~10)
        public const int DishBuffRegenGoldPerPoint = 30;        // HP / MP regen per round
        public const int DishPriceRoundTo = 5;                  // menu prices round to the nearest 5 gold
        public const int DishPriceMin = 10;                     // minimum menu price
        public const float CookSimpleBaseSeconds = 5f;          // cook time at CookingProficiency 1 by dish complexity
        public const float CookStandardBaseSeconds = 7f;
        public const float CookComplexBaseSeconds = 10f;
        public const float CookProficiencySpeedStep = 0.06f;    // cook duration reduced 6% per proficiency point above 1
        public const float CookDurationFloorSeconds = 5f;       // cook time never drops below 5 in-game minutes
        public const float DeluxeChancePerProficiency = 0.05f;  // deluxe-dish chance per CookingProficiency point
        public const float DeluxeChanceMax = 0.45f;
        public const float DeluxeMagnitudeMultiplier = 1.5f;    // deluxe party dishes get +50% buff magnitude (rounded up)
        public const float EatSnackSeconds = 5f;                // eat time by dish size
        public const float EatMealSeconds = 7f;
        public const float EatFeastSeconds = 10f;
        public const float PatronPatiencePreOrderSeconds = 600f;  // scaled seconds a patron waits for a server to take their order (10 min)
        public const float PatronPatiencePostOrderSeconds = 600f; // scaled seconds a patron waits for ordered food to arrive (10 min)
        public const float PatronLingerAfterEatingSeconds = 300f; // scaled seconds a patron sticks around after finishing their meal (5 min)
        public const float MealBuffDurationSeconds = 360f;          // scaled seconds a food buff lasts (6 in-game hours, issue #392)
        public const float PatronClosedKitchenPatienceFactor = 0.25f; // patience multiplier for patrons waiting to order while kitchen is closed (issue #392)
        public const float DishTipChance = 0.5f;                // chance an unhired merc tips on finishing a meal
        public const float DishTipMinPercent = 0.05f;           // tip is 5-15% of dish price, rounded up
        public const float DishTipMaxPercent = 0.15f;

        // Automated monster job assignment (issue #321, backpressure scaling issue #375)
        public const float AutoJobReassessIntervalSeconds = 15f;   // scaled seconds between solve/apply passes (15 in-game minutes)
        public const float AutoJobPressureSampleIntervalSeconds = 5f;  // scaled seconds between backpressure signal samples
        public const float AutoJobScaleDownDrainIntervalSeconds = 60f; // min scaled seconds between releasing successive workers per job (farming)
        public const float AutoJobKitchenScaleDownDrainIntervalSeconds = 360f; // kitchen drains slower — departures are highly visible in a service area
        public const float AutoJobPressureDecayAlpha = 0.15f;      // per-sample EMA decay on falling pressure (rising pressure is instant)
        public const float AutoJobKitchenHighWaitSeconds = 60f;    // a patron waiting this long (1 in-game hour) adds a worker of pressure
        public const int AutoJobFarmTasksPerWorker = 6;            // burst demand: outstanding farm tasks each farmer can absorb
        public const int AutoJobKitchenBaseStaff = 3;              // cook + server + runner minimum (no runner -> fridge runs dry)
        public const int AutoJobKitchenBacklogPerExtraWorker = 3;  // tickets/patrons per extra kitchen worker beyond base staff
        public const int AutoJobKitchenMaxWorkers = 8;             // mirrors KitchenTaskCoordinator.MaxWorkerPosts (3 cooks + 2 servers + 3 runners)
        public const float KitchenRoleMixDwellSeconds = 45f;       // min scaled seconds between demand-weighted role-mix recomputes (anti-thrash)
        public const float KitchenRolePressureSampleIntervalSeconds = 5f; // scaled seconds between per-role pressure samples
        public const float KitchenRolePressureEmaAlpha = 0.1f;     // per-sample EMA weight; ~50 scaled-sec time constant spans a full service cycle
        public const float KitchenRoleMixSwitchMargin = 1.5f;      // smoothed-pressure gap before an occupied post switches role; a lone ticket pulses each role's signal by 1, so noise can never flip a post

        // Camera Configuration
        public const float CameraDefaultZoom = 1f; // default zoom level
        public const float CameraMinimumZoom = 0.5f; // can't zoom out past default for normal maps
        public const float CameraMaximumZoom = 3f; // can zoom in really close
        public const float CameraHalfSizeWindowZoom = 1.00f; // default zoom applied automatically when switching to Half Size window (2 zoom-slider levels in from 1x)
        public const float CameraMinimumZoomLargeMap = 0.25f; // can zoom out to 0.5x for large maps (clean divisor)
        public const float CameraZoomSpeed = 0.001f; // zoom sensitivity per mouse wheel notch
        public const float CameraPanSpeed = 1f; // pan speed multiplier
        public const float CameraKeyboardPanSpeed = 300f; // starting screen pixels per second scrolled when an arrow/WASD key is first held
        public const float CameraKeyboardPanMaxSpeed = 1200f; // top screen pixels per second reached after holding pan keys continuously
        public const float CameraKeyboardPanAccelSeconds = 1.5f; // seconds of continuous key-hold to ramp pan speed from starting to top
        public const float CameraFollowLerpSpeed = 5f; // speed at which camera lerps to hero position
        public const float CameraManualControlTimeout = 7f; // seconds of inactivity before auto-following resumes (paused when player interacts with selectables)
        public const bool CameraAutoScrollToHeroDefault = false; // default value for auto-scroll to hero setting
        public const int MapQuadrantCount = 4; // horizontal map quadrants reachable via keys 1-4

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
        public const int MapCenterTileX = 34;
        public const int MapCenterTileY = 6;

        // Sensor radii (in pixels)
        public const float CenterRadiusPixels = 14f;

        // Adjacency ring radius in tiles (outside pit)
        public const int PitAdjacencyRadiusTiles = 2;

        // Pit collider padding (pixels around tile boundaries)
        public const int PitColliderPadding = 4;

        // Stuck detection: if hero/mercenary makes no movement progress for this many seconds, warp to destination
        public const float MovementStuckTimeoutSeconds = 5f;

        /// <summary>
        /// Safety timeout (seconds) waiting for all mercenaries to exit the pit before resetting it on hero death.
        /// </summary>
        public const float MercenaryExitPitTimeoutSeconds = 30f;

        // Jump movement configuration
        public const float JumpMovementSpeed = 4.0f; // tiles per second for pit jumping (faster than normal movement)

        // Colors
        public static readonly Color HeroColor = Color.Blue;
        public static readonly Color PitColor = Color.Red;
        public static readonly Color TownColor = Color.Green;
        public static readonly Color BackgroundColor = Color.Black;
        public static readonly Color TransparentMenu = new Color(255, 255, 255, 230);

        // Top-level UI vertical offsets (applied when window shrinks so text/buttons are not clipped at top)
        public const int TopUiYOffsetNormal = 0;
        public const int TopUiYOffsetHalf = 6;      // adjust as needed for Hud2x font height
        public const int TopUiYOffsetQuarter = 12;  // adjust as needed for Hud4x font height

        // Tags
        public const int TAG_TILEMAP = 1; // Tag for tilemap entities
        public const int TAG_HERO = 2; // Tag for hero entity
        public const int TAG_PIT = 3; // Tag for pit entity
        public const int TAG_OBSTACLE = 4; // Tag for obstacle entities
        public const int TAG_TREASURE = 5; // Tag for treasure entities
        public const int TAG_MONSTER = 6; // Tag for monster entities
        public const int TAG_WIZARD_ORB = 7; // Tag for wizard orb entity
        public const int TAG_MERCENARY = 8; // Tag for mercenary entities
        public const int TAG_HERO_STATUE = 9; // Tag for hero statue entity
        public const int TAG_INNKEEPER = 10; // Tag for innkeeper entity
        public const int TAG_TRAP = 11; // Tag for hidden trap entities

        // Trap configuration (Phase 6 — minimal trap system)
        public const int TrapMinPerFloor = 0; // Minimum traps spawned per pit floor
        public const int TrapMaxPerFloor = 2; // Maximum traps spawned per pit floor

        // New-game starting resources
        public const int NewGameStartingGold = 200; // Gold the player starts with in a new game
        public const int NewGameStartingHPPotions = 5; // HPPotions in the hero's bag at new game start
        public const int NewGameStartingMPPotions = 5; // MPPotions in the hero's bag at new game start
        public const int NewGameStartingWheatSeeds = 12; // Wheat seeds in the seed inventory at new game start
        public const int NewGameStartingTomatoSeeds = 6; // Tomato seeds in the seed inventory at new game start
        public const int NewGameStartingAppleTreeSeeds = 2; // Apple tree seeds in the seed inventory at new game start

        // New-game starting farm buildings (issue #316). Anchor tiles; footprints span
        // MonsterHouse tiles 121-125 x 0-4 and CropStorage tiles 126-128 x 0-3.
        public const int NewGameMonsterHouseAnchorTileX = 123;
        public const int NewGameMonsterHouseAnchorTileY = 2;
        public const int NewGameCropStorageAnchorTileX = 127;
        public const int NewGameCropStorageAnchorTileY = 2;

        // Starter farming Slime housed in the new-game Monster House (proficiencies 1-9)
        public const int NewGameStarterSlimeFarmingProficiency = 7;
        public const int NewGameStarterSlimeFishingProficiency = 3;
        public const int NewGameStarterSlimeCookingProficiency = 3;

        // Render Layers (the lower the number, the higher the layer)

        public const int RenderLayerLowest = 0; // Lowest possible layer
        public const int RenderLayerPickupItem = 1; // Pickup items layer
        public const int RenderLayerTop = 2;

        public const int RenderLayerActorPropOverlay = 41; // Actor prop overlay layer (above actors).  Useful for things like watering can, water, harvested crops that display over monster workers.
        // Actors layer — heroes, mercenaries, monsters, and treasures. Y-sorted within layer via LayerDepth.
        public const int RenderLayerActors = 60;
        public const int RenderLayerSingleTileObject = 61; // Single tile object layer (below actors, so single tile objects render below monsters/heroes)
        public const int RenderLayerDroppedItems = 65; // Dropped items layer
        // Fog of war renders BEHIND actors so the party is never partially hidden by adjacent fog (#337).
        // Pit statics/monsters under covered fog are hidden via FogHideableComponent / EnemyAnimationComponent instead.
        public const int RenderLayerFogOfWar = 70;
        // NOTE: Placed buildings (Monster House / Crop Storage) render at RenderLayerActors and are
        // Y-sorted with monsters via YSortSpriteRenderer.YSortOffset (see IYSortOffset / YSortManager).
        // Decorative tree bands north/south of the map (#348). Above the Base/Detail tilemap layers so the
        // fringe that spills over the map edge covers terrain, but behind fog and actors. Deliberately not
        // 60/61 so YSortManager ignores it — the bands are static and never sort.
        public const int RenderLayerTreeBand = 80;
        public const int RenderLayerDetail = 90; // Detail tilemap layer (tilled soil, etc.) — above base, below actors
        public const int RenderLayerBase = 100; // Background layer

        public const int RenderLayerActionQueue = 996; // Action queue layer (screen space, not affected by scene scaling)
        public const int RenderLayerGraphicalHUD = 997; // Graphical HUD layer (screen space, not affected by scene scaling)
        public const int RenderLayerUI = 998; // UI layer (always on top)
        public const int TransparentPauseOverlay = 999; // Transparent overlay for paused action when UI is active
        // Speech bubbles (screen space so they hold constant size at any camera zoom). Back-most of the
        // screen-space group: UI windows and the pause dim draw over them.
        public const int RenderLayerSpeechBubble = 1000;

        // Y-sort: LayerDepth = Mathf.Clamp01(1f - entity.Y * YSortDepthScale)
        // Higher world-Y (closer to camera) → smaller depth → drawn in front.
        public const float YSortDepthScale = 1f / 100000f;

        // Tree Bands (#348) — decorative deterministic tree fills above and below the map, painted
        // once into a RenderTexture at map load so zooming out never exposes empty space north/south.
        public const int TreeBandTopStartTileY = -10;   // first tile row of the top band (inclusive)
        public const int TreeBandTopEndTileY = -1;      // last tile row of the top band (inclusive)
        public const int TreeBandBottomStartTileY = 12; // first tile row of the bottom band (inclusive)
        public const int TreeBandBottomEndTileY = 21;   // last tile row of the bottom band (inclusive)
        public const int TreeBandSeed = 348;            // fixed seed — bands look identical every run
        public const int TreeBandGrassTileGid = 13;     // map tileset gid painted as the bands' grass backdrop
        public const int TreeBandMapOverlapPx = 16;     // px the top band's trunks may spill over the map edge
        public const int TreeBandCanopyPeekPx = 6;      // px the bottom band's canopy tops poke over the map's bottom edge
        public const int TreeBandBaseSpacingPx = 40;    // nominal horizontal step between trunks
        public const int TreeBandSpacingJitterPx = 18;  // +/- horizontal jitter added to the step
        public const int TreeBandRowYJitterPx = 10;     // +/- vertical jitter applied per tree
        public const int TreeBandRowXOffsetPx = 20;     // +/- per-row horizontal stagger so rows do not line up
        public const float TreeBandTree2Chance = 0.45f; // probability a tree uses the taller Tree2 sprite
        public const float TreeBandFlipChance = 0.5f;   // probability a tree is mirrored horizontally

        // Speech bubbles
        public const int SpeechBubbleWidth = 128;          // screen px (design units)
        public const int SpeechBubblePadding = 4;          // inner text padding, all sides
        public const int SpeechBubbleTailOverlap = 2;      // tail top overlaps bottom N rows of bubble
        public const int SpeechBubbleTailTipOffsetY = -36; // tail bottom Y rel. to entity origin (head top -32, +4 clearance)
        public const float SpeechBubbleCharsPerSecond = 20f;
        public const float SpeechBubbleLingerSeconds = 2f;
        public const string FontPathSpeechBubble = FontMainUI; // Express, lineHeight 9
        // Pre-scaled 2x Express (lineHeight 18) — speech bubbles use it in half-size window mode so
        // text reads at the same physical size as the normal window.
        public const string FontPathSpeechBubble2x = "Content/Fonts/Express2x.fnt";
        // Bubble height is derived per mode: padding*2 + visible lines * font line height. Text that
        // wraps to more lines scrolls up a line at a time as the typewriter reveal fills each line.
        public const int SpeechBubbleVisibleLinesNormal = 3;
        public const int SpeechBubbleVisibleLinesHalfWindow = 2;

        // Font paths
        public const string FontMainUI = "Content/Fonts/Express.fnt";
        public const string FontPathHud = "Content/Fonts/Skullboy.fnt";
        public const string FontPathHud2x = "Content/Fonts/Skullboy2x.fnt";
        public const string FontPathHudSmall = "Content/Fonts/SkullboySmall.fnt";

        // UI Button Spacing
        public const float UIButtonPadding = 4f; // Padding between UI buttons

        // UI bar auto-hide
        public const float UIBarAutoHideDelay = 5f;   // Seconds of idle before the UI bar auto-hides
        public const float UIBarSlideSpeed = 500f;    // Stage pixels per second for the slide animation
        public const float UIBarHideOffset = 54f;     // Stage pixels the bar slides up when hidden
        public const float UIBarProximityY = 48f;     // Mouse Y <= this (stage coords) triggers proximity-unhide

        // Second Chance Shop layout positions
        // Composed for a 1920-wide stage; SecondChanceShopUI centers the whole composition on
        // wider stages by shifting all X positions right by (stageWidth - VirtualWidth) / 2.
        // Shop window (vault grid + tabs) positioned near left-center
        public const float SecondChanceShopWindowX = 573f;
        public const float SecondChanceShopWindowY = 12f;
        public const float SecondChanceShopWindowWidth = 350f;
        public const float SecondChanceShopWindowHeight = 310f;

        // Hero panel (inventory/crystal) positioned to fill right side of screen
        public const float SecondChanceHeroPanelX = 1200f;
        public const float SecondChanceHeroPanelY = 12f;
        public const float SecondChanceHeroPanelWidth = 720f;
        public const float SecondChanceHeroPanelHeight = 340f;

        // Merchant sprite positioned between shop window and hero panel
        // Sprite is 256x256; Y=50 places it within the 360px stage height (50 to 306)
        public const float SecondChanceMerchantSpriteX = 935f;
        public const float SecondChanceMerchantSpriteY = 50f;

        // Merchant greeting bubble tail-tip anchor, relative to the sprite's top-left (issue #385).
        // X centers on the 256px art; Y sits at the top of the merchant's head within the frame.
        public const float SecondChanceMerchantBubbleAnchorX = 128f;
        public const float SecondChanceMerchantBubbleAnchorY = 40f;

        // Inventory interaction
        /// <summary>Minimum pixel movement to initiate a drag operation.</summary>
        public const float DragThresholdPixels = 4f;

        // Physics Layers (determines which layer an entity is on for collision)
        public const int PhysicsTileMapLayer = 0;   // Tilemap "Collision" layer
        public const int PhysicsHeroWorldLayer = 1; // Hero layer for collision
        public const int PhysicsPitLayer = 2;       // Pit trigger layer
        public const int PhysicsMercenaryLayer = 3; // Mercenary layer for collision

        // Entity names
        public const string EntityHero = "Hero";

        // Treasure Colors
        public static readonly Color TREASURE_SHADE_1 = new Color(140, 91, 62); //Brown
        public static readonly Color TREASURE_SHADE_2 = new Color(44, 94, 26);  //Green
        public static readonly Color TREASURE_SHADE_3 = new Color(43, 78, 149); //Blue
        public static readonly Color TREASURE_SHADE_4 = new Color(144, 82, 188); //Purple
        public static readonly Color TREASURE_SHADE_5 = new Color(203, 129, 22); //Gold

        // Item Rarity Colors (matching treasure colors)
        public static readonly Color RARITY_NORMAL = Color.White;               // White
        public static readonly Color RARITY_UNCOMMON = new Color(44, 94, 26);   // Green (matches TREASURE_SHADE_2)
        public static readonly Color RARITY_RARE = new Color(43, 78, 149);      // Blue (matches TREASURE_SHADE_3)
        public static readonly Color RARITY_EPIC = new Color(144, 82, 188);     // Purple (matches TREASURE_SHADE_4)
        public static readonly Color RARITY_LEGENDARY = new Color(203, 129, 22); // Gold (matches TREASURE_SHADE_5)

        // Event Console Name Colors
        public static readonly Color ConsoleColorHeroName = new Color(100, 180, 255);  // Light blue — heroes and mercenaries
        public static readonly Color ConsoleColorEnemyName = new Color(220, 80, 80);   // Red — enemy names

        //Hero Paperdoll Colors
        public static readonly Color SKIN_SHADE_1 = new Color(251, 200, 178); //Applies to body and hands
        public static readonly Color SKIN_SHADE_2 = new Color(140, 91, 62);
        public static readonly Color SKIN_SHADE_3 = new Color(89, 207, 147);
        public static readonly Color SKIN_SHADE_4 = new Color(138, 161, 246);
        public static readonly Color SKIN_SHADE_5 = new Color(248, 197, 58);

        public static readonly Color HAIR_SHADE_1 = new Color(20, 80, 201); //Applies to hair
        public static readonly Color HAIR_SHADE_2 = new Color(176, 18, 10);
        public static readonly Color HAIR_SHADE_3 = new Color(142, 36, 170);
        public static readonly Color HAIR_SHADE_4 = new Color(0, 188, 212);
        public static readonly Color HAIR_SHADE_5 = new Color(230, 74, 25);
        public static readonly Color HAIR_SHADE_6 = new Color(255, 238, 88);
        public static readonly Color HAIR_SHADE_7 = new Color(94, 51, 43);
        public static readonly Color HAIR_SHADE_8 = new Color(50, 50, 50);

        public static readonly Color SHIRT_SHADE_1 = new Color(173, 184, 52);  //Applies to shirt
        public static readonly Color SHIRT_SHADE_2 = new Color(81, 130, 255);
        public static readonly Color SHIRT_SHADE_3 = new Color(219, 65, 97);
        public static readonly Color SHIRT_SHADE_4 = new Color(250, 180, 11);
        public static readonly Color SHIRT_SHADE_5 = new Color(20, 128, 126);

        public static readonly List<Color> SkinColors = new List<Color>
        {
            SKIN_SHADE_1,
            SKIN_SHADE_2,
            SKIN_SHADE_3,
            SKIN_SHADE_4,
            SKIN_SHADE_5
        };

        public static readonly List<Color> HairColors = new List<Color>
        {
            HAIR_SHADE_1,
            HAIR_SHADE_2,
            HAIR_SHADE_3,
            HAIR_SHADE_4,
            HAIR_SHADE_5,
            HAIR_SHADE_6,
            HAIR_SHADE_7,
            HAIR_SHADE_8
        };

        public static readonly List<Color> ShirtColors = new List<Color>
        {
            SHIRT_SHADE_1,
            SHIRT_SHADE_2,
            SHIRT_SHADE_3,
            SHIRT_SHADE_4,
            SHIRT_SHADE_5
        };
    }
}