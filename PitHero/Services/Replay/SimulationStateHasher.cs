using Nez;
using PitHero.AI;
using PitHero.ECS.Components;

namespace PitHero.Services.Replay
{
    /// <summary>
    /// Cheap FNV-1a fingerprint of the simulation, sampled every GameConfig.ReplayHashIntervalTicks.
    /// The Sim RNG state is the strongest tripwire (any extra or missing roll shifts it); the rest
    /// pins hero, party, pit and economy so a mismatch is reported the second it happens rather than
    /// minutes later. Allocation-free; for-loops only.
    /// </summary>
    public static class SimulationStateHasher
    {
        /// <summary>Hashes the live game state of the current scene.</summary>
        public static ulong Compute(long tick)
        {
            ulong h = ReplayIO.HashSeed;
            h = ReplayIO.Hash(h, tick);

            // RNG streams
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

            if (Core.Instance == null)
                return h;
            var scene = Core.Scene;
            var services = Core.Services;
            if (scene == null || services == null)
                return h;

            // Hero
            var heroEntity = scene.FindEntity("hero");
            var heroComp = heroEntity?.GetComponent<HeroComponent>();
            var hero = heroComp?.LinkedHero;
            if (hero != null)
            {
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
            }
            h = ReplayIO.Hash(h, HeroStateMachine.IsBattleInProgress ? 1 : 0);

            // Economy / pit / clock
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

            // Party
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

            // Allied monsters
            var allied = services.GetService<AlliedMonsterManager>();
            if (allied != null)
            {
                var roster = allied.AlliedMonsters;
                h = ReplayIO.Hash(h, roster.Count);
                for (int i = 0; i < roster.Count; i++)
                    h = ReplayIO.Hash(h, (int)roster[i].Job);
            }

            // Living pit monsters
            var monsters = scene.FindEntitiesWithTag(GameConfig.TAG_MONSTER);
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
    }
}
