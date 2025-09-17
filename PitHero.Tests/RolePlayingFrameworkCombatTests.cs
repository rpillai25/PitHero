using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Jobs;
using RolePlayingFramework.Stats;

namespace PitHero.Tests
{
    [TestClass]
    public class RolePlayingFrameworkCombatTests
    {
        /// <summary>Ensures a Knight hero can hit and damage a Slime.</summary>
        [TestMethod]
        [TestCategory("Combat")]
        public void HeroKnightAttacksSlime_ReducesHP()
        {
            // Arrange: strong Knight to reduce randomness and ensure some damage lands
            var knight = new Knight();
            var baseStats = new StatBlock(strength: 12, agility: 10, vitality: 10, magic: 2);
            var hero = new Hero(name: "Test Knight", job: knight, level: 6, baseStats: baseStats);

            var slime = new Slime(level: 1);
            var resolver = new SimpleAttackResolver();

            // Act: try a few times to avoid flakiness due to RNG miss (acc capped at 95%)
            var attempts = 10;
            var startedHP = slime.CurrentHP;
            var hitOccurred = false;
            AttackResult lastResult = default;

            for (int i = 0; i < attempts; i++)
            {
                lastResult = Battle.HeroAttack(resolver, hero, slime, DamageKind.Physical);
                if (lastResult.Hit)
                {
                    hitOccurred = true;
                    break;
                }
            }

            // Assert
            Assert.IsTrue(hitOccurred, $"Hero did not land a hit after {attempts} attempts.");
            Assert.IsTrue(lastResult.Damage > 0, "A successful hit must deal positive damage.");
            Assert.IsTrue(slime.CurrentHP < startedHP || slime.CurrentHP == 0, "Slime HP should be reduced on a hit.");
        }
    }
}
