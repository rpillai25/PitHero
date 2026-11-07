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

        /// <summary>
        /// Tiles a worker waters per can-full (issue #450): the best owned watering can wins
        /// (Platinum over Gold over Silver), the cans never stack; the plain can without one.
        /// </summary>
        public static int GetWateringCanCapacity(GameStateService gameState)
        {
            if (gameState == null)
                return GameConfig.WateringCanMaxCharges;
            if (gameState.OwnsLocalArtifact(ArtifactType.PlatinumWateringCan))
                return GameConfig.PlatinumWateringCanCharges;
            if (gameState.OwnsLocalArtifact(ArtifactType.GoldWateringCan))
                return GameConfig.GoldWateringCanCharges;
            if (gameState.OwnsLocalArtifact(ArtifactType.SilverWateringCan))
                return GameConfig.SilverWateringCanCharges;
            return GameConfig.WateringCanMaxCharges;
        }

        /// <summary>
        /// True when this hero owns the Prospector's Map (issue #452), which unlocks the Digging Field
        /// building. The PlaceBuilding (Digging Field) and DigGem handlers ignore requests without it.
        /// </summary>
        public static bool HasGemDigSite(GameStateService gameState)
        {
            return gameState != null && gameState.OwnsLocalArtifact(ArtifactType.ProspectorsMap);
        }
    }
}
