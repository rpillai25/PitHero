namespace RolePlayingFramework
{
    /// <summary>
    /// Character gender. Female art does not exist yet (all hero/mercenary sprites are the
    /// MaleHero* atlas set), so every generated character is Male for now. The enum and the
    /// gendered name pools exist so female characters only need art plus a roll at the two
    /// generation sites (HeroCreationUI and MercenaryManager.SpawnMercenary).
    /// </summary>
    public enum Gender
    {
        Male = 0,
        Female = 1
    }
}
