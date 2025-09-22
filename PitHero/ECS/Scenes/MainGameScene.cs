using Microsoft.Xna.Framework;
using Nez;
using Nez.BitmapFonts;
using Nez.Tiled;
using Nez.UI; // Added for Label
using PitHero.AI;
using PitHero.ECS.Components;
using PitHero.Services;
using PitHero.UI;
using PitHero.Util;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Jobs;
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
        private Label _heroLevelLabel; // UI label showing hero level
        private Label _heroHpLabel; // UI label showing hero HP
        private int _lastDisplayedPitLevel = -1; // Track last displayed level to avoid string churn
        private int _lastDisplayedHeroLevel = -1; // Track last displayed hero level
        private int _lastDisplayedHeroHp = -1; // Track last displayed hero HP

        // HUD fonts for different shrink levels
        public BitmapFont _hudFontNormal;
        public BitmapFont _hudFontHalf;
        public BitmapFont _hudFontQuarter;
        private LabelStyle _pitLevelStyleNormal;
        private LabelStyle _pitLevelStyleHalf;
        private LabelStyle _pitLevelStyleQuarter;
        private LabelStyle _heroLevelStyleNormal;
        private LabelStyle _heroLevelStyleHalf;
        private LabelStyle _heroLevelStyleQuarter;
        private LabelStyle _heroHpStyleNormal;
        private LabelStyle _heroHpStyleHalf;
        private LabelStyle _heroHpStyleQuarter;
        private enum HudMode { Normal, Half, Quarter }
        private HudMode _currentHudMode = HudMode.Normal;

        // Cached base Y for top-left anchored UI (so offsets are relative and centralized)
        private const float PitLabelBaseY = 16f; // original Y before offsets applied

        public BitmapFont HudFont; // legacy reference (normal)

        public BitmapFont GetHudFontForCurrentMode()
        {
            return _currentHudMode switch
            {
                HudMode.Normal => _hudFontNormal,
                HudMode.Half => _hudFontHalf,
                HudMode.Quarter => _hudFontQuarter,
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
            _hudFontQuarter = Content.LoadBitmapFont("Content/Fonts/Hud4x.fnt");
            HudFont = _hudFontNormal; // maintain old field

            // Pre-create label styles to avoid per-frame allocations
            _pitLevelStyleNormal = new LabelStyle(_hudFontNormal, Color.White);
            _pitLevelStyleHalf = new LabelStyle(_hudFontHalf, Color.White);
            _pitLevelStyleQuarter = new LabelStyle(_hudFontQuarter, Color.White);

            // We'll use the same styles for hero level and HP labels
            _heroLevelStyleNormal = new LabelStyle(_hudFontNormal, Color.White);
            _heroLevelStyleHalf = new LabelStyle(_hudFontHalf, Color.White);
            _heroLevelStyleQuarter = new LabelStyle(_hudFontQuarter, Color.White);

            _heroHpStyleNormal = new LabelStyle(_hudFontNormal, Color.White);
            _heroHpStyleHalf = new LabelStyle(_hudFontHalf, Color.White);
            _heroHpStyleQuarter = new LabelStyle(_hudFontQuarter, Color.White);

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
            AddPitLevelTestComponent();

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
            
            var heroTileX = Random.Range(minHeroTileX, maxHeroTileX + 1);
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

            Flags.SetFlag(ref collider.CollidesWithLayers, GameConfig.PhysicsTileMapLayer);
            Flags.SetFlag(ref collider.CollidesWithLayers, GameConfig.PhysicsPitLayer);
            Flags.SetFlagExclusive(ref collider.PhysicsLayer, GameConfig.PhysicsHeroWorldLayer);

            hero.AddComponent(new TileByTileMover());
            var tileMover = hero.GetComponent<TileByTileMover>();
            tileMover.MovementSpeed = GameConfig.HeroMovementSpeed;
            Debug.Log("[MainGameScene] Added TileByTileMover to hero for tile-based movement");

            var heroComponent = hero.AddComponent(new HeroComponent
            {
                Health = 100,
                MaxHealth = 100,
                PitInitialized = true
            });

            // Initialize a test HeroCrystal for crystal-infused stats
            var testJob = new RolePlayingFramework.Jobs.Knight(); // Using Knight job for testing
            var baseStats = new StatBlock(strength: 10, agility: 8, vitality: 12, magic: 5);
            var testCrystal = new HeroCrystal("Test Hero", testJob, 5, baseStats); // Level 5 hero for testing
            
            // Create the linked Hero from the crystal
            heroComponent.LinkedHero = new RolePlayingFramework.Heroes.Hero("Test Hero", testJob, 5, baseStats, testCrystal);
            
            Debug.Log($"[MainGameScene] Created test hero with Level {heroComponent.LinkedHero.Level}, HP {heroComponent.LinkedHero.CurrentHP}/{heroComponent.LinkedHero.MaxHP}");
            
            // Add BouncyDigitComponent for damage display (RenderLayerUI, disabled initially)
            var heroBouncyDigit = hero.AddComponent(new BouncyDigitComponent());
            heroBouncyDigit.SetRenderLayer(GameConfig.RenderLayerUI);
            heroBouncyDigit.SetEnabled(false);
            
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

        private void SetupUIOverlay()
        {
            var screenSpaceRenderer = new ScreenSpaceRenderer(100, [GameConfig.TransparentPauseOverlay, GameConfig.RenderLayerUI]);
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

            // Pit level label (always visible top-left). Base position then offset applied per shrink level.
            _pitLevelLabel = uiCanvas.Stage.AddElement(new Label("Pit Lv. 1", _hudFontNormal));
            _pitLevelLabel.SetStyle(_pitLevelStyleNormal);
            _pitLevelLabel.SetPosition(10, PitLabelBaseY);

            // Hero level label (to the right of pit level)
            _heroLevelLabel = uiCanvas.Stage.AddElement(new Label("Hero Lv. 1", _hudFontNormal));
            _heroLevelLabel.SetStyle(_heroLevelStyleNormal);
            _heroLevelLabel.SetPosition(120, PitLabelBaseY); // Offset to the right

            // Hero HP label (to the right of hero level)
            _heroHpLabel = uiCanvas.Stage.AddElement(new Label("HP: 100", _hudFontNormal));
            _heroHpLabel.SetStyle(_heroHpStyleNormal);
            _heroHpLabel.SetPosition(240, PitLabelBaseY); // Offset further to the right
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
        /// Update hero level and HP labels when they change
        /// </summary>
        private void UpdateHeroLabels()
        {
            if (_heroLevelLabel == null || _heroHpLabel == null)
                return;

            var hero = FindEntity("hero");
            if (hero == null)
                return;

            var heroComponent = hero.GetComponent<HeroComponent>();
            if (heroComponent?.LinkedHero == null)
                return;

            var linkedHero = heroComponent.LinkedHero;
            
            // Update hero level if changed
            if (linkedHero.Level != _lastDisplayedHeroLevel)
            {
                _heroLevelLabel.SetText($"Hero Lv. {linkedHero.Level}");
                _lastDisplayedHeroLevel = linkedHero.Level;
            }

            // Update hero HP if changed
            if (linkedHero.CurrentHP != _lastDisplayedHeroHp)
            {
                _heroHpLabel.SetText($"HP: {linkedHero.CurrentHP}");
                _lastDisplayedHeroHp = linkedHero.CurrentHP;
            }
        }

        /// <summary>
        /// Update HUD font and Y offset based on current shrink mode
        /// </summary>
        private void UpdateHudFontMode()
        {
            HudMode desired;
            if (WindowManager.IsQuarterHeightMode())
                desired = HudMode.Quarter;
            else if (WindowManager.IsHalfHeightMode())
                desired = HudMode.Half;
            else
                desired = HudMode.Normal;

            if (desired != _currentHudMode)
            {
                switch (desired)
                {
                    case HudMode.Normal:
                        _pitLevelLabel.SetStyle(_pitLevelStyleNormal);
                        _heroLevelLabel.SetStyle(_heroLevelStyleNormal);
                        _heroHpLabel.SetStyle(_heroHpStyleNormal);
                        break;
                    case HudMode.Half:
                        _pitLevelLabel.SetStyle(_pitLevelStyleHalf);
                        _heroLevelLabel.SetStyle(_heroLevelStyleHalf);
                        _heroHpLabel.SetStyle(_heroHpStyleHalf);
                        break;
                    case HudMode.Quarter:
                        _pitLevelLabel.SetStyle(_pitLevelStyleQuarter);
                        _heroLevelLabel.SetStyle(_heroLevelStyleQuarter);
                        _heroHpLabel.SetStyle(_heroHpStyleQuarter);
                        break;
                }
                _currentHudMode = desired;
                _pitLevelLabel.Invalidate();
                _heroLevelLabel.Invalidate();
                _heroHpLabel.Invalidate();
            }

            // Apply vertical offset based on mode
            int yOffset = 0;
            switch (_currentHudMode)
            {
                case HudMode.Half:
                    yOffset = GameConfig.TopUiYOffsetHalf;
                    break;
                case HudMode.Quarter:
                    yOffset = GameConfig.TopUiYOffsetQuarter;
                    break;
                case HudMode.Normal:
                default:
                    yOffset = GameConfig.TopUiYOffsetNormal;
                    break;
            }
            // Only update position if changed to avoid redundant property sets
            float targetY = PitLabelBaseY + yOffset;
            if (System.Math.Abs(_pitLevelLabel.GetY() - targetY) > 0.1f)
            {
                _pitLevelLabel.SetY(targetY);
                _heroLevelLabel.SetY(targetY);
                _heroHpLabel.SetY(targetY);
            }
        }

        public override void Update()
        {
            base.Update();
            _settingsUI?.Update();

            // Update pause overlay visibility based on pause state
            var pauseService = Core.Services.GetService<PauseService>();
            if (pauseService != null && _pauseOverlayEntity != null)
            {
                _pauseOverlayEntity.SetEnabled(pauseService.IsPaused);
            }

            // Keep pit level label up to date
            UpdatePitLevelLabel();
            UpdateHeroLabels();
            UpdateHudFontMode();
        }
    }
}