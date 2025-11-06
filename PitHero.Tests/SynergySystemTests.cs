using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Skills;
using RolePlayingFramework.Stats;
using RolePlayingFramework.Synergies;
using System.Collections.Generic;

namespace PitHero.Tests
{
    [TestClass]
    public class SynergySystemTests
    {
        [TestMethod]
        public void SynergyPattern_Constructor_InitializesCorrectly()
        {
            var offsets = new List<Point> { new Point(0, 0), new Point(1, 0) };
            var kinds = new List<ItemKind> { ItemKind.WeaponSword, ItemKind.Shield };
            var effects = new List<ISynergyEffect>();
            
            var pattern = new SynergyPattern(
                "test_synergy",
                "Test Synergy",
                "A test synergy pattern",
                offsets,
                kinds,
                effects,
                100
            );
            
            Assert.AreEqual("test_synergy", pattern.Id);
            Assert.AreEqual("Test Synergy", pattern.Name);
            Assert.AreEqual(100, pattern.SynergyPointsRequired);
            Assert.AreEqual(2, pattern.GridOffsets.Count);
            Assert.AreEqual(2, pattern.RequiredKinds.Count);
        }
        
        [TestMethod]
        public void ActiveSynergy_StartsWithZeroPoints()
        {
            var offsets = new List<Point> { new Point(0, 0) };
            var kinds = new List<ItemKind> { ItemKind.WeaponSword };
            var effects = new List<ISynergyEffect>();
            
            var pattern = new SynergyPattern("test", "Test", "Test", offsets, kinds, effects, 100);
            var anchor = new Point(0, 0);
            var slots = new List<Point> { new Point(0, 0) };
            
            var activeSynergy = new ActiveSynergy(pattern, anchor, slots);
            
            Assert.AreEqual(0, activeSynergy.PointsEarned);
            Assert.IsFalse(activeSynergy.IsSkillUnlocked);
        }
        
        [TestMethod]
        public void ActiveSynergy_EarnPoints_IncreasesPointsEarned()
        {
            var offsets = new List<Point> { new Point(0, 0) };
            var kinds = new List<ItemKind> { ItemKind.WeaponSword };
            var effects = new List<ISynergyEffect>();
            
            var pattern = new SynergyPattern("test", "Test", "Test", offsets, kinds, effects, 100);
            var anchor = new Point(0, 0);
            var slots = new List<Point> { new Point(0, 0) };
            
            var activeSynergy = new ActiveSynergy(pattern, anchor, slots);
            
            activeSynergy.EarnPoints(50);
            Assert.AreEqual(50, activeSynergy.PointsEarned);
            
            activeSynergy.EarnPoints(30);
            Assert.AreEqual(80, activeSynergy.PointsEarned);
        }
        
        [TestMethod]
        public void ActiveSynergy_EarnPoints_NegativeAmountIgnored()
        {
            var offsets = new List<Point> { new Point(0, 0) };
            var kinds = new List<ItemKind> { ItemKind.WeaponSword };
            var effects = new List<ISynergyEffect>();
            
            var pattern = new SynergyPattern("test", "Test", "Test", offsets, kinds, effects, 100);
            var anchor = new Point(0, 0);
            var slots = new List<Point> { new Point(0, 0) };
            
            var activeSynergy = new ActiveSynergy(pattern, anchor, slots);
            
            activeSynergy.EarnPoints(50);
            activeSynergy.EarnPoints(-20);
            
            Assert.AreEqual(50, activeSynergy.PointsEarned);
        }
        
        [TestMethod]
        public void ActiveSynergy_IsSkillUnlocked_WhenPointsReachThreshold()
        {
            var offsets = new List<Point> { new Point(0, 0) };
            var kinds = new List<ItemKind> { ItemKind.WeaponSword };
            var effects = new List<ISynergyEffect>();
            var skill = new LightArmorPassive(); // Use an actual skill
            
            var pattern = new SynergyPattern("test", "Test", "Test", offsets, kinds, effects, 100, skill);
            var anchor = new Point(0, 0);
            var slots = new List<Point> { new Point(0, 0) };
            
            var activeSynergy = new ActiveSynergy(pattern, anchor, slots);
            
            Assert.IsFalse(activeSynergy.IsSkillUnlocked);
            
            activeSynergy.EarnPoints(100);
            Assert.IsTrue(activeSynergy.IsSkillUnlocked);
        }
        
        [TestMethod]
        public void SynergyDetector_RegisterPattern_AddsPattern()
        {
            var detector = new SynergyDetector();
            var offsets = new List<Point> { new Point(0, 0) };
            var kinds = new List<ItemKind> { ItemKind.WeaponSword };
            var effects = new List<ISynergyEffect>();
            
            var pattern = new SynergyPattern("test", "Test", "Test", offsets, kinds, effects, 100);
            
            detector.RegisterPattern(pattern);
            
            // Create a simple grid with a sword
            var grid = new IItem[2, 2];
            grid[0, 0] = GearItems.ShortSword();
            
            var synergies = detector.DetectSynergies(grid, 2, 2);
            
            Assert.AreEqual(1, synergies.Count);
            Assert.AreEqual("test", synergies[0].Pattern.Id);
        }
        
        [TestMethod]
        public void SynergyDetector_DetectsSingleItemPattern()
        {
            var detector = new SynergyDetector();
            var offsets = new List<Point> { new Point(0, 0) };
            var kinds = new List<ItemKind> { ItemKind.WeaponSword };
            var effects = new List<ISynergyEffect>();
            
            var pattern = new SynergyPattern("sword_synergy", "Sword Synergy", "Test", offsets, kinds, effects, 100);
            detector.RegisterPattern(pattern);
            
            // Create a 3x3 grid with a sword at (1,1)
            var grid = new IItem[3, 3];
            grid[1, 1] = GearItems.ShortSword();
            
            var synergies = detector.DetectSynergies(grid, 3, 3);
            
            Assert.AreEqual(1, synergies.Count);
            Assert.AreEqual(new Point(1, 1), synergies[0].AnchorSlot);
        }
        
        [TestMethod]
        public void SynergyDetector_DetectsTwoItemHorizontalPattern()
        {
            var detector = new SynergyDetector();
            var offsets = new List<Point> { new Point(0, 0), new Point(1, 0) };
            var kinds = new List<ItemKind> { ItemKind.WeaponSword, ItemKind.Shield };
            var effects = new List<ISynergyEffect>();
            
            var pattern = new SynergyPattern("sword_shield", "Sword & Shield", "Test", offsets, kinds, effects, 100);
            detector.RegisterPattern(pattern);
            
            // Create a grid with sword and shield side-by-side
            var grid = new IItem[4, 4];
            grid[1, 1] = GearItems.ShortSword();
            grid[2, 1] = GearItems.WoodenShield();
            
            var synergies = detector.DetectSynergies(grid, 4, 4);
            
            Assert.AreEqual(1, synergies.Count);
            Assert.AreEqual(new Point(1, 1), synergies[0].AnchorSlot);
            Assert.AreEqual(2, synergies[0].AffectedSlots.Count);
        }
        
        [TestMethod]
        public void SynergyDetector_IgnoresPatternOutOfBounds()
        {
            var detector = new SynergyDetector();
            var offsets = new List<Point> { new Point(0, 0), new Point(1, 0) };
            var kinds = new List<ItemKind> { ItemKind.WeaponSword, ItemKind.Shield };
            var effects = new List<ISynergyEffect>();
            
            var pattern = new SynergyPattern("sword_shield", "Sword & Shield", "Test", offsets, kinds, effects, 100);
            detector.RegisterPattern(pattern);
            
            // Create a grid with sword at the edge (no room for shield)
            var grid = new IItem[2, 2];
            grid[1, 0] = GearItems.ShortSword();
            // No shield at (2, 0) - out of bounds
            
            var synergies = detector.DetectSynergies(grid, 2, 2);
            
            Assert.AreEqual(0, synergies.Count);
        }
        
        [TestMethod]
        public void SynergyDetector_RequiresExactItemKindMatch()
        {
            var detector = new SynergyDetector();
            var offsets = new List<Point> { new Point(0, 0), new Point(1, 0) };
            var kinds = new List<ItemKind> { ItemKind.WeaponSword, ItemKind.Shield };
            var effects = new List<ISynergyEffect>();
            
            var pattern = new SynergyPattern("sword_shield", "Sword & Shield", "Test", offsets, kinds, effects, 100);
            detector.RegisterPattern(pattern);
            
            // Create a grid with sword and armor (not shield)
            var grid = new IItem[4, 4];
            grid[1, 1] = GearItems.ShortSword();
            grid[2, 1] = GearItems.LeatherArmor();
            
            var synergies = detector.DetectSynergies(grid, 4, 4);
            
            Assert.AreEqual(0, synergies.Count);
        }
        
        [TestMethod]
        public void HeroCrystal_EarnSynergyPoints_IncreasesPoints()
        {
            var crystal = new HeroCrystal("TestCrystal", new Knight(), 1, new StatBlock(4, 2, 4, 1));
            
            crystal.EarnSynergyPoints("synergy1", 50);
            
            Assert.AreEqual(50, crystal.GetSynergyPoints("synergy1"));
        }
        
        [TestMethod]
        public void HeroCrystal_EarnSynergyPoints_AccumulatesForSameSynergy()
        {
            var crystal = new HeroCrystal("TestCrystal", new Knight(), 1, new StatBlock(4, 2, 4, 1));
            
            crystal.EarnSynergyPoints("synergy1", 30);
            crystal.EarnSynergyPoints("synergy1", 20);
            
            Assert.AreEqual(50, crystal.GetSynergyPoints("synergy1"));
        }
        
        [TestMethod]
        public void HeroCrystal_EarnSynergyPoints_TracksMultipleSynergies()
        {
            var crystal = new HeroCrystal("TestCrystal", new Knight(), 1, new StatBlock(4, 2, 4, 1));
            
            crystal.EarnSynergyPoints("synergy1", 30);
            crystal.EarnSynergyPoints("synergy2", 50);
            
            Assert.AreEqual(30, crystal.GetSynergyPoints("synergy1"));
            Assert.AreEqual(50, crystal.GetSynergyPoints("synergy2"));
        }
        
        [TestMethod]
        public void HeroCrystal_DiscoverSynergy_MarksAsDiscovered()
        {
            var crystal = new HeroCrystal("TestCrystal", new Knight(), 1, new StatBlock(4, 2, 4, 1));
            
            crystal.DiscoverSynergy("synergy1");
            
            Assert.IsTrue(crystal.HasDiscoveredSynergy("synergy1"));
            Assert.IsFalse(crystal.HasDiscoveredSynergy("synergy2"));
        }
        
        [TestMethod]
        public void HeroCrystal_LearnSynergySkill_MarksAsLearned()
        {
            var crystal = new HeroCrystal("TestCrystal", new Knight(), 1, new StatBlock(4, 2, 4, 1));
            
            crystal.LearnSynergySkill("synergy_skill_1");
            
            Assert.IsTrue(crystal.HasSynergySkill("synergy_skill_1"));
            Assert.IsFalse(crystal.HasSynergySkill("synergy_skill_2"));
        }
        
        [TestMethod]
        public void StatBonusEffect_HasCorrectProperties()
        {
            var statBonus = new StatBlock(5, 3, 4, 2);
            var effect = new StatBonusEffect(
                "stat_boost",
                "+5 STR, +3 AGI, +4 VIT, +2 MAG",
                statBonus,
                isPercentage: false,
                hpBonus: 20,
                mpBonus: 10
            );
            
            Assert.AreEqual("stat_boost", effect.EffectId);
            Assert.AreEqual(5, effect.StatBonus.Strength);
            Assert.AreEqual(3, effect.StatBonus.Agility);
            Assert.AreEqual(20, effect.HPBonus);
            Assert.AreEqual(10, effect.MPBonus);
        }
        
        [TestMethod]
        public void SkillModifierEffect_AppliesAndRemovesMPReduction()
        {
            var hero = new Hero("TestHero", new Knight(), 1, new StatBlock(4, 2, 4, 1));
            var effect = new SkillModifierEffect(
                "mp_reduction",
                "-20% MP cost",
                mpCostReductionPercent: 20f
            );
            
            Assert.AreEqual(0f, hero.MPCostReduction);
            
            effect.Apply(hero);
            Assert.AreEqual(0.2f, hero.MPCostReduction, 0.001);
            
            effect.Remove(hero);
            Assert.AreEqual(0f, hero.MPCostReduction, 0.001);
        }
        
        [TestMethod]
        public void PassiveAbilityEffect_AppliesAndRemovesDefenseBonus()
        {
            var hero = new Hero("TestHero", new Knight(), 1, new StatBlock(4, 2, 4, 1));
            var effect = new PassiveAbilityEffect(
                "defense_boost",
                "+10 Defense",
                defenseBonus: 10
            );
            
            Assert.AreEqual(0, hero.PassiveDefenseBonus);
            
            effect.Apply(hero);
            Assert.AreEqual(10, hero.PassiveDefenseBonus);
            
            effect.Remove(hero);
            Assert.AreEqual(0, hero.PassiveDefenseBonus);
        }
        
        [TestMethod]
        public void Hero_UpdateActiveSynergies_AppliesEffects()
        {
            var crystal = new HeroCrystal("TestCrystal", new Knight(), 1, new StatBlock(4, 2, 4, 1));
            var hero = new Hero("TestHero", new Knight(), 1, new StatBlock(4, 2, 4, 1), crystal);
            
            // Create a synergy with a defense bonus effect
            var offsets = new List<Point> { new Point(0, 0) };
            var kinds = new List<ItemKind> { ItemKind.WeaponSword };
            var effects = new List<ISynergyEffect>
            {
                new PassiveAbilityEffect("defense", "+5 Defense", defenseBonus: 5)
            };
            var pattern = new SynergyPattern("test", "Test", "Test", offsets, kinds, effects, 100);
            
            var anchor = new Point(0, 0);
            var slots = new List<Point> { new Point(0, 0) };
            var activeSynergy = new ActiveSynergy(pattern, anchor, slots);
            
            var synergies = new List<ActiveSynergy> { activeSynergy };
            
            hero.UpdateActiveSynergies(synergies);
            
            Assert.AreEqual(1, hero.ActiveSynergies.Count);
            Assert.AreEqual(5, hero.PassiveDefenseBonus);
        }
        
        [TestMethod]
        public void Hero_UpdateActiveSynergies_RemovesOldEffects()
        {
            var crystal = new HeroCrystal("TestCrystal", new Knight(), 1, new StatBlock(4, 2, 4, 1));
            var hero = new Hero("TestHero", new Knight(), 1, new StatBlock(4, 2, 4, 1), crystal);
            
            // First synergy with defense bonus
            var offsets1 = new List<Point> { new Point(0, 0) };
            var kinds1 = new List<ItemKind> { ItemKind.WeaponSword };
            var effects1 = new List<ISynergyEffect>
            {
                new PassiveAbilityEffect("defense1", "+5 Defense", defenseBonus: 5)
            };
            var pattern1 = new SynergyPattern("test1", "Test1", "Test", offsets1, kinds1, effects1, 100);
            var synergy1 = new ActiveSynergy(pattern1, new Point(0, 0), new List<Point> { new Point(0, 0) });
            
            hero.UpdateActiveSynergies(new List<ActiveSynergy> { synergy1 });
            Assert.AreEqual(5, hero.PassiveDefenseBonus);
            
            // Update with empty list - should remove effects
            hero.UpdateActiveSynergies(new List<ActiveSynergy>());
            Assert.AreEqual(0, hero.PassiveDefenseBonus);
        }
    }
}
