using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Nez;
using Nez.UI;
using PitHero.Services;

namespace PitHero.UI
{
    /// <summary>Read-only MMO-style event log panel displayed in the lower-right corner of the screen.</summary>
    public class EventConsolePanel : Table
    {
        private const int MaxEvents = 50;

        private readonly List<string> _events;
        private readonly Table _logTable;
        private readonly ScrollPane _scrollPane;
        private readonly Skin _skin;
        private readonly GameEventService _eventService;
        private bool _scrollToBottom;

        /// <summary>
        /// Initializes the EventConsolePanel, wires up the log table inside a scroll pane,
        /// and subscribes to the provided GameEventService.
        /// </summary>
        public EventConsolePanel(Skin skin, GameEventService eventService) : base()
        {
            _skin = skin;
            _eventService = eventService;
            _events = new List<string>(MaxEvents);

            _logTable = new Table();
            _logTable.Top().Left();

            _scrollPane = new ScrollPane(_logTable, skin, "ph-default");
            _scrollPane.SetScrollingDisabled(true, false);
            _scrollPane.SetFadeScrollBars(false);

            SetBackground(new PrimitiveDrawable(new Color(0, 0, 0, 180)));
            Add(_scrollPane).Width(480f).Height(120f).Expand().Fill();

            _eventService.OnEvent += OnEventReceived;
        }

        /// <summary>Unsubscribes from the GameEventService to prevent stale listeners after scene unload.</summary>
        public void Dispose()
        {
            _eventService.OnEvent -= OnEventReceived;
        }

        private void OnEventReceived(string message)
        {
            if (_events.Count >= MaxEvents)
            {
                _events.RemoveAt(0);
                _events.Add(message);
                RebuildLog();
            }
            else
            {
                _events.Add(message);
                AppendLabel(message);
            }

            _scrollToBottom = true;
        }

        /// <summary>Scrolls to the bottom after layout has been validated so _maxY is current.</summary>
        public override void Draw(Batcher batcher, float parentAlpha)
        {
            if (_scrollToBottom)
            {
                Validate();
                _scrollPane.Validate();
                _scrollPane.SetScrollPercentY(1f);
                _scrollPane.UpdateVisualScroll();
                _scrollToBottom = false;
            }
            base.Draw(batcher, parentAlpha);
        }

        private void AppendLabel(string message)
        {
            var label = new Label(message, _skin, "console-label");
            label.SetWrap(true);
            _logTable.Add(label).Pad(2f).Left().SetExpandX().SetFillX();
            _logTable.Row();
        }

        private void RebuildLog()
        {
            _logTable.Clear();
            for (int i = 0; i < _events.Count; i++)
                AppendLabel(_events[i]);
        }
    }
}
