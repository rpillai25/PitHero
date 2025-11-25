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
        private static int _selectedShortcutIndex = -1; // For shortcut bar references

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
            _selectedShortcutIndex = -1;
            _heroComponent = hero;
            if (slot != null)
                slot.SlotData.IsHighlighted = true;
        }
        
        /// <summary>Sets the selected slot from shortcut bar (now just an index, not a slot with items)</summary>
        public static void SetSelectedFromShortcut(int shortcutIndex, HeroComponent hero)
        {
            ClearSelectionInternal();
            _selectedSlot = null; // Shortcut bar doesn't have real slots, just references
            _isFromShortcutBar = true;
            _selectedShortcutIndex = shortcutIndex;
            _heroComponent = hero;
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
            _selectedShortcutIndex = -1;
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
        public static bool HasSelection() => _selectedSlot != null || _selectedShortcutIndex >= 0;

        /// <summary>Returns true if two slots can perform stack absorption and outputs amount to absorb.</summary>
        public static bool CanAbsorbStacks(InventorySlot source, InventorySlot target, out int toAbsorb)
        {
            toAbsorb = 0;
            if (source?.SlotData?.Item is not Consumable src || target?.SlotData?.Item is not Consumable dst)
                return false;
            if (!string.Equals(src.Name, dst.Name, System.StringComparison.Ordinal))
                return false;
            if (dst.StackCount >= dst.StackSize)
                return false;
            int space = dst.StackSize - dst.StackCount;
            if (space <= 0 || src.StackCount <= 0)
                return false;
            toAbsorb = System.Math.Min(space, src.StackCount);
            return toAbsorb > 0;
        }

        /// <summary>Applies absorption from source to target using the specified amount. Clears source if empty.</summary>
        public static void PerformAbsorbStacks(InventorySlot source, InventorySlot target, int toAbsorb)
        {
            if (toAbsorb <= 0) return;
            var src = (Consumable)source.SlotData.Item;
            var dst = (Consumable)target.SlotData.Item;
            dst.StackCount += toAbsorb;
            src.StackCount -= toAbsorb;
            if (src.StackCount <= 0)
            {
                source.SlotData.Item = null;
            }
        }

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
    }
}
