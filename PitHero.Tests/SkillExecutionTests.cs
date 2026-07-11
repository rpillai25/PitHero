using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Jobs;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Mercenaries;
using RolePlayingFramework.Skills;
using RolePlayingFramework.Stats;
using System.Collections.Generic;

namespace PitHero.Tests
{
    /// <summary>
    /// Unit tests for skill Execute() bodies using a deterministic fake resolver.
    /// Verifies damage formulas, AoE primary inclusion, fire bonus, and LifeLeech drain.
    /// </summary>
    [TestClass]
    public class SkillExecutionTests
    {
        // ── Helpers ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Deterministic resolver that always hits and returns a fixed base damage.
        /// Not an EnhancedAttackResolver, so ResolveHit uses the legacy fallback path.
        /// </summary>
        private sealed class FixedDamageResolver : IAttackResolver
        {
            private readonly int _base;
            public FixedDamageResolver(int baseDamage = 100) => _base = baseDamage;

            /// <summary>Always returns a hit with the fixed base damage.</summary>
            public AttackResult Resolve(in StatBlock attackerStats, in StatBlock defenderStats,
                DamageKind kind, int attackerLevel, int defenderLevel)
                => new AttackResult(true, _base);
        }

        /// <summary>
        /// Minimal in-memory test enemy.  Agility=0 → evasion=0 (always hit with
        /// EnhancedAttackResolver too).
        /// </summary>
        private sealed class TestEnemy : IEnemy
        {
            private int _hp;
            public TestEnemy(int hp = 1000, ElementType element = ElementType.Neutral)
            {
                _hp = hp;
                MaxHP = hp;
                Element = element;
                ElementalProps = new ElementalProperties(element);
            }
            public string Name => "TestEnemy";
            public EnemyId EnemyId => EnemyId.Slime;
            public int Level => 1;
            // Agility=0 so evasion from BalanceConfig is minimal
            public StatBlock Stats => new StatBlock(5, 0, 5, 5);
            public DamageKind AttackKind => DamageKind.Physical;
            public ElementType Element { get; }
            public ElementalProperties ElementalProps { get; }
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

        private static Mercenary MakeMerc(IJob job, StatBlock baseStats, int level = 5)
            => new Mercenary("TestMerc", job, level, baseStats);

        // ── PowerShot ────────────────────────────────────────────────────────────────

        [TestMethod]
        [TestCategory("Skills")]
        public void PowerShot_DealsOneDotFiveTimesBaseResolverDamage()
        {
            var resolver = new FixedDamageResolver(100);
            var merc = MakeMerc(new Archer(), new StatBlock(10, 10, 10, 5));
            var primary = new TestEnemy(1000);
            var skill = new PowerShotSkill();

            skill.Execute(merc, primary, new List<IEnemy>(), resolver, null);

            int damageDealt = 1000 - primary.CurrentHP;
            Assert.AreEqual(150, damageDealt,
                "PowerShot should deal exactly 1.5× the base resolver damage");
        }

        // ── HeavyStrike ──────────────────────────────────────────────────────────────

        [TestMethod]
        [TestCategory("Skills")]
        public void HeavyStrike_DealsBasePlusStrength()
        {
            var resolver = new FixedDamageResolver(100);
            var merc = MakeMerc(new Knight(), new StatBlock(10, 5, 5, 2));
            var primary = new TestEnemy(2000);
            var skill = new HeavyStrikeSkill();

            var totalStats = merc.GetTotalStats();
            skill.Execute(merc, primary, new List<IEnemy>(), resolver, null);

            int expected = 100 + totalStats.Strength;
            int damageDealt = 2000 - primary.CurrentHP;
            Assert.AreEqual(expected, damageDealt,
                $"HeavyStrike should deal base (100) + total STR ({totalStats.Strength})");
        }

        // ── Volley AoE primary fix ───────────────────────────────────────────────────

        [TestMethod]
        [TestCategory("Skills")]
        public void Volley_HitsPrimaryTarget()
        {
            var resolver = new FixedDamageResolver(100);
            var merc = MakeMerc(new Archer(), new StatBlock(10, 10, 10, 5));
            var primary = new TestEnemy(1000);
            var skill = new VolleySkill();

            skill.Execute(merc, primary, new List<IEnemy>(), resolver, null);

            Assert.AreEqual(900, primary.CurrentHP,
                "Volley should also hit the primary target (AoE fix)");
        }

        [TestMethod]
        [TestCategory("Skills")]
        public void Volley_HitsPrimaryAndSurrounding()
        {
            var resolver = new FixedDamageResolver(100);
            var merc = MakeMerc(new Archer(), new StatBlock(10, 10, 10, 5));
            var primary = new TestEnemy(1000);
            var surrounding = new TestEnemy(1000);
            var skill = new VolleySkill();

            skill.Execute(merc, primary, new List<IEnemy> { surrounding }, resolver, null);

            Assert.AreEqual(900, primary.CurrentHP,
                "Volley should hit the primary target");
            Assert.AreEqual(900, surrounding.CurrentHP,
                "Volley should hit surrounding targets");
        }

        // ── Firestorm AoE primary fix ────────────────────────────────────────────────

        [TestMethod]
        [TestCategory("Skills")]
        public void Firestorm_HitsPrimaryAndSurrounding()
        {
            var resolver = new FixedDamageResolver(100);
            var merc = MakeMerc(new Mage(), new StatBlock(5, 5, 5, 10));
            var primary = new TestEnemy(2000);
            var surrounding = new TestEnemy(2000);
            var skill = new FireStormSkill();

            int hpPrimaryBefore = primary.CurrentHP;
            int hpSurrBefore = surrounding.CurrentHP;

            skill.Execute(merc, primary, new List<IEnemy> { surrounding }, resolver, null);

            Assert.IsTrue(primary.CurrentHP < hpPrimaryBefore,
                "Firestorm should hit the primary target");
            Assert.IsTrue(surrounding.CurrentHP < hpSurrBefore,
                "Firestorm should hit surrounding targets");
        }

        // ── Roundhouse AoE primary fix ───────────────────────────────────────────────

        [TestMethod]
        [TestCategory("Skills")]
        public void Roundhouse_HitsPrimaryAndSurrounding()
        {
            var resolver = new FixedDamageResolver(100);
            var merc = MakeMerc(new Monk(), new StatBlock(10, 10, 10, 5));
            var primary = new TestEnemy(1000);
            var surrounding = new TestEnemy(1000);
            var skill = new RoundhouseSkill();

            skill.Execute(merc, primary, new List<IEnemy> { surrounding }, resolver, null);

            Assert.AreEqual(900, primary.CurrentHP,
                "Roundhouse should hit the primary target");
            Assert.AreEqual(900, surrounding.CurrentHP,
                "Roundhouse should hit surrounding targets");
        }

        // ── LifeLeech drain (Mercenary caster) ───────────────────────────────────────

        [TestMethod]
        [TestCategory("Skills")]
        public void LifeLeech_HealsMercCasterByHalfDamage()
        {
            var resolver = new FixedDamageResolver(100);
            // Use Priest job — life_leech is a synergy skill, any job works
            var merc = MakeMerc(new Priest(), new StatBlock(5, 5, 5, 10));
            // Drain to 1 HP so the full heal amount fits within headroom (MaxHP - 1 headroom).
            merc.TakeDamage(merc.MaxHP - 1);
            int hpBeforeAttack = merc.CurrentHP;

            var primary = new TestEnemy(2000);
            var skill = new LifeLeechSkill();

            var totalStats = merc.GetTotalStats();
            int expectedDamage = 100 + totalStats.Magic / 3;

            skill.Execute(merc, primary, new List<IEnemy>(), resolver, null);

            int enemyDamage = 2000 - primary.CurrentHP;
            int mercHealReceived = merc.CurrentHP - hpBeforeAttack;

            Assert.AreEqual(expectedDamage, enemyDamage,
                "LifeLeech should deal base (100) + MAG/3 damage to the enemy");
            Assert.AreEqual(expectedDamage / 2, mercHealReceived,
                "LifeLeech should heal the Mercenary caster by half the damage dealt");
        }

        // ── mage.fire + heart_fire (Mercenary with fire bonus) ───────────────────────

        [TestMethod]
        [TestCategory("Skills")]
        public void FireSkill_MercWithHeartFire_AppliesFireDamageBonus()
        {
            var resolver = new FixedDamageResolver(100);
            var merc = MakeMerc(new Mage(), new StatBlock(5, 5, 5, 10));
            merc.LearnSkill(new HeartOfFirePassive()); // FireDamageBonus += 0.25f

            var totalStats = merc.GetTotalStats();
            // Formula: res.Damage + (int)(stats.Magic * (1f + FireDamageBonus))
            int expectedDamage = 100 + (int)(totalStats.Magic * (1f + merc.FireDamageBonus));

            var primary = new TestEnemy(5000);
            var skill = new FireSkill();
            skill.Execute(merc, primary, new List<IEnemy>(), resolver, null);

            int damageDealt = 5000 - primary.CurrentHP;
            Assert.AreEqual(expectedDamage, damageDealt,
                "mage.fire with heart_fire (FireDamageBonus=0.25) should scale the magic bonus");
        }

        [TestMethod]
        [TestCategory("Skills")]
        public void FireSkill_MercWithHeartFire_DealsmMoreThanWithout()
        {
            var resolver = new FixedDamageResolver(100);

            var mercWith = MakeMerc(new Mage(), new StatBlock(5, 5, 5, 10));
            mercWith.LearnSkill(new HeartOfFirePassive()); // +25%

            var mercWithout = MakeMerc(new Mage(), new StatBlock(5, 5, 5, 10));

            var primaryWith = new TestEnemy(5000);
            var primaryWithout = new TestEnemy(5000);
            var skill = new FireSkill();

            skill.Execute(mercWith, primaryWith, new List<IEnemy>(), resolver, null);
            skill.Execute(mercWithout, primaryWithout, new List<IEnemy>(), resolver, null);

            int dmgWith = 5000 - primaryWith.CurrentHP;
            int dmgWithout = 5000 - primaryWithout.CurrentHP;

            Assert.IsTrue(dmgWith > dmgWithout,
                $"heart_fire should increase mage.fire damage. With={dmgWith}, Without={dmgWithout}");
        }
    }
}
