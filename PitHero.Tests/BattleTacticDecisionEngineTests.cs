using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.AI;
using PitHero.Combat;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Inventory;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Mercenaries;
using RolePlayingFramework.Skills;
using RolePlayingFramework.Stats;
using System.Collections.Generic;

namespace PitHero.Tests
{
    /// <summary>Party view with directly controllable tactic and critical-HP flags.</summary>
    internal sealed class TacticTestPartyView : IBattlePartyView
    {
        public Hero Hero { get; }
        public BattleTactic CurrentBattleTactic { get; set; }
        public ItemBag Bag { get; }
        public bool HealingItemExhausted { get; set; }
        public bool HealingSkillExhausted { get; set; }
        public bool UseConsumablesOnMercenaries => false;
        public bool MercenariesCanUseConsumables => false;

        /// <summary>Forces IsHeroHPCritical to return this value (burst-flag stand-in).</summary>
        public bool ForceHeroCritical { get; set; }

        /// <summary>Forces IsMercenaryHPCritical to return this value for every merc.</summary>
        public bool ForceMercCritical { get; set; }

        public TacticTestPartyView(Hero hero, BattleTactic tactic)
        {
            Hero = hero;
            CurrentBattleTactic = tactic;
            Bag = new ItemBag("TestBag", 10);
        }

        public HeroHealPriority[] GetHealPrioritiesInOrder()
            => new[] { HeroHealPriority.HealingSkill, HeroHealPriority.HealingItem, HeroHealPriority.Inn };

        public bool IsHeroHPCritical() => ForceHeroCritical;

        public bool IsMercenaryHPCritical(Mercenary merc) => ForceMercCritical;

        public void RegisterHeroBurstDamage(int damage) { }
        public void RegisterMercenaryBurstDamage(Mercenary merc, int damage) { }
    }

    /// <summary>
    /// Direct unit tests for BattleTacticDecisionEngine buff selection (issue #294):
    /// Strategic casts a defensive self-buff when the caster's critical flag is set,
    /// Blitz casts one self-buff opener in round 1 only, Defensive behavior unchanged.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class BattleTacticDecisionEngineTests
    {
        private static readonly List<Mercenary> NoMercs = new List<Mercenary>();

        /// <summary>Hero with a bound crystal so skills can be purchased (JP-only check).</summary>
        private static Hero MakeHeroWithSkills(params ISkill[] skills)
        {
            var crystal = new HeroCrystal("TestCrystal", new Thief(), 10, new StatBlock(10, 10, 10, 8));
            crystal.EarnJP(1_000_000);
            var hero = new Hero("Hero", new Thief(), 10, new StatBlock(10, 10, 10, 8), crystal);
            for (int i = 0; i < skills.Length; i++)
                Assert.IsTrue(hero.TryPurchaseSkill(skills[i]), $"Failed to purchase {skills[i].Id}");
            return hero;
        }

        private static Mercenary MakeMercWithBuffSkill()
        {
            var merc = new Mercenary("TestMerc", new Priest(), 5, new StatBlock(5, 5, 5, 10));
            merc.LearnSkill(new DefenseUpSkill());
            return merc;
        }

        private static List<IEnemy> OneSlime() => new List<IEnemy> { new Slime(5) };

        // ── Strategic: buff on critical ─────────────────────────────────────────

        [TestMethod]
        [TestCategory("TacticDecision")]
        public void Strategic_HeroCritical_NoHealAvailable_CastsBuff()
        {
            var hero = MakeHeroWithSkills(new VanishSkill());
            hero.TakeDamage(hero.MaxHP * 2 / 3); // low HP for realism; flag drives the decision
            var party = new TacticTestPartyView(hero, BattleTactic.Strategic) { ForceHeroCritical = true };

            var action = BattleTacticDecisionEngine.DecideHeroAction(party, OneSlime(), NoMercs, roundNumber: 3, battleCriticalReached: true);

            Assert.AreEqual(BattleAction.ActionKind.UseHealingSkill, action.Kind);
            Assert.AreEqual("thief.vanish", action.Skill.Id);
            Assert.IsTrue(action.TargetsHero, "Self-buff must target the hero");
        }

        [TestMethod]
        [TestCategory("TacticDecision")]
        public void Strategic_HeroHealthy_DoesNotBuff()
        {
            var hero = MakeHeroWithSkills(new VanishSkill());
            var party = new TacticTestPartyView(hero, BattleTactic.Strategic) { ForceHeroCritical = false };

            var action = BattleTacticDecisionEngine.DecideHeroAction(party, OneSlime(), NoMercs, roundNumber: 3, battleCriticalReached: false);

            Assert.AreNotEqual(BattleAction.ActionKind.UseHealingSkill, action.Kind,
                "Healthy hero under Strategic must not spend a turn buffing");
        }

        [TestMethod]
        [TestCategory("TacticDecision")]
        public void Strategic_HeroCritical_HealAvailable_HealWinsOverBuff()
        {
            var hero = MakeHeroWithSkills(new HealSkill(), new VanishSkill());
            hero.TakeDamage(hero.MaxHP * 2 / 3);
            var party = new TacticTestPartyView(hero, BattleTactic.Strategic) { ForceHeroCritical = true };

            var action = BattleTacticDecisionEngine.DecideHeroAction(party, OneSlime(), NoMercs, roundNumber: 3, battleCriticalReached: true);

            Assert.AreEqual(BattleAction.ActionKind.UseHealingSkill, action.Kind);
            Assert.AreEqual("priest.heal", action.Skill.Id, "Heal priority must be preserved over buffing");
        }

        [TestMethod]
        [TestCategory("TacticDecision")]
        public void Strategic_MercCritical_CastsBuffOnNeediestAlly()
        {
            var hero = MakeHeroWithSkills();
            var merc = MakeMercWithBuffSkill();
            merc.TakeDamage(merc.MaxHP * 2 / 3); // merc is the lowest-HP% party member
            var party = new TacticTestPartyView(hero, BattleTactic.Strategic) { ForceMercCritical = true };

            var action = BattleTacticDecisionEngine.DecideMercenaryAction(
                merc, party, OneSlime(), new List<Mercenary> { merc }, roundNumber: 3, battleCriticalReached: true);

            Assert.AreEqual(BattleAction.ActionKind.UseHealingSkill, action.Kind);
            Assert.AreEqual("priest.defup", action.Skill.Id);
            Assert.IsFalse(action.TargetsHero, "Neediest ally is the damaged merc, not the hero");
            Assert.AreSame(merc, action.Target, "Buff must target the lowest-HP% party member");
        }

        [TestMethod]
        [TestCategory("TacticDecision")]
        public void Strategic_MercHealthy_DoesNotBuff()
        {
            var hero = MakeHeroWithSkills();
            var merc = MakeMercWithBuffSkill();
            var party = new TacticTestPartyView(hero, BattleTactic.Strategic) { ForceMercCritical = false };

            var action = BattleTacticDecisionEngine.DecideMercenaryAction(
                merc, party, OneSlime(), new List<Mercenary> { merc }, roundNumber: 3, battleCriticalReached: false);

            Assert.AreNotEqual(BattleAction.ActionKind.UseHealingSkill, action.Kind);
        }

        // ── Blitz: round-1 opener ───────────────────────────────────────────────

        [TestMethod]
        [TestCategory("TacticDecision")]
        public void Blitz_Round1_HeroCastsOpenerBuff()
        {
            var hero = MakeHeroWithSkills(new VanishSkill());
            var party = new TacticTestPartyView(hero, BattleTactic.Blitz);

            var action = BattleTacticDecisionEngine.DecideHeroAction(party, OneSlime(), NoMercs, roundNumber: 1, battleCriticalReached: false);

            Assert.AreEqual(BattleAction.ActionKind.UseHealingSkill, action.Kind);
            Assert.AreEqual("thief.vanish", action.Skill.Id);
            Assert.IsTrue(action.TargetsHero);
        }

        [TestMethod]
        [TestCategory("TacticDecision")]
        public void Blitz_Round2_HeroDoesNotBuff()
        {
            var hero = MakeHeroWithSkills(new VanishSkill());
            var party = new TacticTestPartyView(hero, BattleTactic.Blitz);

            var action = BattleTacticDecisionEngine.DecideHeroAction(party, OneSlime(), NoMercs, roundNumber: 2, battleCriticalReached: false);

            Assert.AreNotEqual(BattleAction.ActionKind.UseHealingSkill, action.Kind,
                "Blitz must be pure aggression after round 1");
        }

        [TestMethod]
        [TestCategory("TacticDecision")]
        public void Blitz_Round1_MercCastsOpenerBuffOnHero()
        {
            // Everyone at full HP: hero wins the tie, so the merc's opener buff lands on the hero
            var hero = MakeHeroWithSkills();
            var merc = MakeMercWithBuffSkill();
            var party = new TacticTestPartyView(hero, BattleTactic.Blitz);

            var action = BattleTacticDecisionEngine.DecideMercenaryAction(
                merc, party, OneSlime(), new List<Mercenary> { merc }, roundNumber: 1, battleCriticalReached: false);

            Assert.AreEqual(BattleAction.ActionKind.UseHealingSkill, action.Kind);
            Assert.AreEqual("priest.defup", action.Skill.Id);
            Assert.IsTrue(action.TargetsHero, "With all members unbuffed at full HP, the hero is buffed first");
            Assert.AreSame(hero, action.Target);
        }

        [TestMethod]
        [TestCategory("TacticDecision")]
        public void Blitz_Round2_MercDoesNotBuff()
        {
            var hero = MakeHeroWithSkills();
            var merc = MakeMercWithBuffSkill();
            var party = new TacticTestPartyView(hero, BattleTactic.Blitz);

            var action = BattleTacticDecisionEngine.DecideMercenaryAction(
                merc, party, OneSlime(), new List<Mercenary> { merc }, roundNumber: 2, battleCriticalReached: false);

            Assert.AreNotEqual(BattleAction.ActionKind.UseHealingSkill, action.Kind);
        }

        [TestMethod]
        [TestCategory("TacticDecision")]
        public void Blitz_Round1_InsufficientMP_FallsThroughToAttack()
        {
            var hero = MakeHeroWithSkills(new VanishSkill());
            hero.SpendMP(hero.CurrentMP - 5); // Vanish costs 6 MP
            var party = new TacticTestPartyView(hero, BattleTactic.Blitz);

            var action = BattleTacticDecisionEngine.DecideHeroAction(party, OneSlime(), NoMercs, roundNumber: 1, battleCriticalReached: false);

            Assert.AreNotEqual(BattleAction.ActionKind.UseHealingSkill, action.Kind,
                "Opener buff must respect the MP gate");
        }

        // ── MaxStacks at-cap skip ───────────────────────────────────────────────

        [TestMethod]
        [TestCategory("TacticDecision")]
        public void Strategic_HeroCritical_BuffAtCap_FallsThroughToAttack()
        {
            var hero = MakeHeroWithSkills(new VanishSkill());
            hero.TakeDamage(hero.MaxHP * 2 / 3);
            hero.AddBattleBuff(new BattleBuff(BuffType.Untargetable, 1, 2, "thief.vanish"));
            var party = new TacticTestPartyView(hero, BattleTactic.Strategic) { ForceHeroCritical = true };

            var action = BattleTacticDecisionEngine.DecideHeroAction(party, OneSlime(), NoMercs, roundNumber: 3, battleCriticalReached: true);

            Assert.AreNotEqual(BattleAction.ActionKind.UseHealingSkill, action.Kind,
                "A buff already at MaxStacks must be skipped");
        }

        // ── Strategic: reactive buffing via the battle-critical latch ───────────

        [TestMethod]
        [TestCategory("TacticDecision")]
        public void Strategic_FlagLatched_HealthyMercPriest_ReactivelyBuffsParty()
        {
            // Someone hit critical earlier this battle (flag latched), was healed, and is
            // no longer critical: the priest now spends free turns buffing reactively.
            var hero = MakeHeroWithSkills();
            var merc = MakeMercWithBuffSkill();
            var party = new TacticTestPartyView(hero, BattleTactic.Strategic)
            { ForceHeroCritical = false, ForceMercCritical = false };

            var action = BattleTacticDecisionEngine.DecideMercenaryAction(
                merc, party, OneSlime(), new List<Mercenary> { merc }, roundNumber: 4, battleCriticalReached: true);

            Assert.AreEqual(BattleAction.ActionKind.UseHealingSkill, action.Kind,
                "Once the battle-critical flag is latched, ally buffs fire on free turns");
            Assert.AreEqual("priest.defup", action.Skill.Id);
            Assert.AreSame(hero, action.Target, "Everyone healthy and unbuffed: the hero is buffed first");
        }

        [TestMethod]
        [TestCategory("TacticDecision")]
        public void Strategic_FlagLatched_SelfBuffOnly_HealthyHero_DoesNotBuff()
        {
            // Vanish is strictly a self-buff: it stays gated on the caster's OWN critical
            // state even after the battle-critical flag is latched.
            var hero = MakeHeroWithSkills(new VanishSkill());
            var party = new TacticTestPartyView(hero, BattleTactic.Strategic) { ForceHeroCritical = false };

            var action = BattleTacticDecisionEngine.DecideHeroAction(party, OneSlime(), NoMercs, roundNumber: 4, battleCriticalReached: true);

            Assert.AreNotEqual(BattleAction.ActionKind.UseHealingSkill, action.Kind,
                "A healthy caster must not Vanish just because the battle got dangerous earlier");
        }

        // ── Ally-buff spread (multi-turn buffs, one cast per target) ────────────

        [TestMethod]
        [TestCategory("TacticDecision")]
        public void Defensive_MercPriest_SpreadsDefUpAcrossPartyThenAttacks()
        {
            var hero = MakeHeroWithSkills();
            var merc = MakeMercWithBuffSkill();
            var party = new TacticTestPartyView(hero, BattleTactic.Defensive);
            var monsters = OneSlime();
            var mercs = new List<Mercenary> { merc };

            // Turn 1: everyone unbuffed at full HP — buff the hero first
            var first = BattleTacticDecisionEngine.DecideMercenaryAction(merc, party, monsters, mercs, roundNumber: 1, battleCriticalReached: false);
            Assert.AreEqual(BattleAction.ActionKind.UseHealingSkill, first.Kind);
            Assert.AreSame(hero, first.Target, "First cast should go to the hero");
            hero.AddBattleBuff(new BattleBuff(BuffType.DefenseUp, 3, 3, "priest.defup"));

            // Turn 2: hero already buffed — next unbuffed member (the priest himself)
            var second = BattleTacticDecisionEngine.DecideMercenaryAction(merc, party, monsters, mercs, roundNumber: 2, battleCriticalReached: false);
            Assert.AreEqual(BattleAction.ActionKind.UseHealingSkill, second.Kind);
            Assert.AreSame(merc, second.Target, "Second cast should go to the next unbuffed member");
            merc.AddBattleBuff(new BattleBuff(BuffType.DefenseUp, 3, 3, "priest.defup"));

            // Turn 3: whole party buffed — no re-cast, fall through to attack
            var third = BattleTacticDecisionEngine.DecideMercenaryAction(merc, party, monsters, mercs, roundNumber: 3, battleCriticalReached: false);
            Assert.AreNotEqual(BattleAction.ActionKind.UseHealingSkill, third.Kind,
                "With every member buffed, the priest should attack instead of re-casting");
        }

        // ── Defensive: regression guard ─────────────────────────────────────────

        [TestMethod]
        [TestCategory("TacticDecision")]
        public void Defensive_HealthyHero_StillCastsBuff()
        {
            var hero = MakeHeroWithSkills(new VanishSkill());
            var party = new TacticTestPartyView(hero, BattleTactic.Defensive);

            var action = BattleTacticDecisionEngine.DecideHeroAction(party, OneSlime(), NoMercs, roundNumber: 3, battleCriticalReached: false);

            Assert.AreEqual(BattleAction.ActionKind.UseHealingSkill, action.Kind);
            Assert.AreEqual("thief.vanish", action.Skill.Id);
        }
    }
}
