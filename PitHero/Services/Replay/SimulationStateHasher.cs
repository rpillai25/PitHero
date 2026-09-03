using Nez;
using PitHero.AI;
using PitHero.ECS.Components;

namespace PitHero.Services.Replay
{
    /// <summary>
    /// Cheap FNV-1a fingerprint of the simulation, sampled every GameConfig.ReplayHashIntervalTicks.
    /// Built from four parts so a divergence can name what drifted: the RNG streams (the strongest
    /// tripwire: any extra or missing roll shifts it), the hero (tile, vitals, FSM, bag), the party
    /// (mercenaries, allied monsters, living pit monsters) and the world (gold, pit, clock).
    /// Allocation-free; for-loops only.
    /// </summary>
    public static class SimulationStateHasher
    {
        /// <summary>Hashes the live game state of the current scene into a combined sample with its parts.</summary>
        public static ReplayHashSample Sample(long tick)
        {
            ulong rng = HashRng();
            ulong hero = ReplayIO.HashSeed;
            ulong party = ReplayIO.HashSeed;
            ulong world = ReplayIO.HashSeed;

            if (Core.Instance != null && Core.Scene != null && Core.Services != null)
            {
                hero = HashHero(hero);
                party = HashParty(party);
                world = HashWorld(world);
            }

            ulong combined = ReplayIO.HashSeed;
            combined = ReplayIO.Hash(combined, tick);
            combined = ReplayIO.Hash(combined, rng);
            combined = ReplayIO.Hash(combined, hero);
            combined = ReplayIO.Hash(combined, party);
            combined = ReplayIO.Hash(combined, world);
            return new ReplayHashSample(tick, combined, rng, hero, party, world);
        }

        /// <summary>Combined hash only.</summary>
        public static ulong Compute(long tick)
        {
            return Sample(tick).Hash;
        }

        private static ulong HashRng()
        {
            ulong h = ReplayIO.HashSeed;
            var sim = GameRandom.Sim;
            if (sim != null)
            {
                sim.GetState(out uint s0, out uint s1, out uint s2, out uint s3);
                h = ReplayIO.Hash(h, s0); h = ReplayIO.Hash(h, s1); h = ReplayIO.Hash(h, s2); h = ReplayIO.Hash(h, s3);
            }
            var loot = GameRandom.Loot;
            if (loot != null)
            {
                loot.GetState(out uint l0, out uint l1, out uint l2, out uint l3);
                h = ReplayIO.Hash(h, l0); h = ReplayIO.Hash(h, l1); h = ReplayIO.Hash(h, l2); h = ReplayIO.Hash(h, l3);
            }
            return h;
        }

        private static ulong HashHero(ulong h)
        {
            var heroEntity = Core.Scene.FindEntity("hero");
            var heroComp = heroEntity?.GetComponent<HeroComponent>();
            var hero = heroComp?.LinkedHero;
            if (hero == null)
                return ReplayIO.Hash(h, -1);

            var tile = heroComp.GetCurrentTilePosition();
            h = ReplayIO.Hash(h, tile.X);
            h = ReplayIO.Hash(h, tile.Y);
            h = ReplayIO.Hash(h, hero.CurrentHP);
            h = ReplayIO.Hash(h, hero.CurrentMP);
            h = ReplayIO.Hash(h, hero.Level);
            h = ReplayIO.Hash(h, hero.Experience);
            h = ReplayIO.Hash(h, heroComp.InsidePit ? 1 : 0);
            h = ReplayIO.Hash(h, heroComp.StoppedAdventure ? 1 : 0);
            var fsm = heroEntity.GetComponent<HeroStateMachine>();
            if (fsm != null)
                h = ReplayIO.Hash(h, (int)fsm.CurrentState);
            h = ReplayIO.Hash(h, HeroStateMachine.IsBattleInProgress ? 1 : 0);

            var bag = heroComp.Bag;
            if (bag != null)
            {
                h = ReplayIO.Hash(h, bag.Count);
                for (int i = 0; i < bag.Capacity; i++)
                {
                    var item = bag.GetSlotItem(i);
                    if (item == null)
                        continue;
                    h = ReplayIO.Hash(h, i);
                    h = ReplayIO.Hash(h, item.Name);
                }
            }
            return h;
        }

        private static ulong HashParty(ulong h)
        {
            var services = Core.Services;
            var mercManager = services.GetService<MercenaryManager>();
            if (mercManager != null)
            {
                var hired = mercManager.GetHiredMercenaries();
                h = ReplayIO.Hash(h, hired.Count);
                for (int i = 0; i < hired.Count; i++)
                {
                    var merc = hired[i].GetComponent<MercenaryComponent>()?.LinkedMercenary;
                    if (merc == null)
                        continue;
                    h = ReplayIO.Hash(h, merc.Name);
                    h = ReplayIO.Hash(h, merc.CurrentHP);
                    h = ReplayIO.Hash(h, merc.Level);
                }
            }

            var allied = services.GetService<AlliedMonsterManager>();
            if (allied != null)
            {
                var roster = allied.AlliedMonsters;
                h = ReplayIO.Hash(h, roster.Count);
                for (int i = 0; i < roster.Count; i++)
                    h = ReplayIO.Hash(h, (int)roster[i].Job);
            }

            var monsters = Core.Scene.FindEntitiesWithTag(GameConfig.TAG_MONSTER);
            int living = 0;
            for (int i = 0; i < monsters.Count; i++)
            {
                var enemy = monsters[i].GetComponent<EnemyComponent>()?.Enemy;
                if (enemy != null && enemy.CurrentHP > 0)
                    living++;
            }
            h = ReplayIO.Hash(h, living);
            return h;
        }

        private static ulong HashWorld(ulong h)
        {
            var services = Core.Services;
            var gameState = services.GetService<GameStateService>();
            if (gameState != null)
                h = ReplayIO.Hash(h, gameState.Funds);
            var pit = services.GetService<PitWidthManager>();
            if (pit != null)
            {
                h = ReplayIO.Hash(h, pit.CurrentPitLevel);
                h = ReplayIO.Hash(h, pit.CurrentPitTier);
            }
            var time = services.GetService<InGameTimeService>();
            if (time != null)
                h = ReplayIO.Hash(h, time.AccumulatedSeconds);
            var pause = services.GetService<PauseService>();
            if (pause != null)
                h = ReplayIO.Hash(h, pause.IsPaused ? 1 : 0);
            return h;
        }
    }
}
