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
        // Maximum waste value for full heal potions (save them for emergencies)
        private const int MAX_WASTE_FULL_HEAL = 999999;

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

            // Get target stats
            int currentHP = isHero ? ((RolePlayingFramework.Heroes.Hero)target).CurrentHP : ((RolePlayingFramework.Mercenaries.Mercenary)target).CurrentHP;
            int maxHP = isHero ? ((RolePlayingFramework.Heroes.Hero)target).MaxHP : ((RolePlayingFramework.Mercenaries.Mercenary)target).MaxHP;
            int currentMP = isHero ? ((RolePlayingFramework.Heroes.Hero)target).CurrentMP : ((RolePlayingFramework.Mercenaries.Mercenary)target).CurrentMP;
            int maxMP = isHero ? ((RolePlayingFramework.Heroes.Hero)target).MaxMP : ((RolePlayingFramework.Mercenaries.Mercenary)target).MaxMP;
            string targetName = isHero ? ((RolePlayingFramework.Heroes.Hero)target).Name : ((RolePlayingFramework.Mercenaries.Mercenary)target).Name;

            // Check if we should use only action bar items or all inventory items
            if (hero.OnlyUseActionBarItemsAndSkills)
            {
                // Original behavior: use only shortcut bar items
                var shortcutBar = GetShortcutBar();
                if (shortcutBar == null)
                {
                    Debug.Warn("[UseHealingItemAction] Could not find shortcut bar");
                    hero.HealingItemExhausted = true;
                    return true;
                }

                (IItem item, int slotIndex) = FindMostEfficientHealingItem(shortcutBar, currentHP, maxHP, currentMP, maxMP);
                if (item == null)
                {
                    Debug.Log("[UseHealingItemAction] No healing items available on shortcut bar");
                    hero.HealingItemExhausted = true;
                    return true;
                }

                bool success = UseHealingItemOnTarget(item as Consumable, slotIndex, target, isHero, shortcutBar, hero);
                if (!success)
                {
                    Debug.Warn("[UseHealingItemAction] Failed to use healing item");
                    hero.HealingItemExhausted = true;
                    return true;
                }

                Debug.Log($"[UseHealingItemAction] Successfully used {item.Name} on {targetName}");
                return true;
            }
            else
            {
                // New behavior: use all items from inventory
                (IItem item, int bagIndex) = FindMostEfficientHealingItemFromBag(hero.Bag, currentHP, maxHP, currentMP, maxMP);
                if (item == null)
                {
                    Debug.Log("[UseHealingItemAction] No healing items available in inventory");
                    hero.HealingItemExhausted = true;
                    return true;
                }

                bool success = UseHealingItemFromBag(item as Consumable, bagIndex, target, isHero, hero);
                if (!success)
                {
                    Debug.Warn("[UseHealingItemAction] Failed to use healing item from bag");
                    hero.HealingItemExhausted = true;
                    return true;
                }

                Debug.Log($"[UseHealingItemAction] Successfully used {item.Name} on {targetName} from inventory");
                return true;
            }
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
        /// Prioritizes by (HPWaste, MPWaste) - minimizes HP waste first, then MP waste as tiebreaker
        /// </summary>
        private (IItem, int) FindMostEfficientHealingItem(ShortcutBar shortcutBar, int currentHP, int maxHP, int currentMP, int maxMP)
        {
            int hpNeeded = maxHP - currentHP;
            int mpNeeded = maxMP - currentMP;
            IItem bestItem = null;
            int bestSlotIndex = -1;
            (int hpWaste, int mpWaste) bestWaste = (int.MaxValue, int.MaxValue);

            Debug.Log($"[UseHealingItemAction] Searching for healing item. HP needed: {hpNeeded}, MP needed: {mpNeeded}");
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
                    Debug.Log($"[UseHealingItemAction] Slot {i + 1}: CONSUMABLE - {consumable.Name} (heals {consumable.HPRestoreAmount} HP, {consumable.MPRestoreAmount} MP, stack: {consumable.StackCount}, battle-only: {consumable.BattleOnly})");
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

                // Special case: -1 means "full heal" (restore to max HP/MP)
                bool isFullHPHeal = consumable.HPRestoreAmount == -1;
                bool isFullMPHeal = consumable.MPRestoreAmount == -1;
                
                // Skip items that don't heal HP (unless they're full heal items)
                if (!isFullHPHeal && consumable.HPRestoreAmount <= 0) continue;

                // Don't use battle-only consumables outside of battle
                if (consumable.BattleOnly) continue;

                // Calculate HP waste (how much HP healing would be wasted)
                // For full heal items, assign maximum waste so they're only used as last resort
                int actualHPHealAmount;
                int hpWaste;
                
                if (isFullHPHeal)
                {
                    actualHPHealAmount = maxHP; // Can heal any amount up to max HP
                    hpWaste = MAX_WASTE_FULL_HEAL; // Maximum waste - save full heal potions for emergencies
                }
                else
                {
                    actualHPHealAmount = consumable.HPRestoreAmount;
                    hpWaste = actualHPHealAmount - hpNeeded;
                }

                // Calculate MP waste (how much MP healing would be wasted)
                // For full MP heal items, assign maximum waste so they're only used as last resort
                int actualMPHealAmount;
                int mpWaste;
                
                if (isFullMPHeal)
                {
                    actualMPHealAmount = maxMP; // Can heal any amount up to max MP
                    mpWaste = MAX_WASTE_FULL_HEAL; // Maximum waste - save full heal potions for emergencies
                }
                else if (consumable.MPRestoreAmount > 0)
                {
                    actualMPHealAmount = consumable.MPRestoreAmount;
                    mpWaste = actualMPHealAmount - mpNeeded;
                }
                else
                {
                    actualMPHealAmount = 0;
                    mpWaste = 0; // No MP restoration = no waste
                }

                // Combine waste as tuple for comparison: (HPWaste, MPWaste)
                // Tuple comparison prioritizes HP waste first, then MP waste as tiebreaker
                var totalWaste = (hpWaste, mpWaste);
                
                // If this item can heal the target and has less waste than our current best, use it
                if (actualHPHealAmount >= hpNeeded && totalWaste.CompareTo(bestWaste) < 0)
                {
                    bestItem = item;
                    bestSlotIndex = i;
                    bestWaste = totalWaste;
                    Debug.Log($"[UseHealingItemAction] New best item: {consumable.Name} (HP waste: {hpWaste}, MP waste: {mpWaste})");
                }
                else if (bestItem == null && actualHPHealAmount > 0)
                {
                    // If we haven't found a perfect fit yet, keep track of any healing item
                    // (even if it won't fully heal, it's better than nothing)
                    bestItem = item;
                    bestSlotIndex = i;
                    bestWaste = totalWaste;
                    Debug.Log($"[UseHealingItemAction] Fallback item: {consumable.Name} (partial heal, HP waste: {hpWaste}, MP waste: {mpWaste})");
                }
            }

            if (bestItem != null)
            {
                Debug.Log($"[UseHealingItemAction] Selected healing item: {bestItem.Name} from slot {bestSlotIndex + 1} (HP waste: {bestWaste.hpWaste}, MP waste: {bestWaste.mpWaste})");
            }
            else
            {
                Debug.Log($"[UseHealingItemAction] No healing items found on shortcut bar");
            }

            return (bestItem, bestSlotIndex);
        }

        /// <summary>
        /// Find the most efficient healing item from the bag for the target
        /// Returns the healing item with the least waste (smallest amount over what's needed)
        /// Prioritizes by (HPWaste, MPWaste) - minimizes HP waste first, then MP waste as tiebreaker
        /// </summary>
        private (IItem, int) FindMostEfficientHealingItemFromBag(RolePlayingFramework.Inventory.ItemBag bag, int currentHP, int maxHP, int currentMP, int maxMP)
        {
            int hpNeeded = maxHP - currentHP;
            int mpNeeded = maxMP - currentMP;
            IItem bestItem = null;
            int bestBagIndex = -1;
            (int hpWaste, int mpWaste) bestWaste = (int.MaxValue, int.MaxValue);

            Debug.Log($"[UseHealingItemAction] Searching for healing item in bag. HP needed: {hpNeeded}, MP needed: {mpNeeded}");

            // Search through all bag slots
            for (int i = 0; i < bag.Capacity; i++)
            {
                var item = bag.GetSlotItem(i);
                if (item == null || item.Kind != ItemKind.Consumable) continue;

                var consumable = item as Consumable;
                if (consumable == null) continue;

                // Skip consumables with zero or negative stack count
                if (consumable.StackCount <= 0) continue;

                // Special case: -1 means "full heal"
                bool isFullHPHeal = consumable.HPRestoreAmount == -1;
                bool isFullMPHeal = consumable.MPRestoreAmount == -1;
                
                // Skip items that don't heal HP
                if (!isFullHPHeal && consumable.HPRestoreAmount <= 0) continue;

                // Don't use battle-only consumables outside of battle
                if (consumable.BattleOnly) continue;

                // Calculate HP waste
                int actualHPHealAmount;
                int hpWaste;
                
                if (isFullHPHeal)
                {
                    actualHPHealAmount = maxHP;
                    hpWaste = MAX_WASTE_FULL_HEAL; // Maximum waste - save full heal potions for emergencies
                }
                else
                {
                    actualHPHealAmount = consumable.HPRestoreAmount;
                    hpWaste = actualHPHealAmount - hpNeeded;
                }

                // Calculate MP waste
                int actualMPHealAmount;
                int mpWaste;
                
                if (isFullMPHeal)
                {
                    actualMPHealAmount = maxMP;
                    mpWaste = MAX_WASTE_FULL_HEAL;
                }
                else if (consumable.MPRestoreAmount > 0)
                {
                    actualMPHealAmount = consumable.MPRestoreAmount;
                    mpWaste = actualMPHealAmount - mpNeeded;
                }
                else
                {
                    actualMPHealAmount = 0;
                    mpWaste = 0;
                }

                var totalWaste = (hpWaste, mpWaste);
                
                // If this item can heal the target and has less waste than our current best, use it
                if (actualHPHealAmount >= hpNeeded && totalWaste.CompareTo(bestWaste) < 0)
                {
                    bestItem = item;
                    bestBagIndex = i;
                    bestWaste = totalWaste;
                    Debug.Log($"[UseHealingItemAction] New best item from bag: {consumable.Name} at index {i} (HP waste: {hpWaste}, MP waste: {mpWaste})");
                }
                else if (bestItem == null && actualHPHealAmount > 0)
                {
                    // If we haven't found a perfect fit yet, keep track of any healing item
                    bestItem = item;
                    bestBagIndex = i;
                    bestWaste = totalWaste;
                    Debug.Log($"[UseHealingItemAction] Fallback item from bag: {consumable.Name} at index {i} (partial heal)");
                }
            }

            if (bestItem != null)
            {
                Debug.Log($"[UseHealingItemAction] Selected healing item from bag: {bestItem.Name} at index {bestBagIndex}");
            }
            else
            {
                Debug.Log($"[UseHealingItemAction] No healing items found in bag");
            }

            return (bestItem, bestBagIndex);
        }

        /// <summary>
        /// Use the healing item on the target (hero or mercenary)
        /// </summary>
        private bool UseHealingItemOnTarget(Consumable consumable, int slotIndex, object target, bool isHero, ShortcutBar shortcutBar, HeroComponent heroComponent)
        {
            if (consumable == null || target == null) return false;

            // Use the consumable's Consume method which handles both Hero and Mercenary contexts
            bool consumed = consumable.Consume(target);
            
            if (consumed)
            {
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
                    
                    // Refresh the shortcut bar to update references after item consumption
                    var shortcutBar = GetShortcutBar();
                    if (shortcutBar != null)
                    {
                        shortcutBar.RefreshItems();
                    }
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
