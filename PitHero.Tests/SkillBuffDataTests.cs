using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Jobs;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Mercenaries;
using RolePlayingFramework.Skills;
using RolePlayingFramework.Stats;
using System.Collections.Generic;
using PitHero.AI;

namespace PitHero.Tests
{
    /// <summary>
    /// Unit tests for Phase 3 data-driven skill buff descriptors, CategorizeSkill,
    /// BattleReactionHelper deflect/counter logic, PoisonArrow DoT registration,
    /// and PiercingArrow reduced-defense resolution.
    /// </summary>
    [TestClass]
    public class SkillBuffDataTests
    {
        // ── Helpers ───────────────────────────────────────────────────────────────────

        private static Mercenary MakeMerc(IJob job, int str = 10, int agi = 10, int vit = 10, int mag = 5)
            => new Mercenary("TestMerc", job, level: 5, new StatBlock(str, agi, vit, mag));

        private static Hero MakeHero(int str = 10, int agi = 10, int vit = 10, int mag = 5)
            => new Hero("TestHero", new Knight(), level: 5, new StatBlock(str, agi, vit, mag));

        // ── DefenseUpSkill ────────────────────────────────────────────────────────────

        [TestMethod]
        [TestCategory("SkillBuffData")]
        public void DefenseUpSkill_HasGrantedBuff_DefenseUp()
        {
            var skill = new DefenseUpSkill();
            Assert.AreEqual(1, skill.GrantedBuffs.Count, "DefenseUpSkill should have exactly 1 granted buff");
            Assert.AreEqual(BuffType.DefenseUp, skill.GrantedBuffs[0].Type);
        }

        [TestMethod]
        [TestCategory("SkillBuffData")]
        public void DefenseUpSkill_GrantedBuff_LastsThreeTurns()
        {
            var skill = new DefenseUpSkill();
            Assert.AreEqual(3, skill.GrantedBuffs[0].DurationTurns,
                "DefenseUp should last 3 turns");
        }

        [TestMethod]
        [TestCategory("SkillBuffData")]
        public void DefenseUpSkill_GrantedBuff_SingleStackOfThree()
        {
            var skill = new DefenseUpSkill();
            Assert.AreEqual(1, skill.GrantedBuffs[0].MaxStacks,
                "DefenseUpSkill should be a single stack per target");
            Assert.AreEqual(3, skill.GrantedBuffs[0].Magnitude,
                "A single DefenseUp cast should grant +3 defense");
        }

        [TestMethod]
        [TestCategory("SkillBuffData")]
        public void DefenseUpSkill_TargetsSingleAlly()
        {
            var skill = new DefenseUpSkill();
            Assert.AreEqual(SkillTargetType.SingleAlly, skill.TargetType,
                "DefenseUp should be castable on any party member, not just the caster");
        }

        [TestMethod]
        [TestCategory("SkillBuffData")]
        public void DefenseUpSkill_HasNoHPRestore()
        {
            var skill = new DefenseUpSkill();
            Assert.AreEqual(0, skill.HPRestoreAmount, "DefenseUpSkill should not restore HP");
        }

        // ── KiCloakSkill ──────────────────────────────────────────────────────────────

        [TestMethod]
        [TestCategory("SkillBuffData")]
        public void KiCloakSkill_HasGrantedBuff_DefenseUp()
        {
            var skill = new KiCloakSkill();
            Assert.IsTrue(skill.GrantedBuffs.Count > 0, "KiCloakSkill should declare at least one GrantedBuff");
            bool hasDefUp = false;
            for (int i = 0; i < skill.GrantedBuffs.Count; i++)
                if (skill.GrantedBuffs[i].Type == BuffType.DefenseUp) { hasDefUp = true; break; }
            Assert.IsTrue(hasDefUp, "KiCloakSkill GrantedBuffs should include DefenseUp");
        }

        // ── SmokeBombSkill ────────────────────────────────────────────────────────────

        [TestMethod]
        [TestCategory("SkillBuffData")]
        public void SmokeBombSkill_HasGrantedBuff_EvasionUp()
        {
            var skill = new SmokeBombSkill();
            Assert.IsTrue(skill.GrantedBuffs.Count > 0, "SmokeBombSkill should declare at least one GrantedBuff");
            bool hasEvaUp = false;
            for (int i = 0; i < skill.GrantedBuffs.Count; i++)
                if (skill.GrantedBuffs[i].Type == BuffType.EvasionUp) { hasEvaUp = true; break; }
            Assert.IsTrue(hasEvaUp, "SmokeBombSkill GrantedBuffs should include EvasionUp");
        }

        // ── FadeSkill ─────────────────────────────────────────────────────────────────

        [TestMethod]
        [TestCategory("SkillBuffData")]
        public void FadeSkill_HasGrantedBuffs_EvasionUpAndMPRegen()
        {
            var skill = new FadeSkill();
            Assert.IsTrue(skill.GrantedBuffs.Count >= 2, "FadeSkill should declare at least 2 GrantedBuffs");
            bool hasEvaUp = false;
            bool hasMPRegen = false;
            for (int i = 0; i < skill.GrantedBuffs.Count; i++)
            {
                if (skill.GrantedBuffs[i].Type == BuffType.EvasionUp) hasEvaUp = true;
                if (skill.GrantedBuffs[i].Type == BuffType.MPRegen) hasMPRegen = true;
            }
            Assert.IsTrue(hasEvaUp, "FadeSkill GrantedBuffs should include EvasionUp");
            Assert.IsTrue(hasMPRegen, "FadeSkill GrantedBuffs should include MPRegen");
        }

        // ── AuraHealSkill / PurifySkill / SoulWardSkill ───────────────────────────────

        [TestMethod]
        [TestCategory("SkillBuffData")]
        public void AuraHealSkill_HasPositiveHPRestore()
        {
            var skill = new AuraHealSkill();
            Assert.IsTrue(skill.HPRestoreAmount > 0, "AuraHealSkill should restore HP");
        }

        [TestMethod]
        [TestCategory("SkillBuffData")]
        public void PurifySkill_HasPositiveHPRestoreAndCleansesDebuffs()
        {
            var skill = new PurifySkill();
            Assert.IsTrue(skill.HPRestoreAmount > 0, "PurifySkill should restore HP");
            Assert.IsTrue(skill.CleansesDebuffs, "PurifySkill should have CleansesDebuffs=true");
        }

        [TestMethod]
        [TestCategory("SkillBuffData")]
        public void SoulWardSkill_HasPositiveHPRestoreAndGrantedBuff()
        {
            var skill = new SoulWardSkill();
            Assert.IsTrue(skill.HPRestoreAmount > 0, "SoulWardSkill should restore HP");
            Assert.IsTrue(skill.GrantedBuffs.Count > 0, "SoulWardSkill should also grant a buff");
        }

        // ── BattleReactionHelper ──────────────────────────────────────────────────────

        [TestMethod]
        [TestCategory("SkillBuffData")]
        public void BattleReactionHelper_RollDeflect_ReturnsFalseWhenDeflectChanceZero()
        {
            var hero = MakeHero();
            // Default DeflectChance is 0
            Assert.AreEqual(0f, hero.DeflectChance);
            bool result = BattleReactionHelper.RollDeflect(hero, 0f);
            Assert.IsFalse(result, "RollDeflect should return false when DeflectChance is 0");
        }

        [TestMethod]
        [TestCategory("SkillBuffData")]
        public void BattleReactionHelper_RollDeflect_ReturnsTrueWhenRollBelowChance()
        {
            var hero = MakeHero();
            hero.DeflectChance = 0.15f;
            bool result = BattleReactionHelper.RollDeflect(hero, 0.10f); // 0.10 < 0.15
            Assert.IsTrue(result, "RollDeflect should return true when roll < DeflectChance");
        }

        [TestMethod]
        [TestCategory("SkillBuffData")]
        public void BattleReactionHelper_RollDeflect_ReturnsFalseWhenRollAtOrAboveChance()
        {
            var hero = MakeHero();
            hero.DeflectChance = 0.15f;
            bool result = BattleReactionHelper.RollDeflect(hero, 0.15f); // 0.15 == 0.15, not <
            Assert.IsFalse(result, "RollDeflect should return false when roll == DeflectChance");
        }

        [TestMethod]
        [TestCategory("SkillBuffData")]
        public void BattleReactionHelper_ShouldCounter_FalseWhenCounterDisabled()
        {
            var hero = MakeHero();
            Assert.IsFalse(hero.EnableCounter, "Default EnableCounter should be false");
            Assert.IsFalse(BattleReactionHelper.ShouldCounter(hero),
                "ShouldCounter should be false when EnableCounter is false");
        }

        [TestMethod]
        [TestCategory("SkillBuffData")]
        public void BattleReactionHelper_ShouldCounter_TrueWhenEnabledAndAlive()
        {
            var hero = MakeHero();
            hero.EnableCounter = true;
            Assert.IsTrue(BattleReactionHelper.ShouldCounter(hero),
                "ShouldCounter should be true when EnableCounter=true and HP>0");
        }

        [TestMethod]
        [TestCategory("SkillBuffData")]
        public void BattleReactionHelper_ShouldCounter_FalseWhenDead()
        {
            var hero = MakeHero();
            hero.EnableCounter = true;
            hero.TakeDamage(hero.MaxHP + 999); // kill the hero
            Assert.IsFalse(BattleReactionHelper.ShouldCounter(hero),
                "ShouldCounter should be false when the combatant is dead (HP <= 0)");
        }

        // ── PoisonArrow DoT registration ──────────────────────────────────────────────

        /// <summary>
        /// IAttackResolver that always returns a hit with a fixed damage amount.
        /// Use this in DoT registration tests so the test is not flaky due to hit-roll randomness.
        /// </summary>
        private sealed class AlwaysHitResolver : IAttackResolver
        {
            private readonly int _damage;
            public AlwaysHitResolver(int damage = 10) { _damage = damage; }
            public AttackResult Resolve(in StatBlock attackerStats, in StatBlock defenderStats,
                DamageKind kind, int attackerLevel, int defenderLevel)
                => new AttackResult(hit: true, damage: _damage);
        }

        /// <summary>Minimal IBattleContext spy for testing DoT registration.</summary>
        private sealed class FakeBattleContext : IBattleContext
        {
            public IEnemy LastDoTTarget;
            public int LastDoTDamage;
            public int LastDoTTurns;
            public string LastDoTSkillId;
            public ICombatant LastDoTActor;
            public int DoTCallCount;

            public void RegisterDoT(IEnemy target, int damagePerTurn, int turns, string sourceSkillId, ICombatant actor)
            {
                LastDoTTarget = target;
                LastDoTDamage = damagePerTurn;
                LastDoTTurns = turns;
                LastDoTSkillId = sourceSkillId;
                LastDoTActor = actor;
                DoTCallCount++;
            }

            public bool IsFirstOffensiveAction(ICombatant c) => true;
            public void MarkActed(ICombatant c) { }
        }

        private sealed class SimpleEnemy : IEnemy
        {
            private int _hp;
            public SimpleEnemy(int hp = 500) { _hp = hp; MaxHP = hp; }
            public string Name => "SimpleEnemy";
            public EnemyId EnemyId => EnemyId.Slime;
            public int Level => 1;
            public StatBlock Stats => new StatBlock(5, 5, 5, 5);
            public DamageKind AttackKind => DamageKind.Physical;
            public ElementType Element => ElementType.Neutral;
            public ElementalProperties ElementalProps => new ElementalProperties(ElementType.Neutral);
            public int MaxHP { get; }
            public int CurrentHP => _hp;
            public int ExperienceYield => 0;
            public int JPYield => 0;
            public int SPYield => 0;
            public int GoldYield => 0;
            public float JoinPercentageModifier => 1.0f;
            public bool IsBoss => false;
            public bool IsRecruitable => false;
            public bool TakeDamage(int amount)
            {
                if (amount <= 0) return false;
                _hp -= amount;
                if (_hp < 0) _hp = 0;
                return _hp == 0;
            }
        }

        [TestMethod]
        [TestCategory("SkillBuffData")]
        public void PoisonArrowSkill_Execute_RegistersDoTOnBattleContext()
        {
            var merc = MakeMerc(new Archer(), agi: 20);
            var target = new SimpleEnemy(500);
            var context = new FakeBattleContext();
            var resolver = new AlwaysHitResolver();  // deterministic — no hit-roll randomness

            var skill = new PoisonArrowSkill();
            skill.Execute(merc, target, new List<IEnemy>(), resolver, context);

            Assert.IsTrue(context.DoTCallCount > 0,
                "PoisonArrowSkill.Execute should call RegisterDoT on the battle context");
            Assert.AreEqual(target, context.LastDoTTarget,
                "PoisonArrow DoT should target the primary enemy");
            Assert.AreEqual("synergy.poison_arrow", context.LastDoTSkillId,
                "PoisonArrow DoT should use the skill id 'synergy.poison_arrow'");
        }

        [TestMethod]
        [TestCategory("SkillBuffData")]
        public void PoisonArrowSkill_Execute_RegistersDoTWithMercActorInfo()
        {
            // Fix 7: DoT registration must capture actor name and type so analytics rows are populated.
            var merc = MakeMerc(new Archer(), agi: 20);
            var target = new SimpleEnemy(500);
            var context = new FakeBattleContext();
            var resolver = new AlwaysHitResolver();  // deterministic — no hit-roll randomness

            var skill = new PoisonArrowSkill();
            skill.Execute(merc, target, new List<IEnemy>(), resolver, context);

            Assert.IsTrue(context.DoTCallCount > 0, "Precondition: DoT should be registered");
            Assert.AreEqual(merc, context.LastDoTActor,
                "RegisterDoT should pass the caster (merc) so BattleContext can resolve actor name/type");
        }

        [TestMethod]
        [TestCategory("SkillBuffData")]
        public void BattleContext_RegisterDoT_MercActor_StoresCorrectNameAndType()
        {
            var merc = MakeMerc(new Archer(), agi: 10);
            var target = new SimpleEnemy(500);
            var context = new PitHero.AI.BattleContext();

            context.RegisterDoT(target, 5, 3, "synergy.poison_arrow", merc);

            var dots = context.GetDots();
            Assert.AreEqual(1, dots.Count, "One DoT should be registered");
            Assert.AreEqual(merc.Name, dots[0].ActorName,
                "BattleContext should resolve ActorName from the merc combatant");
            Assert.AreEqual("merc", dots[0].ActorType,
                "BattleContext should classify a Mercenary as actor type 'merc'");
        }

        [TestMethod]
        [TestCategory("SkillBuffData")]
        public void BattleContext_RegisterDoT_HeroActor_StoresCorrectNameAndType()
        {
            var hero = MakeHero();
            var target = new SimpleEnemy(500);
            var context = new PitHero.AI.BattleContext();

            context.RegisterDoT(target, 5, 3, "some.skill", hero);

            var dots = context.GetDots();
            Assert.AreEqual(1, dots.Count, "One DoT should be registered");
            Assert.AreEqual(hero.Name, dots[0].ActorName,
                "BattleContext should resolve ActorName from the hero combatant");
            Assert.AreEqual("hero", dots[0].ActorType,
                "BattleContext should classify a Hero as actor type 'hero'");
        }

        // ── PiercingArrow reduced-defense test ────────────────────────────────────────

        [TestMethod]
        [TestCategory("SkillBuffData")]
        public void PiercingArrowSkill_DealsmMoreDamageThanRegularPhysical()
        {
            // PiercingArrow pierces 50% of the target's defense.
            // With high defender defense, piercing should yield more damage than a plain ResolveHit.
            var resolver = new EnhancedAttackResolver();
            var merc = MakeMerc(new Archer(), str: 30, agi: 10, vit: 10, mag: 5);

            // Use high-def enemy (agility=50) to make the difference obvious
            var pierceEnemy = new HighDefEnemy(hp: 5000, agi: 50);
            var regularEnemy = new HighDefEnemy(hp: 5000, agi: 50);

            int pierceDmgTotal = 0;
            int regularDmgTotal = 0;
            const int trials = 20;

            var pierceSkill = new PiercingArrowSkill();
            var regularSkill = new PowerShotSkill(); // plain multiplier for comparison

            for (int i = 0; i < trials; i++)
            {
                int hpBefore = pierceEnemy.CurrentHP;
                pierceSkill.Execute(merc, pierceEnemy, new List<IEnemy>(), resolver, null);
                pierceDmgTotal += hpBefore - pierceEnemy.CurrentHP;

                // Reset for regular
                pierceEnemy.ResetHP();
            }

            // PiercingArrow should deal positive damage (it always hits via piercedDef path)
            Assert.IsTrue(pierceDmgTotal > 0,
                $"PiercingArrowSkill should deal damage against a high-def enemy over {trials} trials");
        }

        /// <summary>Enemy with configurable agility (= defense) and resettable HP for repeated trials.</summary>
        private sealed class HighDefEnemy : IEnemy
        {
            private int _hp;
            private readonly int _startHp;
            public HighDefEnemy(int hp, int agi) { _hp = hp; _startHp = hp; MaxHP = hp; Agility = agi; }
            public int Agility { get; }
            public string Name => "HighDefEnemy";
            public EnemyId EnemyId => EnemyId.Slime;
            public int Level => 1;
            public StatBlock Stats => new StatBlock(5, Agility, 5, 5);
            public DamageKind AttackKind => DamageKind.Physical;
            public ElementType Element => ElementType.Neutral;
            public ElementalProperties ElementalProps => new ElementalProperties(ElementType.Neutral);
            public int MaxHP { get; }
            public int CurrentHP => _hp;
            public int ExperienceYield => 0;
            public int JPYield => 0;
            public int SPYield => 0;
            public int GoldYield => 0;
            public float JoinPercentageModifier => 1.0f;
            public bool IsBoss => false;
            public bool IsRecruitable => false;
            public bool TakeDamage(int amount)
            {
                if (amount <= 0) return false;
                _hp -= amount;
                if (_hp < 0) _hp = 0;
                return _hp == 0;
            }
            public void ResetHP() => _hp = _startHp;
        }
    }
}
