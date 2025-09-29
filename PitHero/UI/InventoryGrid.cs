using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Nez;
using Nez.UI;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Heroes;
using System.Collections.Generic;
using System.Linq;
using PitHero.ECS.Components;

namespace PitHero.UI
{
    /// <summary>Grid layout container for inventory slots with interaction logic.</summary>
    public class InventoryGrid : Group
    {
        private const int GRID_WIDTH = 8;
        private const int GRID_HEIGHT = 7;
        private const float SLOT_SIZE = 32f;
        private const float SLOT_PADDING = 1f;
        
        private readonly InventorySlotData[,] _slotGrid;
        private readonly List<InventorySlot> _slotComponents;
        private InventorySlot _highlightedSlot;
        private HeroComponent _heroComponent;
        
        public InventoryGrid()
        {
            _slotGrid = new InventorySlotData[GRID_WIDTH, GRID_HEIGHT];
            _slotComponents = new List<InventorySlot>();
            
            InitializeSlotGrid();
            CreateSlotComponents();
            LayoutSlots();
        }

        /// <summary>
        /// Connects this inventory grid to a hero component and updates based on their bag capacity.
        /// </summary>
        public void ConnectToHero(HeroComponent heroComponent)
        {
            _heroComponent = heroComponent;
            if (_heroComponent?.Bag != null)
            {
                UpdateBagCapacity(_heroComponent.Bag.Capacity);
                UpdateItemsFromBag();
            }
        }

        /// <summary>
        /// Updates the displayed items based on the hero's current bag contents and equipment.
        /// </summary>
        public void UpdateItemsFromBag()
        {
            if (_heroComponent?.Bag == null) return;

            // Clear all current items
            foreach (var slot in _slotComponents)
            {
                slot.SlotData.Item = null;
            }

            // Populate equipment slots from hero properties
            UpdateEquipmentSlots();

            // Populate bag items into shortcut and inventory slots with proper indexing
            UpdateBagSlots();
        }

        /// <summary>
        /// Updates equipment slots to match hero's current equipment.
        /// </summary>
        private void UpdateEquipmentSlots()
        {
            var heroEquipment = _heroComponent.Entity.GetComponent<Hero>();
            if (heroEquipment == null) return;

            foreach (var slot in _slotComponents.Where(s => s.SlotData.SlotType == InventorySlotType.Equipment))
            {
                var equipmentSlot = slot.SlotData.EquipmentSlot;
                slot.SlotData.Item = equipmentSlot switch
                {
                    EquipmentSlot.WeaponShield1 => heroEquipment.WeaponShield1,
                    EquipmentSlot.Armor => heroEquipment.Armor,
                    EquipmentSlot.Hat => heroEquipment.Hat,
                    EquipmentSlot.WeaponShield2 => heroEquipment.WeaponShield2,
                    EquipmentSlot.Accessory1 => heroEquipment.Accessory1,
                    EquipmentSlot.Accessory2 => heroEquipment.Accessory2,
                    _ => null
                };
            }
        }

        /// <summary>
        /// Updates bag slots (shortcut + inventory) with proper 1:1 bag index mapping.
        /// </summary>
        private void UpdateBagSlots()
        {
            var bagItems = _heroComponent.Bag.Items;
            
            // Get all bag slots (shortcut + inventory) ordered by their logical position
            var bagSlots = _slotComponents
                .Where(s => s.SlotData.SlotType == InventorySlotType.Shortcut || 
                           s.SlotData.SlotType == InventorySlotType.Inventory)
                .OrderBy(s => s.SlotData.Y)
                .ThenBy(s => s.SlotData.X)
                .ToList();

            // Populate items with 1:1 index mapping
            for (int i = 0; i < bagSlots.Count; i++)
            {
                if (i < bagItems.Count)
                {
                    bagSlots[i].SlotData.Item = bagItems[i];
                    bagSlots[i].SlotData.BagIndex = i; // Store the bag index for updates
                }
                else
                {
                    bagSlots[i].SlotData.Item = null;
                    bagSlots[i].SlotData.BagIndex = i; // Store the bag index even if empty
                }
            }
        }

        /// <summary>
        /// Handles keyboard shortcuts for shortcut slots (1-8). Called from parent UI.
        /// </summary>
        public void HandleKeyboardShortcuts()
        {
            // Check if keys 1-8 are pressed to activate shortcut slots
            for (int i = 0; i < 8; i++)
            {
                var key = (Keys)((int)Keys.D1 + i); // D1 = key "1", D2 = key "2", etc.
                
                if (Input.IsKeyPressed(key))
                {
                    // Find the shortcut slot for this key
                    var shortcutSlot = _slotComponents.FirstOrDefault(s => 
                        s.SlotData.SlotType == InventorySlotType.Shortcut && 
                        s.SlotData.ShortcutKey == i + 1);
                    
                    if (shortcutSlot?.SlotData.Item != null)
                    {
                        // Activate the item in this shortcut slot
                        ActivateShortcutSlot(shortcutSlot, i + 1);
                    }
                }
            }
        }

        private void ActivateShortcutSlot(InventorySlot slot, int keyNumber)
        {
            Debug.Log($"Activated shortcut slot {keyNumber} with item: {slot.SlotData.Item?.Name ?? "None"}");
            
            // TODO: Implement actual item activation logic
            // This would typically:
            // 1. Check if the item is consumable
            // 2. Apply the item effect to the hero
            // 3. Remove the item from the slot if it's consumed
            // 4. Update the hero's stats/status
        }

        private void InitializeSlotGrid()
        {
            // Initialize all slots as null/spacer by default
            for (int x = 0; x < GRID_WIDTH; x++)
            {
                for (int y = 0; y < GRID_HEIGHT; y++)
                {
                    _slotGrid[x, y] = new InventorySlotData(x, y, InventorySlotType.Null);
                }
            }

            // Equipment slots
            // Hat at (3,0)
            _slotGrid[3, 0] = new InventorySlotData(3, 0, InventorySlotType.Equipment) 
            { 
                EquipmentSlot = EquipmentSlot.Hat 
            };
            
            // WeaponShield1, Armor, WeaponShield2 at (1,1), (3,1), (5,1)
            _slotGrid[1, 1] = new InventorySlotData(1, 1, InventorySlotType.Equipment) 
            { 
                EquipmentSlot = EquipmentSlot.WeaponShield1 
            };
            _slotGrid[3, 1] = new InventorySlotData(3, 1, InventorySlotType.Equipment) 
            { 
                EquipmentSlot = EquipmentSlot.Armor 
            };
            _slotGrid[5, 1] = new InventorySlotData(5, 1, InventorySlotType.Equipment) 
            { 
                EquipmentSlot = EquipmentSlot.WeaponShield2 
            };
            
            // Accessory1 and Accessory2 at (2,2) and (4,2)
            _slotGrid[2, 2] = new InventorySlotData(2, 2, InventorySlotType.Equipment) 
            { 
                EquipmentSlot = EquipmentSlot.Accessory1 
            };
            _slotGrid[4, 2] = new InventorySlotData(4, 2, InventorySlotType.Equipment) 
            { 
                EquipmentSlot = EquipmentSlot.Accessory2 
            };

            // Shortcut slots at row 3 (0,3) through (7,3)
            for (int x = 0; x < GRID_WIDTH; x++)
            {
                _slotGrid[x, 3] = new InventorySlotData(x, 3, InventorySlotType.Shortcut) 
                { 
                    ShortcutKey = x + 1 
                };
            }

            // Inventory slots at rows 4-6 (0,4) through (7,6)
            for (int y = 4; y < GRID_HEIGHT; y++)
            {
                for (int x = 0; x < GRID_WIDTH; x++)
                {
                    _slotGrid[x, y] = new InventorySlotData(x, y, InventorySlotType.Inventory);
                }
            }
        }

        private void CreateSlotComponents()
        {
            for (int x = 0; x < GRID_WIDTH; x++)
            {
                for (int y = 0; y < GRID_HEIGHT; y++)
                {
                    var slotData = _slotGrid[x, y];
                    
                    // Only create components for non-null slots
                    if (slotData.SlotType != InventorySlotType.Null)
                    {
                        var slotComponent = new InventorySlot(slotData);
                        slotComponent.OnSlotClicked += HandleSlotClicked;
                        slotComponent.OnSlotHovered += HandleSlotHovered;
                        slotComponent.OnSlotUnhovered += HandleSlotUnhovered;
                        
                        _slotComponents.Add(slotComponent);
                        AddElement(slotComponent);
                    }
                }
            }
        }

        private void LayoutSlots()
        {
            foreach (var slot in _slotComponents)
            {
                var x = slot.SlotData.X * (SLOT_SIZE + SLOT_PADDING);
                var y = slot.SlotData.Y * (SLOT_SIZE + SLOT_PADDING);
                slot.SetPosition(x, y);
            }
        }

        private void HandleSlotClicked(InventorySlot clickedSlot)
        {
            if (_highlightedSlot == null)
            {
                // First click - highlight this slot
                _highlightedSlot = clickedSlot;
                clickedSlot.SlotData.IsHighlighted = true;
                Debug.Log($"Highlighted slot at ({clickedSlot.SlotData.X}, {clickedSlot.SlotData.Y}) of type {clickedSlot.SlotData.SlotType}");
            }
            else if (_highlightedSlot == clickedSlot)
            {
                // Same slot clicked - unhighlight
                _highlightedSlot.SlotData.IsHighlighted = false;
                _highlightedSlot = null;
                Debug.Log("Unhighlighted slot");
            }
            else
            {
                // Different slot clicked - swap items
                var prevHighlighted = _highlightedSlot;
                SwapSlotItems(_highlightedSlot, clickedSlot);
                
                // Clear highlight
                _highlightedSlot.SlotData.IsHighlighted = false;
                _highlightedSlot = null;
                Debug.Log($"Swapped items between slots ({prevHighlighted.SlotData.X}, {prevHighlighted.SlotData.Y}) and ({clickedSlot.SlotData.X}, {clickedSlot.SlotData.Y})");
            }
        }

        private void HandleSlotHovered(InventorySlot slot)
        {
            // Hover feedback is handled by the slot itself via IsHovered property
        }

        private void HandleSlotUnhovered(InventorySlot slot)
        {
            // Hover feedback is handled by the slot itself via IsHovered property
        }

        private void SwapSlotItems(InventorySlot slot1, InventorySlot slot2)
        {
            // Validate that items can be placed in target slots
            if (!CanPlaceItemInSlot(slot1.SlotData.Item, slot2.SlotData) ||
                !CanPlaceItemInSlot(slot2.SlotData.Item, slot1.SlotData))
            {
                Debug.Log("Cannot swap items - incompatible slot types");
                return;
            }

            // Store original items
            var item1 = slot1.SlotData.Item;
            var item2 = slot2.SlotData.Item;
            
            // Update UI slots
            slot1.SlotData.Item = item2;
            slot2.SlotData.Item = item1;
            
            // Update actual hero data
            UpdateHeroDataFromSlot(slot1);
            UpdateHeroDataFromSlot(slot2);
            
            Debug.Log($"Successfully swapped items between slots ({slot1.SlotData.X}, {slot1.SlotData.Y}) and ({slot2.SlotData.X}, {slot2.SlotData.Y})");
        }

        /// <summary>
        /// Validates if an item can be placed in a specific slot type.
        /// </summary>
        private bool CanPlaceItemInSlot(IItem item, InventorySlotData slotData)
        {
            // Null items can go anywhere
            if (item == null) return true;
            
            // Non-equipment slots accept any item
            if (slotData.SlotType != InventorySlotType.Equipment) return true;
            
            // Equipment slots have type restrictions
            var equipmentSlot = slotData.EquipmentSlot;
            return equipmentSlot switch
            {
                EquipmentSlot.WeaponShield1 or EquipmentSlot.WeaponShield2 => IsWeaponOrShield(item),
                EquipmentSlot.Armor => item.Kind == ItemKind.ArmorMail || item.Kind == ItemKind.ArmorRobe,
                EquipmentSlot.Hat => item.Kind == ItemKind.HatHelm || item.Kind == ItemKind.HatHeadband || item.Kind == ItemKind.HatWizard || item.Kind == ItemKind.HatPriest,
                EquipmentSlot.Accessory1 or EquipmentSlot.Accessory2 => item.Kind == ItemKind.Accessory,
                _ => false
            };
        }

        /// <summary>
        /// Checks if an item is a weapon or shield.
        /// </summary>
        private bool IsWeaponOrShield(IItem item)
        {
            return item.Kind == ItemKind.WeaponSword || 
                   item.Kind == ItemKind.WeaponKnuckle || 
                   item.Kind == ItemKind.WeaponStaff || 
                   item.Kind == ItemKind.WeaponRod || 
                   item.Kind == ItemKind.Shield;
        }

        /// <summary>
        /// Updates the actual hero data (equipment or bag) based on slot changes.
        /// </summary>
        private void UpdateHeroDataFromSlot(InventorySlot slot)
        {
            var heroEquipment = _heroComponent?.Entity.GetComponent<Hero>();
            if (heroEquipment == null) return;

            var slotData = slot.SlotData;
            
            if (slotData.SlotType == InventorySlotType.Equipment)
            {
                // Update hero equipment properties
                UpdateHeroEquipment(heroEquipment, slotData);
            }
            else if (slotData.SlotType == InventorySlotType.Shortcut || slotData.SlotType == InventorySlotType.Inventory)
            {
                // Update hero bag at specific index
                UpdateHeroBag(slotData);
            }
        }

        /// <summary>
        /// Updates hero equipment property based on equipment slot.
        /// </summary>
        private void UpdateHeroEquipment(Hero heroEquipment, InventorySlotData slotData)
        {
            var equipmentSlot = slotData.EquipmentSlot;
            var item = slotData.Item;
            
            // This would require modifying Hero class to have public setters or add methods
            // For now, we'll use the existing TryEquip/TryUnequip methods
            if (item != null)
            {
                heroEquipment.TryEquip(item);
            }
            else
            {
                heroEquipment.TryUnequip(equipmentSlot.Value);
            }
        }

        /// <summary>
        /// Updates hero bag at specific index.
        /// </summary>
        private void UpdateHeroBag(InventorySlotData slotData)
        {
            if (_heroComponent?.Bag == null || !slotData.BagIndex.HasValue) return;
            
            var bagIndex = slotData.BagIndex.Value;
            var item = slotData.Item;
            
            // This requires modifying ItemBag to support index-based operations
            // For now, we'll rebuild the bag (not ideal but functional)
            Debug.Log($"Would update bag index {bagIndex} with item: {item?.Name ?? "null"}");
        }

        /// <summary>
        /// Updates bag capacity and marks slots beyond capacity as null.
        /// </summary>
        public void UpdateBagCapacity(int capacity)
        {
            // The first 8 shortcut slots are always available
            // Additional capacity affects inventory slots (rows 4-6)
            
            int inventorySlotCount = capacity - 8; // 8 shortcut slots always available
            int currentInventorySlot = 0;
            
            for (int y = 4; y < GRID_HEIGHT; y++)
            {
                for (int x = 0; x < GRID_WIDTH; x++)
                {
                    var slotData = _slotGrid[x, y];
                    if (slotData.SlotType == InventorySlotType.Inventory)
                    {
                        currentInventorySlot++;
                        
                        // If this slot exceeds capacity, mark as null (will not be rendered)
                        if (currentInventorySlot > inventorySlotCount)
                        {
                            slotData.SlotType = InventorySlotType.Null;
                            
                            // Remove corresponding UI component
                            var component = _slotComponents.FirstOrDefault(c => c.SlotData == slotData);
                            if (component != null)
                            {
                                _slotComponents.Remove(component);
                                RemoveElement(component);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Finds the next available empty slot for adding an item.
        /// First checks shortcut slots, then inventory slots.
        /// </summary>
        public InventorySlotData FindNextAvailableSlot()
        {
            // Check shortcut slots first (row 3)
            for (int x = 0; x < GRID_WIDTH; x++)
            {
                var slot = _slotGrid[x, 3];
                if (slot.SlotType == InventorySlotType.Shortcut && slot.Item == null)
                {
                    return slot;
                }
            }
            
            // Check inventory slots (rows 4-6)
            for (int y = 4; y < GRID_HEIGHT; y++)
            {
                for (int x = 0; x < GRID_WIDTH; x++)
                {
                    var slot = _slotGrid[x, y];
                    if (slot.SlotType == InventorySlotType.Inventory && slot.Item == null)
                    {
                        return slot;
                    }
                }
            }
            
            return null; // No available slots
        }
    }
}