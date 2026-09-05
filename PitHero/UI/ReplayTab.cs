using System.Collections.Generic;
using Nez;
using Nez.UI;
using PitHero.Services;
using PitHero.Services.Replay;

namespace PitHero.UI
{
    /// <summary>
    /// Content of the Settings window's Replay tab: a scrolling, selectable list of saved replays
    /// plus Play Selected / Delete / Save Session Replay / Replay Current Session. Starting a
    /// replay goes through a confirmation (it interrupts the live session) and then closes the
    /// settings window through its normal path before handing the recording to playback.
    /// </summary>
    public class ReplayTab
    {
        private readonly Skin _skin;
        private readonly Stage _stage;
        private readonly SettingsUI _settingsUI;
        private TextService _textService;

        private Table _rowsTable;
        private ScrollPane _scrollPane;
        private Label _selectedLabel;
        private TextButton _playButton;
        private TextButton _deleteButton;
        private List<ReplayFileInfo> _entries = new List<ReplayFileInfo>();
        private int _selectedIndex = -1;
        private ConfirmationDialog _confirmDialog;
        private MessageDialog _messageDialog;

        private const float RowHeight = 40f;
        private const float RowWidth = 400f;
        private const float RowPad = 3f;
        private const float ButtonRowHeight = 32f;    // tall enough for two text lines; single-line labels center vertically
        private const float TwoLineButtonPad = 16f;   // horizontal room around the wider of the two lines

        /// <summary>
        /// A button whose label wraps at its last space onto two lines. The button is made just wide
        /// enough for the wider line, which is narrower than the whole text, so Nez's word wrap breaks
        /// exactly there ("Replay Current" / "Session").
        /// </summary>
        private TextButton MakeTwoLineButton(string text, out float width)
        {
            var button = new TextButton(text, _skin, "ph-default");
            var label = button.GetLabel();
            label.SetWrap(true);
            label.SetAlignment(Align.Center);

            int split = text.LastIndexOf(' ');
            string line1 = split > 0 ? text.Substring(0, split) : text;
            string line2 = split > 0 ? text.Substring(split + 1) : string.Empty;
            float widest = System.Math.Max(MeasureText(line1), MeasureText(line2));
            float full = MeasureText(text);
            // The button's background insets come out of the cell width before the label sees it
            float insets = button.GetPadX();
            width = widest + TwoLineButtonPad + insets;
            float unbroken = full + insets;
            if (width >= unbroken)
                width = unbroken - 1f; // must be narrower than the unbroken text or it will not wrap
            return button;
        }

        private float MeasureText(string text)
        {
            return new Label(text, _skin, "ph-default").PreferredWidth;
        }

        /// <summary>Creates the tab content builder.</summary>
        public ReplayTab(Skin skin, Stage stage, SettingsUI settingsUI)
        {
            _skin = skin;
            _stage = stage;
            _settingsUI = settingsUI;
        }

        private TextService GetTextService()
        {
            if (_textService == null && Core.Services != null)
                _textService = Core.Services.GetService<TextService>();
            return _textService;
        }

        private string GetText(string key)
        {
            return GetTextService()?.DisplayText(TextType.UI, key) ?? key;
        }

        /// <summary>Builds the tab's widgets into <paramref name="tab"/>.</summary>
        public void Build(Tab tab)
        {
            var content = new Table();
            content.PadLeft(10f).PadRight(10f);

            _rowsTable = new Table();
            _scrollPane = new ScrollPane(_rowsTable, _skin, "ph-default");
            _scrollPane.SetScrollingDisabled(true, false);
            _scrollPane.SetFadeScrollBars(false);
            content.Add(_scrollPane).Expand().Fill().SetPadTop(8f).SetPadBottom(6f);
            content.Row();

            _selectedLabel = new Label(GetText(UITextKey.ReplaySelectedNone), _skin, "ph-default");
            content.Add(_selectedLabel).Left().SetPadBottom(6f);
            content.Row();

            var buttons = new Table();
            _playButton = new TextButton(GetText(UITextKey.ButtonReplayPlaySelected), _skin, "ph-default");
            _playButton.OnClicked += (_) => OnPlaySelected();
            _deleteButton = new TextButton(GetText(UITextKey.ButtonReplayDelete), _skin, "ph-default");
            _deleteButton.ClickSoundCategory = ButtonClickCategory.Cancel;
            _deleteButton.OnClicked += (_) => OnDeleteSelected();
            // The two long labels wrap onto two lines ("Save Session" / "Replay") so the row fits the window
            var saveButton = MakeTwoLineButton(GetText(UITextKey.ButtonReplaySaveSession), out float saveWidth);
            saveButton.OnClicked += (_) => OnSaveSession();
            var replayCurrentButton = MakeTwoLineButton(GetText(UITextKey.ButtonReplayCurrentSession), out float replayCurrentWidth);
            replayCurrentButton.OnClicked += (_) => OnReplayCurrent();

            buttons.Add(_playButton).SetMinWidth(100f).Height(ButtonRowHeight).SetPadRight(6f);
            buttons.Add(_deleteButton).SetMinWidth(70f).Height(ButtonRowHeight).SetPadRight(6f);
            buttons.Add(saveButton).Width(saveWidth).Height(ButtonRowHeight).SetPadRight(6f);
            buttons.Add(replayCurrentButton).Width(replayCurrentWidth).Height(ButtonRowHeight);
            content.Add(buttons).Left().SetPadBottom(8f);

            tab.Add(content).Expand().Fill();

            SetSelected(-1);
            Refresh();
        }

        /// <summary>Re-reads the replay directory and rebuilds the rows. Called when the tab is shown and after save/delete.</summary>
        public void Refresh()
        {
            if (_rowsTable == null)
                return;

            var fileService = Core.Services?.GetService<ReplayFileService>();
            _entries = fileService != null ? fileService.Enumerate() : new List<ReplayFileInfo>();

            _rowsTable.ClearChildren();
            if (_entries.Count == 0)
            {
                var empty = new Label(GetText(UITextKey.ReplayListEmpty), _skin, "ph-default");
                empty.SetWrap(true);
                _rowsTable.Add(empty).Width(RowWidth).SetPadTop(8f);
            }
            else
            {
                for (int i = 0; i < _entries.Count; i++)
                {
                    BuildRow(_entries[i], i);
                    _rowsTable.Row();
                }
            }
            _rowsTable.Invalidate();
            _scrollPane.Invalidate();

            SetSelected(_selectedIndex < _entries.Count ? _selectedIndex : -1);
        }

        private void BuildRow(ReplayFileInfo info, int index)
        {
            var rowTable = new Table();

            var title = new Label(string.Format(GetText(UITextKey.ReplayRowTitleFormat), info.HeroName, info.JobName), _skin, "ph-default");
            rowTable.Add(title).Left().SetPadLeft(6f);
            rowTable.Row();

            var when = info.RecordedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            var detail = new Label(string.Format(GetText(UITextKey.ReplayRowDetailFormat),
                when, ReplayTimeFormatter.FormatSeconds(info.DurationSeconds), info.PitLevelAtStart), _skin, "ph-default");
            rowTable.Add(detail).Left().SetPadLeft(6f);

            var rowButton = new TextButton("", _skin, "ph-default");
            rowButton.ClearChildren();
            rowButton.Add(rowTable).Expand().Fill().Left();
            rowButton.SetSize(RowWidth, RowHeight);

            int captured = index;
            rowButton.OnClicked += (_) => SetSelected(captured);

            _rowsTable.Add(rowButton).Width(RowWidth).Height(RowHeight).SetPadBottom(RowPad);
        }

        private void SetSelected(int index)
        {
            _selectedIndex = index;
            bool has = index >= 0 && index < _entries.Count;
            _selectedLabel?.SetText(has
                ? string.Format(GetText(UITextKey.ReplaySelectedFormat), _entries[index].FileName)
                : GetText(UITextKey.ReplaySelectedNone));
            _playButton?.SetDisabled(!has);
            _deleteButton?.SetDisabled(!has);
        }

        private void OnPlaySelected()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _entries.Count)
                return;
            var entry = _entries[_selectedIndex];
            ShowConfirm(GetText(UITextKey.DialogConfirmReplay), GetText(UITextKey.ConfirmReplayInterruptMessage), () =>
            {
                var fileService = Core.Services?.GetService<ReplayFileService>();
                var data = fileService?.Load(entry.FileName);
                if (data == null)
                {
                    Refresh();
                    return;
                }
                // Close and release the pause on the record BEFORE playback snapshots the live
                // session as its return point, so the return trip lands unpaused
                _settingsUI?.ForceCloseSettings();
                ReleasePausesOnRecord();
                StartPlayback(data, isCurrentSession: false);
            });
        }

        private void OnReplayCurrent()
        {
            var recorder = ReplayRecorder.Current;
            if (recorder == null)
                return;
            ShowConfirm(GetText(UITextKey.DialogConfirmReplay), GetText(UITextKey.ConfirmReplayInterruptMessage), () =>
            {
                var current = ReplayRecorder.Current;
                if (current == null)
                    return;
                // Close first and release the settings pause ON THE RECORD: the scene is about to be
                // torn down, so the queued unpause would never drain and the snapshot would end frozen
                _settingsUI?.ForceCloseSettings();
                ReleasePausesOnRecord();
                StartPlayback(current.Snapshot(SimulationClock.CurrentTick), isCurrentSession: true);
            });
        }

        /// <summary>Applies and records pause releases immediately (the normal drain will not run before the scene swap).</summary>
        private static void ReleasePausesOnRecord()
        {
            var commands = PlayerCommandService.Current;
            if (commands == null)
                return;
            commands.ApplyNow(PlayerCommand.Flag(PlayerCommandType.SetManualPause, false));
            commands.ApplyNow(PlayerCommand.Flag(PlayerCommandType.SetFarmModePause, false));
        }

        private void StartPlayback(ReplayData data, bool isCurrentSession)
        {
            var playback = ReplayPlaybackService.Current;
            if (playback == null)
                return;
            _settingsUI?.ForceCloseSettings();
            playback.Start(data, isCurrentSession);
        }

        private void OnSaveSession()
        {
            var recorder = ReplayRecorder.Current;
            var fileService = Core.Services?.GetService<ReplayFileService>();
            if (recorder == null || fileService == null)
                return;
            string fileName = fileService.Save(recorder.Snapshot(SimulationClock.CurrentTick));
            string message = fileName != null
                ? string.Format(GetText(UITextKey.ReplaySavedMessage), fileName)
                : GetText(UITextKey.ReplaySaveFailedMessage);
            ShowMessage(GetText(UITextKey.DialogReplaySaved), message);
            Refresh();
        }

        private void OnDeleteSelected()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _entries.Count)
                return;
            var entry = _entries[_selectedIndex];
            ShowConfirm(GetText(UITextKey.DialogConfirmDeleteReplay), GetText(UITextKey.ConfirmDeleteReplayMessage), () =>
            {
                Core.Services?.GetService<ReplayFileService>()?.Delete(entry.FileName);
                _selectedIndex = -1;
                Refresh();
            });
        }

        private void ShowConfirm(string title, string message, System.Action onYes)
        {
            _confirmDialog = new ConfirmationDialog(title, message, _skin, onYes);
            _confirmDialog.YesButton.SuppressGlobalClick = true;
            _confirmDialog.Show(_stage);
        }

        private void ShowMessage(string title, string message)
        {
            _messageDialog = new MessageDialog(title, message, _skin);
            _messageDialog.Show(_stage);
        }
    }
}
