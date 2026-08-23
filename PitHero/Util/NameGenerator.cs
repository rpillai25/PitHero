using RolePlayingFramework;

namespace PitHero.Util
{
    /// <summary>
    /// Shared utility for generating random character names. Humans (heroes and mercenaries) draw
    /// a gendered first name plus a surname; monsters draw a single syllable-forged first name with
    /// no surname, from pools that never overlap the human ones.
    /// </summary>
    public static class NameGenerator
    {
        private static readonly string[] MaleFirstNames =
        {
            "Adrian", "Alaric", "Aldric", "Ansel", "Arden",
            "Aymer", "Baldwin", "Bertram", "Bran", "Brom",
            "Cedric", "Corwin", "Cuthbert", "Drake", "Dunstan",
            "Edmund", "Elric", "Emeric", "Everard", "Finn",
            "Fulk", "Gareth", "Garrick", "Godfrey", "Gunnar",
            "Hale", "Hugh", "Ivan", "Jasper", "John",
            "Kael", "Lambert", "Leofric", "Lucan", "Marcus",
            "Merrick", "Milo", "Nolan", "Odo", "Osric",
            "Owen", "Percival", "Quentin", "Ralf", "Reynard",
            "Roland", "Rowan", "Rurik", "Sigurd", "Simon",
            "Talbot", "Thane", "Theobald", "Tom", "Tristan",
            "Ulric", "Wallace", "Warin", "Wulfric", "Wystan"
        };

        /// <summary>
        /// Authored ahead of the art: nothing passes Gender.Female yet, so this pool is currently
        /// unreachable in normal play. Keep it populated so enabling female characters is a one-line change.
        /// </summary>
        private static readonly string[] FemaleFirstNames =
        {
            "Adelina", "Agnes", "Alice", "Amabel", "Aveline",
            "Beatrix", "Blanche", "Brynn", "Cecily", "Clarice",
            "Constance", "Diana", "Edith", "Elara", "Eleanor",
            "Elowen", "Emeline", "Evelyn", "Freya", "Gisela",
            "Godiva", "Greta", "Helena", "Hilda", "Ingrid",
            "Isolde", "Jade", "Joan", "Juliana", "Katrin",
            "Lucia", "Luna", "Mabel", "Margery", "Matilda",
            "Maude", "Nina", "Odilia", "Petra", "Rosalind",
            "Rowena", "Sasha", "Sibyl", "Solene", "Sybilla",
            "Thea", "Ursula", "Vivienne", "Wilhelmina", "Yseult"
        };

        private static readonly string[] LastNames =
        {
            "Swift", "Strong", "Wise", "Brave", "Bold",
            "Quick", "Keen", "True", "Steel", "Bright",
            "Hall", "Romero", "Carmack", "Happ", "Blow",
            "Brush", "Fletcher", "Cooper", "Thatcher", "Mason",
            "Chandler", "Tanner", "Wright", "Baker", "Miller",
            "Turner", "Sawyer", "Weaver", "Marsh", "Ashford",
            "Blackwood", "Thornton", "Greenhill", "Whitlock", "Ravensworth",
            "Holloway", "Ironside", "Longshaw", "Stonebridge", "Hawksley",
            "Redmayne", "Fairbairn", "Underhill", "Bramble", "Falconer",
            "Winterbourne", "Oakes", "Vance", "Alderton", "Grimsby"
        };

        /// <summary>Leading syllables for forged monster names. Deliberately disjoint from the suffix
        /// list (lowercased) so a name can never come out doubled, e.g. "Grimgrim".</summary>
        private static readonly string[] MonsterPrefixes =
        {
            "Gru", "Vrak", "Skar", "Morr", "Ulg",
            "Thug", "Zog", "Nask", "Brol", "Krug",
            "Hesh", "Vord", "Gnash", "Drel", "Skulg",
            "Xar", "Gorm", "Vex", "Snarl", "Rott",
            "Blud", "Kral", "Murg", "Ghol", "Zeth",
            "Yurg", "Thrak", "Wretch", "Slog", "Fell",
            "Grull", "Necr", "Ozz", "Varg", "Skree",
            "Drog", "Hrak", "Nurg", "Bael", "Krix"
        };

        /// <summary>Trailing syllables for forged monster names.</summary>
        private static readonly string[] MonsterSuffixes =
        {
            "nash", "ka", "rul", "ek", "ath",
            "gor", "rim", "ul", "dak", "esh",
            "nak", "var", "mog", "tusk", "fang",
            "maw", "gash", "rax", "zul", "thok",
            "grim", "nok", "vek", "durr", "shak",
            "lok", "rag", "muk", "threx", "gorn"
        };

        /// <summary>Generates a random first-last name for the given gender using Nez.Random</summary>
        public static string GenerateRandomName(Gender gender = Gender.Male)
        {
            return $"{GenerateFirstName(gender)} {LastNames[Nez.Random.Range(0, LastNames.Length)]}";
        }

        /// <summary>Generates a random first name only for the given gender using Nez.Random</summary>
        public static string GenerateFirstName(Gender gender = Gender.Male)
        {
            var pool = gender == Gender.Female ? FemaleFirstNames : MaleFirstNames;
            return pool[Nez.Random.Range(0, pool.Length)];
        }

        /// <summary>
        /// Generates a single syllable-forged monster name (no surname) using Nez.Random. Monsters
        /// never draw from the human name pools.
        /// </summary>
        public static string GenerateMonsterName()
        {
            var prefix = MonsterPrefixes[Nez.Random.Range(0, MonsterPrefixes.Length)];
            var suffix = MonsterSuffixes[Nez.Random.Range(0, MonsterSuffixes.Length)];
            return prefix + suffix;
        }
    }
}
