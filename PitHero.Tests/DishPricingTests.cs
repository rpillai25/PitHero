using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero;
using PitHero.Dining;
using RolePlayingFramework.Combat;

namespace PitHero.Tests
{
    /// <summary>
    /// Menu pricing invariants: prices derive from ingredient sell value × markup PLUS an
    /// effect premium per buff point, so a dish with strictly better effects of the same kind
    /// can never be cheaper than its weaker counterpart.
    /// </summary>
    [TestClass]
    public class DishPricingTests
    {
        [TestMethod]
        public void AllPrices_RespectFloorAndRounding()
        {
            for (int i = 0; i < DishTypeInfo.Count; i++)
            {
                int price = DishConfig.GetPrice((DishType)i);
                Assert.IsTrue(price >= GameConfig.DishPriceMin, $"{(DishType)i} price {price} below floor");
                Assert.AreEqual(0, price % GameConfig.DishPriceRoundTo, $"{(DishType)i} price {price} not rounded");
            }
        }

        [TestMethod]
        public void BetterDefenseDish_CostsMoreThanWeakerOne()
        {
            // ButteredBread DEF+1 vs CheesyMashedPotatoes DEF+2 — the user-reported case
            Assert.IsTrue(DishConfig.GetPrice(DishType.CheesyMashedPotatoes) > DishConfig.GetPrice(DishType.ButteredBread),
                $"DEF+2 mash ({DishConfig.GetPrice(DishType.CheesyMashedPotatoes)}g) must cost more than DEF+1 bread ({DishConfig.GetPrice(DishType.ButteredBread)}g)");
        }

        [TestMethod]
        public void BetterHPRegenDish_CostsMoreThanWeakerOne()
        {
            // TurnipOnionStew HP+1/round vs CornChowder HP+2/round
            Assert.IsTrue(DishConfig.GetPrice(DishType.CornChowder) > DishConfig.GetPrice(DishType.TurnipOnionStew),
                "HP regen 2 chowder must cost more than regen 1 stew");
        }

        [TestMethod]
        public void BetterAttackDishes_ScaleWithMagnitude()
        {
            // ATK+1 skewers < ATK+2 corn < ATK+3 eggplant steak
            int atk1 = DishConfig.GetPrice(DishType.RoastedOnionSkewers);
            int atk2 = DishConfig.GetPrice(DishType.GrilledCornWithButter);
            int atk3 = DishConfig.GetPrice(DishType.SpicedEggplantSteak);
            Assert.IsTrue(atk1 < atk2, $"ATK+1 ({atk1}g) must be cheaper than ATK+2 ({atk2}g)");
            Assert.IsTrue(atk2 < atk3, $"ATK+2 ({atk2}g) must be cheaper than ATK+3 ({atk3}g)");
        }

        [TestMethod]
        public void EffectPremium_MatchesConfiguredPerPointValues()
        {
            // The premium term for a known dish: CheesyMashedPotatoes has exactly DEF+2
            var def = DishConfig.GetDefinition(DishType.CheesyMashedPotatoes);
            Assert.AreEqual(1, def.Buffs.Length);
            Assert.AreEqual(BuffType.DefenseUp, def.Buffs[0].Type);
            Assert.AreEqual(2, def.Buffs[0].Magnitude);
            // Bread is DEF+1 with a 1-wheat recipe; mash is DEF+2 with 2 potatoes — with the
            // premium (15g/point) the gap must be at least one rounding step
            Assert.IsTrue(DishConfig.GetPrice(DishType.CheesyMashedPotatoes)
                - DishConfig.GetPrice(DishType.ButteredBread) >= GameConfig.DishPriceRoundTo);
        }

        [TestMethod]
        public void JobFallbackDishes_AreDistinctAndCheap()
        {
            string[] jobs = { "Knight", "Mage", "Priest", "Thief", "Monk", "Archer" };
            int feastPrice = DishConfig.GetPrice(DishType.HarvestFeastPlatter);
            for (int j = 0; j < jobs.Length; j++)
            {
                var favorite = DishConfig.GetFavoriteForJob(jobs[j]);
                var fb1 = DishConfig.GetFallbackForJob(jobs[j], 0);
                var fb2 = DishConfig.GetFallbackForJob(jobs[j], 1);

                Assert.AreNotEqual(favorite, fb1, $"{jobs[j]}: first fallback duplicates the favorite");
                Assert.AreNotEqual(favorite, fb2, $"{jobs[j]}: second fallback duplicates the favorite");
                Assert.AreNotEqual(fb1, fb2, $"{jobs[j]}: fallbacks duplicate each other");

                // Fallbacks are meant to be modest dishes, never the premium end of the menu
                Assert.IsTrue(DishConfig.GetPrice(fb1) < feastPrice, $"{jobs[j]}: fallback 1 too expensive");
                Assert.IsTrue(DishConfig.GetPrice(fb2) < feastPrice, $"{jobs[j]}: fallback 2 too expensive");
            }
        }

        [TestMethod]
        public void HarvestFeastPlatter_IsTheMostExpensiveDish()
        {
            int feast = DishConfig.GetPrice(DishType.HarvestFeastPlatter);
            for (int i = 0; i < DishTypeInfo.Count; i++)
            {
                if ((DishType)i == DishType.HarvestFeastPlatter)
                    continue;
                Assert.IsTrue(feast > DishConfig.GetPrice((DishType)i),
                    $"Feast ({feast}g) must out-price {(DishType)i} ({DishConfig.GetPrice((DishType)i)}g)");
            }
        }
    }
}
