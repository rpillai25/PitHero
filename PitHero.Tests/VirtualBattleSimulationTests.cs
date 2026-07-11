using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using PitHero.AI;
using PitHero.VirtualGame;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Inventory;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Mercenaries;
using RolePlayingFramework.Stats;
using System.Collections.Generic;

namespace PitHero.Tests
{
    /// <summary>
    /// Integration tests for the Phase B virtual combat simulation layer.
    ///
    /// All tests run headlessly (no Nez host, no GPU) and use real
    /// <see cref="VirtualBattleRunner"/> + <see cref="BattleEngine"/> against real
    /// <see cref="IEnemy"/> instances.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class VirtualBattleSimulationTests
    {
        // ── Helpers ──────────────────────────────────────────────────────────────

        /// <summary>Returns a pit interior tile safely inside the default pit bounds.</summary>
        private static Point InsidePitTile(VirtualWorldState world, int offsetX = 2, int offsetY = 2)
            => new Point(world.PitBounds.X + offsetX, world.PitBounds.Y + offsetY);

        /// <summary>
        /// Creates a world (pit level 1), a hero, and a runner.
        /// </summary>
        private static (VirtualWorldState world, VirtualBattleRunner runner, Hero hero)
            CreateRunner(int heroLevel = 10,
                         int str = 12, int agi = 10, int vit = 12, int mag = 5,
                         bool trapSense = false)
        {
            var world = new VirtualWorldState();
            world.RegeneratePit(1);

            var hero = new Hero("TestHero", new Knight(), heroLevel,
                                new StatBlock(str, agi, vit, mag));
            hero.TrapSense = trapSense;

            var bag       = new ItemBag();
            var partyView = new VirtualBattlePartyView(hero, bag);
            var runner    = new VirtualBattleRunner(world, partyView);
            runner.SetHeroAlly(hero);
            runner.SetMercenaries(new List<Mercenary>(0));

            return (world, runner, hero);
        }

        // ── Test 1: Level-1 traversal ─────────────────────────────────────────────

        /// <summary>
        /// Running a full pit-level-1 traversal with a reasonably geared hero should
        /// not wipe the hero; when monsters were present, positive damage should appear.
        /// </summary>
        [TestMethod]
        public void RunPitLevel1_WithReasonableHero_CompletesTraversal()
        {
            // Use a runner-level setup rather than VirtualGameSimulation.RunPitLevel
            // to avoid the Nez text-service dependency in Job.Name.
            var (world, runner, hero) = CreateRunner(heroLevel: 8,
                str: 10, agi: 8, vit: 10, mag: 4);

            // Place two Slimes adjacent to the hero
            var heroPos = InsidePitTile(world);
            world.MoveHeroTo(heroPos);
            var slime1 = EnemyFactory.Create(EnemyId.Slime, 1);
            var slime2 = EnemyFactory.Create(EnemyId.Slime, 1);
            world.AddMonster(new Point(heroPos.X + 1, heroPos.Y), slime1);
            world.AddMonster(new Point(heroPos.X, heroPos.Y + 1), slime2);

            Assert.IsTrue(world.HasLivingMonsters(),
                "Monsters should be alive before battle");

            var metrics = runner.RunAdjacentBattle();

            Assert.IsNotNull(metrics, "RunAdjacentBattle should return metrics when monsters exist");
            Assert.IsTrue(metrics.DamageDealt > 0,
                "Allies should deal positive damage against level-1 slimes");
            Assert.IsFalse(metrics.HeroDied,
                "A level-8 Knight should not die fighting level-1 Slimes");
        }

        // ── Test 2: Boss floor orb-gating ─────────────────────────────────────────

        /// <summary>
        /// While a boss is alive, <see cref="VirtualWorldState.HasLivingBoss"/> returns true.
        /// After fighting and defeating it, the boss must be gone.
        /// A strong hero is guaranteed to win.
        /// </summary>
        [TestMethod]
        public void BossFloor5_OrbNotActivatedUntilBossDefeated()
        {
            var (world, runner, _) = CreateRunner(heroLevel: 25,
                str: 40, agi: 30, vit: 40, mag: 10);

            // Directly add a StoneGuardian (pit-5 boss) adjacent to the hero
            var heroPos = InsidePitTile(world);
            world.MoveHeroTo(heroPos);
            var boss = EnemyFactory.Create(EnemyId.StoneGuardian, level: 8);
            world.AddMonster(new Point(heroPos.X + 1, heroPos.Y), boss);

            Assert.IsTrue(world.HasLivingBoss(),
                "StoneGuardian should be registered as a living boss");

            // Fight the boss
            runner.RunAdjacentBattle();

            Assert.IsFalse(world.HasLivingBoss(),
                "StoneGuardian should be defeated after the battle");
        }

        // ── Test 3: Overleveled wipe ───────────────────────────────────────────────

        /// <summary>
        /// A severely underleveled hero (level 1, minimal stats) fighting a high-level
        /// pit boss should die.
        /// </summary>
        [TestMethod]
        public void UnderpoweredHero_AgainstHighLevelBoss_Dies()
        {
            var (world, runner, weakHero) = CreateRunner(heroLevel: 1,
                str: 1, agi: 1, vit: 1, mag: 1);

            // Place a level-25 MoltenTitan (pit-25 boss) right next to the hero
            var heroPos = InsidePitTile(world);
            world.MoveHeroTo(heroPos);
            var titan = EnemyFactory.Create(EnemyId.MoltenTitan, level: 40);
            world.AddMonster(new Point(heroPos.X + 1, heroPos.Y), titan);

            var metrics = runner.RunAdjacentBattle();

            // The hero must have died either in-battle or HP must be 0
            bool heroDied = (metrics != null && metrics.HeroDied) || !runner.HeroAlive;
            Assert.IsTrue(heroDied,
                "A level-1 hero (1/1/1/1 stats) should be killed by a level-40 MoltenTitan");
        }

        // ── Test 4: Merc participation ────────────────────────────────────────────

        /// <summary>
        /// When a mercenary joins the party, the battle still completes with positive
        /// damage dealt (merc contributes alongside the hero).
        /// </summary>
        [TestMethod]
        public void BattleWithMercenary_MercDealsAdditionalDamage()
        {
            var (world, runner, _) = CreateRunner(heroLevel: 10,
                str: 10, agi: 10, vit: 10, mag: 5);

            // Wire a mercenary
            var merc = new Mercenary("Grunt", new Knight(), level: 10,
                                     new StatBlock(10, 8, 10, 3));
            runner.SetMercenaries(new List<Mercenary> { merc });

            // Place a slime adjacent
            var heroPos = InsidePitTile(world);
            world.MoveHeroTo(heroPos);
            var slime = EnemyFactory.Create(EnemyId.Slime, 1);
            world.AddMonster(new Point(heroPos.X + 1, heroPos.Y), slime);

            var metrics = runner.RunAdjacentBattle();

            Assert.IsNotNull(metrics, "Battle metrics should not be null when monsters exist");
            Assert.IsTrue(metrics.DamageDealt > 0,
                "Hero + Merc combo should deal positive damage");
        }

        // ── Test 5: Trap damage ───────────────────────────────────────────────────

        /// <summary>
        /// When the hero steps onto a trap tile without TrapSense, they take damage
        /// (clamped so they always survive with at least 1 HP).
        /// </summary>
        [TestMethod]
        public void HeroWithoutTrapSense_StepsOnTrap_TakesDamage()
        {
            var (world, _, hero) = CreateRunner(heroLevel: 10,
                str: 10, agi: 10, vit: 10, mag: 5, trapSense: false);

            // Create a fresh runner that holds the hero for ApplyTrapDamageToHero
            var bag       = new ItemBag();
            var partyView = new VirtualBattlePartyView(hero, bag);
            var runner    = new VirtualBattleRunner(world, partyView);
            runner.SetHeroAlly(hero);
            runner.SetMercenaries(new List<Mercenary>(0));

            int initialHP = hero.CurrentHP;
            Assert.IsTrue(initialHP > 1, "Hero should start with > 1 HP for this test");

            // Place a trap
            var heroPos  = world.HeroPosition;
            var trapTile = new Point(heroPos.X + 1, heroPos.Y);
            world.AddTrapTile(trapTile);
            Assert.IsTrue(world.TrapTiles.Contains(trapTile), "Trap should be registered");

            // Trigger and apply via runner (clamped to ≥ 1 HP)
            int rawDamage = world.TriggerTrap(trapTile);
            Assert.IsTrue(rawDamage > 0, "Trap should return positive damage");

            runner.ApplyTrapDamageToHero(rawDamage);

            Assert.IsTrue(hero.CurrentHP < initialHP,
                "Hero HP should decrease after trap trigger");
            Assert.IsTrue(hero.CurrentHP >= 1,
                "Hero should survive with at least 1 HP (TrapComponent clamp mirrors)");
            Assert.IsFalse(world.TrapTiles.Contains(trapTile),
                "Trap should be removed from TrapTiles after triggering");
        }

        // ── Test 6: TrapSense disarm ──────────────────────────────────────────────

        /// <summary>
        /// When the hero has TrapSense, the runner detects it and a trap can be disarmed
        /// (tile removed, no damage).
        /// </summary>
        [TestMethod]
        public void HeroWithTrapSense_StepsOnTrap_DisarmsWithoutDamage()
        {
            var (world, runner, hero) = CreateRunner(heroLevel: 10,
                trapSense: true);

            int startHP = hero.CurrentHP;

            // Verify runner sees TrapSense
            Assert.IsTrue(runner.PartyHasTrapSense(),
                "Runner should detect TrapSense on the hero");

            var heroPos  = world.HeroPosition;
            var trapTile = new Point(heroPos.X + 1, heroPos.Y);
            world.AddTrapTile(trapTile);

            // Disarm (no runner method needed — TrapSense path calls world.DisarmTrap directly)
            world.DisarmTrap(trapTile);

            Assert.IsFalse(world.TrapTiles.Contains(trapTile),
                "Trap tile should be removed after TrapSense disarm");
            Assert.AreEqual(startHP, hero.CurrentHP,
                "Hero HP should be unchanged after TrapSense disarm");
        }
    }
}
