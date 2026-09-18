using System.Collections.Generic;
using Nez;
using Nez.UI;
using PitHero.Farming;
using PitHero.Services;
using PitHero.Services.Replay;

namespace PitHero.UI
{
    /// <summary>
    /// "Monster Farm Priority" card docked to the right of the aggregate Monsters window. Lets the
    /// player take the farm claim order off the monsters: while "Monsters Decide" is checked (the
    /// default) the game runs its Water/Tend split and the list is greyed out; unchecked, every
    /// worker claims in the listed order.
    ///
    /// Pure view. It reads FarmTaskCoordinator to render and dispatches PlayerCommands to change it,
    /// never writing the service directly — the sim must not depend on this window ever being opened.
    /// </summary>
    public class MonsterFarmPriorityPanel : Window
    {
        private const float ContentPadding = 6f;

        /// <summary>Fixed content width, so a reorder never re-Packs the card to a new size.</summary>
        public const float PanelWidth = 240f;

        /// <summary>One orderable priority, carrying its ordinal so the order never round-trips through display text.</summary>
        private sealed class FarmPriorityItem
        {
            public readonly FarmPriorityKind Kind;
            private readonly string _caption;

            public FarmPriorityItem(FarmPriorityKind kind, string caption)
            {
                Kind = kind;
                _caption = caption;
            }

            public override string ToString() => _caption;
        }

        private readonly HoverableCheckBox _decideCheck;
        private readonly ReorderableTableList<FarmPriorityItem> _list;
        private readonly List<FarmPriorityItem> _items;
        private readonly FarmPriorityItem[] _byKind = new FarmPriorityItem[FarmTaskCoordinator.PriorityCount];

        private TextService _textService;

        public MonsterFarmPriorityPanel(Skin skin, Stage stage) : base("", skin)
        {
            SetMovable(false);
            SetResizable(false);
            SetKeepWithinStage(false);

            GetTitleLabel().SetText(GetText(UITextKey.WindowMonsterFarmPriority));

            _byKind[(int)FarmPriorityKind.Water]   = new FarmPriorityItem(FarmPriorityKind.Water, GetText(UITextKey.FarmPriorityWater));
            _byKind[(int)FarmPriorityKind.Till]    = new FarmPriorityItem(FarmPriorityKind.Till, GetText(UITextKey.FarmPriorityTill));
            _byKind[(int)FarmPriorityKind.Plant]   = new FarmPriorityItem(FarmPriorityKind.Plant, GetText(UITextKey.FarmPriorityPlant));
            _byKind[(int)FarmPriorityKind.Harvest] = new FarmPriorityItem(FarmPriorityKind.Harvest, GetText(UITextKey.FarmPriorityHarvest));

            _items = new List<FarmPriorityItem>(FarmTaskCoordinator.PriorityCount);
            for (int i = 0; i < _byKind.Length; i++)
                _items.Add(_byKind[i]);

            _decideCheck = new HoverableCheckBox(GetText(UITextKey.MonsterFarmPriorityMonstersDecide), skin,
                GetText(UITextKey.MonsterFarmPriorityMonstersDecideTooltip), stage);
            _decideCheck.IsChecked = true; // matches FarmTaskCoordinator.MonstersDecide's default
            _decideCheck.OnChanged += OnDecideChanged;

            // ReorderableTableList mutates _items in place, so this same instance must be reused for
            // the life of the panel — SyncFromCoordinator refills it rather than replacing it.
            _list = new ReorderableTableList<FarmPriorityItem>(skin, _items, OnReordered);
            _list.SetGrayed(true); // checked by default, so the list starts disabled

            var content = new Table();
            content.Top().Left();
            content.Add(_decideCheck).Left().SetPadBottom(6f);
            content.Row();
            content.Add(_list).Left().Width(PanelWidth);

            Add(content).Expand().Fill().Pad(ContentPadding);
            SetVisible(false);
            PackFixedWidth();
        }

        /// <summary>
        /// Mirrors the simulation into the controls. Called whenever the aggregate roster refreshes.
        ///
        /// No guard flag is needed and none should be added: Button.IsChecked assigns through
        /// ProgrammaticChangeEvents (false by default) so it never raises OnChanged, and
        /// ReorderableTableList.Rebuild/SetGrayed never raise OnReordered. Nothing here is recorded
        /// as a player action. The flip side is that the grey state must be applied explicitly,
        /// because setting IsChecked will not do it for us.
        /// </summary>
        public void SyncFromCoordinator()
        {
            // Core.Services?. alone still throws with no Core instance (headless), hence the guard.
            var coordinator = Core.Instance != null ? Core.Services.GetService<FarmTaskCoordinator>() : null;
            if (coordinator == null)
                return; // headless or pre-scene: keep the constructor defaults

            _decideCheck.IsChecked = coordinator.MonstersDecide;

            var order = new int[FarmTaskCoordinator.PriorityCount];
            coordinator.CopyPriorityOrder(order);
            _items.Clear();
            for (int i = 0; i < order.Length; i++)
                _items.Add(_byKind[order[i]]);

            _list.Rebuild();
            SetControlsActive(!coordinator.MonstersDecide);
            PackFixedWidth();
        }

        private void OnDecideChanged(bool isChecked)
        {
            PlayerCommandService.Dispatch(PlayerCommand.Flag(PlayerCommandType.SetFarmMonstersDecide, isChecked));
            SetControlsActive(!isChecked);
        }

        private void OnReordered(int oldIndex, int newIndex, FarmPriorityItem item)
        {
            // _items already holds the resulting order; send it whole so the command is absolute.
            PlayerCommandService.Dispatch(new PlayerCommand(PlayerCommandType.SetFarmPriorityOrder,
                (int)_items[0].Kind, (int)_items[1].Kind, (int)_items[2].Kind, (int)_items[3].Kind));
            PackFixedWidth();
        }

        /// <summary>Greys and disables the order list while the monsters are deciding.</summary>
        private void SetControlsActive(bool active)
        {
            _list.SetGrayed(!active);
        }

        /// <summary>
        /// Packs, then pins the width back. PositionWindow derives the roster's X from this card's
        /// width every frame, so letting Pack() size it would slide the whole window sideways
        /// whenever a row swap changed the widest caption.
        /// </summary>
        private void PackFixedWidth()
        {
            Pack();
            SetSize(PanelWidth + ContentPadding * 2f, GetHeight());
        }

        private string GetText(string key)
        {
            if (_textService == null && Core.Services != null)
                _textService = Core.Services.GetService<TextService>();
            return _textService?.DisplayText(TextType.UI, key) ?? key;
        }
    }
}
