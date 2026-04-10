using Nez;
using Nez.UI;
using PitHero.Services;
using RolePlayingFramework.Heroes;

namespace PitHero.UI
{
    /// <summary>The main content of the Crystals collection tab.</summary>
    public class CrystalsTab
    {
        private const int INVENTORY_COLS = 8;
        private const int INVENTORY_ROWS = 5;
        private const int INVENTORY_TOTAL = INVENTORY_COLS * INVENTORY_ROWS; // 40 slots
        private const int QUEUE_SLOTS = 5;
        private const float SLOT_SIZE = 32f;
        private const float SLOT_PAD = 1f;

        private CrystalSlotElement _forgeInputA;
        private CrystalSlotElement _forgeInputB;
        private CrystalSlotElement _forgeOutput;
        private TextButton _forgeButton;
        private CrystalSlotElement[] _inventorySlots;
        private CrystalSlotElement[] _queueSlots;
        private TextButton _createButton;
        private HeroCrystalCard _crystalCard;
        private CrystalCollectionService _crystalService;
        private Stage _stage;
        private Skin _skin;
        private TextService _textService;

        private int _forgeStep = 0;
        private int _selectedInventorySlot = -1;

        /// <summary>Creates and returns the tab content table.</summary>
        public Table CreateContent(Skin skin, Stage stage)
        {
            _skin = skin;
            _stage = stage;
            _textService = Core.Services?.GetService<TextService>();
            _crystalService = Core.Services?.GetService<CrystalCollectionService>();

            _crystalCard = new HeroCrystalCard(skin, stage);
            stage.AddElement(_crystalCard);

            // ── Root table ───────────────────────────────────────────────────────
            var mainTable = new Table();
            mainTable.Top().Left();

            // ── Forge section (spans both columns) ───────────────────────────────
            var forgeSection = new Table();
            forgeSection.Add(new Label(GetText(UITextKey.CrystalForgeTitle), skin, "ph-default")).Left().Pad(5);
            forgeSection.Row();

            var forgeRow = new Table();
            _forgeInputA = new CrystalSlotElement(CrystalSlotKind.Inventory);
            _forgeInputB = new CrystalSlotElement(CrystalSlotKind.Inventory);
            _forgeOutput = new CrystalSlotElement(CrystalSlotKind.Shortcut);
            forgeRow.Add(_forgeInputA).Size(SLOT_SIZE).Pad(2);
            forgeRow.Add(new Label("+", skin, "ph-default")).Pad(2);
            forgeRow.Add(_forgeInputB).Size(SLOT_SIZE).Pad(2);
            forgeRow.Add(new Label("=", skin, "ph-default")).Pad(2);
            forgeRow.Add(_forgeOutput).Size(SLOT_SIZE).Pad(2);

            _forgeButton = new TextButton(GetText(UITextKey.CrystalForgeButton), skin, "ph-default");
            _forgeButton.OnClicked += OnForgeClicked;
            _forgeButton.SetDisabled(true);
            forgeRow.Add(_forgeButton).Pad(4);

            forgeSection.Add(forgeRow).Left().Pad(2);

            // Forge spans both columns of mainTable so queue label and inv label share the same row
            mainTable.Add(forgeSection).Left().SetColspan(2).Pad(5);
            mainTable.Row();

            // ── Inventory section (left) ──────────────────────────────────────────
            var invCol = new Table();
            invCol.Add(new Label(GetText(UITextKey.CrystalInventoryTitle), skin, "ph-default")).Left().Pad(5);
            invCol.Row();

            var invGrid = new Table();
            _inventorySlots = new CrystalSlotElement[INVENTORY_TOTAL];
            for (int i = 0; i < INVENTORY_TOTAL; i++)
            {
                var slot = new CrystalSlotElement(CrystalSlotKind.Inventory);
                int idx = i;
                slot.OnSlotClicked += s => OnInventorySlotClicked(idx);
                _inventorySlots[i] = slot;
                invGrid.Add(slot).Size(SLOT_SIZE).Pad(SLOT_PAD);
                if ((i + 1) % INVENTORY_COLS == 0) invGrid.Row();
            }
            invCol.Add(invGrid).Left().Pad(2);
            invCol.Row();

            _createButton = new TextButton(GetText(UITextKey.CrystalCreateButton), skin, "ph-default");
            _createButton.OnClicked += OnCreateClicked;
            invCol.Add(_createButton).Left().Pad(5);

            // ── Queue section (right) ─────────────────────────────────────────────
            var queueCol = new Table();
            queueCol.Add(new Label(GetText(UITextKey.CrystalQueueTitle), skin, "ph-default")).Left().Pad(5);
            queueCol.Row();

            _queueSlots = new CrystalSlotElement[QUEUE_SLOTS];
            for (int i = 0; i < QUEUE_SLOTS; i++)
            {
                var slot = new CrystalSlotElement(CrystalSlotKind.Shortcut);
                int idx = i;
                slot.OnSlotClicked += s => OnQueueSlotClicked(idx);
                _queueSlots[i] = slot;
                queueCol.Add(slot).Size(SLOT_SIZE).Pad(SLOT_PAD);
                queueCol.Row();
            }

            mainTable.Add(invCol).Top().Left().Pad(2);
            mainTable.Add(queueCol).Top().Left().Pad(2, 0, 2, 5);

            RefreshAll();
            return mainTable;
        }

        private string GetText(string key) => _textService?.DisplayText(TextType.UI, key) ?? key;

        /// <summary>Refreshes all slots from the service.</summary>
        public void RefreshAll()
        {
            if (_crystalService == null) return;

            for (int i = 0; i < INVENTORY_TOTAL; i++)
                _inventorySlots[i].SetCrystal(_crystalService.Inventory[i]);
            
            for (int i = 0; i < QUEUE_SLOTS; i++)
                _queueSlots[i].SetCrystal(_crystalService.Queue[i]);
        }

        private void OnInventorySlotClicked(int idx)
        {
            _selectedInventorySlot = idx;
            var crystal = _crystalService?.GetInventoryCrystal(idx);
            if (crystal != null && _crystalCard != null)
            {
                _crystalCard.ShowCrystal(crystal);
                _crystalCard.PositionNear(Input.MousePosition);
            }
        }

        private void OnQueueSlotClicked(int idx)
        {
            if (_selectedInventorySlot >= 0 && _crystalService != null)
            {
                _crystalService.EnqueueAt(idx, _selectedInventorySlot);
                RefreshAll();
            }
        }

        private void OnForgeClicked(Button b)
        {
            if (_crystalService != null)
            {
                var result = _crystalService.TryForge("Combo Crystal");
                if (result != null)
                {
                    _crystalService.TryAddToInventory(result);
                    RefreshAll();
                }
            }
        }

        private void OnCreateClicked(Button b)
        {
            var dialog = new CrystalCreationDialog(_skin, _crystalService);
            dialog.OnCrystalCreated += c => RefreshAll();
            _stage.AddElement(dialog);
        }
    }
}
