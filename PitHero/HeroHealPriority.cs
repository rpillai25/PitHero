namespace PitHero
{
    /// <summary>
    /// Enum representing the different healing priorities a hero can have when HP is critical
    /// </summary>
    public enum HeroHealPriority
    {
        /// <summary>
        /// Hero will jump out of pit and sleep at the inn (requires gold)
        /// </summary>
        Inn,

        /// <summary>
        /// Hero will use a healing item from the shortcut bar
        /// </summary>
        HealingItem,

        /// <summary>
        /// Hero will use a healing skill from the shortcut bar
        /// </summary>
        HealingSkill
    }
}
