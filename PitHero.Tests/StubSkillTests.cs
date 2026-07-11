using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Mercenaries;
using RolePlayingFramework.Skills;
using RolePlayingFramework.Stats;
using System.Collections.Generic;

namespace PitHero.Tests
{
    /// <summary>
    /// End-to-end tests for the Phase 4 stub-skill mechanics:
    /// Eagle Eye (sight bonus), Shadowstep (evasion), Vanish (Untargetable buff),
    /// Sneak Attack (first-strike AGI conditioning), and Quickdraw
    /// (BattleReactionHelper.RollFirstAttackCrit boundaries).
    /// </summary>
    [TestClass]
    public class StubSkillTests
    {
        // ── Shared factory helpers ────────────────────────────────────────────────────

        private static Hero MakeHero(int str = 10, int agi = 10, int vit = 10, int mag = 5)
            => new Hero("TestHero", new Archer(), level: 5, new StatBlock(str, agi, vit, mag));

        private static Mercenary MakeMerc(int str = 10, int agi = 10, int vit = 10, int mag = 5)
            => new Mercenary("TestMerc", new Thief(), level: 5, new StatBlock(str, agi, vit, mag));

        // ── Eagle Eye — SightRangeBonus ──────────────────────────────────────────────

        [TestMethod]
        [TestCategory("StubSkills")]
        public void EagleEye_HeroWithSkillLearned_HasSightRangeBonusOfOne()
        {
            // End-to-end via TryPurchaseSkill → ApplyPassiveSkills
            var crystal = new HeroCrystal("ArcherCrystal", new Archer(), 1, new StatBlock(7, 9, 6, 4));
            var hero = new Hero("Archer", crystal.Job, crystal.Level, crystal.BaseStats, crystal);
            Assert.AreEqual(0, hero.SightRangeBonus, "Baseline: no SightRangeBonus before Eagle Eye learned");

            hero.EarnJP(100); // EagleEyePassive costs 70 JP
            var archer = new Archer();
            bool purchased = hero.TryPurchaseSkill(archer.Skills[0]); // EagleEyePassive

            Assert.IsTrue(purchased, "TryPurchaseSkill should succeed with sufficient JP");
            Assert.AreEqual(1, hero.SightRangeBonus,
                "After purchasing Eagle Eye, SightRangeBonus should be 1");
        }

        [TestMethod]
        [TestCategory("StubSkills")]
        public void EagleEye_MercenaryLearnSkill_HasSightRangeBonusOfOne()
        {
            // End-to-end via Mercenary.LearnSkill → ApplyPassiveSkills
            var merc = new Mercenary("TestArcher", new Archer(), level: 5, new StatBlock(7, 9, 6, 4));
            Assert.AreEqual(0, merc.SightRangeBonus, "Baseline: no SightRangeBonus before Eagle Eye learned");

            merc.LearnSkill(new EagleEyePassive());

            Assert.AreEqual(1, merc.SightRangeBonus,
                "After learning Eagle Eye, Mercenary SightRangeBonus should be 1");
        }

        [TestMethod]
        [TestCategory("StubSkills")]
        public void EagleEye_TwoInstancesStack()
        {
            // Verifies += behaviour (relevant if two sources could grant it)
            var hero = MakeHero();
            var passive = new EagleEyePassive();
            passive.ApplyPassive(hero);
            passive.ApplyPassive(hero);
            Assert.AreEqual(2, hero.SightRangeBonus,
                "Applying EagleEye passive twice should stack to +2 (each call does += 1)");
        }

        // ── Shadowstep — +20 evasion in GetBattleStats ──────────────────────────────

        [TestMethod]
        [TestCategory("StubSkills")]
        public void Shadowstep_Hero_EvasionIncreasedByTwenty()
        {
            var hero = MakeHero(agi: 10);
            int baseEvasion = hero.GetBattleStats().Evasion;

            var shadowstep = new ShadowstepPassive();
            shadowstep.ApplyPassive(hero);

            int buffedEvasion = hero.GetBattleStats().Evasion;
            Assert.AreEqual(baseEvasion + 20, buffedEvasion,
                "Shadowstep should raise Hero GetBattleStats().Evasion by exactly 20");
        }

        [TestMethod]
        [TestCategory("StubSkills")]
        public void Shadowstep_Mercenary_EvasionIncreasedByTwenty()
        {
            var merc = MakeMerc(agi: 10);
            int baseEvasion = merc.GetBattleStats().Evasion;

            merc.LearnSkill(new ShadowstepPassive());

            int buffedEvasion = merc.GetBattleStats().Evasion;
            Assert.AreEqual(baseEvasion + 20, buffedEvasion,
                "Shadowstep should raise Mercenary GetBattleStats().Evasion by exactly 20");
        }

        [TestMethod]
        [TestCategory("StubSkills")]
        public void Shadowstep_Mercenary_ForgetSkill_EvasionReturnsToBaseline()
        {
            var merc = MakeMerc(agi: 10);
            int baseEvasion = merc.GetBattleStats().Evasion;

            merc.LearnSkill(new ShadowstepPassive());
            merc.ForgetSkill("thief.shadowstep");

            Assert.AreEqual(baseEvasion, merc.GetBattleStats().Evasion,
                "After forgetting Shadowstep, evasion should return to baseline");
        }

        // ── Vanish — Untargetable buff declaration and lifecycle ─────────────────────

        [TestMethod]
        [TestCategory("StubSkills")]
        public void Vanish_DeclaresUntargetableGrantedBuff()
        {
            var vanish = new VanishSkill();

            Assert.IsTrue(vanish.GrantedBuffs.Count > 0,
                "VanishSkill should declare at least one GrantedBuff");

            bool hasUntargetable = false;
            for (int i = 0; i < vanish.GrantedBuffs.Count; i++)
            {
                if (vanish.GrantedBuffs[i].Type == BuffType.Untargetable)
                {
                    hasUntargetable = true;
                    break;
                }
            }
            Assert.IsTrue(hasUntargetable,
                "VanishSkill GrantedBuffs must include an Untargetable buff");
        }

        [TestMethod]
        [TestCategory("StubSkills")]
        public void Vanish_GrantedBuff_HasOneTurnDurationAndMaxStacksOne()
        {
            var vanish = new VanishSkill();
            SkillBuff buff = default;
            for (int i = 0; i < vanish.GrantedBuffs.Count; i++)
            {
                if (vanish.GrantedBuffs[i].Type == BuffType.Untargetable)
                {
                    buff = vanish.GrantedBuffs[i];
                    break;
                }
            }

            Assert.AreEqual(2, buff.DurationTurns,
                "Vanish Untargetable buff should declare duration 2 (end-of-round tick consumes one turn in the cast round, so 2 = rest of cast round + all of next round)");
            Assert.AreEqual(1, buff.MaxStacks,
                "Vanish Untargetable buff should allow at most 1 stack");
        }

        [TestMethod]
        [TestCategory("StubSkills")]
        public void Vanish_HasNoHPRestore_CategorizedAsBuff()
        {
            var vanish = new VanishSkill();
            // Decision engine categorizes as buff when HPRestoreAmount==0 and GrantedBuffs.Count>0
            Assert.AreEqual(0, vanish.HPRestoreAmount,
                "VanishSkill should not restore HP (pure buff skill)");
            Assert.IsTrue(vanish.GrantedBuffs.Count > 0,
                "VanishSkill must have GrantedBuffs so the decision engine picks it up as a buff skill");
        }

        [TestMethod]
        [TestCategory("StubSkills")]
        public void Vanish_AddBattleBuff_GetBuffTotal_IsPositive()
        {
            var hero = MakeHero();

            // Simulate what ApplyHealingSkillEffectsAndDisplay does when processing GrantedBuffs
            hero.AddBattleBuff(new BattleBuff(BuffType.Untargetable, magnitude: 1, remainingTurns: 1, sourceSkillId: "thief.vanish"));

            Assert.IsTrue(hero.GetBuffTotal(BuffType.Untargetable) > 0,
                "After adding Untargetable BattleBuff, GetBuffTotal(Untargetable) should be > 0");
        }

        [TestMethod]
        [TestCategory("StubSkills")]
        public void Vanish_AfterTwoTickBuffDurations_UntargetableExpires()
        {
            var hero = MakeHero();
            // DurationTurns:2 — one tick consumed at end of cast round, one at end of next round
            hero.AddBattleBuff(new BattleBuff(BuffType.Untargetable, magnitude: 1, remainingTurns: 2, sourceSkillId: "thief.vanish"));
            Assert.IsTrue(hero.GetBuffTotal(BuffType.Untargetable) > 0, "Precondition: buff should be active");

            hero.TickBuffDurations(); // end of cast round
            Assert.IsTrue(hero.GetBuffTotal(BuffType.Untargetable) > 0,
                "After one tick (end of cast round), Untargetable should still be active");

            hero.TickBuffDurations(); // end of next round
            Assert.AreEqual(0, hero.GetBuffTotal(BuffType.Untargetable),
                "Untargetable buff with duration 2 should expire after two TickBuffDurations calls");
        }

        // ── Sneak Attack — first-strike AGI conditioning ─────────────────────────────

        /// <summary>
        /// Fake IAttackResolver that always hits with a fixed damage amount.
        /// Lets tests verify exactly how much damage SneakAttack adds on top.
        /// </summary>
        private sealed class FixedHitResolver : IAttackResolver
        {
            private readonly int _damage;
            public FixedHitResolver(int damage) { _damage = damage; }

            public AttackResult Resolve(in StatBlock attackerStats, in StatBlock defenderStats,
                DamageKind kind, int attackerLevel, int defenderLevel)
                => new AttackResult(hit: true, damage: _damage);
        }

        /// <summary>Minimal IBattleContext that always returns the configured IsFirst value.</summary>
        private sealed class StubBattleContext : IBattleContext
        {
            public bool AlwaysFirst;
            public StubBattleContext(bool alwaysFirst) { AlwaysFirst = alwaysFirst; }

            public bool IsFirstOffensiveAction(ICombatant c) => AlwaysFirst;
            public void MarkActed(ICombatant c) { }
            public void RegisterDoT(IEnemy target, int damagePerTurn, int turns, string sourceSkillId, ICombatant actor) { }
        }

        /// <summary>IEnemy stub that accumulates total damage received.</summary>
        private sealed class DamageAccumulator : IEnemy
        {
            private int _hp;
            private readonly int _startHp;

            public DamageAccumulator(int hp = 10000) { _hp = hp; _startHp = hp; MaxHP = hp; }

            public int TotalDamageReceived { get; private set; }
            public void ResetDamage() { TotalDamageReceived = 0; _hp = _startHp; }

            public string Name => "DummyEnemy";
            public EnemyId EnemyId => EnemyId.Slime;
            public int Level => 1;
            public StatBlock Stats => new StatBlock(1, 1, 1, 1);
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
                TotalDamageReceived += amount;
                _hp -= amount;
                if (_hp < 0) _hp = 0;
                return _hp == 0;
            }
        }

        [TestMethod]
        [TestCategory("StubSkills")]
        public void SneakAttack_FirstAction_DealsBaseAndFullAGI()
        {
            // Caster with AGI = 20 (total stats will include job bonuses; use base 20 to get a known total)
            var hero = new Hero("Rogue", new Thief(), level: 1, new StatBlock(5, 20, 5, 5));
            int expectedAgi = hero.GetTotalStats().Agility;

            var enemy = new DamageAccumulator();
            var resolver = new FixedHitResolver(10); // base hit = 10
            var context = new StubBattleContext(alwaysFirst: true);

            var skill = new SneakAttackSkill();
            skill.Execute(hero, enemy, new List<IEnemy>(), resolver, context);

            int expected = 10 + expectedAgi;
            Assert.AreEqual(expected, enemy.TotalDamageReceived,
                $"First action: SneakAttack should deal base(10) + full AGI({expectedAgi}) = {expected}");
        }

        [TestMethod]
        [TestCategory("StubSkills")]
        public void SneakAttack_SubsequentAction_DealsBaseAndHalfAGI()
        {
            var hero = new Hero("Rogue", new Thief(), level: 1, new StatBlock(5, 20, 5, 5));
            int expectedAgi = hero.GetTotalStats().Agility;

            var enemy = new DamageAccumulator();
            var resolver = new FixedHitResolver(10);
            var context = new StubBattleContext(alwaysFirst: false); // not the first action

            var skill = new SneakAttackSkill();
            skill.Execute(hero, enemy, new List<IEnemy>(), resolver, context);

            int expected = 10 + expectedAgi / 2;
            Assert.AreEqual(expected, enemy.TotalDamageReceived,
                $"Subsequent action: SneakAttack should deal base(10) + AGI/2({expectedAgi / 2}) = {expected}");
        }

        [TestMethod]
        [TestCategory("StubSkills")]
        public void SneakAttack_NullBattleContext_DealsBaseAndHalfAGI()
        {
            var hero = new Hero("Rogue", new Thief(), level: 1, new StatBlock(5, 20, 5, 5));
            int expectedAgi = hero.GetTotalStats().Agility;

            var enemy = new DamageAccumulator();
            var resolver = new FixedHitResolver(10);

            var skill = new SneakAttackSkill();
            skill.Execute(hero, enemy, new List<IEnemy>(), resolver, null); // no battle context

            int expected = 10 + expectedAgi / 2;
            Assert.AreEqual(expected, enemy.TotalDamageReceived,
                $"Out-of-battle (null context): SneakAttack should deal base(10) + AGI/2({expectedAgi / 2}) = {expected}");
        }

        // ── Quickdraw — RollFirstAttackCrit boundaries ──────────────────────────────

        [TestMethod]
        [TestCategory("StubSkills")]
        public void Quickdraw_RollFirstAttackCrit_FirstActionAndRollBelowChance_ReturnsCrit()
        {
            var caster = MakeHero();
            caster.FirstAttackCritChance = 0.5f;

            bool result = BattleReactionHelper.RollFirstAttackCrit(caster, isFirstAction: true, roll: 0.3f);

            Assert.IsTrue(result,
                "First action + roll (0.3) < chance (0.5) should return true (crit)");
        }

        [TestMethod]
        [TestCategory("StubSkills")]
        public void Quickdraw_RollFirstAttackCrit_NotFirstAction_NeverCrits()
        {
            var caster = MakeHero();
            caster.FirstAttackCritChance = 0.5f;

            // Even with roll = 0 (guaranteed win) and high crit chance, non-first action never crits
            bool result = BattleReactionHelper.RollFirstAttackCrit(caster, isFirstAction: false, roll: 0.0f);

            Assert.IsFalse(result,
                "Non-first action should never trigger a crit regardless of roll or chance");
        }

        [TestMethod]
        [TestCategory("StubSkills")]
        public void Quickdraw_RollFirstAttackCrit_RollAtChance_NoCrit()
        {
            var caster = MakeHero();
            caster.FirstAttackCritChance = 0.5f;

            bool result = BattleReactionHelper.RollFirstAttackCrit(caster, isFirstAction: true, roll: 0.5f);

            Assert.IsFalse(result,
                "Roll equal to chance (not strictly less-than) should not crit");
        }

        [TestMethod]
        [TestCategory("StubSkills")]
        public void Quickdraw_RollFirstAttackCrit_RollAboveChance_NoCrit()
        {
            var caster = MakeHero();
            caster.FirstAttackCritChance = 0.5f;

            bool result = BattleReactionHelper.RollFirstAttackCrit(caster, isFirstAction: true, roll: 0.8f);

            Assert.IsFalse(result,
                "Roll (0.8) above chance (0.5) should not crit");
        }

        [TestMethod]
        [TestCategory("StubSkills")]
        public void Quickdraw_RollFirstAttackCrit_ZeroCritChance_NeverCrits()
        {
            var caster = MakeHero();
            // Default FirstAttackCritChance = 0 (no Quickdraw passive)
            Assert.AreEqual(0f, caster.FirstAttackCritChance);

            bool result = BattleReactionHelper.RollFirstAttackCrit(caster, isFirstAction: true, roll: 0.0f);

            Assert.IsFalse(result,
                "Zero FirstAttackCritChance should never produce a crit even with roll = 0");
        }

        [TestMethod]
        [TestCategory("StubSkills")]
        public void Quickdraw_PassiveApply_SetsCritChanceToHalf()
        {
            var hero = MakeHero();
            Assert.AreEqual(0f, hero.FirstAttackCritChance, "Baseline: no crit chance before Quickdraw learned");

            var quickdraw = new QuickdrawPassive();
            quickdraw.ApplyPassive(hero);

            Assert.AreEqual(0.5f, hero.FirstAttackCritChance, 0.001f,
                "QuickdrawPassive should set FirstAttackCritChance to 0.5 (50%)");
        }
    }
}
