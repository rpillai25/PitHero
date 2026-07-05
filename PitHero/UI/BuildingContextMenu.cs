using Microsoft.Xna.Framework;
using Nez;
using Nez.UI;
using PitHero.Services;
using PitHero.Util;

namespace PitHero.UI
{
    /// <summary>
    /// Small popup shown when a placed building is clicked. Offers Move and a type-specific
    /// "Show" action (Show Monsters for a Monster House, Show Crops for a Crop Storage). Pauses the
    /// game while open, mirroring <see cref="MercenaryHireDialog"/>.
    /// </summary>
    public class BuildingContextMenu : Window
    {
        private readonly Label _titleLabel;
        private readonly TextButton _moveButton;
        private readonly TextButton _showButton;
        private readonly TextButton _cancelButton;
        private PlacedBuilding _building;
        private bool _isVisible;
        private TextService _textService;

        /// <summary>Fired when the player chooses Move for the current building.</summary>
        public event System.Action<PlacedBuilding> OnMove;

        /// <summary>Fired when the player chooses the Show action for the current building.</summary>
        public event System.Action<PlacedBuilding> OnShow;

        /// <summary>Whether the context menu is currently visible.</summary>
        public bool IsVisible => _isVisible;

        public BuildingContextMenu(Skin skin) : base("", skin, "ph-default")
        {
            SetMovable(false);
            SetResizable(false);

            var content = new Table();
            content.Pad(12f);

            _titleLabel = new Label("", skin, "ph-default");
            content.Add(_titleLabel).SetPadBottom(8f);
            content.Row();

            _moveButton = new TextButton(GetText(UITextKey.ButtonMove), skin, "ph-default");
            _moveButton.OnClicked += (_) => OnMoveClicked();
            content.Add(_moveButton).Width(140f).SetPadBottom(4f);
            content.Row();

            _showButton = new TextButton(GetText(UITextKey.ButtonShowMonsters), skin, "ph-default");
            _showButton.OnClicked += (_) => OnShowClicked();
            content.Add(_showButton).Width(140f).SetPadBottom(4f);
            content.Row();

            _cancelButton = new TextButton(GetText(UITextKey.ButtonCancel), skin, "ph-default");
            _cancelButton.OnClicked += (_) => Hide();
            content.Add(_cancelButton).Width(140f);

            Add(content).Expand().Fill();
            Pack();
            SetVisible(false);
        }

        /// <summary>Shows the menu for a building, positioned near the given stage-space point.</summary>
        public void Show(Stage stage, PlacedBuilding building, Vector2 stagePos)
        {
            if (stage == null || building == null)
                return;

            _building = building;

            _titleLabel.SetText(GetText(BuildingConfig.GetDisplayNameKey(building.Type)));
            _showButton.SetText(building.Type == BuildingType.MonsterHouse
                ? GetText(UITextKey.ButtonShowMonsters)
                : GetText(UITextKey.ButtonShowCrops));

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

        private string GetText(string key)
        {
            if (_textService == null)
                _textService = Core.Services?.GetService<TextService>();
            return _textService?.DisplayText(TextType.UI, key) ?? key;
        }
    }
}
