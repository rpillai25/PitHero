using PitHero.Services;

namespace PitHero.Artifacts
{
    /// <summary>
    /// The simulation-side effects of the Local artifacts (issue #411), read from the session's
    /// <see cref="GameStateService"/> every fixed step so a replay sees exactly the multiplier that
    /// was in force at that tick. Pure and headless-safe: a null state means no artifacts.
    /// </summary>
    public static class LocalArtifactEffects
    {
        /// <summary>
        /// Crop growth speed multiplier: the Lightning Grow Fertilizer wins over the Fast Grow
        /// Fertilizer, the two never stack.
        /// </summary>
        public static float GetCropGrowthMultiplier(GameStateService gameState)
        {
            if (gameState == null)
                return 1f;
            if (gameState.OwnsLocalArtifact(ArtifactType.LightningGrowFertilizer))
                return GameConfig.LightningGrowFertilizerCropGrowthMultiplier;
            if (gameState.OwnsLocalArtifact(ArtifactType.FastGrowFertilizer))
                return GameConfig.FastGrowFertilizerCropGrowthMultiplier;
            return 1f;
        }

        /// <summary>Movement speed multiplier for farm and kitchen workers (Hermes Boots).</summary>
        public static float GetWorkerMoveSpeedMultiplier(GameStateService gameState)
        {
            if (gameState == null)
                return 1f;
            return gameState.OwnsLocalArtifact(ArtifactType.HermesBoots)
                ? GameConfig.HermesBootsWorkerMoveSpeedMultiplier
                : 1f;
        }
    }
}
