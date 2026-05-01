using System;
using Microsoft.Xna.Framework;
using Nez;
using Nez.BitmapFonts;
using Nez.Sprites;
using Nez.Tiled;
using Nez.UI; // Added for Label
using PitHero.AI;
using PitHero.AI.Interfaces;
using PitHero.ECS.Components;
using PitHero.Services;
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
        private Label _pitLevelLabel; // UI label showing pit level
        private Label _fundsLabel; // UI label showing total funds
        private int _lastDisplayedPitLevel = -1; // Track last displayed level to avoid string churn
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
        private EventConsolePanel _eventConsolePanel; // MMO-style event log panel in the lower-right corner

        // HUD fonts for different shrink levels
        public BitmapFont _hudFontNormal;
        public BitmapFont _hudFontHalf;
        private LabelStyle _pitLevelStyleNormal;
        private LabelStyle _pitLevelStyleHalf;
        private enum HudMode { Normal, Half }
        private HudMode _currentHudMode = HudMode.Normal;

        // Cached base positions for top-left anchored UI (so offsets are relative and centralized)
        private const float PitLabelBaseX = 10f; // X position for Pit Lv label (bottom-left)
        private const float PitLabelBaseY = 350f; // Y position for Pit Lv label (bottom-left, ~30px from bottom at 360px height)
        private const float FundsLabelBaseX = 120f; // X position for Funds label (next to Pit Lv)
        private const float FundsLabelBaseY = 350f; // Y position for Funds label (same as Pit Lv)
        private const float GraphicalHudBaseX = 10f; // Base X position for graphical HUD (shifted left to fill space)
        private const float GraphicalHudBaseY = 4f; // Base Y position for graphical HUD
        private const float GraphicalHudHalfModeXOffset = 0f; // No additional X offset needed since Pit Lv is at bottom
        private const float GraphicalHudSpacing = 170f; // Spacing between HUD elements (hero to merc1, merc1 to merc2)
        private const float HudHeadXOffset = 64f; // X offset to center viz over the HUD head sprite
        private const float HudHeadYOffset = 30f; // Y offset for viz start position (32px below HUD head top)

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
            _hudFontNormal = Content.LoadBitmapFont(GameConfig.FontPathHud);
            // New enlarged fonts for smaller window modes
            _hudFontHalf = Content.LoadBitmapFont(GameConfig.FontPathHud2x);
            HudFont = _hudFontNormal; // maintain old field

            // Pre-create label styles to avoid per-frame allocations
            _pitLevelStyleNormal = new LabelStyle(_hudFontNormal, Color.White);
            _pitLevelStyleHalf = new LabelStyle(_hudFontHalf, Color.White);

            // Register game event service so systems can broadcast events to the event console.
            Core.Services.AddService(new Services.GameEventService());

            // Register crystal collection service before UI is built so CrystalsTab can
            // resolve it via Core.Services.GetService<CrystalCollectionService>() during Initialize.
            Core.Services.AddService(new Services.CrystalCollectionService());

            SetupUIOverlay();
        }

        /// <summary>
        /// Removes scene-specific services and unloads the cached TiledMap so a new
        /// MainGameScene can register them again with a fresh map from disk.
        /// </summary>
        public override void Unload()
        {
            _eventConsolePanel?.Dispose();
            Core.Content.UnloadAsset<TmxMap>(_mapPath);
            Core.Services.RemoveService(typeof(Services.GameEventService));
            Core.Services.RemoveService(typeof(Services.CrystalCollectionService));
            Core.Services.RemoveService(typeof(MercenaryManager));
            Core.Services.RemoveService(typeof(AlliedMonsterManager));
            Core.Services.RemoveService(typeof(HeroPromotionService));
            Core.Services.RemoveService(typeof(PlayerInteractionService));
            Core.Services.RemoveService(typeof(TiledMapService));
            Core.Services.RemoveService(typeof(PitWidthManager));
            Core.Services.RemoveService(typeof(ShortcutBarService));
            Core.Services.RemoveService(typeof(SettingsUI));
        }

        public override void Begin()
        {
            base.Begin();
            if (_isInitializationComplete)
                return;

            LoadMap();
            SpawnPit();

            // Only generate the default pit level when there is no save to load.
            // When a save exists, ApplyPendingLoadData will call SetPitLevel with the
            // saved level.  Generating here first would create deferred entities that
            // ClearExistingPitEntities cannot find, producing a conflicting dual-state pit.
            var pitWidthManager = Core.Services.GetService<PitWidthManager>();
            if (pitWidthManager != null && SaveLoadService.PendingLoadData == null)
                pitWidthManager.SetPitLevel(1);

            SpawnHero();
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

            _isInitializationComplete = true;
        }

        /// <summary>Applies pending save data to restore game state after scene initialization.</summary>
        private void ApplyPendingLoadData()
        {
            var pendingData = SaveLoadService.PendingLoadData;
            if (pendingData == null)
                return;

            // Clear pending data so it's not applied again
            SaveLoadService.PendingLoadData = null;
            
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
            
            // Adjust MP from max to saved value
            int mpDiff = hero.MaxMP - pendingData.CurrentMP;
            if (mpDiff > 0)
                hero.SpendMP(mpDiff);
            
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
            
            // Restore pit level (always call SetPitLevel so the pit is generated once
            // from saved state; the initial SetPitLevel(1) in Begin is skipped when
            // pending load data exists to prevent a conflicting dual-state pit)
            var pitManager = Core.Services.GetService<PitWidthManager>();
            if (pitManager != null)
            {
                pitManager.SetPitLevel(Math.Max(1, pendingData.PitLevel));
            }
            
            // Restore allied monsters
            var alliedManager = Core.Services.GetService<AlliedMonsterManager>();
            if (alliedManager != null && pendingData.AlliedMonsters != null)
            {
                for (int i = 0; i < pendingData.AlliedMonsters.Count; i++)
                {
                    var saved = pendingData.AlliedMonsters[i];
                    var allied = new AlliedMonster(
                        saved.Name, saved.MonsterTypeName,
                        saved.FishingProficiency, saved.CookingProficiency, saved.FarmingProficiency);
                    alliedManager.AddAlliedMonster(allied);
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
                                vaultService.AddItem(consumable);
                            }
                            else
                            {
                                for (int q = 0; q < vi.Quantity; q++)
                                {
                                    if (ItemRegistry.TryCreateItem(vi.Name, out var gearCopy))
                                        vaultService.AddItem(gearCopy);
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
            var txtSvc = Core.Services.GetService<TextService>();
            if (evtSvc == null || txtSvc == null) return;

            var heroComp = FindEntity("hero")?.GetComponent<ECS.Components.HeroComponent>();
            string heroName = heroComp?.LinkedHero?.Name ?? "Hero";

            evtSvc.Emit(Services.ConsoleSegment.Build(txtSvc.DisplayText(TextType.UI, UITextKey.ConsoleWelcome),
                (heroName, GameConfig.ConsoleColorHeroName)));

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

        /// <summary>
        /// Spawns the initial hero at tile (62, 6)
        /// </summary>
        private void SpawnHero()
        {
            CreateHeroEntity(62, 6);
        }

        /// <summary>
        /// Creates a hero entity at the specified tile coordinates using HeroDesign for appearance.
        /// When needsCrystal is true, the hero spawns without a crystal and waits for the promotion ceremony.
        /// </summary>
        private Entity CreateHeroEntity(int tileX, int tileY, bool needsCrystal = false)
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
            heroBodyAnimator.SetRenderLayer(GameConfig.RenderLayerHeroBody);
            heroBodyAnimator.SetLocalOffset(offset);

            // Hand2 layer (top-most paperdoll layer)
            var heroHand2Animator = hero.AddComponent(new HeroHand2AnimationComponent(design.SkinColor));
            heroHand2Animator.SetRenderLayer(GameConfig.RenderLayerHeroHand2);
            heroHand2Animator.SetLocalOffset(offset);
            heroHand2Animator.ComponentColor = design.SkinColor;

            // Pants layer
            var heroPantsAnimator = hero.AddComponent(new HeroPantsAnimationComponent(Color.White));
            heroPantsAnimator.SetRenderLayer(GameConfig.RenderLayerHeroPants);
            heroPantsAnimator.SetLocalOffset(offset);

            // Shirt layer
            var heroShirtAnimator = hero.AddComponent(new HeroShirtAnimationComponent(design.ShirtColor));
            heroShirtAnimator.SetRenderLayer(GameConfig.RenderLayerHeroShirt);
            heroShirtAnimator.SetLocalOffset(offset);

            // Head layer
            var heroHeadAnimator = hero.AddComponent(new HeroHeadAnimationComponent(design.SkinColor));
            heroHeadAnimator.SetRenderLayer(GameConfig.RenderLayerHeroHead);
            heroHeadAnimator.SetLocalOffset(offset);
            heroHeadAnimator.ComponentColor = design.SkinColor;

            // Eyes layer
            var heroEyesAnimator = hero.AddComponent(new HeroEyesAnimationComponent(Color.White));
            heroEyesAnimator.SetRenderLayer(GameConfig.RenderLayerHeroEyes);
            heroEyesAnimator.SetLocalOffset(offset);

            // Hair layer
            var heroHairAnimator = hero.AddComponent(new HeroHairAnimationComponent(design.HairColor, design.HairstyleIndex));
            heroHairAnimator.SetRenderLayer(GameConfig.RenderLayerHeroHair);
            heroHairAnimator.SetLocalOffset(offset);

            // Hand1 layer (bottom-most paperdoll layer)
            var heroHand1Animator = hero.AddComponent(new HeroHand1AnimationComponent(design.SkinColor));
            heroHand1Animator.SetRenderLayer(GameConfig.RenderLayerHeroHand1);
            heroHand1Animator.SetLocalOffset(offset);
            heroHand1Animator.ComponentColor = design.SkinColor;

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
                heroCrystal.EarnJP(550);

                // Create the linked Hero from the crystal
                heroComponent.LinkedHero = new RolePlayingFramework.Heroes.Hero(design.Name, heroJob, 1, baseStats, heroCrystal);

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
            hero.AddComponent(new HeroStateMachine());
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

            Debug.Log($"[MainGameScene] Resetting pit from level {pitManager.CurrentPitLevel} to level 1 after hero death.");
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
            bodyAnimator.SetRenderLayer(GameConfig.RenderLayerHeroBody);
            bodyAnimator.SetLocalOffset(offset);

            var hand2Animator = innkeeperEntity.AddComponent(new HeroHand2AnimationComponent(bodyColor));
            hand2Animator.SetRenderLayer(GameConfig.RenderLayerHeroHand2);
            hand2Animator.SetLocalOffset(offset);

            var pantsAnimator = innkeeperEntity.AddComponent(new HeroPantsAnimationComponent(Color.White));
            pantsAnimator.SetRenderLayer(GameConfig.RenderLayerHeroPants);
            pantsAnimator.SetLocalOffset(offset);

            // Use a distinctive shirt color for innkeeper (brown/beige for apron-like appearance)
            var shirtColor = new Color(140, 91, 62); // Brown
            var shirtAnimator = innkeeperEntity.AddComponent(new HeroShirtAnimationComponent(shirtColor));
            shirtAnimator.SetRenderLayer(GameConfig.RenderLayerHeroShirt);
            shirtAnimator.SetLocalOffset(offset);

            var headAnimator = innkeeperEntity.AddComponent(new HeroHeadAnimationComponent(bodyColor));
            headAnimator.SetRenderLayer(GameConfig.RenderLayerHeroHead);
            headAnimator.SetLocalOffset(offset);

            var eyesAnimator = innkeeperEntity.AddComponent(new HeroEyesAnimationComponent(Color.White));
            eyesAnimator.SetRenderLayer(GameConfig.RenderLayerHeroEyes);
            eyesAnimator.SetLocalOffset(offset);

            // Gray hair for older innkeeper appearance
            var hairColor = new Color(100, 100, 100); // Gray
            var hairAnimator = innkeeperEntity.AddComponent(new HeroHairAnimationComponent(hairColor, hairstyleIndex: 1)); // Use default hairstyle for innkeeper
            hairAnimator.SetRenderLayer(GameConfig.RenderLayerHeroHair);
            hairAnimator.SetLocalOffset(offset);

            var hand1Animator = innkeeperEntity.AddComponent(new HeroHand1AnimationComponent(bodyColor));
            hand1Animator.SetRenderLayer(GameConfig.RenderLayerHeroHand1);
            hand1Animator.SetLocalOffset(offset);

            Debug.Log($"[MainGameScene] Innkeeper spawned at tile ({tileX}, {tileY}) facing left");
        }

        private void SetupUIOverlay()
        {
            var screenSpaceRenderer = new ScreenSpaceRenderer(100, [GameConfig.TransparentPauseOverlay, GameConfig.RenderLayerUI, GameConfig.RenderLayerGraphicalHUD, GameConfig.RenderLayerActionQueue]);
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
            uiCanvas.IsFullScreen = false;
            uiCanvas.RenderLayer = GameConfig.RenderLayerUI;

            _settingsUI = new SettingsUI(Core.Instance);
            _settingsUI.InitializeUI(uiCanvas.Stage);
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
            _pitLevelLabel.SetPosition(PitLabelBaseX, PitLabelBaseY);

            // Funds label (bottom-left next to Pit Lv, always visible, no scaling)
            _fundsLabel = uiCanvas.Stage.AddElement(new Label("Gold: 0", _hudFontNormal));
            _fundsLabel.SetStyle(_pitLevelStyleNormal);
            _fundsLabel.SetPosition(FundsLabelBaseX, FundsLabelBaseY);

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

            // Create screen-space action queue visualization entities positioned over HUD heads
            var heroVizEntity = CreateEntity("hero-action-queue-viz");
            heroVizEntity.SetPosition(GraphicalHudBaseX + HudHeadXOffset, GraphicalHudBaseY + HudHeadYOffset);
            _heroActionQueueViz = heroVizEntity.AddComponent(new ActionQueueVisualizationComponent());
            _heroActionQueueViz.SetRenderLayer(GameConfig.RenderLayerActionQueue);

            var merc1VizEntity = CreateEntity("merc1-action-queue-viz");
            merc1VizEntity.SetPosition(GraphicalHudBaseX + GraphicalHudSpacing + HudHeadXOffset, GraphicalHudBaseY + HudHeadYOffset);
            _merc1ActionQueueViz = merc1VizEntity.AddComponent(new ActionQueueVisualizationComponent());
            _merc1ActionQueueViz.SetRenderLayer(GameConfig.RenderLayerActionQueue);
            _merc1ActionQueueViz.SetEnabled(false);

            var merc2VizEntity = CreateEntity("merc2-action-queue-viz");
            merc2VizEntity.SetPosition(GraphicalHudBaseX + GraphicalHudSpacing * 2 + HudHeadXOffset, GraphicalHudBaseY + HudHeadYOffset);
            _merc2ActionQueueViz = merc2VizEntity.AddComponent(new ActionQueueVisualizationComponent());
            _merc2ActionQueueViz.SetRenderLayer(GameConfig.RenderLayerActionQueue);
            _merc2ActionQueueViz.SetEnabled(false);

            // Shortcut bar at bottom center
            _shortcutBar = new ShortcutBar();
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
                _eventConsolePanel.SetPosition(GameConfig.VirtualWidth - 790f, GameConfig.VirtualHeight - 120f - 16f);
                uiCanvas.Stage.AddElement(_eventConsolePanel);
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
            }

            // Pit level label and Funds label stay at bottom-left with no scaling or offset changes
            // (They are always at their base positions)

            // Update graphical HUD position based on mode (no scaling needed - it's in screen space)
            // Apply vertical offset for normal/half mode
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

            if (_graphicalHUD != null)
            {
                var hudEntity = _graphicalHUD.Entity;
                if (hudEntity != null)
                {
                    float hudTargetY = GraphicalHudBaseY + yOffset;
                    float hudTargetX = GraphicalHudBaseX; // No extra offset needed

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
                    float hudTargetX = GraphicalHudBaseX + GraphicalHudSpacing; // No extra offset needed

                    merc1Entity.SetPosition(hudTargetX, hudTargetY);
                }
            }

            if (_mercenary2HUD != null)
            {
                var merc2Entity = _mercenary2HUD.Entity;
                if (merc2Entity != null)
                {
                    float hudTargetY = GraphicalHudBaseY + yOffset;
                    float hudTargetX = GraphicalHudBaseX + GraphicalHudSpacing * 2; // No extra offset needed

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
            UpdateFundsLabel();
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

            // Check if a living hero who respawned without a crystal has arrived at the statue
            _heroPromotionService?.CheckAndPromoteHeroIfNeeded();

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

            // Don't process clicks if dialog is already open
            if (_mercenaryHireDialog?.IsDialogVisible == true)
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
    }
}