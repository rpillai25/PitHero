using Microsoft.Xna.Framework;
using Nez;
using Nez.UI;
using PitHero.Services;

namespace PitHero.UI
{
    /// <summary>A 9x6 scrollable grid showing items from the Second Chance Merchant Vault.</summary>
    public class VaultItemGrid : Group
    {
        private const int COLS = 9;
        private const int ROWS = 6;
        private const int MAX_VISIBLE = COLS * ROWS; // 54
        private const float SLOT_SIZE = 32f;
        private const float SLOT_PAD = 1f;

        private readonly VaultItemSlot[] _slots;
        private Stage _tooltipStage;
        private ItemCardTooltip _tooltip;
        private int _hoverCheckFrame;

        /// <summary>Fired when a vault slot drag begins.</summary>
        public event System.Action<VaultItemSlot> OnVaultSlotDragStarted;

        /// <summary>Fired when a vault slot drag is dropped. The Vector2 is the stage-coordinate drop position.</summary>
        public event System.Action<VaultItemSlot, Vector2> OnVaultSlotDragDropped;

        /// <summary>Creates a new vault item grid.</summary>
        public VaultItemGrid()
        {
            _slots = new VaultItemSlot[MAX_VISIBLE];
            for (int i = 0; i < MAX_VISIBLE; i++)
            {
                var slot = new VaultItemSlot();
                _slots[i] = slot;
                slot.OnSlotHovered += HandleSlotHovered;
                slot.OnSlotUnhovered += HandleSlotUnhovered;
                slot.OnDragStarted += HandleSlotDragStarted;
                slot.OnDragMoved += HandleSlotDragMoved;
                slot.OnDragDropped += HandleSlotDragDropped;
                AddElement(slot);
            }
            LayoutSlots();
            SetSize(COLS * (SLOT_SIZE + SLOT_PAD), ROWS * (SLOT_SIZE + SLOT_PAD));
        }

        /// <summary>Initializes the tooltip for item hover display.</summary>
        public void InitializeTooltip(Stage stage, Skin skin)
        {
            _tooltipStage = stage;
            var dummyTarget = new Element();
            dummyTarget.SetSize(0, 0);
            _tooltip = new ItemCardTooltip(dummyTarget, skin);
        }

        /// <summary>
        /// Refreshes the grid from the vault, displaying the 54 stacks on the given page.
        /// <paramref name="pageIndex"/> is zero-based; stacks shown are at indices
        /// [pageIndex * MAX_VISIBLE … pageIndex * MAX_VISIBLE + MAX_VISIBLE).
        /// </summary>
        public void RefreshFromVault(SecondChanceMerchantVault vault, int pageIndex = 0)
        {
            var stacks = vault?.Stacks;
            int count = stacks != null ? stacks.Count : 0;
            int offset = pageIndex * MAX_VISIBLE;
            for (int i = 0; i < MAX_VISIBLE; i++)
            {
                int stackIdx = offset + i;
                if (stackIdx < count)
                    _slots[i].SetStack(stacks[stackIdx]);
                else
                    _slots[i].SetStack(null);
            }
        }

        /// <summary>Shows the item sprite in all slots (called after a cancelled drag).</summary>
        public void ShowAllItemSprites()
        {
            for (int i = 0; i < MAX_VISIBLE; i++)
                _slots[i].SetItemSpriteHidden(false);
        }

        private void LayoutSlots()
        {
            for (int i = 0; i < MAX_VISIBLE; i++)
            {
                int col = i % COLS;
                int row = i / COLS;
                _slots[i].SetPosition(col * (SLOT_SIZE + SLOT_PAD), row * (SLOT_SIZE + SLOT_PAD));
            }
        }

        /// <summary>Called every frame by SecondChanceShopUI to run periodic hover checks for missed hover events.</summary>
        public void Update(Vector2 mouseStagePos)
        {
            _hoverCheckFrame++;
            if (_hoverCheckFrame % 5 != 0) return;

            // No hover cards while an item is in hand or a buy/sell prompt is up — otherwise this
            // probe re-shows a card over the dialog every few frames.
            if (InventoryDragManager.DragBlocked) return;

            if (_tooltip != null && _tooltip.GetContainer().HasParent()) return;

            for (int i = 0; i < MAX_VISIBLE; i++)
            {
                var slot = _slots[i];
                if (slot == null || slot.Stack?.ItemTemplate == null) continue;
                var topLeft = slot.LocalToStageCoordinates(Vector2.Zero);
                if (mouseStagePos.X >= topLeft.X && mouseStagePos.X <= topLeft.X + slot.GetWidth() &&
                    mouseStagePos.Y >= topLeft.Y && mouseStagePos.Y <= topLeft.Y + slot.GetHeight())
                {
                    HandleSlotHovered(slot);
                    return;
                }
            }
        }

        private void HandleSlotHovered(VaultItemSlot slot)
        {
            if (slot.Stack?.ItemTemplate == null || _tooltip == null || _tooltipStage == null)
                return;
            // Suppressed while dragging or while a buy/sell prompt is open — the prompt carries the
            // item's details itself, and a floating card over it just gets in the way.
            if (InventoryDragManager.DragBlocked)
                return;

            _tooltip.ShowItem(slot.Stack.ItemTemplate, showBuyPrice: true);
            var container = _tooltip.GetContainer();
            if (container.GetParent() == null)
                _tooltipStage.AddElement(container);

            // Position tooltip at cursor (same pattern as HeroUI.HandleItemHovered)
            container.Validate();
            var mousePos = _tooltipStage.GetMousePosition();
            float tx = mousePos.X + 10f;
            float ty = mousePos.Y + 10f;
            float stageH = _tooltipStage.GetHeight();
            float stageW = _tooltipStage.GetWidth();
            if (ty + container.GetHeight() > stageH)
                ty = stageH - container.GetHeight();
            if (ty < 0) ty = 0;
            if (tx + container.GetWidth() > stageW)
                tx = mousePos.X - container.GetWidth() - 10f;
            container.SetPosition(tx, ty);
            container.ToFront();
        }

        private void HandleSlotUnhovered(VaultItemSlot slot)
        {
            _tooltip?.GetContainer().Remove();
        }

        private void HandleSlotDragStarted(VaultItemSlot slot, Vector2 pos)
        {
            if (slot.Stack == null) return;
            // Drop the gesture entirely while a buy/sell confirmation is up — hiding the sprite for a
            // drag that BeginVaultItemDrag will refuse leaves the slot looking empty with nothing in hand.
            if (InventoryDragManager.DragBlocked) return;
            slot.SetItemSpriteHidden(true);
            var stagePos = slot.LocalToStageCoordinates(pos);
            InventoryDragManager.BeginVaultItemDrag(slot.Stack, _tooltipStage);
            InventoryDragManager.UpdateDrag(stagePos);
            // The card no longer rides along with the dragged item: it would still be up when the
            // buy prompt opens, covering it. The prompt shows the item's details instead.
            _tooltip?.GetContainer().Remove();
            OnVaultSlotDragStarted?.Invoke(slot);
        }

        private void HandleSlotDragMoved(VaultItemSlot slot, Vector2 pos)
        {
            var stagePos = slot.LocalToStageCoordinates(pos);
            InventoryDragManager.UpdateDrag(stagePos);
        }

        private void HandleSlotDragDropped(VaultItemSlot slot, Vector2 pos)
        {
            var stagePos = slot.LocalToStageCoordinates(pos);
            InventoryDragManager.UpdateDrag(stagePos);
            _tooltip?.GetContainer().Remove();
            OnVaultSlotDragDropped?.Invoke(slot, stagePos);
        }

    }
}
