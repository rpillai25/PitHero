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

                default:
                    return false;
            }
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
