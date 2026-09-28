using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Nez;
using Nez.UI;
using PitHero.Services;
using PitHero.Services.Replay;

namespace PitHero.UI
{
    /// <summary>
    /// Bottom-of-screen replay transport: Exit, Play/Pause, rewind (frame view only), speed cycle, a
    /// scrub slider with the current/total time and a status label (seeking progress, end of replay,
    /// divergence). Lives on the UI stage for the scene's lifetime and is shown only while a replay is
    /// active. The slider commits on release so dragging previews the target time without seeking every
    /// frame. SHIFT + left arrow rewinds for as long as it is held (issue #431): the camera ignores the
    /// arrow keys while SHIFT is down, so the two never fight over the key.
    /// </summary>
    public class ReplayScrubberPanel : Window
    {
        private readonly Skin _skin;
        private TextService _textService;

        private TextButton _exitButton;
        private TextButton _continueButton;
        private Cell _continueCell;            // collapsed to zero width when time travel is not offered
        private float _continueWidth;
        private ConfirmationDialog _continueDialog;
        private TextButton _playPauseButton;
        private TextButton _rewindButton;
        private Cell _rewindCell;              // collapsed to zero width outside the frame view
        private float _rewindWidth;
        private bool _rewindOffered = true;
        private bool _holdKeyDown;             // SHIFT + left arrow is held: the hold ends when either key comes up
        private TextButton _speedButton;
        private ReplayTimelineSlider _slider;
        private Label _timeLabel;
        private Label _statusLabel;

        private long _lastShownTick = -1;
        private long _lastShownTotal = -1;
        private ReplayPlaybackState _lastShownState = ReplayPlaybackState.Idle;
        private int _lastShownSpeedIndex = -1;
        private long _lastShownDivergence = -2;
        private int _lastSeekPercent = -1;
        private bool _previewing;

        private const float ButtonTextPad = 14f; // horizontal room around a button's label
        private const float EdgePad = 10f;       // gap between the outermost controls and the window edge

        private static float TextButtonWidth(TextButton button)
        {
            return button.GetLabel().PreferredWidth + ButtonTextPad;
        }

        private float MeasureButtonText(string text)
        {
            var probe = new Label(text, _skin, "ph-default");
            return probe.PreferredWidth;
        }

        private const float ButtonHeight = 20f;

        /// <summary>Builds the panel. Positioned by MainGameScene.PositionReplayScrubber.</summary>
        public ReplayScrubberPanel(Skin skin) : base("", skin.Get<WindowStyle>("ph-default"))
        {
            _skin = skin;
            SetMovable(false);
            Pad(4f);

            _exitButton = new TextButton(GetText(UITextKey.ButtonReplayExit), skin, "ph-default");
            _exitButton.ClickSoundCategory = ButtonClickCategory.Cancel;
            _exitButton.OnClicked += (_) => ReplayPlaybackService.Current?.Exit();

            _continueButton = new TextButton(GetText(UITextKey.ButtonReplayContinueHere), skin, "ph-default");
            _continueButton.OnClicked += (_) => ConfirmContinueHere();

            _playPauseButton = new TextButton(GetText(UITextKey.ButtonReplayPause), skin, "ph-default");
            _playPauseButton.OnClicked += (_) => ReplayPlaybackService.Current?.TogglePause();

            _rewindButton = new TextButton(GetText(UITextKey.ButtonReplayRewind), skin, "ph-default");
            _rewindButton.OnClicked += (_) => ReplayPlaybackService.Current?.ToggleRewind();

            _speedButton = new TextButton(string.Format(GetText(UITextKey.ReplaySpeedFormat), GameConfig.SpeedStepLabels[0]), skin, "ph-default");
            _speedButton.OnClicked += (_) => ReplayPlaybackService.Current?.CycleSpeed();

            _slider = new ReplayTimelineSlider(skin, useDeferredCommit: true);
            _slider.OnChanged += OnSliderChanged;
            _slider.OnValueCommitted += OnSliderCommitted;

            _timeLabel = new Label(string.Format(GetText(UITextKey.ReplayTimeFormat), "00:00", "00:00"), skin, "ph-default");
            _statusLabel = new Label(GetText(UITextKey.ReplayNoDivergence), skin, "ph-default");

            // Buttons size to their text (plus breathing room) so no label is clipped; the outer
            // padding keeps the first button and the status label off the window edges
            Add(_exitButton).Width(TextButtonWidth(_exitButton)).Height(ButtonHeight).SetPadLeft(EdgePad).SetPadRight(6f);
            _continueWidth = TextButtonWidth(_continueButton);
            _continueCell = Add(_continueButton).Width(_continueWidth).Height(ButtonHeight).SetPadRight(6f);
            // Play/Pause swaps text: size for the wider of the two so the layout never shifts
            float playPauseWidth = System.Math.Max(TextButtonWidth(_playPauseButton),
                MeasureButtonText(GetText(UITextKey.ButtonReplayPlay)) + ButtonTextPad);
            Add(_playPauseButton).Width(playPauseWidth).Height(ButtonHeight).SetPadRight(6f);
            _rewindWidth = TextButtonWidth(_rewindButton);
            _rewindCell = Add(_rewindButton).Width(_rewindWidth).Height(ButtonHeight).SetPadRight(6f);
            // The speed face grows to "32X" in the frame view: size for the widest label of either ladder
            float speedWidth = TextButtonWidth(_speedButton);
            for (int i = 0; i < GameConfig.ReplayFrameViewSpeedStepLabels.Length; i++)
                speedWidth = System.Math.Max(speedWidth, MeasureButtonText(string.Format(GetText(UITextKey.ReplaySpeedFormat), GameConfig.ReplayFrameViewSpeedStepLabels[i])) + ButtonTextPad);
            Add(_speedButton).Width(speedWidth + 8f).Height(ButtonHeight).SetPadRight(10f);
            Add(_slider).Expand().Fill().Height(ButtonHeight).SetPadRight(10f);
            Add(_timeLabel).SetPadRight(10f);
            // Status strings are kept short (one or two words, "Diverged at m:ss") so the cell's
            // natural width never squeezes the slider; a long status here overruns the buttons
            Add(_statusLabel).SetPadRight(EdgePad);

            SetVisible(false);
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

        /// <summary>Time travel is destructive for the set-aside session, so it always asks first.</summary>
        private void ConfirmContinueHere()
        {
            var playback = ReplayPlaybackService.Current;
            var stage = GetStage();
            if (playback == null || !playback.IsActive || stage == null || !playback.TimeTravelAllowed)
                return;
            if (playback.State == ReplayPlaybackState.Seeking || playback.State == ReplayPlaybackState.Starting)
                return;
            // Continuing from the past discards what happened since
            string message = GetText(UITextKey.ConfirmContinueHereMessage);
            string warning = null;
            if (playback.IsOlderSimulation)
            {
                // Older game version: the world on screen is what this build computed from the old
                // recording, which may not be the game as it was originally played. Say which it is,
                // in red on its own line under the usual message.
                string fidelity = GetText(playback.DivergenceTick >= 0
                    ? UITextKey.ConfirmContinueOlderDiverged
                    : UITextKey.ConfirmContinueOlderInSync);
                warning = string.Format(GetText(UITextKey.ConfirmContinueOlderFormat), fidelity);
            }
            _continueDialog = new ConfirmationDialog(
                GetText(UITextKey.DialogConfirmContinueHere),
                message,
                _skin,
                onYes: () => ReplayPlaybackService.Current?.ContinueFromHere(),
                onNo: null,
                detailContent: null,
                warning: warning,
                warningStyle: "ph-notice"); // buff green: a paragraph in red reads as an alarm
            _continueDialog.YesButton.SuppressGlobalClick = true;
            _continueDialog.Show(stage);
        }

        private void OnSliderChanged(float value)
        {
            if (!_slider.IsPointerHeld)
                return; // programmatic sync, not the user
            _previewing = true;
            var playback = ReplayPlaybackService.Current;
            long total = playback != null ? playback.TotalTicks : 0;
            _timeLabel.SetText(string.Format(GetText(UITextKey.ReplayTimeFormat),
                ReplayTimeFormatter.FormatTicks((long)value), ReplayTimeFormatter.FormatTicks(total)));
        }

        private void OnSliderCommitted(float value)
        {
            _previewing = false;
            var playback = ReplayPlaybackService.Current;
            if (playback == null || !playback.IsActive)
                return;
            long target = (long)value;
            if (target > playback.TotalTicks)
                target = playback.TotalTicks;
            if (target != playback.CurrentTick)
                playback.Seek(target);
            _lastShownTick = -1; // force a label refresh
        }

        /// <summary>Mirrors the playback service into the controls. Called every rendered frame while visible.</summary>
        public void Update()
        {
            var playback = ReplayPlaybackService.Current;
            if (playback == null || !playback.IsActive)
                return;

            // Hold-to-rewind: SHIFT + left arrow, polled here so the order the two keys go down in does not matter
            bool holdKeyDown = (Input.IsKeyDown(Keys.LeftShift) || Input.IsKeyDown(Keys.RightShift)) && Input.IsKeyDown(Keys.Left);
            if (holdKeyDown != _holdKeyDown)
            {
                _holdKeyDown = holdKeyDown;
                if (holdKeyDown)
                    playback.BeginHoldRewind();
                else
                    playback.EndHoldRewind();
            }

            long total = playback.TotalTicks; // the slider ends at the session end
            if (total != _lastShownTotal)
            {
                _lastShownTotal = total;
                _slider.SetMinMax(0f, total > 0 ? total : 1f);
            }

            // While seeking the knob shows the destination, not the ticks racing toward it
            var state0 = playback.State;
            long tick = state0 == ReplayPlaybackState.Seeking || state0 == ReplayPlaybackState.Starting
                ? playback.SeekTarget
                : playback.CurrentTick;
            if (tick > total) tick = total;
            if (!_slider.IsPointerHeld && !_previewing && tick != _lastShownTick)
            {
                _lastShownTick = tick;
                _slider.SetValue(tick);
                _timeLabel.SetText(string.Format(GetText(UITextKey.ReplayTimeFormat),
                    ReplayTimeFormatter.FormatTicks(tick), ReplayTimeFormatter.FormatTicks(total)));
            }

            if (playback.SpeedIndex != _lastShownSpeedIndex)
            {
                _lastShownSpeedIndex = playback.SpeedIndex;
                _speedButton.SetText(string.Format(GetText(UITextKey.ReplaySpeedFormat), playback.SpeedLabel));
            }

            var state = playback.State;
            if (state != _lastShownState)
            {
                _lastShownState = state;
                _playPauseButton.SetText(GetText(state == ReplayPlaybackState.Playing ? UITextKey.ButtonReplayPause : UITextKey.ButtonReplayPlay));
                // Every button is dead while a seek runs (issue #432): an Exit mid-rebuild would hijack a Time
                // Travel's continuation, a second Time Travel or a speed change mean nothing until the seek lands
                bool busy = state == ReplayPlaybackState.Seeking || state == ReplayPlaybackState.Starting;
                _exitButton.SetDisabled(busy);
                _continueButton.SetDisabled(busy);
                _playPauseButton.SetDisabled(busy);
                _rewindButton.SetDisabled(busy);
                _speedButton.SetDisabled(busy);
                SetTimeTravelOffered(playback.TimeTravelAllowed);
                _lastSeekPercent = -1;
                _lastShownDivergence = -2;
            }
            SetRewindOffered(playback.RewindAvailable);

            UpdateStatusLabel(playback, state);
        }

        /// <summary>Shows the rewind button in the frame view, or collapses its cell (a re-simulation cannot run backwards).</summary>
        private void SetRewindOffered(bool offered)
        {
            if (_rewindOffered == offered)
                return;
            _rewindOffered = offered;
            _rewindButton.SetVisible(offered);
            _rewindButton.SetTouchable(offered ? Touchable.Enabled : Touchable.Disabled);
            _rewindCell.Width(offered ? _rewindWidth : 0f).SetPadRight(offered ? 6f : 0f);
            Invalidate();
        }

        /// <summary>The panel left the screen (replay over): let go of any held rewind.</summary>
        public void OnHidden()
        {
            _holdKeyDown = false;
            ReplayPlaybackService.Current?.EndHoldRewind();
        }

        private void UpdateStatusLabel(ReplayPlaybackService playback, ReplayPlaybackState state)
        {
            if (state == ReplayPlaybackState.Starting)
            {
                if (_lastSeekPercent != -100)
                {
                    _lastSeekPercent = -100;
                    _statusLabel.SetText(GetText(UITextKey.ReplayStarting));
                }
                return;
            }
            if (state == ReplayPlaybackState.Seeking)
            {
                int percent = (int)(playback.SeekProgress * 100f);
                if (percent != _lastSeekPercent)
                {
                    _lastSeekPercent = percent;
                    _statusLabel.SetText(string.Format(GetText(UITextKey.ReplaySeekingFormat), percent));
                }
                return;
            }

            long divergence = playback.DivergenceTick;
            if (divergence == _lastShownDivergence && state != ReplayPlaybackState.AtEnd)
                return;
            _lastShownDivergence = divergence;
            if (divergence >= 0)
                _statusLabel.SetText(string.Format(GetText(UITextKey.ReplayDivergenceAt),
                    ReplayTimeFormatter.FormatTicks(divergence),
                    GetText(playback.DivergenceIsDecision ? UITextKey.ReplayDivergenceDecision : UITextKey.ReplayDivergenceState)));
            else if (state == ReplayPlaybackState.AtEnd)
                _statusLabel.SetText(GetText(UITextKey.ReplayEndReached));
            else if (playback.IsOlderSimulation)
                _statusLabel.SetText(GetText(UITextKey.ReplayOlderSimulationStatus));
            else
                _statusLabel.SetText(GetText(UITextKey.ReplayNoDivergence));
        }

        /// <summary>
        /// Shows the Time Travel button, or hides it and collapses its cell so the slider takes the
        /// room: another hero's recording is watch-only and a dead button would only invite clicks.
        /// </summary>
        private void SetTimeTravelOffered(bool offered)
        {
            if (_continueButton.IsVisible() == offered)
                return;
            _continueButton.SetVisible(offered);
            _continueButton.SetTouchable(offered ? Touchable.Enabled : Touchable.Disabled);
            _continueCell.Width(offered ? _continueWidth : 0f).SetPadRight(offered ? 6f : 0f);
            Invalidate();
        }

        /// <summary>Resets cached display state so the next Update repaints everything (on show).</summary>
        public void ResetDisplayCache()
        {
            _lastShownTick = -1;
            _lastShownTotal = -1;
            _lastShownState = ReplayPlaybackState.Idle;
            _lastShownSpeedIndex = -1;
            _lastShownDivergence = -2;
            _lastSeekPercent = -1;
            _previewing = false;
        }
    }
}
