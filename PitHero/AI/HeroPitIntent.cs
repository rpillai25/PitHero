namespace PitHero.AI
{
    /// <summary>
    /// The pit transition the hero's current GOAP plan intends. Set at plan formation so
    /// mercenaries can adopt the same goal immediately instead of waiting for the hero's
    /// InsidePit flag to flip after landing.
    /// </summary>
    public enum HeroPitIntent
    {
        None,
        EnteringPit,
        ExitingPit
    }
}
