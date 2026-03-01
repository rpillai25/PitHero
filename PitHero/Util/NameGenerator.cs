namespace PitHero.Util
{
    /// <summary>Shared utility for generating random character names</summary>
    public static class NameGenerator
    {
        private static readonly string[] FirstNames =
        {
            "Aldric", "Brynn", "Cedric", "Diana", "Elara",
            "Finn", "Gareth", "Helena", "Ivan", "Jade",
            "Kael", "Luna", "Marcus", "Nina", "Owen",
            "Petra", "Quinn", "Rowan", "Sasha", "Thane"
        };

        private static readonly string[] LastNames =
        {
            "Swift", "Strong", "Wise", "Brave", "Bold",
            "Quick", "Keen", "True", "Steel", "Bright"
        };

        /// <summary>Generates a random first-last name using Nez.Random</summary>
        public static string GenerateRandomName()
        {
            return $"{FirstNames[Nez.Random.Range(0, FirstNames.Length)]} {LastNames[Nez.Random.Range(0, LastNames.Length)]}";
        }
    }
}
