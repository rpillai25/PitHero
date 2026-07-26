using Nez;
using Nez.Sprites;
using Nez.Textures;
using Nez.UI;
using PitHero.Services;
using RolePlayingFramework.Equipment;

namespace PitHero.UI
{
    /// <summary>
    /// The "Consumable Purchase Options" window opened from the Automation tab (issue #345).
    /// Shows every catalog consumable with its sprite, a selection checkbox, and a 1-3 "Stacks"
    /// slider naming how many stacks of that item the party should hold. Changes commit immediately
    /// to <see cref="AutoItemPurchaseService.ConsumableSelected"/> /
    /// <see cref="AutoItemPurchaseService.ConsumableStackTargets"/>.
    /// </summary>
    public class ConsumablePurchaseOptionsDialog
    {
        private const int Columns = 3;
        private const float SpriteSize = 32f;
        private const float WinPad = 16f;
        private const float GridMaxHeight = 200f;
        private const float SliderWidth = 120f;

        private readonly Stage _stage;
        private readonly SpriteAtlas _itemsAtlas;
        private readonly CheckBox[] _checks = new CheckBox[ConsumableCatalog.Count];
        private readonly HoverableLabel[] _stackLabels = new HoverableLabel[ConsumableCatalog.Count];
        private readonly EnhancedSlider[] _stackSliders = new EnhancedSlider[ConsumableCatalog.Count];

        private Window _window;
        private TextService _textService;

        public ConsumablePurchaseOptionsDialog(Stage stage)
        {
            _stage = stage;
            _itemsAtlas = Core.Content.LoadSpriteAtlas("Content/Atlases/Items.atlas");
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
            _window = new Window(GetText(UITextKey.WindowConsumablePurchaseOptions), skin, "ph-default");
            _window.SetMovable(false);
            _window.SetResizable(false);

            var content = new Table();
            content.Pad(WinPad);

            var grid = new Table();
            for (int i = 0; i < ConsumableCatalog.Count; i++)
            {
                int index = i;
                var cell = new Table();

                var topRow = new Table();
                var spriteName = ConsumableCatalog.GetSpriteName(i);
                if (!string.IsNullOrEmpty(spriteName))
                {
                    var sprite = _itemsAtlas.GetSprite(spriteName);
                    if (sprite != null)
                        topRow.Add(new Image(new SpriteDrawable(sprite))).Size(SpriteSize, SpriteSize).SetPadRight(4f);
                }

                var check = new CheckBox(ConsumableCatalog.GetDisplayName(i), skin, "ph-default");
                check.IsChecked = false;
                check.OnChanged += (isChecked) =>
                {
                    var svc = Core.Services?.GetService<AutoItemPurchaseService>();
                    if (svc != null && index < svc.ConsumableSelected.Length)
                        svc.ConsumableSelected[index] = isChecked;
                };
                _checks[i] = check;
                topRow.Add(check).Left();

                cell.Add(topRow).Left();
                cell.Row();

                var stackLabel = new HoverableLabel(
                    string.Format(GetText(UITextKey.SettingsConsumableStacks), AutoItemPurchaseService.MinStackTarget),
                    skin, "ph-default", GetText(UITextKey.SettingsConsumableStacksTooltip), _stage);
                _stackLabels[i] = stackLabel;
                cell.Add(stackLabel).Left().SetPadTop(2f);
                cell.Row();

                var slider = new EnhancedSlider(
                    AutoItemPurchaseService.MinStackTarget, AutoItemPurchaseService.MaxStackTarget, 1, false, skin, null, false);
                slider.SetValueAndCommit(AutoItemPurchaseService.MinStackTarget);
                slider.OnChanged += (value) =>
                {
                    _stackLabels[index].SetText(string.Format(GetText(UITextKey.SettingsConsumableStacks), (int)value));
                };
                slider.OnValueCommitted += (value) =>
                {
                    var svc = Core.Services?.GetService<AutoItemPurchaseService>();
                    if (svc != null && index < svc.ConsumableStackTargets.Length)
                        svc.ConsumableStackTargets[index] = (int)value;
                };
                _stackSliders[i] = slider;
                cell.Add(slider).Width(SliderWidth).Left();

                grid.Add(cell).Left().Top().Pad(6f);
                if ((i + 1) % Columns == 0)
                    grid.Row();
            }

            var scrollPane = new ScrollPane(grid, skin, "ph-default");
            scrollPane.SetScrollingDisabled(true, false);
            scrollPane.SetFadeScrollBars(false);
            content.Add(scrollPane).SetMaxHeight(GridMaxHeight).Expand().Fill();
            content.Row();

            var buttonRow = new Table();
            var deselectAllButton = new TextButton(GetText(UITextKey.ButtonDeselectAll), skin, "ph-default");
            deselectAllButton.OnClicked += (_) => SetAllSelected(false);
            buttonRow.Add(deselectAllButton).Width(110f).SetPadRight(8f);

            var closeButton = new TextButton(GetText(UITextKey.ButtonClose), skin, "ph-default");
            closeButton.OnClicked += (_) => Hide();
            buttonRow.Add(closeButton).Width(100f);

            content.Add(buttonRow).SetPadTop(12f);

            _window.Add(content).Expand().Fill();
            _window.SetVisible(false);
            _stage.AddElement(_window);
        }

        /// <summary>Syncs the controls from the service, then shows the window centered on the stage.</summary>
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
        /// Sets every selection checkbox. Commits to the service directly: programmatic IsChecked
        /// assignment does not fire OnChanged (ProgrammaticChangeEvents is off).
        /// </summary>
        private void SetAllSelected(bool selected)
        {
            var svc = Core.Services?.GetService<AutoItemPurchaseService>();
            for (int i = 0; i < _checks.Length; i++)
            {
                if (svc != null && i < svc.ConsumableSelected.Length)
                    svc.ConsumableSelected[i] = selected;
                if (_checks[i] != null)
                    _checks[i].IsChecked = selected;
            }
        }

        /// <summary>Copies the service's current selections and stack targets into the controls.</summary>
        public void SyncFromService()
        {
            var svc = Core.Services?.GetService<AutoItemPurchaseService>();
            if (svc == null) return;

            for (int i = 0; i < _checks.Length && i < svc.ConsumableSelected.Length; i++)
            {
                if (_checks[i] != null)
                    _checks[i].IsChecked = svc.ConsumableSelected[i];
            }

            for (int i = 0; i < _stackSliders.Length && i < svc.ConsumableStackTargets.Length; i++)
            {
                int target = svc.ConsumableStackTargets[i];
                _stackSliders[i]?.SetValueAndCommit(target);
                _stackLabels[i]?.SetText(string.Format(GetText(UITextKey.SettingsConsumableStacks), target));
            }
        }
    }
}
