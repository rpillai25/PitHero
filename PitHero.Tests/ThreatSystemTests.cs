using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero;
using PitHero.AI;
using PitHero.Combat;
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
    /// Threat / aggro system (PitHero/docs/ThreatSystem.md): ledger math, job scaling,
    /// evasion escalation, and end-to-end monster targeting through BattleEngine.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class ThreatSystemTests
    {
        /// <summary>Sink that records threat events, target changes, and monster attack targets in order.</summary>
        private sealed class ThreatRecordingSink : BattleEventSinkBase
        {
            public List<BattleThreatEvent> ThreatEvents { get; } = new List<BattleThreatEvent>();
            public List<IBattleAlly> TargetChanges { get; } = new List<IBattleAlly>();
            public List<string> MonsterAttackTargets { get; } = new List<string>();
            public int MonsterAttacksOnThreatTarget;
            private string _currentTarget;

            public List<BattleBuffEvent> BuffEvents { get; } = new List<BattleBuffEvent>();
            public List<BattleProvokeEvent> ProvokeEvents { get; } = new List<BattleProvokeEvent>();

            /// <summary>Ordered "provoke:&lt;tank&gt;" / "swing:&lt;target&gt;" entries for sequencing assertions.</summary>
            public List<string> Timeline { get; } = new List<string>();

            public override void OnThreatGenerated(in BattleThreatEvent evt) => ThreatEvents.Add(evt);
            public override void OnBuffApplied(in BattleBuffEvent evt) => BuffEvents.Add(evt);

            public override System.Collections.IEnumerator OnProvoke(IBattleAlly tank, in BattleProvokeEvent evt)
            {
                ProvokeEvents.Add(evt);
                Timeline.Add("provoke:" + evt.TankName);
                return null;
            }

            public override void OnThreatTargetChanged(IBattleAlly target)
            {
                TargetChanges.Add(target);
                _currentTarget = target?.Combatant?.Name;
            }

            public override void OnAttackResolved(in BattleAttackEvent evt)
            {
                if (evt.ActorType != "monster") return;
                MonsterAttackTargets.Add(evt.TargetName);
                Timeline.Add("swing:" + evt.TargetName);
                if (_currentTarget != null && evt.TargetName == _currentTarget)
                    MonsterAttacksOnThreatTarget++;
            }
        }

        private static Hero MakeMageHero(int level = 10)
            => new Hero("Hero", new Mage(), level, new StatBlock(6, 10, 14, 8));

        private static Mercenary MakeKnightMerc(int level = 10)
        {
            var merc = new Mercenary("Tank", new Knight(), level, new StatBlock(20, 10, 18, 12));
            merc.LearnAllJobSkills();
            return merc;
        }

        private static (BattleEngine engine, ThreatRecordingSink sink) RunBattle(
            Hero hero, List<IEnemy> monsters, List<Mercenary> mercs, int seed, BattleTactic tactic = BattleTactic.Blitz,
            ActionQueue heroQueue = null)
        {
            Nez.Random.SetSeed(seed);
            var party = new TestPartyView(hero, tactic);
            var sink = new ThreatRecordingSink();
            var engine = new BattleEngine(party, sink);
            var heroAlly = new TestBattleAlly(hero, isHero: true);
            var mercAllies = new List<IBattleAlly>();
            if (mercs != null)
                for (int i = 0; i < mercs.Count; i++)
                    mercAllies.Add(new TestBattleAlly(mercs[i], isHero: false));
            HeadlessCoroutineRunner.RunToCompletion(engine.Run(heroAlly, mercAllies, monsters, heroQueue ?? new ActionQueue()));
            return (engine, sink);
        }

        // ── ThreatTable math ────────────────────────────────────────────────────

        [TestMethod]
        [TestCategory("Threat")]
        public void ThreatTable_AddDecayRemove()
        {
            var table = new ThreatTable();
            var mage = MakeMageHero();

            Assert.AreEqual(0f, table.Get(mage));
            Assert.AreEqual(10f, table.Add(mage, 10f));
            Assert.AreEqual(25f, table.Add(mage, 15f));
            Assert.AreEqual(25f, table.Add(mage, -5f), "Negative amounts are ignored");

            table.DecayRound();
            Assert.AreEqual(25f * GameConfig.ThreatDecayPerRound, table.Get(mage), 0.001f);

            // Decay far enough and it snaps to zero (ThreatFloor)
            for (int i = 0; i < 20; i++) table.DecayRound();
            Assert.AreEqual(0f, table.Get(mage));

            table.Add(mage, 5f);
            table.Remove(mage);
            Assert.AreEqual(0f, table.Get(mage));
            Assert.AreEqual(0, table.Count);
        }

        [TestMethod]
        [TestCategory("Threat")]
        public void JobMultiplier_KnightDoubles_OthersUnscaled()
        {
            var knight = new Hero("K", new Knight(), 10, new StatBlock(10, 10, 10, 10));
            var mage = MakeMageHero();
            var combo = new Hero("C", new CompositeJob(new Knight(), new Mage()), 10, new StatBlock(10, 10, 10, 10));

            Assert.AreEqual(GameConfig.ThreatKnightMultiplier, ThreatTable.JobThreatMultiplier(knight));
            Assert.AreEqual(1f, ThreatTable.JobThreatMultiplier(mage));
            Assert.AreEqual(GameConfig.ThreatKnightMultiplier, ThreatTable.JobThreatMultiplier(combo),
                "A composite job containing Knight keeps the tank multiplier");

            var table = new ThreatTable();
            float added = table.AddScaled(knight, 10f, out float total);
            Assert.AreEqual(20f, added);
            Assert.AreEqual(20f, total);
        }

        [TestMethod]
        [TestCategory("Threat")]
        public void Evasion_EscalatesPerDodge()
        {
            var thief = new Mercenary("Sly", new Thief(), 10, new StatBlock(10, 20, 10, 5));
            var table = new ThreatTable();
            float b = GameConfig.ThreatEvasionBase;

            Assert.AreEqual(b * 1, table.RegisterEvasion(thief, out _));
            Assert.AreEqual(b * 2, table.RegisterEvasion(thief, out _));
            Assert.AreEqual(b * 3, table.RegisterEvasion(thief, out float total));
            Assert.AreEqual(b * 6, total, 0.001f);
            Assert.AreEqual(3, table.GetEvasionCount(thief));
        }

        [TestMethod]
        [TestCategory("Threat")]
        public void HighestAmong_PicksMax_TiesToFirst_NullWhenNone()
        {
            var hero = MakeMageHero();
            var merc = MakeKnightMerc();
            var allies = new List<IBattleAlly>
            {
                new TestBattleAlly(hero, isHero: true),
                new TestBattleAlly(merc, isHero: false)
            };
            var table = new ThreatTable();

            Assert.IsNull(table.HighestAmong(allies), "No threat → no target");

            table.Add(hero, 10f);
            table.Add(merc, 10f);
            Assert.AreSame(allies[0], table.HighestAmong(allies), "Tie → earliest (hero)");

            table.Add(merc, 1f);
            Assert.AreSame(allies[1], table.HighestAmong(allies));
        }

        [TestMethod]
        [TestCategory("Threat")]
        public void SkillFlatThreat_ExplicitAndDefaults()
        {
            Assert.AreEqual(55, ThreatTable.SkillFlatThreat(new HeavyStrikeSkill()));
            Assert.AreEqual(45, ThreatTable.SkillFlatThreat(new FireSkill()));
            Assert.AreEqual(30, ThreatTable.SkillFlatThreat(new HealSkill()));
            Assert.AreEqual(0, ThreatTable.SkillFlatThreat(new VanishSkill()), "Vanish deliberately generates no threat");
            Assert.AreEqual(GameConfig.ThreatSkillAttackDefault, ThreatTable.SkillFlatThreat(new MeteorSkill()));
            Assert.AreEqual(GameConfig.ThreatSkillSupportDefault, ThreatTable.SkillFlatThreat(new FadeSkill()));
            Assert.AreEqual(0, ThreatTable.SkillFlatThreat(null));
        }

        [TestMethod]
        [TestCategory("Threat")]
        public void DamageAndHealThreat_PercentUnits_Capped()
        {
            // 50 damage on a 200-HP enemy = 25% → × ThreatPerDamagePercent
            Assert.AreEqual(25f * GameConfig.ThreatPerDamagePercent, ThreatTable.DamageThreat(50, 200), 0.001f);
            // Overkill caps at 100%
            Assert.AreEqual(100f * GameConfig.ThreatPerDamagePercent, ThreatTable.DamageThreat(5000, 200), 0.001f);
            Assert.AreEqual(0f, ThreatTable.DamageThreat(0, 200));
            Assert.AreEqual(40f * GameConfig.ThreatPerHealPercent, ThreatTable.HealThreat(40, 100), 0.001f);
        }

        // ── End-to-end through BattleEngine ─────────────────────────────────────

        [TestMethod]
        [TestCategory("Threat")]
        public void Battle_KnightMercDrawsMajorityOfMonsterAttacks()
        {
            int total = 0, onKnight = 0;
            for (int seed = 1; seed <= 12; seed++)
            {
                var hero = MakeMageHero();
                var merc = MakeKnightMerc();
                var monsters = new List<IEnemy> { new Slime(6), new Slime(6), new Slime(6) };
                var (_, sink) = RunBattle(hero, monsters, new List<Mercenary> { merc }, seed);

                for (int i = 0; i < sink.MonsterAttackTargets.Count; i++)
                {
                    total++;
                    if (sink.MonsterAttackTargets[i] == merc.Name) onKnight++;
                }
            }

            Assert.IsTrue(total > 20, $"Expected a meaningful sample of monster attacks, got {total}");
            float share = (float)onKnight / total;
            Assert.IsTrue(share >= 0.6f,
                $"Knight should absorb the majority of monster attacks (got {share:P0} of {total})");
        }

        [TestMethod]
        [TestCategory("Threat")]
        public void Battle_GuaranteedPull_AlwaysHitsThreatTarget()
        {
            float saved = GameConfig.ThreatTargetHitChance;
            GameConfig.ThreatTargetHitChance = 1.0f;
            try
            {
                var hero = MakeMageHero();
                var merc = MakeKnightMerc();
                var monsters = new List<IEnemy> { new Slime(6), new Slime(6) };
                var (_, sink) = RunBattle(hero, monsters, new List<Mercenary> { merc }, seed: 7);

                // Every monster attack after a target was announced must land on that target
                Assert.IsTrue(sink.TargetChanges.Count >= 1, "A threat target must be announced");
                Assert.AreEqual(sink.MonsterAttackTargets.Count, sink.MonsterAttacksOnThreatTarget,
                    "With ThreatTargetHitChance=1 every monster attack hits the current threat target");
            }
            finally
            {
                GameConfig.ThreatTargetHitChance = saved;
            }
        }

        [TestMethod]
        [TestCategory("Threat")]
        public void Battle_ThreatEventsFire_AndTargetClearsAtBattleEnd()
        {
            var hero = MakeMageHero();
            var merc = MakeKnightMerc();
            // Enough monsters that at least one monster turn happens before the fight ends
            var monsters = new List<IEnemy> { new Slime(10), new Slime(10), new Slime(10), new Slime(10) };
            var (engine, sink) = RunBattle(hero, monsters, new List<Mercenary> { merc }, seed: 3);

            Assert.AreEqual(BattleOutcome.MonstersCleared, engine.Outcome);
            Assert.IsTrue(sink.ThreatEvents.Count > 0, "Damage/skill use must generate threat events");

            bool sawSkill = false;
            for (int i = 0; i < sink.ThreatEvents.Count; i++)
            {
                var e = sink.ThreatEvents[i];
                Assert.IsTrue(e.Amount > 0f);
                Assert.IsTrue(e.Total >= e.Amount);
                if (e.Source.StartsWith("knight.")) sawSkill = true;
            }
            Assert.IsTrue(sawSkill, "Knight skill use must be attributed as a threat source");

            Assert.IsTrue(sink.TargetChanges.Count >= 2, "Target announced during battle and cleared at end");
            Assert.IsNull(sink.TargetChanges[sink.TargetChanges.Count - 1],
                "Battle end must announce a null threat target so the HUD tint clears");
        }

        // ── Threat rescue (tank AI pulls aggro off a wounded non-tank) ───────────

        /// <summary>Composite Knight/Thief merc: on Defensive it would normally open with Vanish (buff).</summary>
        private static Mercenary MakeKnightThiefMerc()
        {
            var merc = new Mercenary("Paladin", new CompositeJob(new Knight(), new Thief()), 10, new StatBlock(18, 10, 16, 12));
            merc.LearnAllJobSkills();
            return merc;
        }

        [TestMethod]
        [TestCategory("Threat")]
        public void Rescue_CompositeKnight_PrefersHighestThreatSkill_WhenNonTankTargetWounded()
        {
            var hero = MakeMageHero();
            hero.TakeDamage(hero.MaxHP / 2);            // 50% HP ≤ ThreatRescueHpPercent
            var merc = MakeKnightThiefMerc();
            var party = new TacticTestPartyView(hero, BattleTactic.Defensive);
            var monsters = new List<IEnemy> { new Slime(5) };

            // Hero (Mage, non-tank) holds the monsters' attention → composite Knight must intervene
            var action = BattleTacticDecisionEngine.DecideMercenaryAction(
                merc, party, monsters, new List<Mercenary> { merc }, roundNumber: 1, battleCriticalReached: false,
                threatTarget: hero);

            Assert.AreEqual(BattleAction.ActionKind.UseAttackSkill, action.Kind, "Rescue must be an attack skill, not the Vanish buff");
            Assert.IsTrue(action.Skill.Id.StartsWith("knight."), $"Expected a 55-threat Knight skill, got {action.Skill.Id}");
            Assert.AreEqual(55, action.Skill.ThreatValue);
        }

        [TestMethod]
        [TestCategory("Threat")]
        public void HoldAggro_TankGrabsThreat_WhenNotTarget_EvenIfTargetHealthy()
        {
            var hero = MakeMageHero();
            var merc = MakeKnightThiefMerc();
            var party = new TacticTestPartyView(hero, BattleTactic.Defensive);
            var monsters = new List<IEnemy> { new Slime(5) };
            var mercs = new List<Mercenary> { merc };

            // Healthy hero holds threat → tank still wants it back (hold-aggro tier)
            var healthy = BattleTacticDecisionEngine.DecideMercenaryAction(merc, party, monsters, mercs, 1, false, threatTarget: hero);
            Assert.AreEqual(BattleAction.ActionKind.UseAttackSkill, healthy.Kind);
            Assert.IsTrue(healthy.Skill.Id.StartsWith("knight."), $"got {healthy.Skill.Id}");

            // Empty ledger (round 1, nobody has threat) → tank opens with its pull skill, not Vanish
            var none = BattleTacticDecisionEngine.DecideMercenaryAction(merc, party, monsters, mercs, 1, false);
            Assert.AreEqual(BattleAction.ActionKind.UseAttackSkill, none.Kind);
            Assert.IsTrue(none.Skill.Id.StartsWith("knight."), $"got {none.Skill.Id}");

            // Hold-aggro off → back to the normal Defensive path (Vanish) when nobody is wounded
            bool saved = GameConfig.ThreatTankHoldAggro;
            GameConfig.ThreatTankHoldAggro = false;
            try
            {
                var off = BattleTacticDecisionEngine.DecideMercenaryAction(merc, party, monsters, mercs, 1, false, threatTarget: hero);
                Assert.AreEqual("thief.vanish", off.Skill.Id);

                // …but the rescue tier still fires for a wounded non-tank target
                hero.TakeDamage(hero.MaxHP / 2);
                var rescue = BattleTacticDecisionEngine.DecideMercenaryAction(merc, party, monsters, mercs, 1, false, threatTarget: hero);
                Assert.AreEqual(BattleAction.ActionKind.UseAttackSkill, rescue.Kind);
            }
            finally
            {
                GameConfig.ThreatTankHoldAggro = saved;
            }
        }

        [TestMethod]
        [TestCategory("Threat")]
        public void HoldAggro_Maintain_RefiresWhenRivalWithinMargin()
        {
            var hero = MakeMageHero();
            var merc = MakeKnightThiefMerc();
            var party = new TacticTestPartyView(hero, BattleTactic.Defensive);
            var monsters = new List<IEnemy> { new Slime(5) };
            var mercs = new List<Mercenary> { merc };
            float m = GameConfig.ThreatHoldMargin;

            // Tank holds the target but a rival is within the margin → re-assert with a Knight skill
            var close = BattleTacticDecisionEngine.DecideMercenaryAction(merc, party, monsters, mercs, 2, false,
                threatTarget: merc, selfThreat: 100f, rivalThreat: 100f / m + 1f);
            Assert.AreEqual(BattleAction.ActionKind.UseAttackSkill, close.Kind);
            Assert.IsTrue(close.Skill.Id.StartsWith("knight."), $"got {close.Skill.Id}");

            // Comfortable lead → normal path (Vanish on Defensive)
            var safe = BattleTacticDecisionEngine.DecideMercenaryAction(merc, party, monsters, mercs, 2, false,
                threatTarget: merc, selfThreat: 100f, rivalThreat: 100f / m - 1f);
            Assert.AreEqual("thief.vanish", safe.Skill.Id);

            // No rivals with threat at all → nothing to maintain against
            var alone = BattleTacticDecisionEngine.DecideMercenaryAction(merc, party, monsters, mercs, 2, false,
                threatTarget: merc, selfThreat: 100f, rivalThreat: 0f);
            Assert.AreEqual("thief.vanish", alone.Skill.Id);
        }

        [TestMethod]
        [TestCategory("Threat")]
        public void ThreatTable_HighestThreatExcluding()
        {
            var hero = MakeMageHero();
            var merc = MakeKnightMerc();
            var allies = new List<IBattleAlly> { new TestBattleAlly(hero, true), new TestBattleAlly(merc, false) };
            var table = new ThreatTable();
            table.Add(hero, 40f);
            table.Add(merc, 90f);
            Assert.AreEqual(40f, table.HighestThreatExcluding(allies, merc));
            Assert.AreEqual(90f, table.HighestThreatExcluding(allies, hero));
            Assert.AreEqual(0f, new ThreatTable().HighestThreatExcluding(allies, merc));
        }

        [TestMethod]
        [TestCategory("Threat")]
        public void Battle_PureKnightHero_KeepsPullingVsMageMerc()
        {
            // Ivan-style setup: pure Knight hero (slow, Strategic) with a Mage merc casting Fire every turn.
            // With maintain + turn-start re-evaluation the Knight must re-cast Spin Slash within fights,
            // not just once, and absorb the majority of monster hits.
            int total = 0, onHero = 0, multiCastBattles = 0, battles = 0;
            var diag = new System.Text.StringBuilder();
            for (int seed = 1; seed <= 12; seed++)
            {
                var crystal = new HeroCrystal("C", new Knight(), 10, new StatBlock(14, 6, 16, 12));
                crystal.EarnJP(1_000_000);
                var hero = new Hero("Ivan", new Knight(), 10, new StatBlock(14, 6, 16, 12), crystal);
                Assert.IsTrue(hero.TryPurchaseSkill(new SpinSlashSkill()));
                var mage = new Mercenary("Bran", new Mage(), 10, new StatBlock(6, 12, 8, 18));
                mage.LearnAllJobSkills();
                var monsters = new List<IEnemy> { new Slime(18), new Slime(18), new Slime(18), new Slime(18), new Slime(18) };
                var (engine, sink) = RunBattle(hero, monsters, new List<Mercenary> { mage }, seed, BattleTactic.Strategic);
                battles++;
                int heroSkillCasts = 0, heroPhys = 0;
                for (int i = 0; i < sink.ThreatEvents.Count; i++)
                {
                    if (sink.ThreatEvents[i].ActorName != hero.Name) continue;
                    if (sink.ThreatEvents[i].Source == "knight.spin_slash") heroSkillCasts++;
                    else if (sink.ThreatEvents[i].Source == "physical") heroPhys++;
                }
                if (heroSkillCasts >= 2) multiCastBattles++;
                int hits = 0;
                for (int i = 0; i < sink.MonsterAttackTargets.Count; i++)
                {
                    total++;
                    if (sink.MonsterAttackTargets[i] == hero.Name) { onHero++; hits++; }
                }
                diag.Append($"[seed {seed}: spin={heroSkillCasts} phys={heroPhys} mp={hero.CurrentMP}/{hero.MaxMP} monAtk={sink.MonsterAttackTargets.Count} onHero={hits} outcome={engine.Outcome}] ");
            }
            Assert.IsTrue(multiCastBattles >= battles / 2,
                $"Knight should re-cast Spin Slash mid-fight in most battles (did so in {multiCastBattles}/{battles}) {diag}");
            Assert.IsTrue(total >= 12, $"Expected a meaningful sample, got {total}");
            float share = (float)onHero / total;
            Assert.IsTrue(share >= 0.55f, $"Pure Knight hero should absorb most hits vs a Mage merc (got {share:P0} of {total})");
        }

        [TestMethod]
        [TestCategory("Threat")]
        public void Rescue_NotTriggered_WhenTankHoldsIt_OrSelfIsNotTank()
        {
            var hero = MakeMageHero();
            var merc = MakeKnightThiefMerc();
            var party = new TacticTestPartyView(hero, BattleTactic.Defensive);
            var monsters = new List<IEnemy> { new Slime(5) };
            var mercs = new List<Mercenary> { merc };

            // Wounded, but the target is the tank itself → normal path (Vanish)
            merc.TakeDamage(merc.MaxHP / 2);
            var self = BattleTacticDecisionEngine.DecideMercenaryAction(merc, party, monsters, mercs, 1, false, threatTarget: merc);
            Assert.AreEqual("thief.vanish", self.Skill.Id);

            // A non-tank merc never rescues, however wounded the target
            hero.TakeDamage(hero.MaxHP / 2);
            var mage = new Mercenary("Wiz", new Mage(), 10, new StatBlock(5, 10, 10, 20));
            mage.LearnAllJobSkills();
            var mageAction = BattleTacticDecisionEngine.DecideMercenaryAction(mage, party, monsters, new List<Mercenary> { mage }, 1, false, threatTarget: hero);
            Assert.AreEqual(BattleAction.ActionKind.UseAttackSkill, mageAction.Kind);
            Assert.IsTrue(mageAction.Skill.Id.StartsWith("mage."), "Mage follows its normal path (no Knight rescue branch)");
        }

        [TestMethod]
        [TestCategory("Threat")]
        public void Rescue_Hero_CompositeKnight_AlsoIntervenes()
        {
            var crystal = new HeroCrystal("C", new CompositeJob(new Knight(), new Priest()), 10, new StatBlock(16, 10, 16, 12));
            crystal.EarnJP(1_000_000);
            var hero = new Hero("Legend", new CompositeJob(new Knight(), new Priest()), 10, new StatBlock(16, 10, 16, 12), crystal);
            Assert.IsTrue(hero.TryPurchaseSkill(new HeavyStrikeSkill()));
            Assert.IsTrue(hero.TryPurchaseSkill(new DefenseUpSkill()));

            var priest = new Mercenary("Cleric", new Priest(), 10, new StatBlock(5, 10, 10, 20));
            priest.TakeDamage(priest.MaxHP / 2);
            var party = new TacticTestPartyView(hero, BattleTactic.Defensive);
            var monsters = new List<IEnemy> { new Slime(5) };

            var action = BattleTacticDecisionEngine.DecideHeroAction(party, monsters, new List<Mercenary> { priest }, 2, false,
                threatTarget: priest);

            Assert.AreEqual(BattleAction.ActionKind.UseAttackSkill, action.Kind, "Composite hero must skip DefUp and pull aggro");
            Assert.AreEqual("knight.heavy_strike", action.Skill.Id);
        }

        [TestMethod]
        [TestCategory("Threat")]
        public void Battle_HoldAggro_TankSkipsOpenerBuffs()
        {
            // With hold-aggro on, a Defensive composite tank opens with Knight skills instead of Vanish;
            // with it off (and nobody wounded) the old Vanish opener returns.
            int VanishCasts(bool holdAggro)
            {
                bool saved = GameConfig.ThreatTankHoldAggro;
                GameConfig.ThreatTankHoldAggro = holdAggro;
                int casts = 0;
                try
                {
                for (int seed = 1; seed <= 12; seed++)
                {
                    var hero = MakeMageHero();
                    var merc = MakeKnightThiefMerc();
                    var monsters = new List<IEnemy> { new Slime(8), new Slime(8), new Slime(8) };
                    var (_, sink) = RunBattle(hero, monsters, new List<Mercenary> { merc }, seed, BattleTactic.Defensive);
                    // Vanish has ThreatValue 0 (no threat event) — count its buff application instead
                    for (int i = 0; i < sink.BuffEvents.Count; i++)
                        if (sink.BuffEvents[i].Source == "thief.vanish") casts++;
                }
                }
                finally { GameConfig.ThreatTankHoldAggro = saved; }
                return casts;
            }

            int off = VanishCasts(false);
            int on = VanishCasts(true);
            // Note: once the tank already holds aggro the normal Defensive path (Vanish) resumes, so
            // casts don't drop to zero — but the openers while not-yet-target must be Knight skills.
            Assert.IsTrue(off > 0, "Without hold-aggro the Defensive composite tank should still Vanish sometimes");
            Assert.IsTrue(on < off, $"Hold-aggro should replace Vanish openers with Knight skills (vanish casts: off={off}, on={on})");
        }

        [TestMethod]
        [TestCategory("Threat")]
        public void Battle_HoldAggro_CompositeHeroOutThreatsMageMerc()
        {
            // Legend-style hero (Knight+Mage) with a Mage merc: the hero must absorb the majority of hits
            // even though both have Fire — the playtest gap that motivated hold-aggro.
            int total = 0, onHero = 0;
            for (int seed = 1; seed <= 12; seed++)
            {
                var crystal = new HeroCrystal("C", new CompositeJob(new Knight(), new Mage()), 12, new StatBlock(16, 14, 16, 14));
                crystal.EarnJP(1_000_000);
                var hero = new Hero("Legend", new CompositeJob(new Knight(), new Mage()), 12, new StatBlock(16, 14, 16, 14), crystal);
                Assert.IsTrue(hero.TryPurchaseSkill(new HeavyStrikeSkill()));
                Assert.IsTrue(hero.TryPurchaseSkill(new SpinSlashSkill()));
                Assert.IsTrue(hero.TryPurchaseSkill(new FireSkill()));
                var mage = new Mercenary("Wiz", new Mage(), 12, new StatBlock(6, 10, 10, 20));
                mage.LearnAllJobSkills();
                var monsters = new List<IEnemy> { new Slime(14), new Slime(14), new Slime(14), new Slime(14) };
                var (_, sink) = RunBattle(hero, monsters, new List<Mercenary> { mage }, seed, BattleTactic.Strategic);
                for (int i = 0; i < sink.MonsterAttackTargets.Count; i++)
                {
                    total++;
                    if (sink.MonsterAttackTargets[i] == hero.Name) onHero++;
                }
            }
            Assert.IsTrue(total >= 12, $"Expected a meaningful sample, got {total}");
            float share = (float)onHero / total;
            Assert.IsTrue(share >= 0.6f, $"Composite Knight hero should absorb the majority of hits (got {share:P0} of {total})");
        }

        [TestMethod]
        [TestCategory("Threat")]
        public void Battle_Determinism_SameSeedSameTargets()
        {
            List<string> Run()
            {
                var hero = MakeMageHero();
                var merc = MakeKnightMerc();
                var monsters = new List<IEnemy> { new Slime(6), new Slime(6) };
                var (_, sink) = RunBattle(hero, monsters, new List<Mercenary> { merc }, seed: 99);
                return sink.MonsterAttackTargets;
            }

            var a = Run();
            var b = Run();
            CollectionAssert.AreEqual(a, b, "Threat targeting must be fully deterministic per seed (one RNG draw)");
        }

        // ── Provoke (out-of-turn tank reaction, ThreatSystem.md) ────────────────

        [TestMethod]
        [TestCategory("Threat")]
        public void Provoke_Reaction_FiresOncePerBattle_WhenNonTankDropsBelowRescueHp_AndForcesNextSwing()
        {
            int battlesWithProvoke = 0, swingsAfterProvoke = 0, swingsAfterProvokeOnTank = 0;
            for (int seed = 1; seed <= 12; seed++)
            {
                var hero = MakeMageHero();
                hero.TakeDamage((int)(hero.MaxHP * 0.3f));   // 70% — one monster hit should cross the 60% line
                var merc = MakeKnightMerc();
                var monsters = new List<IEnemy> { new Slime(14), new Slime(14), new Slime(14), new Slime(14) };
                var (_, sink) = RunBattle(hero, monsters, new List<Mercenary> { merc }, seed);

                Assert.IsTrue(sink.ProvokeEvents.Count <= 1, "Provoke is once per battle per tank");
                if (sink.ProvokeEvents.Count == 0) continue;
                battlesWithProvoke++;

                var evt = sink.ProvokeEvents[0];
                Assert.IsTrue(evt.Reaction, "Engine-fired Provoke must be flagged as a reaction");
                Assert.AreEqual(merc.Name, evt.TankName);
                Assert.AreEqual(hero.Name, evt.ProtectedName, "The wounded Mage hero is the protected ally");
                Assert.AreEqual(ProvokeSkill.ProvokeMPCost, evt.MpSpent);
                Assert.IsTrue(evt.ThreatTotal >= ProvokeSkill.ProvokeThreat * GameConfig.ThreatKnightMultiplier,
                    $"Knight-scaled Provoke threat expected, got {evt.ThreatTotal}");

                bool sawThreatRow = false;
                for (int i = 0; i < sink.ThreatEvents.Count; i++)
                    if (sink.ThreatEvents[i].Source == ProvokeSkill.SkillId) sawThreatRow = true;
                Assert.IsTrue(sawThreatRow, "Provoke must be attributed on the threat ledger");

                // The swing right after the provoke is a guaranteed pull onto the tank
                int idx = sink.Timeline.IndexOf("provoke:" + merc.Name);
                for (int i = idx + 1; i < sink.Timeline.Count; i++)
                {
                    if (!sink.Timeline[i].StartsWith("swing:")) continue;
                    swingsAfterProvoke++;
                    if (sink.Timeline[i] == "swing:" + merc.Name) swingsAfterProvokeOnTank++;
                    break;
                }
            }

            Assert.IsTrue(battlesWithProvoke >= 4, $"Expected Provoke to trigger in most battles, got {battlesWithProvoke}/12");
            Assert.IsTrue(swingsAfterProvoke >= 3, $"Need battles that continue after the provoke, got {swingsAfterProvoke}");
            Assert.AreEqual(swingsAfterProvoke, swingsAfterProvokeOnTank,
                "Every monster swing immediately after a Provoke must land on the provoking tank");
        }

        [TestMethod]
        [TestCategory("Threat")]
        public void Provoke_DoesNotFire_WhenNobodyIsInDanger()
        {
            // Full-HP hero, weak monsters: no ally ever drops to ≤60% → no reaction
            var hero = MakeMageHero(20);
            var merc = MakeKnightMerc(20);
            var monsters = new List<IEnemy> { new Slime(1), new Slime(1) };
            var (_, sink) = RunBattle(hero, monsters, new List<Mercenary> { merc }, seed: 5);

            Assert.AreEqual(0, sink.ProvokeEvents.Count, "Provoke is a rescue, not an opener");
            for (int i = 0; i < sink.ThreatEvents.Count; i++)
                Assert.AreNotEqual(ProvokeSkill.SkillId, sink.ThreatEvents[i].Source);
        }

        [TestMethod]
        [TestCategory("Threat")]
        public void Provoke_NeverChosenAsTurnAction_ByMercAI()
        {
            var hero = MakeMageHero();
            var merc = new Mercenary("Tank", new Knight(), 10, new StatBlock(20, 10, 18, 12));
            merc.LearnSkill(new ProvokeSkill());          // only Provoke learned
            var party = new TacticTestPartyView(hero, BattleTactic.Defensive);
            var monsters = new List<IEnemy> { new Slime(5) };

            var action = BattleTacticDecisionEngine.DecideMercenaryAction(
                merc, party, monsters, new List<Mercenary> { merc }, roundNumber: 1, battleCriticalReached: false,
                threatTarget: hero);

            Assert.AreEqual(BattleAction.ActionKind.PhysicalAttack, action.Kind,
                "A reaction-only skill must be invisible to turn decisions (would otherwise be a self-buff)");
        }

        [TestMethod]
        [TestCategory("Threat")]
        public void Provoke_PlayerQueued_CastsOnHeroTurn_OnlyOncePerBattle()
        {
            var crystal = new HeroCrystal("C", new Knight(), 10, new StatBlock(14, 6, 16, 12));
            crystal.EarnJP(1_000_000);
            var hero = new Hero("Ivan", new Knight(), 10, new StatBlock(14, 6, 16, 12), crystal);
            Assert.IsTrue(hero.TryPurchaseSkill(new ProvokeSkill()));
            var provoke = hero.LearnedSkills[ProvokeSkill.SkillId];

            var queue = new ActionQueue();
            queue.EnqueueSkill(provoke);
            queue.EnqueueSkill(provoke);
            int mpBefore = hero.CurrentMP;

            var monsters = new List<IEnemy> { new Slime(12), new Slime(12), new Slime(12) };
            var (_, sink) = RunBattle(hero, monsters, new List<Mercenary>(), seed: 7, heroQueue: queue);

            Assert.AreEqual(1, sink.ProvokeEvents.Count, "Second queued Provoke is skipped without cost");
            Assert.IsFalse(sink.ProvokeEvents[0].Reaction);
            Assert.IsNull(sink.ProvokeEvents[0].ProtectedName);
            Assert.AreEqual("hero", sink.ProvokeEvents[0].TankType);
            Assert.AreEqual(ProvokeSkill.ProvokeMPCost, sink.ProvokeEvents[0].MpSpent);
        }

        [TestMethod]
        [TestCategory("Threat")]
        public void Provoke_Reaction_IsDeterministic_PerSeed()
        {
            (List<string> timeline, int provokes) Run()
            {
                var hero = MakeMageHero();
                hero.TakeDamage((int)(hero.MaxHP * 0.3f));
                var merc = MakeKnightMerc();
                var monsters = new List<IEnemy> { new Slime(14), new Slime(14), new Slime(14) };
                var (_, sink) = RunBattle(hero, monsters, new List<Mercenary> { merc }, seed: 4);
                return (sink.Timeline, sink.ProvokeEvents.Count);
            }

            var a = Run();
            var b = Run();
            Assert.AreEqual(a.provokes, b.provokes);
            CollectionAssert.AreEqual(a.timeline, b.timeline, "Provoke consumes no RNG; sequence must repeat per seed");
        }
    }
}
