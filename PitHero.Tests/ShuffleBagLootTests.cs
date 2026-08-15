using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using PitHero;
using PitHero.Dining;
using PitHero.Farming;
using PitHero.Services;
using PitHero.VirtualGame;
using RolePlayingFramework.Enemies;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Inventory;
using RolePlayingFramework.Jobs.Primary;
using RolePlayingFramework.Mercenaries;
using RolePlayingFramework.Stats;
using System.Collections.Generic;

namespace PitHero.Tests
{
    /// <summary>
    /// Issue #382: shuffle-bag loot conformance. Each bag must reproduce its configured
    /// rate EXACTLY over one full cycle (that is the whole point of the marble bag),
    /// the epic pool must cycle all four PitLord items before any repeat, the Molten
    /// Titan kill must drop an epic chest at the boss tile, and tavern patrons must
    /// order dishes at inverse-price bag weights.
    /// </summary>
    [TestClass]
    [DoNotParallelize] // draws consume the shared Nez.Random stream in some paths
    public class ShuffleBagLootTests
    {
        // ── LootBagSet rate conformance ──────────────────────────────────────────

        private static int[] DrawRarityCycle(LootBagSet bags, int pitLevel, System.Random rng, int draws)
        {
            var counts = new int[4]; // index = treasure level 1..3
            for (int i = 0; i < draws; i++)
                counts[bags.DrawCaveTreasureLevel(pitLevel, (float)rng.NextDouble())]++;
            return counts;
        }

        [TestMethod]
        public void CaveRarity_NonBoss16To25_ExactCompositionPer20()
        {
            var bags = new LootBagSet();
            var counts = DrawRarityCycle(bags, 18, new System.Random(7), 20);
            Assert.AreEqual(2, counts[3], "16-25 non-boss band: exactly 2 rare per 20");
            Assert.AreEqual(7, counts[2], "16-25 non-boss band: exactly 7 uncommon per 20");
            Assert.AreEqual(11, counts[1], "16-25 non-boss band: exactly 11 normal per 20");
        }

        [TestMethod]
        public void CaveRarity_BossFloor25_ExactCompositionPer20()
        {
            var bags = new LootBagSet();
            var counts = DrawRarityCycle(bags, 25, new System.Random(11), 20);
            Assert.AreEqual(4, counts[3], "boss 20/25 band: exactly 4 rare per 20");
            Assert.AreEqual(10, counts[2], "boss 20/25 band: exactly 10 uncommon per 20");
            Assert.AreEqual(6, counts[1], "boss 20/25 band: exactly 6 normal per 20");
        }

        [TestMethod]
        public void CaveRarity_Levels1To10_AlwaysNormal()
        {
            var bags = new LootBagSet();
            var rng = new System.Random(3);
            for (int i = 0; i < 30; i++)
                Assert.AreEqual(1, bags.DrawCaveTreasureLevel(1 + i % 10, (float)rng.NextDouble()));
        }

        [TestMethod]
        public void SeedGate_ExactlyOnePerTen()
        {
            var bags = new LootBagSet();
            var rng = new System.Random(21);
            for (int cycle = 0; cycle < 3; cycle++)
            {
                int seeds = 0;
                for (int i = 0; i < 10; i++)
                    if (bags.DrawSeedGate((float)rng.NextDouble())) seeds++;
                Assert.AreEqual(1, seeds, $"cycle {cycle}: exactly 1 seed chest per 10 eligible rolls");
            }
        }

        [TestMethod]
        public void SeedType_AllCropsCycleBeforeRepeat()
        {
            var bags = new LootBagSet();
            var rng = new System.Random(5);
            var seen = new HashSet<CropType>();
            for (int i = 0; i < CropTypeInfo.Count; i++)
                Assert.IsTrue(seen.Add(bags.DrawSeedType((float)rng.NextDouble())),
                    "every crop type must appear exactly once per rotation");
        }

        [TestMethod]
        public void ConsumableGate_ExactlyThreePerFive()
        {
            var bags = new LootBagSet();
            var rng = new System.Random(13);
            for (int cycle = 0; cycle < 4; cycle++)
            {
                int consumables = 0;
                for (int i = 0; i < 5; i++)
                    if (bags.DrawConsumableGate((float)rng.NextDouble())) consumables++;
                Assert.AreEqual(3, consumables, $"cycle {cycle}: exactly 3 consumables per 5 L1 chests");
            }
        }

        [TestMethod]
        public void PotionType_StrictRotationOfThree()
        {
            var bags = new LootBagSet();
            var rng = new System.Random(17);
            var seen = new HashSet<int>();
            for (int i = 0; i < 3; i++)
                Assert.IsTrue(seen.Add(bags.DrawPotionType((float)rng.NextDouble())),
                    "HP/MP/Mix must each appear once per rotation");
        }

        [TestMethod]
        public void AccessoryShare_ExactlyOnePerTen_PerRarityPool()
        {
            var bags = new LootBagSet();
            var rng = new System.Random(29);
            for (int level = 1; level <= 3; level++)
            {
                int accessories = 0;
                for (int i = 0; i < 10; i++)
                    if (bags.DrawAccessoryShare(level, (float)rng.NextDouble())) accessories++;
                Assert.AreEqual(1, accessories, $"treasure level {level}: exactly 1 accessory per 10 equipment rolls");
            }
        }

        [TestMethod]
        public void EpicItems_AllFourPitLordPiecesBeforeAnyRepeat()
        {
            var bags = new LootBagSet();
            var rng = new System.Random(31);
            for (int cycle = 0; cycle < 2; cycle++)
            {
                var names = new HashSet<string>();
                for (int i = 0; i < 4; i++)
                {
                    var item = bags.DrawEpicItem((float)rng.NextDouble());
                    Assert.IsInstanceOfType(item, typeof(Gear));
                    Assert.AreEqual(ItemRarity.Epic, ((Gear)item).Rarity);
                    Assert.IsTrue(names.Add(item.Name), $"cycle {cycle}: {item.Name} repeated before the epic pool was exhausted");
                }
            }
        }

        [TestMethod]
        public void LootShuffleService_DrawEpicItem_CyclesAllFour()
        {
            var service = new LootShuffleService();
            var names = new HashSet<string>();
            for (int i = 0; i < 4; i++)
                Assert.IsTrue(names.Add(service.DrawEpicItem().Name), "service epic draw repeated early");
        }

        // ── Boss epic chest (virtual parity) ─────────────────────────────────────

        [TestMethod]
        public void MoltenTitanKill_SpawnsEpicChestAtBossTile()
        {
            var world = new VirtualWorldState();
            world.RegeneratePit(25);

            var hero = new Hero("EpicTester", new Knight(), 60, new StatBlock(60, 40, 60, 20));
            var bag = new ItemBag();
            var partyView = new VirtualBattlePartyView(hero, bag);
            var runner = new VirtualBattleRunner(world, partyView);
            runner.SetHeroAlly(hero);
            runner.SetMercenaries(new List<Mercenary>(0));
            runner.LootBags = new LootBagSet();

            var heroPos = new Point(world.PitBounds.X + 2, world.PitBounds.Y + 2);
            world.MoveHeroTo(heroPos);
            var bossTile = new Point(heroPos.X + 1, heroPos.Y);
            var boss = EnemyFactory.Create(EnemyId.MoltenTitan, level: 22);
            world.AddMonster(bossTile, boss);

            var metrics = runner.RunAdjacentBattle();

            Assert.IsNotNull(metrics, "battle should have run");
            Assert.IsFalse(world.HasLivingBoss(), "a level-60 Knight must defeat the Molten Titan");
            Assert.IsTrue(world.TryGetTreasureAt(bossTile, out var item),
                "an epic chest must spawn at the boss tile after a Molten Titan kill");
            Assert.IsInstanceOfType(item, typeof(Gear));
            Assert.AreEqual(ItemRarity.Epic, ((Gear)item).Rarity, "the boss chest must contain an Epic item");
        }

        [TestMethod]
        public void NonMainBossKill_SpawnsNoChest()
        {
            var world = new VirtualWorldState();
            world.RegeneratePit(5);

            var hero = new Hero("BossTester", new Knight(), 40, new StatBlock(50, 30, 50, 10));
            var partyView = new VirtualBattlePartyView(hero, new ItemBag());
            var runner = new VirtualBattleRunner(world, partyView);
            runner.SetHeroAlly(hero);
            runner.SetMercenaries(new List<Mercenary>(0));
            runner.LootBags = new LootBagSet();

            var heroPos = new Point(world.PitBounds.X + 2, world.PitBounds.Y + 2);
            world.MoveHeroTo(heroPos);
            var bossTile = new Point(heroPos.X + 1, heroPos.Y);
            world.AddMonster(bossTile, EnemyFactory.Create(EnemyId.StoneGuardian, level: 8));

            runner.RunAdjacentBattle();

            Assert.IsFalse(world.HasLivingBoss(), "StoneGuardian should be defeated");
            Assert.IsFalse(world.TryGetTreasureAt(bossTile, out _),
                "only the biome main boss (Molten Titan) drops an epic chest");
        }

        // ── Tavern dish bag ──────────────────────────────────────────────────────

        private static int ExpectedMarbles(DishType dish, int maxPrice)
            => System.Math.Max(1, (int)System.Math.Round((float)maxPrice / DishConfig.GetPrice(dish)));

        [TestMethod]
        public void PickPatronDish_FullMenu_MatchesInversePriceComposition()
        {
            var coordinator = new KitchenTaskCoordinator(null, new BuildingService(), 240, 12);

            var allDishes = new List<DishType>(DishTypeInfo.Count);
            for (int d = 0; d < DishTypeInfo.Count; d++)
                allDishes.Add((DishType)d);

            int maxPrice = 0;
            for (int d = 0; d < DishTypeInfo.Count; d++)
                maxPrice = System.Math.Max(maxPrice, DishConfig.GetPrice((DishType)d));

            int totalMarbles = 0;
            for (int d = 0; d < DishTypeInfo.Count; d++)
                totalMarbles += ExpectedMarbles((DishType)d, maxPrice);

            // One full bag cycle with everything orderable: draw counts must equal the
            // inverse-price marble composition exactly.
            var counts = new Dictionary<DishType, int>();
            for (int i = 0; i < totalMarbles; i++)
            {
                var dish = coordinator.PickPatronDish(allDishes);
                counts.TryGetValue(dish, out int c);
                counts[dish] = c + 1;
            }

            for (int d = 0; d < DishTypeInfo.Count; d++)
            {
                var dish = (DishType)d;
                counts.TryGetValue(dish, out int actual);
                Assert.AreEqual(ExpectedMarbles(dish, maxPrice), actual,
                    $"{dish} ({DishConfig.GetPrice(dish)}g) draw count must match its inverse-price marble count");
            }
        }

        [TestMethod]
        public void PickPatronDish_SingleOrderableDish_AlwaysReturnsIt()
        {
            var coordinator = new KitchenTaskCoordinator(null, new BuildingService(), 240, 12);
            var onlyDish = new List<DishType> { DishType.RoastedOnionSkewers };
            for (int i = 0; i < 5; i++)
                Assert.AreEqual(DishType.RoastedOnionSkewers, coordinator.PickPatronDish(onlyDish));
        }
    }
}
