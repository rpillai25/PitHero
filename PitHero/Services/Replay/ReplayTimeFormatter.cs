using System.Text;

namespace PitHero.Services.Replay
{
    /// <summary>Formats replay positions as mm:ss (under an hour) or h:mm:ss. Allocation-light via a reusable builder.</summary>
    public static class ReplayTimeFormatter
    {
        private static readonly StringBuilder _sb = new StringBuilder(16);

        /// <summary>Formats a tick count using the fixed simulation step.</summary>
        public static string FormatTicks(long ticks)
        {
            long stepsPerSecond = (long)System.Math.Round(1f / GameConfig.SimulationFixedStepSeconds);
            return FormatSeconds(stepsPerSecond > 0 ? ticks / stepsPerSecond : 0);
        }

        /// <summary>Formats whole seconds as mm:ss or h:mm:ss.</summary>
        public static string FormatSeconds(float seconds)
        {
            if (seconds < 0f)
                seconds = 0f;
            long total = (long)(seconds + 0.001f); // ticks * (1/60f) lands a hair under whole seconds
            long hours = total / 3600;
            long minutes = (total / 60) % 60;
            long secs = total % 60;

            _sb.Clear();
            if (hours > 0)
            {
                _sb.Append(hours).Append(':');
                AppendTwoDigits(minutes);
            }
            else
            {
                AppendTwoDigits(minutes);
            }
            _sb.Append(':');
            AppendTwoDigits(secs);
            return _sb.ToString();
        }

        private static void AppendTwoDigits(long value)
        {
            if (value < 10)
                _sb.Append('0');
            _sb.Append(value);
        }
    }
}
