using Microsoft.Xna.Framework;
using Nez;
using PitHero.AI.Interfaces;
using PitHero.ECS.Components;
using PitHero.UI;
using RolePlayingFramework.Skills;
using System.Collections.Generic;

namespace PitHero.AI
{
    /// <summary>
    /// Action that uses a healing skill from the shortcut bar to restore HP
    /// Chooses the most efficient healing skill (least waste) for the hero's current HP
    /// </summary>
    public class UseHealingSkillAction : HeroActionBase
    {
        public UseHealingSkillAction() : base(GoapConstants.UseHealingSkillAction, 20)
        {
            // Preconditions: HP is critical and we haven't exhausted healing skills
            SetPrecondition(GoapConstants.HPCritical, true);
            SetPrecondition(GoapConstants.HealingSkillExhausted, false);

            // Postcondition: HP is no longer critical
            SetPostcondition(GoapConstants.HPCritical, false);
        }

        /// <summary>
        /// Execute the healing skill action
        /// </summary>
        public override bool Execute(HeroComponent hero)
        {
            // Check if hero actually has critical HP
            if (hero.LinkedHero == null)
            {
                Debug.Log("[UseHealingSkillAction] No linked hero found");
                hero.HealingSkillExhausted = true;
                return true; // Action complete (failed, but complete)
            }

            float hpPercent = (float)hero.LinkedHero.CurrentHP / hero.LinkedHero.MaxHP;
            if (hpPercent >= GameConfig.HeroCriticalHPPercent)
            {
                Debug.Log("[UseHealingSkillAction] Hero HP not critical");
                return true; // HP is fine, action complete
            }

            // Get the shortcut bar
            var shortcutBar = GetShortcutBar();
            if (shortcutBar == null)
            {
                Debug.Warn("[UseHealingSkillAction] Could not find shortcut bar");
                hero.HealingSkillExhausted = true;
                return true;
            }

            // Find the most efficient healing skill on the shortcut bar
            var healingSkill = FindMostEfficientHealingSkill(shortcutBar, hero.LinkedHero);
            if (healingSkill == null)
            {
                Debug.Log("[UseHealingSkillAction] No healing skills available on shortcut bar");
                hero.HealingSkillExhausted = true;
                return true;
            }

            // Check if hero has enough MP to use the skill
            if (hero.LinkedHero.CurrentMP < healingSkill.MPCost)
            {
                Debug.Log($"[UseHealingSkillAction] Not enough MP to use {healingSkill.Name}. Need {healingSkill.MPCost}, have {hero.LinkedHero.CurrentMP}");
                hero.HealingSkillExhausted = true;
                return true;
            }

            // Use the healing skill on the hero
            bool success = UseHealingSkillOnTarget(healingSkill, hero.LinkedHero);
            if (!success)
            {
                Debug.Warn("[UseHealingSkillAction] Failed to use healing skill");
                hero.HealingSkillExhausted = true;
                return true;
            }

            Debug.Log($"[UseHealingSkillAction] Successfully used {healingSkill.Name} on {hero.LinkedHero.Name}");
            return true; // Action complete
        }

        /// <summary>
        /// Execute action using interface-based context (for virtual game)
        /// </summary>
        public override bool Execute(IGoapContext context)
        {
            context.LogDebug("[UseHealingSkillAction] Healing skill action executed (interface-based context)");
            return true;
        }

        /// <summary>
        /// Get the shortcut bar from the scene
        /// </summary>
        private ShortcutBar GetShortcutBar()
        {
            var scene = Core.Scene;
            if (scene == null) return null;

            var hudEntity = scene.FindEntity("hud");
            return hudEntity?.GetComponent<ShortcutBar>();
        }

        /// <summary>
        /// Find the most efficient healing skill on the shortcut bar for the target
        /// Returns the healing skill with the least waste (smallest amount over what's needed)
        /// Also prefers skills with lower MP cost when healing amounts are equal
        /// </summary>
        private ISkill FindMostEfficientHealingSkill(ShortcutBar shortcutBar, RolePlayingFramework.Heroes.Hero target)
        {
            int hpNeeded = target.MaxHP - target.CurrentHP;
            ISkill bestSkill = null;
            int bestWaste = int.MaxValue;
            int bestMPCost = int.MaxValue;

            for (int i = 0; i < 8; i++) // 8 shortcut slots
            {
                var slotData = shortcutBar.GetShortcutSlotData(i);
                if (slotData == null || slotData.SlotType != ShortcutSlotType.Skill) continue;

                var skill = slotData.ReferencedSkill;
                if (skill == null || skill.HPRestoreAmount <= 0) continue;

                // Don't use battle-only skills outside of battle
                if (skill.BattleOnly) continue;

                // Skip if not enough MP
                if (target.CurrentMP < skill.MPCost) continue;

                // Calculate waste (how much healing would be wasted)
                int waste = skill.HPRestoreAmount - hpNeeded;
                
                // If this skill can heal the target and has less waste than our current best, use it
                if (skill.HPRestoreAmount >= hpNeeded && waste < bestWaste)
                {
                    bestSkill = skill;
                    bestWaste = waste;
                    bestMPCost = skill.MPCost;
                }
                // If waste is equal, prefer the skill with lower MP cost
                else if (skill.HPRestoreAmount >= hpNeeded && waste == bestWaste && skill.MPCost < bestMPCost)
                {
                    bestSkill = skill;
                    bestMPCost = skill.MPCost;
                }
                else if (bestSkill == null && skill.HPRestoreAmount > 0)
                {
                    // If we haven't found a perfect fit yet, keep track of any healing skill
                    // (even if it won't fully heal, it's better than nothing)
                    bestSkill = skill;
                    bestWaste = waste;
                    bestMPCost = skill.MPCost;
                }
            }

            return bestSkill;
        }

        /// <summary>
        /// Use the healing skill on the target
        /// </summary>
        private bool UseHealingSkillOnTarget(ISkill skill, RolePlayingFramework.Heroes.Hero target)
        {
            if (skill == null || target == null) return false;

            // Check MP cost again
            if (target.CurrentMP < skill.MPCost)
            {
                return false;
            }

            // Consume MP
            bool mpSpent = target.SpendMP(skill.MPCost);
            if (!mpSpent)
            {
                return false;
            }

            // Restore HP
            int healAmount = skill.HPRestoreAmount;
            bool healed = target.RestoreHP(healAmount);

            if (healed)
            {
                Debug.Log($"[UseHealingSkillAction] Healed {target.Name} for {healAmount} HP using {skill.Name}. Current HP: {target.CurrentHP}/{target.MaxHP}, MP: {target.CurrentMP}/{target.MaxMP}");
                return true;
            }

            return false;
        }
    }
}
