using Microsoft.Xna.Framework;
using Nez;
using Nez.UI;
using PitHero.Services;
using RolePlayingFramework.Heroes;

namespace PitHero.UI
{
    /// <summary>A 9x6 scrollable grid showing crystals from the Second Chance Merchant Vault.</summary>
    public class VaultCrystalGrid : Group
    {
        private const int COLS = 9;
        private const int ROWS = 6;
        private const int MAX_VISIBLE = COLS * ROWS; // 54
        private const float SLOT_SIZE = 32f;
        private const float SLOT_PAD = 1f;

        private readonly VaultCrystalSlot[] _slots;
        private Stage _tooltipStage;
        private Window _hoverTooltip;
        private Label _hoverLabel;

        /// <summary>Fired when a vault crystal drag starts.</summary>
        public event System.Action<VaultCrystalSlot> OnVaultCrystalDragStarted;

        /// <summary>Fired when a vault crystal drag is dropped. The Vector2 is the stage-coordinate drop position.</summary>
        public event System.Action<VaultCrystalSlot, Vector2> OnVaultCrystalDragDropped;

        /// <summary>Creates a new vault crystal grid.</summary>
        public VaultCrystalGrid()
        {
            _slots = new VaultCrystalSlot[MAX_VISIBLE];
            for (int i = 0; i < MAX_VISIBLE; i++)
            {
                var slot = new VaultCrystalSlot();
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

        /// <summary>Initializes the hover tooltip for crystal name display.</summary>
        public void InitializeTooltip(Stage stage, Skin skin)
        {
            _tooltipStage = stage;
            _hoverTooltip = new Window("", skin);
            _hoverTooltip.SetMovable(false);
            _hoverTooltip.SetResizable(false);
            _hoverTooltip.SetKeepWithinStage(false);
            _hoverTooltip.SetColor(GameConfig.TransparentMenu);
            _hoverLabel = new Label("", skin, "ph-default");
            _hoverTooltip.Add(_hoverLabel).Pad(4f);
            _hoverTooltip.Pack();
            _hoverTooltip.SetVisible(false);
        }

        /// <summary>Refreshes the grid from the vault, displaying up to 54 crystals.</summary>
        public void RefreshFromVault(SecondChanceMerchantVault vault)
        {
            var crystals = vault?.LostCrystals;
            int count = crystals != null ? crystals.Count : 0;
            for (int i = 0; i < MAX_VISIBLE; i++)
            {
                if (i < count)
                    _slots[i].SetCrystal(crystals[i]);
                else
                    _slots[i].SetCrystal(null);
            }
        }

        /// <summary>Shows all crystal sprites (called after drag cancel).</summary>
        public void ShowAllCrystalSprites()
        {
            for (int i = 0; i < MAX_VISIBLE; i++)
                _slots[i].SetCrystalHidden(false);
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

        private void HandleSlotHovered(VaultCrystalSlot slot)
        {
            if (slot.Crystal == null || _hoverTooltip == null || _tooltipStage == null) return;
            var crystal = slot.Crystal;
            _hoverLabel.SetText(crystal.Name + " (Lv." + crystal.Level + ") - " + (GameConfig.CrystalBuyBackBasePrice * crystal.Level) + "g");
            _hoverTooltip.Pack();
            if (_hoverTooltip.GetParent() == null)
                _tooltipStage.AddElement(_hoverTooltip);
            _hoverTooltip.SetVisible(true);
            _hoverTooltip.ToFront();
            var mousePos = _tooltipStage.GetMousePosition();
            float tx = mousePos.X + 10f;
            float ty = mousePos.Y + 10f;
            float stageH = _tooltipStage.GetHeight();
            if (ty + _hoverTooltip.GetHeight() > stageH)
                ty = stageH - _hoverTooltip.GetHeight();
            _hoverTooltip.SetPosition(tx, ty);
        }

        private void HandleSlotUnhovered(VaultCrystalSlot slot)
        {
            _hoverTooltip?.SetVisible(false);
            _hoverTooltip?.Remove();
        }

        private void HandleSlotDragStarted(VaultCrystalSlot slot, Vector2 pos)
        {
            if (slot.Crystal == null) return;
            _hoverTooltip?.SetVisible(false);
            _hoverTooltip?.Remove();
            slot.SetCrystalHidden(true);
            var stagePos = slot.LocalToStageCoordinates(pos);
            InventoryDragManager.BeginVaultCrystalDrag(slot.Crystal, _tooltipStage);
            InventoryDragManager.UpdateDrag(stagePos);
            OnVaultCrystalDragStarted?.Invoke(slot);
        }

        private void HandleSlotDragMoved(VaultCrystalSlot slot, Vector2 pos)
        {
            var stagePos = slot.LocalToStageCoordinates(pos);
            InventoryDragManager.UpdateDrag(stagePos);
        }

        private void HandleSlotDragDropped(VaultCrystalSlot slot, Vector2 pos)
        {
            var stagePos = slot.LocalToStageCoordinates(pos);
            InventoryDragManager.UpdateDrag(stagePos);
            OnVaultCrystalDragDropped?.Invoke(slot, stagePos);
        }
    }
}
