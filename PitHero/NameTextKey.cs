namespace PitHero
{
    /// <summary>
    /// Localization keys for the character name pools in Names.txt. Each key maps to a
    /// comma-separated list rather than a single string, so look them up with
    /// TextService.DisplayTextList rather than DisplayText.
    /// </summary>
    public sealed class NameTextKey : TextKey
    {
        public const string MaleFirstNames   = "MaleFirstNames";
        public const string FemaleFirstNames = "FemaleFirstNames";
        public const string LastNames        = "LastNames";
        public const string MonsterPrefixes  = "MonsterPrefixes";
        public const string MonsterSuffixes  = "MonsterSuffixes";
    }
}
