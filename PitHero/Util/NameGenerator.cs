using Nez;
using PitHero.Services;
using RolePlayingFramework;

namespace PitHero.Util
{
    /// <summary>
    /// Shared utility for generating random character names. Humans (heroes and mercenaries) draw
    /// a gendered first name plus a surname; monsters draw a single syllable-forged first name with
    /// no surname, from pools that never overlap the human ones.
    ///
    /// The pools themselves live in Content/Localization/{lang}/Names.txt so they can be swapped
    /// per language without a rebuild; see NameTextKey for the keys.
    /// </summary>
    public static class NameGenerator
    {
        // Last-resort pools, used only when Names.txt is missing or a key is absent. They keep a
        // broken content install from crashing character creation, and the placeholder names make
        // the failure obvious rather than silent.
        private static readonly string[] FallbackMaleFirstNames = { "Nameless" };
        private static readonly string[] FallbackFemaleFirstNames = { "Nameless" };
        private static readonly string[] FallbackLastNames = { "Onemore" };
        private static readonly string[] FallbackMonsterPrefixes = { "Grunt" };
        private static readonly string[] FallbackMonsterSuffixes = { "ling" };

        private static TextService _textService;
        private static string[] _maleFirstNames;
        private static string[] _femaleFirstNames;
        private static string[] _lastNames;
        private static string[] _monsterPrefixes;
        private static string[] _monsterSuffixes;

        private static TextService GetTextService()
        {
            if (_textService == null)
            {
                // Core.Instance is null in headless hosts (unit tests, virtual balance runs) and
                // Core.Services would throw there, so load the localization files directly instead.
                if (Core.Instance != null)
                    _textService = Core.Services?.GetService<TextService>();
                if (_textService == null)
                    _textService = new TextService();
            }
            return _textService;
        }

        /// <summary>Loads a pool from Names.txt once and caches it, falling back if the key is missing.</summary>
        private static string[] GetPool(ref string[] cache, string key, string[] fallback)
        {
            if (cache != null)
                return cache;

            var loaded = GetTextService().DisplayTextList(TextType.Name, key);
            cache = loaded.Length > 0 ? loaded : fallback;
            return cache;
        }

        /// <summary>Generates a random first-last name for the given gender using Nez.Random</summary>
        public static string GenerateRandomName(Gender gender = Gender.Male)
        {
            var lastNames = GetPool(ref _lastNames, NameTextKey.LastNames, FallbackLastNames);
            return $"{GenerateFirstName(gender)} {lastNames[Random.Range(0, lastNames.Length)]}";
        }

        /// <summary>Generates a random first name only for the given gender using Nez.Random</summary>
        public static string GenerateFirstName(Gender gender = Gender.Male)
        {
            var pool = gender == Gender.Female
                ? GetPool(ref _femaleFirstNames, NameTextKey.FemaleFirstNames, FallbackFemaleFirstNames)
                : GetPool(ref _maleFirstNames, NameTextKey.MaleFirstNames, FallbackMaleFirstNames);
            return pool[Random.Range(0, pool.Length)];
        }

        /// <summary>
        /// Generates a single syllable-forged monster name (no surname) using Nez.Random. Monsters
        /// never draw from the human name pools.
        /// </summary>
        public static string GenerateMonsterName()
        {
            var prefixes = GetPool(ref _monsterPrefixes, NameTextKey.MonsterPrefixes, FallbackMonsterPrefixes);
            var suffixes = GetPool(ref _monsterSuffixes, NameTextKey.MonsterSuffixes, FallbackMonsterSuffixes);
            return prefixes[Random.Range(0, prefixes.Length)] + suffixes[Random.Range(0, suffixes.Length)];
        }
    }
}
