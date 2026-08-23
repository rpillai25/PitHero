using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Util;
using RolePlayingFramework;
using System.Collections.Generic;

namespace PitHero.Tests
{
    /// <summary>
    /// Tests for the gendered human name pools and the syllable-forged monster name pool.
    /// The pools are private, so every assertion here is behavioural: sample enough names that a
    /// missing entry is statistically impossible, then assert membership and disjointness.
    /// </summary>
    [TestClass]
    public class NameGeneratorTests
    {
        private const int Samples = 5000;

        /// <summary>Names that must never be handed to a male character.</summary>
        private static readonly string[] KnownFemaleOnly =
        {
            "Diana", "Elara", "Helena", "Jade", "Luna", "Nina", "Petra", "Sasha", "Brynn"
        };

        private static HashSet<string> SampleFirstNames(Gender gender)
        {
            var seen = new HashSet<string>();
            for (int i = 0; i < Samples; i++)
                seen.Add(NameGenerator.GenerateFirstName(gender));
            return seen;
        }

        [TestMethod]
        public void GenerateRandomName_IsFirstAndLastName()
        {
            for (int i = 0; i < 200; i++)
            {
                var parts = NameGenerator.GenerateRandomName(Gender.Male).Split(' ');
                Assert.AreEqual(2, parts.Length, "A human name is exactly 'First Last'");
                Assert.IsFalse(string.IsNullOrWhiteSpace(parts[0]));
                Assert.IsFalse(string.IsNullOrWhiteSpace(parts[1]));
            }
        }

        [TestMethod]
        public void GenerateRandomName_DefaultsToMale()
        {
            var defaulted = new HashSet<string>();
            for (int i = 0; i < Samples; i++)
                defaulted.Add(NameGenerator.GenerateRandomName().Split(' ')[0]);

            foreach (var female in KnownFemaleOnly)
                Assert.IsFalse(defaulted.Contains(female), $"The default gender must be male, but '{female}' was generated");
        }

        [TestMethod]
        public void MaleFirstNames_ContainOwnerAdditions_AndNoFemaleNames()
        {
            var male = SampleFirstNames(Gender.Male);

            foreach (var required in new[] { "John", "Adrian", "Tom" })
                Assert.IsTrue(male.Contains(required), $"Male pool must contain '{required}'");

            foreach (var female in KnownFemaleOnly)
                Assert.IsFalse(male.Contains(female), $"Male pool must not contain '{female}'");
        }

        [TestMethod]
        public void FemaleFirstNames_ArePopulated_AndDisjointFromMale()
        {
            var female = SampleFirstNames(Gender.Female);
            var male = SampleFirstNames(Gender.Male);

            Assert.IsTrue(female.Count >= 40, "The female pool should be authored ahead of the art");
            foreach (var expected in new[] { "Diana", "Luna", "Petra" })
                Assert.IsTrue(female.Contains(expected), $"Female pool must contain '{expected}'");

            female.IntersectWith(male);
            Assert.AreEqual(0, female.Count, "The male and female first-name pools must not overlap");
        }

        [TestMethod]
        public void LastNames_ContainOwnerAdditions()
        {
            var surnames = new HashSet<string>();
            for (int i = 0; i < Samples; i++)
                surnames.Add(NameGenerator.GenerateRandomName(Gender.Male).Split(' ')[1]);

            foreach (var required in new[] { "Hall", "Romero", "Carmack", "Happ", "Blow", "Brush" })
                Assert.IsTrue(surnames.Contains(required), $"Surname pool must contain '{required}'");
        }

        [TestMethod]
        public void GenerateMonsterName_IsSingleCapitalizedToken()
        {
            for (int i = 0; i < 500; i++)
            {
                var name = NameGenerator.GenerateMonsterName();
                Assert.IsFalse(string.IsNullOrWhiteSpace(name), "Monster names are never empty");
                Assert.IsFalse(name.Contains(" "), $"Monsters get a first name only, but got '{name}'");
                Assert.IsTrue(char.IsUpper(name[0]), $"Monster names are capitalized, but got '{name}'");
            }
        }

        [TestMethod]
        public void GenerateMonsterName_NeverCollidesWithHumanNames()
        {
            var monsters = new HashSet<string>();
            for (int i = 0; i < Samples; i++)
                monsters.Add(NameGenerator.GenerateMonsterName());

            Assert.IsTrue(monsters.Count >= 500, $"Monster pool should be varied, only saw {monsters.Count} distinct names");

            var humans = SampleFirstNames(Gender.Male);
            humans.UnionWith(SampleFirstNames(Gender.Female));

            monsters.IntersectWith(humans);
            Assert.AreEqual(0, monsters.Count, "Monster names must never overlap the human first-name pools");
        }
    }
}
