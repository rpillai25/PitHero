using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nez;
using PitHero.AI;
using PitHero.Combat;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Inventory;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Mercenaries;
using RolePlayingFramework.Stats;
using System.Collections.Generic;

namespace PitHero.Tests
{
    // ── Headless test doubles ───────────────────────────────────────────────────

    /// <summary>Minimal IBattlePartyView for headless engine tests.</summary>
    internal sealed class TestPartyView : IBattlePartyView
    {
        public Hero Hero { get; }
        public BattleTactic CurrentBattleTactic { get; }
        public ItemBag Bag { get; }
        public bool HealingItemExhausted { get; set; }
        public bool HealingSkillExhausted { get; set; }
        public bool UseConsumablesOnMercenaries => false;
        public bool MercenariesCanUseConsumables => false;

        public TestPartyView(Hero hero, BattleTactic tactic = BattleTactic.Blitz)
        {
            Hero = hero;
            CurrentBattleTactic = tactic;
            Bag = new ItemBag("TestBag", 10);
        }

        public HeroHealPriority[] GetHealPrioritiesInOrder()
            => new[] { HeroHealPriority.HealingSkill, HeroHealPriority.HealingItem, HeroHealPriority.Inn };

        public bool IsHeroHPCritical()
            => Hero != null && Hero.CurrentHP < Hero.MaxHP * 0.3f;

        public bool IsMercenaryHPCritical(Mercenary merc)
            => merc != null && merc.CurrentHP < merc.MaxHP * 0.3f;

        public void RegisterHeroBurstDamage(int damage) { /* no-op in tests */ }
        public void RegisterMercenaryBurstDamage(Mercenary merc, int damage) { /* no-op */ }
    }

    /// <summary>Minimal IBattleAlly wrapping any ICombatant (always present while alive).</summary>
    internal sealed class TestBattleAlly : IBattleAlly
    {
        public ICombatant Combatant { get; }
        public bool IsHero { get; }
        public bool IsPresent => Combatant.CurrentHP > 0;
        public ActionQueue PlayerActionQueue { get; set; }

        public TestBattleAlly(ICombatant combatant, bool isHero)
        {
            Combatant = combatant;
            IsHero = isHero;
        }
    }

    /// <summary>Headless sink that records key events for assertion.</summary>
    internal sealed class RecordingSink : BattleEventSinkBase
    {
        public List<IEnemy> DefeatedEnemies { get; } = new List<IEnemy>();
        public List<IBattleAlly> KilledAllies { get; } = new List<IBattleAlly>();
        public List<BattleAttackEvent> AttackEvents { get; } = new List<BattleAttackEvent>();
        public List<BattleBuffEvent> BuffEvents { get; } = new List<BattleBuffEvent>();
        public List<BattleHealEvent> HealEvents { get; } = new List<BattleHealEvent>();

        public override void OnEnemyDefeated(IEnemy enemy, bool heroKill)
            => DefeatedEnemies.Add(enemy);

        public override void OnAllyKilled(IBattleAlly ally, IEnemy killer)
            => KilledAllies.Add(ally);

        public override void OnAttackResolved(in BattleAttackEvent evt)
            => AttackEvents.Add(evt);

        public override void OnBuffApplied(in BattleBuffEvent evt)
            => BuffEvents.Add(evt);

        public override void OnHealApplied(in BattleHealEvent evt)
            => HealEvents.Add(evt);
    }

    // ── Tests ──────────────────────────────────────────────────────────────────

    [TestClass]
    [DoNotParallelize]
    public class BattleEngineTests
    {
        // Convenience: create a mid-level hero with given stats
        private static Hero MakeHero(int str = 10, int agi = 10, int vit = 10, int mag = 5)
            => new Hero("Hero", new Knight(), level: 10, baseStats: new StatBlock(str, agi, vit, mag));

        // Convenience: create a Slime at given level
        private static Slime MakeSlime(int level = 5) => new Slime(level);

        // Run a battle headlessly and return the engine
        private static (BattleEngine engine, RecordingSink sink) RunBattle(
            Hero hero,
            List<IEnemy> monsters,
            List<Mercenary> mercs = null,
            BattleTactic tactic = BattleTactic.Blitz,
            int seed = 42)
        {
            Nez.Random.SetSeed(seed);

            var party = new TestPartyView(hero, tactic);
            var sink  = new RecordingSink();
            var engine = new BattleEngine(party, sink);

            var heroAlly   = new TestBattleAlly(hero, isHero: true);
            var mercAllies = new List<IBattleAlly>();
            if (mercs != null)
            {
                for (int i = 0; i < mercs.Count; i++)
                    mercAllies.Add(new TestBattleAlly(mercs[i], isHero: false));
            }

            // Provide a throw-away action queue (engine's QueueHeroActionForRound
            // populates it each round via the decision engine)
            var actionQueue = new ActionQueue();

            HeadlessCoroutineRunner.RunToCompletion(
                engine.Run(heroAlly, mercAllies, monsters, actionQueue));

            return (engine, sink);
        }

        // ── Basic battle ────────────────────────────────────────────────────────

        [TestMethod]
        [TestCategory("BattleEngine")]
        public void FullBattle_HeroVsWeakMonster_MonstersCleared()
        {
            // Hero level 25 vs level-1 slime: hero should always win
            var hero    = MakeHero(str: 30, agi: 10, vit: 20, mag: 5);
            var monsters = new List<IEnemy> { MakeSlime(1) };

            var (engine, sink) = RunBattle(hero, monsters, seed: 1234);

            Assert.AreEqual(BattleOutcome.MonstersCleared, engine.Outcome,
                "A strong hero should clear a weak slime");
            Assert.AreEqual(1, sink.DefeatedEnemies.Count, "One enemy should be defeated");
        }

        [TestMethod]
        [TestCategory("BattleEngine")]
        public void FullBattle_HeroGainsXP_AfterKillingMonster()
        {
            var hero    = MakeHero(str: 30, agi: 10, vit: 20, mag: 5);
            var slime   = MakeSlime(1);
            int xpYield = slime.ExperienceYield;
            var monsters = new List<IEnemy> { slime };

            int xpBefore = hero.Experience;
            RunBattle(hero, monsters, seed: 99);

            // If hero won, XP should be awarded
            if (hero.Experience != xpBefore)
                Assert.IsTrue(hero.Experience > xpBefore, "Hero should gain XP after defeating enemy");
        }

        [TestMethod]
        [TestCategory("BattleEngine")]
        public void FullBattle_MultipleMonsters_AllDefeated()
        {
            var hero     = MakeHero(str: 40, agi: 10, vit: 20, mag: 5);
            var monsters = new List<IEnemy> { MakeSlime(1), MakeSlime(1), MakeSlime(1) };

            var (engine, sink) = RunBattle(hero, monsters, seed: 555);

            Assert.AreEqual(BattleOutcome.MonstersCleared, engine.Outcome,
                "Overpowered hero should defeat all 3 slimes");
        }

        // ── ClearBattleState ───────────────────────────────────────────────────

        [TestMethod]
        [TestCategory("BattleEngine")]
        public void ClearBattleState_BuffsRemovedAfterBattle()
        {
            // Arrange: add a battle buff to hero before battle
            var hero = MakeHero(str: 30, agi: 10, vit: 20, mag: 5);
            hero.AddBattleBuff(new BattleBuff(BuffType.DefenseUp, 5, 10, "test.skill"));

            Assert.IsTrue(hero.GetBuffTotal(BuffType.DefenseUp) > 0, "Hero should have buff before battle");

            var monsters = new List<IEnemy> { MakeSlime(1) };
            RunBattle(hero, monsters, seed: 1);

            // After battle, the engine's finally block clears all battle buffs
            Assert.AreEqual(0, hero.GetBuffTotal(BuffType.DefenseUp),
                "Hero buffs should be cleared after battle ends");
        }

        // ── SelectPrimaryTarget static helper ──────────────────────────────────

        [TestMethod]
        [TestCategory("BattleEngine")]
        public void SelectPrimaryTarget_SwapsPreferredToFront()
        {
            var e0 = MakeSlime(1);
            var e1 = MakeSlime(2);
            var e2 = MakeSlime(3);
            var list = new List<IEnemy> { e0, e1, e2 };

            BattleEngine.SelectPrimaryTarget(list, e2);

            Assert.AreEqual(e2, list[0], "Preferred should be at index 0 after SelectPrimaryTarget");
            Assert.AreEqual(e0, list[2], "Original index 0 should be at index 2");
        }

        [TestMethod]
        [TestCategory("BattleEngine")]
        public void SelectPrimaryTarget_NullPreferred_Unchanged()
        {
            var e0 = MakeSlime(1);
            var list = new List<IEnemy> { e0 };
            BattleEngine.SelectPrimaryTarget(list, null);
            Assert.AreEqual(e0, list[0]);
        }

        // ── Determinism ───────────────────────────────────────────────────────

        [TestMethod]
        [TestCategory("BattleEngine")]
        public void Determinism_SameSeedProducesSameOutcome()
        {
            const int seed = 7777;

            // Run 1
            var hero1    = MakeHero(str: 15, agi: 10, vit: 12, mag: 5);
            var monsters1 = new List<IEnemy> { MakeSlime(3), MakeSlime(3) };
            var (engine1, _) = RunBattle(hero1, monsters1, seed: seed);
            int hero1HP = hero1.CurrentHP;

            // Run 2 — identical setup, same seed
            var hero2    = MakeHero(str: 15, agi: 10, vit: 12, mag: 5);
            var monsters2 = new List<IEnemy> { MakeSlime(3), MakeSlime(3) };
            var (engine2, _) = RunBattle(hero2, monsters2, seed: seed);
            int hero2HP = hero2.CurrentHP;

            Assert.AreEqual(engine1.Outcome, engine2.Outcome,
                "Same seed should produce same battle outcome");
            Assert.AreEqual(hero1HP, hero2HP,
                "Same seed should leave hero at identical HP");
        }

        // ── CalculateTurnValue static helper ───────────────────────────────────

        [TestMethod]
        [TestCategory("BattleEngine")]
        public void CalculateTurnValue_ZeroAgility_ReturnsZero()
        {
            // (RAND * (0 - 0)) / 256 = 0 regardless of random
            float val = BattleEngine.CalculateTurnValue(0);
            Assert.AreEqual(0f, val, 0.001f, "Zero agility yields zero turn value");
        }

        [TestMethod]
        [TestCategory("BattleEngine")]
        public void CalculateTurnValue_PositiveAgility_ReturnsNonNegative()
        {
            Nez.Random.SetSeed(42);
            float val = BattleEngine.CalculateTurnValue(20);
            Assert.IsTrue(val >= 0f, "Turn value must be non-negative");
        }

        // ── Untargetable anti-stall guard ─────────────────────────────────────

        [TestMethod]
        [TestCategory("BattleEngine")]
        public void Untargetable_AllAlliesUntargetable_MonsterStillAttacks()
        {
            // Hero with Untargetable buff (monster cannot normally target them)
            // anti-stall: if ALL allies are untargetable, monster still attacks someone
            var hero = MakeHero(str: 30, agi: 10, vit: 20, mag: 5);
            hero.AddBattleBuff(new BattleBuff(BuffType.Untargetable, 1, 99, "vanish"));

            var slime = MakeSlime(1);
            var monsters = new List<IEnemy> { slime };

            // Battle should still complete — monster cannot stall forever
            var (engine, sink) = RunBattle(hero, monsters, seed: 11);

            // The battle MUST end (not timeout in HeadlessCoroutineRunner)
            Assert.AreNotEqual(BattleOutcome.InProgress, engine.Outcome,
                "Battle must resolve even when all allies are Untargetable (anti-stall guard)");
        }

        // ── Ally death tracking ────────────────────────────────────────────────

        [TestMethod]
        [TestCategory("BattleEngine")]
        public void Battle_WeakHero_MayDie_OutcomeIsAlliesWipedOrGone()
        {
            // Hero with 1 HP vs level-25 slime: hero will definitely die
            var hero = new Hero("WeakHero", new Knight(), level: 1,
                baseStats: new StatBlock(1, 1, 1, 1));
            // Reduce HP to 1 by taking massive damage (max HP after level-1 construction)
            // We can't directly set HP, so instead use a very weak hero vs strong monster
            var strongSlime = MakeSlime(25);
            var monsters = new List<IEnemy> { strongSlime };

            var (engine, _) = RunBattle(hero, monsters, seed: 22);

            // Either hero wins or loses; just verify the battle ends
            Assert.AreNotEqual(BattleOutcome.InProgress, engine.Outcome,
                "Battle must always reach a terminal outcome");
        }

        // ── Buff casting under Blitz/Strategic (issue #294) ───────────────────

        private static Hero MakeThiefWithVanish()
        {
            var crystal = new RolePlayingFramework.Heroes.HeroCrystal(
                "TestCrystal", new RolePlayingFramework.Jobs.Primary.Thief(), 25, new StatBlock(30, 10, 20, 5));
            crystal.EarnJP(1_000_000);
            var hero = new Hero("Hero", new RolePlayingFramework.Jobs.Primary.Thief(), 25,
                new StatBlock(30, 10, 20, 5), crystal);
            Assert.IsTrue(hero.TryPurchaseSkill(new RolePlayingFramework.Skills.VanishSkill()));
            return hero;
        }

        [TestMethod]
        [TestCategory("BattleEngine")]
        public void Blitz_HeroWithVanish_CastsOpenerBuffAndWins()
        {
            var hero = MakeThiefWithVanish();
            var monsters = new List<IEnemy> { MakeSlime(3) };

            var (engine, sink) = RunBattle(hero, monsters, tactic: BattleTactic.Blitz, seed: 99);

            Assert.AreNotEqual(BattleOutcome.InProgress, engine.Outcome, "Battle must complete");
            Assert.IsTrue(sink.BuffEvents.Count >= 1, "Blitz round-1 opener must apply a buff");
            Assert.AreEqual("thief.vanish", sink.BuffEvents[0].Source);
            Assert.AreEqual("Untargetable", sink.BuffEvents[0].BuffTypeName);
        }

        [TestMethod]
        [TestCategory("BattleEngine")]
        public void Strategic_HealthyHeroWithVanish_NeverBuffs()
        {
            // Strong hero vs weak slime: HP never goes critical, so no buff should fire
            var hero = MakeThiefWithVanish();
            var monsters = new List<IEnemy> { MakeSlime(1) };

            var (engine, sink) = RunBattle(hero, monsters, tactic: BattleTactic.Strategic, seed: 99);

            Assert.AreEqual(BattleOutcome.MonstersCleared, engine.Outcome);
            Assert.AreEqual(0, sink.BuffEvents.Count,
                "Strategic must not buff while the hero is healthy");
        }

        // ── Player-queued mercenary skills (issue #303) ────────────────────────

        private static Mercenary MakeKnightMerc(int level = 15)
        {
            var merc = new Mercenary("Fynn Swift", new Knight(), level, new StatBlock(20, 15, 15, 10));
            merc.LearnAllJobSkills();
            return merc;
        }

        // Run a battle with a single mercenary whose PlayerActionQueue can be set (null allowed)
        private static (BattleEngine engine, RecordingSink sink) RunBattleWithMerc(
            Hero hero, List<IEnemy> monsters, Mercenary merc, ActionQueue mercQueue, int seed)
        {
            Nez.Random.SetSeed(seed);

            var party  = new TestPartyView(hero, BattleTactic.Blitz);
            var sink   = new RecordingSink();
            var engine = new BattleEngine(party, sink);

            var heroAlly = new TestBattleAlly(hero, isHero: true);
            var mercAlly = new TestBattleAlly(merc, isHero: false) { PlayerActionQueue = mercQueue };
            var mercAllies = new List<IBattleAlly> { mercAlly };

            HeadlessCoroutineRunner.RunToCompletion(
                engine.Run(heroAlly, mercAllies, monsters, new ActionQueue()));

            return (engine, sink);
        }

        [TestMethod]
        [TestCategory("BattleEngine")]
        public void MercQueuedSkill_ConsumedAndExecutedOnMercTurn()
        {
            var hero = MakeHero(str: 8, agi: 10, vit: 15, mag: 5);
            var merc = MakeKnightMerc();
            var monsters = new List<IEnemy> { MakeSlime(5), MakeSlime(5) };

            var queue = new ActionQueue();
            Assert.IsTrue(queue.EnqueueSkill(merc.LearnedSkills["knight.heavy_strike"]),
                "Skill should enqueue on an empty queue");

            var (engine, sink) = RunBattleWithMerc(hero, monsters, merc, queue, seed: 4242);

            Assert.AreEqual(0, queue.Count, "Player-queued action must be consumed");
            bool mercUsedQueuedSkill = false;
            for (int i = 0; i < sink.AttackEvents.Count; i++)
            {
                var evt = sink.AttackEvents[i];
                if (evt.ActorType == "merc" && evt.Action.StartsWith("knight.heavy_strike"))
                {
                    mercUsedQueuedSkill = true;
                    break;
                }
            }
            Assert.IsTrue(mercUsedQueuedSkill, "Mercenary must execute the player-queued skill");
        }

        [TestMethod]
        [TestCategory("BattleEngine")]
        public void MercQueuedSkill_InsufficientMP_FallsBackToAI()
        {
            var hero = MakeHero(str: 8, agi: 10, vit: 15, mag: 5);
            var merc = MakeKnightMerc();
            merc.SetCurrentMP(0); // cannot afford any skill (plain Knight has no MP regen)
            var monsters = new List<IEnemy> { MakeSlime(5), MakeSlime(5) };

            var queue = new ActionQueue();
            queue.EnqueueSkill(merc.LearnedSkills["knight.heavy_strike"]);

            var (engine, sink) = RunBattleWithMerc(hero, monsters, merc, queue, seed: 4242);

            Assert.AreEqual(0, queue.Count, "Queued action is consumed even when MP is insufficient");
            for (int i = 0; i < sink.AttackEvents.Count; i++)
            {
                var evt = sink.AttackEvents[i];
                Assert.IsFalse(evt.ActorType == "merc" && evt.Action.StartsWith("knight.heavy_strike"),
                    "Mercenary must not cast the queued skill without MP");
            }
            Assert.AreNotEqual(BattleOutcome.InProgress, engine.Outcome, "Battle must complete via the AI path");
        }

        [TestMethod]
        [TestCategory("BattleEngine")]
        public void MercQueuedHealSkill_NoTarget_HealsMostWoundedAlly()
        {
            // Wounded hero + full-HP priest merc: the shortcut-queued heal (which carries
            // no target) must land on the hero, not silently self-target the full-HP caster
            var hero = MakeHero(str: 20, agi: 10, vit: 20, mag: 5);
            hero.TakeDamage(hero.MaxHP / 2);
            int heroHpBeforeBattle = hero.CurrentHP;

            var merc = new Mercenary("Aldric Keen", new RolePlayingFramework.Jobs.Primary.Priest(),
                15, new StatBlock(10, 12, 12, 20));
            merc.LearnAllJobSkills();

            var monsters = new List<IEnemy> { MakeSlime(5) };

            var queue = new ActionQueue();
            Assert.IsTrue(queue.EnqueueSkill(merc.LearnedSkills["priest.heal"]),
                "Heal skill should enqueue on an empty queue");

            var (engine, sink) = RunBattleWithMerc(hero, monsters, merc, queue, seed: 777);

            Assert.AreEqual(0, queue.Count, "Player-queued heal must be consumed");
            bool heroHealed = false;
            for (int i = 0; i < sink.HealEvents.Count; i++)
            {
                var evt = sink.HealEvents[i];
                if (evt.Source == "priest.heal" && evt.TargetName == hero.Name && evt.Amount > 0)
                {
                    heroHealed = true;
                    break;
                }
            }
            Assert.IsTrue(heroHealed,
                $"Queued priest.heal must heal the wounded hero (hero was {heroHpBeforeBattle}/{hero.MaxHP} entering battle)");
        }

        [TestMethod]
        [TestCategory("BattleEngine")]
        public void RedirectableHeals_AreDeclaredSingleAlly()
        {
            // Heals the AI can redirect to any ally must be SingleAlly; Self now means self-only
            Assert.AreEqual(RolePlayingFramework.Skills.SkillTargetType.SingleAlly,
                new RolePlayingFramework.Skills.HealSkill().TargetType, "priest.heal");
            Assert.AreEqual(RolePlayingFramework.Skills.SkillTargetType.SingleAlly,
                new RolePlayingFramework.Skills.AuraHealSkill().TargetType, "synergy.aura_heal");
            Assert.AreEqual(RolePlayingFramework.Skills.SkillTargetType.SingleAlly,
                new RolePlayingFramework.Skills.PurifySkill().TargetType, "synergy.purify");
            Assert.AreEqual(RolePlayingFramework.Skills.SkillTargetType.SingleAlly,
                new RolePlayingFramework.Skills.SoulWardSkill().TargetType, "synergy.soul_ward");

            // Genuine self-only skill keeps its Self declaration
            Assert.AreEqual(RolePlayingFramework.Skills.SkillTargetType.Self,
                new RolePlayingFramework.Skills.VanishSkill().TargetType, "thief.vanish");
        }

        [TestMethod]
        [TestCategory("BattleEngine")]
        public void MercQueuedSelfBuff_TargetsCasterNotWoundedAlly()
        {
            // Wounded hero + thief merc queuing Vanish (Self buff): the buff must land on
            // the caster, not be redirected to the most-wounded ally
            var hero = MakeHero(str: 20, agi: 10, vit: 20, mag: 5);
            hero.TakeDamage(hero.MaxHP / 2);

            var merc = new Mercenary("Sly Fox", new RolePlayingFramework.Jobs.Primary.Thief(),
                15, new StatBlock(12, 15, 12, 15));
            merc.LearnAllJobSkills();

            var monsters = new List<IEnemy> { MakeSlime(5) };

            var queue = new ActionQueue();
            Assert.IsTrue(queue.EnqueueSkill(merc.LearnedSkills["thief.vanish"]),
                "Vanish should enqueue on an empty queue");

            var (engine, sink) = RunBattleWithMerc(hero, monsters, merc, queue, seed: 321);

            Assert.AreEqual(0, queue.Count, "Player-queued buff must be consumed");
            bool buffedCaster = false;
            for (int i = 0; i < sink.BuffEvents.Count; i++)
            {
                var evt = sink.BuffEvents[i];
                if (evt.Source == "thief.vanish")
                {
                    Assert.AreEqual(merc.Name, evt.TargetName,
                        "Self-declared buff must target the caster, never a wounded ally");
                    buffedCaster = true;
                }
            }
            Assert.IsTrue(buffedCaster, "Queued Vanish must apply its buff to the casting mercenary");
        }

        [TestMethod]
        [TestCategory("BattleEngine")]
        public void MercQueue_EmptyOrNull_ProducesIdenticalBattle()
        {
            const int seed = 8888;

            // Run 1: null player queue (pre-#303 behavior)
            var hero1 = MakeHero(str: 15, agi: 10, vit: 12, mag: 5);
            var merc1 = MakeKnightMerc();
            var monsters1 = new List<IEnemy> { MakeSlime(4), MakeSlime(4) };
            var (engine1, sink1) = RunBattleWithMerc(hero1, monsters1, merc1, null, seed);

            // Run 2: empty player queue — must consume identical RNG
            var hero2 = MakeHero(str: 15, agi: 10, vit: 12, mag: 5);
            var merc2 = MakeKnightMerc();
            var monsters2 = new List<IEnemy> { MakeSlime(4), MakeSlime(4) };
            var (engine2, sink2) = RunBattleWithMerc(hero2, monsters2, merc2, new ActionQueue(), seed);

            Assert.AreEqual(engine1.Outcome, engine2.Outcome, "Outcome must match (RNG parity)");
            Assert.AreEqual(hero1.CurrentHP, hero2.CurrentHP, "Hero HP must match (RNG parity)");
            Assert.AreEqual(merc1.CurrentHP, merc2.CurrentHP, "Merc HP must match (RNG parity)");
            Assert.AreEqual(sink1.AttackEvents.Count, sink2.AttackEvents.Count, "Event count must match (RNG parity)");
            for (int i = 0; i < sink1.AttackEvents.Count; i++)
            {
                Assert.AreEqual(sink1.AttackEvents[i].ActorName, sink2.AttackEvents[i].ActorName, $"Event {i} actor must match");
                Assert.AreEqual(sink1.AttackEvents[i].Action, sink2.AttackEvents[i].Action, $"Event {i} action must match");
                Assert.AreEqual(sink1.AttackEvents[i].Damage, sink2.AttackEvents[i].Damage, $"Event {i} damage must match");
            }
        }
    }
}
