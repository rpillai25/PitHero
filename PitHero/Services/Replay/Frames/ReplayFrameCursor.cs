using System.Collections.Generic;

namespace PitHero.Services.Replay.Frames
{
    /// <summary>
    /// The frame viewer's playhead (design §3.2): a tick position advanced by wall time × speed × 60
    /// with fractional carry, in either direction, clamped to [0, Max], skipping recorded pause spans
    /// while playing forward. The tick semantics match <c>ReplayPlaybackService.CurrentTick</c>: the
    /// cursor is the number of completed ticks, so the frame to draw is <see cref="FrameTick"/>
    /// (cursor − 1, floored at 0). Headless; no Nez.
    /// </summary>
    public sealed class ReplayFrameCursor
    {
        private const float TicksPerSecond = 60f;

        private double _carry;

        /// <summary>Completed ticks at the playhead.</summary>
        public long Cursor { get; private set; }
        /// <summary>Highest cursor value allowed.</summary>
        public long Max { get; set; }
        /// <summary>+1 forward, −1 reverse.</summary>
        public int Direction { get; set; } = 1;

        /// <summary>The recorded frame that shows the world at <see cref="Cursor"/>.</summary>
        public long FrameTick => Cursor > 0 ? Cursor - 1 : 0;

        public ReplayFrameCursor(long max)
        {
            Max = max < 0 ? 0 : max;
        }

        /// <summary>Places the playhead (clamped) and drops the fractional carry.</summary>
        public void Seek(long tick)
        {
            Cursor = Clamp(tick);
            _carry = 0.0;
        }

        /// <summary>
        /// Advances by <paramref name="wallDeltaSeconds"/> at <paramref name="speed"/> in the current
        /// direction, then skips a recorded pause span the playhead landed in (forward only).
        /// Returns true when the playhead hit an end of the range.
        /// </summary>
        public bool Advance(float wallDeltaSeconds, float speed, List<ReplayPauseSpan> pauseSpans)
        {
            if (wallDeltaSeconds < 0f) wallDeltaSeconds = 0f;
            _carry += (double)wallDeltaSeconds * speed * TicksPerSecond;
            long whole = (long)_carry;
            if (whole != 0)
            {
                _carry -= whole;
                Cursor = Clamp(Cursor + whole * Direction);
            }
            if (Direction > 0 && pauseSpans != null)
            {
                long skipTo = ReplayPauseSpans.FindSkipTarget(pauseSpans, Cursor);
                if (skipTo > Cursor)
                {
                    Cursor = Clamp(skipTo);
                    _carry = 0.0;
                }
            }
            return Direction > 0 ? Cursor >= Max : Cursor <= 0;
        }

        private long Clamp(long tick)
        {
            if (tick < 0) return 0;
            if (tick > Max) return Max;
            return tick;
        }
    }
}
