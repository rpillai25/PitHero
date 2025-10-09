using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Nez;
using Nez.UI;
using PitHero.ECS.Components;
using RolePlayingFramework.Equipment;
using System.Collections.Generic;

namespace PitHero.UI
{
    /// <summary>UI component displaying shortcut slots (y=3, x=0-7) at bottom center of game HUD.</summary>
    public class ShortcutBar : Group
    {
        private const int SHORTCUT_COUNT = 8;
        private const int SHORTCUT_ROW = 3;
        private const float SLOT_SIZE = 32f;
        private const float SLOT_PADDING = 1f;
        
        private readonly FastList<InventorySlot> _slots;
        private HeroComponent _heroComponent;
        private InventorySlot _highlightedSlot;
        
        // Track scaling for different window modes
        private float _currentScale = 1f;
        
        // Track base position and offset for inventory window
        private float _baseX = 0f;
        private float _baseY = 0f;
        private float _offsetX = 0f;
        
        // Public events for item card display (compatible with InventoryGrid)
        public event System.Action<IItem> OnItemHovered;
        public event System.Action OnItemUnhovered;
        public event System.Action<IItem> OnItemSelected;
        public event System.Action OnItemDeselected;
        
        public ShortcutBar()
        {
            _slots = new FastList<InventorySlot>(SHORTCUT_COUNT);
            BuildSlots();
            LayoutSlots();
        }
        
        /// <summary>Builds shortcut slot components (x=0-7 at y=3).</summary>
        private void BuildSlots()
        {
            for (int x = 0; x < SHORTCUT_COUNT; x++)
            {
                var data = new InventorySlotData(x, SHORTCUT_ROW, InventorySlotType.Shortcut) 
                { 
                    ShortcutKey = x + 1 
                };
                
                var slot = new InventorySlot(data);
                slot.OnSlotClicked += HandleSlotClicked;
                slot.OnSlotDoubleClicked += HandleSlotDoubleClicked;
                slot.OnSlotHovered += HandleSlotHovered;
                slot.OnSlotUnhovered += HandleSlotUnhovered;
                
                _slots.Add(slot);
                AddElement(slot);
            }
        }
        
        /// <summary>Positions slot components based on current scale.</summary>
        private void LayoutSlots()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                var slot = _slots.Buffer[i];
                if (slot == null) continue;
                
                float scaledSlotSize = SLOT_SIZE * _currentScale;
                float scaledPadding = SLOT_PADDING * _currentScale;
                slot.SetPosition(i * (scaledSlotSize + scaledPadding), 0);
                slot.SetScale(_currentScale);
            }
        }
        
        /// <summary>Sets the scale of the shortcut bar (1x for Normal, 2x for Half).</summary>
        public void SetScale(float scale)
        {
            if (System.Math.Abs(_currentScale - scale) < 0.01f)
                return;
                
            _currentScale = scale;
            LayoutSlots();
        }
        
        /// <summary>Sets the base position of the shortcut bar.</summary>
        public void SetBasePosition(float x, float y)
        {
            _baseX = x;
            _baseY = y;
            UpdatePosition();
        }
        
        /// <summary>Sets the horizontal offset (used when inventory is open).</summary>
        public void SetOffsetX(float offsetX)
        {
            _offsetX = offsetX;
            UpdatePosition();
        }
        
        /// <summary>Updates the actual position based on base + offset.</summary>
        private void UpdatePosition()
        {
            SetPosition(_baseX + _offsetX, _baseY);
        }
        
        /// <summary>Connects shortcut bar to hero and loads items.</summary>
        public void ConnectToHero(HeroComponent heroComponent)
        {
            _heroComponent = heroComponent;
            UpdateItemsFromBag();
            
            // Subscribe to cross-component inventory changes
            InventorySelectionManager.OnInventoryChanged += UpdateItemsFromBag;
        }
        
        /// <summary>Updates slot items from hero's shortcut bag.</summary>
        private void UpdateItemsFromBag()
        {
            if (_heroComponent?.ShortcutBag == null)
                return;
                
            var bag = _heroComponent.ShortcutBag;
            
            // The 8 items in the shortcut bag correspond to the 8 shortcut slots
            for (int i = 0; i < _slots.Length; i++)
            {
                var slot = _slots.Buffer[i];
                if (slot != null)
                {
                    slot.SlotData.BagIndex = i;
                    slot.SlotData.Item = bag.GetSlotItem(i);
                }
            }
        }
        
        /// <summary>Handles slot click highlighting and swapping.</summary>
        private void HandleSlotClicked(InventorySlot clickedSlot)
        {
            // Check if there's a cross-component selection (from InventoryGrid)
            if (InventorySelectionManager.HasSelection() && !InventorySelectionManager.IsSelectionFromShortcutBar())
            {
                // Attempt cross-component swap
                if (InventorySelectionManager.TrySwapCrossComponent(clickedSlot, true, _heroComponent))
                {
                    // Refresh is handled by callback
                    OnItemDeselected?.Invoke();
                    return;
                }
            }
            
            if (_highlightedSlot == null)
            {
                _highlightedSlot = clickedSlot;
                clickedSlot.SlotData.IsHighlighted = true;
                InventorySelectionManager.SetSelectedFromShortcut(clickedSlot, _heroComponent);
                if (clickedSlot.SlotData.Item != null)
                    OnItemSelected?.Invoke(clickedSlot.SlotData.Item);
            }
            else if (_highlightedSlot == clickedSlot)
            {
                _highlightedSlot.SlotData.IsHighlighted = false;
                _highlightedSlot = null;
                InventorySelectionManager.ClearSelection();
                OnItemDeselected?.Invoke();
            }
            else
            {
                SwapSlotItems(_highlightedSlot, clickedSlot);
                _highlightedSlot.SlotData.IsHighlighted = false;
                _highlightedSlot = null;
                InventorySelectionManager.ClearSelection();
                OnItemDeselected?.Invoke();
            }
        }
        
        /// <summary>Handles double-click to use consumables.</summary>
        private void HandleSlotDoubleClicked(InventorySlot slot)
        {
            if (slot.SlotData.Item == null || !slot.SlotData.BagIndex.HasValue)
                return;
                
            var item = slot.SlotData.Item;
            var bagIndex = slot.SlotData.BagIndex.Value;
            
            // Only consumables can be used from shortcut bar
            if (item is Consumable)
            {
                UseConsumable(item, bagIndex);
            }
        }
        
        private void HandleSlotHovered(InventorySlot slot)
        {
            if (slot.SlotData.Item != null)
                OnItemHovered?.Invoke(slot.SlotData.Item);
        }
        
        private void HandleSlotUnhovered(InventorySlot slot)
        {
            OnItemUnhovered?.Invoke();
        }
        
        /// <summary>Swaps items between two shortcut slots.</summary>
        private void SwapSlotItems(InventorySlot slotA, InventorySlot slotB)
        {
            if (_heroComponent?.ShortcutBag == null)
                return;
                
            var bag = _heroComponent.ShortcutBag;
            var dataA = slotA.SlotData;
            var dataB = slotB.SlotData;
            
            // Get bag indices
            int? bagIndexA = dataA.BagIndex;
            int? bagIndexB = dataB.BagIndex;
            
            // Swap in bag using bag's API
            if (bagIndexA.HasValue && bagIndexB.HasValue)
            {
                bag.SwapSlots(bagIndexA.Value, bagIndexB.Value);
            }
            
            // Update display
            UpdateItemsFromBag();
        }
        
        /// <summary>Uses a consumable item.</summary>
        private void UseConsumable(IItem item, int bagIndex)
        {
            if (item is not Consumable consumable)
                return;
            
            var hero = _heroComponent?.LinkedHero;
            if (hero == null)
            {
                Debug.Log($"[ShortcutBar] Cannot use {item.Name}: No hero linked");
                return;
            }

            // Try to consume the item
            if (consumable.Consume(hero))
            {
                Debug.Log($"[ShortcutBar] Used {item.Name}");
                
                // Decrement stack or remove item from shortcut bag
                if (_heroComponent.ShortcutBag.ConsumeFromStack(bagIndex))
                {
                    // Refresh the UI to show updated stack counts
                    UpdateItemsFromBag();
                }
            }
            else
            {
                Debug.Log($"[ShortcutBar] Failed to use {item.Name}");
            }
        }
        
        /// <summary>Public method to refresh items from bag (called externally when inventory changes).</summary>
        public void RefreshItems()
        {
            UpdateItemsFromBag();
        }
        
        /// <summary>Handles shortcut key presses (1-8).</summary>
        public void HandleKeyboardShortcuts()
        {
            for (int keyOffset = 0; keyOffset < SHORTCUT_COUNT; keyOffset++)
            {
                var key = (Keys)((int)Keys.D1 + keyOffset);
                if (!Input.IsKeyPressed(key)) continue;
                
                var slot = _slots.Buffer[keyOffset];
                if (slot == null) continue;
                
                var data = slot.SlotData;
                if (data.Item != null && data.BagIndex.HasValue)
                {
                    Debug.Log($"[ShortcutBar] Activated shortcut slot {data.ShortcutKey} with item: {data.Item.Name}");
                    
                    // Use the consumable if it's a consumable
                    if (data.Item is Consumable)
                    {
                        UseConsumable(data.Item, data.BagIndex.Value);
                    }
                    break;
                }
            }
        }
    }
}
