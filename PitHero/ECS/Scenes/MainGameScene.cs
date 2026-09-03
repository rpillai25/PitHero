using System;
using Microsoft.Xna.Framework;
using Nez;
using Nez.BitmapFonts;
using Nez.Sprites;
using Nez.Textures;
using Nez.Tiled;
using Nez.UI; // Added for Label
using PitHero.AI;
using PitHero.AI.Interfaces;
using PitHero.Dining;
using PitHero.ECS.Components;
using PitHero.Services;
using PitHero.Rendering;
using PitHero.UI;
using PitHero.Util;
using RolePlayingFramework.AlliedMonsters;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Jobs;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Stats;

namespace PitHero.ECS.Scenes
{
    public class MainGameScene : Scene
    {
        private SettingsUI _settingsUI;
        private string _mapPath;
        private bool _isInitializationComplete;
        private CameraControllerComponent _cameraController;
        private TmxMap _tmxMap; // Store reference to the map
        private Entity _pauseOverlayEntity; // Pause overlay entity
        private PrototypeSpriteRenderer _pauseOverlayRenderer; // resized when the stage size changes
        private float _lastStageWidth;  // stage-size-change detection for right/center-anchored HUD
        private float _lastStageHeight;
        private Label _pitLevelLabel; // UI label showing pit level
        private Label _fundsLabel; // UI label showing total funds
        private Label _clockLabel; // UI label showing in-game time
        private int _lastDisplayedPitLevel = -1; // Track last displayed level to avoid string churn
        private int _lastDisplayedPitTier = -1; // Track last displayed tier to avoid string churn
        private int _lastDisplayedFunds = -1; // Track last displayed funds to avoid string churn
        private ShortcutBar _shortcutBar; // Shortcut bar displayed at bottom center
        private GraphicalHUD _graphicalHUD; // Graphical HUD component for HP/MP/Level display
        private GraphicalHUD _mercenary1HUD; // Graphical HUD for mercenary #1
        private GraphicalHUD _mercenary2HUD; // Graphical HUD for mercenary #2
        private ActionQueueVisualizationComponent _heroActionQueueViz; // Screen-space action queue viz for hero
        private ActionQueueVisualizationComponent _merc1ActionQueueViz; // Screen-space action queue viz for mercenary #1
        private ActionQueueVisualizationComponent _merc2ActionQueueViz; // Screen-space action queue viz for mercenary #2
        private MercenaryHireDialog _mercenaryHireDialog; // Dialog for hiring mercenaries
        private Entity _hoveredMercenary; // Currently hovered mercenary
        private Entity _mercenarySelectBoxEntity; // Entity for rendering SelectBox over hovered mercenary
        private Entity _mercenaryNameLabelEntity; // Entity for rendering name above hovered mercenary
        private Services.HeroPromotionService _heroPromotionService; // Manages hero crystal promotion after death
        private SimulationClock _simulationClock; // Session tick counter; advanced last in every Update (replay system)

        /// <summary>Building placement overlay (player command handlers apply placements/moves through it).</summary>
        public BuildingModeOverlay BuildingModeOverlay => _buildingModeOverlay;
        /// <summary>Seed planting overlay (command handlers apply crop plans through it).</summary>
        public SeedPlantingModeOverlay SeedModeOverlay => _seedModeOverlay;
        /// <summary>Till overlay (command handlers apply till marks through it).</summary>
        public TillModeOverlay TillModeOverlay => _tillModeOverlay;
        /// <summary>Harvested-crops storage viewer (command handlers apply storage sales/moves through it).</summary>
        public HarvestedCropsModeOverlay HarvestedCropsOverlay => _harvestedCropsModeOverlay;
        /// <summary>Refrigerator window (command handlers apply fridge returns/sales through it).</summary>
        public RefrigeratorDialog RefrigeratorDialog => _refrigeratorDialog;
        /// <summary>Add-monster dialog (command handlers apply monster purchases through it).</summary>
        public AddMonsterDialog AddMonsterDialog => _addMonsterDialog;
        private Services.Replay.PlayerCommandService _playerCommands; // Player input -> simulation doorway (replay system)
        private Services.NewGameIntroService _newGameIntroService; // Scripted new-game opening at the hero statue (issue #396)
        private EventConsolePanel _eventConsolePanel; // MMO-style event log panel in the lower-right corner
        private Rendering.ColorGradingController _colorGrading;
        private Rendering.CloudOverlayController _cloudOverlay;
        private Entity _cloudOverlayEntity;
        private TillModeOverlay _tillModeOverlay;
        private Label _tillingLabel;
        private Label _restoringGrassLabel;
        private bool _wasInTillMode;
        private BuildingModeOverlay _buildingModeOverlay;
        private bool _wasInBuildingMode;
        private bool _wasInFarmMode;
        private bool _savedFarmAutoScroll;
        private bool _farmModeRestoreHalfZoom;
        private SeedPlantingModeOverlay _seedModeOverlay;
        private bool _wasInSeedMode;
        private bool _wasInRemoveCropsMode;
        private HarvestedCropsModeOverlay _harvestedCropsModeOverlay;
        private UI.RestoreGrassModeOverlay _restoreGrassModeOverlay;
        private bool _wasInRestoreGrassMode;
        private RefrigeratorDialog _refrigeratorDialog; // Fridge inventory window (issue #386)
        private bool _wasFridgeDialogVisible;
        private bool _fridgeRestoreHalfZoom;
        private bool _wasInHarvestedCropsMode;
        private BuildingContextMenu _buildingContextMenu; // Popup shown when a placed building is clicked
        private bool _wasBuildingMenuVisible;
        private bool _buildingMenuRestoreHalfZoom;
        private AddMonsterDialog _addMonsterDialog; // Dialog for manually adding monsters to a house (issue #283)
        private Services.PlacedBuilding _hoveredBuilding; // Building currently under the cursor (hover outline)
        private Entity _buildingHoverOutlineEntity; // Entity rendering the white hover outline
        private Label _plantingCropsLabel;
        private Nez.UI.Stage _uiStage;
        private int _lastInGameHour = -1;

        // HUD fonts for different shrink levels
        public BitmapFont _hudFontNormal;
        public BitmapFont _hudFontHalf;
        private LabelStyle _pitLevelStyleNormal;
        private LabelStyle _pitLevelStyleHalf;
        private LabelStyle _modeStyleNormal;
        private LabelStyle _modeStyleHalf;
        private enum HudMode { Normal, Half }
        private HudMode _currentHudMode = HudMode.Normal;

        // Cached base positions for corner-anchored UI (so offsets are relative and centralized).
        // The hero/mercenary HUD panels sit bottom-left and the Pit Lv / Gold labels top-left; those
        // two swapped places, so the HUD's Y is now derived from the stage height and must be
        // re-applied whenever the stage resizes.
        private const float PitLabelBaseX = 10f; // X position for Pit Lv label (top-left)
        private const float PitLabelBaseY = 10f; // Y position for the Pit Lv / Gold labels (top-left)
        private const float FundsLabelGapX = 16f; // Gap between the measured Pit Lv label text and the Funds label
        private const float ClockLabelRightPadding = 32f; // Pixels from right edge for clock label
        private const float ClockLabelBaseY = 16f; // Y position for clock label (top area, offset to avoid cutoff)
        private const float GraphicalHudBaseX = 10f; // Base X position for graphical HUD (shifted left to fill space)
        private const float GraphicalHudHeight = 32f; // Height of the HUD template sprite (UI.atlas HudTemplate)
        private const float GraphicalHudBottomMargin = 8f; // Gap between the HUD panels and the stage bottom
        private const float GraphicalHudHalfModeXOffset = 0f; // No additional X offset needed since Pit Lv is at bottom
        private const float GraphicalHudSpacing = 170f; // Spacing between HUD elements (hero to merc1, merc1 to merc2)
        // Anchor for the battle action visualization, relative to a HUD panel's top-left. This is the
        // HUD head sprite, which GraphicalHUD draws at (LEVEL_TEXT_X_OFFSET - 7, LEVEL_TEXT_Y_OFFSET
        // - 15) = (64, -2). The ACTIVE action shows here and rises out of it.
        private const float HudQueueXOffset = 64f;
        private const float HudQueueYOffset = -2f;
        // The WAITING queue is shifted left from that anchor to sit over the HP bar, so it never
        // covers the active action rising off the head. Lands a 32px sprite centred on the 51px bar
        // (HP_UNIT_X_OFFSET 3 + (51 - 32) / 2 = 12, i.e. 12 - 64 from the head).
        private const float HudQueuedActionXOffset = -52f;
        // ...and raised a full sprite height so the waiting stack sits just ABOVE the panel
        // instead of on top of it — the first queued sprite's bottom lands 2px over the panel top,
        // leaving the HP bar and HP text fully readable during battle.
        private const float HudQueuedActionYOffset = -32f;

        // Party-visibility auto-hide. The panels describe the party, so they only hold screen space
        // while somebody in the party is on camera; otherwise they slide down out of the bottom edge
        // and slide back up when the party returns to view.
        private const float HudAutoHideDuration = 0.18f;  // seconds for a full hide or show sweep
        private const float HudAutoHideMargin = 16f;      // world-pixel hysteresis band on the camera edge
        private const float HudAutoHideClearance = 4f;    // extra travel so the panels fully clear the edge
        private float _hudSlideT;                         // 0 = fully up, 1 = fully parked off the bottom
        private float _appliedHudSlideT = -1f;            // last value pushed into the entity positions
        private bool _hudPartyVisible = true;             // hysteresis state for IsPartyInCameraView

        public BitmapFont HudFont; // legacy reference (normal)

        public BitmapFont GetHudFontForCurrentMode()
        {
            return _currentHudMode switch
            {
                HudMode.Normal => _hudFontNormal,
                HudMode.Half => _hudFontHalf,
                _ => _hudFontNormal
            };
        }

        public MainGameScene() : this("Content/Tilemaps/PitHero.tmx") { }
        public MainGameScene(string mapPath) { _mapPath = mapPath; }

        public override void Initialize()
        {
            base.Initialize();

            // Grant new-game starting gold before session-start analytics so the event records it.
            // Loads skip this — SaveLoadService.ApplyLoadedState already restored Funds.
            if (SaveLoadService.PendingLoadData == null)
            {
                var newGameState = Core.Services.GetService<GameStateService>();
                if (newGameState != null)
                    newGameState.Funds = GameConfig.NewGameStartingGold;
            }

            // Log session start before any scene setup so it is the first analytics event
            // (pit generation and mercenary spawns fire during initialization below).
            // Funds are already restored by SaveLoadService.ApplyLoadedState at load time.
            Services.Analytics.AnalyticsService.LogSessionStart(
                SaveLoadService.PendingLoadData != null ? "load" : "new_game",
                Core.Services.GetService<GameStateService>()?.Funds ?? 0);

            // FixedHeight locks the vertical resolution at VirtualHeight and expands the render
            // target width to match the window aspect, so ultrawide monitors see more world.
            SetDesignResolution(GameConfig.VirtualWidth, GameConfig.VirtualHeight, SceneResolutionPolicy.FixedHeight);
            ClearColor = Color.Transparent;

            var cameraEntity = CreateEntity("camera-controller");
            cameraEntity.AddComponent(Camera);
            _cameraController = cameraEntity.AddComponent(new CameraControllerComponent());
            // Delegate reads _uiStage at call time, so it is safe to wire before the stage exists
            _cameraController.IsPointerOverUI = () => _uiStage != null && _uiStage.Hit(_uiStage.GetMousePosition()) != null;
            _cameraController.HasKeyboardFocus = () => _uiStage != null && _uiStage.GetKeyboardFocus() != null;

            // Load HUD fonts (normal, 2x, 4x for shrink levels)
            _hudFontNormal = Content.LoadBitmapFont(GameConfig.FontPathHud);
            // New enlarged fonts for smaller window modes
            _hudFontHalf = Content.LoadBitmapFont(GameConfig.FontPathHud2x);
            HudFont = _hudFontNormal; // maintain old field

            // Pre-create label styles to avoid per-frame allocations
            _pitLevelStyleNormal = new LabelStyle(_hudFontNormal, Color.White);
            _pitLevelStyleHalf = new LabelStyle(_hudFontHalf, Color.White);
            _modeStyleNormal = new LabelStyle(_hudFontNormal, Color.White);
            _modeStyleHalf = new LabelStyle(_hudFontHalf, Color.White);



            // Register game event service so systems can broadcast events to the event console.
            Core.Services.AddService(new Services.GameEventService(Core.Services.GetService<TextService>()));

            // Register crystal collection service before UI is built so CrystalsTab can
            // resolve it via Core.Services.GetService<CrystalCollectionService>() during Initialize.
            Core.Services.AddService(new Services.CrystalCollectionService());

            // Register the loot shuffle bags (#382) before any pit generation so chest rolls
            // route through session-persistent bags. Transient by design — never saved.
            Core.Services.AddService(new Services.LootShuffleService());

            // Register building service so farm building placement and counts are queryable.
            Core.Services.AddService(new Services.BuildingService());

            // Register crop planting service so plan tracking and seed inventory are queryable.
            Core.Services.AddService(new Services.CropPlantingService());

            // Register harvested-crop storage so workers can deposit crops and the UI can view them.
            Core.Services.AddService(new Services.CropStorageInventoryService(
                Core.Services.GetService<Services.BuildingService>()));

            // Register dropped-crop tracking so unstorable crops fall to the ground for later pickup.
            var droppedCropService = new Services.DroppedCropService();
            droppedCropService.SetScene(this);
            Core.Services.AddService(droppedCropService);

            // Register dish entity service so kitchen workers can spawn dish sprites on tables/stoves.
            var dishEntityService = new Services.DishEntityService();
            dishEntityService.SetScene(this);
            Core.Services.AddService(dishEntityService);

            // Register kitchen hat service: pre-created job hats worn while doing kitchen work.
            var kitchenHatService = new Services.KitchenHatService();
            kitchenHatService.SetScene(this);
            Core.Services.AddService(kitchenHatService);

            AddSceneComponent<YSortManager>();

            SetupUIOverlay();
        }

        /// <summary>
        /// Removes scene-specific services and unloads the cached TiledMap so a new
        /// MainGameScene can register them again with a fresh map from disk.
        /// </summary>
        public override void Unload()
        {
            // Unsubscribe the shortcut bar from static drag events. Without this, the dead
            // scene's bar keeps intercepting skill drops in the next game session (its stale
            // handler runs first and cancels the drag before the new bar can handle it).
            _shortcutBar?.DisconnectFromStaticEvents();
            Core.Services.RemoveService(typeof(Rendering.ColorGradingController));
            _colorGrading?.Dispose();
            _colorGrading = null;
            _cloudOverlay?.Dispose();
            _cloudOverlay = null;
            _eventConsolePanel?.Dispose();
            Core.Content.UnloadAsset<TmxMap>(_mapPath);
            Core.Services.RemoveService(typeof(Services.GameEventService));
            Core.Services.RemoveService(typeof(Services.CrystalCollectionService));
            Core.Services.RemoveService(typeof(Services.LootShuffleService));
            Core.Services.RemoveService(typeof(Services.BuildingService));
            Core.Services.RemoveService(typeof(Services.CropPlantingService));
            Core.Services.RemoveService(typeof(Services.CropStorageInventoryService));
            Core.Services.RemoveService(typeof(Services.DroppedCropService));
            Core.Services.RemoveService(typeof(Services.TilledTileService));
            Core.Services.RemoveService(typeof(Services.WetTileService));
            Core.Services.RemoveService(typeof(Services.CropGrowthService));
            Core.Services.RemoveService(typeof(Services.AutoSeedPurchaseService));
            Core.Services.RemoveService(typeof(Services.AutoCropSellService));
            Core.Services.RemoveService(typeof(Services.AutoSellExcessItemsService));
            Core.Services.RemoveService(typeof(Services.AutoItemPurchaseService));
            Core.Services.RemoveService(typeof(Services.AutoHireMercenaryService));
            Core.Services.RemoveService(typeof(Services.AutoLearnSkillsService));
            Core.Services.GetService<Services.FarmTaskCoordinator>()?.Detach();
            Core.Services.RemoveService(typeof(Services.FarmTaskCoordinator));
            Core.Services.RemoveService(typeof(Services.MealBuffService));
            Core.Services.RemoveService(typeof(Services.FridgeInventoryService));
            Core.Services.GetService<Services.KitchenTaskCoordinator>()?.Detach();
            Core.Services.RemoveService(typeof(Services.KitchenTaskCoordinator));
            Core.Services.RemoveService(typeof(Services.PartyDiningService));
            Core.Services.RemoveService(typeof(Services.AutoJobAssignmentService));
            Core.Services.RemoveService(typeof(Services.DishEntityService));
            Core.Services.RemoveService(typeof(Services.KitchenHatService));
            Core.Services.RemoveService(typeof(MercenaryManager));
            Core.Services.RemoveService(typeof(AlliedMonsterManager));
            Core.Services.RemoveService(typeof(HeroPromotionService));
            Core.Services.RemoveService(typeof(PlayerInteractionService));
            Core.Services.RemoveService(typeof(TiledMapService));
            Core.Services.RemoveService(typeof(PitWidthManager));
            Core.Services.RemoveService(typeof(ShortcutBarService));
            Core.Services.RemoveService(typeof(SettingsUI));
            _simulationClock?.Detach();
            Core.Services.RemoveService(typeof(SimulationClock));
            _playerCommands?.Detach();
            Core.Services.RemoveService(typeof(Services.Replay.PlayerCommandService));
            // A new scene always starts unpaused; pending pause commands die with this scene
            Core.Services.GetService<PauseService>()?.ResetImmediate();
        }

        public override void Begin()
        {
            base.Begin();
            if (_isInitializationComplete)
                return;

            // Captured up front: ApplyPendingLoadData() below clears PendingLoadData
            bool isNewGame = SaveLoadService.PendingLoadData == null;

            // ── Deterministic session seed (replay system) ───────────────────────────────
            // Must run before ANY world content is generated (SpawnPit/SetPitLevel draw RNG).
            // A replay bootstrap supplies the recorded seed; otherwise a fresh one is generated.
            var replayBootstrap = Services.Replay.ReplaySessionBootstrap.Consume();
            int masterSeed = replayBootstrap != null ? replayBootstrap.MasterSeed : GameRandom.GenerateMasterSeed();
            GameRandom.InitializeSession(masterSeed);
            Core.Services.GetService<HairstyleQueueService>()?.ResetAndRefill();
            SpeechBubbleDialogue.Reseed(masterSeed ^ GameConfig.ReplaySpeechSeedSalt);
            Core.Services.GetService<Services.LootShuffleService>()?.SetEpicRng(GameRandom.Loot);
            _simulationClock = new SimulationClock();
            Core.Services.AddService(_simulationClock);
            _playerCommands = new Services.Replay.PlayerCommandService();
            Core.Services.AddService(_playerCommands);
            Debug.Log($"[MainGameScene] Session master seed {masterSeed}");

            LoadMap();
            SpawnPit();

            // Only generate the default pit level when there is no save to load.
            // When a save exists, ApplyPendingLoadData will call SetPitLevel with the
            // saved level.  Generating here first would create deferred entities that
            // ClearExistingPitEntities cannot find, producing a conflicting dual-state pit.
            var pitWidthManager = Core.Services.GetService<PitWidthManager>();
            if (pitWidthManager != null && isNewGame)
            {
                // New game — ensure tier state is clean before setting the initial level.
                pitWidthManager.SetPitTier(1);
                pitWidthManager.SetTierBaseLevel(1);
                pitWidthManager.SetPitLevel(1);
            }

            var hero = SpawnHero(isNewGame);
            SpawnHeroStatue();
            SpawnInnkeeper();

            // Connect shortcut bar to hero
            ConnectShortcutBarToHero();

            // Initialize mercenary manager
            var mercenaryManager = new MercenaryManager();
            Core.Services.AddService(mercenaryManager);
            mercenaryManager.Initialize(this);

            // Initialize allied monster manager
            var alliedMonsterManager = new AlliedMonsterManager();
            Core.Services.AddService(alliedMonsterManager);

            // Initialize farm task coordinator (central till/plant/water queue + farming monster lifecycle)
            var farmTaskCoordinator = new Services.FarmTaskCoordinator(
                Core.Services.GetService<TileStateService>(),
                Core.Services.GetService<Services.BuildingService>(),
                _tmxMap.Width, _tmxMap.Height, alliedMonsterManager,
                Core.Services.GetService<Services.TilledTileService>(),
                _tmxMap.GetLayer<Nez.Tiled.TmxLayer>("Collision"));
            farmTaskCoordinator.SetDroppedCropService(Core.Services.GetService<Services.DroppedCropService>());
            farmTaskCoordinator.Initialize(this);
            Core.Services.AddService(farmTaskCoordinator);

            // Auto seed purchase service buys seeds automatically to fulfil unplanted plans.
            // Registered after the coordinator so its rescan hook is wired up.
            var autoSeedPurchaseService = new Services.AutoSeedPurchaseService(
                Core.Services.GetService<Services.CropPlantingService>(),
                Core.Services.GetService<Services.CropGrowthService>(),
                Core.Services.GetService<Services.GameStateService>(),
                farmTaskCoordinator);
            Core.Services.AddService(autoSeedPurchaseService);

            // Auto crop sell service sells designated crop stacks once they reach max stack size.
            var autoCropSellService = new Services.AutoCropSellService(
                Core.Services.GetService<Services.CropStorageInventoryService>(),
                Core.Services.GetService<Services.GameStateService>());
            Core.Services.AddService(autoCropSellService);

            // Auto-sell excess items service frees a bag slot when a chest item arrives and the bag is full
            Core.Services.AddService(new Services.AutoSellExcessItemsService());

            // Auto item purchase service buys gear/consumables back from the Second Chance shop before
            // the party jumps into the pit. Registered after AutoSeedPurchaseService, which owns the
            // shared Gold Buffer setting it reads (issue #345).
            Core.Services.AddService(new Services.AutoItemPurchaseService(
                Core.Services.GetService<Services.GameStateService>(),
                Core.Services.GetService<Services.SecondChanceMerchantVault>(),
                autoSeedPurchaseService));

            // Auto-hire mercenary service hires tavern mercenaries matching the configured job slots.
            // Registered after AutoSeedPurchaseService, which owns the shared Gold Buffer setting it
            // reads (issue #350).
            Core.Services.AddService(new Services.AutoHireMercenaryService(
                Core.Services.GetService<Services.GameStateService>(),
                autoSeedPurchaseService,
                mercenaryManager));

            // Auto-learn skills service spends hero JP automatically (issue #353).
            // Registered after AutoHireMercenaryService to keep service order consistent.
            Core.Services.AddService(new Services.AutoLearnSkillsService());

            // Meal buff service holds each party member's day-long food buffs (issue #319)
            Core.Services.AddService(new Services.MealBuffService());

            // Refrigerator inventory (issue #386) — registered before the kitchen coordinator,
            // which resolves it in EnsureServices
            Core.Services.AddService(new Services.FridgeInventoryService());

            // Kitchen task coordinator manages cook/server/runner workers and the ticket queue (issue #319)
            var kitchenCoordinator = new Services.KitchenTaskCoordinator(
                Core.Services.GetService<AlliedMonsterManager>(),
                Core.Services.GetService<Services.BuildingService>(),
                _tmxMap.Width, _tmxMap.Height,
                _tmxMap.GetLayer<Nez.Tiled.TmxLayer>("Collision"));
            kitchenCoordinator.Initialize(this);
            Core.Services.AddService(kitchenCoordinator);

            // Peer the worker coordinators: on a job change, the monster's old-job entity must
            // walk home and despawn before the new job's entity spawns (one entity per monster).
            farmTaskCoordinator.AddPeer(kitchenCoordinator);
            kitchenCoordinator.AddPeer(farmTaskCoordinator);

            // Party dining service orchestrates once-a-day tavern meals for the party (issue #319)
            var partyDiningService = new Services.PartyDiningService();
            kitchenCoordinator.SetPartyOrderSource(partyDiningService);
            Core.Services.AddService(partyDiningService);

            // Auto job assignment service reassigns monster jobs from workload demand (issue #321)
            var autoJobAssignmentService = new Services.AutoJobAssignmentService(
                alliedMonsterManager,
                new Services.AutoJob.KitchenJobDemandEvaluator(kitchenCoordinator,
                    mercenaryManager, partyDiningService),
                new Services.AutoJob.FarmingJobDemandEvaluator(farmTaskCoordinator,
                    Core.Services.GetService<Services.CropGrowthService>(),
                    Core.Services.GetService<Services.CropPlantingService>()));
            Core.Services.AddService(autoJobAssignmentService);

            // Initialize hero promotion service (handles mercenary promotions and hero crystal ceremonies after death)
            _heroPromotionService = new Services.HeroPromotionService(this);
            Core.Services.AddService(_heroPromotionService);

            // Initialize player interaction service for camera control
            var playerInteractionService = new PlayerInteractionService();
            Core.Services.AddService(playerInteractionService);
            Debug.Log("[MainGameScene] PlayerInteractionService initialized");

            // Apply pending load data if available
            ApplyPendingLoadData();

            EmitWelcomeMessage();

            // Clear any UI window count leaked from the previous scene (windows destroyed by a
            // scene swap never run their close path) and re-apply the persistent window size —
            // otherwise the deferred size restore never fires again after loading a save.
            UI.UIWindowManager.ResetForNewScene();

            // A brand-new game opens with the hero dropping in at the statue (issue #396). Started
            // last so the window size above is settled and every UI element exists.
            if (isNewGame)
                StartNewGameIntro(hero);

            _isInitializationComplete = true;
        }

        /// <summary>Spawns the scripted new-game farm content: Monster House + Crop Storage + starter farming Slime (issue #316).</summary>
        private void SetupNewGameFarmContent()
        {
            var buildingService = Core.Services.GetService<Services.BuildingService>();
            if (buildingService == null || _buildingModeOverlay == null)
                return;

            var houseId = buildingService.AllocateId();
            _buildingModeOverlay.SpawnRestoredBuilding(Util.BuildingType.MonsterHouse,
                GameConfig.NewGameMonsterHouseAnchorTileX, GameConfig.NewGameMonsterHouseAnchorTileY, houseId);

            var storageId = buildingService.AllocateId();
            _buildingModeOverlay.SpawnRestoredBuilding(Util.BuildingType.CropStorage,
                GameConfig.NewGameCropStorageAnchorTileX, GameConfig.NewGameCropStorageAnchorTileY, storageId);

            var alliedManager = Core.Services.GetService<AlliedMonsterManager>();
            if (alliedManager != null)
            {
                var starter = new AlliedMonster(NameGenerator.GenerateMonsterName(), MonsterTextKey.Monster_Slime,
                    GameConfig.NewGameStarterSlimeFishingProficiency,
                    GameConfig.NewGameStarterSlimeCookingProficiency,
                    GameConfig.NewGameStarterSlimeFarmingProficiency,
                    houseId);
                // Scripted pre-assignment unique to this starter monster; recruits/purchases stay Job=None
                starter.Job = MonsterJob.Farming;
                alliedManager.AddAlliedMonster(starter);
                // A housed monster implies its type was defeated (issue #283 invariant)
                Core.Services.GetService<DefeatedMonsterService>()?.MarkDefeatedByTypeName(MonsterTextKey.Monster_Slime);
            }
        }

        /// <summary>Applies pending save data to restore game state after scene initialization.</summary>
        private void ApplyPendingLoadData()
        {
            var pendingData = SaveLoadService.PendingLoadData;
            if (pendingData == null)
            {
                Core.Services.GetService<SaveLoadService>()?.ResetForNewGame();
                // InGameTimeService survives a quit to title, so a fresh hero must start the clock over
                Core.Services.GetService<InGameTimeService>()?.ResetToDefault();
                SetupNewGameFarmContent();
                return;
            }

            // Clear pending data so it's not applied again
            SaveLoadService.PendingLoadData = null;

            // Restore tile states (farming)
            var tileStateService = Core.Services.GetService<TileStateService>();
            if (tileStateService != null && pendingData.TileStates != null)
            {
                tileStateService.Clear();
                for (int i = 0; i < pendingData.TileStates.Count; i++)
                {
                    var ts = pendingData.TileStates[i];
                    tileStateService.SetFlag(new Microsoft.Xna.Framework.Point(ts.X, ts.Y), (Farming.TileStateFlag)ts.Flags);
                }

                // Re-derive real tilled tiles on the Detail layer from the restored Tilled flags
                Core.Services.GetService<Services.TilledTileService>()?.RestoreAllTilledTiles();

                // Rebuild the till queue from the restored ReadyToTill flags (idempotent)
                Core.Services.GetService<Services.FarmTaskCoordinator>()?.RescanReadyToTill();
            }

            // Restore wet tile visuals
            Core.Services.GetService<Services.WetTileService>()?.RestoreAllWetTiles();

            // Restore active crop entities and growth state
            var cropGrowthService = Core.Services.GetService<Services.CropGrowthService>();
            if (cropGrowthService != null && pendingData.CropGrowthStates != null)
            {
                var cropsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/CropsProps.atlas");
                cropGrowthService.RestoreAll(pendingData.CropGrowthStates, this, cropsAtlas);
            }

            // Rebuild the water queue from restored crop growth state. The plant queue is rebuilt
            // later, after crop plans are restored — RescanForPlanting here would see an empty
            // CropPlantingService and orphan plans on already-tilled tiles.
            Core.Services.GetService<Services.FarmTaskCoordinator>()?.PopulateWaterQueue();

            // Restore placed buildings
            var buildingService = Core.Services.GetService<Services.BuildingService>();
            buildingService?.Clear();
            if (pendingData.PlacedBuildings != null && _buildingModeOverlay != null)
            {
                for (int i = 0; i < pendingData.PlacedBuildings.Count; i++)
                {
                    var sb   = pendingData.PlacedBuildings[i];
                    var type = (Util.BuildingType)sb.BuildingTypeId;
                    _buildingModeOverlay.SpawnRestoredBuilding(type, sb.TileX, sb.TileY, sb.UniqueId);
                }
            }
            if (buildingService != null)
                buildingService.NextId = pendingData.NextBuildingId;

            // Tile flags restored from a save don't raise per-tile events, so re-derive the idle
            // wander bound now that both buildings and tile states are in place
            Core.Services.GetService<Services.FarmTaskCoordinator>()?.RecalculateRightmostFarmObject();

            // Restore harvested-crop storage inventories (keyed by building UniqueId, so after buildings)
            var cropStorageService = Core.Services.GetService<Services.CropStorageInventoryService>();
            if (cropStorageService != null)
            {
                cropStorageService.Clear();
                if (pendingData.CropStorageInventories != null)
                {
                    for (int i = 0; i < pendingData.CropStorageInventories.Count; i++)
                    {
                        var inv = pendingData.CropStorageInventories[i];
                        var arr = new Services.HarvestSlot[Services.CropStorageInventoryService.SlotsPerBuilding];
                        if (inv.Slots != null)
                        {
                            for (int s = 0; s < inv.Slots.Count; s++)
                            {
                                var sl = inv.Slots[s];
                                if (sl.SlotIndex < 0 || sl.SlotIndex >= arr.Length)
                                    continue;
                                arr[sl.SlotIndex] = new Services.HarvestSlot
                                {
                                    Type  = (Farming.CropType)sl.CropTypeId,
                                    Count = sl.Count,
                                };
                            }
                        }
                        cropStorageService.RestoreInventory(inv.BuildingUniqueId, arr);
                    }
                }
            }

            // Restore refrigerator contents + pre-stock setting (v28+, issue #386)
            var fridgeInvService = Core.Services.GetService<Services.FridgeInventoryService>();
            if (fridgeInvService != null)
            {
                var fridgeArr = new Services.HarvestSlot[Services.FridgeInventoryService.SlotCount];
                if (pendingData.FridgeSlots != null)
                {
                    for (int i = 0; i < pendingData.FridgeSlots.Count; i++)
                    {
                        var sl = pendingData.FridgeSlots[i];
                        if (sl.SlotIndex < 0 || sl.SlotIndex >= fridgeArr.Length)
                            continue;
                        fridgeArr[sl.SlotIndex] = new Services.HarvestSlot
                        {
                            Type  = (Farming.CropType)sl.CropTypeId,
                            Count = sl.Count,
                        };
                    }
                }
                fridgeInvService.RestoreSlots(fridgeArr);
                fridgeInvService.PreStockStackSize = pendingData.FridgePreStockStackSize;
            }

            // Restore dropped crops awaiting pickup (respawns ground entities)
            var droppedCropService = Core.Services.GetService<Services.DroppedCropService>();
            if (droppedCropService != null)
            {
                droppedCropService.Clear();
                if (pendingData.DroppedCrops != null)
                {
                    for (int i = 0; i < pendingData.DroppedCrops.Count; i++)
                    {
                        var d = pendingData.DroppedCrops[i];
                        droppedCropService.Restore((Farming.CropType)d.CropTypeId, d.Count,
                            new Microsoft.Xna.Framework.Point(d.TileX, d.TileY));
                    }
                }
            }

            // Restore seed inventory
            if (pendingData.SeedInventory != null && _seedModeOverlay != null)
                _seedModeOverlay.SetSeedInventory(pendingData.SeedInventory);

            // Restore crop plans
            var cropPlantingService = Core.Services.GetService<Services.CropPlantingService>();
            cropPlantingService?.Clear();
            if (pendingData.CropPlans != null && _seedModeOverlay != null)
            {
                for (int i = 0; i < pendingData.CropPlans.Count; i++)
                {
                    var cp = pendingData.CropPlans[i];
                    _seedModeOverlay.SpawnRestoredCropPlan((Farming.CropType)cp.CropTypeId, cp.TileX, cp.TileY);
                }
            }

            // Apply auto shop settings from save data and sync the settings UI controls
            var autoSeedSvc = Core.Services.GetService<Services.AutoSeedPurchaseService>();
            if (autoSeedSvc != null)
            {
                autoSeedSvc.Enabled    = pendingData.AutomateSeedPurchases;
                autoSeedSvc.GoldBuffer = pendingData.AutoShopGoldBuffer;
            }
            var autoCropSellSvc = Core.Services.GetService<Services.AutoCropSellService>();
            if (autoCropSellSvc != null)
            {
                autoCropSellSvc.Enabled = pendingData.AutoSellCrops;
                autoCropSellSvc.KeepStacks = pendingData.AutoSellKeepStacks;
                if (pendingData.AutoSellCropDesignations != null)
                {
                    int count = pendingData.AutoSellCropDesignations.Length < Farming.CropTypeInfo.Count
                        ? pendingData.AutoSellCropDesignations.Length
                        : Farming.CropTypeInfo.Count;
                    for (int i = 0; i < count; i++)
                        autoCropSellSvc.Designations[i] = pendingData.AutoSellCropDesignations[i];
                }
            }
            var autoJobSvc = Core.Services.GetService<Services.AutoJobAssignmentService>();
            if (autoJobSvc != null)
                autoJobSvc.Enabled = pendingData.AutomateMonsterJobs;
            var autoSellExcessSvc = Core.Services.GetService<Services.AutoSellExcessItemsService>();
            if (autoSellExcessSvc != null)
            {
                autoSellExcessSvc.Enabled = pendingData.AutoSellExcessItems;
                autoSellExcessSvc.ConsumablesFirst = pendingData.AutoSellConsumablesFirst;
                if (pendingData.AutoSellRarityAllowed != null)
                {
                    int count = pendingData.AutoSellRarityAllowed.Length < autoSellExcessSvc.RarityAllowed.Length
                        ? pendingData.AutoSellRarityAllowed.Length
                        : autoSellExcessSvc.RarityAllowed.Length;
                    for (int i = 0; i < count; i++)
                        autoSellExcessSvc.RarityAllowed[i] = pendingData.AutoSellRarityAllowed[i];
                }
                if (pendingData.AutoSellGearTypeAllowed != null)
                {
                    int count = pendingData.AutoSellGearTypeAllowed.Length < autoSellExcessSvc.GearTypeAllowed.Length
                        ? pendingData.AutoSellGearTypeAllowed.Length
                        : autoSellExcessSvc.GearTypeAllowed.Length;
                    for (int i = 0; i < count; i++)
                        autoSellExcessSvc.GearTypeAllowed[i] = pendingData.AutoSellGearTypeAllowed[i];
                }
                if (pendingData.AutoSellConsumableSelected != null)
                {
                    int count = pendingData.AutoSellConsumableSelected.Length < autoSellExcessSvc.ConsumableSellAllowed.Length
                        ? pendingData.AutoSellConsumableSelected.Length
                        : autoSellExcessSvc.ConsumableSellAllowed.Length;
                    for (int i = 0; i < count; i++)
                        autoSellExcessSvc.ConsumableSellAllowed[i] = pendingData.AutoSellConsumableSelected[i];
                }
                if (pendingData.AutoSellConsumableMinStacks != null)
                {
                    int count = pendingData.AutoSellConsumableMinStacks.Length < autoSellExcessSvc.ConsumableMinStacks.Length
                        ? pendingData.AutoSellConsumableMinStacks.Length
                        : autoSellExcessSvc.ConsumableMinStacks.Length;
                    for (int i = 0; i < count; i++)
                        autoSellExcessSvc.ConsumableMinStacks[i] = pendingData.AutoSellConsumableMinStacks[i];
                }
            }
            var autoItemPurchaseSvc = Core.Services.GetService<Services.AutoItemPurchaseService>();
            if (autoItemPurchaseSvc != null)
            {
                autoItemPurchaseSvc.Enabled = pendingData.AutoPurchaseItems;
                autoItemPurchaseSvc.ConsumablesFirst = pendingData.AutoPurchaseConsumablesFirst;
                autoItemPurchaseSvc.PurchaseMercenaryGear = pendingData.AutoPurchaseMercenaryGear;
                if (pendingData.AutoPurchaseRarityAllowed != null)
                {
                    int count = pendingData.AutoPurchaseRarityAllowed.Length < autoItemPurchaseSvc.BuyRarityAllowed.Length
                        ? pendingData.AutoPurchaseRarityAllowed.Length
                        : autoItemPurchaseSvc.BuyRarityAllowed.Length;
                    for (int i = 0; i < count; i++)
                        autoItemPurchaseSvc.BuyRarityAllowed[i] = pendingData.AutoPurchaseRarityAllowed[i];
                }
                if (pendingData.AutoPurchaseGearTypeAllowed != null)
                {
                    int count = pendingData.AutoPurchaseGearTypeAllowed.Length < autoItemPurchaseSvc.BuyGearTypeAllowed.Length
                        ? pendingData.AutoPurchaseGearTypeAllowed.Length
                        : autoItemPurchaseSvc.BuyGearTypeAllowed.Length;
                    for (int i = 0; i < count; i++)
                        autoItemPurchaseSvc.BuyGearTypeAllowed[i] = pendingData.AutoPurchaseGearTypeAllowed[i];
                }
                if (pendingData.AutoPurchaseConsumableSelected != null)
                {
                    int count = pendingData.AutoPurchaseConsumableSelected.Length < autoItemPurchaseSvc.ConsumableSelected.Length
                        ? pendingData.AutoPurchaseConsumableSelected.Length
                        : autoItemPurchaseSvc.ConsumableSelected.Length;
                    for (int i = 0; i < count; i++)
                        autoItemPurchaseSvc.ConsumableSelected[i] = pendingData.AutoPurchaseConsumableSelected[i];
                }
                if (pendingData.AutoPurchaseConsumableStacks != null)
                {
                    int count = pendingData.AutoPurchaseConsumableStacks.Length < autoItemPurchaseSvc.ConsumableStackTargets.Length
                        ? pendingData.AutoPurchaseConsumableStacks.Length
                        : autoItemPurchaseSvc.ConsumableStackTargets.Length;
                    for (int i = 0; i < count; i++)
                        autoItemPurchaseSvc.ConsumableStackTargets[i] = pendingData.AutoPurchaseConsumableStacks[i];
                }
            }
            var autoHireSvc = Core.Services.GetService<Services.AutoHireMercenaryService>();
            if (autoHireSvc != null)
            {
                autoHireSvc.Enabled = pendingData.AutoHireMercenariesEnabled;
                autoHireSvc.Merc1Job = Services.AutoHireMercenaryService.SanitizeJob(
                    (RolePlayingFramework.Jobs.JobType)pendingData.AutoHireMerc1Job);
                autoHireSvc.Merc2Job = Services.AutoHireMercenaryService.SanitizeJob(
                    (RolePlayingFramework.Jobs.JobType)pendingData.AutoHireMerc2Job);
                // Slot 2 is only meaningful while slot 1 holds a job — enforce the invariant on load
                if (autoHireSvc.Merc1Job == RolePlayingFramework.Jobs.JobType.None)
                    autoHireSvc.Merc2Job = RolePlayingFramework.Jobs.JobType.None;
            }
            var autoLearnSvc = Core.Services.GetService<Services.AutoLearnSkillsService>();
            if (autoLearnSvc != null)
            {
                autoLearnSvc.Enabled = pendingData.AutoLearnSkillsEnabled;
                autoLearnSvc.Mode = Services.AutoLearnSkillsService.SanitizeMode(pendingData.AutoLearnMode);
            }
            _settingsUI?.SyncAutomationControlsFromService();

            // Rebuild the plant queue now that both tile states and crop plans are restored
            Core.Services.GetService<Services.FarmTaskCoordinator>()?.RescanForPlanting();

            // Restore in-game time so Color Grading reflects the correct time of day
            var inGameTimeService = Core.Services.GetService<InGameTimeService>();
            if (inGameTimeService != null)
            {
                if (pendingData.InGameTimeAccumulatedSeconds > 0)
                    inGameTimeService.SetAccumulatedTime(pendingData.InGameTimeAccumulatedSeconds);
                else
                    inGameTimeService.ResetToDefault(); // never inherit the previous session's clock
            }

            Debug.Log("[MainGameScene] Applying pending load data...");
            
            // Find hero entity and component
            var heroEntity = FindEntity("hero");
            if (heroEntity == null)
            {
                Debug.Error("[MainGameScene] Cannot apply load data - hero entity not found");
                return;
            }
            
            var heroComp = heroEntity.GetComponent<HeroComponent>();
            if (heroComp == null)
            {
                Debug.Error("[MainGameScene] Cannot apply load data - HeroComponent not found");
                return;
            }
            
            // Reconstruct the hero from saved data
            var job = RolePlayingFramework.Jobs.JobFactory.CreateJob(pendingData.JobName ?? "Knight");
            var baseStats = new StatBlock(
                pendingData.BaseStrength, pendingData.BaseAgility,
                pendingData.BaseVitality, pendingData.BaseMagic);
            
            // Reconstruct crystal if present
            HeroCrystal heroCrystal = null;
            if (pendingData.HasCrystal)
            {
                var crystalJob = RolePlayingFramework.Jobs.JobFactory.CreateJob(pendingData.CrystalJobName ?? "Knight");
                var crystalStats = new StatBlock(
                    pendingData.CrystalBaseStrength, pendingData.CrystalBaseAgility,
                    pendingData.CrystalBaseVitality, pendingData.CrystalBaseMagic);
                
                heroCrystal = new HeroCrystal(
                    pendingData.HeroName ?? "Hero", crystalJob, pendingData.CrystalLevel, crystalStats);
                
                // Restore JP
                heroCrystal.EarnJP(pendingData.TotalJP);
                
                // Restore learned skills on the crystal
                for (int i = 0; i < pendingData.LearnedSkillIds.Count; i++)
                {
                    heroCrystal.AddLearnedSkill(pendingData.LearnedSkillIds[i]);
                }
                
                // Restore synergy data
                for (int i = 0; i < pendingData.DiscoveredSynergyIds.Count; i++)
                {
                    heroCrystal.DiscoverSynergy(pendingData.DiscoveredSynergyIds[i]);
                }
                
                for (int i = 0; i < pendingData.LearnedSynergySkillIds.Count; i++)
                {
                    heroCrystal.LearnSynergySkill(pendingData.LearnedSynergySkillIds[i]);
                }
                
                // Restore synergy points
                var synergyEnumerator = pendingData.SynergyPoints.GetEnumerator();
                while (synergyEnumerator.MoveNext())
                {
                    heroCrystal.EarnSynergyPoints(synergyEnumerator.Current.Key, synergyEnumerator.Current.Value);
                }
                synergyEnumerator.Dispose();
            }
            
            // Create hero with saved level and stats
            var hero = new Hero(
                pendingData.HeroName ?? "Hero",
                job,
                pendingData.Level,
                baseStats,
                heroCrystal);
            
            // Restore equipment (affects MaxHP/MaxMP through RecalculateDerived)
            if (pendingData.EquipmentNames != null)
            {
                for (int i = 0; i < 6 && i < pendingData.EquipmentNames.Length; i++)
                {
                    string itemName = pendingData.EquipmentNames[i];
                    if (string.IsNullOrEmpty(itemName))
                        continue;
                    
                    if (RolePlayingFramework.Equipment.ItemRegistry.TryCreateItem(itemName, out var item))
                    {
                        var slot = (RolePlayingFramework.Equipment.EquipmentSlot)i;
                        hero.SetEquipmentSlot(slot, item);
                    }
                    else
                    {
                        Debug.Warn("[MainGameScene] Could not find equipment item: " + itemName);
                    }
                }
            }
            
            // Restore remaining experience toward next level
            if (pendingData.Experience > 0)
            {
                hero.AddExperience(pendingData.Experience);
            }
            
            // Adjust HP from max to saved value
            int hpDiff = hero.MaxHP - pendingData.CurrentHP;
            if (hpDiff > 0)
                hero.TakeDamage(hpDiff);
            
            // Adjust MP from max to saved value using SetCurrentMP (not SpendMP) so
            // MPCostReduction is NOT applied to the delta — state-restore must land exactly at saved value.
            hero.SetCurrentMP(pendingData.CurrentMP);
            
            // Assign reconstructed hero to the component
            heroComp.LinkedHero = hero;
            
            // Store pending inventory items on the HeroComponent for deferred restoration.
            // Nez defers OnAddedToEntity, so Bag is null at this point during Begin().
            // HeroComponent.OnAddedToEntity will restore these items after creating the Bag.
            if (pendingData.InventoryItems != null && pendingData.InventoryItems.Count > 0)
            {
                heroComp.PendingInventoryItems = pendingData.InventoryItems;
                Debug.Log("[MainGameScene] Stored " + pendingData.InventoryItems.Count + " pending inventory items for deferred restoration");
            }
            
            // Restore priorities
            heroComp.Priority1 = (HeroPitPriority)pendingData.Priority1;
            heroComp.Priority2 = (HeroPitPriority)pendingData.Priority2;
            heroComp.Priority3 = (HeroPitPriority)pendingData.Priority3;
            heroComp.HealPriority1 = (HeroHealPriority)pendingData.HealPriority1;
            heroComp.HealPriority2 = (HeroHealPriority)pendingData.HealPriority2;
            heroComp.HealPriority3 = (HeroHealPriority)pendingData.HealPriority3;
            
            // Restore behavior settings
            heroComp.CurrentBattleTactic = (BattleTactic)pendingData.BattleTacticValue;
            heroComp.UseConsumablesOnMercenaries = pendingData.UseConsumablesOnMercenaries;
            heroComp.MercenariesCanUseConsumables = pendingData.MercenariesCanUseConsumables;

            // Auto-equip options live on the hero but are edited from the Settings Automation tab,
            // which syncs earlier in this load than the hero rebuild — so refresh those controls now.
            heroComp.AutoEquipHero = pendingData.AutoEquipHero;
            heroComp.AutoEquipMercenaries = pendingData.AutoEquipMercenaries;
            _settingsUI?.SyncAutoEquipControlsFromHero();


            // Restore pit tier and base level BEFORE pit level so that width-regen uses
            // the correct effective depth (tier 1 behaviour is identical to before).
            var pitManager = Core.Services.GetService<PitWidthManager>();
            if (pitManager != null)
            {
                pitManager.SetPitTier(pendingData.PitTier);
                pitManager.SetTierBaseLevel(pendingData.TierBaseLevel);
                pitManager.SetPitLevel(Math.Max(1, pendingData.PitLevel));
            }
            
            // Restore allied monsters
            var alliedManager = Core.Services.GetService<AlliedMonsterManager>();
            if (alliedManager != null && pendingData.AlliedMonsters != null)
            {
                for (int i = 0; i < pendingData.AlliedMonsters.Count; i++)
                {
                    var saved = pendingData.AlliedMonsters[i];
                    var allied = new AlliedMonster(saved.Name, saved.MonsterTypeName,
                        saved.FishingProficiency, saved.CookingProficiency, saved.FarmingProficiency,
                        saved.MonsterHouseId);
                    allied.Job = (MonsterJob)saved.MonsterJobId;
                    alliedManager.AddAlliedMonster(allied);
                }
            }

            // Restore defeated-monster record (issue #283)
            var defeatedMonsterService = Core.Services.GetService<DefeatedMonsterService>();
            if (defeatedMonsterService != null)
            {
                defeatedMonsterService.LoadFrom(pendingData.DefeatedMonsterTypes);
                // Reconcile from the allied roster: any monster living in a house must have been
                // defeated at least once, so retroactively mark its type (covers pre-#283 saves).
                if (alliedManager != null)
                {
                    var roster = alliedManager.AlliedMonsters;
                    for (int i = 0; i < roster.Count; i++)
                        defeatedMonsterService.MarkDefeatedByTypeName(roster[i].MonsterTypeName);
                }
            }

            // Restore hired mercenaries
            var mercManager = Core.Services.GetService<MercenaryManager>();
            if (mercManager != null && pendingData.HiredMercenaries != null && pendingData.HiredMercenaries.Count > 0)
            {
                for (int i = 0; i < pendingData.HiredMercenaries.Count; i++)
                {
                    mercManager.SpawnHiredMercenaryFromSave(pendingData.HiredMercenaries[i], heroEntity, i);
                }
                Debug.Log("[MainGameScene] Restored " + pendingData.HiredMercenaries.Count + " hired mercenaries");
            }

            // Loading during sleeping hours: spawn the party directly in the inn beds instead of
            // making them walk to the innkeeper first. Must run after the mercenaries are spawned
            // (their default spawn positions derive from the hero's). SleepInBedAction consumes
            // SpawnedAsleepPending and takes over from the in-bed state.
            if (Core.Services.GetService<InGameTimeService>()?.IsNighttime == true)
            {
                PositionPartyInBedsForNightLoad(heroEntity, heroComp);
            }

            // Restore party dining state (issue #319) — after hero + hired mercs exist so
            // active meal buffs can be re-registered against their combatants
            Core.Services.GetService<Services.PartyDiningService>()?.RestoreFromSave(pendingData);
            
            // Store pending shortcut slots on the shortcut bar for deferred restoration
            if (pendingData.ShortcutSlots != null && pendingData.ShortcutSlots.Count > 0 && _shortcutBar != null)
            {
                _shortcutBar.SetPendingShortcutSlots(pendingData.ShortcutSlots);
                Debug.Log("[MainGameScene] Stored " + pendingData.ShortcutSlots.Count + " pending shortcut slots for deferred restoration");
            }

            // Restore crystal collection
            var crystalService = Core.Services.GetService<CrystalCollectionService>();
            if (crystalService != null && pendingData.CrystalCollection != null)
            {
                for (int i = 0; i < pendingData.CrystalCollection.Count; i++)
                {
                    var saved = pendingData.CrystalCollection[i];
                    var crystalJob = RolePlayingFramework.Jobs.JobFactory.CreateJob(saved.JobName ?? "Knight");
                    var crystalStats = new StatBlock(
                        saved.BaseStrength, saved.BaseAgility,
                        saved.BaseVitality, saved.BaseMagic);
                    var color = new Color(saved.R, saved.G, saved.B, saved.A);
                    
                    var crystal = new HeroCrystal(saved.Name, crystalJob, saved.Level, crystalStats, color);
                    
                    // Restore JP
                    crystal.EarnJP(saved.TotalJP);
                    
                    // Restore learned skills
                    if (saved.LearnedSkillIds != null)
                    {
                        for (int j = 0; j < saved.LearnedSkillIds.Count; j++)
                        {
                            crystal.AddLearnedSkill(saved.LearnedSkillIds[j]);
                        }
                    }
                    
                    // Restore synergy data
                    if (saved.DiscoveredSynergyIds != null)
                    {
                        for (int j = 0; j < saved.DiscoveredSynergyIds.Count; j++)
                        {
                            crystal.DiscoverSynergy(saved.DiscoveredSynergyIds[j]);
                        }
                    }
                    
                    if (saved.LearnedSynergySkillIds != null)
                    {
                        for (int j = 0; j < saved.LearnedSynergySkillIds.Count; j++)
                        {
                            crystal.LearnSynergySkill(saved.LearnedSynergySkillIds[j]);
                        }
                    }
                    
                    if (saved.SynergyPoints != null)
                    {
                        var synEnumerator = saved.SynergyPoints.GetEnumerator();
                        while (synEnumerator.MoveNext())
                        {
                            crystal.EarnSynergyPoints(synEnumerator.Current.Key, synEnumerator.Current.Value);
                        }
                        synEnumerator.Dispose();
                    }
                    
                    crystalService.TryAddToInventory(crystal);
                }
                
                // Restore crystal queue
                if (pendingData.CrystalQueue != null)
                {
                    for (int i = 0; i < pendingData.CrystalQueue.Count; i++)
                    {
                        var qSaved = pendingData.CrystalQueue[i];
                        var qCrystal = qSaved.ToHeroCrystal();
                        crystalService.TryEnqueue(qCrystal);
                    }
                }

                // Restore pending next crystal
                if (pendingData.PendingNextCrystal.HasValue)
                {
                    crystalService.PendingNextCrystal = pendingData.PendingNextCrystal.Value.ToHeroCrystal();
                }

                // Restore forge slots (physical crystals not stored in inventory)
                if (pendingData.ForgeSlotA.HasValue)
                    crystalService.SetForgeSlotADirect(pendingData.ForgeSlotA.Value.ToHeroCrystal());
                if (pendingData.ForgeSlotB.HasValue)
                    crystalService.SetForgeSlotBDirect(pendingData.ForgeSlotB.Value.ToHeroCrystal());
                
                Debug.Log("[MainGameScene] Restored " + pendingData.CrystalCollection.Count + " crystals to collection");
            }

            // Restore Second Chance Vault crystals
            var vaultService = Core.Services.GetService<SecondChanceMerchantVault>();
            if (vaultService != null)
            {
                // Clear vault before restoring to prevent duplication on repeated loads
                vaultService.Clear();

                // Restore vault crystals
                if (pendingData.SecondChanceVaultCrystals != null)
                {
                    for (int i = 0; i < pendingData.SecondChanceVaultCrystals.Count; i++)
                    {
                        var saved = pendingData.SecondChanceVaultCrystals[i];
                        var crystalJob = RolePlayingFramework.Jobs.JobFactory.CreateJob(saved.JobName ?? "Knight");
                        var crystalStats = new StatBlock(
                            saved.BaseStrength, saved.BaseAgility,
                            saved.BaseVitality, saved.BaseMagic);
                        var color = new Color(saved.R, saved.G, saved.B, saved.A);
                        
                        var crystal = new HeroCrystal(saved.Name, crystalJob, saved.Level, crystalStats, color);
                        
                        // Restore JP
                        crystal.EarnJP(saved.TotalJP);
                        
                        // Restore learned skills
                        if (saved.LearnedSkillIds != null)
                        {
                            for (int j = 0; j < saved.LearnedSkillIds.Count; j++)
                            {
                                crystal.AddLearnedSkill(saved.LearnedSkillIds[j]);
                            }
                        }
                        
                        // Restore synergy data
                        if (saved.DiscoveredSynergyIds != null)
                        {
                            for (int j = 0; j < saved.DiscoveredSynergyIds.Count; j++)
                            {
                                crystal.DiscoverSynergy(saved.DiscoveredSynergyIds[j]);
                            }
                        }
                        
                        if (saved.LearnedSynergySkillIds != null)
                        {
                            for (int j = 0; j < saved.LearnedSynergySkillIds.Count; j++)
                            {
                                crystal.LearnSynergySkill(saved.LearnedSynergySkillIds[j]);
                            }
                        }
                        
                        if (saved.SynergyPoints != null)
                        {
                            var synEnumerator = saved.SynergyPoints.GetEnumerator();
                            while (synEnumerator.MoveNext())
                            {
                                crystal.EarnSynergyPoints(synEnumerator.Current.Key, synEnumerator.Current.Value);
                            }
                            synEnumerator.Dispose();
                        }
                        
                        vaultService.AddCrystal(crystal);
                    }
                    
                    Debug.Log("[MainGameScene] Restored " + pendingData.SecondChanceVaultCrystals.Count + " crystals to Second Chance Vault");
                }

                // Restore vault items
                if (pendingData.SecondChanceVaultItems != null)
                {
                    for (int i = 0; i < pendingData.SecondChanceVaultItems.Count; i++)
                    {
                        var vi = pendingData.SecondChanceVaultItems[i];
                        if (string.IsNullOrEmpty(vi.Name)) continue;

                        if (ItemRegistry.TryCreateItem(vi.Name, out var itemTemplate))
                        {
                            if (itemTemplate is Consumable consumable)
                            {
                                consumable.StackCount = vi.Quantity;
                                vaultService.AddItem(consumable, logEvictions: false);
                            }
                            else
                            {
                                for (int q = 0; q < vi.Quantity; q++)
                                {
                                    if (ItemRegistry.TryCreateItem(vi.Name, out var gearCopy))
                                        vaultService.AddItem(gearCopy, logEvictions: false);
                                }
                            }
                        }
                    }

                    Debug.Log("[MainGameScene] Restored " + pendingData.SecondChanceVaultItems.Count + " item stacks to Second Chance Vault");
                }
            }
            
            Debug.Log("[MainGameScene] Load data applied successfully - Hero: " + (pendingData.HeroName ?? "?") + " Level " + pendingData.Level);
        }

        /// <summary>Emits the welcome greeting and a random introductory phrase to the event console.</summary>
        private void EmitWelcomeMessage()
        {
            var evtSvc = Core.Services.GetService<Services.GameEventService>();
            if (evtSvc == null) return;

            var heroComp = FindEntity("hero")?.GetComponent<ECS.Components.HeroComponent>();
            string heroName = heroComp?.LinkedHero?.Name ?? "Hero";

            evtSvc.EmitLocalized(UITextKey.ConsoleWelcome,
                (heroName, GameConfig.ConsoleColorHeroName));

            var txtSvc = Core.Services.GetService<TextService>();
            int phraseIndex = Nez.Random.Range(0, 3);
            string phrase = phraseIndex == 0 ? txtSvc.DisplayText(TextType.UI, UITextKey.ConsoleWelcomePhrase1)
                          : phraseIndex == 1 ? txtSvc.DisplayText(TextType.UI, UITextKey.ConsoleWelcomePhrase2)
                          : txtSvc.DisplayText(TextType.UI, UITextKey.ConsoleWelcomePhrase3);
            evtSvc.Emit(phrase);
        }

        private void LoadMap()
        {
            if (string.IsNullOrEmpty(_mapPath))
                return;

            _tmxMap = Core.Content.LoadTiledMap(_mapPath);
            Core.Services.AddService(new TiledMapService(_tmxMap));
            var tiledEntity = CreateEntity("tilemap").SetTag(GameConfig.TAG_TILEMAP);

            var baseLayerRenderer = tiledEntity.AddComponent(new TiledMapRenderer(_tmxMap, "Collision"));
            baseLayerRenderer.SetLayerToRender("Base");
            baseLayerRenderer.RenderLayer = GameConfig.RenderLayerBase;

            var detailLayerRenderer = tiledEntity.AddComponent(new TiledMapRenderer(_tmxMap));
            detailLayerRenderer.SetLayerToRender("Detail");
            detailLayerRenderer.SetRenderLayer(GameConfig.RenderLayerDetail);

            var topLayerRenderer = tiledEntity.AddComponent(new TiledMapRenderer(_tmxMap));
            topLayerRenderer.SetLayerToRender("Top");
            topLayerRenderer.SetRenderLayer(GameConfig.RenderLayerTop);

            var fogLayerRenderer = tiledEntity.AddComponent(new TiledMapRenderer(_tmxMap));
            fogLayerRenderer.SetLayerToRender("FogOfWar");
            fogLayerRenderer.SetRenderLayer(GameConfig.RenderLayerFogOfWar);

            // Day/night color grading applies to the terrain tilemap layers plus a few
            // environment sprites (pit walls, placed buildings) via a shared material, so
            // actors/monsters/dropped items/UI keep their normal daytime colors. FogOfWar is
            // intentionally left ungraded. Registered as a service so PitGenerator and
            // BuildingModeOverlay can attach the same material to the sprites they create.
            _colorGrading = new Rendering.ColorGradingController();
            Core.Services.AddService(_colorGrading);
            baseLayerRenderer.SetMaterial(_colorGrading.Material);
            detailLayerRenderer.SetMaterial(_colorGrading.Material);
            topLayerRenderer.SetMaterial(_colorGrading.Material);

            SpawnTreeBands();

            // Volumetric scrolling cloud overlay (front-most world-space layer, drawn over terrain,
            // actors, fog-of-war, tree bands, and buildings; screen-space UI still renders over it).
            // Not registered as a service — nothing else consumes it.
            _cloudOverlay = new Rendering.CloudOverlayController();
            _cloudOverlayEntity = CreateEntity("cloud-overlay");
            var cloudComponent = _cloudOverlayEntity.AddComponent(new CloudOverlayComponent(_cloudOverlay));
            cloudComponent.SetRenderLayer(GameConfig.RenderLayerCloudOverlay);
            cloudComponent.SetMaterial(_cloudOverlay.Material);

            _cameraController?.ConfigureZoomForMap(_mapPath);

            // Initialize till mode overlay now that the map is loaded. SetupUIOverlay() runs
            // earlier (in Initialize, before Begin) so _uiStage already exists here; wire it now
            // so the overlay can detect when the mouse is over UI and suppress tile placement.
            _tillModeOverlay = new TillModeOverlay(this, _tmxMap);
            _tillModeOverlay.SetStage(_uiStage);

            // Tilled tile service writes real tilled tiles to the Detail layer when farming
            // monsters complete till actions; the overlay drops its grayscale sprite in response.
            var tileStateService = Core.Services.GetService<TileStateService>();
            var tilledTileService = new Services.TilledTileService(_tmxMap, tileStateService);
            tilledTileService.OnTileTilled  += tile => _tillModeOverlay?.OnTileTilled(tile);
            // Restore-grass: a tile returning to grass must also refresh the ReadyToTill neighbor bitmasks.
            tilledTileService.OnTileRestored += tile => _tillModeOverlay?.OnTileTilled(tile);
            Core.Services.AddService(tilledTileService);

            // Wet tile service writes watered-soil bitmask tiles to the Detail layer.
            var wetTileService = new Services.WetTileService(_tmxMap, tileStateService);
            Core.Services.AddService(wetTileService);

            // Crop growth service tracks all actively growing crops and advances frames.
            var cropGrowthService = new Services.CropGrowthService(Core.Services.GetService<Services.CropPlantingService>());
            Core.Services.AddService(cropGrowthService);

            // Building mode overlay — creates its UI panels on the same stage.
            _buildingModeOverlay = new BuildingModeOverlay(this, _uiStage);
            _buildingModeOverlay.RequestExitBuildingMode += () => _settingsUI?.ExitBuildingModeViaFarm();

            // Seed planting overlay — creates its UI panels on the same stage.
            _seedModeOverlay = new SeedPlantingModeOverlay(this, _uiStage);
            _seedModeOverlay.RequestExitSeedMode        += () => _settingsUI?.ExitSeedModeViaFarm();
            _seedModeOverlay.RequestExitRemoveCropsMode += () => _settingsUI?.ExitRemoveCropsModeViaFarm();

            // Harvested Crops viewer — read-only storage grid on the same stage.
            _harvestedCropsModeOverlay = new HarvestedCropsModeOverlay(this, _uiStage);
            _harvestedCropsModeOverlay.RequestExitHarvestedCropsMode += () => _settingsUI?.ExitHarvestedCropsModeViaFarm();

            // Restore Grass mode overlay — cursor + drag to revert tilled tiles back to grass.
            _restoreGrassModeOverlay = new UI.RestoreGrassModeOverlay(this);
            _restoreGrassModeOverlay.SetStage(_uiStage);

            // Refrigerator window — opened by clicking the kitchen fridge (issue #386).
            _refrigeratorDialog = new RefrigeratorDialog(_uiStage);

            // Wire the Farm sub-button "Refrigerator" to open the fridge dialog.
            // UpdateFridgeDialogGate drives pause/zoom purely off IsVisible(), so no extra preconditions.
            if (_settingsUI != null)
            {
                _settingsUI.RefrigeratorRequested = () => _refrigeratorDialog?.Show();
                _settingsUI.RefrigeratorDialogOpen = () => _refrigeratorDialog != null && _refrigeratorDialog.IsVisible();
            }

            // Building context menu — shown when a placed building is clicked (Move / Show ...).
            _buildingContextMenu = new BuildingContextMenu(UI.PitHeroSkin.CreateSkin());
            _buildingContextMenu.OnMove += (pb) => _buildingModeOverlay?.BeginMove(pb);
            _buildingContextMenu.OnShow += (pb) =>
            {
                if (pb.Type == Util.BuildingType.MonsterHouse)
                {
                    _settingsUI?.ShowMonstersForHouse(pb.UniqueId);
                }
                else
                {
                    _harvestedCropsModeOverlay?.SetBuildingFilter(pb.UniqueId);
                    _settingsUI?.EnterHarvestedCropsMode();
                }
            };

            // Sell an empty building (issue #285). The context menu only offers this once the
            // building holds nothing — no crops in a Crop Storage, no monsters in a Monster House.
            _buildingContextMenu.OnSellBuilding += (pb) =>
            {
                int gold = Util.BuildingConfig.GetSellPrice(pb.Type);
                var textSvc = Core.Services.GetService<Services.TextService>();
                var dialog = new ConfirmationDialog(
                    textSvc?.DisplayText(TextType.UI, UITextKey.ButtonSellBuilding),
                    string.Format(textSvc?.DisplayText(TextType.UI, UITextKey.DialogSellBuildingPrompt) ?? "{0}", gold),
                    UI.PitHeroSkin.CreateSkin(),
                    onYes: () =>
                    {
                        // Lands on a deterministic tick via the command queue; the handler removes
                        // first and pays only if this call is what actually removed it (replay system)
                        Services.Replay.PlayerCommandService.Dispatch(new Services.Replay.PlayerCommand(
                            Services.Replay.PlayerCommandType.RemoveBuilding, pb.UniqueId));
                    });
                dialog.YesButton.SuppressGlobalClick = true;
                dialog.Show(_uiStage);
            };

            // Add Monsters dialog — opened from the Monster House context menu (issue #283).
            _addMonsterDialog = new AddMonsterDialog(UI.PitHeroSkin.CreateSkin(), _uiStage);
            _buildingContextMenu.OnAddMonsters += (pb) => _addMonsterDialog?.ShowForHouse(pb.UniqueId);

            // Initialize pit width manager after map and services are set up
            SetupPitWidthManager();
        }

        /// <summary>
        /// Creates the decorative tree bands north and south of the map (#348). Each band paints
        /// itself once into its own RenderTexture on the first frame, then blits a single quad.
        /// </summary>
        private void SpawnTreeBands()
        {
            if (_tmxMap == null)
                return;

            var atlas = Core.Content.LoadSpriteAtlas("Content/Atlases/CropsProps.atlas");
            var tree = atlas?.GetSprite("Tree");
            var tree2 = atlas?.GetSprite("Tree2");
            if (tree == null || tree2 == null)
            {
                Debug.Warn("[MainGameScene] Tree sprites missing from CropsProps atlas; skipping tree bands");
                return;
            }

            var grass = GetMapTileSprite(GameConfig.TreeBandGrassTileGid);
            var mapWidthPx = _tmxMap.Width * _tmxMap.TileWidth;
            var mapHeightPx = _tmxMap.Height * _tmxMap.TileHeight;
            var bandEntity = CreateEntity("tree-bands");

            AddTreeBand(bandEntity, tree, tree2, grass, mapWidthPx, mapHeightPx,
                GameConfig.TreeBandTopStartTileY, GameConfig.TreeBandTopEndTileY,
                GameConfig.TreeBandSeed, true);
            AddTreeBand(bandEntity, tree, tree2, grass, mapWidthPx, mapHeightPx,
                GameConfig.TreeBandBottomStartTileY, GameConfig.TreeBandBottomEndTileY,
                GameConfig.TreeBandSeed + 1, false);
        }

        /// <summary>
        /// Builds a top-left-origin sprite for a single tile of the map tileset, by global tile id
        /// (the same 1-based numbering Tiled shows, since the tileset's firstgid is 1).
        /// </summary>
        private Sprite GetMapTileSprite(int gid)
        {
            var tileset = _tmxMap?.GetTilesetForTileGid(gid);
            if (tileset?.Image?.Texture == null)
                return null;
            if (!tileset.TileRegions.TryGetValue(gid, out var region))
            {
                Debug.Warn("[MainGameScene] Map tileset has no region for gid {0}", gid);
                return null;
            }

            var sprite = new Sprite(tileset.Image.Texture,
                new Rectangle((int)region.X, (int)region.Y, (int)region.Width, (int)region.Height));
            sprite.Origin = Vector2.Zero;
            return sprite;
        }

        /// <summary>Adds one tree band component to the shared band entity and grades it day/night.</summary>
        private void AddTreeBand(Entity bandEntity, Sprite tree, Sprite tree2, Sprite grass,
            int mapWidthPx, int mapHeightPx, int startTileY, int endTileY, int seed, bool overlapBelow)
        {
            var band = bandEntity.AddComponent(new TreeBandComponent(
                tree, tree2, grass, mapWidthPx, mapHeightPx, startTileY, endTileY, seed, overlapBelow));
            band.SetRenderLayer(GameConfig.RenderLayerTreeBand);
            if (_colorGrading?.Material != null)
                band.SetMaterial(_colorGrading.Material);
        }

        private void SetupPitWidthManager()
        {
            var pitWidthManager = new PitWidthManager();
            Core.Services.AddService(pitWidthManager);
            pitWidthManager.Initialize();
            Debug.Log("[MainGameScene] PitWidthManager initialized and registered as service");
        }

        private void SpawnPit()
        {
            var pitEntity = CreateEntity("pit");
            pitEntity.SetTag(GameConfig.TAG_PIT); // Make sure this is set!

            // Calculate pit bounds in world coordinates with padding
            var pitWorldBounds = CalculatePitWorldBounds();

            // Position the pit entity at the center of the bounds
            pitEntity.SetPosition(pitWorldBounds.Center.ToVector2());

            // Add logical pit component
            pitEntity.AddComponent(new PitComponent
            {
                CrystalPower = 1f,
                IsActive = true,
                EffectRadius = 100f
            });

            // Add trigger collider covering the pit area
            var pitCollider = pitEntity.AddComponent(new BoxCollider(pitWorldBounds.Width, pitWorldBounds.Height));
            pitCollider.IsTrigger = true; // Make it a trigger so it doesn't block movement
            Flags.SetFlagExclusive(ref pitCollider.PhysicsLayer, GameConfig.PhysicsPitLayer);

            Debug.Log($"[MainGameScene] Created pit entity with Tag={pitEntity.Tag} at position {pitEntity.Transform.Position.X},{pitEntity.Transform.Position.Y}");
            Debug.Log($"[MainGameScene] Pit trigger collider bounds: X={pitWorldBounds.X}, Y={pitWorldBounds.Y}, Width={pitWorldBounds.Width}, Height={pitWorldBounds.Height}");

            // Do NOT add synthetic pit walls here. Collision layer + generated obstacles will populate walls.
        }

        private Rectangle CalculatePitWorldBounds()
        {
            // Use dynamic pit bounds from PitWidthManager if available
            var pitWidthManager = Core.Services.GetService<PitWidthManager>();
            if (pitWidthManager != null)
            {
                return pitWidthManager.CalculateCurrentPitWorldBounds();
            }

            // Fallback to default static calculation
            var topLeftWorld = new Vector2(
                GameConfig.PitRectX * GameConfig.TileSize - GameConfig.PitColliderPadding,
                GameConfig.PitRectY * GameConfig.TileSize - GameConfig.PitColliderPadding
            );

            var bottomRightWorld = new Vector2(
                (GameConfig.PitRectX + GameConfig.PitRectWidth) * GameConfig.TileSize + GameConfig.PitColliderPadding,
                (GameConfig.PitRectY + GameConfig.PitRectHeight) * GameConfig.TileSize + GameConfig.PitColliderPadding
            );

            return new Rectangle(
                (int)topLeftWorld.X,
                (int)topLeftWorld.Y,
                (int)(bottomRightWorld.X - topLeftWorld.X),
                (int)(bottomRightWorld.Y - topLeftWorld.Y)
            );
        }

        /// <summary>
        /// Spawns the initial hero. A new game starts at the hero statue's feet without a state
        /// machine (the intro adds it when it ends); a loaded game spawns at tile (62, 6) as before.
        /// </summary>
        private Entity SpawnHero(bool isNewGame)
        {
            if (isNewGame)
                return CreateHeroEntity(GameConfig.HeroStatueStandTileX, GameConfig.HeroStatueStandTileY, addStateMachine: false);
            return CreateHeroEntity(62, 6);
        }

        /// <summary>True while the new-game intro sequence owns the HUD, input and hero</summary>
        public bool IsIntroActive => _newGameIntroService != null && _newGameIntroService.IsActive;

        /// <summary>
        /// Kicks off the new-game intro: hides the hero until the drop is posed, locks the
        /// presentation and launches the sequence coroutine.
        /// </summary>
        private void StartNewGameIntro(Entity hero)
        {
            if (hero == null)
                return;

            hero.GetComponent<MultiSpriteAnimator>()?.SetEnabled(false);
            BeginIntroPresentation();
            _newGameIntroService = new Services.NewGameIntroService(this, _cameraController);
            _newGameIntroService.Start(hero);
        }

        /// <summary>
        /// Hides every HUD element, locks input and parks the camera on the statue for the intro.
        /// The camera centre is latched and applied by the controller's deferred init.
        /// </summary>
        private void BeginIntroPresentation()
        {
            _graphicalHUD?.SetEnabled(false);
            _heroActionQueueViz?.SetEnabled(false);
            _pitLevelLabel?.SetVisible(false);
            _fundsLabel?.SetVisible(false);
            _clockLabel?.SetVisible(false);
            _settingsUI?.EnterIntroMode();

            if (_cameraController != null)
            {
                _cameraController.InputSuspended = true;
                _cameraController.CenterOnWorldPosition(new Vector2(
                    GameConfig.HeroStatueStandTileX * GameConfig.TileSize + GameConfig.TileSize / 2f,
                    GameConfig.HeroStatueStandTileY * GameConfig.TileSize + GameConfig.TileSize / 2f));
            }
        }

        /// <summary>
        /// Ends the intro presentation: restores the HUD and input, and gives the hero its GOAP state
        /// machine so it plans its first pit trip from the statue (the pit-adventure bubble fires
        /// naturally — the speech bubble component is long initialised by now).
        /// </summary>
        public void EndIntroPresentation(Entity hero)
        {
            _pitLevelLabel?.SetVisible(true);
            _fundsLabel?.SetVisible(true);
            _clockLabel?.SetVisible(true);
            _settingsUI?.ExitIntroMode();
            if (_cameraController != null)
                _cameraController.InputSuspended = false;

            if (hero != null && !hero.IsDestroyed && hero.GetComponent<HeroStateMachine>() == null)
                hero.AddComponent(new HeroStateMachine());
        }

        /// <summary>
        /// Places the hero and hired mercenaries directly into the inn beds after a night-time
        /// load, so they wake through the normal SleepInBedAction path instead of walking to the
        /// innkeeper first (issue #371).
        /// </summary>
        private void PositionPartyInBedsForNightLoad(Entity heroEntity, HeroComponent heroComp)
        {
            heroEntity.Transform.Position = new Vector2(
                GameConfig.InnHeroBedTileX * GameConfig.TileSize + GameConfig.TileSize / 2,
                GameConfig.InnHeroBedTileY * GameConfig.TileSize + GameConfig.TileSize / 2);
            heroEntity.GetComponent<TileByTileMover>()?.SnapToTileGrid();
            heroComp.IsSleeping = true;
            heroComp.SpawnedAsleepPending = true;

            var mercBedTiles = new Point[]
            {
                new Point(GameConfig.InnMercBed1TileX, GameConfig.InnMercBed1TileY),
                new Point(GameConfig.InnMercBed2TileX, GameConfig.InnMercBed2TileY),
            };

            var hiredMercenaries = Core.Services.GetService<MercenaryManager>()?.GetHiredMercenaries();
            for (int i = 0; hiredMercenaries != null && i < hiredMercenaries.Count && i < 2; i++)
            {
                var merc = hiredMercenaries[i];
                var bedTile = mercBedTiles[i];
                merc.Transform.Position = new Vector2(
                    bedTile.X * GameConfig.TileSize + GameConfig.TileSize / 2,
                    bedTile.Y * GameConfig.TileSize + GameConfig.TileSize / 2);
                merc.GetComponent<TileByTileMover>()?.SnapToTileGrid();

                var mercComp = merc.GetComponent<MercenaryComponent>();
                if (mercComp != null)
                {
                    mercComp.LastTilePosition = bedTile;
                    mercComp.InsidePit = false;
                    merc.GetComponent<PathfindingActorComponent>()?.RefreshPathfindingWithObstacles();
                }

                // Pre-add the follow component disabled so the merc stays in bed until the sleep
                // action's wake path re-enables it (FollowTargetAction only adds-if-missing and
                // never forces Enabled back on).
                var followComp = merc.GetComponent<MercenaryFollowComponent>();
                if (followComp == null)
                    followComp = merc.AddComponent(new MercenaryFollowComponent());
                followComp.Enabled = false;
            }

            // Freeze everyone into their sleep pose (closed eyes, paused animation) right away —
            // without this the party stands in the beds playing the open-eyed walk cycle until
            // the GOAP sleep action's coroutine catches up.
            Core.StartCoroutine(ApplySpawnSleepPoses(heroEntity, heroComp));

            Debug.Log("[MainGameScene] Night load — party spawned asleep in the inn beds");
        }

        /// <summary>
        /// Waits for the party's animation layers to initialize (the atlas loads in
        /// OnAddedToEntity, which can run after the load restore), then poses the hero and
        /// hired mercenaries asleep in their beds. Bails if the party already woke up
        /// (degenerate load right as the clock crosses 6 AM).
        /// </summary>
        private System.Collections.IEnumerator ApplySpawnSleepPoses(Entity heroEntity, HeroComponent heroComp)
        {
            for (int frame = 0; frame < 10; frame++)
            {
                yield return null;
                var anim = heroEntity.GetComponent<HeroAnimationComponent>();
                if (anim != null && anim.Animations != null && anim.Animations.Count > 0)
                    break;
            }

            if (!heroComp.IsSleeping)
                yield break;

            AI.SleepInBedAction.ApplySleepPose(heroEntity);

            var hiredMercenaries = Core.Services.GetService<MercenaryManager>()?.GetHiredMercenaries();
            for (int i = 0; hiredMercenaries != null && i < hiredMercenaries.Count && i < 2; i++)
                AI.SleepInBedAction.ApplySleepPose(hiredMercenaries[i]);
        }

        /// <summary>
        /// Creates a hero entity at the specified tile coordinates using HeroDesign for appearance.
        /// When needsCrystal is true, the hero spawns without a crystal and waits for the promotion ceremony.
        /// </summary>
        private Entity CreateHeroEntity(int tileX, int tileY, bool needsCrystal = false, bool addStateMachine = true)
        {
            var designService = Core.Services.GetService<HeroDesignService>();
            var design = designService.GetDesign();

            var heroStart = new Vector2(
                tileX * GameConfig.TileSize + GameConfig.TileSize / 2,
                tileY * GameConfig.TileSize + GameConfig.TileSize / 2
            );

            var hero = CreateEntity("hero").SetPosition(heroStart);
            hero.SetTag(GameConfig.TAG_HERO);

            Debug.Log($"[MainGameScene] Hero spawned at position {heroStart.X},{heroStart.Y}, tile coordinates: ({tileX}, {tileY})");

            // Add facing component first so animators can query it immediately
            hero.AddComponent(new ActorFacingComponent());

            // Add all paperdoll layer animators in the correct order (Hand2 to Hand1)
            var offset = new Vector2(0, -GameConfig.TileSize / 2); // Offset so feet are at entity position

            // Body layer
            var heroBodyAnimator = hero.AddComponent(new HeroBodyAnimationComponent(design.SkinColor));
            heroBodyAnimator.SetLocalOffset(offset);

            // Hand2 layer (top-most paperdoll layer)
            var heroHand2Animator = hero.AddComponent(new HeroHand2AnimationComponent(design.SkinColor));
            heroHand2Animator.SetLocalOffset(offset);
            heroHand2Animator.ComponentColor = design.SkinColor;

            // Pants layer
            var heroPantsAnimator = hero.AddComponent(new HeroPantsAnimationComponent(Color.White));
            heroPantsAnimator.SetLocalOffset(offset);

            // Shirt layer
            var heroShirtAnimator = hero.AddComponent(new HeroShirtAnimationComponent(design.ShirtColor));
            heroShirtAnimator.SetLocalOffset(offset);

            // Head layer
            var heroHeadAnimator = hero.AddComponent(new HeroHeadAnimationComponent(design.SkinColor));
            heroHeadAnimator.SetLocalOffset(offset);
            heroHeadAnimator.ComponentColor = design.SkinColor;

            // Eyes layer
            var heroEyesAnimator = hero.AddComponent(new HeroEyesAnimationComponent(Color.White));
            heroEyesAnimator.SetLocalOffset(offset);

            // Hair layer
            var heroHairAnimator = hero.AddComponent(new HeroHairAnimationComponent(design.HairColor, design.HairstyleIndex));
            heroHairAnimator.SetLocalOffset(offset);

            // Hand1 layer (bottom-most paperdoll layer)
            var heroHand1Animator = hero.AddComponent(new HeroHand1AnimationComponent(design.SkinColor));
            heroHand1Animator.SetLocalOffset(offset);
            heroHand1Animator.ComponentColor = design.SkinColor;

            // Composite all paperdoll layers into a single render target to prevent z-order artifacts
            var heroMultiAnimator = hero.AddComponent(new MultiSpriteAnimator(
                heroHand2Animator, heroBodyAnimator, heroPantsAnimator, heroShirtAnimator,
                heroHeadAnimator, heroEyesAnimator, heroHairAnimator, heroHand1Animator));
            heroMultiAnimator.SetRenderLayer(GameConfig.RenderLayerActors);

            // Add jump animation component for pit jumping animations
            var heroJumpController = hero.AddComponent(new HeroJumpComponent());
            var collider = hero.AddComponent(new BoxCollider(GameConfig.HeroWidth, GameConfig.HeroHeight));

            collider.IsTrigger = true; // Hero should not block mercenaries or other entities
            Flags.SetFlag(ref collider.CollidesWithLayers, GameConfig.PhysicsTileMapLayer);
            Flags.SetFlag(ref collider.CollidesWithLayers, GameConfig.PhysicsPitLayer);
            Flags.SetFlagExclusive(ref collider.PhysicsLayer, GameConfig.PhysicsHeroWorldLayer);

            hero.AddComponent(new TileByTileMover());
            var tileMover = hero.GetComponent<TileByTileMover>();
            tileMover.MovementSpeed = GameConfig.HeroMovementSpeed;
            Debug.Log("[MainGameScene] Added TileByTileMover to hero for tile-based movement");

            var heroComponent = hero.AddComponent(new HeroComponent
            {
                Health = 25,
                MaxHealth = 25,
                PitInitialized = true
            });

            if (!needsCrystal)
            {
                // Initialize HeroCrystal for crystal-infused stats (normal spawn)
                var heroJob = JobFactory.CreateJob(design.JobName);
                var baseStats = new StatBlock(strength: 4, agility: 3, vitality: 5, magic: 1);
                var heroCrystal = new HeroCrystal(design.Name, heroJob, 1, baseStats);

                // Create the linked Hero from the crystal
                heroComponent.LinkedHero = new RolePlayingFramework.Heroes.Hero(design.Name, heroJob, 1, baseStats, heroCrystal);

                // Starting items are granted only for a brand-new game — loads restore their saved
                // inventory instead, and death-respawns (needsCrystal) keep the existing bag.
                heroComponent.GrantNewGameStartingItems = SaveLoadService.PendingLoadData == null;

                Debug.Log($"[MainGameScene] Created hero '{design.Name}' with Level {heroComponent.LinkedHero.Level}, HP {heroComponent.LinkedHero.CurrentHP}/{heroComponent.LinkedHero.MaxHP}");
            }
            else
            {
                // Hero respawned without a crystal — will receive one at the statue
                heroComponent.NeedsCrystal = true;
                Debug.Log($"[MainGameScene] Hero '{design.Name}' respawned without crystal — walking to statue for crystal ceremony");
            }

            // Add BouncyDigitComponent for damage display (RenderLayerUI, disabled initially)
            var heroBouncyDigit = hero.AddComponent(new BouncyDigitComponent());
            heroBouncyDigit.SetRenderLayer(GameConfig.RenderLayerLowest);
            heroBouncyDigit.SetEnabled(false);

            // Add BouncyTextComponent for miss display (RenderLayerUI, disabled initially)
            var heroBouncyText = hero.AddComponent(new BouncyTextComponent());
            heroBouncyText.SetRenderLayer(GameConfig.RenderLayerLowest);
            heroBouncyText.SetEnabled(false);

            hero.AddComponent(new Historian());
            // The new-game intro adds the state machine when it ends: adding it here would plan the
            // first pit trip immediately (Idle_Enter runs inside the deferred OnAddedToEntity).
            if (addStateMachine)
                hero.AddComponent(new HeroStateMachine());
            hero.AddComponent(new SpeechBubbleComponent());
            hero.AddComponent(new CharacterSelectorComponent());

            // Wait for pathfinding initialization then add obstacles
            Core.StartCoroutine(AddObstaclesAfterPathfindingReady(hero));

            return hero;
        }

        /// <summary>
        /// Coroutine that waits for the specified delay then respawns the hero
        /// </summary>
        public System.Collections.IEnumerator RespawnHeroAfterDelay(float delay)
        {
            float elapsed = 0f;
            while (elapsed < delay)
            {
                elapsed += Time.DeltaTime;
                yield return null;
            }

            RespawnHero();
        }

        /// <summary>
        /// Respawns the hero at the hero statue location (112, 8) after death.
        /// The hero spawns without a crystal and must walk to the statue for the crystal ceremony.
        /// </summary>
        private void RespawnHero()
        {
            // Reset the pit cycle before the crystal ceremony so the new hero's spawn level
            // is computed from a fresh TierBaseLevel, not the dead run's progression.
            Core.Services.GetService<PitWidthManager>()?.ResetTierForNewCycle();

            CreateHeroEntity(34, 6, needsCrystal: true);

            // Disable save while hero walks to statue — saving in this transitional state puts the game in an odd state
            Core.Services.GetService<SettingsUI>()?.SetSaveEnabled(false);

            // Unfreeze and reassign mercenaries to follow the new hero
            var mercenaryManager = Core.Services.GetService<MercenaryManager>();
            if (mercenaryManager != null)
            {
                var heroEntity = FindEntity("hero");
                if (heroEntity != null)
                {
                    mercenaryManager.UnblockHiring();
                    mercenaryManager.UnfreezeAndReassignMercenaries(heroEntity);
                    Debug.Log("[MainGameScene] Unblocked hiring and reassigned mercenaries to respawned hero");
                }
            }

            Core.StartCoroutine(WaitForAllMercenariesToExitPitThenReset());

            Debug.Log("[MainGameScene] Hero respawned at tile (34, 6) — awaiting crystal ceremony");
        }

        /// <summary>
        /// Waits until all hired mercenaries have exited the pit (or a safety timeout elapses),
        /// then resets the pit back to level 1.
        /// </summary>
        private System.Collections.IEnumerator WaitForAllMercenariesToExitPitThenReset()
        {
            var mercenaryManager = Core.Services.GetService<MercenaryManager>();
            if (mercenaryManager == null)
            {
                ResetPitToLevelOne();
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < GameConfig.MercenaryExitPitTimeoutSeconds)
            {
                if (mercenaryManager.AreAllHiredMercenariesOutOfPit())
                    break;
                elapsed += Time.DeltaTime;
                yield return null;
            }

            if (elapsed >= GameConfig.MercenaryExitPitTimeoutSeconds)
                Debug.Warn("[MainGameScene] Timed out waiting for mercenaries to exit pit — resetting anyway.");

            ResetPitToLevelOne();
        }

        /// <summary>
        /// Starts the wait-for-mercenaries-then-reset-pit coroutine. Called by the crystal
        /// ceremony when a manual job change completes (the death path starts it from RespawnHero).
        /// </summary>
        public void StartPitResetForNewCycle()
        {
            Core.StartCoroutine(WaitForAllMercenariesToExitPitThenReset());
        }

        /// <summary>
        /// Resets the pit level back to 1, shrinking its width and regenerating level-1 content.
        /// </summary>
        private void ResetPitToLevelOne()
        {
            var pitManager = Core.Services.GetService<PitWidthManager>();
            if (pitManager == null)
            {
                Debug.Warn("[MainGameScene] PitWidthManager service not found — cannot reset pit.");
                return;
            }

            Debug.Log($"[MainGameScene] Resetting pit from level {pitManager.CurrentPitLevel} (tier {pitManager.CurrentPitTier}) to level 1, tier 1 after hero death.");
            pitManager.ResetTierForNewCycle();
            pitManager.SetPitLevel(1);
        }

        /// <summary>
        /// Coroutine to wait for hero pathfinding to be ready, then add existing obstacles
        /// </summary>
        private System.Collections.IEnumerator AddObstaclesAfterPathfindingReady(Entity hero)
        {
            var heroComponent = hero.GetComponent<HeroComponent>();

            // Wait until pathfinding is initialized
            while (heroComponent != null && !heroComponent.IsPathfindingInitialized)
            {
                yield return null; // Wait one frame
            }

            // Now add existing obstacles to the pathfinding graph
            AddExistingObstaclesToHeroPathfinding(hero);
        }

        /// <summary>
        /// Add all existing obstacle entities to the hero's pathfinding graph
        /// This is needed when hero is spawned after obstacles are already created
        /// </summary>
        private void AddExistingObstaclesToHeroPathfinding(Entity hero)
        {
            var heroComponent = hero.GetComponent<HeroComponent>();
            if (heroComponent == null || !heroComponent.IsPathfindingInitialized)
            {
                Debug.Warn("[MainGameScene] Hero pathfinding not initialized when adding existing obstacles");
                return;
            }

            // Find all existing obstacle entities
            var obstacles = FindEntitiesWithTag(GameConfig.TAG_OBSTACLE);
            var addedWalls = 0;

            for (int i = 0; i < obstacles.Count; i++)
            {
                var obstacle = obstacles[i];
                // Calculate tile position from world position
                var worldPos = obstacle.Transform.Position;
                var tileX = (int)(worldPos.X / GameConfig.TileSize);
                var tileY = (int)(worldPos.Y / GameConfig.TileSize);
                var tilePos = new Point(tileX, tileY);

                // Add wall to hero's pathfinding graph
                heroComponent.AddWall(tilePos);
                addedWalls++;
            }

            Debug.Log($"[MainGameScene] Added {addedWalls} existing obstacle walls to hero pathfinding graph");
        }

        /// <summary>
        /// Spawn the hero statue sprite anchored at GameConfig.HeroStatueTileX/Y (112, 3). The
        /// 181px-tall sprite's base lands on row 6, so heroes stand at HeroStatueStandTileX/Y (112, 6).
        /// </summary>
        private void SpawnHeroStatue()
        {
            var tileX = GameConfig.HeroStatueTileX;
            var tileY = GameConfig.HeroStatueTileY;

            var worldPos = new Vector2(
                tileX * GameConfig.TileSize + GameConfig.TileSize / 2,
                tileY * GameConfig.TileSize + GameConfig.TileSize / 2
            );

            var statueEntity = CreateEntity("hero-statue");
            statueEntity.SetTag(GameConfig.TAG_HERO_STATUE);
            statueEntity.SetPosition(worldPos);

            // Load sprite from Actors.atlas
            var actorsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/Actors.atlas");
            if (actorsAtlas != null)
            {
                var statueSprite = actorsAtlas.GetSprite("HeroStatue");
                if (statueSprite != null)
                {
                    var renderer = statueEntity.AddComponent(new YSortSpriteRenderer(statueSprite));
                    renderer.SetRenderLayer(GameConfig.RenderLayerActors);
                    Debug.Log($"[MainGameScene] Hero statue spawned at tile ({tileX}, {tileY}) with HeroStatue sprite");
                }
                else
                {
                    Debug.Warn("[MainGameScene] HeroStatue sprite not found in Actors.atlas");
                }
            }
            else
            {
                Debug.Error("[MainGameScene] Failed to load Actors.atlas for hero statue");
            }
        }

        /// <summary>
        /// Spawn the innkeeper at tile coordinate (69, 3) facing left
        /// </summary>
        private void SpawnInnkeeper()
        {
            var tileX = GameConfig.InnkeeperTileX;
            var tileY = GameConfig.InnkeeperTileY;

            var worldPos = new Vector2(
                tileX * GameConfig.TileSize + GameConfig.TileSize / 2,
                tileY * GameConfig.TileSize + GameConfig.TileSize / 2
            );

            var innkeeperEntity = CreateEntity("innkeeper");
            innkeeperEntity.SetTag(GameConfig.TAG_INNKEEPER);
            innkeeperEntity.SetPosition(worldPos);

            // Add facing component and set to face left
            var facingComponent = innkeeperEntity.AddComponent(new ActorFacingComponent());
            facingComponent.SetFacing(Direction.Left);

            // Add animation components (similar to hero/mercenary)
            var offset = new Vector2(0, -GameConfig.TileSize / 2);

            // Use a distinct color scheme for innkeeper
            var bodyColor = new Color(251, 200, 178); // Fair skin tone
            var bodyAnimator = innkeeperEntity.AddComponent(new HeroBodyAnimationComponent(bodyColor));
            bodyAnimator.SetLocalOffset(offset);

            var hand2Animator = innkeeperEntity.AddComponent(new HeroHand2AnimationComponent(bodyColor));
            hand2Animator.SetLocalOffset(offset);

            var pantsAnimator = innkeeperEntity.AddComponent(new HeroPantsAnimationComponent(Color.White));
            pantsAnimator.SetLocalOffset(offset);

            // Use a distinctive shirt color for innkeeper (brown/beige for apron-like appearance)
            var shirtColor = new Color(140, 91, 62); // Brown
            var shirtAnimator = innkeeperEntity.AddComponent(new HeroShirtAnimationComponent(shirtColor));
            shirtAnimator.SetLocalOffset(offset);

            var headAnimator = innkeeperEntity.AddComponent(new HeroHeadAnimationComponent(bodyColor));
            headAnimator.SetLocalOffset(offset);

            var eyesAnimator = innkeeperEntity.AddComponent(new HeroEyesAnimationComponent(Color.White));
            eyesAnimator.SetLocalOffset(offset);

            // Gray hair for older innkeeper appearance
            var hairColor = new Color(100, 100, 100); // Gray
            var hairAnimator = innkeeperEntity.AddComponent(new HeroHairAnimationComponent(hairColor, hairstyleIndex: 1)); // Use default hairstyle for innkeeper
            hairAnimator.SetLocalOffset(offset);

            var hand1Animator = innkeeperEntity.AddComponent(new HeroHand1AnimationComponent(bodyColor));
            hand1Animator.SetLocalOffset(offset);

            var innkeeperMultiAnimator = innkeeperEntity.AddComponent(new MultiSpriteAnimator(
                hand2Animator, bodyAnimator, pantsAnimator, shirtAnimator,
                headAnimator, eyesAnimator, hairAnimator, hand1Animator));
            innkeeperMultiAnimator.SetRenderLayer(GameConfig.RenderLayerActors);

            innkeeperEntity.AddComponent(new SpeechBubbleComponent());

            Debug.Log($"[MainGameScene] Innkeeper spawned at tile ({tileX}, {tileY}) facing left");
        }

        private void SetupUIOverlay()
        {
            var screenSpaceRenderer = new ScreenSpaceRenderer(100, [GameConfig.RenderLayerSpeechBubble, GameConfig.TransparentPauseOverlay, GameConfig.RenderLayerUI, GameConfig.RenderLayerGraphicalHUD, GameConfig.RenderLayerActionQueue]);
            AddRenderer(screenSpaceRenderer);

            // Create pause overlay entity
            _pauseOverlayEntity = CreateEntity("pause-overlay");
            _pauseOverlayEntity.SetPosition(0, 0); // Top-left corner

            // Size to the render target (stage space) with margin; resized on stage-size change.
            // Under FixedHeight the render target can be wider than the backbuffer, so Screen.* is
            // not a safe stand-in for stage dimensions.
            var pauseOverlay = _pauseOverlayEntity.AddComponent(
                new PrototypeSpriteRenderer(SceneRenderTargetSize.X * 2, SceneRenderTargetSize.Y * 2)
            );
            pauseOverlay.SetOrigin(Vector2.Zero); // or pauseOverlay.SetOriginNormalized(Vector2.Zero);
            pauseOverlay.SetColor(new Color(0, 0, 0, 100));
            pauseOverlay.SetRenderLayer(GameConfig.TransparentPauseOverlay);
            _pauseOverlayRenderer = pauseOverlay;
            _pauseOverlayEntity.SetEnabled(false); // Initially hidden

            var uiEntity = CreateEntity("ui-overlay");
            var uiCanvas = uiEntity.AddComponent(new UICanvas());
            uiCanvas.IsFullScreen = false;
            uiCanvas.RenderLayer = GameConfig.RenderLayerUI;
            _uiStage = uiCanvas.Stage;
            // Note: _tillModeOverlay is created later in LoadMap (during Begin) and wires itself
            // to _uiStage there. Do not call SetStage here — the overlay does not exist yet.

            _settingsUI = new SettingsUI(Core.Instance);
            _settingsUI.InitializeUI(uiCanvas.Stage);
            // Create auto-hide marker entities on THIS scene. During Scene.Initialize() (where this
            // runs) Core.Scene still points at the previous scene, so pass the scene explicitly.
            _settingsUI.CreateMarkers(this);
            Core.Services.AddService(_settingsUI);
            // Remove duplicate HeroUI creation - it's already handled by SettingsUI
            // Initialize HeroUI for pit priority management
            // _heroUI = new HeroUI();
            // _heroUI.InitializeUI(uiCanvas.Stage);
            // Position the Hero button in the bottom-left corner  
            // _heroUI.SetPosition(10f, Screen.Height - _heroUI.GetHeight() - 10f);

            // Pit level label (bottom-left, always visible, no scaling)
            _pitLevelLabel = uiCanvas.Stage.AddElement(new Label("Pit Lv. 1", _hudFontNormal));
            _pitLevelLabel.SetStyle(_pitLevelStyleNormal);
            _pitLevelLabel.SetPosition(PitLabelBaseX, HudLabelY());

            // Funds label (bottom-left next to Pit Lv, always visible, no scaling)
            _fundsLabel = uiCanvas.Stage.AddElement(new Label("Gold: 0", _hudFontNormal));
            _fundsLabel.SetStyle(_pitLevelStyleNormal);
            RepositionFundsLabel();

            // Clock label (upper-right, position adjusted dynamically based on text width)
            _clockLabel = uiCanvas.Stage.AddElement(new Label("6:00 AM", _hudFontNormal));
            _clockLabel.SetStyle(_pitLevelStyleNormal);

            // Tilling label (upper area, centered between button bar and clock — visible only in till mode)
            string tillingText = Core.Services.GetService<TextService>()?.DisplayText(TextType.UI, UITextKey.LabelTillingSoil) ?? "Tilling Soil";
            _tillingLabel = uiCanvas.Stage.AddElement(new Label(tillingText, _hudFontNormal));
            _tillingLabel.SetStyle(_modeStyleNormal);
            _tillingLabel.SetVisible(false);

            // Planting label (same area, visible only during the Placing sub-state of seed mode)
            string plantingText = Core.Services.GetService<TextService>()?.DisplayText(TextType.UI, UITextKey.LabelPlantingCrops) ?? "Planting Crops";
            _plantingCropsLabel = uiCanvas.Stage.AddElement(new Label(plantingText, _hudFontNormal));
            _plantingCropsLabel.SetStyle(_modeStyleNormal);
            _plantingCropsLabel.SetVisible(false);

            // Restoring Grass label (same area, visible only in restore-grass mode)
            string restoringText = Core.Services.GetService<TextService>()?.DisplayText(TextType.UI, UITextKey.LabelRestoringGrass) ?? "Restoring Grass";
            _restoringGrassLabel = uiCanvas.Stage.AddElement(new Label(restoringText, _hudFontNormal));
            _restoringGrassLabel.SetStyle(_modeStyleNormal);
            _restoringGrassLabel.SetVisible(false);

            // Create graphical HUD entity to display HP/MP/Level
            var hudEntity = CreateEntity("graphical-hud");
            hudEntity.SetPosition(GraphicalHudBaseX, GraphicalHudY());
            _graphicalHUD = hudEntity.AddComponent(new GraphicalHUD());
            _graphicalHUD.SetRenderLayer(GameConfig.RenderLayerGraphicalHUD); // Use screen space renderer

            // Create mercenary #1 HUD entity
            var merc1HudEntity = CreateEntity("mercenary1-hud");
            merc1HudEntity.SetPosition(GraphicalHudBaseX + GraphicalHudSpacing, GraphicalHudY());
            _mercenary1HUD = merc1HudEntity.AddComponent(new GraphicalHUD());
            _mercenary1HUD.SetRenderLayer(GameConfig.RenderLayerGraphicalHUD);
            _mercenary1HUD.SetEnabled(false); // Initially hidden until mercenary is hired

            // Create mercenary #2 HUD entity
            var merc2HudEntity = CreateEntity("mercenary2-hud");
            merc2HudEntity.SetPosition(GraphicalHudBaseX + GraphicalHudSpacing * 2, GraphicalHudY());
            _mercenary2HUD = merc2HudEntity.AddComponent(new GraphicalHUD());
            _mercenary2HUD.SetRenderLayer(GameConfig.RenderLayerGraphicalHUD);
            _mercenary2HUD.SetEnabled(false); // Initially hidden until mercenary is hired

            // Create screen-space action visualization entities anchored on the HUD heads (active action);
            // their waiting queues are offset left over the HP bar and raised above the panel so
            // they never cover the HP display.
            var heroVizEntity = CreateEntity("hero-action-queue-viz");
            heroVizEntity.SetPosition(GraphicalHudBaseX + HudQueueXOffset, GraphicalHudY() + HudQueueYOffset);
            _heroActionQueueViz = heroVizEntity.AddComponent(new ActionQueueVisualizationComponent());
            _heroActionQueueViz.SetRenderLayer(GameConfig.RenderLayerActionQueue);
            _heroActionQueueViz.QueuedActionXOffset = HudQueuedActionXOffset;
            _heroActionQueueViz.QueuedActionYOffset = HudQueuedActionYOffset;

            var merc1VizEntity = CreateEntity("merc1-action-queue-viz");
            merc1VizEntity.SetPosition(GraphicalHudBaseX + GraphicalHudSpacing + HudQueueXOffset, GraphicalHudY() + HudQueueYOffset);
            _merc1ActionQueueViz = merc1VizEntity.AddComponent(new ActionQueueVisualizationComponent());
            _merc1ActionQueueViz.SetRenderLayer(GameConfig.RenderLayerActionQueue);
            _merc1ActionQueueViz.QueuedActionXOffset = HudQueuedActionXOffset;
            _merc1ActionQueueViz.QueuedActionYOffset = HudQueuedActionYOffset;
            _merc1ActionQueueViz.SetEnabled(false);

            var merc2VizEntity = CreateEntity("merc2-action-queue-viz");
            merc2VizEntity.SetPosition(GraphicalHudBaseX + GraphicalHudSpacing * 2 + HudQueueXOffset, GraphicalHudY() + HudQueueYOffset);
            _merc2ActionQueueViz = merc2VizEntity.AddComponent(new ActionQueueVisualizationComponent());
            _merc2ActionQueueViz.SetRenderLayer(GameConfig.RenderLayerActionQueue);
            _merc2ActionQueueViz.QueuedActionXOffset = HudQueuedActionXOffset;
            _merc2ActionQueueViz.QueuedActionYOffset = HudQueuedActionYOffset;
            _merc2ActionQueueViz.SetEnabled(false);

            // Shortcut bar at bottom center
            _shortcutBar = new ShortcutBar();
            _shortcutBar.EnableTooltips(PitHeroSkin.CreateSkin());
            uiCanvas.Stage.AddElement(_shortcutBar);
            PositionShortcutBar();

            // Register shortcut bar service so AI actions can find it
            var shortcutBarService = new ShortcutBarService();
            shortcutBarService.SetShortcutBar(_shortcutBar);
            Core.Services.AddService(shortcutBarService);

            // Let SettingsUI manage the shortcut bar hide/show animation
            _settingsUI?.SetShortcutBar(_shortcutBar);

            // Mercenary hire dialog
            _mercenaryHireDialog = new MercenaryHireDialog();
            uiCanvas.Stage.AddElement(_mercenaryHireDialog);

            // Event console panel (lower-right corner)
            var eventService = Core.Services.GetService<Services.GameEventService>();
            if (eventService != null)
            {
                var consoleSkin = PitHeroSkin.CreateSkin();
                _eventConsolePanel = new EventConsolePanel(consoleSkin, eventService);
                _eventConsolePanel.SetSize(480f, 120f);
                uiCanvas.Stage.AddElement(_eventConsolePanel);
                PositionEventConsolePanel();
                _settingsUI?.SetEventConsolePanel(_eventConsolePanel);
            }
        }

        private void AddPitLevelTestComponent()
        {
#if DEBUG
            var testEntity = CreateEntity("pit-level-test");
            testEntity.AddComponent(new PitLevelTestComponent());
            Debug.Log("[MainGameScene] Added PitLevelTestComponent - Press number keys 0-9 to test pit level changes");
#endif
        }

        /// <summary>
        /// Update the pit collider bounds to match the current dynamic pit width
        /// </summary>
        public void UpdatePitColliderBounds()
        {
            var pitEntity = FindEntity("pit");
            if (pitEntity == null)
            {
                Debug.Error("[MainGameScene] Could not find pit entity to update collider bounds");
                return;
            }

            var pitCollider = pitEntity.GetComponent<BoxCollider>();
            if (pitCollider == null)
            {
                Debug.Error("[MainGameScene] Pit entity missing BoxCollider component");
                return;
            }

            // Calculate new pit bounds
            var newPitBounds = CalculatePitWorldBounds();

            // Update collider size
            pitCollider.SetWidth(newPitBounds.Width);
            pitCollider.SetHeight(newPitBounds.Height);

            // Update pit entity position to center of new bounds
            pitEntity.SetPosition(newPitBounds.Center.ToVector2());

            Debug.Log($"[MainGameScene] Updated pit collider bounds: X={newPitBounds.X}, Y={newPitBounds.Y}, Width={newPitBounds.Width}, Height={newPitBounds.Height}");
        }

        /// <summary>
        /// Update pit level label text when the pit level or tier changes
        /// </summary>
        private void UpdatePitLevelLabel()
        {
            if (_pitLevelLabel == null)
                return;

            var pitWidthManager = Core.Services.GetService<PitWidthManager>();
            if (pitWidthManager == null)
                return;

            var currentLevel = pitWidthManager.CurrentPitLevel;
            var currentTier = pitWidthManager.CurrentPitTier;
            if (currentLevel != _lastDisplayedPitLevel || currentTier != _lastDisplayedPitTier)
            {
                if (currentTier >= 2)
                    _pitLevelLabel.SetText($"Pit Lv. {currentLevel}({currentTier})");
                else
                    _pitLevelLabel.SetText($"Pit Lv. {currentLevel}");
                _lastDisplayedPitLevel = currentLevel;
                _lastDisplayedPitTier = currentTier;
                RepositionFundsLabel();
            }
        }

        /// <summary>
        /// Y for the bottom-left HUD labels, derived from the live stage height so they follow the
        /// configured design height (GameConfig.VirtualHeight) and every window/dock mode.
        /// </summary>
        private float HudLabelY() => PitLabelBaseY;

        /// <summary>
        /// Y for the hero/mercenary HUD panels. Bottom-anchored, so unlike the old fixed top position
        /// it has to be recomputed whenever the stage height changes.
        /// </summary>
        private float GraphicalHudY()
        {
            float stageH = _uiStage != null ? _uiStage.GetHeight() : GameConfig.VirtualHeight;
            return stageH - GraphicalHudHeight - GraphicalHudBottomMargin;
        }

        /// <summary>
        /// Re-anchor the top-left HUD labels (Pit Lv / Gold) after a stage size change
        /// </summary>
        private void RepositionHudLabels()
        {
            if (_pitLevelLabel != null)
                _pitLevelLabel.SetPosition(PitLabelBaseX, HudLabelY());
            RepositionFundsLabel();
        }

        /// <summary>
        /// Position the Funds label just right of the Pit Lv label based on its measured text width
        /// </summary>
        private void RepositionFundsLabel()
        {
            if (_fundsLabel == null || _pitLevelLabel == null || _hudFontNormal == null)
                return;

            float pitLabelWidth = _hudFontNormal.MeasureString(_pitLevelLabel.GetText()).X;
            _fundsLabel.SetPosition(PitLabelBaseX + pitLabelWidth + FundsLabelGapX, HudLabelY());
        }

        /// <summary>
        /// Update funds label text when the funds change
        /// </summary>
        private void UpdateFundsLabel()
        {
            if (_fundsLabel == null)
                return;

            var gameState = Core.Services.GetService<GameStateService>();
            if (gameState == null)
                return;

            var currentFunds = gameState.Funds;
            if (currentFunds != _lastDisplayedFunds)
            {
                _fundsLabel.SetText($"Gold: {currentFunds}");
                _lastDisplayedFunds = currentFunds;
            }
        }

        private void UpdateClockLabel()
        {
            if (_clockLabel == null || _hudFontNormal == null) return;
            var timeService = Core.Services.GetService<InGameTimeService>();
            if (timeService == null) return;
            string text = timeService.FormatTime();
            _clockLabel.SetText(text);
            float labelWidth = _hudFontNormal.MeasureString(text).X;
            _clockLabel.SetPosition(_uiStage.GetWidth() - labelWidth - ClockLabelRightPadding, ClockLabelBaseY);
        }

        private void UpdateTillingLabel()
        {
            if (_tillingLabel == null || _hudFontNormal == null) return;
            bool inTillMode = _settingsUI?.IsTillModeActive ?? false;
            _tillingLabel.SetVisible(inTillMode);
            if (!inTillMode) return;

            float alpha = (float)Math.Sin(Time.TotalTime * Math.PI * 1.2f) * 0.5f + 0.5f;
            _tillingLabel.SetFontColor(new Color(0, 255, 255, (int)(alpha * 255)));

            // Measure the label text directly from the label so we stay in sync with the localized string.
            string labelText = _tillingLabel.GetText();
            float tillingWidth = _hudFontNormal.MeasureString(labelText).X;

            // Clock left edge
            string timeText = Core.Services.GetService<InGameTimeService>()?.FormatTime() ?? "6:00 AM";
            float clockWidth = _hudFontNormal.MeasureString(timeText).X;
            float clockX = _uiStage.GetWidth() - clockWidth - ClockLabelRightPadding;

            // Button bar right edge (exposed by SettingsUI; falls back to 0 before first PositionUI)
            float barRight = _settingsUI?.UIBarRight ?? 0f;

            // Center the label in the gap between the button bar and the clock
            float midX = (barRight + clockX) / 2f;
            _tillingLabel.SetPosition(midX - tillingWidth / 2f, ClockLabelBaseY);
        }

        /// <summary>Shows and animates the "Restoring Grass" label while restore-grass mode is active.</summary>
        private void UpdateRestoringGrassLabel()
        {
            if (_restoringGrassLabel == null || _hudFontNormal == null) return;
            bool inRestoreGrassMode = _settingsUI?.IsRestoreGrassModeActive ?? false;
            _restoringGrassLabel.SetVisible(inRestoreGrassMode);
            if (!inRestoreGrassMode) return;

            float alpha = (float)Math.Sin(Time.TotalTime * Math.PI * 1.2f) * 0.5f + 0.5f;
            _restoringGrassLabel.SetFontColor(new Color(0, 255, 255, (int)(alpha * 255)));

            string labelText = _restoringGrassLabel.GetText();
            float labelWidth = _hudFontNormal.MeasureString(labelText).X;
            string timeText = Core.Services.GetService<InGameTimeService>()?.FormatTime() ?? "6:00 AM";
            float clockWidth = _hudFontNormal.MeasureString(timeText).X;
            float clockX = _uiStage.GetWidth() - clockWidth - ClockLabelRightPadding;
            float barRight = _settingsUI?.UIBarRight ?? 0f;
            float midX = (barRight + clockX) / 2f;
            _restoringGrassLabel.SetPosition(midX - labelWidth / 2f, ClockLabelBaseY);
        }

        /// <summary>Shows and animates the "Planting Crops" label while the player is in the placing sub-state.</summary>
        private void UpdatePlantingCropsLabel()
        {
            if (_plantingCropsLabel == null || _hudFontNormal == null) return;
            bool inPlacingState = (_settingsUI?.IsSeedModeActive ?? false) && (_seedModeOverlay?.IsInPlacingState ?? false);
            _plantingCropsLabel.SetVisible(inPlacingState);
            if (!inPlacingState) return;

            float alpha = (float)Math.Sin(Time.TotalTime * Math.PI * 1.2f) * 0.5f + 0.5f;
            _plantingCropsLabel.SetFontColor(new Color(0, 255, 255, (int)(alpha * 255)));

            string labelText = _plantingCropsLabel.GetText();
            float labelWidth = _hudFontNormal.MeasureString(labelText).X;
            string timeText = Core.Services.GetService<InGameTimeService>()?.FormatTime() ?? "6:00 AM";
            float clockWidth = _hudFontNormal.MeasureString(timeText).X;
            float clockX = _uiStage.GetWidth() - clockWidth - ClockLabelRightPadding;
            float barRight = _settingsUI?.UIBarRight ?? 0f;
            float midX = (barRight + clockX) / 2f;
            _plantingCropsLabel.SetPosition(midX - labelWidth / 2f, ClockLabelBaseY);
        }

        /// <summary>
        /// Update graphical HUD with current hero stats
        /// </summary>
        private void UpdateHeroHUD()
        {
            if (_graphicalHUD == null)
                return;

            var hero = FindEntity("hero");
            if (hero == null)
            {
                // Hero doesn't exist - hide hero HUD and action queue viz
                _graphicalHUD.SetEnabled(false);
                if (_heroActionQueueViz != null) _heroActionQueueViz.SetEnabled(false);
                return;
            }

            var heroComponent = hero.GetComponent<HeroComponent>();
            if (heroComponent?.LinkedHero == null)
            {
                _graphicalHUD.SetEnabled(false);
                if (_heroActionQueueViz != null) _heroActionQueueViz.SetEnabled(false);
                return;
            }

            // Check if hero has HeroDeathComponent - if so, hero is dead
            if (hero.HasComponent<HeroDeathComponent>())
            {
                _graphicalHUD.SetEnabled(false);
                if (_heroActionQueueViz != null) _heroActionQueueViz.SetEnabled(false);
                return;
            }

            var linkedHero = heroComponent.LinkedHero;

            // Hero is alive - show and update HUD
            _graphicalHUD.SetEnabled(true);
            _graphicalHUD.SetHeroEntity(hero);
            _graphicalHUD.SetThreatTarget(ReferenceEquals(linkedHero, HeroStateMachine.CurrentThreatTarget));
            _graphicalHUD.UpdateValues(
                linkedHero.CurrentHP,
                linkedHero.MaxHP,
                linkedHero.CurrentMP,
                linkedHero.MaxMP,
                linkedHero.Level
            );

            // Wire up hero action queue visualization with hero component
            if (_heroActionQueueViz != null)
            {
                _heroActionQueueViz.SetHeroComponent(heroComponent);
                _heroActionQueueViz.SetEnabled(true);
            }

            // Update mercenary HUDs
            UpdateMercenaryHUDs();
        }

        /// <summary>
        /// Update graphical HUDs for hired mercenaries
        /// </summary>
        private void UpdateMercenaryHUDs()
        {
            var mercenaryManager = Core.Services.GetService<MercenaryManager>();
            if (mercenaryManager == null)
            {
                // No mercenary manager - hide all mercenary HUDs and action queue vizs
                if (_mercenary1HUD != null) _mercenary1HUD.SetEnabled(false);
                if (_mercenary2HUD != null) _mercenary2HUD.SetEnabled(false);
                if (_merc1ActionQueueViz != null) _merc1ActionQueueViz.SetEnabled(false);
                if (_merc2ActionQueueViz != null) _merc2ActionQueueViz.SetEnabled(false);
                return;
            }

            var hiredMercenaries = mercenaryManager.GetHiredMercenaries();

            // Update mercenary #1 HUD
            if (hiredMercenaries.Count >= 1 && _mercenary1HUD != null)
            {
                var merc1Entity = hiredMercenaries[0];
                var merc1Component = merc1Entity.GetComponent<MercenaryComponent>();

                if (merc1Component?.LinkedMercenary != null && merc1Component.LinkedMercenary.CurrentHP > 0)
                {
                    _mercenary1HUD.SetEnabled(true);
                    _mercenary1HUD.SetHeroEntity(merc1Entity);
                    _mercenary1HUD.SetThreatTarget(ReferenceEquals(merc1Component.LinkedMercenary, HeroStateMachine.CurrentThreatTarget));
                    _mercenary1HUD.UpdateValues(
                        merc1Component.LinkedMercenary.CurrentHP,
                        merc1Component.LinkedMercenary.MaxHP,
                        merc1Component.LinkedMercenary.CurrentMP,
                        merc1Component.LinkedMercenary.MaxMP,
                        merc1Component.LinkedMercenary.Level
                    );

                    // Wire up action queue viz for mercenary #1
                    if (_merc1ActionQueueViz != null)
                    {
                        merc1Component.ActionQueueVisualization = _merc1ActionQueueViz;
                        _merc1ActionQueueViz.SetMercenaryComponent(merc1Component);
                        _merc1ActionQueueViz.SetEnabled(true);
                    }
                }
                else
                {
                    // Mercenary is dead or invalid
                    _mercenary1HUD.SetEnabled(false);
                    if (_merc1ActionQueueViz != null) _merc1ActionQueueViz.SetEnabled(false);
                }
            }
            else
            {
                // No mercenary #1 hired
                if (_mercenary1HUD != null) _mercenary1HUD.SetEnabled(false);
                if (_merc1ActionQueueViz != null) _merc1ActionQueueViz.SetEnabled(false);
            }

            // Update mercenary #2 HUD
            if (hiredMercenaries.Count >= 2 && _mercenary2HUD != null)
            {
                var merc2Entity = hiredMercenaries[1];
                var merc2Component = merc2Entity.GetComponent<MercenaryComponent>();

                if (merc2Component?.LinkedMercenary != null && merc2Component.LinkedMercenary.CurrentHP > 0)
                {
                    _mercenary2HUD.SetEnabled(true);
                    _mercenary2HUD.SetHeroEntity(merc2Entity);
                    _mercenary2HUD.SetThreatTarget(ReferenceEquals(merc2Component.LinkedMercenary, HeroStateMachine.CurrentThreatTarget));
                    _mercenary2HUD.UpdateValues(
                        merc2Component.LinkedMercenary.CurrentHP,
                        merc2Component.LinkedMercenary.MaxHP,
                        merc2Component.LinkedMercenary.CurrentMP,
                        merc2Component.LinkedMercenary.MaxMP,
                        merc2Component.LinkedMercenary.Level
                    );

                    // Wire up action queue viz for mercenary #2
                    if (_merc2ActionQueueViz != null)
                    {
                        merc2Component.ActionQueueVisualization = _merc2ActionQueueViz;
                        _merc2ActionQueueViz.SetMercenaryComponent(merc2Component);
                        _merc2ActionQueueViz.SetEnabled(true);
                    }
                }
                else
                {
                    // Mercenary is dead or invalid
                    _mercenary2HUD.SetEnabled(false);
                    if (_merc2ActionQueueViz != null) _merc2ActionQueueViz.SetEnabled(false);
                }
            }
            else
            {
                // No mercenary #2 hired
                if (_mercenary2HUD != null) _mercenary2HUD.SetEnabled(false);
                if (_merc2ActionQueueViz != null) _merc2ActionQueueViz.SetEnabled(false);
            }
        }

        /// <summary>
        /// Update HUD font and position offsets based on current shrink mode
        /// </summary>
        private void UpdateHudFontMode()
        {
            HudMode desired;
            if (WindowManager.IsHalfHeightMode())
                desired = HudMode.Half;
            else
                desired = HudMode.Normal;

            if (desired != _currentHudMode)
            {
                _currentHudMode = desired;

                // Update shortcut bar position and scale when mode changes
                PositionShortcutBar();
                PositionEventConsolePanel();
            }

            // Pit level label and Funds label stay at top-left with no scaling or offset changes
            // (They are always at their base positions)

            // Update graphical HUD position based on mode (no scaling needed - it's in screen space).
            // The offset nudges the HUD away from the edge it is anchored to; the HUD is bottom
            // anchored, so it is SUBTRACTED here (the constant is named for its original top anchor).
            int yOffset = 0;

            switch (_currentHudMode)
            {
                case HudMode.Half:
                    yOffset = GameConfig.TopUiYOffsetHalf;
                    break;
                case HudMode.Normal:
                default:
                    yOffset = GameConfig.TopUiYOffsetNormal;
                    break;
            }

            RepositionGraphicalHud(yOffset);
        }

        /// <summary>
        /// Re-anchors the three HUD panels and the action-queue visualizations that sit over their
        /// heads. Called on HUD mode changes and on stage resize — the panels are bottom-anchored, so
        /// their Y moves with the stage height. Also applies the party-visibility auto-hide slide.
        /// </summary>
        private void RepositionGraphicalHud(int yOffset)
        {
            float baseY = GraphicalHudY() - yOffset;

            // Park position for the auto-hide: far enough down that the whole panel clears the stage
            // bottom, measured off the live stage height so it holds in every window/dock mode.
            float stageH = _uiStage != null ? _uiStage.GetHeight() : GameConfig.VirtualHeight;
            float parkTravel = (stageH - baseY) + GraphicalHudHeight + HudAutoHideClearance;

            float hudY = baseY + parkTravel * SmoothStep(_hudSlideT);
            float vizY = hudY + HudQueueYOffset;

            var huds = new[] { _graphicalHUD, _mercenary1HUD, _mercenary2HUD };
            var vizzes = new[] { _heroActionQueueViz, _merc1ActionQueueViz, _merc2ActionQueueViz };

            for (int i = 0; i < huds.Length; i++)
            {
                float slotX = GraphicalHudBaseX + GraphicalHudSpacing * i;
                huds[i]?.Entity?.SetPosition(slotX, hudY);
                vizzes[i]?.Entity?.SetPosition(slotX + HudQueueXOffset, vizY);
            }
        }

        /// <summary>Re-anchors the HUD panels using the offset for the current HUD mode.</summary>
        private void RepositionGraphicalHud()
        {
            RepositionGraphicalHud(_currentHudMode == HudMode.Half
                ? GameConfig.TopUiYOffsetHalf
                : GameConfig.TopUiYOffsetNormal);
        }

        /// <summary>Ease for the HUD auto-hide slide so it starts and lands softly.</summary>
        private static float SmoothStep(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// Drives the party-visibility auto-hide. The panels slide down out of the bottom edge while
        /// nobody in the party is on camera and slide back up when they return. The action-queue
        /// visualizations are suppressed for the whole slide: they render upwards out of the HUD
        /// heads, so sliding with the panels would leave them hanging in mid-air.
        /// </summary>
        private void UpdateHudAutoHide()
        {
            _hudPartyVisible = IsPartyInCameraView();

            float target = _hudPartyVisible ? 0f : 1f;
            _hudSlideT = Mathf.Approach(_hudSlideT, target, Time.UnscaledDeltaTime / HudAutoHideDuration);

            if (_hudSlideT != _appliedHudSlideT)
            {
                _appliedHudSlideT = _hudSlideT;
                RepositionGraphicalHud();
            }

            if (_hudSlideT > 0f)
            {
                _heroActionQueueViz?.SetEnabled(false);
                _merc1ActionQueueViz?.SetEnabled(false);
                _merc2ActionQueueViz?.SetEnabled(false);
            }
        }

        /// <summary>
        /// True while the living hero or any hired mercenary overlaps the camera viewport. The test
        /// band is inflated while the HUD is up and deflated while it is down, so a party member
        /// walking the screen edge can't make the HUD chatter.
        /// </summary>
        private bool IsPartyInCameraView()
        {
            var camera = Camera;
            if (camera == null)
                return true;

            float inset = _hudPartyVisible ? -HudAutoHideMargin : HudAutoHideMargin;
            var b = camera.Bounds;
            var view = new RectangleF(b.X + inset, b.Y + inset, b.Width - inset * 2f, b.Height - inset * 2f);

            var hero = FindEntity("hero");
            if (hero != null && !hero.HasComponent<HeroDeathComponent>() && IsActorInView(hero, view))
                return true;

            var hiredMercenaries = Core.Services.GetService<MercenaryManager>()?.GetHiredMercenaries();
            if (hiredMercenaries != null)
            {
                for (int i = 0; i < hiredMercenaries.Count; i++)
                {
                    if (IsActorInView(hiredMercenaries[i], view))
                        return true;
                }
            }

            return false;
        }

        /// <summary>True when an enabled actor's tile-sized footprint overlaps the given view rect.</summary>
        private static bool IsActorInView(Entity actor, RectangleF view)
        {
            if (actor == null || !actor.Enabled)
                return false;

            const float half = GameConfig.TileSize * 0.5f;
            var p = actor.Transform.Position;
            return view.Intersects(new RectangleF(p.X - half, p.Y - half, GameConfig.TileSize, GameConfig.TileSize));
        }

        /// <summary>
        /// Positions the shortcut bar at bottom center of screen based on current window mode
        /// </summary>
        private void PositionShortcutBar()
        {
            if (_shortcutBar == null)
                return;

            // Determine scale and visibility based on window mode
            float scale = 1f;
            bool visible = true;

            if (WindowManager.IsHalfHeightMode())
            {
                // Scale 2x for Half mode
                scale = 2f;
            }

            _shortcutBar.SetVisible(visible);
            _shortcutBar.SetShortcutScale(scale);

            if (visible)
            {
                // Calculate bottom center position
                // 8 slots * (32px slot size + 1px padding) * scale
                float barWidth = 8 * (32f + 1f) * scale;
                float barHeight = 32f * scale;

                // Stage space, not Screen.* — under FixedHeight the stage width varies per monitor
                float halfShift = WindowManager.IsHalfHeightMode() ? -64f : 0f;
                float centerX = _uiStage.GetWidth() / 2f - barWidth / 2f + halfShift;
                // Add extra padding for shortcut number text below slots (14px for text + 2px offset = 16px total)
                // Shift up by 16 pixels when in Half mode
                float yOffset = WindowManager.IsHalfHeightMode() ? -16f : 0f;
                float bottomY = _uiStage.GetHeight() - barHeight - 16f + yOffset;

                _shortcutBar.SetBasePosition(centerX, bottomY);

                // Offset left when inventory is open
                bool inventoryOpen = _settingsUI?.HeroUI?.IsWindowVisible ?? false;
                float offsetX = inventoryOpen ? -150f : 0f; // Offset left by 150px when inventory open
                _shortcutBar.SetOffsetX(offsetX);
            }
        }

        /// <summary>
        /// Positions the event console panel just to the right of the shortcut bar, with one-slot padding.
        /// Mirrors PositionShortcutBar()'s scale logic so both stay in sync across window modes.
        /// </summary>
        private void PositionEventConsolePanel()
        {
            if (_eventConsolePanel == null)
                return;

            bool halfMode = WindowManager.IsHalfHeightMode();
            float scale = halfMode ? 2f : 1f;
            float displayScale = halfMode ? 2f : 1f;

            float stageW = _uiStage.GetWidth();
            float slotSize = 32f;
            float barWidth = 8 * (slotSize + 1f) * scale;
            float barRightEdge = stageW / 2f + barWidth / 2f;
            float oneSlotPadding = slotSize * scale;

            const float panelH = 120f;
            float visualH = panelH * displayScale;

            // Anchor the visual bottom edge 16px above the screen bottom.
            float panelY = _uiStage.GetHeight() - 16f - visualH;

            // Lower bound for panelX so the console never overlaps the shortcut bar.
            float halfShift = halfMode ? -96f : 0f;
            float minPanelX = barRightEdge + oneSlotPadding + halfShift;

            // Anchor the console's right edge flush against the right screen edge (issue #279).
            // Normal mode keeps the fixed 480px layout width; half mode stretches to fill the space
            // from panelX to the right edge (divided by displayScale since the Group transform scales
            // the layout back up visually).
            float panelX, layoutW;
            if (halfMode)
            {
                panelX = minPanelX;
                layoutW = (stageW - panelX) / displayScale;
            }
            else
            {
                layoutW = 480f;
                panelX = System.Math.Max(minPanelX, stageW - layoutW * displayScale);
            }

            _eventConsolePanel.SetDisplayScale(displayScale);
            _eventConsolePanel.SetLayoutSize(layoutW, panelH);
            _eventConsolePanel.SetBasePosition(panelX, panelY);
        }

        /// <summary>
        /// Connects the shortcut bar to the hero component and inventory grid
        /// </summary>
        private void ConnectShortcutBarToHero()
        {
            if (_shortcutBar == null)
                return;

            var heroEntity = FindEntity("hero");
            if (heroEntity == null)
            {
                Debug.Warn("[MainGameScene] Could not find hero entity to connect shortcut bar");
                return;
            }

            var heroComponent = heroEntity.GetComponent<HeroComponent>();
            if (heroComponent == null)
            {
                Debug.Warn("[MainGameScene] Hero entity missing HeroComponent");
                return;
            }

            // Get the inventory grid from HeroUI
            var inventoryGrid = _settingsUI?.HeroUI?.GetInventoryGrid();

            // Connect the inventory grid to the hero so it can resolve item references
            // (required for TryRestorePendingShortcuts to find items after save/load)
            if (inventoryGrid != null)
            {
                inventoryGrid.ConnectToHero(heroComponent);
                Debug.Log("[MainGameScene] Connected inventory grid to hero in ConnectShortcutBarToHero");
            }

            _shortcutBar.ConnectToHero(heroComponent, inventoryGrid);
            _shortcutBar.ConnectToDragManager();
            Debug.Log("[MainGameScene] Connected shortcut bar to hero and inventory grid");
        }

        /// <summary>
        /// Reconnects all UI components to the hero (called after hero promotion)
        /// </summary>
        public void ReconnectUIToHero()
        {
            var heroEntity = FindEntity("hero");
            if (heroEntity == null)
            {
                Debug.Warn("[MainGameScene] Could not find hero entity to reconnect UI");
                return;
            }

            var heroComponent = heroEntity.GetComponent<HeroComponent>();
            if (heroComponent == null)
            {
                Debug.Warn("[MainGameScene] Hero entity missing HeroComponent");
                return;
            }

            // Reconnect shortcut bar
            ConnectShortcutBarToHero();

            // Reconnect inventory grid in HeroUI
            var inventoryGrid = _settingsUI?.HeroUI?.GetInventoryGrid();
            if (inventoryGrid != null)
            {
                inventoryGrid.ConnectToHero(heroComponent);
                Debug.Log("[MainGameScene] Reconnected inventory grid to new hero");
            }

            // The ceremony pulled a crystal off the queue and pushed the outgoing one back into
            // the inventory — resync the Crystals tab slots so they aren't stale next open.
            _settingsUI?.HeroUI?.RefreshCrystalsTab();

            Debug.Log("[MainGameScene] Reconnected all UI to new hero");
        }

        /// <summary>
        /// One fixed simulation step. Only simulation state advances here (entities, coroutines via
        /// Core, in-game clock, coordinators, automation). Nothing in this method may read input or
        /// depend on the wall clock — see <see cref="PresentationUpdate"/> for UI/camera work.
        /// </summary>
        public override void Update()
        {
            base.Update();

            Core.Services.GetService<InGameTimeService>()?.Update();

            // Update mercenary manager
            var mercenaryManager = Core.Services.GetService<MercenaryManager>();
            mercenaryManager?.Update();

            // Sync farming / kitchen workers with job assignments. Held during the new-game intro so
            // the starter Slime stays inside its house until the hero has arrived (issue #396).
            if (!IsIntroActive)
            {
                Core.Services.GetService<Services.FarmTaskCoordinator>()?.Update();
                Core.Services.GetService<Services.KitchenTaskCoordinator>()?.Update();
            }

            // Tick party dining (eat timers, auto-resume, reload restart)
            Core.Services.GetService<Services.PartyDiningService>()?.Update();

            // Hour-edge triggers: 6 AM (morning reset), 12 PM (lunch), 6 PM (dinner)
            var timeService = Core.Services.GetService<InGameTimeService>();
            if (timeService != null)
            {
                int currentHour = timeService.Hour;
                if (_lastInGameHour != -1)
                {
                    if (currentHour == 6 && _lastInGameHour != 6)
                    {
                        // Morning reset: clear wet tiles, re-populate watering queue
                        Core.Services.GetService<Services.WetTileService>()?.ClearAllWet();
                        Core.Services.GetService<Services.FarmTaskCoordinator>()?.PopulateWaterQueue();
                        // Belt-and-braces ClearAll (last dinner ~9:59 PM expires ~3:59 AM naturally)
                        Core.Services.GetService<Services.MealBuffService>()?.ClearAll();
                        // Reset so breakfast trip can fire (breakfast itself is wake-driven from SleepInBedAction)
                        Core.Services.GetService<Services.PartyDiningService>()?.ResetForNewMealPeriod();
                    }
                    else if (currentHour == 12 && _lastInGameHour != 12)
                    {
                        // Lunch (issue #392)
                        var partyDining = Core.Services.GetService<Services.PartyDiningService>();
                        partyDining?.ResetForNewMealPeriod();
                        partyDining?.BeginAutoDine(MealPeriod.Lunch);
                    }
                    else if (currentHour == 18 && _lastInGameHour != 18)
                    {
                        // Dinner (issue #392)
                        var partyDining = Core.Services.GetService<Services.PartyDiningService>();
                        partyDining?.ResetForNewMealPeriod();
                        partyDining?.BeginAutoDine(MealPeriod.Dinner);
                    }
                }
                _lastInGameHour = currentHour;
            }

            // Advance crop growth when not paused
            var pauseService = Core.Services.GetService<PauseService>();
            bool isPaused = pauseService?.IsPaused ?? false;
            if (!isPaused)
            {
                var cropsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/CropsProps.atlas");
                Core.Services.GetService<Services.CropGrowthService>()?.Update(
                    Core.Services.GetService<TileStateService>(), cropsAtlas);
                Core.Services.GetService<Services.AutoSeedPurchaseService>()?.Update();
                Core.Services.GetService<Services.AutoCropSellService>()?.Update();
                Core.Services.GetService<Services.AutoJobAssignmentService>()?.Update();
                Core.Services.GetService<Services.AutoLearnSkillsService>()?.Update();
            }

            // Check if a living hero who respawned without a crystal has arrived at the statue
            _heroPromotionService?.CheckAndPromoteHeroIfNeeded();

            // Player commands land here, at the same point of every tick, live or replayed
            _playerCommands?.Drain(_simulationClock != null ? _simulationClock.Tick : 0L);

            // Always last: this step is complete
            _simulationClock?.Advance();
        }

        /// <summary>
        /// Once per rendered frame, after all simulation steps: UI stages, camera, HUD, labels, mode
        /// overlays and world hover/click handling. Anything here that changes simulation state must
        /// go through a player command so it lands on a deterministic tick.
        /// </summary>
        public override void PresentationUpdate()
        {
            // Camera before the UI stages, matching the entity-order the camera component used to update in
            _cameraController?.PresentationUpdate();
            base.PresentationUpdate();

            // Re-anchor stage-space HUD when the render target size changes (window shrink/restore,
            // dock, monitor swap). Clock/tilling/planting labels already reposition every frame.
            if (_uiStage != null)
            {
                float stageW = _uiStage.GetWidth();
                float stageH = _uiStage.GetHeight();
                if (stageW != _lastStageWidth || stageH != _lastStageHeight)
                {
                    _lastStageWidth = stageW;
                    _lastStageHeight = stageH;
                    PositionShortcutBar();
                    RepositionHudLabels();
                    RepositionGraphicalHud();
                    PositionEventConsolePanel();
                    if (_pauseOverlayRenderer != null)
                    {
                        _pauseOverlayRenderer.SetWidth(stageW * 2f);
                        _pauseOverlayRenderer.SetHeight(stageH * 2f);
                    }
                }
            }

            _settingsUI?.Update();
            _eventConsolePanel?.Update();
            // Remove duplicate HeroUI update since SettingsUI handles it

            // Update pause overlay visibility based on pause state. During free-move mode the
            // overlay (which renders above the UI stage) is suppressed — the free-move blocker
            // draws the identical dim below the Exit Free Move button so the button stays bright.
            var pauseService = Core.Services.GetService<PauseService>();
            if (pauseService != null && _pauseOverlayEntity != null)
            {
                _pauseOverlayEntity.SetEnabled(pauseService.IsPaused && !(_settingsUI?.IsFreeMoveModeActive ?? false));
            }

            // Keep pit level label up to date
            UpdatePitLevelLabel();
            UpdateFundsLabel();
            _colorGrading?.UpdateTimeOfDay();
            _cloudOverlay?.Update();
            // Hide the clouds while the Farm/Construction sub-bars or their ground-editing sub-modes
            // are open so they never obscure the tiles being edited; polling covers every enter/exit
            // path (button toggle, outside-click dismiss, cross-UI mutual exclusion).
            _cloudOverlayEntity?.SetEnabled(!(_settingsUI?.IsFarmOrConstructionModeActive ?? false));
            UpdateClockLabel();
            UpdateTillingLabel();
            UpdateRestoringGrassLabel();
            bool inTillMode = _settingsUI?.IsTillModeActive ?? false;
            bool prevInTillMode = _wasInTillMode;
            if (inTillMode != _wasInTillMode)
            {
                if (inTillMode) _tillModeOverlay?.OnEnterTillMode();
                else            _tillModeOverlay?.OnExitTillMode();
                _wasInTillMode = inTillMode;
            }
            if (inTillMode)
                _tillModeOverlay?.Update();

            bool inBuildingMode = _settingsUI?.IsBuildingModeActive ?? false;
            if (inBuildingMode != _wasInBuildingMode)
            {
                if (inBuildingMode) _buildingModeOverlay?.OnEnterBuildingMode();
                else                _buildingModeOverlay?.OnExitBuildingMode();
                _wasInBuildingMode = inBuildingMode;
            }
            if (inBuildingMode)
                _buildingModeOverlay?.Update();
            else if (_buildingModeOverlay?.IsMoving == true)
                _buildingModeOverlay.Update(); // relocating a placed building (independent of farm build mode)

            bool inSeedMode = _settingsUI?.IsSeedModeActive ?? false;
            if (inSeedMode != _wasInSeedMode)
            {
                if (inSeedMode) _seedModeOverlay?.OnEnterSeedMode();
                else            _seedModeOverlay?.OnExitSeedMode();
                _wasInSeedMode = inSeedMode;
            }
            if (inSeedMode)
                _seedModeOverlay?.Update();

            bool inRemoveCropsMode = _settingsUI?.IsRemoveCropsModeActive ?? false;
            if (inRemoveCropsMode != _wasInRemoveCropsMode)
            {
                if (inRemoveCropsMode) _seedModeOverlay?.OnEnterRemoveCropsMode();
                else                   _seedModeOverlay?.OnExitRemoveCropsMode();
                _wasInRemoveCropsMode = inRemoveCropsMode;
            }
            if (inRemoveCropsMode)
                _seedModeOverlay?.Update();

            bool inHarvestedCropsMode = _settingsUI?.IsHarvestedCropsModeActive ?? false;
            if (inHarvestedCropsMode != _wasInHarvestedCropsMode)
            {
                if (inHarvestedCropsMode) _harvestedCropsModeOverlay?.OnEnterHarvestedCropsMode();
                else                      _harvestedCropsModeOverlay?.OnExitHarvestedCropsMode();
                _wasInHarvestedCropsMode = inHarvestedCropsMode;
            }

            bool inRestoreGrassMode = _settingsUI?.IsRestoreGrassModeActive ?? false;
            if (inRestoreGrassMode != _wasInRestoreGrassMode)
            {
                if (inRestoreGrassMode) _restoreGrassModeOverlay?.OnEnterRestoreGrassMode();
                else                    _restoreGrassModeOverlay?.OnExitRestoreGrassMode();
                _wasInRestoreGrassMode = inRestoreGrassMode;
            }
            if (inRestoreGrassMode)
                _restoreGrassModeOverlay?.Update();

            // Show tilled-tile overlays and translucent crop plans whenever the farm menu is open
            // (sub-buttons visible or any sub-mode active). Also manages pause gate, crop visibility,
            // auto-scroll suppression, and post-close rescan for planting.
            bool inFarmMode = (_settingsUI?.IsFarmSubMenuOpen ?? false) || (_settingsUI?.IsConstructionSubMenuOpen ?? false)
                || inTillMode || inBuildingMode || inSeedMode || inRemoveCropsMode || inHarvestedCropsMode || inRestoreGrassMode;
            if (inFarmMode != _wasInFarmMode)
            {
                if (inFarmMode)
                {
                    // Farm work is easier at full window size — temporarily restore full size
                    // and default zoom while the farm UI is open; OnUIWindowClosing re-applies
                    // the player's half-size preference on dismissal.
                    bool wasHalfSize = WindowManager.IsHalfHeightMode();
                    _farmModeRestoreHalfZoom = wasHalfSize;
                    UIWindowManager.OnUIWindowOpening();
                    if (wasHalfSize)
                        _cameraController?.ResetZoomToDefault();
                    pauseService?.SetFarmModePause(true);
                    Core.Services.GetService<Services.CropGrowthService>()?.SetCropsVisible(false);
                    _savedFarmAutoScroll = UIWindowManager.AutoScrollToHeroEnabled;
                    UIWindowManager.SetAutoScrollToHero(false);
                    if (!inTillMode)
                        _tillModeOverlay?.ShowTilledOverlays();
                    _seedModeOverlay?.ShowPlanVisuals();
                }
                else
                {
                    UIWindowManager.OnUIWindowClosing();
                    // Restore the half-window default zoom that was reset when the farm UI opened
                    // (skip if the persistent size changed to Normal while the farm UI was open).
                    // Check the persistent preference, not the live window state: when another UI
                    // (e.g. Settings) is still open the window is temporarily Normal here, but it
                    // returns to Half once that UI closes and the zoom must be ready for it.
                    if (_farmModeRestoreHalfZoom && UIWindowManager.PersistentWindowSize == UIWindowManager.WindowSizeMode.Half)
                        _cameraController?.ApplyHalfWindowZoom();
                    _farmModeRestoreHalfZoom = false;
                    pauseService?.SetFarmModePause(false);
                    Core.Services.GetService<Services.CropGrowthService>()?.SetCropsVisible(true);
                    // Rescan on a deterministic tick, after the unpause command above (replay system)
                    Services.Replay.PlayerCommandService.Dispatch(
                        new Services.Replay.PlayerCommand(Services.Replay.PlayerCommandType.FarmRescan));
                    UIWindowManager.SetAutoScrollToHero(_savedFarmAutoScroll);
                    if (!inTillMode)
                        _tillModeOverlay?.HideTilledOverlays();
                    _seedModeOverlay?.HidePlanVisuals();
                }
                _wasInFarmMode = inFarmMode;
            }

            // OnExitTillMode hides the tilled overlays, but if farm mode is still open they should
            // remain visible. Re-show them whenever till mode just exited inside an active farm session.
            if (inFarmMode && prevInTillMode && !inTillMode)
                _tillModeOverlay?.ShowTilledOverlays();

            UpdatePlantingCropsLabel();

            // The intro keeps the graphical HUD hidden; UpdateHeroHUD would re-enable it every frame
            if (!IsIntroActive)
                UpdateHeroHUD();
            UpdateHudFontMode();
            if (!IsIntroActive)
                UpdateHudAutoHide();

            // Update shortcut bar position (handles offset when inventory open)
            PositionShortcutBar();

            // Refresh shortcut bar to keep it in sync with inventory
            _shortcutBar?.RefreshItems();

            // Handle keyboard shortcuts via shortcut bar (suspended during the new-game intro)
            if (!IsIntroActive)
                _shortcutBar?.HandleKeyboardShortcuts();

            // Handle mercenary hover and click detection
            HandleMercenaryHover();
            HandleMercenaryClicks();

            // Handle placed-building hover outline and click-to-open context menu
            HandleBuildingHover();
            HandleBuildingClicks();

            // Handle hero-statue hover outline and click-to-open job change dialog
            HandleStatueHover();
            HandleStatueClicks();

            // Handle kitchen-fridge hover outline and click-to-open refrigerator window
            HandleFridgeHover();
            HandleFridgeClicks();
            _refrigeratorDialog?.Update();
            UpdateFridgeDialogGate();
            UpdateBuildingMenuGate();
        }

        /// <summary>
        /// Mirrors the crop-storage viewer's treatment for the Refrigerator window: while it is
        /// open the game pauses (pause overlay shows) and a half-size window temporarily
        /// restores to normal so the inventory is fully visible. Watching the visibility edge
        /// here covers every close path — Close button and outside-click dismissal alike.
        /// </summary>
        private void UpdateFridgeDialogGate()
        {
            bool fridgeDialogVisible = _refrigeratorDialog?.IsVisible() ?? false;
            if (fridgeDialogVisible == _wasFridgeDialogVisible)
                return;

            var pauseService = Core.Services.GetService<Services.PauseService>();
            if (fridgeDialogVisible)
            {
                bool wasHalfSize = WindowManager.IsHalfHeightMode();
                _fridgeRestoreHalfZoom = wasHalfSize;
                UI.UIWindowManager.OnUIWindowOpening();
                if (wasHalfSize)
                    _cameraController?.ResetZoomToDefault();
                pauseService?.Pause();
            }
            else
            {
                UI.UIWindowManager.OnUIWindowClosing();
                // Restore the half-window default zoom that was reset when the dialog opened
                // (skip if the persistent size changed to Normal while it was open).
                if (_fridgeRestoreHalfZoom
                    && UI.UIWindowManager.PersistentWindowSize == UI.UIWindowManager.WindowSizeMode.Half)
                    _cameraController?.ApplyHalfWindowZoom();
                _fridgeRestoreHalfZoom = false;
                pauseService?.Unpause();
            }
            // Keep the top bar shown (and its auto-hide idle timer reset) while the dialog is
            // open, exactly like SettingsUI's own windows — otherwise the bar can slide away at
            // Normal-window scale and come back parked half off-screen after the half restore.
            if (_settingsUI != null)
                _settingsUI.ExternalUIWindowOpen = fridgeDialogVisible || (_buildingContextMenu?.IsVisible ?? false);
            _wasFridgeDialogVisible = fridgeDialogVisible;
        }

        /// <summary>
        /// Mirrors the Refrigerator window treatment for the building context menu (shown when
        /// a placed Monster House / Crop Storage is clicked): while it is open, a half-size
        /// window temporarily restores to normal so the menu items are readable. Watching the
        /// visibility edge here covers every close path - Cancel and each action button alike.
        /// Pause is not handled here: the menu pauses/unpauses itself in Show/Hide.
        /// </summary>
        private void UpdateBuildingMenuGate()
        {
            bool menuVisible = _buildingContextMenu?.IsVisible ?? false;
            if (menuVisible == _wasBuildingMenuVisible)
                return;

            if (menuVisible)
            {
                bool wasHalfSize = WindowManager.IsHalfHeightMode();
                _buildingMenuRestoreHalfZoom = wasHalfSize;
                UI.UIWindowManager.OnUIWindowOpening();
                if (wasHalfSize)
                    _cameraController?.ResetZoomToDefault();
            }
            else
            {
                UI.UIWindowManager.OnUIWindowClosing();
                // Restore the half-window default zoom that was reset when the menu opened
                // (skip if the persistent size changed to Normal while it was open).
                if (_buildingMenuRestoreHalfZoom
                    && UI.UIWindowManager.PersistentWindowSize == UI.UIWindowManager.WindowSizeMode.Half)
                    _cameraController?.ApplyHalfWindowZoom();
                _buildingMenuRestoreHalfZoom = false;
            }
            // Keep the top bar shown while the menu is open, exactly like the Refrigerator window.
            // Both gates share ExternalUIWindowOpen, so each sets the OR of both windows'
            // visibility to keep one gate closing edge from clearing the other open window.
            if (_settingsUI != null)
                _settingsUI.ExternalUIWindowOpen = menuVisible || (_refrigeratorDialog?.IsVisible() ?? false);
            _wasBuildingMenuVisible = menuVisible;
        }

        /// <summary>
        /// Handles mouse hover over mercenaries to show SelectBox and name
        /// </summary>
        private void HandleMercenaryHover()
        {
            // Get mouse position in world coordinates
            var mousePos = Camera.MouseToWorldPoint();

            // Find all mercenary entities
            var mercenaries = FindEntitiesWithTag(GameConfig.TAG_MERCENARY);
            
            Entity newHoveredMercenary = null;

            // Suppress hover while the cursor is outside the game window or over open UI so the
            // SelectBox doesn't latch onto a mercenary the user isn't actually pointing at.
            bool interactable = !MercenaryInteractionsBlocked();

            for (int i = 0; interactable && i < mercenaries.Count; i++)
            {
                var mercEntity = mercenaries[i];
                var mercComponent = mercEntity.GetComponent<MercenaryComponent>();

                // Skip hired mercenaries and mercenaries being removed
                if (mercComponent == null || mercComponent.IsHired || mercComponent.IsBeingRemoved)
                    continue;

                // Check if mouse is within mercenary bounds
                var distance = Vector2.Distance(mousePos, mercEntity.Transform.Position);
                if (distance < GameConfig.TileSize)
                {
                    newHoveredMercenary = mercEntity;
                    break;
                }
            }

            // Get player interaction service
            var interactionService = Core.Services.GetService<PlayerInteractionService>();

            // Update hovered mercenary
            if (newHoveredMercenary != _hoveredMercenary)
            {
                _hoveredMercenary = newHoveredMercenary;
                UpdateMercenaryHoverDisplay();

                // Notify interaction service
                if (_hoveredMercenary != null && interactionService != null)
                {
                    interactionService.OnSelectableHoverStart(_hoveredMercenary);
                }
                else if (interactionService != null)
                {
                    interactionService.OnSelectableHoverEnd();
                }
            }
            else if (_hoveredMercenary != null)
            {
                // Update position even if same mercenary (in case they're moving)
                UpdateMercenaryHoverDisplay();

                // Update hover state (resets camera timer if mouse moved)
                if (interactionService != null)
                {
                    interactionService.UpdateHoverState();
                }
            }
            else if (interactionService != null)
            {
                // No mercenary hovered - ensure interaction state is cleared
                interactionService.OnSelectableHoverEnd();
            }
        }

        /// <summary>
        /// Updates the SelectBox and name label display for hovered mercenary
        /// </summary>
        private void UpdateMercenaryHoverDisplay()
        {
            if (_hoveredMercenary == null)
            {
                // Hide SelectBox and name
                if (_mercenarySelectBoxEntity != null)
                    _mercenarySelectBoxEntity.SetEnabled(false);
                if (_mercenaryNameLabelEntity != null)
                    _mercenaryNameLabelEntity.SetEnabled(false);
                return;
            }

            var mercComponent = _hoveredMercenary.GetComponent<MercenaryComponent>();
            if (mercComponent == null)
                return;

            var mercPos = _hoveredMercenary.Transform.Position;

            // Create or update SelectBox entity
            if (_mercenarySelectBoxEntity == null)
            {
                _mercenarySelectBoxEntity = CreateEntity("mercenary-selectbox");
                var selectBox = _mercenarySelectBoxEntity.AddComponent(new SelectBoxRenderComponent());
                selectBox.SetRenderLayer(GameConfig.RenderLayerTop);
            }
            
            _mercenarySelectBoxEntity.SetEnabled(true);
            _mercenarySelectBoxEntity.SetPosition(mercPos);

            // Create or update name label entity
            if (_mercenaryNameLabelEntity == null)
            {
                _mercenaryNameLabelEntity = CreateEntity("mercenary-namelabel");
                var nameLabel = _mercenaryNameLabelEntity.AddComponent(new TextRenderComponent());
                nameLabel.SetRenderLayer(GameConfig.RenderLayerTop);
                nameLabel.SetFont(Content.LoadBitmapFont(GameConfig.FontPathHud));
                nameLabel.SetColor(Color.White);
            }

            var textComponent = _mercenaryNameLabelEntity.GetComponent<TextRenderComponent>();
            if (textComponent != null)
            {
                textComponent.SetText(mercComponent.LinkedMercenary.Name);
            }

            // Position name label above the SelectBox (32 pixels up + additional offset for text height)
            var namePos = new Vector2(mercPos.X, mercPos.Y - 40);
            _mercenaryNameLabelEntity.SetEnabled(true);
            _mercenaryNameLabelEntity.SetPosition(namePos);
        }

        /// <summary>
        /// Handles mouse clicks on mercenaries for hiring
        /// </summary>
        private void HandleMercenaryClicks()
        {
            // Only check if left mouse button was just pressed
            if (!Input.LeftMouseButtonPressed)
                return;

            if (MercenaryInteractionsBlocked())
                return;

            var mercenaryManager = Core.Services.GetService<MercenaryManager>();
            if (mercenaryManager == null)
                return;

            // Get mouse position in world coordinates
            var mousePos = Camera.MouseToWorldPoint();

            // Find all mercenary entities
            var mercenaries = FindEntitiesWithTag(GameConfig.TAG_MERCENARY);
            
            for (int i = 0; i < mercenaries.Count; i++)
            {
                var mercEntity = mercenaries[i];
                var mercComponent = mercEntity.GetComponent<MercenaryComponent>();
                
                // Skip hired mercenaries and mercenaries being removed
                if (mercComponent == null || mercComponent.IsHired || mercComponent.IsBeingRemoved)
                    continue;

                // Allow clicking anywhere (not just in tavern)
                // Check if click is within mercenary bounds (use simple distance check)
                var distance = Vector2.Distance(mousePos, mercEntity.Transform.Position);
                if (distance < GameConfig.TileSize)
                {
                    // Notify interaction service that player clicked a selectable
                    var interactionService = Core.Services.GetService<PlayerInteractionService>();
                    if (interactionService != null)
                    {
                        interactionService.OnSelectableClicked(mercEntity);
                    }

                    _mercenaryHireDialog?.Show(mercEntity);
                    break;
                }
            }
        }

        /// <summary>
        /// True when tavern-mercenary hover/click interactions should be suppressed — while the
        /// cursor is outside the game window, while the hire dialog is already open, or when the
        /// pointer is over UI (so mercs behind an open window can't be clicked through it).
        /// </summary>
        private bool MercenaryInteractionsBlocked()
        {
            if (!Util.MouseUtils.IsMouseInsideWindow())
                return true;
            if (_mercenaryHireDialog?.IsDialogVisible == true)
                return true;
            if (_uiStage != null && _uiStage.Hit(_uiStage.GetMousePosition()) != null)
                return true;
            return false;
        }

        /// <summary>
        /// True when placed-building hover/click interactions should be suppressed — while the cursor
        /// is outside the game window, while relocating, during any farm sub-mode, when the context
        /// menu is already open, while a confirmation dialog is awaiting an answer, or when the
        /// pointer is over UI.
        /// </summary>
        private bool BuildingInteractionsBlocked()
        {
            if (!Util.MouseUtils.IsMouseInsideWindow())
                return true;
            if (_buildingModeOverlay?.IsMoving == true)
                return true;
            if (_buildingContextMenu?.IsVisible == true)
                return true;
            // The sell confirmation is non-modal and only covers the middle of the strip, so without
            // this the player could re-click the same building and stack a second sell dialog on it.
            if (UI.ConfirmationDialog.AnyVisible)
                return true;
            if (_settingsUI != null &&
                (_settingsUI.IsFarmSubMenuOpen || _settingsUI.IsConstructionSubMenuOpen ||
                 _settingsUI.IsTillModeActive || _settingsUI.IsBuildingModeActive ||
                 _settingsUI.IsSeedModeActive || _settingsUI.IsRemoveCropsModeActive ||
                 _settingsUI.IsHarvestedCropsModeActive || _settingsUI.IsRestoreGrassModeActive ||
                 _settingsUI.IsFreeMoveModeActive))
                return true;
            if (_uiStage != null && _uiStage.Hit(_uiStage.GetMousePosition()) != null)
                return true;
            return false;
        }

        /// <summary>Returns the placed building under the cursor, or null (respecting interaction guards).</summary>
        private Services.PlacedBuilding GetBuildingUnderCursor()
        {
            var buildingService = Core.Services.GetService<Services.BuildingService>();
            if (buildingService == null)
                return null;

            var worldPos = Camera.MouseToWorldPoint();
            int tileX = (int)(worldPos.X / GameConfig.TileSize);
            int tileY = (int)(worldPos.Y / GameConfig.TileSize);
            return buildingService.GetBuildingAtTile(tileX, tileY);
        }

        /// <summary>Draws a white outline around the placed building under the cursor to signal it is clickable.</summary>
        private void HandleBuildingHover()
        {
            Services.PlacedBuilding hovered = BuildingInteractionsBlocked() ? null : GetBuildingUnderCursor();

            if (hovered == _hoveredBuilding)
                return;

            _hoveredBuilding = hovered;

            if (hovered == null)
            {
                _buildingHoverOutlineEntity?.SetEnabled(false);
                return;
            }

            if (_buildingHoverOutlineEntity == null)
            {
                _buildingHoverOutlineEntity = CreateEntity("building-hover-outline");
                var outline = _buildingHoverOutlineEntity.AddComponent(new BuildingOutlineRenderComponent());
                outline.SetRenderLayer(GameConfig.RenderLayerTop);
                outline.SetColor(Color.White);
            }

            var bounds = Util.BuildingConfig.GetFootprintBounds(hovered.Type);
            float left = (hovered.TileX + bounds.dxMin) * GameConfig.TileSize;
            float top  = (hovered.TileY + bounds.dyMin) * GameConfig.TileSize;
            float w = (bounds.dxMax - bounds.dxMin + 1) * GameConfig.TileSize;
            float h = (bounds.dyMax - bounds.dyMin + 1) * GameConfig.TileSize;

            _buildingHoverOutlineEntity.GetComponent<BuildingOutlineRenderComponent>()?.SetSize(w, h);
            _buildingHoverOutlineEntity.SetPosition(left, top);
            _buildingHoverOutlineEntity.SetEnabled(true);
        }

        private bool _statueHovered;
        private Entity _statueHoverOutlineEntity;

        /// <summary>
        /// True when hero-statue hover/click interactions should be suppressed — while the cursor
        /// is outside the game window, while a confirmation is open, when the pointer is over UI,
        /// or when no job change can currently be requested (no hero, or ceremony already pending).
        /// </summary>
        private bool StatueInteractionsBlocked()
        {
            if (!Util.MouseUtils.IsMouseInsideWindow())
                return true;
            if (UI.ConfirmationDialog.AnyVisible)
                return true;
            if (_uiStage != null && _uiStage.Hit(_uiStage.GetMousePosition()) != null)
                return true;
            if (!UI.JobChangeFlow.CanRequestJobChange())
                return true;
            return false;
        }

        /// <summary>Returns the hero statue's renderable bounds if the cursor is over it, else null.</summary>
        private Entity GetStatueUnderCursor()
        {
            var statues = FindEntitiesWithTag(GameConfig.TAG_HERO_STATUE);
            if (statues.Count == 0)
                return null;

            var statue = statues[0];
            var mousePos = Camera.MouseToWorldPoint();
            var renderer = statue.GetComponent<YSortSpriteRenderer>();
            if (renderer != null)
                return renderer.Bounds.Contains(mousePos) ? statue : null;

            return Vector2.Distance(mousePos, statue.Transform.Position) < GameConfig.TileSize ? statue : null;
        }

        /// <summary>Draws a white outline around the hero statue under the cursor to signal it is clickable.</summary>
        private void HandleStatueHover()
        {
            var hovered = StatueInteractionsBlocked() ? null : GetStatueUnderCursor();
            bool isHovered = hovered != null;

            // Statue never moves, so only state changes need work
            if (isHovered == _statueHovered)
                return;

            _statueHovered = isHovered;

            if (!isHovered)
            {
                _statueHoverOutlineEntity?.SetEnabled(false);
                return;
            }

            if (_statueHoverOutlineEntity == null)
            {
                _statueHoverOutlineEntity = CreateEntity("statue-hover-outline");
                var outline = _statueHoverOutlineEntity.AddComponent(new BuildingOutlineRenderComponent());
                outline.SetRenderLayer(GameConfig.RenderLayerTop);
                outline.SetColor(Color.White);
            }

            var renderer = hovered.GetComponent<YSortSpriteRenderer>();
            if (renderer != null)
            {
                var bounds = renderer.Bounds;
                _statueHoverOutlineEntity.GetComponent<BuildingOutlineRenderComponent>()?.SetSize(bounds.Width, bounds.Height);
                _statueHoverOutlineEntity.SetPosition(bounds.X, bounds.Y);
            }
            _statueHoverOutlineEntity.SetEnabled(true);
        }

        /// <summary>Opens the job change dialog when the hero statue is clicked.</summary>
        private void HandleStatueClicks()
        {
            if (!Input.LeftMouseButtonPressed)
                return;
            if (StatueInteractionsBlocked())
                return;

            var statue = GetStatueUnderCursor();
            if (statue == null)
                return;

            // Clear the hover outline before the dialog opens.
            _statueHovered = false;
            _statueHoverOutlineEntity?.SetEnabled(false);

            UI.JobChangeFlow.ShowChangeJobDialog(_uiStage, UI.PitHeroSkin.CreateSkin());
        }

        private bool _fridgeHovered;
        private Entity _fridgeHoverOutlineEntity;

        /// <summary>
        /// True when kitchen-fridge hover/click interactions should be suppressed — while the
        /// cursor is outside the game window, while a confirmation is open, or when the pointer
        /// is over UI.
        /// </summary>
        private bool FridgeInteractionsBlocked()
        {
            if (!Util.MouseUtils.IsMouseInsideWindow())
                return true;
            if (UI.ConfirmationDialog.AnyVisible)
                return true;
            if (_uiStage != null && _uiStage.Hit(_uiStage.GetMousePosition()) != null)
                return true;
            return false;
        }

        /// <summary>
        /// True when the cursor is over the kitchen fridge. The fridge is a fixed map fixture (no
        /// entity) whose art spans two tiles vertically at (87,1)-(87,2).
        /// </summary>
        private bool IsMouseOverFridge()
        {
            var mousePos = Camera.MouseToWorldPoint();
            float left = GameConfig.KitchenFridgeTileX * GameConfig.TileSize;
            float top = GameConfig.KitchenFridgeArtTopTileY * GameConfig.TileSize;
            float height = (GameConfig.KitchenFridgeTileY - GameConfig.KitchenFridgeArtTopTileY + 1) * GameConfig.TileSize;
            return mousePos.X >= left && mousePos.X < left + GameConfig.TileSize
                && mousePos.Y >= top && mousePos.Y < top + height;
        }

        /// <summary>Draws a white outline around the kitchen fridge under the cursor to signal it is clickable.</summary>
        private void HandleFridgeHover()
        {
            bool isHovered = !FridgeInteractionsBlocked() && IsMouseOverFridge();

            // The fridge never moves, so only state changes need work
            if (isHovered == _fridgeHovered)
                return;

            _fridgeHovered = isHovered;

            if (!isHovered)
            {
                _fridgeHoverOutlineEntity?.SetEnabled(false);
                return;
            }

            if (_fridgeHoverOutlineEntity == null)
            {
                _fridgeHoverOutlineEntity = CreateEntity("fridge-hover-outline");
                var outline = _fridgeHoverOutlineEntity.AddComponent(new BuildingOutlineRenderComponent());
                outline.SetRenderLayer(GameConfig.RenderLayerTop);
                outline.SetColor(Color.White);
                outline.SetSize(GameConfig.TileSize,
                    (GameConfig.KitchenFridgeTileY - GameConfig.KitchenFridgeArtTopTileY + 1) * GameConfig.TileSize);
                _fridgeHoverOutlineEntity.SetPosition(
                    GameConfig.KitchenFridgeTileX * GameConfig.TileSize,
                    GameConfig.KitchenFridgeArtTopTileY * GameConfig.TileSize);
            }
            _fridgeHoverOutlineEntity.SetEnabled(true);
        }

        /// <summary>Opens the Refrigerator window when the kitchen fridge is clicked.</summary>
        private void HandleFridgeClicks()
        {
            if (!Input.LeftMouseButtonPressed)
                return;
            if (FridgeInteractionsBlocked())
                return;
            if (!IsMouseOverFridge())
                return;

            // Clear the hover outline before the window opens.
            _fridgeHovered = false;
            _fridgeHoverOutlineEntity?.SetEnabled(false);

            _refrigeratorDialog?.Show();
        }

        /// <summary>Opens the building context menu when a placed building is clicked.</summary>
        private void HandleBuildingClicks()
        {
            // Always consume so the flag never lingers past the frame a move ended.
            bool moveJustEnded = _buildingModeOverlay != null && _buildingModeOverlay.ConsumeMoveJustEnded();

            if (!Input.LeftMouseButtonPressed)
                return;
            // Ignore the same click that just confirmed a relocation.
            if (moveJustEnded)
                return;
            if (BuildingInteractionsBlocked())
                return;

            var building = GetBuildingUnderCursor();
            if (building == null)
                return;

            // Clear the hover outline before the menu opens.
            _hoveredBuilding = null;
            _buildingHoverOutlineEntity?.SetEnabled(false);

            _buildingContextMenu?.Show(_uiStage, building, _uiStage.GetMousePosition());
        }
    }
}