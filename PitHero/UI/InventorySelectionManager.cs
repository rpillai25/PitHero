using Microsoft.Xna.Framework;
using Nez;
using Nez.Textures;
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

        // Overlay for cross-component and in-grid swap animations (stage-space tween)
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

        /// <summary>Animates a swap between two UI slots in stage space using a shared overlay.</summary>
        public static bool TryAnimateSwap(InventorySlot slotA, InventorySlot slotB, float durationSeconds, System.Action onCompleted = null)
        {
            if (slotA == null || slotB == null)
            {
                onCompleted?.Invoke();
                return false;
            }

            var itemA = slotA.SlotData.Item;
            var itemB = slotB.SlotData.Item;
            if (itemA == null && itemB == null)
            {
                onCompleted?.Invoke();
                return false;
            }

            if (Core.Content == null)
            {
                onCompleted?.Invoke();
                return false;
            }

            // Load sprites
            SpriteDrawable drawableA = null;
            SpriteDrawable drawableB = null;
            try
            {
                var itemsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/Items.atlas");
                if (itemA != null)
                {
                    var sA = itemsAtlas.GetSprite(itemA.Name);
                    if (sA != null) drawableA = new SpriteDrawable(sA);
                }
                if (itemB != null)
                {
                    var sB = itemsAtlas.GetSprite(itemB.Name);
                    if (sB != null) drawableB = new SpriteDrawable(sB);
                }
            }
            catch
            {
                onCompleted?.Invoke();
                return false;
            }

            if (drawableA == null && drawableB == null)
            {
                onCompleted?.Invoke();
                return false;
            }

            // Ensure same stage and get stage-space coords
            var stageA = slotA.GetStage();
            var stageB = slotB.GetStage();
            if (stageA == null || stageB == null || stageA != stageB)
            {
                onCompleted?.Invoke();
                return false;
            }

            var startA = slotA.LocalToStageCoordinates(Vector2.Zero);
            var endA = slotB.LocalToStageCoordinates(Vector2.Zero);
            var startB = endA;
            var endB = startA;

            // Reset hover offsets and hide originals during tween
            slotA.SetItemSpriteOffsetY(0f);
            slotB.SetItemSpriteOffsetY(0f);
            if (itemA != null) slotA.SetItemSpriteHidden(true);
            if (itemB != null) slotB.SetItemSpriteHidden(true);

            // Ensure overlay is attached to the proper stage
            if (_swapOverlay == null)
            {
                _swapOverlay = new SwapAnimationOverlay();
                stageA.AddElement(_swapOverlay);
                _swapOverlay.SetVisible(false);
            }
            else
            {
                // If overlay is not on this stage, move it
                if (_swapOverlay.GetStage() != stageA)
                {
                    _swapOverlay.Remove();
                    stageA.AddElement(_swapOverlay);
                    _swapOverlay.SetVisible(false);
                }
            }

            _swapOverlay.ToFront();

            _swapOverlay.Begin(
                drawableA,
                startA,
                endA,
                drawableB,
                startB,
                endB,
                durationSeconds <= 0f ? CrossComponentSwapDuration : durationSeconds,
                () =>
                {
                    // Unhide originals now in their new positions
                    slotA.SetItemSpriteHidden(false);
                    slotB.SetItemSpriteHidden(false);
                    onCompleted?.Invoke();
                }
            );

            return true;
        }
        
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

            // Check if both items are consumables of the same type and can stack (absorption logic)
            // Source is from inventory, target is shortcut (or vice versa)
            // For cross-component, we want to absorb the source into the target
            IItem sourceItem = _isFromShortcutBar ? shortcutItem : inventoryItem;
            IItem targetItem = _isFromShortcutBar ? inventoryItem : shortcutItem;
            int sourceBagIndex = _isFromShortcutBar ? shortcutBagIndex : inventoryBagIndex;
            int targetBagIndex = _isFromShortcutBar ? inventoryBagIndex : shortcutBagIndex;
            var sourceBag = _isFromShortcutBar ? _heroComponent.ShortcutBag : _heroComponent.Bag;
            var targetBag = _isFromShortcutBar ? _heroComponent.Bag : _heroComponent.ShortcutBag;

            if (sourceItem is Consumable sourceConsumable && targetItem is Consumable targetConsumable &&
                sourceConsumable.Name == targetConsumable.Name && targetConsumable.StackCount < targetConsumable.StackSize)
            {
                // Calculate how much can be absorbed into target
                int availableSpace = targetConsumable.StackSize - targetConsumable.StackCount;
                int toAbsorb = System.Math.Min(availableSpace, sourceConsumable.StackCount);
                
                // Try to animate via overlay
                bool animated = TryAnimateSwap(inventorySlot, shortcutSlot, CrossComponentSwapDuration, () =>
                {
                    // Transfer items from source to target
                    targetConsumable.StackCount += toAbsorb;
                    sourceConsumable.StackCount -= toAbsorb;
                    
                    // If source is depleted, clear it
                    if (sourceConsumable.StackCount <= 0)
                    {
                        sourceBag.SetSlotItem(sourceBagIndex, null);
                    }

                    ClearSelection();
                    OnInventoryChanged?.Invoke();
                });

                if (!animated)
                {
                    // Fallback: instant absorption
                    targetConsumable.StackCount += toAbsorb;
                    sourceConsumable.StackCount -= toAbsorb;
                    
                    if (sourceConsumable.StackCount <= 0)
                    {
                        sourceBag.SetSlotItem(sourceBagIndex, null);
                    }
                    
                    ClearSelection();
                    OnInventoryChanged?.Invoke();
                }

                return true;
            }

            // Regular swap if not absorbing
            // Try to animate via overlay. If we can't, fall back to instant swap
            bool animatedSwap = TryAnimateSwap(inventorySlot, shortcutSlot, CrossComponentSwapDuration, () =>
            {
                // perform logical swap after animation
                _heroComponent.Bag.SetSlotItem(inventoryBagIndex, shortcutItem);
                _heroComponent.ShortcutBag.SetSlotItem(shortcutBagIndex, inventoryItem);

                ClearSelection();
                OnInventoryChanged?.Invoke();
            });

            if (!animatedSwap)
            {
                // Fallback: no stage or sprites. Do instant swap
                _heroComponent.Bag.SetSlotItem(inventoryBagIndex, shortcutItem);
                _heroComponent.ShortcutBag.SetSlotItem(shortcutBagIndex, inventoryItem);
                ClearSelection();
                OnInventoryChanged?.Invoke();
            }

            return true;
        }
    }
}
