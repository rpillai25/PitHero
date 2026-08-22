using System;
using Nez;
using Nez.UI;
using PitHero.Services;
using RolePlayingFramework.Equipment;

namespace PitHero.UI
{
    /// <summary>
    /// Rarity + gear-type filter window shared by the Automation tab's "Gear Sell Options" and
    /// "Gear Purchase Options" buttons (issue #345). Both dialogs have identical content; only
    /// their titles, labels and the backing filter arrays differ, so one class serves both.
    /// Checkbox changes commit immediately to the arrays returned by the supplied accessors.
    /// </summary>
    public class GearFilterOptionsDialog
    {
        private const float WinPad = 16f;

        private static readonly string[] RarityKeys =
        {
            UITextKey.RarityNormal, UITextKey.RarityUncommon, UITextKey.RarityRare,
            UITextKey.RarityEpic, UITextKey.RarityLegendary
        };

        private readonly Stage _stage;
        private readonly string _titleKey;
        private readonly string _rarityLabelKey;
        private readonly string _rarityTooltipKey;
        private readonly string _typeLabelKey;
        private readonly string _typeTooltipKey;
        private readonly Func<bool[]> _rarityAccessor;
        private readonly Func<bool[]> _typeAccessor;

        private readonly CheckBox[] _rarityChecks = new CheckBox[RarityKeys.Length];
        private readonly CheckBox[] _typeChecks = new CheckBox[GearCategoryUtils.Count];

        private Window _window;
        private uint _shownFrame;
        private TextService _textService;

        /// <summary>
        /// Builds the (initially hidden) window and adds it to the stage. The filter arrays are
        /// resolved lazily through accessors because the owning service may not be registered yet.
        /// </summary>
        public GearFilterOptionsDialog(Stage stage, string titleKey, string rarityLabelKey, string rarityTooltipKey,
            string typeLabelKey, string typeTooltipKey, Func<bool[]> rarityAccessor, Func<bool[]> typeAccessor)
        {
            _stage = stage;
            _titleKey = titleKey;
            _rarityLabelKey = rarityLabelKey;
            _rarityTooltipKey = rarityTooltipKey;
            _typeLabelKey = typeLabelKey;
            _typeTooltipKey = typeTooltipKey;
            _rarityAccessor = rarityAccessor;
            _typeAccessor = typeAccessor;
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
            _window = new Window(GetText(_titleKey), skin, "ph-default");
            _window.SetMovable(false);
            _window.SetResizable(false);

            var content = new Table();
            content.Pad(WinPad);

            content.Add(new HoverableLabel(GetText(_rarityLabelKey), skin, "ph-default", GetText(_rarityTooltipKey), _stage))
                .Left().SetPadBottom(4f);
            content.Row();

            var rarityTable = new Table();
            for (int i = 0; i < RarityKeys.Length; i++)
            {
                int rarityIndex = i;
                var check = new CheckBox(GetText(RarityKeys[i]), skin, "ph-default");
                check.IsChecked = true;
                check.OnChanged += (isChecked) =>
                {
                    var flags = _rarityAccessor?.Invoke();
                    if (flags != null && rarityIndex < flags.Length)
                        flags[rarityIndex] = isChecked;
                };
                _rarityChecks[i] = check;
                rarityTable.Add(check).Left().SetPadRight(12f).SetPadBottom(4f);

                // Two rows (Normal/Uncommon/Rare, then Epic/Legendary) so the window isn't widened
                if (i == 2)
                    rarityTable.Row();
            }
            content.Add(rarityTable).Left().SetPadBottom(12f);
            content.Row();

            content.Add(new HoverableLabel(GetText(_typeLabelKey), skin, "ph-default", GetText(_typeTooltipKey), _stage))
                .Left().SetPadBottom(4f);
            content.Row();

            var typeTable = new Table();
            for (int i = 0; i < GearCategoryUtils.Count; i++)
            {
                int typeIndex = i;
                var check = new CheckBox(GetText(GearCategoryUtils.GetDisplayNameKey((GearCategory)i)), skin, "ph-default");
                check.IsChecked = true;
                check.OnChanged += (isChecked) =>
                {
                    var flags = _typeAccessor?.Invoke();
                    if (flags != null && typeIndex < flags.Length)
                        flags[typeIndex] = isChecked;
                };
                _typeChecks[i] = check;
                typeTable.Add(check).Left().SetPadRight(12f).SetPadBottom(4f);

                if (i == 2)
                    typeTable.Row();
            }
            content.Add(typeTable).Left();
            content.Row();

            var buttonRow = new Table();
            var selectAllButton = new TextButton(GetText(UITextKey.ButtonSelectAll), skin, "ph-default");
            selectAllButton.OnClicked += (_) => SetAll(true);
            buttonRow.Add(selectAllButton).Width(110f).SetPadRight(8f);

            var deselectAllButton = new TextButton(GetText(UITextKey.ButtonDeselectAll), skin, "ph-default");
            deselectAllButton.OnClicked += (_) => SetAll(false);
            buttonRow.Add(deselectAllButton).Width(110f).SetPadRight(8f);

            var closeButton = new TextButton(GetText(UITextKey.ButtonClose), skin, "ph-default");
            closeButton.ClickSoundCategory = ButtonClickCategory.Cancel;
            closeButton.OnClicked += (_) => Hide();
            buttonRow.Add(closeButton).Width(100f);

            content.Add(buttonRow).SetPadTop(12f);

            _window.Add(content).Expand().Fill();
            _window.SetVisible(false);
            _stage.AddElement(_window);
        }

        /// <summary>Syncs the checkboxes from the filter arrays, then shows the window centered on the stage.</summary>
        public void Show()
        {
            SyncFromService();
            _window.Pack();
            _window.SetPosition(
                (_stage.GetWidth() - _window.GetWidth()) / 2f,
                UILayout.CenterY(_window.GetHeight(), _stage.GetHeight(), 0f));
            _window.SetVisible(true);
            _window.ToFront();
            _shownFrame = Time.FrameCount;
        }

        /// <summary>Hides the window.</summary>
        public void Hide()
        {
            _window?.SetVisible(false);
        }

        /// <summary>True while the dialog window is visible.</summary>
        public bool IsVisible() => _window != null && _window.IsVisible();

        /// <summary>Hides the dialog when a click lands outside it without consuming the click. Call once per frame.</summary>
        public void Update()
        {
            if (OutsideClickDismissal.ShouldDismiss(_window, _stage, _shownFrame))
                Hide();
        }

        /// <summary>
        /// Sets every rarity and type flag. Commits to the arrays directly: programmatic IsChecked
        /// assignment does not fire OnChanged (ProgrammaticChangeEvents is off).
        /// </summary>
        private void SetAll(bool allowed)
        {
            var rarityFlags = _rarityAccessor?.Invoke();
            for (int i = 0; i < _rarityChecks.Length; i++)
            {
                if (rarityFlags != null && i < rarityFlags.Length)
                    rarityFlags[i] = allowed;
                if (_rarityChecks[i] != null)
                    _rarityChecks[i].IsChecked = allowed;
            }

            var typeFlags = _typeAccessor?.Invoke();
            for (int i = 0; i < _typeChecks.Length; i++)
            {
                if (typeFlags != null && i < typeFlags.Length)
                    typeFlags[i] = allowed;
                if (_typeChecks[i] != null)
                    _typeChecks[i].IsChecked = allowed;
            }
        }

        /// <summary>Copies the current filter arrays into the checkboxes.</summary>
        public void SyncFromService()
        {
            var rarityFlags = _rarityAccessor?.Invoke();
            if (rarityFlags != null)
            {
                for (int i = 0; i < _rarityChecks.Length && i < rarityFlags.Length; i++)
                    if (_rarityChecks[i] != null)
                        _rarityChecks[i].IsChecked = rarityFlags[i];
            }

            var typeFlags = _typeAccessor?.Invoke();
            if (typeFlags != null)
            {
                for (int i = 0; i < _typeChecks.Length && i < typeFlags.Length; i++)
                    if (_typeChecks[i] != null)
                        _typeChecks[i].IsChecked = typeFlags[i];
            }
        }
    }
}
