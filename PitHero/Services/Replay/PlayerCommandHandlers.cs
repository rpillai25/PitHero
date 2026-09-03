using Nez;
using PitHero.AI;
using PitHero.ECS.Components;
using PitHero.UI;
using RolePlayingFramework.Skills;

namespace PitHero.Services.Replay
{
    /// <summary>
    /// Applies <see cref="PlayerCommand"/>s to the simulation. One static method per command type,
    /// dispatched by a switch (no delegates, no reflection). Every handler re-validates its inputs
    /// against live state and no-ops when they no longer hold, so live play and replay take the same
    /// branch whether or not the originating window is still open.
    /// </summary>
    public static partial class PlayerCommandHandlers
    {
        /// <summary>Global service container, or null when no Nez core exists (headless tests).</summary>
        private static Microsoft.Xna.Framework.GameServiceContainer Services => Core.Instance != null ? Core.Services : null;

        /// <summary>Current scene, or null when no Nez core exists.</summary>
        private static Scene CurrentScene => Core.Instance != null ? Core.Scene : null;

        /// <summary>Applies one command.</summary>
        public static void Apply(in PlayerCommand cmd)
        {
            switch (cmd.Type)
            {
                case PlayerCommandType.SetManualPause:
                    Services?.GetService<PauseService>()?.ApplyManualPause(cmd.ABool);
                    break;
                case PlayerCommandType.SetFarmModePause:
                    Services?.GetService<PauseService>()?.ApplyFarmModePause(cmd.ABool);
                    break;

                case PlayerCommandType.SetStoppedAdventure:
                    GetSettingsUI()?.StopAdventuringUI?.SetStopped(cmd.ABool);
                    break;
                case PlayerCommandType.Replenish:
                    ReplenishUI.ApplyReplenish();
                    break;
                case PlayerCommandType.SetPitPriorities:
                    ApplyPitPriorities(in cmd);
                    break;
                case PlayerCommandType.SetHealPriorities:
                    ApplyHealPriorities(in cmd);
                    break;
                case PlayerCommandType.SetBattleTactic:
                {
                    var hero = GetHeroComponent();
                    if (hero != null)
                        hero.CurrentBattleTactic = (BattleTactic)cmd.A;
                    break;
                }
                case PlayerCommandType.SetUseConsumablesOnMercs:
                {
                    var hero = GetHeroComponent();
                    if (hero != null)
                        hero.UseConsumablesOnMercenaries = cmd.ABool;
                    break;
                }
                case PlayerCommandType.SetMercsCanUseConsumables:
                {
                    var hero = GetHeroComponent();
                    if (hero != null)
                        hero.MercenariesCanUseConsumables = cmd.ABool;
                    break;
                }
                case PlayerCommandType.SetAutoEquipHero:
                {
                    var hero = GetHeroComponent();
                    if (hero != null)
                        hero.AutoEquipHero = cmd.ABool;
                    break;
                }
                case PlayerCommandType.SetAutoEquipMercs:
                {
                    var hero = GetHeroComponent();
                    if (hero != null)
                        hero.AutoEquipMercenaries = cmd.ABool;
                    break;
                }
                case PlayerCommandType.RequestManualJobChange:
                    if (CurrentScene != null)
                        JobChangeFlow.BeginManualJobChange();
                    break;
                case PlayerCommandType.PurchaseSkill:
                    ApplyPurchaseSkill(in cmd);
                    break;

                case PlayerCommandType.UseShortcut:
                    GetShortcutBar()?.ApplyUseShortcut(cmd.A);
                    break;
                case PlayerCommandType.UseBagConsumable:
                    GetHeroInventoryGrid()?.ApplyUseConsumable(cmd.A);
                    break;

                case PlayerCommandType.DebugQueuePitLevel:
#if DEBUG
                    if (CurrentScene != null)
                        PitLevelTestComponent.ApplyPitLevel(cmd.A);
#endif
                    break;

                default:
                    if (!ApplyExtended(in cmd))
                        Debug.Warn($"[PlayerCommandHandlers] No handler for {cmd.Type}");
                    break;
            }
        }

        // ── Shared lookups ───────────────────────────────────────────────────────────

        /// <summary>The hero component of the current scene, or null.</summary>
        public static HeroComponent GetHeroComponent()
        {
            return CurrentScene?.FindEntity("hero")?.GetComponent<HeroComponent>();
        }

        /// <summary>The settings UI service (owns the top-bar controls and HeroUI), or null.</summary>
        public static SettingsUI GetSettingsUI()
        {
            return Services?.GetService<SettingsUI>();
        }

        /// <summary>The shortcut bar, or null.</summary>
        public static ShortcutBar GetShortcutBar()
        {
            return Services?.GetService<ShortcutBarService>()?.ShortcutBar;
        }

        /// <summary>The Party window's inventory grid (bound to the hero bag), or null.</summary>
        public static InventoryGrid GetHeroInventoryGrid()
        {
            return GetSettingsUI()?.HeroUI?.GetInventoryGrid();
        }

        // ── Hero priorities ──────────────────────────────────────────────────────────

        private static void ApplyPitPriorities(in PlayerCommand cmd)
        {
            var hero = GetHeroComponent();
            if (hero == null)
                return;
            var priorities = new HeroPitPriority[3];
            priorities[0] = (HeroPitPriority)cmd.A;
            priorities[1] = (HeroPitPriority)cmd.B;
            priorities[2] = (HeroPitPriority)cmd.C;
            hero.SetPrioritiesInOrder(priorities);
        }

        private static void ApplyHealPriorities(in PlayerCommand cmd)
        {
            var heroEntity = CurrentScene?.FindEntity("hero");
            var hero = heroEntity?.GetComponent<HeroComponent>();
            if (hero == null)
                return;
            var priorities = new HeroHealPriority[3];
            priorities[0] = (HeroHealPriority)cmd.A;
            priorities[1] = (HeroHealPriority)cmd.B;
            priorities[2] = (HeroHealPriority)cmd.C;
            hero.SetHealPrioritiesInOrder(priorities);
            heroEntity.GetComponent<HeroStateMachine>()?.UpdateHealingActionCosts();
        }

        private static void ApplyPurchaseSkill(in PlayerCommand cmd)
        {
            var hero = GetHeroComponent()?.LinkedHero;
            if (hero == null || string.IsNullOrEmpty(cmd.S))
                return;
            ISkill skill = null;
            var skills = hero.Job.Skills;
            for (int i = 0; i < skills.Count; i++)
            {
                if (skills[i].Name == cmd.S)
                {
                    skill = skills[i];
                    break;
                }
            }
            if (skill == null)
            {
                Debug.Log($"[PlayerCommandHandlers] PurchaseSkill: {cmd.S} is not a skill of the hero's current job");
                return;
            }
            if (hero.TryPurchaseSkill(skill))
                GetSettingsUI()?.HeroUI?.RefreshAfterSkillPurchase();
        }
    }
}
