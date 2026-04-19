using Microsoft.Xna.Framework;
using Nez;
using Nez.UI;
using PitHero.Services;
using RolePlayingFramework.Heroes;

namespace PitHero.UI
{
    /// <summary>
    /// Displays the hero's crystal inventory and queue for the Second Chance Shop.
    /// Forge slots are omitted — they are not needed in the buy-back flow.
    /// Accepts vault crystal drops for purchase.
    /// </summary>
    public class SecondChanceHeroCrystalPanel
    {
        private const int INV_COLS = 8;
        private const int INV_ROWS = 5;
        private const int INV_TOTAL = INV_COLS * INV_ROWS; // 40
        private const int QUEUE_SLOTS = 5;
        private const float SLOT_SIZE = 32f;
        private const float SLOT_PAD = 1f;

        // Pre-allocated slot number strings to avoid dynamic allocation (AOT compliance)
        private static readonly string[] QueueSlotNumbers = { "1", "2", "3", "4", "5" };

        private CrystalSlotElement[] _inventorySlots;
        private CrystalSlotElement[] _queueSlots;

        private Stage _stage;
        private Skin _skin;
        private CrystalCollectionService _crystalService;

        // Hover tooltip
        private Window _hoverTooltip;
        private Label _hoverLabel;

        /// <summary>Fired when a vault crystal is dropped on a valid empty slot.</summary>
        public event System.Action<CrystalSlotType, int, HeroCrystal> OnVaultCrystalDropRequested;

        /// <summary>Creates the panel content and returns the root Table.</summary>
        public Table CreateContent(Skin skin, Stage stage)
        {
            _skin = skin;
            _stage = stage;
            _crystalService = Core.Services?.GetService<CrystalCollectionService>();

            _hoverTooltip = new Window("", skin);
            _hoverTooltip.SetMovable(false);
            _hoverTooltip.SetResizable(false);
            _hoverTooltip.SetKeepWithinStage(false);
            _hoverTooltip.SetColor(GameConfig.TransparentMenu);
            _hoverLabel = new Label("", skin, "ph-default");
            _hoverTooltip.Add(_hoverLabel).Pad(4f);
            _hoverTooltip.Pack();
            _hoverTooltip.SetVisible(false);

            var mainTable = new Table();
            mainTable.Top().Left().PadLeft(4f);

            // ── Inventory section (left column) ─────────────────────────────────
            var invCol = new Table();
            invCol.Add(new Label("Crystal Inventory:", skin, "ph-default")).Left().Pad(5);
            invCol.Row();

            _inventorySlots = new CrystalSlotElement[INV_TOTAL];
            var invGrid = new Table();
            for (int i = 0; i < INV_TOTAL; i++)
            {
                var slot = new CrystalSlotElement(CrystalSlotKind.Inventory);
                int idx = i;
                slot.OnSlotHovered += OnSlotHovered;
                slot.OnSlotUnhovered += OnSlotUnhovered;
                slot.OnDragDropped += (s, pos) => HandleHeroSlotDrop(CrystalSlotType.Inventory, idx, s);
                _inventorySlots[i] = slot;
                invGrid.Add(slot).Size(SLOT_SIZE).Pad(SLOT_PAD);
                if ((i + 1) % INV_COLS == 0) invGrid.Row();
            }
            invCol.Add(invGrid).Left().Pad(2);

            // ── Queue section (right column, slots stacked vertically) ─────────
            var queueCol = new Table();
            queueCol.Add(new Label("Queue:", skin, "ph-default")).Left().Pad(5);
            queueCol.Row();

            _queueSlots = new CrystalSlotElement[QUEUE_SLOTS];
            for (int i = 0; i < QUEUE_SLOTS; i++)
            {
                var slot = new CrystalSlotElement(CrystalSlotKind.Shortcut);
                int idx = i;
                slot.OnSlotHovered += OnSlotHovered;
                slot.OnSlotUnhovered += OnSlotUnhovered;
                slot.OnDragDropped += (s, pos) => HandleHeroSlotDrop(CrystalSlotType.Queue, idx, s);
                _queueSlots[i] = slot;
                var numLabel = new Label(QueueSlotNumbers[i], skin, "ph-default");
                var slotRow = new Table();
                slotRow.Add(numLabel).Width(14f).Right().Pad(0, 0, 0, 3);
                slotRow.Add(slot).Size(SLOT_SIZE).Pad(SLOT_PAD);
                queueCol.Add(slotRow).Left();
                queueCol.Row();
            }

            // Side-by-side: inventory left, queue right (mirrors CrystalsTab layout)
            mainTable.Add(invCol).Top().Left().Pad(2);
            mainTable.Add(queueCol).Top().Left().Pad(2, 16, 2, 5);

            RefreshAll();
            return mainTable;
        }

        /// <summary>Refreshes all slots from CrystalCollectionService.</summary>
        public void RefreshAll()
        {
            var svc = _crystalService;
            if (svc == null) return;

            for (int i = 0; i < INV_TOTAL; i++)
                _inventorySlots[i].SetCrystal(svc.Inventory[i]);
            for (int i = 0; i < QUEUE_SLOTS; i++)
                _queueSlots[i].SetCrystal(svc.Queue[i]);
        }

        /// <summary>
        /// Finds a crystal slot at the given stage position.
        /// Returns true and sets slotType, slotIdx, and slot if a slot was found.
        /// Returns false if no slot is under the position.
        /// </summary>
        public bool TryGetCrystalSlotAtStagePosition(Vector2 stagePos,
            out CrystalSlotType slotType, out int slotIdx, out CrystalSlotElement slot)
        {
            if (_inventorySlots != null)
            {
                for (int i = 0; i < INV_TOTAL; i++)
                {
                    var s = _inventorySlots[i];
                    if (s == null) continue;
                    var topLeft = s.LocalToStageCoordinates(Vector2.Zero);
                    if (stagePos.X >= topLeft.X && stagePos.X <= topLeft.X + s.GetWidth() &&
                        stagePos.Y >= topLeft.Y && stagePos.Y <= topLeft.Y + s.GetHeight())
                    {
                        slotType = CrystalSlotType.Inventory;
                        slotIdx = i;
                        slot = s;
                        return true;
                    }
                }
            }

            if (_queueSlots != null)
            {
                for (int i = 0; i < QUEUE_SLOTS; i++)
                {
                    var s = _queueSlots[i];
                    if (s == null) continue;
                    var topLeft = s.LocalToStageCoordinates(Vector2.Zero);
                    if (stagePos.X >= topLeft.X && stagePos.X <= topLeft.X + s.GetWidth() &&
                        stagePos.Y >= topLeft.Y && stagePos.Y <= topLeft.Y + s.GetHeight())
                    {
                        slotType = CrystalSlotType.Queue;
                        slotIdx = i;
                        slot = s;
                        return true;
                    }
                }
            }

            slotType = CrystalSlotType.Inventory;
            slotIdx = -1;
            slot = null;
            return false;
        }

        private void HandleHeroSlotDrop(CrystalSlotType slotType, int slotIdx, CrystalSlotElement slot)
        {
            // Only handle vault crystal drags
            if (!InventoryDragManager.IsVaultCrystalDrag)
                return;

            // Only accept drops on empty slots
            if (slot.Crystal != null)
            {
                InventoryDragManager.CancelDrag();
                return;
            }

            var crystal = InventoryDragManager.SourceVaultCrystal;
            if (crystal == null)
            {
                InventoryDragManager.CancelDrag();
                return;
            }

            OnVaultCrystalDropRequested?.Invoke(slotType, slotIdx, crystal);
        }

        private void OnSlotHovered(CrystalSlotElement slot)
        {
            if (slot.Crystal == null || _hoverTooltip == null || _stage == null) return;
            var crystal = slot.Crystal;
            _hoverLabel.SetText(crystal.Name + " (Lv." + crystal.Level + ")");
            _hoverTooltip.Pack();
            if (_hoverTooltip.GetParent() == null)
                _stage.AddElement(_hoverTooltip);
            _hoverTooltip.SetVisible(true);
            _hoverTooltip.ToFront();
            var mousePos = _stage.GetMousePosition();
            float tx = mousePos.X + 10f;
            float ty = mousePos.Y + 10f;
            float stageH = _stage.GetHeight();
            if (ty + _hoverTooltip.GetHeight() > stageH)
                ty = stageH - _hoverTooltip.GetHeight();
            _hoverTooltip.SetPosition(tx, ty);
        }

        private void OnSlotUnhovered(CrystalSlotElement slot)
        {
            _hoverTooltip?.SetVisible(false);
            _hoverTooltip?.Remove();
        }
    }
}
