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

            var closeButton = new TextButton(GetText(UITextKey.ButtonClose), skin, "ph-default");
            closeButton.OnClicked += (_) => Hide();
            content.Add(closeButton).Width(100f).SetPadTop(12f);

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

        /// <summary>Copies the service's current designations into the checkbox states.</summary>
        public void SyncFromService()
        {
            var svc = Core.Services?.GetService<AutoCropSellService>();
            if (svc == null) return;
            for (int i = 0; i < _cropChecks.Length; i++)
                if (_cropChecks[i] != null)
                    _cropChecks[i].IsChecked = svc.Designations[i];
        }
    }
}
