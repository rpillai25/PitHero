using Microsoft.Xna.Framework;
using Nez;
using Nez.BitmapFonts;
using Nez.Sprites;
using Nez.Tiled;
using Nez.UI; // Added for Label
using PitHero.AI;
using PitHero.ECS.Components;
using PitHero.Services;
using PitHero.UI;
using PitHero.Util;
using RolePlayingFramework.Heroes;
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
        private Label _pitLevelLabel; // UI label showing pit level
        private int _lastDisplayedPitLevel = -1; // Track last displayed level to avoid string churn
        private ShortcutBar _shortcutBar; // Shortcut bar displayed at bottom center
        private GraphicalHUD _graphicalHUD; // Graphical HUD component for HP/MP/Level display
        private GraphicalHUD _mercenary1HUD; // Graphical HUD for mercenary #1
        private GraphicalHUD _mercenary2HUD; // Graphical HUD for mercenary #2
        private MercenaryHireDialog _mercenaryHireDialog; // Dialog for hiring mercenaries
        private Entity _hoveredMercenary; // Currently hovered mercenary
        private Entity _mercenarySelectBoxEntity; // Entity for rendering SelectBox over hovered mercenary
        private Entity _mercenaryNameLabelEntity; // Entity for rendering name above hovered mercenary

        // HUD fonts for different shrink levels
        public BitmapFont _hudFontNormal;
        public BitmapFont _hudFontHalf;
        private LabelStyle _pitLevelStyleNormal;
        private LabelStyle _pitLevelStyleHalf;
        private enum HudMode { Normal, Half }
        private HudMode _currentHudMode = HudMode.Normal;

        // Cached base positions for top-left anchored UI (so offsets are relative and centralized)
        private const float PitLabelBaseY = 16f; // original Y before offsets applied
        private const float GraphicalHudBaseX = 110f; // Base X position for graphical HUD (to the right of Pit Lv label)
        private const float GraphicalHudBaseY = 4f; // Base Y position for graphical HUD
        private const float GraphicalHudHalfModeXOffset = 110f; // Additional X offset when in half mode to avoid covering pit label
        private const float GraphicalHudSpacing = 170f; // Spacing between HUD elements (hero to merc1, merc1 to merc2)

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
            SetDesignResolution(GameConfig.VirtualWidth, GameConfig.VirtualHeight, SceneResolutionPolicy.BestFit);
            ClearColor = Color.Transparent;

            var cameraEntity = CreateEntity("camera-controller");
            cameraEntity.AddComponent(Camera);
            _cameraController = cameraEntity.AddComponent(new CameraControllerComponent());

            // Load HUD fonts (normal, 2x, 4x for shrink levels)
            _hudFontNormal = Content.LoadBitmapFont("Content/Fonts/HUD.fnt");
            // New enlarged fonts for smaller window modes
            _hudFontHalf = Content.LoadBitmapFont("Content/Fonts/Hud2x.fnt");
            HudFont = _hudFontNormal; // maintain old field

            // Pre-create label styles to avoid per-frame allocations
            _pitLevelStyleNormal = new LabelStyle(_hudFontNormal, Color.White);
            _pitLevelStyleHalf = new LabelStyle(_hudFontHalf, Color.White);

            SetupUIOverlay();
        }

        public override void Begin()
        {
            base.Begin();
            if (_isInitializationComplete)
                return;

            LoadMap();
            SpawnPit();

            // Set starting pit level to 9 (after pit exists to avoid early collider warnings)
            var pitWidthManager = Core.Services.GetService<PitWidthManager>();
            if (pitWidthManager != null)
                pitWidthManager.SetPitLevel(1);

            SpawnHero();
            SpawnHeroStatue();

            // Connect shortcut bar to hero
            ConnectShortcutBarToHero();

            // Initialize mercenary manager
            var mercenaryManager = new MercenaryManager();
            Core.Services.AddService(mercenaryManager);
            mercenaryManager.Initialize(this);

            // Initialize hero promotion service
            var heroPromotionService = new HeroPromotionService(this);
            Core.Services.AddService(heroPromotionService);
            Debug.Log("[MainGameScene] HeroPromotionService initialized");

            // Initialize player interaction service for camera control
            var playerInteractionService = new PlayerInteractionService();
            Core.Services.AddService(playerInteractionService);
            Debug.Log("[MainGameScene] PlayerInteractionService initialized");

            _isInitializationComplete = true;
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

            var topLayerRenderer = tiledEntity.AddComponent(new TiledMapRenderer(_tmxMap));
            topLayerRenderer.SetLayerToRender("Top");
            topLayerRenderer.SetRenderLayer(GameConfig.RenderLayerTop);

            var fogLayerRenderer = tiledEntity.AddComponent(new TiledMapRenderer(_tmxMap));
            fogLayerRenderer.SetLayerToRender("FogOfWar");
            fogLayerRenderer.SetRenderLayer(GameConfig.RenderLayerFogOfWar);

            _cameraController?.ConfigureZoomForMap(_mapPath);

            // Initialize pit width manager after map and services are set up
            SetupPitWidthManager();
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

        private void SpawnHero()
        {
            // Calculate random position at least 8 tiles to the right of rightmost pit edge
            var pitWidthManager = Core.Services.GetService<PitWidthManager>();
            var pitWidth = pitWidthManager?.CurrentPitRectWidthTiles ?? GameConfig.PitRectWidth;
            var rightmostPitTile = GameConfig.PitRectX + pitWidth - 1;

            var minHeroTileX = rightmostPitTile + 8; // 20
            var maxHeroTileX = 50; // Leave some space from map edge

            var heroTileX = 62; // Random.Range(minHeroTileX, maxHeroTileX + 1);
            var heroTileY = 6;

            var heroStart = new Vector2(
                heroTileX * GameConfig.TileSize + GameConfig.TileSize / 2,
                heroTileY * GameConfig.TileSize + GameConfig.TileSize / 2
            );

            var hero = CreateEntity("hero").SetPosition(heroStart);
            hero.SetTag(GameConfig.TAG_HERO); // Make sure this is set!

            Debug.Log($"[MainGameScene] Hero spawned at random position {heroStart.X},{heroStart.Y} , tile coordinates: " +
                      $"({heroTileX}, {heroTileY}) - {minHeroTileX - rightmostPitTile} tiles from pit edge");

            // NEW: add facing component first so animators can query it immediately
            hero.AddComponent(new ActorFacingComponent());

            // Add all paperdoll layer animators in the correct order (Hand2 to Hand1)
            var offset = new Vector2(0, -GameConfig.TileSize / 2); // Offset so feet are at entity position

            // Body layer
            var heroBodyAnimator = hero.AddComponent(new HeroBodyAnimationComponent(GameConfig.SkinColors.RandomItem()));
            heroBodyAnimator.SetRenderLayer(GameConfig.RenderLayerHeroBody);
            heroBodyAnimator.SetLocalOffset(offset);

            // Hand2 layer (top-most paperdoll layer)
            var heroHand2Animator = hero.AddComponent(new HeroHand2AnimationComponent(heroBodyAnimator.ComponentColor));
            heroHand2Animator.SetRenderLayer(GameConfig.RenderLayerHeroHand2);
            heroHand2Animator.SetLocalOffset(offset);
            heroHand2Animator.ComponentColor = heroBodyAnimator.ComponentColor; // Sync color with body

            // Pants layer
            var heroPantsAnimator = hero.AddComponent(new HeroPantsAnimationComponent(Color.White));
            heroPantsAnimator.SetRenderLayer(GameConfig.RenderLayerHeroPants);
            heroPantsAnimator.SetLocalOffset(offset);

            // Shirt layer
            var heroShirtAnimator = hero.AddComponent(new HeroShirtAnimationComponent(GameConfig.ShirtColors.RandomItem()));
            heroShirtAnimator.SetRenderLayer(GameConfig.RenderLayerHeroShirt);
            heroShirtAnimator.SetLocalOffset(offset);

            // Hair layer
            var heroHairAnimator = hero.AddComponent(new HeroHairAnimationComponent(GameConfig.HairColors.RandomItem()));
            heroHairAnimator.SetRenderLayer(GameConfig.RenderLayerHeroHair);
            heroHairAnimator.SetLocalOffset(offset);

            // Hand1 layer (bottom-most paperdoll layer)
            var heroHand1Animator = hero.AddComponent(new HeroHand1AnimationComponent(heroBodyAnimator.ComponentColor));
            heroHand1Animator.SetRenderLayer(GameConfig.RenderLayerHeroHand1);
            heroHand1Animator.SetLocalOffset(offset);
            heroHand1Animator.ComponentColor = heroBodyAnimator.ComponentColor; // Sync color with body

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

            // Initialize a test HeroCrystal for crystal-infused stats
            var testJob = new Knight(); // Using Knight job for testing
            var baseStats = new StatBlock(strength: 4, agility: 3, vitality: 5, magic: 1);
            var testCrystal = new HeroCrystal("Test Hero", testJob, 1, baseStats); // Level 1 hero for testing
            testCrystal.EarnJP(550); // Give some starting JP

            // Create the linked Hero from the crystal
            heroComponent.LinkedHero = new RolePlayingFramework.Heroes.Hero("Test Hero", testJob, 1, baseStats, testCrystal);

            Debug.Log($"[MainGameScene] Created test hero with Level {heroComponent.LinkedHero.Level}, HP {heroComponent.LinkedHero.CurrentHP}/{heroComponent.LinkedHero.MaxHP}");

            // Add BouncyDigitComponent for damage display (RenderLayerUI, disabled initially)
            var heroBouncyDigit = hero.AddComponent(new BouncyDigitComponent());
            heroBouncyDigit.SetRenderLayer(GameConfig.RenderLayerLowest);
            heroBouncyDigit.SetEnabled(false);

            // Add BouncyTextComponent for miss display (RenderLayerUI, disabled initially)
            var heroBouncyText = hero.AddComponent(new BouncyTextComponent());
            heroBouncyText.SetRenderLayer(GameConfig.RenderLayerLowest);
            heroBouncyText.SetEnabled(false);

            // Add action queue visualization component
            var actionQueueViz = hero.AddComponent(new ActionQueueVisualizationComponent());
            actionQueueViz.SetRenderLayer(GameConfig.RenderLayerLowest);

            hero.AddComponent(new Historian());
            hero.AddComponent(new HeroStateMachine());

            // Force pathfinding initialization to complete before adding obstacles
            // OnAddedToEntity() is called automatically by the framework after this method completes
            // But we need to explicitly wait for pathfinding to be ready
            Core.StartCoroutine(AddObstaclesAfterPathfindingReady(hero));
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
                Debug.Log($"[MainGameScene] Added existing obstacle at ({tileX},{tileY}) to hero pathfinding");
            }

            Debug.Log($"[MainGameScene] Added {addedWalls} existing obstacle walls to hero pathfinding graph");
        }

        /// <summary>
        /// Spawn the hero statue at tile coordinate (112, 6)
        /// </summary>
        private void SpawnHeroStatue()
        {
            var tileX = 112;
            var tileY = 3;

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
                    var renderer = statueEntity.AddComponent(new SpriteRenderer(statueSprite));
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

        private void SetupUIOverlay()
        {
            var screenSpaceRenderer = new ScreenSpaceRenderer(100, [GameConfig.TransparentPauseOverlay, GameConfig.RenderLayerUI, GameConfig.RenderLayerGraphicalHUD]);
            AddRenderer(screenSpaceRenderer);

            // Create pause overlay entity
            _pauseOverlayEntity = CreateEntity("pause-overlay");
            _pauseOverlayEntity.SetPosition(0, 0); // Top-left corner

            // Size to backbuffer for ScreenSpaceRenderer and set origin to top-left
            var pauseOverlay = _pauseOverlayEntity.AddComponent(
                new PrototypeSpriteRenderer(Screen.Width * 2, Screen.Height * 2)
            );
            pauseOverlay.SetOrigin(Vector2.Zero); // or pauseOverlay.SetOriginNormalized(Vector2.Zero);
            pauseOverlay.SetColor(new Color(0, 0, 0, 100));
            pauseOverlay.SetRenderLayer(GameConfig.TransparentPauseOverlay);
            _pauseOverlayEntity.SetEnabled(false); // Initially hidden

            var uiEntity = CreateEntity("ui-overlay");
            var uiCanvas = uiEntity.AddComponent(new UICanvas());
            uiCanvas.IsFullScreen = true;
            uiCanvas.RenderLayer = GameConfig.RenderLayerUI;

            _settingsUI = new SettingsUI(Core.Instance);
            _settingsUI.InitializeUI(uiCanvas.Stage);

            // Remove duplicate HeroUI creation - it's already handled by SettingsUI
            // Initialize HeroUI for pit priority management
            // _heroUI = new HeroUI();
            // _heroUI.InitializeUI(uiCanvas.Stage);
            // Position the Hero button in the bottom-left corner  
            // _heroUI.SetPosition(10f, Screen.Height - _heroUI.GetHeight() - 10f);

            // Pit level label (always visible top-left). Base position then offset applied per shrink level.
            _pitLevelLabel = uiCanvas.Stage.AddElement(new Label("Pit Lv. 1", _hudFontNormal));
            _pitLevelLabel.SetStyle(_pitLevelStyleNormal);
            _pitLevelLabel.SetPosition(10, PitLabelBaseY);

            // Create graphical HUD entity to display HP/MP/Level
            var hudEntity = CreateEntity("graphical-hud");
            hudEntity.SetPosition(GraphicalHudBaseX, GraphicalHudBaseY);
            _graphicalHUD = hudEntity.AddComponent(new GraphicalHUD());
            _graphicalHUD.SetRenderLayer(GameConfig.RenderLayerGraphicalHUD); // Use screen space renderer

            // Create mercenary #1 HUD entity
            var merc1HudEntity = CreateEntity("mercenary1-hud");
            merc1HudEntity.SetPosition(GraphicalHudBaseX + GraphicalHudSpacing, GraphicalHudBaseY);
            _mercenary1HUD = merc1HudEntity.AddComponent(new GraphicalHUD());
            _mercenary1HUD.SetRenderLayer(GameConfig.RenderLayerGraphicalHUD);
            _mercenary1HUD.SetEnabled(false); // Initially hidden until mercenary is hired

            // Create mercenary #2 HUD entity
            var merc2HudEntity = CreateEntity("mercenary2-hud");
            merc2HudEntity.SetPosition(GraphicalHudBaseX + GraphicalHudSpacing * 2, GraphicalHudBaseY);
            _mercenary2HUD = merc2HudEntity.AddComponent(new GraphicalHUD());
            _mercenary2HUD.SetRenderLayer(GameConfig.RenderLayerGraphicalHUD);
            _mercenary2HUD.SetEnabled(false); // Initially hidden until mercenary is hired

            // Shortcut bar at bottom center
            _shortcutBar = new ShortcutBar();
            uiCanvas.Stage.AddElement(_shortcutBar);
            PositionShortcutBar();

            // Mercenary hire dialog
            _mercenaryHireDialog = new MercenaryHireDialog();
            uiCanvas.Stage.AddElement(_mercenaryHireDialog);
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
        /// Update pit level label text when the pit level changes
        /// </summary>
        private void UpdatePitLevelLabel()
        {
            if (_pitLevelLabel == null)
                return;

            var pitWidthManager = Core.Services.GetService<PitWidthManager>();
            if (pitWidthManager == null)
                return;

            var currentLevel = pitWidthManager.CurrentPitLevel;
            if (currentLevel != _lastDisplayedPitLevel)
            {
                _pitLevelLabel.SetText($"Pit Lv. {currentLevel}");
                _lastDisplayedPitLevel = currentLevel;
            }
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
                // Hero doesn't exist - hide hero HUD
                _graphicalHUD.SetEnabled(false);
                return;
            }

            var heroComponent = hero.GetComponent<HeroComponent>();
            if (heroComponent?.LinkedHero == null)
            {
                _graphicalHUD.SetEnabled(false);
                return;
            }

            // Check if hero has HeroDeathComponent - if so, hero is dead
            if (hero.HasComponent<HeroDeathComponent>())
            {
                _graphicalHUD.SetEnabled(false);
                return;
            }

            var linkedHero = heroComponent.LinkedHero;

            // Hero is alive - show and update HUD
            _graphicalHUD.SetEnabled(true);
            _graphicalHUD.SetHeroEntity(hero);
            _graphicalHUD.UpdateValues(
                linkedHero.CurrentHP,
                linkedHero.MaxHP,
                linkedHero.CurrentMP,
                linkedHero.MaxMP,
                linkedHero.Level
            );

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
                // No mercenary manager - hide all mercenary HUDs
                if (_mercenary1HUD != null) _mercenary1HUD.SetEnabled(false);
                if (_mercenary2HUD != null) _mercenary2HUD.SetEnabled(false);
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
                    _mercenary1HUD.UpdateValues(
                        merc1Component.LinkedMercenary.CurrentHP,
                        merc1Component.LinkedMercenary.MaxHP,
                        merc1Component.LinkedMercenary.CurrentMP,
                        merc1Component.LinkedMercenary.MaxMP,
                        merc1Component.LinkedMercenary.Level
                    );
                }
                else
                {
                    // Mercenary is dead or invalid
                    _mercenary1HUD.SetEnabled(false);
                }
            }
            else
            {
                // No mercenary #1 hired
                if (_mercenary1HUD != null) _mercenary1HUD.SetEnabled(false);
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
                    _mercenary2HUD.UpdateValues(
                        merc2Component.LinkedMercenary.CurrentHP,
                        merc2Component.LinkedMercenary.MaxHP,
                        merc2Component.LinkedMercenary.CurrentMP,
                        merc2Component.LinkedMercenary.MaxMP,
                        merc2Component.LinkedMercenary.Level
                    );
                }
                else
                {
                    // Mercenary is dead or invalid
                    _mercenary2HUD.SetEnabled(false);
                }
            }
            else
            {
                // No mercenary #2 hired
                if (_mercenary2HUD != null) _mercenary2HUD.SetEnabled(false);
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
                switch (desired)
                {
                    case HudMode.Normal:
                        _pitLevelLabel.SetStyle(_pitLevelStyleNormal);
                        break;
                    case HudMode.Half:
                        _pitLevelLabel.SetStyle(_pitLevelStyleHalf);
                        break;
                }
                _currentHudMode = desired;
                _pitLevelLabel.Invalidate();

                // Update shortcut bar position and scale when mode changes
                PositionShortcutBar();
            }

            // Apply vertical offset based on mode
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

            // Only update positions if changed to avoid redundant property sets
            float targetY = PitLabelBaseY + yOffset;

            if (System.Math.Abs(_pitLevelLabel.GetY() - targetY) > 0.1f)
            {
                _pitLevelLabel.SetY(targetY);
            }

            // Update graphical HUD position based on mode (no scaling needed - it's in screen space)
            if (_graphicalHUD != null)
            {
                var hudEntity = _graphicalHUD.Entity;
                if (hudEntity != null)
                {
                    float hudTargetY = GraphicalHudBaseY + yOffset;
                    float hudTargetX = GraphicalHudBaseX;

                    if (_currentHudMode == HudMode.Half)
                    {
                        // Shift right to avoid covering the pit label (no scale needed in screen space)
                        hudTargetX += GraphicalHudHalfModeXOffset;
                    }

                    hudEntity.SetPosition(hudTargetX, hudTargetY);
                }
            }

            // Update mercenary HUD positions based on mode
            if (_mercenary1HUD != null)
            {
                var merc1Entity = _mercenary1HUD.Entity;
                if (merc1Entity != null)
                {
                    float hudTargetY = GraphicalHudBaseY + yOffset;
                    float hudTargetX = GraphicalHudBaseX + GraphicalHudSpacing;

                    if (_currentHudMode == HudMode.Half)
                    {
                        hudTargetX += GraphicalHudHalfModeXOffset;
                    }

                    merc1Entity.SetPosition(hudTargetX, hudTargetY);
                }
            }

            if (_mercenary2HUD != null)
            {
                var merc2Entity = _mercenary2HUD.Entity;
                if (merc2Entity != null)
                {
                    float hudTargetY = GraphicalHudBaseY + yOffset;
                    float hudTargetX = GraphicalHudBaseX + GraphicalHudSpacing * 2;

                    if (_currentHudMode == HudMode.Half)
                    {
                        hudTargetX += GraphicalHudHalfModeXOffset;
                    }

                    merc2Entity.SetPosition(hudTargetX, hudTargetY);
                }
            }
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

                float centerX = Screen.Width / 2f - barWidth / 2f;
                // Add extra padding for shortcut number text below slots (14px for text + 2px offset = 16px total)
                // Shift up by 16 pixels when in Half mode
                float yOffset = WindowManager.IsHalfHeightMode() ? -16f : 0f;
                float bottomY = Screen.Height - barHeight - 16f + yOffset;

                _shortcutBar.SetBasePosition(centerX, bottomY);

                // Offset left when inventory is open
                bool inventoryOpen = _settingsUI?.HeroUI?.IsWindowVisible ?? false;
                float offsetX = inventoryOpen ? -150f : 0f; // Offset left by 150px when inventory open
                _shortcutBar.SetOffsetX(offsetX);
            }
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

            _shortcutBar.ConnectToHero(heroComponent, inventoryGrid);
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

            Debug.Log("[MainGameScene] Reconnected all UI to new hero");
        }

        public override void Update()
        {
            base.Update();
            _settingsUI?.Update();
            // Remove duplicate HeroUI update since SettingsUI handles it

            // Update pause overlay visibility based on pause state
            var pauseService = Core.Services.GetService<PauseService>();
            if (pauseService != null && _pauseOverlayEntity != null)
            {
                _pauseOverlayEntity.SetEnabled(pauseService.IsPaused);
            }

            // Keep pit level label up to date
            UpdatePitLevelLabel();
            UpdateHeroHUD();
            UpdateHudFontMode();

            // Update shortcut bar position (handles offset when inventory open)
            PositionShortcutBar();

            // Refresh shortcut bar to keep it in sync with inventory
            _shortcutBar?.RefreshItems();

            // Handle keyboard shortcuts via shortcut bar
            _shortcutBar?.HandleKeyboardShortcuts();

            // Update mercenary manager
            var mercenaryManager = Core.Services.GetService<MercenaryManager>();
            mercenaryManager?.Update();

            // Check if hero needs to be promoted from mercenary
            var heroPromotionService = Core.Services.GetService<HeroPromotionService>();
            heroPromotionService?.CheckAndPromoteIfNeeded();

            // Handle mercenary hover and click detection
            HandleMercenaryHover();
            HandleMercenaryClicks();
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
            
            for (int i = 0; i < mercenaries.Count; i++)
            {
                var mercEntity = mercenaries[i];
                var mercComponent = mercEntity.GetComponent<MercenaryComponent>();
                
                // Skip hired mercenaries, mercenaries being removed, and mercenaries being promoted
                if (mercComponent == null || mercComponent.IsHired || mercComponent.IsBeingRemoved || mercComponent.IsBeingPromoted)
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
                nameLabel.SetFont(Content.LoadBitmapFont("Content/Fonts/HUD.fnt"));
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

            // Don't process clicks if dialog is already open
            if (_mercenaryHireDialog?.IsDialogVisible == true)
                return;

            var mercenaryManager = Core.Services.GetService<MercenaryManager>();
            if (mercenaryManager == null)
                return;

            // Don't show dialog if player can't hire more mercenaries (includes hiring block check)
            if (!mercenaryManager.CanHireMore())
                return;

            // Get mouse position in world coordinates
            var mousePos = Camera.MouseToWorldPoint();

            // Find all mercenary entities
            var mercenaries = FindEntitiesWithTag(GameConfig.TAG_MERCENARY);
            
            for (int i = 0; i < mercenaries.Count; i++)
            {
                var mercEntity = mercenaries[i];
                var mercComponent = mercEntity.GetComponent<MercenaryComponent>();
                
                // Skip hired mercenaries, mercenaries being removed, and mercenaries being promoted
                if (mercComponent == null || mercComponent.IsHired || mercComponent.IsBeingRemoved || mercComponent.IsBeingPromoted)
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
    }
}