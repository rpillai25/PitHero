using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Stats;

namespace PitHero.Tests
{
    /// <summary>
    /// Tests for ItemWeaknessRanking — the shared key-extraction helpers used by both
    /// ExcessItemSellSelector and SecondChanceMerchantVault eviction (issue #373).
    /// Behaviour-preservation of ExcessItemSellSelector is verified transitively by
    /// AutoSellExcessItemsTests running unmodified after Step 1.
    /// </summary>
    [TestClass]
    public class ItemWeaknessRankingTests
    {
        // ── Helpers ───────────────────────────────────────────────────────────────

        /// <summary>Gear with a given gear score (attack bonus only, other stats zero).</summary>
        private static Gear MakeGear(string name, int score, ItemRarity rarity = ItemRarity.Normal, int price = 100)
            => new Gear(name, ItemKind.WeaponSword, rarity, "desc", price,
                        new StatBlock(0, 0, 0, 0), atk: score);

        private sealed class TestPotion : Consumable
        {
            private readonly int _hp;
            private readonly int _mp;
            public TestPotion(string name, int price, int hp, int mp = 0)
                : base(name, ItemRarity.Normal, "desc", price, hp, mp)
            { _hp = hp; _mp = mp; }
            public override Consumable CreateFreshInstance() => new TestPotion(Name, Price, _hp, _mp);
        }

        // ── GearKey ───────────────────────────────────────────────────────────────

        [TestMethod]
        public void GearKey_LowerScoreGear_SmallerKeyA()
        {
            var weak   = MakeGear("Weak",   score: 1);
            var strong = MakeGear("Strong", score: 50);

            ItemWeaknessRanking.GearKey(weak,   out long weakA,   out _, out _);
            ItemWeaknessRanking.GearKey(strong, out long strongA, out _, out _);

            Assert.IsTrue(weakA < strongA, "Weaker gear should have a smaller key-A (gear score)");
        }

        [TestMethod]
        public void GearKey_LowerRarityGear_SmallerKeyB_WhenScoreTied()
        {
            var normalGear    = MakeGear("Normal", score: 10, rarity: ItemRarity.Normal);
            var uncommonGear  = MakeGear("Uncommon", score: 10, rarity: ItemRarity.Uncommon);

            ItemWeaknessRanking.GearKey(normalGear,   out long aA, out long aB, out _);
            ItemWeaknessRanking.GearKey(uncommonGear, out long bA, out long bB, out _);

            Assert.AreEqual(aA, bA, "Same score → same key-A");
            Assert.IsTrue(aB < bB, "Normal rarity is lower enum value → smaller key-B");
        }

        [TestMethod]
        public void GearKey_LowerPriceGear_SmallerKeyC_WhenScoreAndRarityTied()
        {
            var cheap     = MakeGear("Cheap",     score: 10, price: 50);
            var expensive = MakeGear("Expensive", score: 10, price: 200);

            ItemWeaknessRanking.GearKey(cheap,     out long aA, out long aB, out long aC);
            ItemWeaknessRanking.GearKey(expensive, out long bA, out long bB, out long bC);

            Assert.AreEqual(aA, bA);
            Assert.AreEqual(aB, bB);
            Assert.IsTrue(aC < bC, "Cheaper gear → smaller key-C (sell price)");
        }

        // ── ConsumableKey ─────────────────────────────────────────────────────────

        [TestMethod]
        public void ConsumableKey_LowerRestoreEffect_SmallerKeyA()
        {
            var weak   = new TestPotion("Weak",   20, hp: 30);
            var strong = new TestPotion("Strong", 20, hp: 200);

            ItemWeaknessRanking.ConsumableKey(weak,   1, out long wA, out _, out _);
            ItemWeaknessRanking.ConsumableKey(strong, 1, out long sA, out _, out _);

            Assert.IsTrue(wA < sA, "Lower HP restore → smaller key-A");
        }

        [TestMethod]
        public void ConsumableKey_MPPlusHPCombined_DriveKeyA()
        {
            var hpOnly    = new TestPotion("HPOnly",    20, hp: 100, mp: 0);
            var combined  = new TestPotion("Combined",  20, hp: 50,  mp: 60); // 50+60=110 > 100

            ItemWeaknessRanking.ConsumableKey(hpOnly,   1, out long hA, out _, out _);
            ItemWeaknessRanking.ConsumableKey(combined, 1, out long cA, out _, out _);

            Assert.IsTrue(hA < cA, "HP+MP combined restore drives key-A; combined potion should be 'stronger'");
        }

        [TestMethod]
        public void ConsumableKey_LowerSellPrice_SmallerKeyB_WhenRestoreTied()
        {
            var cheap     = new TestPotion("Cheap",     price: 10, hp: 50);
            var expensive = new TestPotion("Expensive", price: 50, hp: 50);

            ItemWeaknessRanking.ConsumableKey(cheap,     1, out long aA, out long aB, out _);
            ItemWeaknessRanking.ConsumableKey(expensive, 1, out long bA, out long bB, out _);

            Assert.AreEqual(aA, bA);
            Assert.IsTrue(aB < bB, "Cheaper consumable → smaller key-B (sell price)");
        }

        [TestMethod]
        public void ConsumableKey_LowerStackCount_SmallerKeyC_WhenRestoreAndPriceTied()
        {
            var potion = new TestPotion("Potion", 20, hp: 50);

            // stackCount is passed explicitly (vault uses stack.Quantity, not c.StackCount)
            ItemWeaknessRanking.ConsumableKey(potion, stackCount: 1,   out long aA, out long aB, out long aC);
            ItemWeaknessRanking.ConsumableKey(potion, stackCount: 999, out long bA, out long bB, out long bC);

            Assert.AreEqual(aA, bA);
            Assert.AreEqual(aB, bB);
            Assert.IsTrue(aC < bC, "Smaller stack count → smaller key-C");
        }

        // ── IsWeaker ─────────────────────────────────────────────────────────────

        [TestMethod]
        public void IsWeaker_SmallerA_IsWeaker()
        {
            // (a=1) vs best (a=10): a < bestA → weaker
            bool result = ItemWeaknessRanking.IsWeaker(1, 0, 0, 5, 10, 0, 0, 0);
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsWeaker_LargerA_IsNotWeaker()
        {
            bool result = ItemWeaknessRanking.IsWeaker(10, 0, 0, 5, 1, 0, 0, 0);
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsWeaker_TiedA_SmallerB_IsWeaker()
        {
            bool result = ItemWeaknessRanking.IsWeaker(5, 1, 0, 3, 5, 10, 0, 0);
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsWeaker_TiedAB_SmallerC_IsWeaker()
        {
            bool result = ItemWeaknessRanking.IsWeaker(5, 5, 1, 3, 5, 5, 10, 0);
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsWeaker_FullKeyTie_LowerIndexIsWeaker()
        {
            // All keys equal — lower index wins (is "weaker")
            bool lowerIndexResult = ItemWeaknessRanking.IsWeaker(5, 5, 5, 0, 5, 5, 5, 1);
            Assert.IsTrue(lowerIndexResult, "Lower index is weaker when all keys tie");

            bool higherIndexResult = ItemWeaknessRanking.IsWeaker(5, 5, 5, 1, 5, 5, 5, 0);
            Assert.IsFalse(higherIndexResult, "Higher index is not weaker when all keys tie");
        }

        [TestMethod]
        public void IsWeaker_FullKeyTie_IncomingVsExisting_ExistingIsWeaker()
        {
            // The existing stack is at index 0, incoming is at int.MaxValue.
            // On a full key tie, the existing (lower index) is selected for eviction.
            int existingIndex = 0;
            int incomingIndex = int.MaxValue;

            bool incomingWeaker = ItemWeaknessRanking.IsWeaker(
                5, 5, 5, incomingIndex,
                5, 5, 5, existingIndex);

            Assert.IsFalse(incomingWeaker,
                "Incoming (int.MaxValue) is not weaker than existing (index 0) on a full key tie — existing gets evicted");
        }
    }
}
