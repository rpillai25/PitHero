using Microsoft.Xna.Framework;
using Nez;
using PitHero.AI.Interfaces;
using PitHero.ECS.Components;
using PitHero.Services;
using PitHero.UI;
using RolePlayingFramework.Skills;
using System;
using System.Collections.Generic;

namespace PitHero.AI
{
    /// <summary>
    /// Action that uses a healing skill from the shortcut bar to restore HP
    /// Chooses the most efficient healing skill (least waste) for the target with critical HP (hero or mercenary)
    /// </summary>
    public class UseHealingSkillAction : HeroActionBase
    {
        public UseHealingSkillAction() : base(GoapConstants.UseHealingSkillAction, 20)
        {
            // Preconditions: HP is critical and we haven't exhausted healing skills
            SetPrecondition(GoapConstants.HPCritical, true);

            // Postcondition: HP is no longer critical
            SetPostcondition(GoapConstants.HPCritical, false);
        }

        public override bool Validate()
        {
            var heroComponent = Game1.Scene.FindEntity("hero")?.GetComponent<HeroComponent>();
            var healPrioritiesInOrder = heroComponent?.GetHealPrioritiesInOrder();
            if (!heroComponent.HPCritical)
            {
                return false;
            }
            if (healPrioritiesInOrder != null)
            {
                //If healing item priority is lower then healing skill and inn, and healing skill is not exhausted and inn is not exhausted, then we cannot use healing item
                if (Array.IndexOf(healPrioritiesInOrder, HeroHealPriority.HealingSkill) >
                    Array.IndexOf(healPrioritiesInOrder, HeroHealPriority.HealingItem) &&
                    !heroComponent.HealingItemExhausted ||
                    Array.IndexOf(healPrioritiesInOrder, HeroHealPriority.HealingSkill) >
                    Array.IndexOf(healPrioritiesInOrder, HeroHealPriority.Inn) &&
                    !heroComponent.InnExhausted)
                {
                    return false;
                }
            }

            return !heroComponent.HealingSkillExhausted;
        }

        /// <summary>
        /// Execute the healing skill action
        /// </summary>
        public override bool Execute(HeroComponent hero)
        {
            // Find the target (hero or mercenary) with critical HP
            var target = FindCriticalHPTarget(hero, out bool isHero, out Entity targetEntity);
            if (target == null)
            {
                Debug.Log("[UseHealingSkillAction] No target with critical HP found");
                hero.HealingSkillExhausted = true;
                return true; // Action complete (failed, but complete)
            }

            // Get target stats
            int currentHP = isHero ? ((RolePlayingFramework.Heroes.Hero)target).CurrentHP : ((RolePlayingFramework.Mercenaries.Mercenary)target).CurrentHP;
            int maxHP = isHero ? ((RolePlayingFramework.Heroes.Hero)target).MaxHP : ((RolePlayingFramework.Mercenaries.Mercenary)target).MaxHP;
            int currentMP = isHero ? ((RolePlayingFramework.Heroes.Hero)target).CurrentMP : ((RolePlayingFramework.Mercenaries.Mercenary)target).CurrentMP;
            string targetName = isHero ? ((RolePlayingFramework.Heroes.Hero)target).Name : ((RolePlayingFramework.Mercenaries.Mercenary)target).Name;

            ISkill healingSkill = null;

            // Check if we should use only action bar skills or all learned skills
            if (hero.OnlyUseActionBarItemsAndSkills)
            {
                // Original behavior: use only shortcut bar skills
                var shortcutBar = GetShortcutBar();
                if (shortcutBar == null)
                {
                    Debug.Warn("[UseHealingSkillAction] Could not find shortcut bar");
                    hero.HealingSkillExhausted = true;
                    return true;
                }

                healingSkill = FindMostEfficientHealingSkill(shortcutBar, currentHP, maxHP, currentMP);
                if (healingSkill == null)
                {
                    Debug.Log("[UseHealingSkillAction] No healing skills available on shortcut bar");
                    hero.HealingSkillExhausted = true;
                    return true;
                }
            }
            else
            {
                // New behavior: use all learned skills from hero crystal
                healingSkill = FindMostEfficientHealingSkillFromCrystal(hero.LinkedHero, currentHP, maxHP, currentMP);
                if (healingSkill == null)
                {
                    Debug.Log("[UseHealingSkillAction] No healing skills available in hero crystal");
                    hero.HealingSkillExhausted = true;
                    return true;
                }
            }

            // Check if target has enough MP to use the skill
            if (currentMP < healingSkill.MPCost)
            {
                Debug.Log($"[UseHealingSkillAction] Not enough MP to use {healingSkill.Name}. Need {healingSkill.MPCost}, have {currentMP}");
                hero.HealingSkillExhausted = true;
                return true;
            }

            // Use the healing skill on the target
            bool success = UseHealingSkillOnTarget(healingSkill, target, isHero);
            if (!success)
            {
                Debug.Warn("[UseHealingSkillAction] Failed to use healing skill");
                hero.HealingSkillExhausted = true;
                return true;
            }

            Debug.Log($"[UseHealingSkillAction] Successfully used {healingSkill.Name} on {targetName}");
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
        /// Find the target (hero or mercenary) with critical HP
        /// Returns the target object, whether it's a hero, and the entity reference
        /// </summary>
        private object FindCriticalHPTarget(HeroComponent heroComponent, out bool isHero, out Entity targetEntity)
        {
            isHero = true;
            targetEntity = null;

            // Check hero first
            if (heroComponent.LinkedHero != null)
            {
                float hpPercent = (float)heroComponent.LinkedHero.CurrentHP / heroComponent.LinkedHero.MaxHP;
                if (hpPercent < GameConfig.HeroCriticalHPPercent)
                {
                    targetEntity = heroComponent.Entity;
                    return heroComponent.LinkedHero;
                }
            }

            // Check mercenaries
            var mercenaryManager = Core.Services.GetService<MercenaryManager>();
            if (mercenaryManager != null)
            {
                var hiredMercenaries = mercenaryManager.GetHiredMercenaries();
                for (int i = 0; i < hiredMercenaries.Count; i++)
                {
                    var merc = hiredMercenaries[i];
                    var mercComp = merc.GetComponent<MercenaryComponent>();
                    if (mercComp?.LinkedMercenary != null)
                    {
                        float mercHpPercent = (float)mercComp.LinkedMercenary.CurrentHP / mercComp.LinkedMercenary.MaxHP;
                        if (mercHpPercent < GameConfig.HeroCriticalHPPercent)
                        {
                            isHero = false;
                            targetEntity = merc;
                            return mercComp.LinkedMercenary;
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Get the shortcut bar from the service
        /// </summary>
        private ShortcutBar GetShortcutBar()
        {
            var service = Core.Services.GetService<ShortcutBarService>();
            return service?.ShortcutBar;
        }

        /// <summary>
        /// Find the most efficient healing skill on the shortcut bar for the target
        /// Returns the healing skill with the least waste (smallest amount over what's needed)
        /// Also prefers skills with lower MP cost when healing amounts are equal
        /// </summary>
        private ISkill FindMostEfficientHealingSkill(ShortcutBar shortcutBar, int currentHP, int maxHP, int currentMP)
        {
            int hpNeeded = maxHP - currentHP;
            ISkill bestSkill = null;
            int bestWaste = int.MaxValue;
            int bestMPCost = int.MaxValue;

            Debug.Log($"[UseHealingSkillAction] Searching for healing skill. HP needed: {hpNeeded}, MP available: {currentMP}");

            for (int i = 0; i < 8; i++) // 8 shortcut slots
            {
                var slotData = shortcutBar.GetShortcutSlotData(i);
                if (slotData == null || slotData.SlotType != ShortcutSlotType.Skill) continue;

                var skill = slotData.ReferencedSkill;
                if (skill == null || skill.HPRestoreAmount <= 0) continue;

                // Don't use battle-only skills outside of battle
                if (skill.BattleOnly) continue;

                // Skip if not enough MP
                if (currentMP < skill.MPCost) 
                {
                    Debug.Log($"[UseHealingSkillAction] Slot {i + 1}: {skill.Name} (not enough MP: need {skill.MPCost}, have {currentMP})");
                    continue;
                }

                Debug.Log($"[UseHealingSkillAction] Slot {i + 1}: {skill.Name} (heals {skill.HPRestoreAmount} HP, costs {skill.MPCost} MP)");

                // Calculate waste (how much healing would be wasted)
                int waste = skill.HPRestoreAmount - hpNeeded;
                
                // If this skill can heal the target and has less waste than our current best, use it
                if (skill.HPRestoreAmount >= hpNeeded && waste < bestWaste)
                {
                    bestSkill = skill;
                    bestWaste = waste;
                    bestMPCost = skill.MPCost;
                    Debug.Log($"[UseHealingSkillAction] New best skill: {skill.Name} (waste: {waste}, MP cost: {skill.MPCost})");
                }
                // If waste is equal, prefer the skill with lower MP cost
                else if (skill.HPRestoreAmount >= hpNeeded && waste == bestWaste && skill.MPCost < bestMPCost)
                {
                    bestSkill = skill;
                    bestMPCost = skill.MPCost;
                    Debug.Log($"[UseHealingSkillAction] Better MP efficiency: {skill.Name} (MP cost: {skill.MPCost})");
                }
                else if (bestSkill == null && skill.HPRestoreAmount > 0)
                {
                    // If we haven't found a perfect fit yet, keep track of any healing skill
                    // (even if it won't fully heal, it's better than nothing)
                    bestSkill = skill;
                    bestWaste = waste;
                    bestMPCost = skill.MPCost;
                    Debug.Log($"[UseHealingSkillAction] Fallback skill: {skill.Name} (partial heal)");
                }
            }

            if (bestSkill != null)
            {
                Debug.Log($"[UseHealingSkillAction] Selected healing skill: {bestSkill.Name}");
            }
            else
            {
                Debug.Log($"[UseHealingSkillAction] No healing skills found on shortcut bar");
            }

            return bestSkill;
        }

        /// <summary>
        /// Find the most efficient healing skill from all learned skills in the hero crystal
        /// Returns the healing skill with the least waste (smallest amount over what's needed)
        /// Also prefers skills with lower MP cost when healing amounts are equal
        /// </summary>
        private ISkill FindMostEfficientHealingSkillFromCrystal(RolePlayingFramework.Heroes.Hero hero, int currentHP, int maxHP, int currentMP)
        {
            if (hero == null)
            {
                Debug.Log("[UseHealingSkillAction] Hero is null, cannot find healing skills");
                return null;
            }

            int hpNeeded = maxHP - currentHP;
            ISkill bestSkill = null;
            int bestWaste = int.MaxValue;
            int bestMPCost = int.MaxValue;

            Debug.Log($"[UseHealingSkillAction] Searching for healing skill in hero crystal. HP needed: {hpNeeded}, MP available: {currentMP}");

            // Search through all learned skills (both regular and synergy skills)
            var learnedSkills = hero.LearnedSkills;
            if (learnedSkills != null)
            {
                foreach (var skillEntry in learnedSkills)
                {
                    var skill = skillEntry.Value;
                    if (skill == null || skill.HPRestoreAmount <= 0) continue;

                    // Don't use battle-only skills outside of battle
                    if (skill.BattleOnly) continue;

                    // Skip if not enough MP
                    if (currentMP < skill.MPCost) 
                    {
                        Debug.Log($"[UseHealingSkillAction] {skill.Name} (not enough MP: need {skill.MPCost}, have {currentMP})");
                        continue;
                    }

                    Debug.Log($"[UseHealingSkillAction] Learned skill: {skill.Name} (heals {skill.HPRestoreAmount} HP, costs {skill.MPCost} MP)");

                    // Calculate waste
                    int waste = skill.HPRestoreAmount - hpNeeded;
                    
                    // If this skill can heal the target and has less waste than our current best, use it
                    if (skill.HPRestoreAmount >= hpNeeded && waste < bestWaste)
                    {
                        bestSkill = skill;
                        bestWaste = waste;
                        bestMPCost = skill.MPCost;
                        Debug.Log($"[UseHealingSkillAction] New best skill: {skill.Name} (waste: {waste}, MP cost: {skill.MPCost})");
                    }
                    // If waste is equal, prefer the skill with lower MP cost
                    else if (skill.HPRestoreAmount >= hpNeeded && waste == bestWaste && skill.MPCost < bestMPCost)
                    {
                        bestSkill = skill;
                        bestMPCost = skill.MPCost;
                        Debug.Log($"[UseHealingSkillAction] Better MP efficiency: {skill.Name} (MP cost: {skill.MPCost})");
                    }
                    else if (bestSkill == null && skill.HPRestoreAmount > 0)
                    {
                        // If we haven't found a perfect fit yet, keep track of any healing skill
                        bestSkill = skill;
                        bestWaste = waste;
                        bestMPCost = skill.MPCost;
                        Debug.Log($"[UseHealingSkillAction] Fallback skill: {skill.Name} (partial heal)");
                    }
                }
            }

            // Also check synergy skills from active synergies
            var activeSynergies = hero.ActiveSynergies;
            if (activeSynergies != null)
            {
                for (int i = 0; i < activeSynergies.Count; i++)
                {
                    var synergy = activeSynergies[i];
                    if (synergy?.Pattern?.UnlockedSkill == null) continue;
                    if (!synergy.IsSkillUnlocked) continue; // Skip if not unlocked yet

                    var skill = synergy.Pattern.UnlockedSkill;
                    if (skill.HPRestoreAmount <= 0) continue;

                    // Don't use battle-only skills outside of battle
                    if (skill.BattleOnly) continue;

                    // Skip if not enough MP
                    if (currentMP < skill.MPCost) 
                    {
                        Debug.Log($"[UseHealingSkillAction] Synergy skill: {skill.Name} (not enough MP: need {skill.MPCost}, have {currentMP})");
                        continue;
                    }

                    Debug.Log($"[UseHealingSkillAction] Synergy skill: {skill.Name} (heals {skill.HPRestoreAmount} HP, costs {skill.MPCost} MP)");

                    // Calculate waste
                    int waste = skill.HPRestoreAmount - hpNeeded;
                    
                    // If this skill can heal the target and has less waste than our current best, use it
                    if (skill.HPRestoreAmount >= hpNeeded && waste < bestWaste)
                    {
                        bestSkill = skill;
                        bestWaste = waste;
                        bestMPCost = skill.MPCost;
                        Debug.Log($"[UseHealingSkillAction] New best synergy skill: {skill.Name} (waste: {waste}, MP cost: {skill.MPCost})");
                    }
                    // If waste is equal, prefer the skill with lower MP cost
                    else if (skill.HPRestoreAmount >= hpNeeded && waste == bestWaste && skill.MPCost < bestMPCost)
                    {
                        bestSkill = skill;
                        bestMPCost = skill.MPCost;
                        Debug.Log($"[UseHealingSkillAction] Better MP efficiency: {skill.Name} (MP cost: {skill.MPCost})");
                    }
                    else if (bestSkill == null && skill.HPRestoreAmount > 0)
                    {
                        // If we haven't found a perfect fit yet, keep track of any healing skill
                        bestSkill = skill;
                        bestWaste = waste;
                        bestMPCost = skill.MPCost;
                        Debug.Log($"[UseHealingSkillAction] Fallback synergy skill: {skill.Name} (partial heal)");
                    }
                }
            }

            if (bestSkill != null)
            {
                Debug.Log($"[UseHealingSkillAction] Selected healing skill from hero crystal: {bestSkill.Name}");
            }
            else
            {
                Debug.Log($"[UseHealingSkillAction] No healing skills found in hero crystal");
            }

            return bestSkill;
        }

        /// <summary>
        /// Use the healing skill on the target (hero or mercenary)
        /// </summary>
        private bool UseHealingSkillOnTarget(ISkill skill, object target, bool isHero)
        {
            if (skill == null || target == null) return false;

            int currentMP = 0;
            int maxMP = 0;
            string targetName = "";
            bool mpSpent = false;

            // Check MP cost and consume MP
            if (isHero)
            {
                var hero = (RolePlayingFramework.Heroes.Hero)target;
                currentMP = hero.CurrentMP;
                maxMP = hero.MaxMP;
                targetName = hero.Name;

                if (currentMP < skill.MPCost)
                {
                    return false;
                }

                mpSpent = hero.SpendMP(skill.MPCost);
            }
            else
            {
                var mercenary = (RolePlayingFramework.Mercenaries.Mercenary)target;
                currentMP = mercenary.CurrentMP;
                maxMP = mercenary.MaxMP;
                targetName = mercenary.Name;

                if (currentMP < skill.MPCost)
                {
                    return false;
                }

                mpSpent = mercenary.UseMP(skill.MPCost);
            }

            if (!mpSpent)
            {
                return false;
            }

            // Restore HP
            int healAmount = skill.HPRestoreAmount;
            bool healed = false;
            int currentHP = 0;
            int maxHP = 0;

            if (isHero)
            {
                var hero = (RolePlayingFramework.Heroes.Hero)target;
                healed = hero.RestoreHP(healAmount);
                currentHP = hero.CurrentHP;
                maxHP = hero.MaxHP;
                currentMP = hero.CurrentMP;
            }
            else
            {
                var mercenary = (RolePlayingFramework.Mercenaries.Mercenary)target;
                healed = mercenary.RestoreHP(healAmount);
                currentHP = mercenary.CurrentHP;
                maxHP = mercenary.MaxHP;
                currentMP = mercenary.CurrentMP;
            }

            if (healed)
            {
                Debug.Log($"[UseHealingSkillAction] Healed {targetName} for {healAmount} HP using {skill.Name}. Current HP: {currentHP}/{maxHP}, MP: {currentMP}/{maxMP}");
                return true;
            }

            return false;
        }
    }
}
