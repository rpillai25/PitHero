using Microsoft.Xna.Framework;
using Nez;
using Nez.UI;
using PitHero.Services;
using PitHero.Services.Replay;

namespace PitHero.UI
{
    /// <summary>
    /// Bottom-of-screen replay transport: Exit, Play/Pause, speed cycle, a scrub slider with the
    /// current/total time and a status label (seeking progress, end of replay, divergence). Lives on
    /// the UI stage for the scene's lifetime and is shown only while a replay is active. The slider
    /// commits on release so dragging previews the target time without seeking every frame.
    /// </summary>
    public class ReplayScrubberPanel : Window
    {
        private readonly Skin _skin;
        private TextService _textService;

        private TextButton _exitButton;
        private TextButton _playPauseButton;
        private TextButton _speedButton;
        private EnhancedSlider _slider;
        private Label _timeLabel;
        private Label _statusLabel;

        private long _lastShownTick = -1;
        private long _lastShownTotal = -1;
        private ReplayPlaybackState _lastShownState = ReplayPlaybackState.Idle;
        private int _lastShownSpeedIndex = -1;
        private long _lastShownDivergence = -2;
        private int _lastSeekPercent = -1;
        private bool _previewing;

        private const float ButtonWidth = 70f;
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

            _playPauseButton = new TextButton(GetText(UITextKey.ButtonReplayPause), skin, "ph-default");
            _playPauseButton.OnClicked += (_) => ReplayPlaybackService.Current?.TogglePause();

            _speedButton = new TextButton(string.Format(GetText(UITextKey.ReplaySpeedFormat), GameConfig.ReplaySpeedSteps[0]), skin, "ph-default");
            _speedButton.OnClicked += (_) => ReplayPlaybackService.Current?.CycleSpeed();

            _slider = new EnhancedSlider(0f, 1f, 1f, false, skin, null, useDeferredCommit: true);
            _slider.OnChanged += OnSliderChanged;
            _slider.OnValueCommitted += OnSliderCommitted;

            _timeLabel = new Label(string.Format(GetText(UITextKey.ReplayTimeFormat), "00:00", "00:00"), skin, "ph-default");
            _statusLabel = new Label(GetText(UITextKey.ReplayNoDivergence), skin, "ph-default");

            Add(_exitButton).Width(ButtonWidth).Height(ButtonHeight).SetPadRight(6f);
            Add(_playPauseButton).Width(ButtonWidth).Height(ButtonHeight).SetPadRight(6f);
            Add(_speedButton).Width(44f).Height(ButtonHeight).SetPadRight(10f);
            Add(_slider).Expand().Fill().Height(ButtonHeight).SetPadRight(10f);
            Add(_timeLabel).SetPadRight(10f);
            Add(_statusLabel);

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

        private void OnSliderChanged(float value)
        {
            if (!_slider.IsDragging())
                return;
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

            long total = playback.TotalTicks;
            if (total != _lastShownTotal)
            {
                _lastShownTotal = total;
                _slider.SetMinMax(0f, total > 0 ? total : 1f);
            }

            long tick = playback.CurrentTick;
            if (tick > total) tick = total;
            if (!_slider.IsDragging() && !_previewing && tick != _lastShownTick)
            {
                _lastShownTick = tick;
                _slider.SetValue(tick);
                _timeLabel.SetText(string.Format(GetText(UITextKey.ReplayTimeFormat),
                    ReplayTimeFormatter.FormatTicks(tick), ReplayTimeFormatter.FormatTicks(total)));
            }

            if (playback.SpeedIndex != _lastShownSpeedIndex)
            {
                _lastShownSpeedIndex = playback.SpeedIndex;
                _speedButton.SetText(string.Format(GetText(UITextKey.ReplaySpeedFormat), playback.Speed));
            }

            var state = playback.State;
            if (state != _lastShownState)
            {
                _lastShownState = state;
                _playPauseButton.SetText(GetText(state == ReplayPlaybackState.Playing ? UITextKey.ButtonReplayPause : UITextKey.ButtonReplayPlay));
                _playPauseButton.SetDisabled(state == ReplayPlaybackState.Seeking || state == ReplayPlaybackState.Starting);
                _lastSeekPercent = -1;
                _lastShownDivergence = -2;
            }

            UpdateStatusLabel(playback, state);
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
                _statusLabel.SetText(string.Format(GetText(UITextKey.ReplayDivergenceAt), ReplayTimeFormatter.FormatTicks(divergence)));
            else if (state == ReplayPlaybackState.AtEnd)
                _statusLabel.SetText(GetText(UITextKey.ReplayEndReached));
            else
                _statusLabel.SetText(GetText(UITextKey.ReplayNoDivergence));
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
