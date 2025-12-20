namespace PitHero
{
    /// <summary>
    /// Enum representing the different pit priorities a hero can have
    /// </summary>
    public enum HeroPitPriority
    {
        /// <summary>
        /// Hero will move and uncover fogOfWar, and when he sees a treasure he will set his sights on getting to and opening treasure
        /// </summary>
        Treasure,

        /// <summary>
        /// Hero will move and uncover fogOfWar, and when he sees a monster he will set his sights on getting to and battling the monster
        /// </summary>
        Battle,

        /// <summary>
        /// Hero will activate the wizard orb to advance the pit level. Upon activating he will jump out of the pit and regenerate pit.
        /// </summary>
        Advance
    }
}