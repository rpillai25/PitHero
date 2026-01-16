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
            bool success = UseHealingItemOnTarget(item as Consumable, slotIndex, target, isHero, shortcutBar, hero);
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
        /// Get the shortcut bar from the service
        /// </summary>
        private ShortcutBar GetShortcutBar()
        {
            var service = Core.Services.GetService<ShortcutBarService>();
            return service?.ShortcutBar;
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

            Debug.Log($"[UseHealingItemAction] Searching for healing item. HP needed: {hpNeeded}");
            Debug.Log($"[UseHealingItemAction] === SHORTCUT BAR STATE ===");

            // First pass: log ALL slots to debug the shortcut bar state
            for (int i = 0; i < 8; i++)
            {
                var slotData = shortcutBar.GetShortcutSlotData(i);
                if (slotData == null)
                {
                    Debug.Log($"[UseHealingItemAction] Slot {i + 1}: NULL slotData");
                    continue;
                }

                if (slotData.SlotType == ShortcutSlotType.Empty)
                {
                    Debug.Log($"[UseHealingItemAction] Slot {i + 1}: EMPTY");
                    continue;
                }

                if (slotData.SlotType == ShortcutSlotType.Skill)
                {
                    var skill = slotData.ReferencedSkill;
                    Debug.Log($"[UseHealingItemAction] Slot {i + 1}: SKILL - {skill?.Name ?? "null"}");
                    continue;
                }

                // SlotType.Item
                var referencedSlot = slotData.ReferencedSlot;
                if (referencedSlot == null)
                {
                    Debug.Log($"[UseHealingItemAction] Slot {i + 1}: ITEM (null reference)");
                    continue;
                }

                var item = referencedSlot.SlotData?.Item;
                if (item == null)
                {
                    Debug.Log($"[UseHealingItemAction] Slot {i + 1}: ITEM (null item)");
                    continue;
                }

                if (item is Consumable consumable)
                {
                    Debug.Log($"[UseHealingItemAction] Slot {i + 1}: CONSUMABLE - {consumable.Name} (heals {consumable.HPRestoreAmount} HP, stack: {consumable.StackCount}, battle-only: {consumable.BattleOnly})");
                }
                else
                {
                    Debug.Log($"[UseHealingItemAction] Slot {i + 1}: ITEM - {item.Name} (kind: {item.Kind})");
                }
            }

            Debug.Log($"[UseHealingItemAction] === END SHORTCUT BAR STATE ===");

            // Second pass: find the best healing item
            for (int i = 0; i < 8; i++) // 8 shortcut slots
            {
                var slotData = shortcutBar.GetShortcutSlotData(i);
                if (slotData == null || slotData.SlotType != ShortcutSlotType.Item) continue;

                var item = slotData.ReferencedSlot?.SlotData?.Item;
                if (item == null || item.Kind != ItemKind.Consumable) continue;

                var consumable = item as Consumable;
                if (consumable == null) continue;

                // Skip consumables with zero or negative stack count (already consumed)
                if (consumable.StackCount <= 0)
                {
                    Debug.Log($"[UseHealingItemAction] Skipping {consumable.Name} - stack count is {consumable.StackCount} (already consumed)");
                    continue;
                }

                // Special case: -1 means "full heal" (restore to max HP)
                bool isFullHeal = consumable.HPRestoreAmount == -1;
                
                // Skip items that don't heal HP (unless they're full heal items)
                if (!isFullHeal && consumable.HPRestoreAmount <= 0) continue;

                // Don't use battle-only consumables outside of battle
                if (consumable.BattleOnly) continue;

                // Calculate waste (how much healing would be wasted)
                // For full heal items, assign maximum waste so they're only used as last resort
                // This ensures we always prefer cheaper/weaker potions when they can do the job
                int actualHealAmount;
                int waste;
                
                if (isFullHeal)
                {
                    actualHealAmount = maxHP; // Can heal any amount up to max HP
                    waste = 999999; // Maximum waste - save full heal potions for emergencies
                }
                else
                {
                    actualHealAmount = consumable.HPRestoreAmount;
                    waste = actualHealAmount - hpNeeded;
                }
                
                // If this item can heal the target and has less waste than our current best, use it
                if (actualHealAmount >= hpNeeded && waste < bestWaste)
                {
                    bestItem = item;
                    bestSlotIndex = i;
                    bestWaste = waste;
                    Debug.Log($"[UseHealingItemAction] New best item: {consumable.Name} (waste: {waste})");
                }
                else if (bestItem == null && actualHealAmount > 0)
                {
                    // If we haven't found a perfect fit yet, keep track of any healing item
                    // (even if it won't fully heal, it's better than nothing)
                    bestItem = item;
                    bestSlotIndex = i;
                    bestWaste = waste;
                    Debug.Log($"[UseHealingItemAction] Fallback item: {consumable.Name} (partial heal)");
                }
            }

            if (bestItem != null)
            {
                Debug.Log($"[UseHealingItemAction] Selected healing item: {bestItem.Name} from slot {bestSlotIndex + 1}");
            }
            else
            {
                Debug.Log($"[UseHealingItemAction] No healing items found on shortcut bar");
            }

            return (bestItem, bestSlotIndex);
        }

        /// <summary>
        /// Use the healing item on the target (hero or mercenary)
        /// </summary>
        private bool UseHealingItemOnTarget(Consumable consumable, int slotIndex, object target, bool isHero, ShortcutBar shortcutBar, HeroComponent heroComponent)
        {
            if (consumable == null || target == null) return false;

            // Special case: -1 means "full heal" (restore to max HP)
            bool isFullHeal = consumable.HPRestoreAmount == -1;
            
            // Restore HP
            int healAmount = 0;
            bool healed = false;
            int currentHP = 0;
            int maxHP = 0;
            string targetName = "";

            if (isHero)
            {
                var hero = (RolePlayingFramework.Heroes.Hero)target;
                // For full heal items, restore to max HP
                healAmount = isFullHeal ? hero.MaxHP : consumable.HPRestoreAmount;
                healed = hero.RestoreHP(healAmount);
                currentHP = hero.CurrentHP;
                maxHP = hero.MaxHP;
                targetName = hero.Name;
            }
            else
            {
                var mercenary = (RolePlayingFramework.Mercenaries.Mercenary)target;
                // For full heal items, restore to max HP
                healAmount = isFullHeal ? mercenary.MaxHP : consumable.HPRestoreAmount;
                mercenary.Heal(healAmount);
                healed = true;
                currentHP = mercenary.CurrentHP;
                maxHP = mercenary.MaxHP;
                targetName = mercenary.Name;
            }

            if (healed)
            {
                Debug.Log($"[UseHealingItemAction] Healed {targetName} for {healAmount} HP. Current HP: {currentHP}/{maxHP}");

                // Consume the item from the hero's bag using the proper bag index
                var referencedSlot = shortcutBar.GetReferencedSlot(slotIndex);
                if (referencedSlot?.SlotData?.BagIndex.HasValue == true)
                {
                    int bagIndex = referencedSlot.SlotData.BagIndex.Value;
                    if (heroComponent.Bag.ConsumeFromStack(bagIndex))
                    {
                        Debug.Log($"[UseHealingItemAction] Consumed {consumable.Name} from bag index {bagIndex}");
                        
                        // Refresh the shortcut bar to update references after item consumption
                        shortcutBar.RefreshItems();
                    }
                    else
                    {
                        Debug.Warn($"[UseHealingItemAction] Failed to consume {consumable.Name} from bag index {bagIndex}");
                    }
                }
                else
                {
                    Debug.Warn($"[UseHealingItemAction] Could not find bag index for {consumable.Name}");
                }

                return true;
            }

            return false;
        }
    }
}
