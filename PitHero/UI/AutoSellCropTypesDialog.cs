using Nez;
using Nez.Sprites;
using Nez.Textures;
using Nez.UI;
using PitHero.Farming;
using PitHero.Services;
using PitHero.Util;

namespace PitHero.UI
{
    /// <summary>
    /// The "Auto-Sell Crop Types" designation window opened from the Auto Shop tab's
    /// "Designate Crops" button. Shows a grid of every crop type with its harvested-product sprite
    /// and a checkbox; checked crops are auto-sold when their stack reaches max size. Checkbox
    /// changes commit immediately to <see cref="AutoCropSellService.Designations"/>.
    /// </summary>
    public class AutoSellCropTypesDialog
    {
        private const int Columns = 4;
        private const float SpriteSize = 32f;
        private const float WinPad = 16f;

        private readonly Stage _stage;
        private readonly SpriteAtlas _cropsAtlas;
        private readonly CheckBox[] _cropChecks = new CheckBox[CropTypeInfo.Count];

        private Window _window;
        private HoverableLabel _keepStacksLabel;
        private EnhancedSlider _keepStacksSlider;
        private TextService _textService;

        public AutoSellCropTypesDialog(Stage stage)
        {
            _stage = stage;
            _cropsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/CropsProps.atlas");
            CreateWindow();
        }

        /// <summary>Resolves a localized UI string, falling back to the key if the service is unavailable.</summary>
        private string GetText(string key)
        {
            if (_textService == null)
                _textService = Core.Services?.GetService<TextService>();
            return _textService?.DisplayText(TextType.UI, key) ?? key;
        }

        private void CreateWindow()
        {
            var skin = PitHeroSkin.CreateSkin();
            _window = new Window(GetText(UITextKey.WindowAutoSellCropTypes), skin, "ph-default");
            _window.SetMovable(false);
            _window.SetResizable(false);

            var content = new Table();
            content.Pad(WinPad);

            var grid = new Table();
            int col = 0;
            for (int i = 0; i < CropTypeInfo.Count; i++)
            {
                var crop = (CropType)i;
                var cell = new Table();

                var sprite = _cropsAtlas.GetSprite(CropConfig.GetHarvestSpriteName(crop));
                cell.Add(new Image(new SpriteDrawable(sprite))).Size(SpriteSize, SpriteSize).SetPadRight(4f);

                var check = new CheckBox(GetText(CropConfig.GetHarvestDisplayNameKey(crop)), skin, "ph-default");
                check.IsChecked = true;
                int captured = i;
                check.OnChanged += (isChecked) =>
                {
                    var svc = Core.Services?.GetService<AutoCropSellService>();
                    if (svc != null) svc.Designations[captured] = isChecked;
                };
                _cropChecks[i] = check;
                cell.Add(check).Left();

                grid.Add(cell).Left().Pad(4f);
                col++;
                if (col % Columns == 0)
                    grid.Row();
            }
            content.Add(grid);
            content.Row();

            var keepStacksRow = new Table();
            _keepStacksLabel = new HoverableLabel(
                string.Format(GetText(UITextKey.SettingsAutoSellKeepStacks), 0),
                skin, "ph-default", GetText(UITextKey.SettingsAutoSellKeepStacksTooltip), _stage);
            keepStacksRow.Add(_keepStacksLabel).Left().SetPadRight(12f);

            _keepStacksSlider = new EnhancedSlider(0, AutoCropSellService.MaxKeepStacks, 1, false, skin, null, false);
            _keepStacksSlider.SetValueAndCommit(0);
            _keepStacksSlider.OnChanged += (value) =>
            {
                _keepStacksLabel.SetText(string.Format(GetText(UITextKey.SettingsAutoSellKeepStacks), (int)value));
            };
            _keepStacksSlider.OnValueCommitted += (value) =>
            {
                var svc = Core.Services?.GetService<AutoCropSellService>();
                if (svc != null) svc.KeepStacks = (int)value;
            };
            keepStacksRow.Add(_keepStacksSlider).Width(200f).Left();

            content.Add(keepStacksRow).Left().SetPadTop(12f);
            content.Row();

            var buttonRow = new Table();
            var selectAllButton = new TextButton(GetText(UITextKey.ButtonSelectAll), skin, "ph-default");
            selectAllButton.OnClicked += (_) => SetAllDesignations(true);
            buttonRow.Add(selectAllButton).Width(110f).SetPadRight(8f);

            var deselectAllButton = new TextButton(GetText(UITextKey.ButtonDeselectAll), skin, "ph-default");
            deselectAllButton.OnClicked += (_) => SetAllDesignations(false);
            buttonRow.Add(deselectAllButton).Width(110f).SetPadRight(8f);

            var closeButton = new TextButton(GetText(UITextKey.ButtonClose), skin, "ph-default");
            closeButton.OnClicked += (_) => Hide();
            buttonRow.Add(closeButton).Width(100f);

            content.Add(buttonRow).SetPadTop(12f);

            _window.Add(content).Expand().Fill();
            _window.SetVisible(false);
            _stage.AddElement(_window);
        }

        /// <summary>Syncs the checkboxes from the service, then shows the window centered on the stage.</summary>
        public void Show()
        {
            SyncFromService();
            _window.Pack();
            _window.SetPosition(
                (_stage.GetWidth() - _window.GetWidth()) / 2f,
                (_stage.GetHeight() - _window.GetHeight()) / 2f);
            _window.SetVisible(true);
            _window.ToFront();
        }

        /// <summary>Hides the window.</summary>
        public void Hide()
        {
            _window?.SetVisible(false);
        }

        /// <summary>
        /// Sets every crop's designation and checkbox state. Commits to the service directly:
        /// programmatic IsChecked assignment does not fire OnChanged (ProgrammaticChangeEvents is off).
        /// </summary>
        private void SetAllDesignations(bool designated)
        {
            var svc = Core.Services?.GetService<AutoCropSellService>();
            for (int i = 0; i < _cropChecks.Length; i++)
            {
                if (svc != null)
                    svc.Designations[i] = designated;
                if (_cropChecks[i] != null)
                    _cropChecks[i].IsChecked = designated;
            }
        }

        /// <summary>Copies the service's current designations and keep-stacks value into the controls.</summary>
        public void SyncFromService()
        {
            var svc = Core.Services?.GetService<AutoCropSellService>();
            if (svc == null) return;
            for (int i = 0; i < _cropChecks.Length; i++)
                if (_cropChecks[i] != null)
                    _cropChecks[i].IsChecked = svc.Designations[i];
            _keepStacksSlider?.SetValueAndCommit(svc.KeepStacks);
            _keepStacksLabel?.SetText(string.Format(GetText(UITextKey.SettingsAutoSellKeepStacks), svc.KeepStacks));
        }
    }
}
