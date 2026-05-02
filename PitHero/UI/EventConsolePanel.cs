using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Nez;
using Nez.BitmapFonts;
using Nez.UI;
using PitHero.Services;

namespace PitHero.UI
{
    /// <summary>Read-only MMO-style event log panel displayed in the lower-right corner of the screen.</summary>
    public class EventConsolePanel : Table
    {
        private const int MaxEvents = 50;

        private readonly List<ConsoleSegment[]> _events;
        private readonly Table _logTable;
        private readonly ScrollPane _scrollPane;
        private readonly BitmapFont _consoleFont;
        private readonly GameEventService _eventService;
        private bool _scrollToBottom;

        /// <summary>Fires whenever a new event is added to the log, including while hidden.</summary>
        public event Action OnNewEvent;

        private float _baseX;
        private float _baseY;
        private float _slideOffsetY;

        /// <summary>The resting X position before any slide offset is applied.</summary>
        public float BaseX => _baseX;
        /// <summary>The resting Y position before any slide offset is applied.</summary>
        public float BaseY => _baseY;

        public EventConsolePanel(Skin skin, GameEventService eventService) : base()
        {
            _eventService = eventService;
            _events = new List<ConsoleSegment[]>(MaxEvents);
            _consoleFont = skin.Get<LabelStyle>("console-label").Font;

            _logTable = new Table();
            _logTable.Top().Left();

            _scrollPane = new ScrollPane(_logTable, skin, "ph-default");
            _scrollPane.SetScrollingDisabled(true, false);
            _scrollPane.SetFadeScrollBars(false);

            SetBackground(new PrimitiveDrawable(new Color(0, 0, 0, 180)));
            Add(_scrollPane).Width(480f).Height(120f).Expand().Fill();

            _eventService.OnEvent += OnEventReceived;
        }

        /// <summary>
        /// Sets a visual display scale applied via Group transform. Use 1f for normal mode, 1.5f for half-window mode.
        /// The panel's layout footprint stays the same; only the rendered output is scaled.
        /// </summary>
        public void SetDisplayScale(float scale)
        {
            SetTransform(scale != 1f);
            SetScale(scale);
        }

        /// <summary>Sets the resting position and re-applies the current slide offset.</summary>
        public void SetBasePosition(float x, float y)
        {
            _baseX = x;
            _baseY = y;
            SetPosition(x, y + _slideOffsetY);
        }

        /// <summary>Applies a vertical slide offset (positive = moves downward off screen). Called by SettingsUI for auto-hide animation.</summary>
        public void SetSlideOffsetY(float offsetY)
        {
            _slideOffsetY = offsetY;
            SetPosition(_baseX, _baseY + offsetY);
        }

        /// <summary>Unsubscribes from the GameEventService to prevent stale listeners after scene unload.</summary>
        public void Dispose()
        {
            _eventService.OnEvent -= OnEventReceived;
        }

        private void OnEventReceived(ConsoleSegment[] segments)
        {
            if (_events.Count >= MaxEvents)
            {
                _events.RemoveAt(0);
                _events.Add(segments);
                RebuildLog();
            }
            else
            {
                _events.Add(segments);
                AppendRow(segments);
            }

            _scrollToBottom = true;
            OnNewEvent?.Invoke();
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

        private void AppendRow(ConsoleSegment[] segments)
        {
            var rowTable = new Table();
            rowTable.Left();
            for (int i = 0; i < segments.Length; i++)
            {
                // Each label gets its own LabelStyle instance so colors are independent.
                // Sharing a skin style and calling SetFontColor mutates the shared object,
                // causing all labels from that style to render in the last-set color.
                var label = new Label(segments[i].Text, new LabelStyle(_consoleFont, segments[i].Color));
                if (i == segments.Length - 1)
                    rowTable.Add(label).Left().SetExpandX().SetFillX();
                else
                    rowTable.Add(label).Left();
            }
            _logTable.Add(rowTable).Pad(2f).Left().SetExpandX().SetFillX();
            _logTable.Row();
        }

        private void RebuildLog()
        {
            _logTable.Clear();
            for (int i = 0; i < _events.Count; i++)
                AppendRow(_events[i]);
        }
    }
}
