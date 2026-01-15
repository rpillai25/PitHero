using Microsoft.Xna.Framework;
using Nez;
using PitHero.AI.Interfaces;
using PitHero.ECS.Components;
using PitHero.Services;
using PitHero.UI;
using RolePlayingFramework.Equipment;
using System.Collections.Generic;

namespace PitHero.AI
{
    /// <summary>
    /// Action that uses a healing item from the shortcut bar to restore HP
    /// Chooses the most efficient healing item (least waste) for the target with critical HP (hero or mercenary)
    /// </summary>
    public class UseHealingItemAction : HeroActionBase
    {
        public UseHealingItemAction() : base(GoapConstants.UseHealingItemAction, 10)
        {
            // Preconditions: HP is critical and we haven't exhausted healing items
            SetPrecondition(GoapConstants.HPCritical, true);
            SetPrecondition(GoapConstants.HealingItemExhausted, false);

            // Postcondition: HP is no longer critical
            SetPostcondition(GoapConstants.HPCritical, false);
        }

        /// <summary>
        /// Execute the healing item action
        /// </summary>
        public override bool Execute(HeroComponent hero)
        {
            // Find the target (hero or mercenary) with critical HP
            var target = FindCriticalHPTarget(hero, out bool isHero, out Entity targetEntity);
            if (target == null)
            {
                Debug.Log("[UseHealingItemAction] No target with critical HP found");
                hero.HealingItemExhausted = true;
                return true; // Action complete (failed, but complete)
            }

            // Get the shortcut bar
            var shortcutBar = GetShortcutBar();
            if (shortcutBar == null)
            {
                Debug.Warn("[UseHealingItemAction] Could not find shortcut bar");
                hero.HealingItemExhausted = true;
                return true;
            }

            // Get target stats
            int currentHP = isHero ? ((RolePlayingFramework.Heroes.Hero)target).CurrentHP : ((RolePlayingFramework.Mercenaries.Mercenary)target).CurrentHP;
            int maxHP = isHero ? ((RolePlayingFramework.Heroes.Hero)target).MaxHP : ((RolePlayingFramework.Mercenaries.Mercenary)target).MaxHP;
            string targetName = isHero ? ((RolePlayingFramework.Heroes.Hero)target).Name : ((RolePlayingFramework.Mercenaries.Mercenary)target).Name;

            // Find the most efficient healing item on the shortcut bar
            (IItem item, int slotIndex) = FindMostEfficientHealingItem(shortcutBar, currentHP, maxHP);
            if (item == null)
            {
                Debug.Log("[UseHealingItemAction] No healing items available on shortcut bar");
                hero.HealingItemExhausted = true;
                return true;
            }

            // Use the healing item on the target
            bool success = UseHealingItemOnTarget(item as Consumable, slotIndex, target, isHero, shortcutBar);
            if (!success)
            {
                Debug.Warn("[UseHealingItemAction] Failed to use healing item");
                hero.HealingItemExhausted = true;
                return true;
            }

            Debug.Log($"[UseHealingItemAction] Successfully used {item.Name} on {targetName}");
            return true; // Action complete
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
        /// Find the most efficient healing item on the shortcut bar for the target
        /// Returns the healing item with the least waste (smallest amount over what's needed)
        /// </summary>
        private (IItem, int) FindMostEfficientHealingItem(ShortcutBar shortcutBar, int currentHP, int maxHP)
        {
            int hpNeeded = maxHP - currentHP;
            IItem bestItem = null;
            int bestSlotIndex = -1;
            int bestWaste = int.MaxValue;

            for (int i = 0; i < 8; i++) // 8 shortcut slots
            {
                var slotData = shortcutBar.GetShortcutSlotData(i);
                if (slotData == null || slotData.SlotType != ShortcutSlotType.Item) continue;

                var item = slotData.ReferencedSlot?.SlotData?.Item;
                if (item == null || item.Kind != ItemKind.Consumable) continue;

                var consumable = item as Consumable;
                if (consumable == null || consumable.HPRestoreAmount <= 0) continue;

                // Don't use battle-only consumables outside of battle
                if (consumable.BattleOnly) continue;

                // Calculate waste (how much healing would be wasted)
                int waste = consumable.HPRestoreAmount - hpNeeded;
                
                // If this item can heal the target and has less waste than our current best, use it
                if (consumable.HPRestoreAmount >= hpNeeded && waste < bestWaste)
                {
                    bestItem = item;
                    bestSlotIndex = i;
                    bestWaste = waste;
                }
                else if (bestItem == null && consumable.HPRestoreAmount > 0)
                {
                    // If we haven't found a perfect fit yet, keep track of any healing item
                    // (even if it won't fully heal, it's better than nothing)
                    bestItem = item;
                    bestSlotIndex = i;
                    bestWaste = waste;
                }
            }

            return (bestItem, bestSlotIndex);
        }

        /// <summary>
        /// Use the healing item on the target (hero or mercenary)
        /// </summary>
        private bool UseHealingItemOnTarget(Consumable consumable, int slotIndex, object target, bool isHero, ShortcutBar shortcutBar)
        {
            if (consumable == null || target == null) return false;

            // Restore HP
            int healAmount = consumable.HPRestoreAmount;
            bool healed = false;
            int currentHP = 0;
            int maxHP = 0;
            string targetName = "";

            if (isHero)
            {
                var hero = (RolePlayingFramework.Heroes.Hero)target;
                healed = hero.RestoreHP(healAmount);
                currentHP = hero.CurrentHP;
                maxHP = hero.MaxHP;
                targetName = hero.Name;
            }
            else
            {
                var mercenary = (RolePlayingFramework.Mercenaries.Mercenary)target;
                mercenary.Heal(healAmount);
                healed = true;
                currentHP = mercenary.CurrentHP;
                maxHP = mercenary.MaxHP;
                targetName = mercenary.Name;
            }

            if (healed)
            {
                Debug.Log($"[UseHealingItemAction] Healed {targetName} for {healAmount} HP. Current HP: {currentHP}/{maxHP}");

                // Consume the item (reduce stack count or remove from inventory)
                var referencedSlot = shortcutBar.GetReferencedSlot(slotIndex);
                if (referencedSlot != null)
                {
                    // Decrement stack count or remove item
                    if (consumable.StackCount > 1)
                    {
                        consumable.StackCount--;
                        Debug.Log($"[UseHealingItemAction] Decremented {consumable.Name} stack count to {consumable.StackCount}");
                    }
                    else
                    {
                        // Remove the item from the inventory
                        referencedSlot.SlotData.Item = null;
                        Debug.Log($"[UseHealingItemAction] Removed {consumable.Name} from inventory (last in stack)");
                    }
                }

                return true;
            }

            return false;
        }
    }
}
