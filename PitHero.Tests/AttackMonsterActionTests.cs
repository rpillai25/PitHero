using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.AI;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Stats;

namespace PitHero.Tests
{
    [TestClass]
    public class AttackMonsterActionTests
    {
        /// <summary>Test that Slime now has fixed stats regardless of level</summary>
        [TestMethod]
        [TestCategory("Combat")]
        public void Slime_FixedStats_DoNotScaleWithLevel()
        {
            // Arrange & Act: Create slimes of different levels
            var slimeLevel1 = new Slime(1);
            var slimeLevel5 = new Slime(5);
            var slimeLevel10 = new Slime(10);

            // Assert: Stats should be identical regardless of level (all use preset level 1)
            Assert.AreEqual(slimeLevel1.Stats.Strength, slimeLevel5.Stats.Strength, "Slime strength should be fixed across levels");
            Assert.AreEqual(slimeLevel1.Stats.Agility, slimeLevel5.Stats.Agility, "Slime agility should be fixed across levels");
            Assert.AreEqual(slimeLevel1.Stats.Vitality, slimeLevel5.Stats.Vitality, "Slime vitality should be fixed across levels");
            Assert.AreEqual(slimeLevel1.Stats.Magic, slimeLevel5.Stats.Magic, "Slime magic should be fixed across levels");

            Assert.AreEqual(slimeLevel1.Stats.Strength, slimeLevel10.Stats.Strength, "Slime strength should be fixed across levels");
            Assert.AreEqual(slimeLevel1.Stats.Agility, slimeLevel10.Stats.Agility, "Slime agility should be fixed across levels");
            Assert.AreEqual(slimeLevel1.Stats.Vitality, slimeLevel10.Stats.Vitality, "Slime vitality should be fixed across levels");
            Assert.AreEqual(slimeLevel1.Stats.Magic, slimeLevel10.Stats.Magic, "Slime magic should be fixed across levels");

            // Experience yield should also be fixed
            Assert.AreEqual(slimeLevel1.ExperienceYield, slimeLevel5.ExperienceYield, "Slime experience yield should be fixed across levels");
            Assert.AreEqual(slimeLevel1.ExperienceYield, slimeLevel10.ExperienceYield, "Slime experience yield should be fixed across levels");
            Assert.AreEqual(18, slimeLevel1.ExperienceYield, "Slime should give exactly 18 experience");
        }

        /// <summary>Test the basic structure of BattleParticipant</summary>
        [TestMethod]
        [TestCategory("Combat")]
        public void BattleParticipant_Structure_WorksCorrectly()
        {
            // Arrange: Create participants for hero and monster (using nulls since we're just testing structure)
            var heroParticipant = new BattleParticipant((PitHero.ECS.Components.HeroComponent)null);
            var monsterParticipant = new BattleParticipant((Nez.Entity)null);

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
    }
}