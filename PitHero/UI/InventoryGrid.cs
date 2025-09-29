using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Nez;
using Nez.UI;
using RolePlayingFramework.Equipment;
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
        /// Updates the displayed items based on the hero's current bag contents.
        /// </summary>
        public void UpdateItemsFromBag()
        {
            if (_heroComponent?.Bag == null) return;

            // Clear all current items
            foreach (var slot in _slotComponents)
            {
                slot.SlotData.Item = null;
            }

            // Populate items from bag into available slots
            var bagItems = _heroComponent.Bag.Items;
            var availableSlots = _slotComponents
                .Where(s => s.SlotData.SlotType == InventorySlotType.Shortcut || 
                           s.SlotData.SlotType == InventorySlotType.Inventory)
                .OrderBy(s => s.SlotData.Y)
                .ThenBy(s => s.SlotData.X)
                .ToList();

            for (int i = 0; i < bagItems.Count && i < availableSlots.Count; i++)
            {
                availableSlots[i].SlotData.Item = bagItems[i];
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
            // Swap items between the two slots
            var tempItem = slot1.SlotData.Item;
            slot1.SlotData.Item = slot2.SlotData.Item;
            slot2.SlotData.Item = tempItem;
            
            // TODO: Validate item can be placed in target slot type (equipment restrictions etc.)
            // TODO: Update actual hero inventory/equipment data
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