using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Mercenaries;
using RolePlayingFramework.Skills;
using RolePlayingFramework.Stats;

namespace PitHero.Tests
{
    /// <summary>
    /// Unit tests for the Phase 3 battle-scoped buff system.
    /// Covers AddBattleBuff, GetBuffTotal, GetBuffStacks, TickBuffDurations, ClearBattleState,
    /// and how live buffs affect GetBattleStats() for both Hero and Mercenary.
    /// </summary>
    [TestClass]
    public class BattleBuffTests
    {
        // ── Shared factory helpers ────────────────────────────────────────────────────

        private static Hero MakeHero(int str = 10, int agi = 10, int vit = 10, int mag = 5)
        {
            var baseStats = new StatBlock(str, agi, vit, mag);
            return new Hero("TestHero", new Knight(), level: 5, baseStats: baseStats);
        }

        private static Mercenary MakeMerc(int str = 10, int agi = 10, int vit = 10, int mag = 5)
        {
            var baseStats = new StatBlock(str, agi, vit, mag);
            return new Mercenary("TestMerc", new Monk(), level: 5, baseStats: baseStats);
        }

        // ── Basic add/get ─────────────────────────────────────────────────────────────

        [TestMethod]
        [TestCategory("Buffs")]
        public void Hero_AddBattleBuff_GetBuffTotal_ReturnsMagnitude()
        {
            var hero = MakeHero();
            hero.AddBattleBuff(new BattleBuff(BuffType.DefenseUp, magnitude: 3, remainingTurns: -1, sourceSkillId: "priest.defup"));

            Assert.AreEqual(3, hero.GetBuffTotal(BuffType.DefenseUp),
                "GetBuffTotal should return the magnitude of the added buff");
        }

        [TestMethod]
        [TestCategory("Buffs")]
        public void Mercenary_AddBattleBuff_GetBuffTotal_ReturnsMagnitude()
        {
            var merc = MakeMerc();
            merc.AddBattleBuff(new BattleBuff(BuffType.EvasionUp, magnitude: 40, remainingTurns: 3, sourceSkillId: "thief.smoke_bomb"));

            Assert.AreEqual(40, merc.GetBuffTotal(BuffType.EvasionUp),
                "Mercenary GetBuffTotal should return the magnitude of the added buff");
        }

        [TestMethod]
        [TestCategory("Buffs")]
        public void Hero_MultipleSameTypeBuff_TotalsStack()
        {
            var hero = MakeHero();
            hero.AddBattleBuff(new BattleBuff(BuffType.DefenseUp, magnitude: 1, remainingTurns: -1, sourceSkillId: "priest.defup"));
            hero.AddBattleBuff(new BattleBuff(BuffType.DefenseUp, magnitude: 2, remainingTurns: -1, sourceSkillId: "synergy.auraSword"));

            Assert.AreEqual(3, hero.GetBuffTotal(BuffType.DefenseUp),
                "Multiple DefenseUp buffs from different skill ids should sum their magnitudes");
        }

        [TestMethod]
        [TestCategory("Buffs")]
        public void Hero_GetBuffStacks_CountsBySourceSkillIdAndType()
        {
            var hero = MakeHero();
            hero.AddBattleBuff(new BattleBuff(BuffType.DefenseUp, magnitude: 1, remainingTurns: -1, sourceSkillId: "priest.defup"));
            hero.AddBattleBuff(new BattleBuff(BuffType.DefenseUp, magnitude: 1, remainingTurns: -1, sourceSkillId: "priest.defup"));

            Assert.AreEqual(2, hero.GetBuffStacks("priest.defup", BuffType.DefenseUp),
                "Two DefenseUp buffs from the same skill should count as 2 stacks");
        }

        [TestMethod]
        [TestCategory("Buffs")]
        public void Hero_GetBuffStacks_MultiBuffSkill_CountsEachTypeIndependently()
        {
            // FadeSkill grants EvasionUp + MPRegen from the same source skill id.
            // The stack cap check must filter by type so one type capping does not block the other.
            var hero = MakeHero();
            hero.AddBattleBuff(new BattleBuff(BuffType.EvasionUp, magnitude: 30, remainingTurns: -1, sourceSkillId: "synergy.fade"));

            // EvasionUp has 1 stack; MPRegen has 0 stacks — they must be counted independently
            Assert.AreEqual(1, hero.GetBuffStacks("synergy.fade", BuffType.EvasionUp),
                "After adding EvasionUp from synergy.fade, EvasionUp stack count should be 1");
            Assert.AreEqual(0, hero.GetBuffStacks("synergy.fade", BuffType.MPRegen),
                "MPRegen stack count should be 0 — it was not yet added");
        }

        [TestMethod]
        [TestCategory("Buffs")]
        public void FadeSkill_BothBuffsApplyOnFirstCast_NeitherOnSecond()
        {
            // Simulates what ApplyHealingSkillEffectsAndDisplay does for FadeSkill's two GrantedBuffs.
            var hero = MakeHero();
            var skill = new RolePlayingFramework.Skills.FadeSkill();

            // First cast: apply each buff if stack count < MaxStacks
            for (int b = 0; b < skill.GrantedBuffs.Count; b++)
            {
                var grantedBuff = skill.GrantedBuffs[b];
                int currentStacks = hero.GetBuffStacks(skill.Id, grantedBuff.Type);
                if (currentStacks < grantedBuff.MaxStacks)
                    hero.AddBattleBuff(new BattleBuff(grantedBuff.Type, grantedBuff.Magnitude, grantedBuff.DurationTurns, skill.Id));
            }

            Assert.IsTrue(hero.GetBuffTotal(BuffType.EvasionUp) > 0, "After first cast, EvasionUp should be active");
            Assert.IsTrue(hero.GetBuffTotal(BuffType.MPRegen) > 0, "After first cast, MPRegen should be active");

            // Second cast: both types are at cap (maxStacks=1) — neither should be applied again
            int evaUpBefore = hero.GetBuffTotal(BuffType.EvasionUp);
            int mpRegenBefore = hero.GetBuffTotal(BuffType.MPRegen);
            for (int b = 0; b < skill.GrantedBuffs.Count; b++)
            {
                var grantedBuff = skill.GrantedBuffs[b];
                int currentStacks = hero.GetBuffStacks(skill.Id, grantedBuff.Type);
                if (currentStacks < grantedBuff.MaxStacks)
                    hero.AddBattleBuff(new BattleBuff(grantedBuff.Type, grantedBuff.Magnitude, grantedBuff.DurationTurns, skill.Id));
            }

            Assert.AreEqual(evaUpBefore, hero.GetBuffTotal(BuffType.EvasionUp), "Second cast should not add another EvasionUp stack (already capped)");
            Assert.AreEqual(mpRegenBefore, hero.GetBuffTotal(BuffType.MPRegen), "Second cast should not add another MPRegen stack (already capped)");
        }

        [TestMethod]
        [TestCategory("Buffs")]
        public void DefenseUpSkill_SingleCastCapsUntilExpiry()
        {
            var hero = MakeHero();
            var skill = new RolePlayingFramework.Skills.DefenseUpSkill();

            // One cast grants the full +3 for 3 turns (single stack)
            ApplyGrantedBuffsRespectingCap(hero, skill);
            Assert.AreEqual(1, hero.GetBuffStacks(skill.Id, BuffType.DefenseUp),
                "DefenseUp should be a single stack per target");
            Assert.AreEqual(3, hero.GetBuffTotal(BuffType.DefenseUp),
                "A single DefenseUp cast should grant +3 defense");

            // Second cast while active: at cap — should not add more
            ApplyGrantedBuffsRespectingCap(hero, skill);
            Assert.AreEqual(1, hero.GetBuffStacks(skill.Id, BuffType.DefenseUp),
                "Re-cast while active should not exceed the single-stack cap");
            Assert.AreEqual(3, hero.GetBuffTotal(BuffType.DefenseUp));

            // After 3 duration ticks the buff expires and can be re-cast
            hero.TickBuffDurations();
            hero.TickBuffDurations();
            hero.TickBuffDurations();
            Assert.AreEqual(0, hero.GetBuffStacks(skill.Id, BuffType.DefenseUp),
                "DefenseUp should expire after 3 turns");

            ApplyGrantedBuffsRespectingCap(hero, skill);
            Assert.AreEqual(1, hero.GetBuffStacks(skill.Id, BuffType.DefenseUp),
                "DefenseUp should be re-castable after expiry");
        }

        // Replicates the at-cap guard used by ApplyHealingSkillEffectsAndDisplay
        private static void ApplyGrantedBuffsRespectingCap(Hero hero, RolePlayingFramework.Skills.ISkill skill)
        {
            for (int b = 0; b < skill.GrantedBuffs.Count; b++)
            {
                var grantedBuff = skill.GrantedBuffs[b];
                int currentStacks = hero.GetBuffStacks(skill.Id, grantedBuff.Type);
                if (currentStacks < grantedBuff.MaxStacks)
                    hero.AddBattleBuff(new BattleBuff(grantedBuff.Type, grantedBuff.Magnitude, grantedBuff.DurationTurns, skill.Id));
            }
        }

        [TestMethod]
        [TestCategory("Buffs")]
        public void Hero_GetBuffTotal_UnrelatedTypeReturnsZero()
        {
            var hero = MakeHero();
            hero.AddBattleBuff(new BattleBuff(BuffType.DefenseUp, magnitude: 5, remainingTurns: -1, sourceSkillId: "priest.defup"));

            Assert.AreEqual(0, hero.GetBuffTotal(BuffType.EvasionUp),
                "GetBuffTotal for an unrelated buff type should return 0");
        }

        // ── Duration ticking ─────────────────────────────────────────────────────────

        [TestMethod]
        [TestCategory("Buffs")]
        public void Hero_TickBuffDurations_FiniteDurationDecrements()
        {
            var hero = MakeHero();
            hero.AddBattleBuff(new BattleBuff(BuffType.EvasionUp, magnitude: 30, remainingTurns: 3, sourceSkillId: "thief.fade"));

            hero.TickBuffDurations();

            // Buff should still be active (2 turns left), just not yet at GetBuffTotal 0
            Assert.AreEqual(30, hero.GetBuffTotal(BuffType.EvasionUp),
                "After 1 tick, a 3-turn buff should still be active");
        }

        [TestMethod]
        [TestCategory("Buffs")]
        public void Hero_TickBuffDurations_BuffExpiresWhenTurnsReachZero()
        {
            var hero = MakeHero();
            hero.AddBattleBuff(new BattleBuff(BuffType.EvasionUp, magnitude: 30, remainingTurns: 1, sourceSkillId: "thief.fade"));

            hero.TickBuffDurations();

            Assert.AreEqual(0, hero.GetBuffTotal(BuffType.EvasionUp),
                "A 1-turn buff should expire after one tick");
        }

        [TestMethod]
        [TestCategory("Buffs")]
        public void Hero_TickBuffDurations_PermanentBuffNeverExpires()
        {
            var hero = MakeHero();
            // -1 = permanent until battle end
            hero.AddBattleBuff(new BattleBuff(BuffType.DefenseUp, magnitude: 1, remainingTurns: -1, sourceSkillId: "priest.defup"));

            hero.TickBuffDurations();
            hero.TickBuffDurations();
            hero.TickBuffDurations();

            Assert.AreEqual(1, hero.GetBuffTotal(BuffType.DefenseUp),
                "A battle-end buff (RemainingTurns=-1) should never be decremented by TickBuffDurations");
        }

        // ── ClearBattleState ─────────────────────────────────────────────────────────

        [TestMethod]
        [TestCategory("Buffs")]
        public void Hero_ClearBattleState_RemovesAllBuffs()
        {
            var hero = MakeHero();
            hero.AddBattleBuff(new BattleBuff(BuffType.DefenseUp, magnitude: 5, remainingTurns: -1, sourceSkillId: "priest.defup"));
            hero.AddBattleBuff(new BattleBuff(BuffType.EvasionUp, magnitude: 40, remainingTurns: 3, sourceSkillId: "smoke_bomb"));

            hero.ClearBattleState();

            Assert.AreEqual(0, hero.GetBuffTotal(BuffType.DefenseUp), "ClearBattleState should remove DefenseUp buffs");
            Assert.AreEqual(0, hero.GetBuffTotal(BuffType.EvasionUp), "ClearBattleState should remove EvasionUp buffs");
        }

        [TestMethod]
        [TestCategory("Buffs")]
        public void Mercenary_ClearBattleState_RemovesAllBuffs()
        {
            var merc = MakeMerc();
            merc.AddBattleBuff(new BattleBuff(BuffType.DefenseUp, magnitude: 3, remainingTurns: -1, sourceSkillId: "knight.ki_cloak"));
            merc.ClearBattleState();

            Assert.AreEqual(0, merc.GetBuffTotal(BuffType.DefenseUp), "Mercenary ClearBattleState should remove all buffs");
        }

        // ── GetBattleStats with buffs ─────────────────────────────────────────────────

        [TestMethod]
        [TestCategory("Buffs")]
        public void Hero_DefenseUpBuff_RaisesGetBattleStatsDefense()
        {
            var hero = MakeHero(agi: 10);
            var baselineDefense = hero.GetBattleStats().Defense;

            hero.AddBattleBuff(new BattleBuff(BuffType.DefenseUp, magnitude: 5, remainingTurns: -1, sourceSkillId: "priest.defup"));

            var buffedDefense = hero.GetBattleStats().Defense;
            Assert.AreEqual(baselineDefense + 5, buffedDefense,
                "DefenseUp buff of +5 should raise GetBattleStats().Defense by exactly 5");
        }

        [TestMethod]
        [TestCategory("Buffs")]
        public void Mercenary_DefenseUpBuff_RaisesGetBattleStatsDefense()
        {
            var merc = MakeMerc(agi: 10);
            var baselineDefense = merc.GetBattleStats().Defense;

            merc.AddBattleBuff(new BattleBuff(BuffType.DefenseUp, magnitude: 3, remainingTurns: -1, sourceSkillId: "knight.ki_cloak"));

            var buffedDefense = merc.GetBattleStats().Defense;
            Assert.AreEqual(baselineDefense + 3, buffedDefense,
                "Mercenary DefenseUp buff of +3 should raise GetBattleStats().Defense by exactly 3");
        }

        [TestMethod]
        [TestCategory("Buffs")]
        public void Hero_EvasionUpBuff_RaisesGetBattleStatsEvasion()
        {
            var hero = MakeHero(agi: 10);
            var baselineEvasion = hero.GetBattleStats().Evasion;

            hero.AddBattleBuff(new BattleBuff(BuffType.EvasionUp, magnitude: 40, remainingTurns: 3, sourceSkillId: "thief.smoke_bomb"));

            var buffedEvasion = hero.GetBattleStats().Evasion;
            Assert.AreEqual(System.Math.Min(255, baselineEvasion + 40), buffedEvasion,
                "EvasionUp buff of +40 should raise GetBattleStats().Evasion by 40 (capped at 255)");
        }

        [TestMethod]
        [TestCategory("Buffs")]
        public void Hero_ClearBattleState_RestoresBaselineStats()
        {
            var hero = MakeHero(agi: 10);
            var baselineDefense = hero.GetBattleStats().Defense;

            hero.AddBattleBuff(new BattleBuff(BuffType.DefenseUp, magnitude: 10, remainingTurns: -1, sourceSkillId: "priest.defup"));
            hero.ClearBattleState();

            Assert.AreEqual(baselineDefense, hero.GetBattleStats().Defense,
                "After ClearBattleState, GetBattleStats().Defense should return to baseline");
        }

        // ── MPRegen buff + TickRegeneration ──────────────────────────────────────────

        [TestMethod]
        [TestCategory("Buffs")]
        public void Hero_MPRegenBuff_StacksWithMPTickRegen()
        {
            var hero = MakeHero();
            // Drain some MP to give the regen room to work
            hero.SpendMP(10);
            int mpAfterSpend = hero.CurrentMP;

            // Base MPTickRegen is 0; add +2 via buff
            hero.AddBattleBuff(new BattleBuff(BuffType.MPRegen, magnitude: 2, remainingTurns: -1, sourceSkillId: "thief.fade"));
            hero.TickRegeneration();

            Assert.AreEqual(mpAfterSpend + 2, hero.CurrentMP,
                "MPRegen buff of +2 should restore 2 MP per TickRegeneration when base MPTickRegen is 0");
        }
    }
}
