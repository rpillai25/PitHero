using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.Enemies;
using System;

namespace PitHero.Tests
{
    [TestClass]
    public class TempMonsterStatsTest
    {
        [TestMethod]
        public void PrintAllMonsterStats()
        {
            var monsters = new (string name, IEnemy monster)[] {
                ("Slime", new Slime()),
                ("Bat", new Bat()),
                ("Rat", new Rat()),
                ("Goblin", new Goblin()),
                ("Spider", new Spider()),
                ("Snake", new Snake()),
                ("Skeleton", new Skeleton()),
                ("Orc", new Orc()),
                ("Wraith", new Wraith()),
                ("PitLord", new PitLord())
            };

            Console.WriteLine("Monster Stats:");
            Console.WriteLine("=============================================================");
            foreach (var (name, monster) in monsters)
            {
                Console.WriteLine($"{name,-12} L{monster.Level,2}: HP={monster.MaxHP,3}, Str={monster.Stats.Strength,2}, Agi={monster.Stats.Agility,2}, Vit={monster.Stats.Vitality,2}, Mag={monster.Stats.Magic,2}, XP={monster.ExperienceYield,3}");
            }
            
            Assert.IsTrue(true); // Always pass, just want to see the output
        }
    }
}
