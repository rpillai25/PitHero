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

        /// <summary>Refreshes the grid from the vault, displaying up to 54 stacks.</summary>
        public void RefreshFromVault(SecondChanceMerchantVault vault)
        {
            var stacks = vault?.Stacks;
            int count = stacks != null ? stacks.Count : 0;
            for (int i = 0; i < MAX_VISIBLE; i++)
            {
                if (i < count)
                    _slots[i].SetStack(stacks[i]);
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
            if (InventoryDragManager.IsVaultItemDrag) return;
            _tooltip?.GetContainer().Remove();
        }

        private void HandleSlotDragStarted(VaultItemSlot slot, Vector2 pos)
        {
            if (slot.Stack == null) return;
            slot.SetItemSpriteHidden(true);
            var stagePos = slot.LocalToStageCoordinates(pos);
            InventoryDragManager.BeginVaultItemDrag(slot.Stack, _tooltipStage);
            InventoryDragManager.UpdateDrag(stagePos);
            PositionTooltipAtStagePos(stagePos);
            OnVaultSlotDragStarted?.Invoke(slot);
        }

        private void HandleSlotDragMoved(VaultItemSlot slot, Vector2 pos)
        {
            var stagePos = slot.LocalToStageCoordinates(pos);
            InventoryDragManager.UpdateDrag(stagePos);
            PositionTooltipAtStagePos(stagePos);
        }

        private void HandleSlotDragDropped(VaultItemSlot slot, Vector2 pos)
        {
            var stagePos = slot.LocalToStageCoordinates(pos);
            InventoryDragManager.UpdateDrag(stagePos);
            _tooltip?.GetContainer().Remove();
            OnVaultSlotDragDropped?.Invoke(slot, stagePos);
        }

        /// <summary>Positions the tooltip container near the given stage position (cursor).</summary>
        private void PositionTooltipAtStagePos(Vector2 stagePos)
        {
            if (_tooltip == null || _tooltipStage == null) return;
            var container = _tooltip.GetContainer();
            if (container.GetParent() == null)
                _tooltipStage.AddElement(container);
            container.Validate();
            float stageW = _tooltipStage.GetWidth();
            float stageH = _tooltipStage.GetHeight();
            float tx = stagePos.X + 12f;
            float ty = stagePos.Y + 12f;
            if (ty + container.GetHeight() > stageH)
                ty = stageH - container.GetHeight();
            if (ty < 0) ty = 0;
            if (tx + container.GetWidth() > stageW)
                tx = stagePos.X - container.GetWidth() - 12f;
            container.SetPosition(tx, ty);
        }
    }
}
