using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.Balance;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Equipment.Potions;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Stats;
using System;
using System.Collections.Generic;
using System.Text;

namespace PitHero.Tests
{
    /// <summary>
    /// Comprehensive balance system tests validating RPG progression, challenge, and gear impact.
    /// Tests hero progression through Pit levels 1-10 with and without equipment,
    /// validates combat balance, and provides dynamic feedback for rebalancing.
    /// </summary>
    [TestClass]
    public class BalanceSystemTests
    {
        private readonly EnhancedAttackResolver _attackResolver = new EnhancedAttackResolver();
        
        #region Test Infrastructure and Helpers

        /// <summary>
        /// Simulates a complete battle between a hero and a monster.
        /// </summary>
        /// <returns>Battle result with winner, turn count, and combat log</returns>
        private BattleResult SimulateBattle(Hero hero, IEnemy monster, bool usePotion = false, int potionHealAmount = 100)
        {
            var log = new StringBuilder();
            int turn = 0;
            const int maxTurns = 100; // Prevent infinite loops
            
            log.AppendLine($"=== BATTLE START ===");
            log.AppendLine($"{hero.Name} (Lv{hero.Level}, HP:{hero.CurrentHP}/{hero.MaxHP}) vs {monster.Name} (Lv{monster.Level}, HP:{monster.CurrentHP}/{monster.MaxHP})");
            log.AppendLine($"Hero Stats - Attack:{GetHeroAttack(hero)}, Defense:{GetHeroDefense(hero)}, Evasion:{GetHeroEvasion(hero)}");
            log.AppendLine($"Monster Stats - Attack:{GetMonsterAttack(monster)}, Defense:{GetMonsterDefense(monster)}, Evasion:{GetMonsterEvasion(monster)}");
            log.AppendLine();
            
            while (hero.CurrentHP > 0 && monster.CurrentHP > 0 && turn < maxTurns)
            {
                turn++;
                
                // Hero attacks monster
                var heroStats = BattleStats.CalculateForHero(hero);
                var monsterStats = BattleStats.CalculateForMonster(monster);
                var heroAttackResult = _attackResolver.Resolve(heroStats, monsterStats, DamageKind.Physical);
                
                if (heroAttackResult.Hit)
                {
                    monster.TakeDamage(heroAttackResult.Damage);
                    log.AppendLine($"Turn {turn}: Hero attacks for {heroAttackResult.Damage} damage. Monster HP: {monster.CurrentHP}/{monster.MaxHP}");
                }
                else
                {
                    log.AppendLine($"Turn {turn}: Hero attacks but monster evades!");
                }
                
                if (monster.CurrentHP == 0)
                {
                    log.AppendLine($"{monster.Name} defeated!");
                    break;
                }
                
                // Monster attacks hero
                var monsterAttackResult = _attackResolver.Resolve(monsterStats, heroStats, monster.AttackKind);
                
                if (monsterAttackResult.Hit)
                {
                    hero.TakeDamage(monsterAttackResult.Damage);
                    log.AppendLine($"Turn {turn}: Monster attacks for {monsterAttackResult.Damage} damage. Hero HP: {hero.CurrentHP}/{hero.MaxHP}");
                }
                else
                {
                    log.AppendLine($"Turn {turn}: Monster attacks but hero evades!");
                }
                
                // Use potion if hero HP drops below 50% and potions are enabled
                if (usePotion && hero.CurrentHP > 0 && hero.CurrentHP < hero.MaxHP / 2)
                {
                    int healedAmount = Math.Min(potionHealAmount, hero.MaxHP - hero.CurrentHP);
                    hero.RestoreHP(potionHealAmount);
                    log.AppendLine($"Turn {turn}: Hero uses potion! Restored {healedAmount} HP. Hero HP: {hero.CurrentHP}/{hero.MaxHP}");
                }
                
                if (hero.CurrentHP == 0)
                {
                    log.AppendLine($"{hero.Name} defeated!");
                    break;
                }
            }
            
            log.AppendLine($"=== BATTLE END (Turns: {turn}) ===");
            log.AppendLine();
            
            return new BattleResult
            {
                HeroWon = hero.CurrentHP > 0,
                MonsterWon = monster.CurrentHP > 0,
                TurnCount = turn,
                Log = log.ToString()
            };
        }

        /// <summary>Creates a monster appropriate for the given pit level</summary>
        private IEnemy CreateMonsterForPitLevel(int pitLevel)
        {
            // Use balanced archetype for consistent testing
            int monsterLevel = BalanceConfig.EstimatePlayerLevelForPitLevel(pitLevel);
            return new TestMonster(monsterLevel, BalanceConfig.MonsterArchetype.Balanced);
        }

        /// <summary>Creates scaled equipment for a given pit level</summary>
        private (Gear weapon, Gear armor) CreateEquipmentForPitLevel(int pitLevel, ItemRarity rarity = ItemRarity.Normal)
        {
            int attackBonus = BalanceConfig.CalculateEquipmentAttackBonus(pitLevel, rarity);
            int defenseBonus = BalanceConfig.CalculateEquipmentDefenseBonus(pitLevel, rarity);
            int statBonus = BalanceConfig.CalculateEquipmentStatBonus(pitLevel, rarity);
            
            var weapon = new Gear(
                $"Sword+{pitLevel}",
                ItemKind.WeaponSword,
                rarity,
                $"+{attackBonus} Attack",
                100,
                new StatBlock(statBonus, 0, 0, 0),
                atk: attackBonus,
                elementalProps: new ElementalProperties(ElementType.Neutral)
            );
            
            var armor = new Gear(
                $"Armor+{pitLevel}",
                ItemKind.ArmorMail,
                rarity,
                $"+{defenseBonus} Defense",
                120,
                new StatBlock(0, 0, statBonus, 0),
                def: defenseBonus,
                elementalProps: new ElementalProperties(ElementType.Neutral)
            );
            
            return (weapon, armor);
        }

        private int GetHeroAttack(Hero hero) => hero.GetTotalStats().Strength + hero.GetEquipmentAttackBonus();
        private int GetHeroDefense(Hero hero) => hero.GetTotalStats().Agility / 2 + hero.GetEquipmentDefenseBonus();
        private int GetHeroEvasion(Hero hero) => BalanceConfig.CalculateEvasion(hero.GetTotalStats().Agility, hero.Level);
        
        private int GetMonsterAttack(IEnemy monster) => monster.Stats.Strength;
        private int GetMonsterDefense(IEnemy monster) => monster.Stats.Agility / 2;
        private int GetMonsterEvasion(IEnemy monster) => BalanceConfig.CalculateEvasion(monster.Stats.Agility, monster.Level);

        #endregion

        #region 1. Hero Progression - Unequipped (Knight)

        [TestMethod]
        [TestCategory("Balance")]
        [TestCategory("Progression")]
        public void HeroProgression_UnequippedKnight_Pit1to10_FacesSubstantialChallenge()
        {
            var testLog = new StringBuilder();
            testLog.AppendLine("===========================================");
            testLog.AppendLine("HERO PROGRESSION - UNEQUIPPED KNIGHT");
            testLog.AppendLine("Testing Pit Levels 1-10");
            testLog.AppendLine("===========================================");
            testLog.AppendLine();
            
            var results = new List<PitLevelResult>();
            
            for (int pitLevel = 1; pitLevel <= 10; pitLevel++)
            {
                testLog.AppendLine($"--- PIT LEVEL {pitLevel} ---");
                
                // Create fresh unequipped Knight for each pit level
                var knight = new Knight();
                int heroLevel = BalanceConfig.EstimatePlayerLevelForPitLevel(pitLevel);
                var baseStats = new StatBlock(strength: 12, agility: 10, vitality: 8, magic: 2);
                var hero = new Hero(name: "Test Knight", job: knight, level: heroLevel, baseStats: baseStats);
                
                // Fight 3 battles to get statistical data
                int wins = 0;
                int totalTurns = 0;
                var battleLogs = new List<string>();
                
                for (int battle = 1; battle <= 3; battle++)
                {
                    var monster = CreateMonsterForPitLevel(pitLevel);
                    var result = SimulateBattle(hero, monster, usePotion: false);
                    
                    battleLogs.Add(result.Log);
                    totalTurns += result.TurnCount;
                    if (result.HeroWon) wins++;
                    
                    // Restore hero to full for next battle
                    hero.RestoreHP(hero.MaxHP);
                }
                
                double winRate = wins / 3.0;
                double avgTurns = totalTurns / 3.0;
                
                testLog.AppendLine($"Pit {pitLevel} Results: {wins}/3 wins ({winRate:P0}), Avg Turns: {avgTurns:F1}");
                testLog.AppendLine(battleLogs[0]); // Include first battle log for detail
                
                results.Add(new PitLevelResult
                {
                    PitLevel = pitLevel,
                    WinRate = winRate,
                    AverageTurns = avgTurns
                });
            }
            
            // Analysis and Assertions
            testLog.AppendLine();
            testLog.AppendLine("=== ANALYSIS ===");
            
            // Assert: Early levels (1-3) should have some challenge (not 100% win rate)
            var earlyLevels = results.FindAll(r => r.PitLevel <= 3);
            foreach (var result in earlyLevels)
            {
                testLog.AppendLine($"Pit {result.PitLevel}: Win Rate {result.WinRate:P0}, Avg Turns {result.AverageTurns:F1}");
                Assert.IsTrue(result.WinRate < 1.0 || result.AverageTurns > 5, 
                    $"Pit {result.PitLevel} should provide challenge for unequipped hero");
            }
            
            // Assert: Mid levels (4-6) should be difficult (< 70% win rate)
            var midLevels = results.FindAll(r => r.PitLevel >= 4 && r.PitLevel <= 6);
            foreach (var result in midLevels)
            {
                testLog.AppendLine($"Pit {result.PitLevel}: Win Rate {result.WinRate:P0}, Avg Turns {result.AverageTurns:F1}");
                Assert.IsTrue(result.WinRate < 0.7, 
                    $"Pit {result.PitLevel} should be difficult for unequipped hero (win rate < 70%)");
            }
            
            // Assert: Late levels (7-10) should be very difficult or impossible (< 40% win rate)
            var lateLevels = results.FindAll(r => r.PitLevel >= 7);
            int lateWins = 0;
            foreach (var result in lateLevels)
            {
                testLog.AppendLine($"Pit {result.PitLevel}: Win Rate {result.WinRate:P0}, Avg Turns {result.AverageTurns:F1}");
                if (result.WinRate > 0.3) lateWins++;
            }
            
            // At most 1 of the late pits (7-10) should have > 30% win rate
            Assert.IsTrue(lateWins <= 1, 
                $"Pits 7-10 should be nearly impossible for unequipped hero. {lateWins} pits had > 30% win rate.");
            
            // Assert: Combat should last multiple turns (back-and-forth, not one-sided)
            var allResults = results.FindAll(r => r.WinRate > 0);
            foreach (var result in allResults)
            {
                Assert.IsTrue(result.AverageTurns >= 3, 
                    $"Pit {result.PitLevel} battles should last at least 3 turns on average (got {result.AverageTurns:F1})");
            }
            
            // Dynamic feedback: Flag if any pit is too easy
            testLog.AppendLine();
            testLog.AppendLine("=== DYNAMIC FEEDBACK ===");
            bool needsRebalancing = false;
            foreach (var result in results)
            {
                if (result.WinRate > 0.8)
                {
                    needsRebalancing = true;
                    testLog.AppendLine($"⚠ WARNING: Pit {result.PitLevel} is too easy ({result.WinRate:P0} win rate)");
                    testLog.AppendLine($"  SUGGESTION: Increase monster stats for pit level {result.PitLevel}");
                    testLog.AppendLine($"  - Increase HP by 20-30%");
                    testLog.AppendLine($"  - Increase Attack (Strength) by 15-25%");
                }
            }
            
            if (!needsRebalancing)
            {
                testLog.AppendLine("✓ Balance is appropriate - unequipped hero faces substantial challenge at all levels");
            }
            
            Console.WriteLine(testLog.ToString());
        }

        #endregion

        #region 2. Hero Progression - Equipped (Knight)

        [TestMethod]
        [TestCategory("Balance")]
        [TestCategory("Progression")]
        public void HeroProgression_EquippedKnight_Pit1to10_CanOvercomeWithGearAndPotions()
        {
            var testLog = new StringBuilder();
            testLog.AppendLine("===========================================");
            testLog.AppendLine("HERO PROGRESSION - EQUIPPED KNIGHT");
            testLog.AppendLine("Testing Pit Levels 1-10 with Scaled Gear");
            testLog.AppendLine("===========================================");
            testLog.AppendLine();
            
            var results = new List<PitLevelResult>();
            
            for (int pitLevel = 1; pitLevel <= 10; pitLevel++)
            {
                testLog.AppendLine($"--- PIT LEVEL {pitLevel} ---");
                
                // Create Knight with equipment scaled to pit level
                var knight = new Knight();
                int heroLevel = BalanceConfig.EstimatePlayerLevelForPitLevel(pitLevel);
                var baseStats = new StatBlock(strength: 12, agility: 10, vitality: 8, magic: 2);
                var hero = new Hero(name: "Equipped Knight", job: knight, level: heroLevel, baseStats: baseStats);
                
                // Equip gear appropriate for pit level
                var (weapon, armor) = CreateEquipmentForPitLevel(pitLevel, ItemRarity.Normal);
                hero.TryEquip(weapon);
                hero.TryEquip(armor);
                
                testLog.AppendLine($"Hero Level: {heroLevel}, Equipment: {weapon.Name} (+{weapon.AttackBonus} Atk), {armor.Name} (+{armor.DefenseBonus} Def)");
                
                // Fight 3 battles with potion support
                int wins = 0;
                int totalTurns = 0;
                var battleLogs = new List<string>();
                
                for (int battle = 1; battle <= 3; battle++)
                {
                    var monster = CreateMonsterForPitLevel(pitLevel);
                    var result = SimulateBattle(hero, monster, usePotion: true, potionHealAmount: 100);
                    
                    battleLogs.Add(result.Log);
                    totalTurns += result.TurnCount;
                    if (result.HeroWon) wins++;
                    
                    // Restore hero to full for next battle
                    hero.RestoreHP(hero.MaxHP);
                }
                
                double winRate = wins / 3.0;
                double avgTurns = totalTurns / 3.0;
                
                testLog.AppendLine($"Pit {pitLevel} Results: {wins}/3 wins ({winRate:P0}), Avg Turns: {avgTurns:F1}");
                testLog.AppendLine(battleLogs[0]); // Include first battle log
                
                results.Add(new PitLevelResult
                {
                    PitLevel = pitLevel,
                    WinRate = winRate,
                    AverageTurns = avgTurns
                });
            }
            
            // Analysis and Assertions
            testLog.AppendLine();
            testLog.AppendLine("=== ANALYSIS ===");
            
            // Assert: With appropriate gear and potions, hero should win most battles (> 60%)
            foreach (var result in results)
            {
                testLog.AppendLine($"Pit {result.PitLevel}: Win Rate {result.WinRate:P0}, Avg Turns {result.AverageTurns:F1}");
                Assert.IsTrue(result.WinRate >= 0.6, 
                    $"Pit {result.PitLevel}: Equipped hero with potions should win at least 60% of battles (got {result.WinRate:P0})");
            }
            
            // Assert: Early levels (1-5) should have high success rate (> 80%)
            var earlyLevels = results.FindAll(r => r.PitLevel <= 5);
            foreach (var result in earlyLevels)
            {
                Assert.IsTrue(result.WinRate >= 0.8, 
                    $"Pit {result.PitLevel}: Equipped hero should dominate early levels (win rate >= 80%, got {result.WinRate:P0})");
            }
            
            // Assert: Combat should still be meaningful (not trivial one-shots)
            foreach (var result in results)
            {
                Assert.IsTrue(result.AverageTurns >= 2, 
                    $"Pit {result.PitLevel}: Even equipped battles should last multiple turns (got {result.AverageTurns:F1})");
            }
            
            testLog.AppendLine();
            testLog.AppendLine("✓ Equipped hero with potions can overcome challenges at appropriate pit levels");
            
            Console.WriteLine(testLog.ToString());
        }

        [TestMethod]
        [TestCategory("Balance")]
        [TestCategory("Progression")]
        public void HeroProgression_UndergearKnight_Pit10_FailsWithInadequateGear()
        {
            var testLog = new StringBuilder();
            testLog.AppendLine("===========================================");
            testLog.AppendLine("UNDERGEARED TEST - Pit 10 with Pit 1 Gear");
            testLog.AppendLine("===========================================");
            testLog.AppendLine();
            
            // Create Knight for Pit 10 but with Pit 1 gear
            var knight = new Knight();
            int heroLevel = BalanceConfig.EstimatePlayerLevelForPitLevel(10);
            var baseStats = new StatBlock(strength: 12, agility: 10, vitality: 8, magic: 2);
            var hero = new Hero(name: "Undergeared Knight", job: knight, level: heroLevel, baseStats: baseStats);
            
            // Equip low-level gear (Pit 1)
            var (weapon, armor) = CreateEquipmentForPitLevel(1, ItemRarity.Normal);
            hero.TryEquip(weapon);
            hero.TryEquip(armor);
            
            testLog.AppendLine($"Hero Level: {heroLevel}, Equipment: {weapon.Name} (+{weapon.AttackBonus} Atk), {armor.Name} (+{armor.DefenseBonus} Def)");
            testLog.AppendLine("Fighting Pit 10 monsters with Pit 1 gear...");
            testLog.AppendLine();
            
            // Fight 5 battles
            int wins = 0;
            for (int battle = 1; battle <= 5; battle++)
            {
                var monster = CreateMonsterForPitLevel(10);
                var result = SimulateBattle(hero, monster, usePotion: true, potionHealAmount: 100);
                
                if (battle == 1) testLog.AppendLine(result.Log);
                if (result.HeroWon) wins++;
                
                hero.RestoreHP(hero.MaxHP);
            }
            
            double winRate = wins / 5.0;
            testLog.AppendLine($"Results: {wins}/5 wins ({winRate:P0})");
            
            // Assert: Undergeared hero should struggle (< 50% win rate)
            Assert.IsTrue(winRate < 0.5, 
                $"Undergeared hero should struggle at Pit 10 with Pit 1 gear (expected < 50% win rate, got {winRate:P0})");
            
            testLog.AppendLine();
            testLog.AppendLine("✓ Inadequate gear results in failure at higher pit levels");
            
            Console.WriteLine(testLog.ToString());
        }

        #endregion

        #region 3. Battle Sequence Validation

        [TestMethod]
        [TestCategory("Balance")]
        [TestCategory("Combat")]
        public void BattleSequence_ShowsBackAndForthCombat()
        {
            var testLog = new StringBuilder();
            testLog.AppendLine("===========================================");
            testLog.AppendLine("BATTLE SEQUENCE VALIDATION");
            testLog.AppendLine("===========================================");
            testLog.AppendLine();
            
            // Test at multiple pit levels
            foreach (int pitLevel in new[] { 1, 5, 10 })
            {
                testLog.AppendLine($"--- Pit Level {pitLevel} ---");
                
                var knight = new Knight();
                int heroLevel = BalanceConfig.EstimatePlayerLevelForPitLevel(pitLevel);
                var baseStats = new StatBlock(strength: 12, agility: 10, vitality: 8, magic: 2);
                var hero = new Hero(name: "Test Knight", job: knight, level: heroLevel, baseStats: baseStats);
                
                // Moderate gear for balanced combat
                var (weapon, armor) = CreateEquipmentForPitLevel(pitLevel, ItemRarity.Normal);
                hero.TryEquip(weapon);
                hero.TryEquip(armor);
                
                var monster = CreateMonsterForPitLevel(pitLevel);
                var result = SimulateBattle(hero, monster, usePotion: false);
                
                testLog.AppendLine(result.Log);
                
                // Assert: Battle should last reasonable number of turns
                Assert.IsTrue(result.TurnCount >= 2 && result.TurnCount <= 50, 
                    $"Pit {pitLevel} battle should last 2-50 turns (got {result.TurnCount})");
            }
            
            testLog.AppendLine("✓ Battles show natural back-and-forth combat progression");
            
            Console.WriteLine(testLog.ToString());
        }

        [TestMethod]
        [TestCategory("Balance")]
        [TestCategory("Combat")]
        public void BattleSequence_EquippedVsUnequipped_ShowsClearDifference()
        {
            var testLog = new StringBuilder();
            testLog.AppendLine("===========================================");
            testLog.AppendLine("EQUIPPED VS UNEQUIPPED COMPARISON");
            testLog.AppendLine("===========================================");
            testLog.AppendLine();
            
            int pitLevel = 5;
            var knight = new Knight();
            int heroLevel = BalanceConfig.EstimatePlayerLevelForPitLevel(pitLevel);
            var baseStats = new StatBlock(strength: 12, agility: 10, vitality: 8, magic: 2);
            
            // Test 1: Unequipped
            testLog.AppendLine("--- UNEQUIPPED TEST ---");
            var unequippedHero = new Hero(name: "Unequipped Knight", job: knight, level: heroLevel, baseStats: baseStats);
            int unequippedWins = 0;
            double unequippedAvgTurns = 0;
            
            for (int i = 0; i < 5; i++)
            {
                var monster = CreateMonsterForPitLevel(pitLevel);
                var result = SimulateBattle(unequippedHero, monster, usePotion: false);
                if (result.HeroWon) unequippedWins++;
                unequippedAvgTurns += result.TurnCount;
                unequippedHero.RestoreHP(unequippedHero.MaxHP);
                
                if (i == 0) testLog.AppendLine(result.Log);
            }
            unequippedAvgTurns /= 5;
            
            testLog.AppendLine($"Unequipped Results: {unequippedWins}/5 wins ({unequippedWins / 5.0:P0}), Avg Turns: {unequippedAvgTurns:F1}");
            testLog.AppendLine();
            
            // Test 2: Equipped
            testLog.AppendLine("--- EQUIPPED TEST ---");
            var equippedHero = new Hero(name: "Equipped Knight", job: knight, level: heroLevel, baseStats: baseStats);
            var (weapon, armor) = CreateEquipmentForPitLevel(pitLevel, ItemRarity.Normal);
            equippedHero.TryEquip(weapon);
            equippedHero.TryEquip(armor);
            
            int equippedWins = 0;
            double equippedAvgTurns = 0;
            
            for (int i = 0; i < 5; i++)
            {
                var monster = CreateMonsterForPitLevel(pitLevel);
                var result = SimulateBattle(equippedHero, monster, usePotion: false);
                if (result.HeroWon) equippedWins++;
                equippedAvgTurns += result.TurnCount;
                equippedHero.RestoreHP(equippedHero.MaxHP);
                
                if (i == 0) testLog.AppendLine(result.Log);
            }
            equippedAvgTurns /= 5;
            
            testLog.AppendLine($"Equipped Results: {equippedWins}/5 wins ({equippedWins / 5.0:P0}), Avg Turns: {equippedAvgTurns:F1}");
            testLog.AppendLine();
            
            // Assert: Equipped should perform significantly better
            Assert.IsTrue(equippedWins > unequippedWins, 
                "Equipped hero should win more battles than unequipped");
            
            testLog.AppendLine($"=== COMPARISON ===");
            testLog.AppendLine($"Win Rate Improvement: {unequippedWins}/5 → {equippedWins}/5 ({(equippedWins - unequippedWins) * 20}% increase)");
            testLog.AppendLine($"✓ Equipment provides clear survival advantage");
            
            Console.WriteLine(testLog.ToString());
        }

        #endregion

        #region 4. Dynamic Challenge Feedback

        [TestMethod]
        [TestCategory("Balance")]
        [TestCategory("Feedback")]
        public void DynamicFeedback_IdentifiesImbalancedPitLevels()
        {
            var testLog = new StringBuilder();
            testLog.AppendLine("===========================================");
            testLog.AppendLine("DYNAMIC CHALLENGE FEEDBACK SYSTEM");
            testLog.AppendLine("===========================================");
            testLog.AppendLine();
            
            var imbalancedPits = new List<ImbalanceReport>();
            
            for (int pitLevel = 1; pitLevel <= 10; pitLevel++)
            {
                var knight = new Knight();
                int heroLevel = BalanceConfig.EstimatePlayerLevelForPitLevel(pitLevel);
                var baseStats = new StatBlock(strength: 12, agility: 10, vitality: 8, magic: 2);
                var hero = new Hero(name: "Test Knight", job: knight, level: heroLevel, baseStats: baseStats);
                
                // Test unequipped performance
                int wins = 0;
                double avgTurns = 0;
                
                for (int battle = 0; battle < 3; battle++)
                {
                    var monster = CreateMonsterForPitLevel(pitLevel);
                    var result = SimulateBattle(hero, monster, usePotion: false);
                    if (result.HeroWon) wins++;
                    avgTurns += result.TurnCount;
                    hero.RestoreHP(hero.MaxHP);
                }
                
                avgTurns /= 3;
                double winRate = wins / 3.0;
                
                // Flag if too easy (> 70% win rate for unequipped)
                if (winRate > 0.7)
                {
                    var monster = CreateMonsterForPitLevel(pitLevel);
                    var heroAttack = GetHeroAttack(hero);
                    var heroDefense = GetHeroDefense(hero);
                    var monsterAttack = GetMonsterAttack(monster);
                    var monsterDefense = GetMonsterDefense(monster);
                    
                    imbalancedPits.Add(new ImbalanceReport
                    {
                        PitLevel = pitLevel,
                        WinRate = winRate,
                        AverageTurns = avgTurns,
                        CurrentMonsterHP = monster.MaxHP,
                        CurrentMonsterAttack = monsterAttack,
                        CurrentMonsterDefense = monsterDefense,
                        HeroAttack = heroAttack,
                        HeroDefense = heroDefense
                    });
                }
            }
            
            testLog.AppendLine("=== IMBALANCE REPORT ===");
            if (imbalancedPits.Count == 0)
            {
                testLog.AppendLine("✓ No imbalanced pit levels detected");
                testLog.AppendLine("All pit levels provide appropriate challenge for unequipped heroes");
            }
            else
            {
                testLog.AppendLine($"⚠ Found {imbalancedPits.Count} potentially imbalanced pit level(s):");
                testLog.AppendLine();
                
                foreach (var report in imbalancedPits)
                {
                    testLog.AppendLine($"PIT LEVEL {report.PitLevel}:");
                    testLog.AppendLine($"  Current Performance: {report.WinRate:P0} win rate, {report.AverageTurns:F1} avg turns");
                    testLog.AppendLine($"  Current Monster Stats: HP={report.CurrentMonsterHP}, Attack={report.CurrentMonsterAttack}, Defense={report.CurrentMonsterDefense}");
                    testLog.AppendLine($"  Hero Stats (Unequipped): Attack={report.HeroAttack}, Defense={report.HeroDefense}");
                    testLog.AppendLine();
                    
                    // Calculate suggested adjustments
                    int suggestedHPIncrease = (int)(report.CurrentMonsterHP * 0.25); // 25% increase
                    int suggestedAttackIncrease = (int)(report.CurrentMonsterAttack * 0.20); // 20% increase
                    int suggestedAgilityIncrease = 2; // Modest agility boost
                    
                    testLog.AppendLine($"  SUGGESTED ADJUSTMENTS:");
                    testLog.AppendLine($"  - Increase Monster HP: {report.CurrentMonsterHP} → {report.CurrentMonsterHP + suggestedHPIncrease} (+{suggestedHPIncrease})");
                    testLog.AppendLine($"  - Increase Monster Attack (Strength): {report.CurrentMonsterAttack} → {report.CurrentMonsterAttack + suggestedAttackIncrease} (+{suggestedAttackIncrease})");
                    testLog.AppendLine($"  - Increase Monster Agility: +{suggestedAgilityIncrease} (for better evasion)");
                    testLog.AppendLine();
                }
            }
            
            Console.WriteLine(testLog.ToString());
        }

        #endregion

        #region 5. Maintain Previous Scope - Stat Progression at Key Levels

        [TestMethod]
        [TestCategory("Balance")]
        [TestCategory("Progression")]
        public void StatProgression_KeyLevels_ValidateHeroAndMonsterScaling()
        {
            var testLog = new StringBuilder();
            testLog.AppendLine("===========================================");
            testLog.AppendLine("STAT PROGRESSION AT KEY LEVELS");
            testLog.AppendLine("Levels: 1, 10, 25, 50, 75, 99");
            testLog.AppendLine("===========================================");
            testLog.AppendLine();
            
            var keyLevels = new[] { 1, 10, 25, 50, 75, 99 };
            
            foreach (int level in keyLevels)
            {
                testLog.AppendLine($"--- LEVEL {level} ---");
                
                // Hero stats
                var knight = new Knight();
                var baseStats = new StatBlock(strength: 12, agility: 10, vitality: 8, magic: 2);
                var hero = new Hero(name: "Test Knight", job: knight, level: level, baseStats: baseStats);
                var heroStats = hero.GetTotalStats();
                
                testLog.AppendLine($"Hero - HP:{hero.MaxHP}, MP:{hero.MaxMP}, Str:{heroStats.Strength}, Agi:{heroStats.Agility}, Vit:{heroStats.Vitality}, Mag:{heroStats.Magic}");
                
                // Monster stats (Balanced archetype)
                var monster = new TestMonster(level, BalanceConfig.MonsterArchetype.Balanced);
                testLog.AppendLine($"Monster (Balanced) - HP:{monster.MaxHP}, Str:{monster.Stats.Strength}, Agi:{monster.Stats.Agility}, Vit:{monster.Stats.Vitality}, Mag:{monster.Stats.Magic}");
                
                // Validate progression
                Assert.IsTrue(hero.MaxHP > 0 && hero.MaxHP <= StatConstants.MaxHP, 
                    $"Level {level} hero HP should be within valid range");
                Assert.IsTrue(heroStats.Strength <= StatConstants.MaxStat, 
                    $"Level {level} hero stats should not exceed cap");
                Assert.IsTrue(monster.MaxHP > 0 && monster.Stats.Strength > 0, 
                    $"Level {level} monster should have valid stats");
                
                testLog.AppendLine();
            }
            
            testLog.AppendLine("✓ All key levels show valid stat progression within caps");
            Console.WriteLine(testLog.ToString());
        }

        [TestMethod]
        [TestCategory("Balance")]
        [TestCategory("Equipment")]
        public void Equipment_Effectiveness_AtDifferentPitLevelsAndRarities()
        {
            var testLog = new StringBuilder();
            testLog.AppendLine("===========================================");
            testLog.AppendLine("EQUIPMENT EFFECTIVENESS");
            testLog.AppendLine("Pit Levels 1, 10, 25, 50 across rarities");
            testLog.AppendLine("===========================================");
            testLog.AppendLine();
            
            var pitLevels = new[] { 1, 10, 25, 50 };
            var rarities = new[] { ItemRarity.Normal, ItemRarity.Rare, ItemRarity.Legendary };
            
            foreach (int pitLevel in pitLevels)
            {
                testLog.AppendLine($"--- Pit Level {pitLevel} ---");
                
                foreach (var rarity in rarities)
                {
                    int attackBonus = BalanceConfig.CalculateEquipmentAttackBonus(pitLevel, rarity);
                    int defenseBonus = BalanceConfig.CalculateEquipmentDefenseBonus(pitLevel, rarity);
                    int statBonus = BalanceConfig.CalculateEquipmentStatBonus(pitLevel, rarity);
                    
                    testLog.AppendLine($"{rarity,10}: Attack +{attackBonus,3}, Defense +{defenseBonus,3}, Stats +{statBonus,2}");
                    
                    // Validate scaling
                    Assert.IsTrue(attackBonus > 0, $"Pit {pitLevel} {rarity} should have positive attack bonus");
                    Assert.IsTrue(defenseBonus > 0, $"Pit {pitLevel} {rarity} should have positive defense bonus");
                }
                
                testLog.AppendLine();
            }
            
            testLog.AppendLine("✓ Equipment scales appropriately with pit level and rarity");
            Console.WriteLine(testLog.ToString());
        }

        [TestMethod]
        [TestCategory("Balance")]
        [TestCategory("Elemental")]
        public void ElementalSystem_AdvantageAndDisadvantage_WorksCorrectly()
        {
            var testLog = new StringBuilder();
            testLog.AppendLine("===========================================");
            testLog.AppendLine("ELEMENTAL MATCHUP VALIDATION");
            testLog.AppendLine("===========================================");
            testLog.AppendLine();
            
            // Test Fire vs Water (advantage)
            var fireElement = ElementType.Fire;
            var waterProps = new ElementalProperties(ElementType.Water);
            var fireVsWater = BalanceConfig.GetElementalDamageMultiplier(fireElement, waterProps);
            
            testLog.AppendLine($"Fire vs Water: {fireVsWater}x damage (expected 2.0x - advantage)");
            Assert.AreEqual(2.0f, fireVsWater, 0.01f, "Fire should deal 2x damage to Water");
            
            // Test Fire vs Fire (resistance)
            var fireProps = new ElementalProperties(ElementType.Fire);
            var fireVsFire = BalanceConfig.GetElementalDamageMultiplier(fireElement, fireProps);
            
            testLog.AppendLine($"Fire vs Fire: {fireVsFire}x damage (expected 0.5x - resistance)");
            Assert.AreEqual(0.5f, fireVsFire, 0.01f, "Fire should deal 0.5x damage to Fire");
            
            // Test Neutral (no advantage/disadvantage)
            var neutralProps = new ElementalProperties(ElementType.Neutral);
            var fireVsNeutral = BalanceConfig.GetElementalDamageMultiplier(fireElement, neutralProps);
            
            testLog.AppendLine($"Fire vs Neutral: {fireVsNeutral}x damage (expected 1.0x - neutral)");
            Assert.AreEqual(1.0f, fireVsNeutral, 0.01f, "Fire should deal normal damage to Neutral");
            
            testLog.AppendLine();
            testLog.AppendLine("✓ Elemental matchups work correctly (2x advantage, 0.5x resistance, 1x neutral)");
            
            Console.WriteLine(testLog.ToString());
        }

        #endregion

        #region Helper Classes

        private class BattleResult
        {
            public bool HeroWon { get; set; }
            public bool MonsterWon { get; set; }
            public int TurnCount { get; set; }
            public string Log { get; set; } = "";
        }

        private class PitLevelResult
        {
            public int PitLevel { get; set; }
            public double WinRate { get; set; }
            public double AverageTurns { get; set; }
        }

        private class ImbalanceReport
        {
            public int PitLevel { get; set; }
            public double WinRate { get; set; }
            public double AverageTurns { get; set; }
            public int CurrentMonsterHP { get; set; }
            public int CurrentMonsterAttack { get; set; }
            public int CurrentMonsterDefense { get; set; }
            public int HeroAttack { get; set; }
            public int HeroDefense { get; set; }
        }

        /// <summary>Test-only monster that doesn't depend on EnemyLevelConfig</summary>
        private class TestMonster : IEnemy
        {
            private int _hp;
            
            public string Name => $"Test Monster L{Level}";
            public int Level { get; }
            public StatBlock Stats { get; }
            public DamageKind AttackKind => DamageKind.Physical;
            public ElementType Element => ElementType.Neutral;
            public ElementalProperties ElementalProps { get; }
            public int MaxHP { get; }
            public int CurrentHP => _hp;
            public int ExperienceYield { get; }

            public TestMonster(int level, BalanceConfig.MonsterArchetype archetype)
            {
                Level = level;
                
                var strength = BalanceConfig.CalculateMonsterStat(level, archetype, BalanceConfig.StatType.Strength);
                var agility = BalanceConfig.CalculateMonsterStat(level, archetype, BalanceConfig.StatType.Agility);
                var vitality = BalanceConfig.CalculateMonsterStat(level, archetype, BalanceConfig.StatType.Vitality);
                var magic = BalanceConfig.CalculateMonsterStat(level, archetype, BalanceConfig.StatType.Magic);
                
                Stats = new StatBlock(strength, agility, vitality, magic);
                MaxHP = BalanceConfig.CalculateMonsterHP(level, archetype);
                _hp = MaxHP;
                ExperienceYield = BalanceConfig.CalculateMonsterExperience(level);
                ElementalProps = new ElementalProperties(ElementType.Neutral);
            }

            public bool TakeDamage(int amount)
            {
                if (amount <= 0) return false;
                _hp -= amount;
                if (_hp < 0) _hp = 0;
                return _hp == 0;
            }
        }

        #endregion
    }
}
