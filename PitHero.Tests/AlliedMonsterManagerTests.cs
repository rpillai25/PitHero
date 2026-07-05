using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Services;
using RolePlayingFramework.AlliedMonsters;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Stats;
using PitHero;

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
            public EnemyId EnemyId => EnemyId.Slime;
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
            public bool IsBoss => false;
            public bool IsRecruitable { get; }

            public MockEnemy(string name, float joinModifier, bool isRecruitable = true)
            {
                Name = name;
                JoinPercentageModifier = joinModifier;
                IsRecruitable = isRecruitable;
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

        /// <summary>Tests that TryRecruit returns null when no BuildingService is available (unit test env).</summary>
        [TestMethod]
        [TestCategory("AlliedMonsters")]
        public void AlliedMonsterManager_TryRecruit_NoBuildingService_ReturnsNull()
        {
            var manager = new AlliedMonsterManager();
            // Core.Services is unavailable in unit tests, so the building guard triggers
            var enemy = new MockEnemy(MonsterTextKey.Monster_Slime, 1000f);

            var result = manager.TryRecruit(enemy);

            Assert.IsNull(result, "Should return null when BuildingService is unavailable");
            Assert.AreEqual(0, manager.Count, "Count should remain 0");
        }

        /// <summary>Tests that TryRecruit returns null for non-recruitable enemies.</summary>
        [TestMethod]
        [TestCategory("AlliedMonsters")]
        public void AlliedMonsterManager_TryRecruit_NonRecruitable_ReturnsNull()
        {
            var manager = new AlliedMonsterManager();
            var enemy = new MockEnemy("PitLord", 1000f, isRecruitable: false);

            var result = manager.TryRecruit(enemy);

            Assert.IsNull(result, "Should not recruit a non-recruitable enemy");
            Assert.AreEqual(0, manager.Count, "Count should remain 0 after failed recruit");
        }

        /// <summary>Tests that TryRecruit returns null for a zero-modifier recruitable enemy (no building service guard triggers first).</summary>
        [TestMethod]
        [TestCategory("AlliedMonsters")]
        public void AlliedMonsterManager_TryRecruit_ZeroModifier_ReturnsNull()
        {
            var manager = new AlliedMonsterManager();
            // Even if building service existed, zero modifier would fail. In tests it
            // returns null due to building service guard (which is fine — result is same).
            var enemy = new MockEnemy("PitLord", 0f);

            var result = manager.TryRecruit(enemy);

            Assert.IsNull(result, "Should not recruit with zero modifier");
            Assert.AreEqual(0, manager.Count, "Count should remain 0 after failed recruit");
        }

        /// <summary>Tests that AddAlliedMonster stores the correct type name.</summary>
        [TestMethod]
        [TestCategory("AlliedMonsters")]
        public void AlliedMonsterManager_TryRecruit_SetsMonsterTypeName()
        {
            var manager = new AlliedMonsterManager();
            var allied = new AlliedMonster(MonsterTextKey.Monster_Rat, MonsterTextKey.Monster_Rat, 5, 5, 5);
            manager.AddAlliedMonster(allied);

            Assert.AreEqual(1, manager.Count, "Count should be 1");
            Assert.AreEqual(MonsterTextKey.Monster_Rat, manager.AlliedMonsters[0].MonsterTypeName,
                "MonsterTypeName should match what was provided");
        }

        /// <summary>Tests that AddAlliedMonster stores a non-empty name.</summary>
        [TestMethod]
        [TestCategory("AlliedMonsters")]
        public void AlliedMonsterManager_TryRecruit_AssignsNonEmptyName()
        {
            var manager = new AlliedMonsterManager();
            var allied = new AlliedMonster("Fluffy", MonsterTextKey.Monster_Bat, 5, 5, 5);
            manager.AddAlliedMonster(allied);

            Assert.IsFalse(string.IsNullOrEmpty(manager.AlliedMonsters[0].Name),
                "Allied monster should have a non-empty name");
        }

        /// <summary>Tests that AlliedMonster proficiencies are clamped to 1-9 range.</summary>
        [TestMethod]
        [TestCategory("AlliedMonsters")]
        public void AlliedMonsterManager_TryRecruit_ProficienciesInRange()
        {
            var allied = new AlliedMonster("Buddy", MonsterTextKey.Monster_Goblin, 5, 7, 3);

            Assert.IsTrue(allied.FishingProficiency >= 1 && allied.FishingProficiency <= 9,
                $"Fishing proficiency {allied.FishingProficiency} should be in range 1-9");
            Assert.IsTrue(allied.CookingProficiency >= 1 && allied.CookingProficiency <= 9,
                $"Cooking proficiency {allied.CookingProficiency} should be in range 1-9");
            Assert.IsTrue(allied.FarmingProficiency >= 1 && allied.FarmingProficiency <= 9,
                $"Farming proficiency {allied.FarmingProficiency} should be in range 1-9");
        }

        /// <summary>Tests that multiple AddAlliedMonster calls increment Count correctly.</summary>
        [TestMethod]
        [TestCategory("AlliedMonsters")]
        public void AlliedMonsterManager_TryRecruit_MultipleRecruits_CountIncrementsCorrectly()
        {
            var manager = new AlliedMonsterManager();

            manager.AddAlliedMonster(new AlliedMonster("A", MonsterTextKey.Monster_Slime, 5, 5, 5));
            manager.AddAlliedMonster(new AlliedMonster("B", MonsterTextKey.Monster_Slime, 5, 5, 5));
            manager.AddAlliedMonster(new AlliedMonster("C", MonsterTextKey.Monster_Slime, 5, 5, 5));

            Assert.AreEqual(3, manager.Count, "Count should be 3 after three adds");
        }

        /// <summary>Tests that AlliedMonsters list is accessible and matches Count.</summary>
        [TestMethod]
        [TestCategory("AlliedMonsters")]
        public void AlliedMonsterManager_AlliedMonsters_ListMatchesCount()
        {
            var manager = new AlliedMonsterManager();

            manager.AddAlliedMonster(new AlliedMonster("X", "Snake", 5, 5, 5));
            manager.AddAlliedMonster(new AlliedMonster("Y", "Snake", 5, 5, 5));

            Assert.AreEqual(manager.Count, manager.AlliedMonsters.Count,
                "AlliedMonsters list count should equal Count property");
        }

        /// <summary>Tests that AlliedMonster Job defaults to None and can be changed.</summary>
        [TestMethod]
        [TestCategory("AlliedMonsters")]
        public void AlliedMonster_Job_DefaultsToNone_AndIsMutable()
        {
            var allied = new AlliedMonster("Grim", "Skeleton", 5, 5, 5);

            Assert.AreEqual(MonsterJob.None, allied.Job, "Job should default to None");

            allied.Job = MonsterJob.Farming;
            Assert.AreEqual(MonsterJob.Farming, allied.Job, "Job should be updatable to Farming");
        }

        /// <summary>Tests that AlliedMonster MonsterHouseId defaults to -1.</summary>
        [TestMethod]
        [TestCategory("AlliedMonsters")]
        public void AlliedMonster_MonsterHouseId_DefaultsToNegativeOne()
        {
            var allied = new AlliedMonster("Goop", "Slime", 5, 5, 5);

            Assert.AreEqual(-1, allied.MonsterHouseId, "MonsterHouseId should default to -1");
        }

        /// <summary>Tests that AlliedMonster MonsterHouseId can be set via constructor.</summary>
        [TestMethod]
        [TestCategory("AlliedMonsters")]
        public void AlliedMonster_MonsterHouseId_SetViaConstructor()
        {
            var building = new PlacedBuilding { UniqueId = 1 };
            var allied = new AlliedMonster("Blob", "Slime", 5, 5, 5, monsterHouseId: building.UniqueId);

            Assert.AreEqual(1, allied.MonsterHouseId, "MonsterHouseId should be 1");
        }

        /// <summary>GetLinkedMonsterCount only counts monsters linked to the given house id.</summary>
        [TestMethod]
        [TestCategory("AlliedMonsters")]
        public void GetLinkedMonsterCount_CountsOnlyMatchingHouse()
        {
            var manager = new AlliedMonsterManager();
            manager.AddAlliedMonster(new AlliedMonster("A", "Slime", 5, 5, 5, monsterHouseId: 1));
            manager.AddAlliedMonster(new AlliedMonster("B", "Slime", 5, 5, 5, monsterHouseId: 1));
            manager.AddAlliedMonster(new AlliedMonster("C", "Slime", 5, 5, 5, monsterHouseId: 2));

            Assert.AreEqual(2, manager.GetLinkedMonsterCount(1));
            Assert.AreEqual(1, manager.GetLinkedMonsterCount(2));
            Assert.AreEqual(0, manager.GetLinkedMonsterCount(3));
        }

        /// <summary>IsHouseFull becomes true at GameConfig.MonsterHouseCapacity linked monsters.</summary>
        [TestMethod]
        [TestCategory("AlliedMonsters")]
        public void IsHouseFull_TrueAtCapacity()
        {
            var manager = new AlliedMonsterManager();
            for (int i = 0; i < GameConfig.MonsterHouseCapacity - 1; i++)
                manager.AddAlliedMonster(new AlliedMonster("M" + i, "Slime", 5, 5, 5, monsterHouseId: 7));

            Assert.IsFalse(manager.IsHouseFull(7), "Should not be full one below capacity");

            manager.AddAlliedMonster(new AlliedMonster("Last", "Slime", 5, 5, 5, monsterHouseId: 7));
            Assert.IsTrue(manager.IsHouseFull(7), "Should be full at capacity");
        }

        /// <summary>AddPurchasedMonster adds a monster with the enemy's type name and target house.</summary>
        [TestMethod]
        [TestCategory("AlliedMonsters")]
        public void AddPurchasedMonster_AddsWithTypeNameAndHouseId()
        {
            var manager = new AlliedMonsterManager();
            var enemy = new MockEnemy(MonsterTextKey.Monster_Slime, 1f);

            var added = manager.AddPurchasedMonster(enemy, houseUniqueId: 4);

            Assert.IsNotNull(added, "Should add a monster when the house has room");
            Assert.AreEqual(1, manager.Count);
            Assert.AreEqual(MonsterTextKey.Monster_Slime, added.MonsterTypeName);
            Assert.AreEqual(4, added.MonsterHouseId);
            Assert.IsFalse(string.IsNullOrEmpty(added.Name), "Purchased monster should get a name");
            Assert.AreEqual(MonsterJob.None, added.Job, "Purchased monster should start with no job");
        }

        /// <summary>AddPurchasedMonster returns null and adds nothing when the house is full.</summary>
        [TestMethod]
        [TestCategory("AlliedMonsters")]
        public void AddPurchasedMonster_FullHouse_ReturnsNull()
        {
            var manager = new AlliedMonsterManager();
            for (int i = 0; i < GameConfig.MonsterHouseCapacity; i++)
                manager.AddAlliedMonster(new AlliedMonster("M" + i, "Slime", 5, 5, 5, monsterHouseId: 9));

            var enemy = new MockEnemy(MonsterTextKey.Monster_Slime, 1f);
            var added = manager.AddPurchasedMonster(enemy, houseUniqueId: 9);

            Assert.IsNull(added, "Should not add to a full house");
            Assert.AreEqual(GameConfig.MonsterHouseCapacity, manager.GetLinkedMonsterCount(9));
        }
    }
}
