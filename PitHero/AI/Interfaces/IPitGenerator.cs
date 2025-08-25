namespace PitHero.AI.Interfaces
{
    /// <summary>
    /// Interface for pit generation operations
    /// </summary>
    public interface IPitGenerator
    {
        /// <summary>
        /// Clear all existing pit entities and regenerate content for the current pit level
        /// </summary>
        void RegenerateForCurrentLevel();

        /// <summary>
        /// Clear all existing pit entities and regenerate content for the specified level
        /// </summary>
        void RegenerateForLevel(int level);
    }
}