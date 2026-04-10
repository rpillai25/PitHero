using Nez;
using Nez.UI;
using PitHero.Services;
using RolePlayingFramework.Heroes;

namespace PitHero.UI
{
    /// <summary>The main content of the Crystals collection tab.</summary>
    public class CrystalsTab
    {
        private const int INVENTORY_COLS = 10;
        private const int INVENTORY_ROWS = 8;
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

            var mainTable = new Table();
            
            // Load sprites
            var itemsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/Items.atlas");
            var baseSprite = itemsAtlas.GetSprite("HeroCrystalBase");
            var crystalSprite = itemsAtlas.GetSprite("HeroCrystal");

            // Left column: Forge + Inventory
            var leftCol = new Table();
            
            // Forge section
            leftCol.Add(new Label(GetText(UITextKey.CrystalForgeTitle), skin, "ph-default")).Left().Pad(5);
            leftCol.Row();
            var forgeTable = new Table();
            _forgeInputA = new CrystalSlotElement(baseSprite, crystalSprite, CrystalSlotKind.Inventory);
            _forgeInputB = new CrystalSlotElement(baseSprite, crystalSprite, CrystalSlotKind.Inventory);
            _forgeOutput = new CrystalSlotElement(baseSprite, crystalSprite, CrystalSlotKind.Shortcut);
            forgeTable.Add(_forgeInputA).Pad(2);
            forgeTable.Add(_forgeInputB).Pad(2);
            forgeTable.Add(new Label("+", skin, "ph-default")).Pad(2);
            forgeTable.Add(_forgeOutput).Pad(2);
            leftCol.Add(forgeTable).Pad(5);
            leftCol.Row();
            _forgeButton = new TextButton(GetText(UITextKey.CrystalForgeButton), skin, "ph-default");
            _forgeButton.OnClicked += OnForgeClicked;
            _forgeButton.SetDisabled(true);
            leftCol.Add(_forgeButton).Pad(5);
            leftCol.Row();

            // Inventory grid
            leftCol.Add(new Label(GetText(UITextKey.CrystalInventoryTitle), skin, "ph-default")).Left().Pad(5);
            leftCol.Row();
            var invGrid = new Table();
            _inventorySlots = new CrystalSlotElement[80];
            for (int i = 0; i < 80; i++)
            {
                var slot = new CrystalSlotElement(baseSprite, crystalSprite, CrystalSlotKind.Inventory);
                int idx = i;
                slot.OnSlotClicked += s => OnInventorySlotClicked(idx);
                _inventorySlots[i] = slot;
                invGrid.Add(slot).Pad(SLOT_PAD);
                if ((i + 1) % INVENTORY_COLS == 0) invGrid.Row();
            }
            leftCol.Add(invGrid).Pad(5);
            leftCol.Row();

            _createButton = new TextButton(GetText(UITextKey.CrystalCreateButton), skin, "ph-default");
            _createButton.OnClicked += OnCreateClicked;
            leftCol.Add(_createButton).Pad(5);

            // Right column: Queue
            var rightCol = new Table();
            rightCol.Add(new Label(GetText(UITextKey.CrystalQueueTitle), skin, "ph-default")).Left().Pad(5);
            rightCol.Row();
            _queueSlots = new CrystalSlotElement[5];
            for (int i = 0; i < 5; i++)
            {
                var slot = new CrystalSlotElement(baseSprite, crystalSprite, CrystalSlotKind.Shortcut);
                int idx = i;
                slot.OnSlotClicked += s => OnQueueSlotClicked(idx);
                _queueSlots[i] = slot;
                rightCol.Add(slot).Pad(SLOT_PAD);
                rightCol.Row();
            }

            mainTable.Add(leftCol).Top().Left();
            mainTable.Add(rightCol).Top().Right().Pad(0, 0, 0, 10);

            RefreshAll();
            return mainTable;
        }

        private string GetText(string key) => _textService?.DisplayText(TextType.UI, key) ?? key;

        /// <summary>Refreshes all slots from the service.</summary>
        public void RefreshAll()
        {
            if (_crystalService == null) return;

            for (int i = 0; i < 80; i++)
                _inventorySlots[i].SetCrystal(_crystalService.Inventory[i]);
            
            for (int i = 0; i < 5; i++)
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
