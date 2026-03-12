using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Services;
using RolePlayingFramework.AlliedMonsters;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Stats;

namespace PitHero.Tests
{
    [TestClass]
    public class AlliedMonsterManagerTests
    {
        /// <summary>Mock IEnemy implementation for recruitment testing.</summary>
        private class MockEnemy : IEnemy
        {
            private int _hp = 100;

            public string Name { get; }
            public int Level => 1;
            public StatBlock Stats => new StatBlock(5, 5, 5, 5);
            public DamageKind AttackKind => DamageKind.Physical;
            public ElementType Element => ElementType.Neutral;
            public ElementalProperties ElementalProps => new ElementalProperties(ElementType.Neutral);
            public int MaxHP => 100;
            public int CurrentHP => _hp;
            public int ExperienceYield => 10;
            public int JPYield => 5;
            public int SPYield => 1;
            public int GoldYield => 8;
            public float JoinPercentageModifier { get; }

            public MockEnemy(string name, float joinModifier)
            {
                Name = name;
                JoinPercentageModifier = joinModifier;
            }

            public bool TakeDamage(int amount)
            {
                if (amount <= 0) return false;
                _hp -= amount;
                if (_hp < 0) _hp = 0;
                return _hp == 0;
            }
        }

        /// <summary>Tests that a new AlliedMonsterManager starts with an empty roster.</summary>
        [TestMethod]
        [TestCategory("AlliedMonsters")]
        public void AlliedMonsterManager_New_HasEmptyRoster()
        {
            var manager = new AlliedMonsterManager();

            Assert.AreEqual(0, manager.Count, "New manager should have 0 allied monsters");
        }

        /// <summary>Tests that TryRecruit with a guaranteed modifier returns a non-null monster.</summary>
        [TestMethod]
        [TestCategory("AlliedMonsters")]
        public void AlliedMonsterManager_TryRecruit_GuaranteedJoin_ReturnsMonster()
        {
            var manager = new AlliedMonsterManager();
            // JoinPercentageModifier = 1000f => joinChance = 100.0f, always beats any 0..1 roll
            var enemy = new MockEnemy("Slime", 1000f);

            var result = manager.TryRecruit(enemy);

            Assert.IsNotNull(result, "Should recruit successfully with a very high modifier");
            Assert.AreEqual(1, manager.Count, "Count should be 1 after successful recruit");
        }

        /// <summary>Tests that TryRecruit with zero modifier never recruits.</summary>
        [TestMethod]
        [TestCategory("AlliedMonsters")]
        public void AlliedMonsterManager_TryRecruit_ZeroModifier_ReturnsNull()
        {
            var manager = new AlliedMonsterManager();
            // JoinPercentageModifier = 0f => joinChance = 0, which triggers early-out
            var enemy = new MockEnemy("PitLord", 0f);

            var result = manager.TryRecruit(enemy);

            Assert.IsNull(result, "Should not recruit with zero modifier");
            Assert.AreEqual(0, manager.Count, "Count should remain 0 after failed recruit");
        }

        /// <summary>Tests that recruited monster stores correct type name.</summary>
        [TestMethod]
        [TestCategory("AlliedMonsters")]
        public void AlliedMonsterManager_TryRecruit_SetsMonsterTypeName()
        {
            var manager = new AlliedMonsterManager();
            var enemy = new MockEnemy("Rat", 1000f);

            var result = manager.TryRecruit(enemy);

            Assert.IsNotNull(result, "Should recruit successfully");
            Assert.AreEqual("Rat", result.MonsterTypeName, "MonsterTypeName should match enemy Name");
        }

        /// <summary>Tests that recruited monster has a non-empty name.</summary>
        [TestMethod]
        [TestCategory("AlliedMonsters")]
        public void AlliedMonsterManager_TryRecruit_AssignsNonEmptyName()
        {
            var manager = new AlliedMonsterManager();
            var enemy = new MockEnemy("Bat", 1000f);

            var result = manager.TryRecruit(enemy);

            Assert.IsNotNull(result, "Should recruit successfully");
            Assert.IsFalse(string.IsNullOrEmpty(result.Name), "Recruited monster should have a non-empty name");
        }

        /// <summary>Tests that recruited monster proficiencies are within 1-9 range.</summary>
        [TestMethod]
        [TestCategory("AlliedMonsters")]
        public void AlliedMonsterManager_TryRecruit_ProficienciesInRange()
        {
            var manager = new AlliedMonsterManager();
            var enemy = new MockEnemy("Goblin", 1000f);

            var result = manager.TryRecruit(enemy);

            Assert.IsNotNull(result, "Should recruit successfully");
            Assert.IsTrue(result.FishingProficiency >= 1 && result.FishingProficiency <= 9,
                $"Fishing proficiency {result.FishingProficiency} should be in range 1-9");
            Assert.IsTrue(result.CookingProficiency >= 1 && result.CookingProficiency <= 9,
                $"Cooking proficiency {result.CookingProficiency} should be in range 1-9");
            Assert.IsTrue(result.FarmingProficiency >= 1 && result.FarmingProficiency <= 9,
                $"Farming proficiency {result.FarmingProficiency} should be in range 1-9");
        }

        /// <summary>Tests that multiple successful recruits increment Count correctly.</summary>
        [TestMethod]
        [TestCategory("AlliedMonsters")]
        public void AlliedMonsterManager_TryRecruit_MultipleRecruits_CountIncrementsCorrectly()
        {
            var manager = new AlliedMonsterManager();
            var enemy = new MockEnemy("Slime", 1000f);

            manager.TryRecruit(enemy);
            manager.TryRecruit(enemy);
            manager.TryRecruit(enemy);

            Assert.AreEqual(3, manager.Count, "Count should be 3 after three successful recruits");
        }

        /// <summary>Tests that AlliedMonsters list is accessible and matches Count.</summary>
        [TestMethod]
        [TestCategory("AlliedMonsters")]
        public void AlliedMonsterManager_AlliedMonsters_ListMatchesCount()
        {
            var manager = new AlliedMonsterManager();
            var enemy = new MockEnemy("Snake", 1000f);

            manager.TryRecruit(enemy);
            manager.TryRecruit(enemy);

            Assert.AreEqual(manager.Count, manager.AlliedMonsters.Count,
                "AlliedMonsters list count should equal Count property");
        }
    }
}
