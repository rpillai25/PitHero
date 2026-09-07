using System.Collections.Generic;

namespace PitHero.Services.Replay
{
    /// <summary>A stretch of ticks during which the recorded session was paused (menus open).</summary>
    public struct ReplayPauseSpan
    {
        /// <summary>First tick that ran paused.</summary>
        public long Start;
        /// <summary>First tick that runs unpaused again.</summary>
        public long End;
    }

    /// <summary>
    /// Derives the paused stretches of a recording from its pause commands so playback can skip
    /// them: watching the world stand still while the player had a menu open is never useful.
    /// A pause command drained at tick T takes effect from tick T+1.
    /// </summary>
    public static class ReplayPauseSpans
    {
        /// <summary>Builds the spans at least <paramref name="minLengthTicks"/> long, in tick order.</summary>
        public static List<ReplayPauseSpan> Build(IReadOnlyList<ReplayCommandRecord> commands, long totalTicks, long minLengthTicks)
        {
            var spans = new List<ReplayPauseSpan>();
            if (commands == null)
                return spans;

            bool manual = false;
            bool farm = false;
            bool paused = false;
            long pausedSince = 0;

            for (int i = 0; i < commands.Count; i++)
            {
                var rec = commands[i];
                if (rec.Command.Type == PlayerCommandType.SetManualPause)
                    manual = rec.Command.ABool;
                else if (rec.Command.Type == PlayerCommandType.SetFarmModePause)
                    farm = rec.Command.ABool;
                else
                    continue;

                bool nowPaused = manual || farm;
                if (nowPaused && !paused)
                {
                    paused = true;
                    pausedSince = rec.Tick + 1;
                }
                else if (!nowPaused && paused)
                {
                    paused = false;
                    AddSpan(spans, pausedSince, rec.Tick + 1, minLengthTicks);
                }
            }

            if (paused)
                AddSpan(spans, pausedSince, totalTicks, minLengthTicks);
            return spans;
        }

        private static void AddSpan(List<ReplayPauseSpan> spans, long start, long end, long minLengthTicks)
        {
            if (end - start < minLengthTicks)
                return;
            spans.Add(new ReplayPauseSpan { Start = start, End = end });
        }

        /// <summary>Returns the end of the span containing <paramref name="tick"/>, or -1 when the tick is not inside one.</summary>
        public static long FindSkipTarget(List<ReplayPauseSpan> spans, long tick)
        {
            for (int i = 0; i < spans.Count; i++)
            {
                if (tick >= spans[i].Start && tick < spans[i].End)
                    return spans[i].End;
            }
            return -1;
        }
    }
}
