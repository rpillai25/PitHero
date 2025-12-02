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
        
        [TestMethod]
        public void Hero_SynergyStatBonuses_AreAppliedToTotalStats()
        {
            var crystal = new HeroCrystal("TestCrystal", new Knight(), 1, new StatBlock(10, 5, 10, 5));
            var hero = new Hero("TestHero", new Knight(), 1, new StatBlock(10, 5, 10, 5), crystal);
            
            var baseStats = hero.GetTotalStats();
            var baseSTR = baseStats.Strength;
            
            // Create a synergy with stat bonus
            var offsets = new List<Point> { new Point(0, 0) };
            var kinds = new List<ItemKind> { ItemKind.WeaponSword };
            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect("str_boost", "+5 STR", new StatBlock(5, 0, 0, 0))
            };
            var pattern = new SynergyPattern("test", "Test", "Test", offsets, kinds, effects, 100);
            var synergy = new ActiveSynergy(pattern, new Point(0, 0), new List<Point> { new Point(0, 0) });
            
            hero.UpdateActiveSynergies(new List<ActiveSynergy> { synergy });
            
            var statsWithSynergy = hero.GetTotalStats();
            Assert.AreEqual(baseSTR + 5, statsWithSynergy.Strength, "Synergy stat bonus should be applied");
        }
        
        [TestMethod]
        public void Hero_SynergyHPBonus_IsAppliedToMaxHP()
        {
            var crystal = new HeroCrystal("TestCrystal", new Knight(), 1, new StatBlock(10, 5, 10, 5));
            var hero = new Hero("TestHero", new Knight(), 1, new StatBlock(10, 5, 10, 5), crystal);
            
            var baseMaxHP = hero.MaxHP;
            
            // Create a synergy with HP bonus
            var offsets = new List<Point> { new Point(0, 0) };
            var kinds = new List<ItemKind> { ItemKind.WeaponSword };
            var effects = new List<ISynergyEffect>
            {
                new StatBonusEffect("hp_boost", "+50 HP", new StatBlock(0, 0, 0, 0), hpBonus: 50)
            };
            var pattern = new SynergyPattern("test", "Test", "Test", offsets, kinds, effects, 100);
            var synergy = new ActiveSynergy(pattern, new Point(0, 0), new List<Point> { new Point(0, 0) });
            
            hero.UpdateActiveSynergies(new List<ActiveSynergy> { synergy });
            
            Assert.AreEqual(baseMaxHP + 50, hero.MaxHP, "Synergy HP bonus should be applied");
        }
        
        [TestMethod]
        public void PassiveAbilityEffect_CounterUseReferenceCount()
        {
            var crystal = new HeroCrystal("TestCrystal", new Knight(), 1, new StatBlock(4, 2, 4, 1));
            var hero = new Hero("TestHero", new Knight(), 1, new StatBlock(4, 2, 4, 1), crystal);
            
            Assert.IsFalse(hero.EnableCounter);
            
            // First counter-enabling synergy
            var offsets1 = new List<Point> { new Point(0, 0) };
            var kinds1 = new List<ItemKind> { ItemKind.WeaponSword };
            var effects1 = new List<ISynergyEffect>
            {
                new PassiveAbilityEffect("counter1", "Counter", enableCounter: true)
            };
            var pattern1 = new SynergyPattern("test1", "Test1", "Test", offsets1, kinds1, effects1, 100);
            var synergy1 = new ActiveSynergy(pattern1, new Point(0, 0), new List<Point> { new Point(0, 0) });
            
            // Second counter-enabling synergy
            var offsets2 = new List<Point> { new Point(1, 0) };
            var kinds2 = new List<ItemKind> { ItemKind.Shield };
            var effects2 = new List<ISynergyEffect>
            {
                new PassiveAbilityEffect("counter2", "Counter", enableCounter: true)
            };
            var pattern2 = new SynergyPattern("test2", "Test2", "Test", offsets2, kinds2, effects2, 100);
            var synergy2 = new ActiveSynergy(pattern2, new Point(1, 0), new List<Point> { new Point(1, 0) });
            
            // Apply both synergies
            hero.UpdateActiveSynergies(new List<ActiveSynergy> { synergy1, synergy2 });
            Assert.IsTrue(hero.EnableCounter, "Counter should be enabled");
            Assert.AreEqual(2, hero.SynergyCounterEnablers, "Should have 2 counter enablers");
            
            // Remove one synergy - counter should still be enabled
            hero.UpdateActiveSynergies(new List<ActiveSynergy> { synergy1 });
            Assert.IsTrue(hero.EnableCounter, "Counter should still be enabled with one synergy");
            Assert.AreEqual(1, hero.SynergyCounterEnablers, "Should have 1 counter enabler");
            
            // Remove all synergies - counter should be disabled
            hero.UpdateActiveSynergies(new List<ActiveSynergy>());
            Assert.IsFalse(hero.EnableCounter, "Counter should be disabled");
            Assert.AreEqual(0, hero.SynergyCounterEnablers, "Should have 0 counter enablers");
        }
        
        #region Monster JP/SP Yield Tests
        
        [TestMethod]
        public void Monster_HasJPYield_CalculatedFromLevel()
        {
            var slime = new RolePlayingFramework.Enemies.Slime();
            Assert.IsTrue(slime.JPYield > 0, "Slime should have positive JP yield");
            
            // Formula is: 5 + level * 2
            int expectedJP = 5 + slime.Level * 2;
            Assert.AreEqual(expectedJP, slime.JPYield, "JP yield should match formula");
        }
        
        [TestMethod]
        public void Monster_HasSPYield_CalculatedFromLevel()
        {
            var slime = new RolePlayingFramework.Enemies.Slime();
            Assert.IsTrue(slime.SPYield > 0, "Slime should have positive SP yield");
            
            // Formula is: 3 + level
            int expectedSP = 3 + slime.Level;
            Assert.AreEqual(expectedSP, slime.SPYield, "SP yield should match formula");
        }
        
        [TestMethod]
        public void AllMonsters_HaveJPAndSPYields()
        {
            // Test all monster types have JP and SP yields
            var slime = new RolePlayingFramework.Enemies.Slime();
            var goblin = new RolePlayingFramework.Enemies.Goblin();
            var bat = new RolePlayingFramework.Enemies.Bat();
            var snake = new RolePlayingFramework.Enemies.Snake();
            var spider = new RolePlayingFramework.Enemies.Spider();
            var rat = new RolePlayingFramework.Enemies.Rat();
            var orc = new RolePlayingFramework.Enemies.Orc();
            var skeleton = new RolePlayingFramework.Enemies.Skeleton();
            var wraith = new RolePlayingFramework.Enemies.Wraith();
            var pitLord = new RolePlayingFramework.Enemies.PitLord();
            
            Assert.IsTrue(slime.JPYield > 0, "Slime JP");
            Assert.IsTrue(slime.SPYield > 0, "Slime SP");
            Assert.IsTrue(goblin.JPYield > 0, "Goblin JP");
            Assert.IsTrue(goblin.SPYield > 0, "Goblin SP");
            Assert.IsTrue(bat.JPYield > 0, "Bat JP");
            Assert.IsTrue(bat.SPYield > 0, "Bat SP");
            Assert.IsTrue(snake.JPYield > 0, "Snake JP");
            Assert.IsTrue(snake.SPYield > 0, "Snake SP");
            Assert.IsTrue(spider.JPYield > 0, "Spider JP");
            Assert.IsTrue(spider.SPYield > 0, "Spider SP");
            Assert.IsTrue(rat.JPYield > 0, "Rat JP");
            Assert.IsTrue(rat.SPYield > 0, "Rat SP");
            Assert.IsTrue(orc.JPYield > 0, "Orc JP");
            Assert.IsTrue(orc.SPYield > 0, "Orc SP");
            Assert.IsTrue(skeleton.JPYield > 0, "Skeleton JP");
            Assert.IsTrue(skeleton.SPYield > 0, "Skeleton SP");
            Assert.IsTrue(wraith.JPYield > 0, "Wraith JP");
            Assert.IsTrue(wraith.SPYield > 0, "Wraith SP");
            Assert.IsTrue(pitLord.JPYield > 0, "PitLord JP");
            Assert.IsTrue(pitLord.SPYield > 0, "PitLord SP");
        }
        
        #endregion
        
        #region Synergy Pattern Registry Tests
        
        [TestMethod]
        public void SynergyDetector_RegisterPattern_AddsToStaticRegistry()
        {
            var detector = new SynergyDetector();
            var offsets = new List<Point> { new Point(0, 0), new Point(1, 0) };
            var kinds = new List<ItemKind> { ItemKind.WeaponSword, ItemKind.Shield };
            var effects = new List<ISynergyEffect>();
            
            var pattern = new SynergyPattern(
                "test_registry_pattern",
                "Test Registry Pattern",
                "A test pattern for registry",
                offsets,
                kinds,
                effects,
                100
            );
            
            detector.RegisterPattern(pattern);
            
            var retrieved = SynergyDetector.GetPatternById("test_registry_pattern");
            Assert.IsNotNull(retrieved, "Pattern should be retrievable from registry");
            Assert.AreEqual("Test Registry Pattern", retrieved.Name);
        }
        
        [TestMethod]
        public void SynergyDetector_GetPatternById_ReturnsNullForUnknownPattern()
        {
            var retrieved = SynergyDetector.GetPatternById("nonexistent_pattern_12345");
            Assert.IsNull(retrieved, "Should return null for unknown pattern ID");
        }
        
        #endregion
        
        #region HeroCrystal Discovered Synergy Tests
        
        [TestMethod]
        public void HeroCrystal_DiscoverSynergy_TracksSynergyIds()
        {
            var knight = new Knight();
            var stats = new StatBlock(5, 5, 5, 5);
            var crystal = new HeroCrystal("Test Crystal", knight, 1, stats);
            
            crystal.DiscoverSynergy("knight.shield_mastery");
            crystal.DiscoverSynergy("knight.holy_strike");
            
            Assert.IsTrue(crystal.HasDiscoveredSynergy("knight.shield_mastery"), "Should have discovered shield mastery");
            Assert.IsTrue(crystal.HasDiscoveredSynergy("knight.holy_strike"), "Should have discovered holy strike");
            Assert.IsFalse(crystal.HasDiscoveredSynergy("knight.spellblade"), "Should not have discovered spellblade");
        }
        
        [TestMethod]
        public void HeroCrystal_GetSynergyPoints_ReturnsZeroForNewSynergy()
        {
            var knight = new Knight();
            var stats = new StatBlock(5, 5, 5, 5);
            var crystal = new HeroCrystal("Test Crystal", knight, 1, stats);
            
            Assert.AreEqual(0, crystal.GetSynergyPoints("knight.shield_mastery"), "New synergy should have 0 points");
        }
        
        [TestMethod]
        public void HeroCrystal_EarnSynergyPoints_AccumulatesPoints()
        {
            var knight = new Knight();
            var stats = new StatBlock(5, 5, 5, 5);
            var crystal = new HeroCrystal("Test Crystal", knight, 1, stats);
            
            crystal.EarnSynergyPoints("knight.shield_mastery", 50);
            Assert.AreEqual(50, crystal.GetSynergyPoints("knight.shield_mastery"));
            
            crystal.EarnSynergyPoints("knight.shield_mastery", 30);
            Assert.AreEqual(80, crystal.GetSynergyPoints("knight.shield_mastery"));
        }
        
        [TestMethod]
        public void HeroCrystal_LearnSynergySkill_TracksLearnedSkills()
        {
            var knight = new Knight();
            var stats = new StatBlock(5, 5, 5, 5);
            var crystal = new HeroCrystal("Test Crystal", knight, 1, stats);
            
            crystal.LearnSynergySkill("holy_strike");
            
            Assert.IsTrue(crystal.HasSynergySkill("holy_strike"), "Should have learned holy strike");
            Assert.IsFalse(crystal.HasSynergySkill("shadow_slash"), "Should not have learned shadow slash");
        }
        
        #endregion
    }
}
