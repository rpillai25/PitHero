using System.Text;

namespace PitHero.Services.Replay
{
    /// <summary>
    /// Ring buffer of the most recent battle decisions (target picks, Provoke, attacks, buffs, threat),
    /// each stamped with the simulation tick. Printed by <see cref="ReplayStateDescriber"/> into
    /// replay_divergence.log so a drifted battle can be compared action-by-action against the live
    /// session's analytics rows. Playback suppresses analytics, so this is the only battle log a replay has.
    /// </summary>
    public static class ReplayBattleTrace
    {
        private const int Capacity = 48;
        private static readonly string[] _lines = new string[Capacity];
        private static int _next;
        private static int _count;

        public static void Add(string line)
        {
            if (!GameConfig.ReplayDivergenceSnapshots)
                return;
            _lines[_next] = SimulationClock.CurrentTick + " " + line;
            _next = (_next + 1) % Capacity;
            if (_count < Capacity) _count++;
        }

        public static void Clear()
        {
            _next = 0;
            _count = 0;
        }

        /// <summary>Oldest first, one line each, indented for the divergence report.</summary>
        public static string Dump()
        {
            if (_count == 0)
                return "      (no battle events)\n";
            var sb = new StringBuilder(_count * 96);
            int start = (_next - _count + Capacity) % Capacity;
            for (int i = 0; i < _count; i++)
                sb.Append("      ").AppendLine(_lines[(start + i) % Capacity]);
            return sb.ToString();
        }
    }
}
