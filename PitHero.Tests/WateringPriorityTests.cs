using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nez.Persistence.Binary;
using PitHero.Farming;
using PitHero.Services;
using System;
using System.IO;

namespace PitHero.Tests
{
    /// <summary>The crop watering priority (issue #450): the sim-side order, unlock promotion, the claim pick and the v41 save section.</summary>
    [TestClass]
    public class WateringPriorityTests
    {
        private static int[] Order(GameStateService state) => state.CopyWateringPriorityOrderArray();

        [TestMethod]
        public void Default_IsHighestTierFirst()
        {
            var order = GameStateService.DefaultWateringPriorityOrder();
            Assert.AreEqual(CropTypeInfo.Count, order.Length);
            Assert.IsTrue(GameStateService.IsWateringPriorityPermutation(order));
            Assert.AreEqual((int)CropType.AppleTree, order[0], "The last crop to unlock is watered first");
            Assert.AreEqual((int)CropType.Wheat, order[order.Length - 1], "A starting crop is watered last");
            for (int i = 1; i < order.Length; i++)
                Assert.IsTrue(CropUnlockConfig.GetTier((CropType)order[i - 1]) >= CropUnlockConfig.GetTier((CropType)order[i]),
                    "Tiers never rise down the default list");

            var state = new GameStateService();
            CollectionAssert.AreEqual(order, Order(state), "A new game state starts on the default");
        }

        [TestMethod]
        public void Swap_ExchangesTwoCrops_AndIgnoresBadPairs()
        {
            var state = new GameStateService();
            int rankWheat = state.GetWateringRank(CropType.Wheat);
            int rankCorn = state.GetWateringRank(CropType.Corn);
            int version = state.WateringPriorityVersion;

            Assert.IsTrue(state.SwapWateringPriority(CropType.Wheat, CropType.Corn));
            Assert.AreEqual(rankCorn, state.GetWateringRank(CropType.Wheat));
            Assert.AreEqual(rankWheat, state.GetWateringRank(CropType.Corn));
            Assert.IsTrue(state.WateringPriorityVersion > version, "A change bumps the version the panel watches");
            Assert.IsTrue(GameStateService.IsWateringPriorityPermutation(Order(state)));

            var before = Order(state);
            Assert.IsFalse(state.SwapWateringPriority(CropType.Corn, CropType.Corn), "Identical pair");
            Assert.IsFalse(state.SwapWateringPriority((CropType)99, CropType.Corn), "Unknown ordinal");
            Assert.IsFalse(state.SwapWateringPriority((CropType)(-1), CropType.Corn), "Negative ordinal");
            CollectionAssert.AreEqual(before, Order(state));
        }

        [TestMethod]
        public void Promote_MovesCropToTop_ShiftingTheRestDown()
        {
            var state = new GameStateService();
            var before = Order(state);
            state.PromoteWateringPriority(CropType.Wheat);
            var after = Order(state);

            Assert.AreEqual((int)CropType.Wheat, after[0]);
            for (int i = 1; i < after.Length; i++)
                Assert.AreEqual(before[i - 1], after[i], "Everything above Wheat moved down one, in order");
            Assert.AreEqual(0, state.GetWateringRank(CropType.Wheat));
        }

        [TestMethod]
        public void UnlockPromotion_PairOrderMatchesTheDefault()
        {
            // Player put Wheat on top; Tomato and Eggplant then unlock together. Both jump above Wheat,
            // in the same relative order the default list gives them.
            var state = new GameStateService();
            state.PromoteWateringPriority(CropType.Wheat);
            int gained = (1 << (int)CropType.Tomato) | (1 << (int)CropType.Eggplant);
            CropUnlockTracker.PromoteUnlockedInWateringOrder(state, gained);

            var defaults = GameStateService.DefaultWateringPriorityOrder();
            int defaultTomato = Array.IndexOf(defaults, (int)CropType.Tomato);
            int defaultEggplant = Array.IndexOf(defaults, (int)CropType.Eggplant);
            var order = Order(state);
            Assert.AreEqual(defaultEggplant < defaultTomato ? (int)CropType.Eggplant : (int)CropType.Tomato, order[0]);
            Assert.AreEqual(defaultEggplant < defaultTomato ? (int)CropType.Tomato : (int)CropType.Eggplant, order[1]);
            Assert.AreEqual((int)CropType.Wheat, order[2]);

            var unchanged = Order(state);
            CropUnlockTracker.PromoteUnlockedInWateringOrder(state, 0);
            CropUnlockTracker.PromoteUnlockedInWateringOrder(null, gained); // no state: no-op, no throw
            CollectionAssert.AreEqual(unchanged, Order(state));
        }

        [TestMethod]
        public void SetOrder_RejectsNonPermutations()
        {
            var state = new GameStateService();
            state.PromoteWateringPriority(CropType.Wheat);

            var bad = GameStateService.DefaultWateringPriorityOrder();
            bad[1] = bad[0]; // duplicate
            state.SetWateringPriorityOrder(bad);
            CollectionAssert.AreEqual(GameStateService.DefaultWateringPriorityOrder(), Order(state), "A corrupt order restores the default");

            state.PromoteWateringPriority(CropType.Corn);
            state.SetWateringPriorityOrder(new int[3]);
            CollectionAssert.AreEqual(GameStateService.DefaultWateringPriorityOrder(), Order(state), "Short array");

            state.PromoteWateringPriority(CropType.Corn);
            state.ResetWateringPriority();
            CollectionAssert.AreEqual(GameStateService.DefaultWateringPriorityOrder(), Order(state), "New hero");
        }

        [TestMethod]
        public void SelectBestRankedIndex_PicksAmongTheTopRankOnly()
        {
            var ranks = new[] { 5, 2, 9, 2, 2, 7 };
            Assert.AreEqual(1, FarmTaskCoordinator.SelectBestRankedIndex(ranks, ranks.Length, 0f), "Front of the best-ranked entries");
            Assert.AreEqual(3, FarmTaskCoordinator.SelectBestRankedIndex(ranks, ranks.Length, 0.5f), "Middle of them");
            Assert.AreEqual(4, FarmTaskCoordinator.SelectBestRankedIndex(ranks, ranks.Length, 1f), "Back of them");
            Assert.AreEqual(0, FarmTaskCoordinator.SelectBestRankedIndex(new[] { 3 }, 1, 0.9f));
            Assert.AreEqual(1, FarmTaskCoordinator.SelectBestRankedIndex(new[] { 4, 1, 1 }, 2, 1f), "Only the first count entries count");
            Assert.AreEqual(-1, FarmTaskCoordinator.SelectBestRankedIndex(ranks, 0, 0f));
            Assert.AreEqual(-1, FarmTaskCoordinator.SelectBestRankedIndex(null, 3, 0f));
        }

        private static byte[] Write(SaveData data)
        {
            var ms = new MemoryStream();
            using (var writer = new BinaryPersistableWriter(ms))
                writer.Write(data);
            return ms.ToArray();
        }

        private static SaveData Read(byte[] bytes)
        {
            var loaded = new SaveData();
            using (var rdr = new BinaryPersistableReader(new MemoryStream(bytes)))
                rdr.ReadPersistableInto(loaded);
            return loaded;
        }

        /// <summary>Bytes of section 55: a count int plus one int per crop.</summary>
        private const int V41Tail = 4 + 4 * CropTypeInfo.Count;

        [TestMethod]
        public void SaveData_V41_WateringOrder_RoundTrips()
        {
            var state = new GameStateService();
            state.PromoteWateringPriority(CropType.Corn);
            state.SwapWateringPriority(CropType.Wheat, CropType.AppleTree);

            var original = new SaveData { HeroName = "Waterer", WateringPriorityOrder = state.CopyWateringPriorityOrderArray() };
            var loaded = Read(Write(original));
            CollectionAssert.AreEqual(original.WateringPriorityOrder, loaded.WateringPriorityOrder);

            var restored = new GameStateService();
            restored.SetWateringPriorityOrder(loaded.WateringPriorityOrder);
            CollectionAssert.AreEqual(Order(state), Order(restored));
        }

        /// <summary>
        /// Backwards compatibility for the v41 bump: a v40 file ends before section 55, so the hero
        /// waters highest tier first. Built by cutting section 55 off a v41 stream and patching the
        /// version header down to 40.
        /// </summary>
        [TestMethod]
        public void SaveData_V40_File_LoadsDefaultWateringOrder()
        {
            var original = new SaveData { HeroName = "OldFarmer", Level = 5 };
            var state = new GameStateService();
            state.PromoteWateringPriority(CropType.Wheat);
            original.WateringPriorityOrder = state.CopyWateringPriorityOrderArray(); // dropped with the bytes

            byte[] v41 = Write(original);
            var v40 = new byte[v41.Length - V41Tail];
            Array.Copy(v41, v40, v40.Length);
            v40[0] = 40; v40[1] = 0; v40[2] = 0; v40[3] = 0;

            var loaded = Read(v40);
            CollectionAssert.AreEqual(GameStateService.DefaultWateringPriorityOrder(), loaded.WateringPriorityOrder);
            Assert.AreEqual("OldFarmer", loaded.HeroName);
            Assert.AreEqual(5, loaded.Level);
        }

        [TestMethod]
        public void SaveData_CorruptWateringOrder_FallsBackToDefault()
        {
            var bad = GameStateService.DefaultWateringPriorityOrder();
            bad[0] = bad[1];
            var loaded = Read(Write(new SaveData { WateringPriorityOrder = bad }));
            CollectionAssert.AreEqual(GameStateService.DefaultWateringPriorityOrder(), loaded.WateringPriorityOrder);
        }
    }
}
