namespace RolePlayingFramework
{
    /// <summary>
    /// Character gender. Selects the MaleHero or FemaleHero sprite set and the name pool; picked in
    /// HeroCreationUI for the hero and rolled in MercenaryManager.SpawnMercenary for mercenaries.
    /// </summary>
    public enum Gender
    {
        Male = 0,
        Female = 1
    }
}
