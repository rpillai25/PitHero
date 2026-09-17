namespace PitHero.Farming
{
    /// <summary>
    /// A farm worker's preferred kind of work. Each duty falls back to the other side's work when its
    /// own queues are empty, so the split only decides who does what first — nobody idles.
    /// </summary>
    public enum FarmDuty
    {
        /// <summary>Water dry growing crops first (a dry crop makes no growth progress).</summary>
        Water,

        /// <summary>Till, swap-destroy, plant and harvest first.</summary>
        Tend
    }
}
