using Microsoft.Xna.Framework;
using Nez;
using Nez.UI;
using RolePlayingFramework.Equipment;

namespace PitHero.UI
{
    /// <summary>
    /// Shared utilities for inventory swap animation and stack absorption.
    /// Selection / click-to-swap state has been removed — drag-and-drop is the only movement model.
    /// </summary>
    public static class InventorySelectionManager
    {
        // Overlay for in-grid and cross-component swap animations (stage-space tween)
        private static SwapAnimationOverlay _swapOverlay;
        private const float CrossComponentSwapDuration = 0.2f; // match in-grid tween duration

        /// <summary>Callback to refresh all components after any inventory mutation.</summary>
        public static System.Action OnInventoryChanged;

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
