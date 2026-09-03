using Nez;
using PitHero.ECS.Components;
using PitHero.Services.Analytics;
using RolePlayingFramework.AlliedMonsters;
using RolePlayingFramework.Jobs;

namespace PitHero.Services.Replay
{
    /// <summary>Automation toggles addressable by <see cref="PlayerCommandType.SetAutomation"/>. Persisted; append only.</summary>
    public enum AutomationKind
    {
        MonsterJobs = 0,
        SeedPurchase = 1,
        CropSell = 2,
        SellExcess = 3,
        ItemPurchase = 4,
        PurchaseMercGear = 5,
        HireMercs = 6,
        LearnSkills = 7,
    }

    /// <summary>
    /// Second half of the command dispatch: automation, thresholds, mercenaries and monsters.
    /// Returns false for types with no handler so the caller can warn.
    /// </summary>
    public static partial class PlayerCommandHandlers
    {
        private static bool ApplyExtended(in PlayerCommand cmd)
        {
            switch (cmd.Type)
            {
                case PlayerCommandType.SetReplenishThresholds:
                {
                    var hero = GetHeroComponent();
                    if (hero != null)
                    {
                        if (cmd.A >= 0) hero.ReplenishHPThreshold = cmd.A / 100f;
                        if (cmd.B >= 0) hero.ReplenishMPThreshold = cmd.B / 100f;
                    }
                    return true;
                }
                case PlayerCommandType.SetAutomation:
                    ApplyAutomation((AutomationKind)cmd.A, cmd.B != 0);
                    return true;
                case PlayerCommandType.SetGoldBuffer:
                {
                    var svc = Services?.GetService<AutoSeedPurchaseService>();
                    if (svc != null) svc.GoldBuffer = cmd.A < 0 ? 0 : cmd.A;
                    return true;
                }
                case PlayerCommandType.SetAutoLearnMode:
                {
                    var svc = Services?.GetService<AutoLearnSkillsService>();
                    if (svc != null)
                    {
                        svc.Mode = AutoLearnSkillsService.SanitizeMode(cmd.A);
                        if (svc.Enabled) svc.TryLearnNow();
                    }
                    return true;
                }
                case PlayerCommandType.SetAutoHireJobSlot:
                {
                    var svc = Services?.GetService<AutoHireMercenaryService>();
                    if (svc != null)
                    {
                        var job = AutoHireMercenaryService.SanitizeJob((JobType)cmd.B);
                        if (cmd.A == 1) svc.Merc1Job = job; else svc.Merc2Job = job;
                    }
                    return true;
                }

                case PlayerCommandType.HireMercenary:
                {
                    var mgr = Services?.GetService<MercenaryManager>();
                    var entity = ResolveMercenary(mgr?.GetUnhiredMercenaries(), cmd.A, cmd.S);
                    if (entity != null)
                    {
                        if (mgr.HireMercenary(entity))
                            Debug.Log($"[PlayerCommandHandlers] Hired {cmd.S}");
                        else
                            Debug.Log($"[PlayerCommandHandlers] Could not hire {cmd.S}");
                    }
                    return true;
                }
                case PlayerCommandType.DismissTavernMercenary:
                {
                    var mgr = Services?.GetService<MercenaryManager>();
                    var entity = ResolveMercenary(mgr?.GetUnhiredMercenaries(), cmd.A, cmd.S);
                    if (entity != null)
                        mgr.DismissTavernMercenary(entity);
                    return true;
                }
                case PlayerCommandType.DismissPartyMercenary:
                {
                    var mgr = Services?.GetService<MercenaryManager>();
                    var entity = ResolveMercenary(mgr?.GetHiredMercenaries(), cmd.A, cmd.S);
                    if (entity != null)
                    {
                        mgr.DismissPartyMercenary(entity);
                        GetSettingsUI()?.HeroUI?.RefreshAfterPartyChange();
                    }
                    return true;
                }
                case PlayerCommandType.SetMonsterJob:
                    ApplySetMonsterJob(in cmd);
                    return true;
                case PlayerCommandType.PurchaseMonster:
                    MainScene?.AddMonsterDialog?.ApplyPurchase(cmd.A, cmd.S, cmd.B);
                    return true;

                // ── Shortcut bar assignment ──────────────────────────────────────────
                case PlayerCommandType.SetShortcutItem:
                    GetShortcutBar()?.ApplySetItemShortcut(cmd.A, cmd.B);
                    return true;
                case PlayerCommandType.SetShortcutSkill:
                    GetShortcutBar()?.ApplySetSkillShortcut(cmd.A, cmd.S, cmd.B);
                    return true;
                case PlayerCommandType.ClearShortcut:
                    GetShortcutBar()?.ClearShortcutReference(cmd.A);
                    return true;
                case PlayerCommandType.SwapShortcuts:
                    GetShortcutBar()?.SwapShortcuts(cmd.A, cmd.B);
                    return true;

                // ── Stencils ─────────────────────────────────────────────────────────
                case PlayerCommandType.PlaceStencil:
                    GetGrid((int)cmd.L)?.ApplyPlaceStencil(cmd.S, cmd.A, cmd.B);
                    GetSettingsUI()?.HeroUI?.RefreshStencilButtonStates();
                    return true;
                case PlayerCommandType.RemoveStencil:
                    GetGrid((int)cmd.L)?.ApplyRemoveStencil(cmd.S);
                    GetSettingsUI()?.HeroUI?.RefreshStencilButtonStates();
                    return true;
                case PlayerCommandType.MoveStencil:
                    GetGrid((int)cmd.L)?.ApplyMoveStencil(cmd.S, cmd.A, cmd.B);
                    return true;

                // ── Automation option dialogs ────────────────────────────────────────
                case PlayerCommandType.SetAutoPurchaseSelected:
                {
                    var svc = Services?.GetService<AutoItemPurchaseService>();
                    if (svc != null && cmd.A >= 0 && cmd.A < svc.ConsumableSelected.Length)
                        svc.ConsumableSelected[cmd.A] = cmd.B != 0;
                    return true;
                }
                case PlayerCommandType.SetConsumableStackTarget:
                {
                    var svc = Services?.GetService<AutoItemPurchaseService>();
                    if (svc != null && cmd.A >= 0 && cmd.A < svc.ConsumableStackTargets.Length)
                        svc.ConsumableStackTargets[cmd.A] = cmd.B;
                    return true;
                }
                case PlayerCommandType.SetConsumableSellAllowed:
                {
                    var svc = Services?.GetService<AutoSellExcessItemsService>();
                    if (svc != null && cmd.A >= 0 && cmd.A < svc.ConsumableSellAllowed.Length)
                        svc.ConsumableSellAllowed[cmd.A] = cmd.B != 0;
                    return true;
                }
                case PlayerCommandType.SetConsumableMinStacks:
                {
                    var svc = Services?.GetService<AutoSellExcessItemsService>();
                    if (svc != null && cmd.A >= 0 && cmd.A < svc.ConsumableMinStacks.Length)
                        svc.ConsumableMinStacks[cmd.A] = cmd.B;
                    return true;
                }
                case PlayerCommandType.SetGearFilterFlag:
                {
                    bool[] flags = null;
                    if (cmd.A == 0)
                    {
                        var svc = Services?.GetService<AutoSellExcessItemsService>();
                        flags = cmd.B == 0 ? svc?.RarityAllowed : svc?.GearTypeAllowed;
                    }
                    else
                    {
                        var svc = Services?.GetService<AutoItemPurchaseService>();
                        flags = cmd.B == 0 ? svc?.BuyRarityAllowed : svc?.BuyGearTypeAllowed;
                    }
                    if (flags != null && cmd.C >= 0 && cmd.C < flags.Length)
                        flags[cmd.C] = cmd.D != 0;
                    return true;
                }
                case PlayerCommandType.SetCropDesignation:
                {
                    var svc = Services?.GetService<AutoCropSellService>();
                    if (svc != null && cmd.A >= 0 && cmd.A < svc.Designations.Length)
                        svc.Designations[cmd.A] = cmd.B != 0;
                    return true;
                }
                case PlayerCommandType.SetCropKeepStacks:
                {
                    var svc = Services?.GetService<AutoCropSellService>();
                    if (svc != null)
                        svc.KeepStacks = cmd.A;
                    return true;
                }

                // ── Inventory / shop ─────────────────────────────────────────────────
                case PlayerCommandType.SwapSlots:
                    GetGrid((int)cmd.L)?.ApplySwapCommand(cmd.A, cmd.B);
                    return true;
                case PlayerCommandType.SellBagItem:
                    GetGrid(cmd.C)?.DiscardItem(cmd.A, cmd.B <= 0 ? int.MaxValue : cmd.B);
                    return true;
                case PlayerCommandType.BuyVaultItem:
                    GetSettingsUI()?.SecondChanceShopUI?.ApplyItemPurchase(cmd.A, cmd.S, cmd.B, cmd.C);
                    return true;
                case PlayerCommandType.BuyVaultCrystal:
                    GetSettingsUI()?.SecondChanceShopUI?.ApplyCrystalPurchase(cmd.A, cmd.S, cmd.B, cmd.C);
                    return true;
                case PlayerCommandType.BuySeeds:
                    ApplyBuySeeds((PitHero.Farming.CropType)cmd.A, cmd.B);
                    return true;

                // ── Farm / construction / storage ────────────────────────────────────
                case PlayerCommandType.PlaceBuilding:
                    MainScene?.BuildingModeOverlay?.ApplyPlacement((PitHero.Util.BuildingType)cmd.A, cmd.B, cmd.C, cmd.D);
                    return true;
                case PlayerCommandType.MoveBuilding:
                    MainScene?.BuildingModeOverlay?.ApplyMove(cmd.A, cmd.B, cmd.C);
                    return true;
                case PlayerCommandType.RemoveBuilding:
                    ApplyRemoveBuilding(cmd.A);
                    return true;
                case PlayerCommandType.TillTile:
                    MainScene?.TillModeOverlay?.ApplyMarkTill(cmd.A, cmd.B);
                    return true;
                case PlayerCommandType.UnmarkTillTile:
                    MainScene?.TillModeOverlay?.ApplyUnmarkTill(cmd.A, cmd.B);
                    return true;
                case PlayerCommandType.RestoreGrassTile:
                {
                    var tile = new Microsoft.Xna.Framework.Point(cmd.A, cmd.B);
                    Services?.GetService<WetTileService>()?.ClearWet(tile);
                    Services?.GetService<TilledTileService>()?.RestoreGrassTile(tile);
                    return true;
                }
                case PlayerCommandType.AddCropPlan:
                    MainScene?.SeedModeOverlay?.ApplyPlaceCrop((PitHero.Farming.CropType)cmd.A, cmd.B, cmd.C);
                    return true;
                case PlayerCommandType.RemoveCropPlan:
                    if (Services != null)
                        PitHero.UI.SeedPlantingModeOverlay.ApplyRemovePlan(cmd.A, cmd.B);
                    return true;
                case PlayerCommandType.SellAllStorageCrops:
                    MainScene?.HarvestedCropsOverlay?.ApplySellAll();
                    return true;
                case PlayerCommandType.SellStorageCrops:
                    MainScene?.HarvestedCropsOverlay?.ApplySellStorage(cmd.A);
                    return true;
                case PlayerCommandType.MoveAllCropsToOtherStorages:
                    MainScene?.HarvestedCropsOverlay?.ApplyMoveAll(cmd.A);
                    return true;
                case PlayerCommandType.FridgeReturnSlot:
                    MainScene?.RefrigeratorDialog?.ApplyReturnSlot(cmd.A, (PitHero.Farming.CropType)cmd.B);
                    return true;
                case PlayerCommandType.FridgeSellSlot:
                    MainScene?.RefrigeratorDialog?.ApplySellSlot(cmd.A, (PitHero.Farming.CropType)cmd.B);
                    return true;
                case PlayerCommandType.FarmRescan:
                    Services?.GetService<FarmTaskCoordinator>()?.RescanForPlanting();
                    return true;
                case PlayerCommandType.AutoHirePass:
                    Services?.GetService<AutoHireMercenaryService>()?.TryHirePass();
                    return true;

                // ── Crystals ─────────────────────────────────────────────────────────
                case PlayerCommandType.CreateCrystal:
                {
                    if (Services == null)
                        return true;
                    var stats = new RolePlayingFramework.Stats.StatBlock(cmd.B, cmd.C, cmd.D, (int)cmd.L);
                    if (PitHero.UI.CrystalCreationDialog.ApplyCreate(cmd.A, stats))
                        GetSettingsUI()?.HeroUI?.RefreshCrystalsTab();
                    return true;
                }
                case PlayerCommandType.ForgeCrystals:
                    GetSettingsUI()?.HeroUI?.GetCrystalsTabComponent()?.ApplyForge(cmd.A);
                    return true;
                case PlayerCommandType.SwapCrystalSlots:
                    GetSettingsUI()?.HeroUI?.GetCrystalsTabComponent()?.ApplyCrystalSlotSwap(cmd.A, cmd.B, cmd.C, cmd.D);
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>The main game scene, or null.</summary>
        private static PitHero.ECS.Scenes.MainGameScene MainScene => CurrentScene as PitHero.ECS.Scenes.MainGameScene;

        /// <summary>Resolves the inventory grid a command targets: 0 = Party window, 1 = Second Chance shop.</summary>
        private static PitHero.UI.InventoryGrid GetGrid(int gridId)
        {
            var settings = GetSettingsUI();
            if (settings == null)
                return null;
            if (gridId == 1)
                return settings.SecondChanceShopUI?.GetHeroInventoryGrid();
            return settings.HeroUI?.GetInventoryGrid();
        }

        private static void ApplyBuySeeds(PitHero.Farming.CropType crop, int qty)
        {
            var services = Services;
            if (services == null || qty <= 0)
                return;
            var gameState = services.GetService<GameStateService>();
            var cropPlantingService = services.GetService<CropPlantingService>();
            if (gameState == null || cropPlantingService == null)
                return;
            int unitPrice = PitHero.Util.CropConfig.GetSeedPrice(crop);
            int totalPrice = unitPrice * qty;
            if (gameState.Funds < totalPrice)
                return;
            gameState.Funds -= totalPrice;
            cropPlantingService.AddSeeds(crop, qty);
            Core.GetGlobalManager<PitHero.Util.SoundEffectManager>()?.PlaySound(PitHero.Util.SoundEffectTypes.SoundEffectType.ItemPurchase);
            AnalyticsService.LogSeedPurchased(crop.ToString(), qty, totalPrice, "manual", gameState.Funds);
            services.GetService<FarmTaskCoordinator>()?.RescanForPlanting();
        }

        private static void ApplyRemoveBuilding(int uniqueId)
        {
            var services = Services;
            if (services == null)
                return;
            var buildingService = services.GetService<BuildingService>();
            if (buildingService == null)
                return;
            PlacedBuilding pb = null;
            var all = buildingService.GetAll();
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].UniqueId == uniqueId) { pb = all[i]; break; }
            }
            if (pb == null)
                return;

            // Remove first and pay only if this call is what actually removed it (non-modal dialogs
            // can stack two sell confirmations for the same building).
            int gold = PitHero.Util.BuildingConfig.GetSellPrice(pb.Type);
            if (!buildingService.RemoveBuilding(pb))
                return;
            pb.WorldEntity?.Destroy();
            services.GetService<GameStateService>()?.AddFunds(gold, "sell_building");
            Core.GetGlobalManager<PitHero.Util.SoundEffectManager>()?.PlaySound(PitHero.Util.SoundEffectTypes.SoundEffectType.ItemSell);
        }

        private static void ApplyAutomation(AutomationKind kind, bool enabled)
        {
            var services = Services;
            if (services == null)
                return;
            switch (kind)
            {
                case AutomationKind.MonsterJobs:
                {
                    var svc = services.GetService<AutoJobAssignmentService>();
                    if (svc != null)
                    {
                        svc.Enabled = enabled;
                        if (enabled) svc.ReassessNow();
                    }
                    break;
                }
                case AutomationKind.SeedPurchase:
                {
                    var svc = services.GetService<AutoSeedPurchaseService>();
                    if (svc != null) svc.Enabled = enabled;
                    break;
                }
                case AutomationKind.CropSell:
                {
                    var svc = services.GetService<AutoCropSellService>();
                    if (svc != null) svc.Enabled = enabled;
                    break;
                }
                case AutomationKind.SellExcess:
                {
                    var svc = services.GetService<AutoSellExcessItemsService>();
                    if (svc != null) svc.Enabled = enabled;
                    break;
                }
                case AutomationKind.ItemPurchase:
                {
                    var svc = services.GetService<AutoItemPurchaseService>();
                    if (svc != null) svc.Enabled = enabled;
                    break;
                }
                case AutomationKind.PurchaseMercGear:
                {
                    var svc = services.GetService<AutoItemPurchaseService>();
                    if (svc != null) svc.PurchaseMercenaryGear = enabled;
                    break;
                }
                case AutomationKind.HireMercs:
                {
                    var svc = services.GetService<AutoHireMercenaryService>();
                    if (svc != null) svc.Enabled = enabled;
                    break;
                }
                case AutomationKind.LearnSkills:
                {
                    var svc = services.GetService<AutoLearnSkillsService>();
                    if (svc != null)
                    {
                        svc.Enabled = enabled;
                        if (enabled) svc.TryLearnNow();
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// Finds a mercenary entity by roster index, falling back to a name search when the index no
        /// longer matches (the list may have shifted between the click and the tick).
        /// </summary>
        private static Entity ResolveMercenary(System.Collections.Generic.List<Entity> list, int index, string name)
        {
            if (list == null)
                return null;
            if (index >= 0 && index < list.Count && MercenaryNameMatches(list[index], name))
                return list[index];
            if (string.IsNullOrEmpty(name))
                return null;
            for (int i = 0; i < list.Count; i++)
            {
                if (MercenaryNameMatches(list[i], name))
                    return list[i];
            }
            Debug.Log($"[PlayerCommandHandlers] Mercenary {name} not found (index {index})");
            return null;
        }

        private static bool MercenaryNameMatches(Entity entity, string name)
        {
            if (string.IsNullOrEmpty(name))
                return true;
            var mc = entity?.GetComponent<MercenaryComponent>();
            return mc?.LinkedMercenary != null && mc.LinkedMercenary.Name == name;
        }

        private static void ApplySetMonsterJob(in PlayerCommand cmd)
        {
            var mgr = Services?.GetService<AlliedMonsterManager>();
            if (mgr == null)
                return;
            var roster = mgr.AlliedMonsters;
            AlliedMonster monster = null;
            if (cmd.A >= 0 && cmd.A < roster.Count && (string.IsNullOrEmpty(cmd.S) || roster[cmd.A].Name == cmd.S))
                monster = roster[cmd.A];
            else
            {
                for (int i = 0; i < roster.Count; i++)
                {
                    if (roster[i].Name == cmd.S)
                    {
                        monster = roster[i];
                        break;
                    }
                }
            }
            if (monster == null)
            {
                Debug.Log($"[PlayerCommandHandlers] Allied monster {cmd.S} not found (index {cmd.A})");
                return;
            }

            var job = (MonsterJob)cmd.B;
            if (monster.Job != job)
            {
                AnalyticsService.LogMonsterJobChanged(monster.Name, monster.MonsterTypeName,
                    monster.Job.ToString(), job.ToString(), "manual");
            }
            monster.Job = job;
            GetSettingsUI()?.MonsterUI?.RefreshMonsterList();
        }
    }
}
