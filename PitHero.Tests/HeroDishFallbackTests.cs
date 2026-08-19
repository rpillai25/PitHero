using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Dining;
using PitHero.Services;

namespace PitHero.Tests
{
    /// <summary>
    /// Tests for the hero's dish fallback ladder (issue #392 follow-up): favorite → job
    /// fallback 0 → job fallback 1, each gated on ingredients AND price. Previously the hero
    /// ordered only his favorite, so one missing crop skipped the whole party's meal.
    /// </summary>
    [TestClass]
    public class HeroDishFallbackTests
    {
        private const int PlentyOfGold = 100000;

        [TestMethod]
        [TestCategory("PartyDining")]
        public void FavoriteCoverableAndAffordable_PicksFavorite()
        {
            bool ok = PartyDiningService.TryPickHeroDishCore(
                DishType.CheesyMashedPotatoes, "Knight", PlentyOfGold,
                _ => true, out var dish, out var anyCoverable);

            Assert.IsTrue(ok);
            Assert.AreEqual(DishType.CheesyMashedPotatoes, dish);
            Assert.IsTrue(anyCoverable);
        }

        [TestMethod]
        [TestCategory("PartyDining")]
        public void FavoriteNotCoverable_FallsBackToJobFallback()
        {
            // Thief fallbacks: TomatoCheeseBisque, RoastedOnionSkewers
            bool ok = PartyDiningService.TryPickHeroDishCore(
                DishType.GardenSalad, "Thief", PlentyOfGold,
                d => d != DishType.GardenSalad, out var dish, out var anyCoverable);

            Assert.IsTrue(ok);
            Assert.AreEqual(DishType.TomatoCheeseBisque, dish);
            Assert.IsTrue(anyCoverable);
        }

        [TestMethod]
        [TestCategory("PartyDining")]
        public void FavoriteEqualsJobFallback_SkipsDuplicateAndTriesNext()
        {
            // The production bug scenario: a Knight favoring Cheesy Mashed Potatoes (which is
            // also the Knight's fallback 0) with no potatoes in stock must land on fallback 1
            // (Buttered Bread) instead of re-checking the favorite and skipping the meal.
            bool ok = PartyDiningService.TryPickHeroDishCore(
                DishType.CheesyMashedPotatoes, "Knight", PlentyOfGold,
                d => d != DishType.CheesyMashedPotatoes, out var dish, out var anyCoverable);

            Assert.IsTrue(ok);
            Assert.AreEqual(DishType.ButteredBread, dish);
            Assert.IsTrue(anyCoverable);
        }

        [TestMethod]
        [TestCategory("PartyDining")]
        public void NothingCoverable_FailsWithAnyCoverableFalse()
        {
            bool ok = PartyDiningService.TryPickHeroDishCore(
                DishType.CheesyMashedPotatoes, "Knight", PlentyOfGold,
                _ => false, out _, out var anyCoverable);

            Assert.IsFalse(ok);
            Assert.IsFalse(anyCoverable, "No candidate had ingredients — skip must read as no_ingredients");
        }

        [TestMethod]
        [TestCategory("PartyDining")]
        public void CoverableButUnaffordable_FailsWithAnyCoverableTrue()
        {
            bool ok = PartyDiningService.TryPickHeroDishCore(
                DishType.CheesyMashedPotatoes, "Knight", 0,
                _ => true, out _, out var anyCoverable);

            Assert.IsFalse(ok);
            Assert.IsTrue(anyCoverable, "Candidates had ingredients — skip must read as no_gold");
        }

        [TestMethod]
        [TestCategory("PartyDining")]
        public void FavoriteUnaffordable_FallsBackToCheaperDish()
        {
            // Gold covers the cheap fallback but not the favorite: the hero still eats.
            int fallbackPrice = DishConfig.GetPrice(DishConfig.GetFallbackForJob("Mage", 0));
            int favoritePrice = DishConfig.GetPrice(DishType.HarvestFeastPlatter);
            Assert.IsTrue(fallbackPrice < favoritePrice, "Test premise: fallback must be cheaper than favorite");

            bool ok = PartyDiningService.TryPickHeroDishCore(
                DishType.HarvestFeastPlatter, "Mage", fallbackPrice,
                _ => true, out var dish, out var anyCoverable);

            Assert.IsTrue(ok);
            Assert.AreEqual(DishConfig.GetFallbackForJob("Mage", 0), dish);
            Assert.IsTrue(anyCoverable);
        }

        [TestMethod]
        [TestCategory("PartyDining")]
        public void NullJobName_UsesDefaultFallbacks()
        {
            // Headless/no-hero contexts resolve jobName null → default fallback set.
            bool ok = PartyDiningService.TryPickHeroDishCore(
                DishType.CheesyMashedPotatoes, null, PlentyOfGold,
                d => d != DishType.CheesyMashedPotatoes, out var dish, out _);

            Assert.IsTrue(ok);
            Assert.AreEqual(DishConfig.GetFallbackForJob(null, 0), dish);
        }
    }
}
