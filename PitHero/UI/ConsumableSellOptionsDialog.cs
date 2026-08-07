using Nez;
using Nez.Sprites;
using Nez.Textures;
using Nez.UI;
using PitHero.Services;
using RolePlayingFramework.Equipment;

namespace PitHero.UI
{
    /// <summary>
    /// The "Consumable Sell Options" window opened from the Automation tab. Shows every catalog
    /// consumable with its sprite, a selection checkbox (checked = may be auto-sold; everything is
    /// selected by default), and a 0-3 "Min Stacks" slider naming how many stacks auto-sell must
    /// leave in the bag. Changes commit immediately to
    /// <see cref="AutoSellExcessItemsService.ConsumableSellAllowed"/> /
    /// <see cref="AutoSellExcessItemsService.ConsumableMinStacks"/>.
    /// </summary>
    public class ConsumableSellOptionsDialog
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

        public ConsumableSellOptionsDialog(Stage stage)
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
            _window = new Window(GetText(UITextKey.WindowConsumableSellOptions), skin, "ph-default");
            _window.SetMovable(false);
            _window.SetResizable(false);

            var content = new Table();
            content.Pad(WinPad);

            content.Add(new Label(GetText(UITextKey.LabelConsumablesAutoSold), skin, "ph-default")).Left().SetPadBottom(8f);
            content.Row();

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
                check.IsChecked = true;
                check.OnChanged += (isChecked) =>
                {
                    var svc = Core.Services?.GetService<AutoSellExcessItemsService>();
                    if (svc != null && index < svc.ConsumableSellAllowed.Length)
                        svc.ConsumableSellAllowed[index] = isChecked;
                    SetStackControlsActive(index, isChecked);
                };
                _checks[i] = check;
                topRow.Add(check).Left();

                cell.Add(topRow).Left();
                cell.Row();

                var stackLabel = new HoverableLabel(
                    string.Format(GetText(UITextKey.SettingsConsumableMinStacks), 1),
                    skin, "ph-default", GetText(UITextKey.SettingsConsumableMinStacksTooltip), _stage);
                _stackLabels[i] = stackLabel;
                cell.Add(stackLabel).Left().SetPadTop(2f);
                cell.Row();

                var slider = new EnhancedSlider(
                    AutoSellExcessItemsService.MinKeepStacks, AutoSellExcessItemsService.MaxKeepStacks, 1, false, skin, null, false);
                slider.SetValueAndCommit(1);
                slider.OnChanged += (value) =>
                {
                    _stackLabels[index].SetText(string.Format(GetText(UITextKey.SettingsConsumableMinStacks), (int)value));
                };
                slider.OnValueCommitted += (value) =>
                {
                    var svc = Core.Services?.GetService<AutoSellExcessItemsService>();
                    if (svc != null && index < svc.ConsumableMinStacks.Length)
                        svc.ConsumableMinStacks[index] = (int)value;
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
            var selectAllButton = new TextButton(GetText(UITextKey.ButtonSelectAll), skin, "ph-default");
            selectAllButton.OnClicked += (_) => SetAllSelected(true);
            buttonRow.Add(selectAllButton).Width(110f).SetMinHeight(16f).SetPadRight(8f);

            var deselectAllButton = new TextButton(GetText(UITextKey.ButtonDeselectAll), skin, "ph-default");
            deselectAllButton.OnClicked += (_) => SetAllSelected(false);
            buttonRow.Add(deselectAllButton).Width(110f).SetMinHeight(16f).SetPadRight(8f);

            var closeButton = new TextButton(GetText(UITextKey.ButtonClose), skin, "ph-default");
            closeButton.ClickSoundCategory = ButtonClickCategory.Cancel;
            closeButton.OnClicked += (_) => Hide();
            buttonRow.Add(closeButton).Width(100f).SetMinHeight(16f);

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
            var svc = Core.Services?.GetService<AutoSellExcessItemsService>();
            for (int i = 0; i < _checks.Length; i++)
            {
                if (svc != null && i < svc.ConsumableSellAllowed.Length)
                    svc.ConsumableSellAllowed[i] = selected;
                if (_checks[i] != null)
                    _checks[i].IsChecked = selected;
                SetStackControlsActive(i, selected);
            }
        }

        /// <summary>
        /// Activates or deactivates one row's "Min Stacks" label and slider. When deactivated they
        /// stay on screen but are drawn grayed out with the tooltip, hover and dragging disabled —
        /// an unselected consumable is never auto-sold, so its floor means nothing.
        /// </summary>
        private void SetStackControlsActive(int index, bool active)
        {
            if (index < 0 || index >= _stackSliders.Length)
                return;

            var skin = PitHeroSkin.CreateSkin();

            var label = _stackLabels[index];
            if (label != null)
            {
                label.SetStyle(skin.Get<LabelStyle>(active ? "ph-default" : "ph-grayed"));
                label.SetTooltipEnabled(active);
            }

            var slider = _stackSliders[index];
            if (slider != null)
            {
                slider.Disabled = !active;
                slider.SetTouchable(active ? Touchable.Enabled : Touchable.Disabled);
            }
        }

        /// <summary>Copies the service's current selections and stack floors into the controls.</summary>
        public void SyncFromService()
        {
            var svc = Core.Services?.GetService<AutoSellExcessItemsService>();
            if (svc == null) return;

            for (int i = 0; i < _checks.Length && i < svc.ConsumableSellAllowed.Length; i++)
            {
                if (_checks[i] != null)
                    _checks[i].IsChecked = svc.ConsumableSellAllowed[i];
            }

            for (int i = 0; i < _stackSliders.Length && i < svc.ConsumableMinStacks.Length; i++)
            {
                int floor = svc.ConsumableMinStacks[i];
                _stackSliders[i]?.SetValueAndCommit(floor);
                _stackLabels[i]?.SetText(string.Format(GetText(UITextKey.SettingsConsumableMinStacks), floor));
            }

            for (int i = 0; i < _stackSliders.Length; i++)
                SetStackControlsActive(i, i < svc.ConsumableSellAllowed.Length && svc.ConsumableSellAllowed[i]);
        }
    }
}
