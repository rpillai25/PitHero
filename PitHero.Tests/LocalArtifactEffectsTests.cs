using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero;
using PitHero.Artifacts;
using PitHero.Services;

namespace PitHero.Tests
{
    /// <summary>The simulation multipliers the Local artifacts apply (issue #411).</summary>
    [TestClass]
    public class LocalArtifactEffectsTests
    {
        [TestMethod]
        public void CropGrowth_NoArtifacts_IsOne()
        {
            Assert.AreEqual(1f, LocalArtifactEffects.GetCropGrowthMultiplier(new GameStateService()));
            Assert.AreEqual(1f, LocalArtifactEffects.GetCropGrowthMultiplier(null), "No state means no artifacts");
        }

        [TestMethod]
        public void CropGrowth_FastThenLightning_HighestWinsNeverStacks()
        {
            var state = new GameStateService();
            state.GrantLocalArtifact(ArtifactType.FastGrowFertilizer);
            Assert.AreEqual(GameConfig.FastGrowFertilizerCropGrowthMultiplier, LocalArtifactEffects.GetCropGrowthMultiplier(state));

            state.GrantLocalArtifact(ArtifactType.LightningGrowFertilizer);
            Assert.AreEqual(GameConfig.LightningGrowFertilizerCropGrowthMultiplier, LocalArtifactEffects.GetCropGrowthMultiplier(state),
                "Owning both gives 3x, not 6x");
        }

        [TestMethod]
        public void WorkerSpeed_HermesBoots_Doubles()
        {
            var state = new GameStateService();
            Assert.AreEqual(1f, LocalArtifactEffects.GetWorkerMoveSpeedMultiplier(state));
            Assert.AreEqual(1f, LocalArtifactEffects.GetWorkerMoveSpeedMultiplier(null));

            state.GrantLocalArtifact(ArtifactType.HermesBoots);
            Assert.AreEqual(GameConfig.HermesBootsWorkerMoveSpeedMultiplier, LocalArtifactEffects.GetWorkerMoveSpeedMultiplier(state));
        }

        [TestMethod]
        public void GameState_LocalArtifacts_GrantOrderVersionAndMask()
        {
            var state = new GameStateService();
            int v0 = state.LocalArtifactVersion;

            Assert.IsTrue(state.GrantLocalArtifact(ArtifactType.HermesBoots));
            Assert.IsFalse(state.GrantLocalArtifact(ArtifactType.HermesBoots), "Idempotent");
            Assert.IsTrue(state.GrantLocalArtifact(ArtifactType.FastGrowFertilizer));
            Assert.AreEqual(v0 + 2, state.LocalArtifactVersion);
            Assert.AreEqual((1 << (int)ArtifactType.HermesBoots) | (1 << (int)ArtifactType.FastGrowFertilizer), state.LocalArtifactMask);

            var ordinals = new System.Collections.Generic.List<int>();
            state.CopyLocalArtifactOrdinals(ordinals);
            CollectionAssert.AreEqual(new System.Collections.Generic.List<int> { (int)ArtifactType.HermesBoots, (int)ArtifactType.FastGrowFertilizer }, ordinals);

            state.ClearLocalArtifacts();
            Assert.AreEqual(0, state.LocalArtifactMask);
            Assert.IsFalse(state.OwnsLocalArtifact(ArtifactType.HermesBoots));

            // Loading keeps unknown ordinals (a newer build's artifact) and skips duplicates
            state.SetLocalArtifacts(new System.Collections.Generic.List<int> { 99, (int)ArtifactType.HermesBoots, (int)ArtifactType.HermesBoots });
            Assert.IsTrue(state.OwnsLocalArtifact(ArtifactType.HermesBoots));
            ordinals.Clear();
            state.CopyLocalArtifactOrdinals(ordinals);
            CollectionAssert.AreEqual(new System.Collections.Generic.List<int> { 99, (int)ArtifactType.HermesBoots }, ordinals);
        }
    }
}
