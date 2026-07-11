using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nez;
using PitHero.AI;
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

        /// <summary>Test the basic structure of BattleParticipant</summary>
        [TestMethod]
        [TestCategory("Combat")]
        public void BattleParticipant_Structure_WorksCorrectly()
        {
#pragma warning disable CS8625
            // Arrange: Create participants for hero and monster (using nulls since we're just testing structure)
            var heroParticipant = new BattleParticipant((PitHero.ECS.Components.HeroComponent)null!);
            var monsterParticipant = new BattleParticipant((Nez.Entity)null!);
#pragma warning restore CS8625

            // Assert: Participants should have correct types
            Assert.AreEqual(BattleParticipant.ParticipantType.Hero, heroParticipant.Type, "Hero participant should be marked as hero");
            Assert.AreEqual(BattleParticipant.ParticipantType.Monster, monsterParticipant.Type, "Monster participant should be marked as monster");

            // Test that we can set turn values
            heroParticipant.TurnValue = 5.5f;
            monsterParticipant.TurnValue = 3.2f;

            Assert.AreEqual(5.5f, heroParticipant.TurnValue, 0.001f, "Hero turn value should be settable");
            Assert.AreEqual(3.2f, monsterParticipant.TurnValue, 0.001f, "Monster turn value should be settable");
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

        // ── SelectPrimaryTarget helper tests (Fix 3) ──────────────────────────────────

        [TestMethod]
        [TestCategory("Combat")]
        public void SelectPrimaryTarget_NullPreferred_DoesNotReorderList()
        {
            var e0 = new Entity("e0");
            var e1 = new Entity("e1");
            var e2 = new Entity("e2");
            var list = new List<Entity> { e0, e1, e2 };

            AttackMonsterAction.SelectPrimaryTarget(list, null);

            Assert.AreEqual(e0, list[0], "Null preferred: list[0] should be unchanged");
            Assert.AreEqual(e1, list[1]);
            Assert.AreEqual(e2, list[2]);
        }

        [TestMethod]
        [TestCategory("Combat")]
        public void SelectPrimaryTarget_PreferredAtIndex0_DoesNotReorderList()
        {
            var e0 = new Entity("e0");
            var e1 = new Entity("e1");
            var list = new List<Entity> { e0, e1 };

            AttackMonsterAction.SelectPrimaryTarget(list, e0);

            Assert.AreEqual(e0, list[0], "Preferred already at index 0: list unchanged");
            Assert.AreEqual(e1, list[1]);
        }

        [TestMethod]
        [TestCategory("Combat")]
        public void SelectPrimaryTarget_PreferredAtIndex2_MovedToIndex0()
        {
            var e0 = new Entity("e0");
            var e1 = new Entity("e1");
            var e2 = new Entity("e2");
            var list = new List<Entity> { e0, e1, e2 };

            AttackMonsterAction.SelectPrimaryTarget(list, e2);

            Assert.AreEqual(e2, list[0], "Preferred at index 2 should be swapped to index 0");
            // e0 ends up at index 2 (swapped), e1 stays at index 1
            Assert.AreEqual(e1, list[1], "e1 should be unchanged at index 1");
            Assert.AreEqual(e0, list[2], "e0 (original index 0) should be at index 2 after swap");
        }

        [TestMethod]
        [TestCategory("Combat")]
        public void SelectPrimaryTarget_PreferredNotInList_DoesNotReorderList()
        {
            var e0 = new Entity("e0");
            var e1 = new Entity("e1");
            var eOther = new Entity("eOther"); // not in list
            var list = new List<Entity> { e0, e1 };

            AttackMonsterAction.SelectPrimaryTarget(list, eOther);

            Assert.AreEqual(e0, list[0], "Preferred not in list: list[0] should be unchanged");
            Assert.AreEqual(e1, list[1]);
        }

        [TestMethod]
        [TestCategory("Combat")]
        public void SelectPrimaryTarget_EmptyList_DoesNotThrow()
        {
            var list = new List<Entity>();
            var e = new Entity("e");
            // Should not throw; list stays empty
            AttackMonsterAction.SelectPrimaryTarget(list, e);
            Assert.AreEqual(0, list.Count, "Empty list should remain empty");
        }
    }
}