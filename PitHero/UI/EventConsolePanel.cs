using System;
using System.Collections.Generic;
using System.Text;
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
        private readonly Cell _scrollPaneCell;
        private readonly BitmapFont _consoleFont;
        private readonly GameEventService _eventService;
        private bool _scrollToBottom;
        private float _layoutWidth = 480f;

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
            _scrollPaneCell = Add(_scrollPane).Width(480f).Height(120f).Expand().Fill();

            _eventService.OnEvent += OnEventReceived;
        }

        /// <summary>
        /// Updates the scrollpane cell constraints and the panel's own size so text wraps
        /// correctly at the new width. Call before SetBasePosition when the layout width changes.
        /// </summary>
        public void SetLayoutSize(float width, float height)
        {
            _layoutWidth = width;
            _scrollPaneCell.Width(width).Height(height);
            SetSize(width, height);
            InvalidateHierarchy();
        }

        /// <summary>
        /// Sets a visual display scale applied via Group transform. Use 1f for normal mode, 2f for half-window mode.
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
            var lines = SplitToLines(segments);
            for (int l = 0; l < lines.Count; l++)
            {
                var lineSegs = lines[l];
                var rowTable = new Table();
                rowTable.Left();
                for (int i = 0; i < lineSegs.Length; i++)
                {
                    // Each label gets its own LabelStyle instance so colors are independent.
                    // Sharing a skin style and calling SetFontColor mutates the shared object,
                    // causing all labels from that style to render in the last-set color.
                    var label = new Label(lineSegs[i].Text, new LabelStyle(_consoleFont, lineSegs[i].Color));
                    rowTable.Add(label).Left();
                }
                _logTable.Add(rowTable).Pad(2f).Left().SetExpandX().SetFillX();
                _logTable.Row();
            }
        }

        /// <summary>Splits a segment array into multiple lines using whole-word wrapping.</summary>
        private List<ConsoleSegment[]> SplitToLines(ConsoleSegment[] segments)
        {
            float spaceWidth = _consoleFont.MeasureString(" ").X;

            // Flatten all segments into (word, color) tokens, stripping spaces.
            var tokens = new List<(string Word, Color Color)>(segments.Length * 4);
            for (int s = 0; s < segments.Length; s++)
            {
                var parts = segments[s].Text.Split(' ');
                for (int p = 0; p < parts.Length; p++)
                {
                    if (parts[p].Length > 0)
                        tokens.Add((parts[p], segments[s].Color));
                }
            }

            var lines = new List<ConsoleSegment[]>(2);
            if (tokens.Count == 0)
            {
                lines.Add(segments);
                return lines;
            }

            // Greedy line-fill: add tokens until width exceeded, then start a new line.
            var lineTokens = new List<(string Word, Color Color)>(tokens.Count);
            float lineWidth = 0f;

            for (int t = 0; t < tokens.Count; t++)
            {
                float wordWidth = _consoleFont.MeasureString(tokens[t].Word).X;
                float needed = lineWidth == 0f ? wordWidth : spaceWidth + wordWidth;

                if (lineWidth == 0f || lineWidth + needed <= _layoutWidth)
                {
                    lineTokens.Add(tokens[t]);
                    lineWidth += needed;
                }
                else
                {
                    lines.Add(MergeTokensToSegments(lineTokens));
                    lineTokens.Clear();
                    lineTokens.Add(tokens[t]);
                    lineWidth = wordWidth;
                }
            }

            if (lineTokens.Count > 0)
                lines.Add(MergeTokensToSegments(lineTokens));

            return lines;
        }

        /// <summary>Merges consecutive same-color tokens back into ConsoleSegment array, space-separated.</summary>
        private static ConsoleSegment[] MergeTokensToSegments(List<(string Word, Color Color)> tokens)
        {
            if (tokens.Count == 0)
                return Array.Empty<ConsoleSegment>();

            var result = new List<ConsoleSegment>(tokens.Count);
            var sb = new StringBuilder();
            Color currentColor = tokens[0].Color;
            sb.Append(tokens[0].Word);

            for (int i = 1; i < tokens.Count; i++)
            {
                if (tokens[i].Color == currentColor)
                {
                    sb.Append(' ');
                    sb.Append(tokens[i].Word);
                }
                else
                {
                    result.Add(new ConsoleSegment(sb.ToString(), currentColor));
                    sb.Clear();
                    // Leading space acts as the separator between differently-colored labels.
                    // Same-color tokens use the ' ' appended in the branch above.
                    sb.Append(' ');
                    sb.Append(tokens[i].Word);
                    currentColor = tokens[i].Color;
                }
            }

            result.Add(new ConsoleSegment(sb.ToString(), currentColor));
            return result.ToArray();
        }

        private void RebuildLog()
        {
            _logTable.Clear();
            for (int i = 0; i < _events.Count; i++)
                AppendRow(_events[i]);
        }
    }
}
