using System;
using System.Collections.Generic;

namespace PitHero.Services.Replay.Frames
{
    /// <summary>One console line as it was emitted: the tick and its segments (text, color, item name).</summary>
    public readonly struct RecordedConsoleLine
    {
        public readonly long Tick;
        public readonly ConsoleSegment[] Segments;

        public RecordedConsoleLine(long tick, ConsoleSegment[] segments)
        {
            Tick = tick;
            Segments = segments;
        }
    }

    /// <summary>
    /// The session's console lines in tick order, kept in memory beside the frame stream so the
    /// viewer can rebuild the event console at any tick without inflating chunks (a session emits a
    /// few lines a minute, so even a day is a few thousand entries). The recorder appends at its
    /// console hook and truncates with the stream; a saved-replay reader (issue #429) rebuilds it from
    /// the chunks' console events. Headless; no Nez.
    /// </summary>
    public sealed class RecordedConsoleLog
    {
        private readonly List<RecordedConsoleLine> _lines = new List<RecordedConsoleLine>(1024);

        public int Count => _lines.Count;

        public RecordedConsoleLine this[int index] => _lines[index];

        /// <summary>Appends a line; ticks must not decrease (a late line is clamped to the last tick).</summary>
        public void Add(long tick, ConsoleSegment[] segments)
        {
            if (segments == null)
                return;
            if (_lines.Count > 0 && tick < _lines[_lines.Count - 1].Tick)
                tick = _lines[_lines.Count - 1].Tick;
            _lines.Add(new RecordedConsoleLine(tick, segments));
        }

        /// <summary>Number of lines emitted at or before <paramref name="tick"/> (binary search).</summary>
        public int CountAtOrBefore(long tick)
        {
            int lo = 0, hi = _lines.Count;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (_lines[mid].Tick <= tick)
                    lo = mid + 1;
                else
                    hi = mid;
            }
            return lo;
        }

        /// <summary>Drops every line after <paramref name="tick"/> (Time Travel).</summary>
        public void TruncateAfter(long tick)
        {
            int keep = CountAtOrBefore(tick);
            if (keep < _lines.Count)
                _lines.RemoveRange(keep, _lines.Count - keep);
        }

        public void Clear() => _lines.Clear();
    }
}
