using Microsoft.Xna.Framework;
using Nez;
using PitHero.AI.Interfaces;
using PitHero.ECS.Components;
using PitHero.Services;
using PitHero.Util;
using PitHero.Util.SoundEffectTypes;
using RolePlayingFramework.Equipment;
using System;
using System.Collections.Generic;

namespace PitHero.AI
{
    /// <summary>
    /// Action that uses a healing item from inventory to restore HP and/or MP.
    /// Uses PotionSelectionEngine out-of-battle rules: minimize total waste,
    /// handle single/dual resource needs, and conserve full potions.
    /// </summary>
    public class UseHealingItemAction : HeroActionBase
    {
        public UseHealingItemAction() : base(GoapConstants.UseHealingItemAction, 10)
        {
            // No static preconditions for HPCritical/MPCritical because this action can satisfy
            // either goal. The Validate() method dynamically checks:
            // - At least one of HPCritical or MPCritical must be true
            // - HealingItemExhausted must be false
            // - Priority checks against other healing options

            // Inn restores both HP and MP to full, so set both postconditions
            SetPostcondition(GoapConstants.HPCritical, false);
            SetPostcondition(GoapConstants.MPCritical, false);
        }

        public override bool Validate()
        {
            var heroComponent = Game1.Scene.FindEntity("hero")?.GetComponent<HeroComponent>();
            if (heroComponent == null) return false;
            
            var healPrioritiesInOrder = heroComponent.GetHealPrioritiesInOrder();
            
            // Must have either HPCritical or MPCritical
            if (!heroComponent.HPCritical && !heroComponent.MPCritical)
            {
                return false;
            }

            if (healPrioritiesInOrder != null)
            {
                int itemPriority = Array.IndexOf(healPrioritiesInOrder, HeroHealPriority.HealingItem);
                int skillPriority = Array.IndexOf(healPrioritiesInOrder, HeroHealPriority.HealingSkill);
                int innPriority = Array.IndexOf(healPrioritiesInOrder, HeroHealPriority.Inn);

                // Check if we should wait for a higher-priority option
                // Note: HealingSkill can only address HPCritical, not MPCritical, so when only MP is low,
                // we should NOT wait for HealingSkill even if it has higher priority
                bool shouldWaitForSkill = itemPriority > skillPriority && 
                                          !heroComponent.HealingSkillExhausted &&
                                          heroComponent.HPCritical; // Only wait if HP is critical (skill can help)
                
                bool shouldWaitForInn = itemPriority > innPriority && 
                                        !heroComponent.InnExhausted;

                if (shouldWaitForSkill || shouldWaitForInn)
                {
                    return false;
                }
            }

            return !heroComponent.HealingItemExhausted;
        }

        /// <summary>
        /// Execute the healing item action using out-of-battle rules.
        /// Finds the most critical target (hero or mercenary) and selects the best potion
        /// using PotionSelectionEngine's waste-minimization logic.
        /// </summary>
        public override bool Execute(HeroComponent hero)
        {
            // Find the most critical target considering both HP and MP
            var target = FindMostCriticalTarget(hero, out bool isHero, out Entity targetEntity);
            if (target != null)
            {
                int currentHP = isHero ? ((RolePlayingFramework.Heroes.Hero)target).CurrentHP : ((RolePlayingFramework.Mercenaries.Mercenary)target).CurrentHP;
                int maxHP = isHero ? ((RolePlayingFramework.Heroes.Hero)target).MaxHP : ((RolePlayingFramework.Mercenaries.Mercenary)target).MaxHP;
                int currentMP = isHero ? ((RolePlayingFramework.Heroes.Hero)target).CurrentMP : ((RolePlayingFramework.Mercenaries.Mercenary)target).CurrentMP;
                int maxMP = isHero ? ((RolePlayingFramework.Heroes.Hero)target).MaxMP : ((RolePlayingFramework.Mercenaries.Mercenary)target).MaxMP;
                string targetName = isHero ? ((RolePlayingFramework.Heroes.Hero)target).Name : ((RolePlayingFramework.Mercenaries.Mercenary)target).Name;

                // Use PotionSelectionEngine out-of-battle rules
                Consumable bestConsumable;
                int bestIndex;
                if (PotionSelectionEngine.SelectOutOfBattlePotion(
                    hero.Bag, currentHP, maxHP, currentMP, maxMP,
                    out bestConsumable, out bestIndex))
                {
                    bool success = UseHealingItemFromBag(bestConsumable, bestIndex, target, isHero, hero);
                    if (success)
                    {
                        Debug.Log($"[UseHealingItemAction] Successfully used {bestConsumable.Name} on {targetName} from inventory");
                        return true;
                    }
                }
            }

            Debug.Log("[UseHealingItemAction] No healing or MP items available");
            hero.HealingItemExhausted = true;
            return true;
        }

        /// <summary>
        /// Execute action using interface-based context (for virtual game)
        /// </summary>
        public override bool Execute(IGoapContext context)
        {
            context.LogDebug("[UseHealingItemAction] Healing item action executed (interface-based context)");
            return true;
        }

        /// <summary>
        /// Find the most critical target (hero or mercenary) that needs HP or MP restoration.
        /// Checks both HP-critical and MP-critical states. Prioritizes the target with the
        /// lowest resource percentage (most in need).
        /// </summary>
        private object FindMostCriticalTarget(HeroComponent heroComponent, out bool isHero, out Entity targetEntity)
        {
            isHero = true;
            targetEntity = null;
            object bestTarget = null;
            float lowestPercent = 1f;

            // Check hero
            if (heroComponent.LinkedHero != null)
            {
                var hero = heroComponent.LinkedHero;
                float hpPercent = hero.MaxHP > 0 ? (float)hero.CurrentHP / hero.MaxHP : 1f;
                float mpPercent = hero.MaxMP > 0 ? (float)hero.CurrentMP / hero.MaxMP : 1f;
                float minPercent = System.Math.Min(hpPercent, mpPercent);

                bool hpCritical = hpPercent < GameConfig.HeroCriticalHPPercent;
                bool mpCritical = mpPercent < GameConfig.HeroCriticalMPPercent;

                if ((hpCritical || mpCritical) && minPercent < lowestPercent)
                {
                    bestTarget = hero;
                    isHero = true;
                    targetEntity = heroComponent.Entity;
                    lowestPercent = minPercent;
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
                        var mercenary = mercComp.LinkedMercenary;
                        float hpPercent = mercenary.MaxHP > 0 ? (float)mercenary.CurrentHP / mercenary.MaxHP : 1f;
                        float mpPercent = mercenary.MaxMP > 0 ? (float)mercenary.CurrentMP / mercenary.MaxMP : 1f;
                        float minPercent = System.Math.Min(hpPercent, mpPercent);

                        bool hpCritical = hpPercent < GameConfig.HeroCriticalHPPercent;
                        bool mpCritical = mpPercent < GameConfig.HeroCriticalMPPercent;

                        if ((hpCritical || mpCritical) && minPercent < lowestPercent)
                        {
                            bestTarget = mercenary;
                            isHero = false;
                            targetEntity = merc;
                            lowestPercent = minPercent;
                        }
                    }
                }
            }

            return bestTarget;
        }

        /// <summary>
        /// Use the healing item from bag on the target (hero or mercenary)
        /// Similar to InventoryGrid.UseConsumable but works for both Hero and Mercenary targets
        /// </summary>
        private bool UseHealingItemFromBag(Consumable consumable, int bagIndex, object target, bool isHero, HeroComponent heroComponent)
        {
            if (consumable == null || target == null || bagIndex < 0) return false;

            // Use the consumable's Consume method which handles both Hero and Mercenary contexts
            bool consumed = consumable.Consume(target);
            
            if (consumed)
            {
                // Reset HealingSkillExhausted so healing skills are re-evaluated next cycle
                // (MP may have been restored, making previously exhausted skills viable again)
                heroComponent.HealingSkillExhausted = false;

                // Play restorative sound effect
                SoundEffectManager soundEffectManager = Core.GetGlobalManager<SoundEffectManager>();
                soundEffectManager.PlaySound(SoundEffectType.Restorative);

                string targetName = isHero 
                    ? ((RolePlayingFramework.Heroes.Hero)target).Name 
                    : ((RolePlayingFramework.Mercenaries.Mercenary)target).Name;
                
                int currentHP = isHero 
                    ? ((RolePlayingFramework.Heroes.Hero)target).CurrentHP 
                    : ((RolePlayingFramework.Mercenaries.Mercenary)target).CurrentHP;
                
                int maxHP = isHero 
                    ? ((RolePlayingFramework.Heroes.Hero)target).MaxHP 
                    : ((RolePlayingFramework.Mercenaries.Mercenary)target).MaxHP;

                Debug.Log($"[UseHealingItemAction] Used {consumable.Name} on {targetName}. Current HP: {currentHP}/{maxHP}");

                // Consume the item from the hero's bag
                if (heroComponent.Bag != null && heroComponent.Bag.ConsumeFromStack(bagIndex))
                {
                    Debug.Log($"[UseHealingItemAction] Consumed {consumable.Name} from bag index {bagIndex}");
                }
                else
                {
                    Debug.Warn($"[UseHealingItemAction] Failed to consume {consumable.Name} from bag index {bagIndex}");
                }

                return true;
            }

            return false;
        }
    }
}
