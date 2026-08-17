using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using PitHero.AI;
using PitHero.VirtualGame;

namespace PitHero.Tests
{
    /// <summary>
    /// Verifies the runtime boss gate in ActivateWizardOrbAction: on a boss floor the
    /// orb must not advance the pit while the boss is alive, regardless of how the
    /// GOAP plan degraded (e.g. Advance priority routing the hero straight to the orb).
    /// </summary>
    [TestClass]
    public class ActivateWizardOrbBossGuardTests
    {
        [TestMethod]
        public void BossFloor_OrbActivation_RefusedWhileBossAlive_AllowedAfterDefeat()
        {
            var world = new VirtualWorldState();
            var context = new VirtualGoapContext(world);
            context.PitWidthManager.Initialize();

            // Boss floor 5 spawns exactly one monster: the boss
            context.PitGenerator.RegenerateForLevel(5);
            Assert.IsTrue(world.HasLivingBoss(), "Sanity: level 5 regeneration should spawn a living boss");
            Assert.IsFalse(context.HeroController.BossDefeated, "Sanity: BossDefeated should derive false while the boss lives");
            Assert.AreEqual(1, world.LastGeneratedBossMonsterCount, "Sanity: level 5 should generate exactly one boss");

            // Simulate a degraded plan reaching the orb with the boss alive
            context.HeroController.ExploredPit = true;
            context.PitLevelManager.QueueLevel(6);

            var action = new ActivateWizardOrbAction();
            var completed = action.Execute(context);

            Assert.IsTrue(completed, "Guard should pop the action (return true) so the hero replans");
            Assert.IsTrue(context.PitLevelManager.HasQueuedLevel, "Queued level must not be consumed while the boss is alive");
            Assert.IsTrue(world.HasLivingBoss(), "Boss must survive a refused activation");
            Assert.AreEqual(1, world.LastGeneratedBossMonsterCount, "Pit content must not regenerate while the boss is alive");
            Assert.IsFalse(context.HeroController.ExploredPit, "Guard should reset ExploredPit so wandering resumes toward the boss");

            // Defeat the boss (the floor's only monster)
            var bossPos = world.GetNearestLivingMonsterPosition(Point.Zero);
            Assert.IsTrue(bossPos.HasValue, "Sanity: boss position should be found");
            Assert.IsTrue(world.TryGetMonsterAt(bossPos.Value, out var boss), "Sanity: boss instance should be retrievable");
            world.RemoveMonster(boss);
            Assert.IsFalse(world.HasLivingBoss(), "Sanity: boss should be gone after removal");

            // Now activation should proceed and advance the pit (queued level 6 still pending)
            completed = action.Execute(context);

            Assert.IsTrue(completed, "Activation should complete once the boss is defeated");
            Assert.IsFalse(context.PitLevelManager.HasQueuedLevel, "Queued level should be consumed once activation succeeds");
            Assert.AreEqual(0, world.LastGeneratedBossMonsterCount, "Level 6 (non-boss) content should have been generated");
            Assert.IsTrue(context.HeroController.BossDefeated, "No boss gates the new non-boss floor");
        }
    }
}
