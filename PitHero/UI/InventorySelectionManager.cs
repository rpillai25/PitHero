using Microsoft.Xna.Framework;
using Nez;
using Nez.UI;
using PitHero.ECS.Components;
using RolePlayingFramework.Equipment;

namespace PitHero.UI
{
    /// <summary>
    /// Manages selection and swapping between InventoryGrid and ShortcutBar
    /// </summary>
    public static class InventorySelectionManager
    {
        private static InventorySlot _selectedSlot;
        private static bool _isFromShortcutBar;
        private static HeroComponent _heroComponent;

        // Overlay for cross-component swap animations (stage-space tween)
        private static SwapAnimationOverlay _swapOverlay;
        private const float CrossComponentSwapDuration = 0.2f; // match in-grid tween duration
        
        /// <summary>Callback to refresh inventory grid after cross-component swap</summary>
        public static System.Action OnInventoryChanged;
        
        /// <summary>Callback to clear local selection state in all components</summary>
        public static System.Action OnSelectionCleared;
        
        /// <summary>Callback to animate cross-component swap (slotA, slotB)</summary>
        public static System.Action<InventorySlot, InventorySlot> OnCrossComponentSwapAnimate;
        
        /// <summary>Sets the selected slot from inventory grid</summary>
        public static void SetSelectedFromInventory(InventorySlot slot, HeroComponent hero)
        {
            ClearSelectionInternal();
            _selectedSlot = slot;
            _isFromShortcutBar = false;
            _heroComponent = hero;
            if (slot != null)
                slot.SlotData.IsHighlighted = true;
        }
        
        /// <summary>Sets the selected slot from shortcut bar</summary>
        public static void SetSelectedFromShortcut(InventorySlot slot, HeroComponent hero)
        {
            ClearSelectionInternal();
            _selectedSlot = slot;
            _isFromShortcutBar = true;
            _heroComponent = hero;
            if (slot != null)
                slot.SlotData.IsHighlighted = true;
        }
        
        /// <summary>Internal method to clear selection without triggering callbacks (used when switching selections)</summary>
        private static void ClearSelectionInternal()
        {
            if (_selectedSlot != null)
            {
                _selectedSlot.SlotData.IsHighlighted = false;
                _selectedSlot = null;
            }
            _isFromShortcutBar = false;
            _heroComponent = null;
        }
        
        /// <summary>Clears the current selection</summary>
        public static void ClearSelection()
        {
            ClearSelectionInternal();
            
            // Notify all components to clear their local state
            OnSelectionCleared?.Invoke();
        }
        
        /// <summary>Gets the currently selected slot</summary>
        public static InventorySlot GetSelectedSlot() => _selectedSlot;
        
        /// <summary>Returns true if the selected slot is from shortcut bar</summary>
        public static bool IsSelectionFromShortcutBar() => _isFromShortcutBar;
        
        /// <summary>Returns true if there is a selected slot</summary>
        public static bool HasSelection() => _selectedSlot != null;
        
        /// <summary>Attempts to swap between inventory and shortcut slot with animation</summary>
        public static bool TrySwapCrossComponent(InventorySlot targetSlot, bool targetIsShortcut, HeroComponent targetHero)
        {
            if (_selectedSlot == null || _heroComponent == null)
                return false;
                
            // Only allow swap if they're from different components
            if (_isFromShortcutBar == targetIsShortcut)
                return false;
                
            // Determine which is inventory and which is shortcut
            InventorySlot inventorySlot = _isFromShortcutBar ? targetSlot : _selectedSlot;
            InventorySlot shortcutSlot = _isFromShortcutBar ? _selectedSlot : targetSlot;
            
            if (!inventorySlot.SlotData.BagIndex.HasValue || !shortcutSlot.SlotData.BagIndex.HasValue)
                return false;
                
            // Capture items and indices before animating
            IItem inventoryItem = inventorySlot.SlotData.Item;
            IItem shortcutItem = shortcutSlot.SlotData.Item;
            int inventoryBagIndex = inventorySlot.SlotData.BagIndex.Value;
            int shortcutBagIndex = shortcutSlot.SlotData.BagIndex.Value;

            // If no item movement is visible (both empty), no-op
            if (inventoryItem == null && shortcutItem == null)
                return true;

            // Attempt to load sprites for visible animation
            SpriteDrawable drawableA = null;
            SpriteDrawable drawableB = null;
            try
            {
                if (Core.Content != null)
                {
                    var itemsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/Items.atlas");
                    if (inventoryItem != null)
                    {
                        var sA = itemsAtlas.GetSprite(inventoryItem.Name);
                        if (sA != null)
                            drawableA = new SpriteDrawable(sA);
                    }
                    if (shortcutItem != null)
                    {
                        var sB = itemsAtlas.GetSprite(shortcutItem.Name);
                        if (sB != null)
                            drawableB = new SpriteDrawable(sB);
                    }
                }
            }
            catch
            {
                // ignore atlas errors and fall back to instant swap
            }

            // Compute stage-space positions for tween
            var stageA = inventorySlot.GetStage();
            var stageB = shortcutSlot.GetStage();
            if (stageA == null || stageB == null || stageA != stageB || (drawableA == null && drawableB == null))
            {
                // Fallback: no stage or sprites. Do instant swap
                _heroComponent.Bag.SetSlotItem(inventoryBagIndex, shortcutItem);
                _heroComponent.ShortcutBag.SetSlotItem(shortcutBagIndex, inventoryItem);
                ClearSelection();
                OnInventoryChanged?.Invoke();
                return true;
            }

            // Reset hover offsets and hide originals during tween
            _selectedSlot.SetItemSpriteOffsetY(0f);
            targetSlot.SetItemSpriteOffsetY(0f);
            if (inventoryItem != null) inventorySlot.SetItemSpriteHidden(true);
            if (shortcutItem != null) shortcutSlot.SetItemSpriteHidden(true);

            // Convert local to stage coordinates (top-left of slots)
            var startA = inventorySlot.LocalToStageCoordinates(Vector2.Zero);
            var endA = shortcutSlot.LocalToStageCoordinates(Vector2.Zero);
            var startB = endA;
            var endB = startA;

            // Ensure overlay exists on stage and is on top
            if (_swapOverlay == null)
            {
                _swapOverlay = new SwapAnimationOverlay();
                stageA.AddElement(_swapOverlay);
                _swapOverlay.SetVisible(false);
            }
            _swapOverlay.ToFront();

            // Begin tween in overlay. On completion do the actual data swap and refresh
            _swapOverlay.Begin(
                drawableA,
                startA,
                endA,
                drawableB,
                startB,
                endB,
                CrossComponentSwapDuration,
                () =>
                {
                    // Perform logical swap after animation
                    _heroComponent.Bag.SetSlotItem(inventoryBagIndex, shortcutItem);
                    _heroComponent.ShortcutBag.SetSlotItem(shortcutBagIndex, inventoryItem);

                    // Unhide sprites (UI refresh will also reset hidden state)
                    inventorySlot.SetItemSpriteHidden(false);
                    shortcutSlot.SetItemSpriteHidden(false);

                    ClearSelection();
                    OnInventoryChanged?.Invoke();
                }
            );

            return true;
        }
    }
}
