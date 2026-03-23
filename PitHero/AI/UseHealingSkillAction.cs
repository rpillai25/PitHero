using Microsoft.Xna.Framework;
using Nez;
using PitHero.AI.Interfaces;
using PitHero.ECS.Components;
using PitHero.Services;
using RolePlayingFramework.Skills;
using System;
using System.Collections.Generic;

namespace PitHero.AI
{
    /// <summary>
    /// Action that uses a healing skill from hero crystal or mercenary learned skills to restore HP
    /// Searches hero skills, synergy skills, and all hired mercenaries' skills for the most efficient heal
    /// The caster (hero or mercenary) spends their own MP; mercenaries do not use healing items
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
            var target = FindCriticalHPTarget(hero, out bool isTargetHero, out Entity targetEntity);
            if (target == null)
            {
                // No target with critical HP — action completed without doing anything.
                // HPCritical world state will be rechecked next GOAP cycle.
                Debug.Log("[UseHealingSkillAction] No target with critical HP found");
                return true;
            }

            // Get target stats
            int currentHP = isTargetHero ? ((RolePlayingFramework.Heroes.Hero)target).CurrentHP : ((RolePlayingFramework.Mercenaries.Mercenary)target).CurrentHP;
            int maxHP = isTargetHero ? ((RolePlayingFramework.Heroes.Hero)target).MaxHP : ((RolePlayingFramework.Mercenaries.Mercenary)target).MaxHP;
            string targetName = isTargetHero ? ((RolePlayingFramework.Heroes.Hero)target).Name : ((RolePlayingFramework.Mercenaries.Mercenary)target).Name;

            // Find the best healer (hero or any hired mercenary) with a healing skill
            FindBestHealerAndSkill(hero, currentHP, maxHP, out object caster, out ISkill healingSkill, out bool isCasterHero);
            if (healingSkill == null)
            {
                Debug.Log("[UseHealingSkillAction] No healing skills available from hero or mercenaries");
                hero.HealingSkillExhausted = true;
                return true;
            }

            // Use the healing skill (caster spends MP, target gets healed)
            bool success = UseHealingSkillOnTarget(healingSkill, caster, isCasterHero, target, isTargetHero);
            if (!success)
            {
                Debug.Warn("[UseHealingSkillAction] Failed to use healing skill");
                hero.HealingSkillExhausted = true;
                return true;
            }

            string casterName = isCasterHero ? hero.LinkedHero.Name : ((RolePlayingFramework.Mercenaries.Mercenary)caster).Name;
            Debug.Log($"[UseHealingSkillAction] {casterName} used {healingSkill.Name} on {targetName}");
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
        /// Find the best healer (hero or any hired mercenary) and their most efficient healing skill.
        /// Searches hero learned skills, synergy skills, and all hired mercenaries' learned skills.
        /// </summary>
        private void FindBestHealerAndSkill(HeroComponent heroComponent, int targetCurrentHP, int targetMaxHP,
            out object bestCaster, out ISkill bestSkill, out bool bestIsCasterHero)
        {
            int hpNeeded = targetMaxHP - targetCurrentHP;
            bestCaster = null;
            bestSkill = null;
            bestIsCasterHero = false;
            int bestWaste = int.MaxValue;
            int bestMPCost = int.MaxValue;

            Debug.Log($"[UseHealingSkillAction] Searching all healers for best skill. HP needed: {hpNeeded}");

            // Search hero's learned skills and synergy skills (using hero's MP)
            if (heroComponent.LinkedHero != null)
            {
                int heroMP = heroComponent.LinkedHero.CurrentMP;

                // Hero learned skills
                var learnedSkills = heroComponent.LinkedHero.LearnedSkills;
                if (learnedSkills != null)
                {
                    foreach (var skillEntry in learnedSkills)
                    {
                        EvaluateSkill(skillEntry.Value, hpNeeded, heroMP, heroComponent.LinkedHero, true,
                            ref bestCaster, ref bestSkill, ref bestIsCasterHero, ref bestWaste, ref bestMPCost);
                    }
                }

                // Hero synergy skills
                var activeSynergies = heroComponent.LinkedHero.ActiveSynergies;
                if (activeSynergies != null)
                {
                    for (int i = 0; i < activeSynergies.Count; i++)
                    {
                        var synergy = activeSynergies[i];
                        if (synergy?.Pattern?.UnlockedSkill == null) continue;
                        if (!synergy.IsSkillUnlocked) continue;

                        EvaluateSkill(synergy.Pattern.UnlockedSkill, hpNeeded, heroMP, heroComponent.LinkedHero, true,
                            ref bestCaster, ref bestSkill, ref bestIsCasterHero, ref bestWaste, ref bestMPCost);
                    }
                }
            }

            // Search all hired mercenaries' learned skills (each uses their own MP)
            var mercenaryManager = Core.Services.GetService<MercenaryManager>();
            if (mercenaryManager != null)
            {
                var hiredMercenaries = mercenaryManager.GetHiredMercenaries();
                for (int i = 0; i < hiredMercenaries.Count; i++)
                {
                    var merc = hiredMercenaries[i];
                    var mercComp = merc.GetComponent<MercenaryComponent>();
                    if (mercComp?.LinkedMercenary == null) continue;

                    var mercenary = mercComp.LinkedMercenary;
                    int mercMP = mercenary.CurrentMP;
                    var mercSkills = mercenary.LearnedSkills;
                    if (mercSkills == null) continue;

                    foreach (var skillEntry in mercSkills)
                    {
                        EvaluateSkill(skillEntry.Value, hpNeeded, mercMP, mercenary, false,
                            ref bestCaster, ref bestSkill, ref bestIsCasterHero, ref bestWaste, ref bestMPCost);
                    }
                }
            }

            if (bestSkill != null)
            {
                string casterName = bestIsCasterHero
                    ? heroComponent.LinkedHero.Name
                    : ((RolePlayingFramework.Mercenaries.Mercenary)bestCaster).Name;
                Debug.Log($"[UseHealingSkillAction] Best healer: {casterName} with {bestSkill.Name} (waste: {bestWaste}, MP cost: {bestMPCost})");
            }
            else
            {
                Debug.Log("[UseHealingSkillAction] No healing skills found from any healer");
            }
        }

        /// <summary>
        /// Evaluate a single skill against the current best and update tracking if this skill is better.
        /// Prefers least waste (smallest overheal), then lowest MP cost as tiebreaker.
        /// </summary>
        private void EvaluateSkill(ISkill skill, int hpNeeded, int casterCurrentMP, object caster, bool isCasterHero,
            ref object bestCaster, ref ISkill bestSkill, ref bool bestIsCasterHero, ref int bestWaste, ref int bestMPCost)
        {
            if (skill == null || skill.HPRestoreAmount <= 0) return;

            // Don't use battle-only skills outside of battle
            if (skill.BattleOnly) return;

            // Skip if caster doesn't have enough MP
            if (casterCurrentMP < skill.MPCost)
            {
                string casterName = isCasterHero
                    ? ((RolePlayingFramework.Heroes.Hero)caster).Name
                    : ((RolePlayingFramework.Mercenaries.Mercenary)caster).Name;
                Debug.Log($"[UseHealingSkillAction] {casterName}: {skill.Name} (not enough MP: need {skill.MPCost}, have {casterCurrentMP})");
                return;
            }

            int waste = skill.HPRestoreAmount - hpNeeded;

            // If this skill can fully heal and has less waste than current best, use it
            if (skill.HPRestoreAmount >= hpNeeded && waste < bestWaste)
            {
                bestCaster = caster;
                bestSkill = skill;
                bestIsCasterHero = isCasterHero;
                bestWaste = waste;
                bestMPCost = skill.MPCost;
            }
            // If waste is equal, prefer the skill with lower MP cost
            else if (skill.HPRestoreAmount >= hpNeeded && waste == bestWaste && skill.MPCost < bestMPCost)
            {
                bestCaster = caster;
                bestSkill = skill;
                bestIsCasterHero = isCasterHero;
                bestMPCost = skill.MPCost;
            }
            // Partial heal fallback: pick the skill that heals the most (least negative waste)
            else if (skill.HPRestoreAmount < hpNeeded &&
                     (bestSkill == null || (bestWaste < 0 && waste > bestWaste) || (bestWaste < 0 && waste == bestWaste && skill.MPCost < bestMPCost)))
            {
                bestCaster = caster;
                bestSkill = skill;
                bestIsCasterHero = isCasterHero;
                bestWaste = waste;
                bestMPCost = skill.MPCost;
            }
        }

        /// <summary>
        /// Use the healing skill: caster spends their own MP, target gets healed
        /// </summary>
        private bool UseHealingSkillOnTarget(ISkill skill, object caster, bool isCasterHero, object target, bool isTargetHero)
        {
            if (skill == null || caster == null || target == null) return false;

            // Spend caster's MP
            string casterName;
            bool mpSpent;

            if (isCasterHero)
            {
                var hero = (RolePlayingFramework.Heroes.Hero)caster;
                casterName = hero.Name;

                if (hero.CurrentMP < skill.MPCost)
                {
                    return false;
                }

                mpSpent = hero.SpendMP(skill.MPCost);
            }
            else
            {
                var mercenary = (RolePlayingFramework.Mercenaries.Mercenary)caster;
                casterName = mercenary.Name;

                if (mercenary.CurrentMP < skill.MPCost)
                {
                    return false;
                }

                mpSpent = mercenary.UseMP(skill.MPCost);
            }

            if (!mpSpent)
            {
                return false;
            }

            // Restore target's HP
            int healAmount = skill.HPRestoreAmount;
            bool healed = false;
            string targetName;
            int currentHP = 0;
            int maxHP = 0;

            if (isTargetHero)
            {
                var hero = (RolePlayingFramework.Heroes.Hero)target;
                healed = hero.RestoreHP(healAmount);
                targetName = hero.Name;
                currentHP = hero.CurrentHP;
                maxHP = hero.MaxHP;
            }
            else
            {
                var mercenary = (RolePlayingFramework.Mercenaries.Mercenary)target;
                healed = mercenary.RestoreHP(healAmount);
                targetName = mercenary.Name;
                currentHP = mercenary.CurrentHP;
                maxHP = mercenary.MaxHP;
            }

            if (healed)
            {
                int casterMP = isCasterHero ? ((RolePlayingFramework.Heroes.Hero)caster).CurrentMP : ((RolePlayingFramework.Mercenaries.Mercenary)caster).CurrentMP;
                int casterMaxMP = isCasterHero ? ((RolePlayingFramework.Heroes.Hero)caster).MaxMP : ((RolePlayingFramework.Mercenaries.Mercenary)caster).MaxMP;
                Debug.Log($"[UseHealingSkillAction] {casterName} healed {targetName} for {healAmount} HP using {skill.Name}. Target HP: {currentHP}/{maxHP}, Caster MP: {casterMP}/{casterMaxMP}");
                return true;
            }

            return false;
        }
    }
}
