using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.AI;
using PitHero.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Stats;
using System.Collections.Generic;

namespace PitHero.Tests
{
    [TestClass]
    public class AttackMonsterActionTests
    {
        /// <summary>Test that Slime stats scale with constructor level.</summary>
        [TestMethod]
        [TestCategory("Combat")]
        public void Slime_FixedStats_DoNotScaleWithLevel()
        {
            // Arrange & Act: Create slimes of different levels
            var slimeLevel1 = new Slime(1);
            var slimeLevel5 = new Slime(5);
            var slimeLevel10 = new Slime(10);

            // Assert: Stats and rewards scale up with requested level
            Assert.IsTrue(slimeLevel5.Stats.Strength >= slimeLevel1.Stats.Strength, "Slime strength should scale across levels");
            Assert.IsTrue(slimeLevel10.Stats.Strength >= slimeLevel5.Stats.Strength, "Slime strength should scale across levels");
            Assert.IsTrue(slimeLevel5.MaxHP > slimeLevel1.MaxHP, "Slime HP should scale across levels");
            Assert.IsTrue(slimeLevel10.MaxHP > slimeLevel5.MaxHP, "Slime HP should scale across levels");
            Assert.IsTrue(slimeLevel5.ExperienceYield > slimeLevel1.ExperienceYield, "Slime experience yield should scale across levels");
            Assert.IsTrue(slimeLevel10.ExperienceYield > slimeLevel5.ExperienceYield, "Slime experience yield should scale across levels");
        }

        /// <summary>Test that AttackMonsterAction can be instantiated and has correct basic properties</summary>
        [TestMethod]
        [TestCategory("GOAP")]
        public void AttackMonsterAction_Instantiation_HasCorrectProperties()
        {
            // Act: Create the action
            var action = new AttackMonsterAction();

            // Assert: Action should have correct basic properties
            Assert.IsNotNull(action, "AttackMonsterAction should be instantiable");
            Assert.AreEqual(GoapConstants.AttackMonster, action.Name, "Action should have correct name");
            Assert.AreEqual(3, action.Cost, "Action should have correct cost");
        }

        // ── BattleEngine.SelectPrimaryTarget helper tests (Fix 3) ─────────────────────

        [TestMethod]
        [TestCategory("Combat")]
        public void SelectPrimaryTarget_NullPreferred_DoesNotReorderList()
        {
            var e0 = new Slime(1);
            var e1 = new Slime(2);
            var e2 = new Slime(3);
            var list = new List<IEnemy> { e0, e1, e2 };

            BattleEngine.SelectPrimaryTarget(list, null);

            Assert.AreEqual(e0, list[0], "Null preferred: list[0] should be unchanged");
            Assert.AreEqual(e1, list[1]);
            Assert.AreEqual(e2, list[2]);
        }

        [TestMethod]
        [TestCategory("Combat")]
        public void SelectPrimaryTarget_PreferredAtIndex0_DoesNotReorderList()
        {
            var e0 = new Slime(1);
            var e1 = new Slime(2);
            var list = new List<IEnemy> { e0, e1 };

            BattleEngine.SelectPrimaryTarget(list, e0);

            Assert.AreEqual(e0, list[0], "Preferred already at index 0: list unchanged");
            Assert.AreEqual(e1, list[1]);
        }

        [TestMethod]
        [TestCategory("Combat")]
        public void SelectPrimaryTarget_PreferredAtIndex2_MovedToIndex0()
        {
            var e0 = new Slime(1);
            var e1 = new Slime(2);
            var e2 = new Slime(3);
            var list = new List<IEnemy> { e0, e1, e2 };

            BattleEngine.SelectPrimaryTarget(list, e2);

            Assert.AreEqual(e2, list[0], "Preferred at index 2 should be swapped to index 0");
            // e0 ends up at index 2 (swapped), e1 stays at index 1
            Assert.AreEqual(e1, list[1], "e1 should be unchanged at index 1");
            Assert.AreEqual(e0, list[2], "e0 (original index 0) should be at index 2 after swap");
        }

        [TestMethod]
        [TestCategory("Combat")]
        public void SelectPrimaryTarget_PreferredNotInList_DoesNotReorderList()
        {
            var e0 = new Slime(1);
            var e1 = new Slime(2);
            var eOther = new Slime(99); // not in list
            var list = new List<IEnemy> { e0, e1 };

            BattleEngine.SelectPrimaryTarget(list, eOther);

            Assert.AreEqual(e0, list[0], "Preferred not in list: list[0] should be unchanged");
            Assert.AreEqual(e1, list[1]);
        }

        [TestMethod]
        [TestCategory("Combat")]
        public void SelectPrimaryTarget_EmptyList_DoesNotThrow()
        {
            var list = new List<IEnemy>();
            var e = new Slime(1);
            // Should not throw; list stays empty
            BattleEngine.SelectPrimaryTarget(list, e);
            Assert.AreEqual(0, list.Count, "Empty list should remain empty");
        }
    }
}
