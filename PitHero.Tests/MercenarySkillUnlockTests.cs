using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nez.Persistence.Binary;
using PitHero;
using PitHero.Services;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Mercenaries;
using RolePlayingFramework.Skills;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Synergies;

namespace PitHero.Tests
{
    /// <summary>Mercenary job skills unlock one at a time to match the hero's class skills (issue #471).</summary>
    [TestClass]
    public class MercenarySkillUnlockTests
    {
        private static Mercenary NewKnightMerc() => new Mercenary("Merc", new Knight(), 5, new StatBlock(5, 5, 5, 5));

        private static Hero NewHeroWithSkills(params string[] skillIds)
        {
            var crystal = new HeroCrystal("Crystal", new Knight(), 5, new StatBlock(5, 5, 5, 5));
            for (int i = 0; i < skillIds.Length; i++)
                crystal.AddLearnedSkill(skillIds[i]);
            return new Hero("Hero", new Knight(), 5, new StatBlock(5, 5, 5, 5), crystal);
        }

        [TestMethod]
        public void NewMercenary_KnowsNoSkills()
        {
            Assert.AreEqual(0, NewKnightMerc().UnlockedJobSkillCount);
        }

        [TestMethod]
        public void UnlockJobSkillsUpTo_UnlocksActivesBeforePassives()
        {
            // Knight job order: Provoke (active), Heavy Armor (passive), Spin Slash, Heavy Strike
            var merc = NewKnightMerc();

            merc.UnlockJobSkillsUpTo(1);
            Assert.IsTrue(merc.LearnedSkills.ContainsKey("knight.provoke"));
            Assert.AreEqual(1, merc.LearnedSkills.Count);

            merc.UnlockJobSkillsUpTo(3);
            Assert.AreEqual(3, merc.LearnedSkills.Count);
            Assert.IsFalse(merc.LearnedSkills.ContainsKey("knight.heavy_armor"), "the passive waits until every active is unlocked");
            foreach (var skill in merc.LearnedSkills.Values)
                Assert.AreEqual(SkillKind.Active, skill.Kind);
            Assert.AreEqual(0, merc.HeavyArmorDefenseBonus, "passive effect not applied yet");

            merc.UnlockJobSkillsUpTo(4);
            Assert.IsTrue(merc.LearnedSkills.ContainsKey("knight.heavy_armor"));
            Assert.IsTrue(merc.HeavyArmorDefenseBonus > 0, "the unlocked passive applies its effect");
        }

        [TestMethod]
        public void UnlockJobSkillsUpTo_NeverForgets_AndCapsAtJobSkillCount()
        {
            var merc = NewKnightMerc();
            Assert.AreEqual(2, merc.UnlockJobSkillsUpTo(2));
            Assert.AreEqual(0, merc.UnlockJobSkillsUpTo(1), "a lower target is a no-op");
            Assert.AreEqual(2, merc.UnlockedJobSkillCount);
            Assert.AreEqual(2, merc.UnlockJobSkillsUpTo(10));
            Assert.AreEqual(4, merc.UnlockedJobSkillCount);
        }

        [TestMethod]
        public void SkillUnlockTarget_FollowsHeroClassSkills()
        {
            Assert.AreEqual(0, Mercenary.SkillUnlockTargetFor(null));
            Assert.AreEqual(0, Mercenary.SkillUnlockTargetFor(NewHeroWithSkills()));
            Assert.AreEqual(1, Mercenary.SkillUnlockTargetFor(NewHeroWithSkills("knight.heavy_armor")));
            Assert.AreEqual(4, Mercenary.SkillUnlockTargetFor(
                NewHeroWithSkills("knight.provoke", "knight.heavy_armor", "knight.spin_slash", "knight.heavy_strike")));
        }

        [TestMethod]
        public void SkillUnlockTarget_IgnoresSynergySkills()
        {
            ISkill synergySkill = null;
            var patterns = SynergyPatternRegistry.All;
            for (int i = 0; i < patterns.Count && synergySkill == null; i++)
                synergySkill = patterns[i].UnlockedSkill;
            Assert.IsNotNull(synergySkill, "the registry should hold at least one synergy skill");

            var crystal = new HeroCrystal("Crystal", new Knight(), 5, new StatBlock(5, 5, 5, 5));
            crystal.AddLearnedSkill("knight.provoke");
            crystal.LearnSynergySkill(synergySkill.Id);
            var hero = new Hero("Hero", new Knight(), 5, new StatBlock(5, 5, 5, 5), crystal);

            Assert.IsTrue(hero.LearnedSkills.ContainsKey(synergySkill.Id), "synergy skill is learned");
            Assert.AreEqual(1, hero.ClassSkillCount);
            Assert.AreEqual(1, Mercenary.SkillUnlockTargetFor(hero));
        }

        [TestMethod]
        public void SaveData_V47_MercUnlockCount_RoundTrip()
        {
            var original = NewSaveWithMercs(2, 0);
            var loaded = RoundTrip(original, patchVersion: 0);
            Assert.AreEqual(2, loaded.HiredMercenaries[0].UnlockedSkillCount);
            Assert.AreEqual(0, loaded.HiredMercenaries[1].UnlockedSkillCount);
            Assert.AreEqual("MercB", loaded.HiredMercenaries[1].Name);
        }

        [TestMethod]
        public void SaveData_V46_File_MercsKeepEverySkill()
        {
            var loaded = RoundTrip(NewSaveWithMercs(2, 0), patchVersion: 46);
            Assert.AreEqual(-1, loaded.HiredMercenaries[0].UnlockedSkillCount, "pre-v47 mercs had all their skills");
            Assert.AreEqual(-1, loaded.HiredMercenaries[1].UnlockedSkillCount);
        }

        private static SaveData NewSaveWithMercs(int countA, int countB)
        {
            return new SaveData
            {
                HeroName = "Unlocker",
                HiredMercenaries = new List<SavedMercenary>
                {
                    new SavedMercenary { Name = "MercA", JobName = JobTextKey.Job_Knight_Name, EquipmentNames = new string[6], UnlockedSkillCount = countA },
                    new SavedMercenary { Name = "MercB", JobName = JobTextKey.Job_Mage_Name, EquipmentNames = new string[6], UnlockedSkillCount = countB }
                }
            };
        }

        // Writes the save, optionally patches the header version down, and reads it back
        private static SaveData RoundTrip(SaveData original, int patchVersion)
        {
            var ms = new MemoryStream();
            using (var writer = new BinaryPersistableWriter(ms))
                writer.Write(original);
            byte[] bytes = ms.ToArray();
            if (patchVersion > 0)
            {
                bytes[0] = (byte)patchVersion;
                bytes[1] = 0;
                bytes[2] = 0;
                bytes[3] = 0;
            }
            var loaded = new SaveData();
            using (var rdr = new BinaryPersistableReader(new MemoryStream(bytes)))
                rdr.ReadPersistableInto(loaded);
            return loaded;
        }
    }
}
