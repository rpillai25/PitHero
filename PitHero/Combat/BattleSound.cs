namespace PitHero.Combat
{
    /// <summary>
    /// Logical sound events the engine requests from the sink.
    /// The sink maps these to concrete SoundEffectType values or no-ops them
    /// in headless mode.
    /// </summary>
    public enum BattleSound
    {
        /// <summary>Unarmed or physical attack hit.</summary>
        Punch,

        /// <summary>An ally took damage.</summary>
        TakeDamage,

        /// <summary>HP or MP was restored.</summary>
        Restorative,

        /// <summary>An enemy was defeated.</summary>
        EnemyDefeat
    }
}
