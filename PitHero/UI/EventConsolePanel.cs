using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Nez;
using Nez.BitmapFonts;
using Nez.UI;
using PitHero.Services;
using PitHero.Util;
using RolePlayingFramework.Equipment;

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
        private float _layoutHeight = 120f;

        // Scroll hold: suppress auto-scroll until the player has not scrolled for the quiet period.
        private float _secondsSinceUserScroll = GameConfig.ConsoleAutoScrollQuietSeconds; // start "quiet"
        private float _expectedScrollY;    // last scroll amount this panel set or observed
        private bool _forceScrollToBottom; // width-change layout reset bypasses the hold

        // Item-name hover tooltips.
        private readonly Skin _skin;
        private readonly Dictionary<Label, string> _labelItemNames = new Dictionary<Label, string>(64);
        private ItemCardTooltip _itemTooltip;
        private int _hoverCheckCounter;
        private string _hoveredItemName;
        private IItem _hoveredItem;

        /// <summary>Fires whenever a new event is added to the log, including while hidden. Carries the event priority.</summary>
        public event Action<EventPriority> OnNewEvent;

        private float _baseX;
        private float _baseY;
        private float _slideOffsetY;

        /// <summary>The resting X position before any slide offset is applied.</summary>
        public float BaseX => _baseX;
        /// <summary>The resting Y position before any slide offset is applied.</summary>
        public float BaseY => _baseY;

        /// <summary>The actual rendered width (layout width times the current display scale).</summary>
        public float VisualWidth => _layoutWidth * GetScaleX();
        /// <summary>The actual rendered height (layout height times the current display scale).</summary>
        public float VisualHeight => _layoutHeight * GetScaleY();

        public EventConsolePanel(Skin skin, GameEventService eventService) : base()
        {
            _eventService = eventService;
            _skin = skin;
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
            bool widthChanged = width != _layoutWidth;
            _layoutWidth = width;
            _layoutHeight = height;
            _scrollPaneCell.Width(width).Height(height);
            SetSize(width, height);
            InvalidateHierarchy();
            if (widthChanged && _events.Count > 0)
            {
                RebuildLog();
                _scrollToBottom = true;
                // A rebuild at a new width invalidates the player's scroll position — snap regardless of the hold.
                _forceScrollToBottom = true;
            }
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
            HideItemTooltip();
        }

        private void OnEventReceived(ConsoleSegment[] segments, EventPriority priority)
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
            OnNewEvent?.Invoke(priority);
        }

        /// <summary>
        /// Drives the item-name hover tooltip poll. MUST be called from the scene's update phase
        /// (MainGameScene.Update), never from Draw: showing/hiding/ToFront-ing the tooltip mutates
        /// the stage root's children list, which crashes Group.DrawChildren mid-iteration.
        /// </summary>
        public void Update()
        {
            UpdateItemHover();
        }

        /// <summary>
        /// Scrolls to the bottom after layout has been validated so _maxY is current — but only
        /// when the player has not scrolled the console for the quiet period.
        /// </summary>
        public override void Draw(Batcher batcher, float parentAlpha)
        {
            // Quiet timer runs on unscaled time so pausing the game doesn't freeze the hold.
            if (_secondsSinceUserScroll < GameConfig.ConsoleAutoScrollQuietSeconds)
                _secondsSinceUserScroll += Time.UnscaledDeltaTime;

            DetectUserScroll();

            // Keep _scrollToBottom pending while suppressed so the console snaps to the latest
            // entry once the quiet period elapses, even if no further event arrives.
            if (_scrollToBottom &&
                (_forceScrollToBottom || _secondsSinceUserScroll >= GameConfig.ConsoleAutoScrollQuietSeconds))
            {
                Validate();
                _scrollPane.Validate();
                _scrollPane.SetScrollPercentY(1f);
                _scrollPane.UpdateVisualScroll();
                _expectedScrollY = _scrollPane.GetScrollY();
                _scrollToBottom = false;
                _forceScrollToBottom = false;
            }

            base.Draw(batcher, parentAlpha);
        }

        /// <summary>
        /// Detects player scrolling by comparing the pane's target scroll amount against the last
        /// value this panel set or observed. Wheel, knob drag and track clicks all mutate the target
        /// amount, while smooth-scroll interpolation only moves the visual amount, so this never
        /// false-positives on animation. Content growth never moves the target amount either; only
        /// a shrink (the 50-event eviction rebuild) can clamp it down, which is exempted below.
        /// </summary>
        private void DetectUserScroll()
        {
            float current = _scrollPane.GetScrollY();
            if (Math.Abs(current - _expectedScrollY) <= 0.5f)
                return;

            float maxY = _scrollPane.GetMaxY();
            if (current >= maxY - 0.5f && _expectedScrollY > maxY)
            {
                // Layout clamp after content shrank (eviction rebuild) — ours, not the player's.
            }
            else if (current >= maxY - 0.5f)
            {
                // Player scrolled back to the bottom: resume auto-scroll immediately.
                _secondsSinceUserScroll = GameConfig.ConsoleAutoScrollQuietSeconds;
            }
            else
            {
                // Player scrolled away from the bottom: restart the quiet period.
                _secondsSinceUserScroll = 0f;
            }
            _expectedScrollY = current;
        }

        /// <summary>Polls for an item-name label under the cursor every few frames and cursor-follows the tooltip every frame.</summary>
        private void UpdateItemHover()
        {
            _hoverCheckCounter++;
            if (_hoverCheckCounter % 5 == 0)
                PerformHoverCheck();
            if (_itemTooltip != null && _itemTooltip.GetContainer().HasParent())
                PositionTooltipAtCursor();
        }

        /// <summary>
        /// Geometric hover poll: hit-tests the stage under the cursor and shows the inventory-style
        /// ItemCardTooltip when the hit resolves to a console label tagged with an item name.
        /// Stage.Hit returns the topmost element (overlapping dialogs win) and ScrollPane.Hit
        /// bounds-checks the pane, so scrolled-out rows can never be hit.
        /// </summary>
        private void PerformHoverCheck()
        {
            var stage = GetStage();
            if (stage == null || !IsVisible() || _slideOffsetY != 0f || !MouseUtils.IsMouseInsideWindow())
            {
                HideItemTooltip();
                return;
            }

            var stagePos = HoverProbe.GetStageMousePosition(stage);
            var hit = stage.Hit(stagePos);
            string itemName = null;
            for (var current = hit; current != null; current = current.GetParent())
            {
                if (current is Label label && _labelItemNames.TryGetValue(label, out itemName))
                {
                    // Self-heal: never trust a detached/hidden label (see HoverProbe docs).
                    if (!HoverProbe.IsLive(label, stage))
                        itemName = null;
                    break;
                }
            }

            if (itemName == null)
            {
                HideItemTooltip();
                return;
            }

            if (itemName != _hoveredItemName)
            {
                // TryCreateItem handles tier-scaled "+N" names via Gear.CreateTierScaledCopy,
                // so scaled drops show their real stats. Cache the instance so ItemCardTooltip's
                // reference-based content cache holds across polls.
                if (!ItemRegistry.TryCreateItem(itemName, out var item))
                {
                    HideItemTooltip();
                    return;
                }
                _hoveredItemName = itemName;
                _hoveredItem = item;
            }

            if (_itemTooltip == null)
            {
                // Dummy zero-size target: the tooltip follows the cursor instead (HeroUI pattern).
                var dummyTarget = new Element();
                dummyTarget.SetSize(0f, 0f);
                _itemTooltip = new ItemCardTooltip(dummyTarget, _skin);
            }

            _itemTooltip.ShowItem(_hoveredItem);
            var container = _itemTooltip.GetContainer();
            if (!container.HasParent())
                stage.AddElement(container);
            container.ToFront();
        }

        /// <summary>
        /// Positions the tooltip beside the cursor, flipping above it when it would run off the
        /// bottom and clamping to the stage — the console sits bottom-right, so both edges overflow otherwise.
        /// </summary>
        private void PositionTooltipAtCursor()
        {
            var stage = GetStage();
            if (stage == null)
                return;

            var container = _itemTooltip.GetContainer();
            container.Validate();

            var stagePos = HoverProbe.GetStageMousePosition(stage);
            float x = stagePos.X + 10f;
            float y = stagePos.Y + 10f;
            float stageWidth = stage.GetWidth();
            float stageHeight = stage.GetHeight();

            if (y + container.GetHeight() > stageHeight)
                y = stagePos.Y - container.GetHeight() - 10f;
            y = Mathf.Clamp(y, 0f, stageHeight - container.GetHeight());
            x = Mathf.Clamp(x, 0f, stageWidth - container.GetWidth());
            container.SetPosition(x, y);
        }

        /// <summary>Removes the tooltip from the stage and clears the hover cache.</summary>
        private void HideItemTooltip()
        {
            _hoveredItemName = null;
            _hoveredItem = null;
            if (_itemTooltip != null && _itemTooltip.GetContainer().HasParent())
                _itemTooltip.GetContainer().Remove();
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
                    if (lineSegs[i].ItemName != null)
                    {
                        // Nez Labels are Touchable.Disabled by construction, so Stage.Hit can never
                        // return one. Item-name labels must be hittable for the hover poll; wheel
                        // events still reach the ScrollPane because Stage.HandleMouseWheel bubbles
                        // from the hit element up the parent chain, and Label is not an IInputListener.
                        label.SetTouchable(Touchable.Enabled);
                        _labelItemNames[label] = lineSegs[i].ItemName;
                    }
                }
                _logTable.Add(rowTable).Pad(2f).Left().SetExpandX().SetFillX();
                _logTable.Row();
            }
        }

        /// <summary>Splits a segment array into multiple lines using whole-word wrapping.</summary>
        private List<ConsoleSegment[]> SplitToLines(ConsoleSegment[] segments)
        {
            float spaceWidth = _consoleFont.MeasureString(" ").X;

            // Flatten all segments into (word, color, itemName) tokens, stripping spaces.
            var tokens = new List<(string Word, Color Color, string ItemName)>(segments.Length * 4);
            for (int s = 0; s < segments.Length; s++)
            {
                var parts = segments[s].Text.Split(' ');
                for (int p = 0; p < parts.Length; p++)
                {
                    if (parts[p].Length > 0)
                        tokens.Add((parts[p], segments[s].Color, segments[s].ItemName));
                }
            }

            var lines = new List<ConsoleSegment[]>(2);
            if (tokens.Count == 0)
            {
                lines.Add(segments);
                return lines;
            }

            // Greedy line-fill: add tokens until width exceeded, then start a new line.
            var lineTokens = new List<(string Word, Color Color, string ItemName)>(tokens.Count);
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

        /// <summary>
        /// Merges consecutive tokens with matching color AND item metadata back into a ConsoleSegment
        /// array, space-separated. Comparing item metadata too keeps a Normal-rarity (white) item name
        /// from fusing with surrounding white literal text, so its label stays individually hoverable.
        /// </summary>
        private static ConsoleSegment[] MergeTokensToSegments(List<(string Word, Color Color, string ItemName)> tokens)
        {
            if (tokens.Count == 0)
                return Array.Empty<ConsoleSegment>();

            var result = new List<ConsoleSegment>(tokens.Count);
            var sb = new StringBuilder();
            Color currentColor = tokens[0].Color;
            string currentItemName = tokens[0].ItemName;
            sb.Append(tokens[0].Word);

            for (int i = 1; i < tokens.Count; i++)
            {
                if (tokens[i].Color == currentColor && tokens[i].ItemName == currentItemName)
                {
                    sb.Append(' ');
                    sb.Append(tokens[i].Word);
                }
                else
                {
                    result.Add(new ConsoleSegment(sb.ToString(), currentColor, currentItemName));
                    sb.Clear();
                    // Leading space acts as the separator between differently-colored labels.
                    // Same-color tokens use the ' ' appended in the branch above.
                    sb.Append(' ');
                    sb.Append(tokens[i].Word);
                    currentColor = tokens[i].Color;
                    currentItemName = tokens[i].ItemName;
                }
            }

            result.Add(new ConsoleSegment(sb.ToString(), currentColor, currentItemName));
            return result.ToArray();
        }

        private void RebuildLog()
        {
            // The old rows' labels are being discarded — drop their tooltip mappings with them.
            _labelItemNames.Clear();
            _logTable.Clear();
            for (int i = 0; i < _events.Count; i++)
                AppendRow(_events[i]);
        }
    }
}
