using Microsoft.Xna.Framework;
using Nez;
using Nez.UI;
using PitHero.Services;
using PitHero.Util;

namespace PitHero.UI
{
    /// <summary>
    /// Small popup shown when a placed building is clicked. Offers Move and a type-specific
    /// "Show" action (Show Monsters for a Monster House, Show Crops for a Crop Storage), plus an
    /// "Add Monsters" action for a Monster House that still has room (issue #283). Pauses the
    /// game while open, mirroring <see cref="MercenaryHireDialog"/>.
    /// </summary>
    public class BuildingContextMenu : Window
    {
        private readonly Table _content;
        private readonly Label _titleLabel;
        private readonly TextButton _moveButton;
        private readonly TextButton _showButton;
        private readonly TextButton _addMonstersButton;
        private readonly TextButton _sellBuildingButton;
        private readonly TextButton _cancelButton;
        private PlacedBuilding _building;
        private bool _isVisible;
        private TextService _textService;

        /// <summary>Fired when the player chooses Move for the current building.</summary>
        public event System.Action<PlacedBuilding> OnMove;

        /// <summary>Fired when the player chooses the Show action for the current building.</summary>
        public event System.Action<PlacedBuilding> OnShow;

        /// <summary>Fired when the player chooses Add Monsters for a Monster House.</summary>
        public event System.Action<PlacedBuilding> OnAddMonsters;

        /// <summary>Fired when the player chooses Sell building for an (empty) Crop Storage.</summary>
        public event System.Action<PlacedBuilding> OnSellBuilding;

        /// <summary>Whether the context menu is currently visible.</summary>
        public bool IsVisible => _isVisible;

        public BuildingContextMenu(Skin skin) : base("", skin, "ph-default")
        {
            SetMovable(false);
            SetResizable(false);

            _content = new Table();
            _content.Pad(12f);

            // Buttons are created once (so their handlers stay wired) and re-added to the content
            // table in Show() based on the building type and capacity.
            _titleLabel = new Label("", skin, "ph-default");

            _moveButton = new TextButton(GetText(UITextKey.ButtonMove), skin, "ph-default");
            _moveButton.OnClicked += (_) => OnMoveClicked();

            _showButton = new TextButton(GetText(UITextKey.ButtonShowMonsters), skin, "ph-default");
            _showButton.OnClicked += (_) => OnShowClicked();

            _addMonstersButton = new TextButton(GetText(UITextKey.ButtonAddMonsters), skin, "ph-default");
            _addMonstersButton.OnClicked += (_) => OnAddMonstersClicked();

            _sellBuildingButton = new TextButton(GetText(UITextKey.ButtonSellBuilding), skin, "ph-default");
            _sellBuildingButton.OnClicked += (_) => OnActionClicked(OnSellBuilding);

            _cancelButton = new TextButton(GetText(UITextKey.ButtonCancel), skin, "ph-default");
            _cancelButton.OnClicked += (_) => Hide();

            Add(_content).Expand().Fill();
            SetVisible(false);
        }

        /// <summary>Shows the menu for a building, positioned near the given stage-space point.</summary>
        public void Show(Stage stage, PlacedBuilding building, Vector2 stagePos)
        {
            if (stage == null || building == null)
                return;

            _building = building;

            bool isMonsterHouse = building.Type == BuildingType.MonsterHouse;
            _showButton.SetText(isMonsterHouse
                ? GetText(UITextKey.ButtonShowMonsters)
                : GetText(UITextKey.ButtonShowCrops));

            // Offer "Add Monsters" only for a Monster House that still has room.
            bool showAddMonsters = isMonsterHouse && !IsHouseFull(building.UniqueId);

            // Rebuild the content rows so hidden buttons don't reserve layout space.
            _content.Clear();
            _titleLabel.SetText(GetText(BuildingConfig.GetDisplayNameKey(building.Type)));
            _content.Add(_titleLabel).SetPadBottom(8f);
            _content.Row();
            _content.Add(_moveButton).Width(140f).SetPadBottom(4f);
            _content.Row();
            _content.Add(_showButton).Width(140f).SetPadBottom(4f);
            _content.Row();
            if (showAddMonsters)
            {
                _content.Add(_addMonstersButton).Width(140f).SetPadBottom(4f);
                _content.Row();
            }

            // Crop-Storage-only option: sell the building once it is empty of crops. (Move/Sell all
            // crops live in the Harvested Crops viewer, opened via "Show Crops".)
            if (!isMonsterHouse && IsStorageEmpty(building.UniqueId))
            {
                _content.Add(_sellBuildingButton).Width(140f).SetPadBottom(4f);
                _content.Row();
            }

            _content.Add(_cancelButton).Width(140f);

            Pack();
            float w = GetWidth();
            float h = GetHeight();

            // Clamp so the whole menu stays on screen.
            float x = stagePos.X + 8f;
            float y = stagePos.Y + 8f;
            if (x + w > stage.GetWidth())  x = stage.GetWidth()  - w;
            if (y + h > stage.GetHeight()) y = stage.GetHeight() - h;
            if (x < 0) x = 0;
            if (y < 0) y = 0;
            SetPosition(x, y);

            stage.AddElement(this);
            SetVisible(true);
            ToFront();
            _isVisible = true;

            Core.Services.GetService<PauseService>()?.Pause();
        }

        /// <summary>Hides the menu and unpauses.</summary>
        public void Hide()
        {
            SetVisible(false);
            Remove();
            _isVisible = false;
            _building = null;
            Core.Services.GetService<PauseService>()?.Unpause();
        }

        private bool IsHouseFull(int uniqueId)
        {
            var allied = Core.Services?.GetService<AlliedMonsterManager>();
            return allied != null && allied.IsHouseFull(uniqueId);
        }

        private bool IsStorageEmpty(int uniqueId)
        {
            var storage = Core.Services?.GetService<CropStorageInventoryService>();
            return storage == null || storage.IsEmpty(uniqueId);
        }

        /// <summary>Hides the menu, then fires the given action for the current building.</summary>
        private void OnActionClicked(System.Action<PlacedBuilding> action)
        {
            var b = _building;
            Hide();
            action?.Invoke(b);
        }

        private void OnMoveClicked()
        {
            var b = _building;
            Hide();
            OnMove?.Invoke(b);
        }

        private void OnShowClicked()
        {
            var b = _building;
            Hide();
            OnShow?.Invoke(b);
        }

        private void OnAddMonstersClicked()
        {
            var b = _building;
            Hide();
            OnAddMonsters?.Invoke(b);
        }

        private string GetText(string key)
        {
            if (_textService == null)
                _textService = Core.Services?.GetService<TextService>();
            return _textService?.DisplayText(TextType.UI, key) ?? key;
        }
    }
}
