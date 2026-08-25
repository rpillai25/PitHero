using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero;
using PitHero.Services;
using PitHero.Util;

namespace PitHero.Tests
{
    /// <summary>
    /// Escalating building purchase prices: each paid building of a type multiplies the next one's
    /// cost by GameConfig.BuildingCostGrowthFactor, rounded to the nearest 5 G and clamped at the
    /// per-type maximum. The free starter placed by SetupNewGameFarmContent is excluded.
    /// </summary>
    [TestClass]
    public class BuildingCostCurveTests
    {
        #region Monster House curve

        [TestMethod]
        public void MonsterHouse_FreeStarterPlaced_NextCostsBasePrice()
        {
            // One placed = the free new-game starter, so the first purchase is still base price
            Assert.AreEqual(100, BuildingConfig.GetCost(BuildingType.MonsterHouse, 1));
        }

        [TestMethod]
        public void MonsterHouse_NoneOrStarterPlaced_BothCostBasePrice()
        {
            Assert.AreEqual(BuildingConfig.GetCost(BuildingType.MonsterHouse, 1),
                            BuildingConfig.GetCost(BuildingType.MonsterHouse, 0),
                            "The free starter must not push the first purchase up the curve");
        }

        [TestMethod]
        public void MonsterHouse_FollowsGeometricCurve()
        {
            var expected = new[] { 100, 150, 225, 340, 505, 760, 1000 };
            for (int i = 0; i < expected.Length; i++)
            {
                int placed = i + GameConfig.BuildingFreeStarterCount;
                Assert.AreEqual(expected[i], BuildingConfig.GetCost(BuildingType.MonsterHouse, placed),
                    $"Monster House #{i + 1} (after the free starter) priced wrong");
            }
        }

        [TestMethod]
        public void MonsterHouse_ClampsAtMax()
        {
            for (int placed = 7; placed < 40; placed++)
                Assert.AreEqual(GameConfig.BuildingCostMonsterHouseMax,
                    BuildingConfig.GetCost(BuildingType.MonsterHouse, placed),
                    "Monster House cost must never exceed its 1000 G ceiling");
        }

        #endregion

        #region Crop Storage curve

        [TestMethod]
        public void CropStorage_FreeStarterPlaced_NextCostsBasePrice()
        {
            Assert.AreEqual(50, BuildingConfig.GetCost(BuildingType.CropStorage, 1));
        }

        [TestMethod]
        public void CropStorage_FollowsGeometricCurve()
        {
            var expected = new[] { 50, 75, 115, 170, 255, 380, 500 };
            for (int i = 0; i < expected.Length; i++)
            {
                int placed = i + GameConfig.BuildingFreeStarterCount;
                Assert.AreEqual(expected[i], BuildingConfig.GetCost(BuildingType.CropStorage, placed),
                    $"Crop Storage #{i + 1} (after the free starter) priced wrong");
            }
        }

        [TestMethod]
        public void CropStorage_ClampsAtMax()
        {
            for (int placed = 7; placed < 40; placed++)
                Assert.AreEqual(GameConfig.BuildingCostCropStorageMax,
                    BuildingConfig.GetCost(BuildingType.CropStorage, placed),
                    "Crop Storage cost must never exceed its 500 G ceiling");
        }

        #endregion

        #region Curve invariants

        [TestMethod]
        public void Cost_IsMonotonicNonDecreasing()
        {
            foreach (var type in new[] { BuildingType.MonsterHouse, BuildingType.CropStorage })
            {
                int prev = 0;
                for (int placed = 0; placed < 25; placed++)
                {
                    int cost = BuildingConfig.GetCost(type, placed);
                    Assert.IsTrue(cost >= prev, $"{type} price dropped from {prev} to {cost} at count {placed}");
                    prev = cost;
                }
            }
        }

        [TestMethod]
        public void Cost_NegativeCountTreatedAsZero()
        {
            Assert.AreEqual(BuildingConfig.GetCost(BuildingType.CropStorage, 0),
                            BuildingConfig.GetCost(BuildingType.CropStorage, -3));
        }

        [TestMethod]
        public void BuildSellCycle_IsNeverProfitable()
        {
            // Buy the Nth building, then sell it: the refund is the flat base price, and after the
            // sale the count is back to N-1 so re-buying costs the same as it just did. Churning
            // buildings must never be farmable for gold. At the base rung the cycle is exactly
            // break-even (buy 100, sell 100) — no gain, so the loop is pointless rather than exploitable.
            foreach (var type in new[] { BuildingType.MonsterHouse, BuildingType.CropStorage })
            {
                for (int placed = 1; placed < 15; placed++)
                {
                    int paid   = BuildingConfig.GetCost(type, placed - 1);  // price of the Nth building
                    int refund = BuildingConfig.GetSellPrice(type);
                    int rebuy  = BuildingConfig.GetCost(type, placed - 1);  // price once the count drops back
                    Assert.IsTrue(refund <= paid,  $"{type} refund at count {placed} exceeds what was paid");
                    Assert.IsTrue(refund <= rebuy, $"{type} could be churned for profit at count {placed}");
                }
            }
        }

        #endregion

        #region Sell prices

        [TestMethod]
        public void SellPrice_IsAlwaysTheInitialBasePrice()
        {
            Assert.AreEqual(GameConfig.BuildingCostMonsterHouseBase,
                BuildingConfig.GetSellPrice(BuildingType.MonsterHouse));
            Assert.AreEqual(GameConfig.BuildingCostCropStorageBase,
                BuildingConfig.GetSellPrice(BuildingType.CropStorage));
        }

        [TestMethod]
        public void SellPrice_NeverExceedsPurchasePrice()
        {
            // Buying high and selling low: past the first purchase, a sale never returns what was paid.
            foreach (var type in new[] { BuildingType.MonsterHouse, BuildingType.CropStorage })
            {
                for (int placed = 1; placed < 15; placed++)
                {
                    int paid = BuildingConfig.GetCost(type, placed - 1);
                    Assert.IsTrue(BuildingConfig.GetSellPrice(type) <= paid,
                        $"{type} sale at count {placed} refunds more than the building cost");
                }
            }
        }

        [TestMethod]
        public void RepeatedSellOfSameBuilding_PaysOutExactlyOnce()
        {
            // The sell confirmation is non-modal, so the player can stack several sell dialogs on one
            // building and confirm them all. RemoveBuilding reports whether IT did the removing, and
            // the sell handler pays only on a true return — otherwise each extra dialog mints gold.
            var svc = new BuildingService();
            var house = new PlacedBuilding { Type = BuildingType.MonsterHouse, UniqueId = svc.AllocateId() };
            svc.AddBuilding(house);

            int gold = 0;
            for (int confirmation = 0; confirmation < 5; confirmation++)
            {
                if (svc.RemoveBuilding(house))
                    gold += BuildingConfig.GetSellPrice(BuildingType.MonsterHouse);
            }

            Assert.AreEqual(GameConfig.BuildingCostMonsterHouseBase, gold,
                "Five confirmations on one building must pay the refund exactly once");
            Assert.AreEqual(0, svc.GetCountOfType(BuildingType.MonsterHouse));
        }

        [TestMethod]
        public void AdversarialBuySellChurn_NeverIncreasesGold()
        {
            // Drive the worst-case loop the economy allows: always buy at the current count, always
            // sell straight back. Gold must never climb above where it started, at any rung.
            foreach (var type in new[] { BuildingType.MonsterHouse, BuildingType.CropStorage })
            {
                var svc = new BuildingService();
                svc.AddBuilding(new PlacedBuilding { Type = type, UniqueId = svc.AllocateId() }); // free starter

                const int startingGold = 100000;
                int gold = startingGold;
                var owned = new System.Collections.Generic.Stack<PlacedBuilding>();

                for (int step = 0; step < 200; step++)
                {
                    // Buy when affordable, otherwise sell one back.
                    int price = BuildingConfig.GetCost(type, svc.GetCountOfType(type));
                    if (gold >= price && owned.Count < 12)
                    {
                        gold -= price;
                        var b = new PlacedBuilding { Type = type, UniqueId = svc.AllocateId() };
                        svc.AddBuilding(b);
                        owned.Push(b);
                    }
                    else if (owned.Count > 0)
                    {
                        var b = owned.Pop();
                        if (svc.RemoveBuilding(b))
                            gold += BuildingConfig.GetSellPrice(type);
                    }

                    Assert.IsTrue(gold <= startingGold,
                        $"{type} churn produced gold at step {step}: {gold} > {startingGold}");
                }
            }
        }

        #endregion

        #region BuildingService counting

        [TestMethod]
        public void GetCountOfType_CountsOnlyMatchingType()
        {
            var svc = new BuildingService();
            svc.AddBuilding(new PlacedBuilding { Type = BuildingType.MonsterHouse, UniqueId = svc.AllocateId() });
            svc.AddBuilding(new PlacedBuilding { Type = BuildingType.CropStorage,  UniqueId = svc.AllocateId() });
            svc.AddBuilding(new PlacedBuilding { Type = BuildingType.CropStorage,  UniqueId = svc.AllocateId() });

            Assert.AreEqual(1, svc.GetCountOfType(BuildingType.MonsterHouse));
            Assert.AreEqual(2, svc.GetCountOfType(BuildingType.CropStorage));
            Assert.AreEqual(svc.MonsterHouseCount, svc.GetCountOfType(BuildingType.MonsterHouse));
            Assert.AreEqual(svc.CropStorageCount,  svc.GetCountOfType(BuildingType.CropStorage));
        }

        [TestMethod]
        public void GetCountOfType_DropsAfterRemoval()
        {
            var svc = new BuildingService();
            var storage = new PlacedBuilding { Type = BuildingType.CropStorage, UniqueId = svc.AllocateId() };
            svc.AddBuilding(storage);
            svc.AddBuilding(new PlacedBuilding { Type = BuildingType.CropStorage, UniqueId = svc.AllocateId() });
            Assert.AreEqual(2, svc.GetCountOfType(BuildingType.CropStorage));

            svc.RemoveBuilding(storage);
            Assert.AreEqual(1, svc.GetCountOfType(BuildingType.CropStorage),
                "Selling a building must lower the count so the next purchase reprices down the curve");
        }

        #endregion
    }
}
