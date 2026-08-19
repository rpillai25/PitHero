using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Dining;
using PitHero.Services;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Stats;

namespace PitHero.Tests
{
    /// <summary>
    /// Tests for MealBuffService expiry (issue #392): apply with expiry stamp, prune before/at/after,
    /// inject-after-prune injects nothing, replace-record keeps latest expiry.
    /// </summary>
    [TestClass]
    public class MealBuffServiceExpiryTests
    {
        private static Hero MakeHero() =>
            new Hero("TestHero", new Knight(), level: 10, baseStats: new StatBlock(10, 10, 10, 10));

        private const DishType Dish = DishType.RoastedOnionSkewers;
        private const float Expiry = 500f;

        // ── Apply + record present ───────────────────────────────────────────────

        [TestMethod]
        [TestCategory("MealBuff")]
        public void ApplyMeal_WithExpiry_RecordIsPresent()
        {
            var service = new MealBuffService();
            var hero = MakeHero();

            service.ApplyMeal(hero, Dish, false, Expiry);

            Assert.IsTrue(service.TryGetMeal(hero, out _, out _), "Record should be present after ApplyMeal");
        }

        // ── Prune before expiry ──────────────────────────────────────────────────

        [TestMethod]
        [TestCategory("MealBuff")]
        public void Prune_BeforeExpiry_RecordKept()
        {
            var service = new MealBuffService();
            var hero = MakeHero();
            service.ApplyMeal(hero, Dish, false, Expiry);

            service.Prune(Expiry - 1f); // one second before expiry

            Assert.IsTrue(service.TryGetMeal(hero, out _, out _), "Record should survive a prune before expiry");
        }

        // ── Prune at expiry ──────────────────────────────────────────────────────

        [TestMethod]
        [TestCategory("MealBuff")]
        public void Prune_AtExpiry_RecordRemoved()
        {
            var service = new MealBuffService();
            var hero = MakeHero();
            service.ApplyMeal(hero, Dish, false, Expiry);

            service.Prune(Expiry); // exactly at expiry

            Assert.IsFalse(service.TryGetMeal(hero, out _, out _), "Record should be pruned when now == expiry");
        }

        // ── Prune after expiry ───────────────────────────────────────────────────

        [TestMethod]
        [TestCategory("MealBuff")]
        public void Prune_AfterExpiry_RecordRemoved()
        {
            var service = new MealBuffService();
            var hero = MakeHero();
            service.ApplyMeal(hero, Dish, false, Expiry);

            service.Prune(Expiry + 100f);

            Assert.IsFalse(service.TryGetMeal(hero, out _, out _), "Record should be pruned after expiry");
        }

        // ── InjectBuffsAtBattleStart after prune injects nothing ─────────────────

        [TestMethod]
        [TestCategory("MealBuff")]
        public void InjectBuffsAtBattleStart_AfterPrune_InjectsNothing()
        {
            var service = new MealBuffService();
            var hero = MakeHero();
            // RoastedOnionSkewers gives AttackUp 1
            service.ApplyMeal(hero, Dish, false, Expiry);
            service.Prune(Expiry); // record removed

            hero.ClearBattleState();
            service.InjectBuffsAtBattleStart(hero);

            // AttackUp stack from "meal" source should be zero
            int attackUpFromMeal = hero.GetBuffStacks(MealBuffService.MealBuffSourceId, RolePlayingFramework.Combat.BuffType.AttackUp);
            Assert.AreEqual(0, attackUpFromMeal, "No meal buffs should be injected after record is pruned");
        }

        // ── Re-ApplyMeal replaces record keeping latest expiry ───────────────────

        [TestMethod]
        [TestCategory("MealBuff")]
        public void ApplyMeal_ReplacesRecord_KeepsLatestExpiry()
        {
            var service = new MealBuffService();
            var hero = MakeHero();

            service.ApplyMeal(hero, DishType.RoastedOnionSkewers, false, 200f);
            service.ApplyMeal(hero, DishType.RoastedOnionSkewers, true,  700f); // replace with later expiry

            // Should survive a prune at the earlier expiry
            service.Prune(200f);
            Assert.IsTrue(service.TryGetMeal(hero, out _, out _),
                "Record with later expiry should survive a prune that would remove the earlier one");

            // Should be removed at the later expiry
            service.Prune(700f);
            Assert.IsFalse(service.TryGetMeal(hero, out _, out _),
                "Record should be pruned at its latest expiry");
        }

        // ── Multiple combatants: only expired ones pruned ────────────────────────

        [TestMethod]
        [TestCategory("MealBuff")]
        public void Prune_OnlyExpiredCombatants_Removed()
        {
            var service = new MealBuffService();
            var hero1 = MakeHero();
            var hero2 = MakeHero();

            service.ApplyMeal(hero1, Dish, false, 100f); // expires at 100
            service.ApplyMeal(hero2, Dish, false, 800f); // expires at 800

            service.Prune(100f); // hero1 expires, hero2 does not

            Assert.IsFalse(service.TryGetMeal(hero1, out _, out _), "hero1 record should be pruned");
            Assert.IsTrue(service.TryGetMeal(hero2, out _, out _),  "hero2 record should remain");
        }
    }
}
