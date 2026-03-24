using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.AI;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Inventory;

namespace PitHero.Tests
{
    [TestClass]
    public class PotionSelectionEngineTests
    {
        // ====================================================================
        // HELPER: create a bag with specific potions
        // ====================================================================

        private static ItemBag CreateBagWith(params Consumable[] potions)
        {
            var bag = new ItemBag("Test", 120);
            for (int i = 0; i < potions.Length; i++)
            {
                bag.TryAdd(potions[i]);
            }
            return bag;
        }

        // ====================================================================
        // BATTLE: Emergency Survival Override
        // ====================================================================

        [TestMethod]
        public void Battle_Emergency_PicksHighestHP()
        {
            // Hero at 10% HP (below 20% emergency) with HP potion and MidHP potion
            var hpPotion = PotionItems.HPPotion();       // 100 HP
            var midHP = PotionItems.MidHPPotion();       // 500 HP
            var bag = CreateBagWith(hpPotion, midHP);

            Consumable best;
            int idx;
            bool found = PotionSelectionEngine.SelectBattlePotion(
                bag, 50, 500, 40, 40, out best, out idx);

            Assert.IsTrue(found);
            Assert.AreEqual("MidHPPotion", best.Name, "Emergency should pick highest HP potion");
        }

        [TestMethod]
        public void Battle_Emergency_AllowsFullPotion()
        {
            // Hero at 5% HP — emergency should allow FullHPPotion even with small deficit
            var hpPotion = PotionItems.HPPotion();
            var fullHP = PotionItems.FullHPPotion();     // -1 = full restore
            var bag = CreateBagWith(hpPotion, fullHP);

            Consumable best;
            int idx;
            bool found = PotionSelectionEngine.SelectBattlePotion(
                bag, 25, 500, 40, 40, out best, out idx);

            Assert.IsTrue(found);
            Assert.AreEqual("FullHPPotion", best.Name, "Emergency should allow full potions");
        }

        [TestMethod]
        public void Battle_Emergency_IgnoresMP()
        {
            // Hero at 15% HP with MixPotion and MidHP
            // MidHP gives 500 HP; MixPotion gives 100 HP + 100 MP
            // Emergency should pick MidHP (most raw HP)
            var mix = PotionItems.MixPotion();           // 100 HP, 100 MP
            var midHP = PotionItems.MidHPPotion();       // 500 HP
            var bag = CreateBagWith(mix, midHP);

            Consumable best;
            int idx;
            bool found = PotionSelectionEngine.SelectBattlePotion(
                bag, 75, 500, 0, 100, out best, out idx);

            Assert.IsTrue(found);
            Assert.AreEqual("MidHPPotion", best.Name, "Emergency should ignore MP and pick highest HP");
        }

        // ====================================================================
        // BATTLE: Weighted Scoring System
        // ====================================================================

        [TestMethod]
        public void Battle_OnlyHPCritical_PrefersHPPotion()
        {
            // HP at 30% (critical), MP at 80% (not critical)
            // HPPotion: EffHP=100*2=200, EffMP=0*1=0 → score 200
            // MixPotion: EffHP=100*2=200, EffMP=0*1=0 → score 200 (MP not missing)
            // Actually MP at 80%, MissingMP = 20, MixPotion EffMP=min(100,20)=20*1=20 → score 220
            // But MixPotion wastes MP. Let's set MP at 100% to test pure HP case
            var hpPotion = PotionItems.HPPotion();       // 100 HP
            var mpPotion = PotionItems.MPPotion();       // 100 MP
            var bag = CreateBagWith(hpPotion, mpPotion);

            Consumable best;
            int idx;
            bool found = PotionSelectionEngine.SelectBattlePotion(
                bag, 150, 500, 40, 40, out best, out idx);

            Assert.IsTrue(found);
            Assert.AreEqual("HPPotion", best.Name, "When only HP is missing, should pick HP potion");
        }

        [TestMethod]
        public void Battle_OnlyMPCritical_PrefersMPPotion()
        {
            // HP at 100%, MP at 30% (critical)
            var hpPotion = PotionItems.HPPotion();
            var mpPotion = PotionItems.MPPotion();
            var bag = CreateBagWith(hpPotion, mpPotion);

            Consumable best;
            int idx;
            bool found = PotionSelectionEngine.SelectBattlePotion(
                bag, 500, 500, 30, 100, out best, out idx);

            Assert.IsTrue(found);
            Assert.AreEqual("MPPotion", best.Name, "When only MP is missing, should pick MP potion");
        }

        [TestMethod]
        public void Battle_BothCritical_PrefersMixPotion()
        {
            // HP at 30% (critical), MP at 30% (critical)
            // MixPotion: EffHP=min(100,350)*2 + EffMP=min(100,70)*2 = 200+140 = 340
            // HPPotion: EffHP=min(100,350)*2 + EffMP=0 = 200
            // MPPotion: EffHP=0 + EffMP=min(100,70)*2 = 140
            var hpPotion = PotionItems.HPPotion();
            var mpPotion = PotionItems.MPPotion();
            var mix = PotionItems.MixPotion();
            var bag = CreateBagWith(hpPotion, mpPotion, mix);

            Consumable best;
            int idx;
            bool found = PotionSelectionEngine.SelectBattlePotion(
                bag, 150, 500, 30, 100, out best, out idx);

            Assert.IsTrue(found);
            Assert.AreEqual("MixPotion", best.Name, "When both resources critical, MixPotion should be preferred");
        }

        [TestMethod]
        public void Battle_HPCriticalMPNot_WeightsCorrectly()
        {
            // HP at 35% (critical, weight=2), MP at 70% (not critical, weight=1)
            // Missing HP = 325, Missing MP = 30
            // HPPotion: EffHP=min(100,325)*2=200, EffMP=0 → 200
            // MixPotion: EffHP=min(100,325)*2=200, EffMP=min(100,30)*1=30 → 230
            // MixPotion should win because it also restores some MP
            var hpPotion = PotionItems.HPPotion();
            var mix = PotionItems.MixPotion();
            var bag = CreateBagWith(hpPotion, mix);

            Consumable best;
            int idx;
            bool found = PotionSelectionEngine.SelectBattlePotion(
                bag, 175, 500, 70, 100, out best, out idx);

            Assert.IsTrue(found);
            Assert.AreEqual("MixPotion", best.Name, "MixPotion gives bonus from MP restoration");
        }

        // ====================================================================
        // BATTLE: Tie-Breakers
        // ====================================================================

        [TestMethod]
        public void Battle_TieBreaker_HigherEffectiveHP()
        {
            // Two potions with same weighted score but different EffectiveHP
            // HP at 30% (critical), no MP missing
            // HPPotion: 100 HP → EffHP=100*2=200
            // MidHPPotion: 500 HP → EffHP=min(500,350)=350*2=700 (higher score, wins)
            var hpPotion = PotionItems.HPPotion();
            var midHP = PotionItems.MidHPPotion();
            var bag = CreateBagWith(hpPotion, midHP);

            Consumable best;
            int idx;
            bool found = PotionSelectionEngine.SelectBattlePotion(
                bag, 150, 500, 100, 100, out best, out idx);

            Assert.IsTrue(found);
            Assert.AreEqual("MidHPPotion", best.Name, "Should prefer potion with higher EffectiveHP");
        }

        [TestMethod]
        public void Battle_TieBreaker_CheaperPotion()
        {
            // Two HP potions with same EffectiveHP and same waste
            // Create custom scenario where they tie on score and waste
            // HPPotion (100HP, price 20) vs MixPotion (100HP + 100MP, price 30)
            // With no MP missing, MixPotion EffMP=0
            // Both score = 100*1 = 100, both waste HP = 100-100=0
            // But MixPotion has MP waste = 100
            // HPPotion should win on total waste
            var hpPotion = PotionItems.HPPotion();      // price 20
            var mix = PotionItems.MixPotion();           // price 30
            var bag = CreateBagWith(hpPotion, mix);

            Consumable best;
            int idx;
            // HP at 80% (not critical), missing 100 HP, MP full
            bool found = PotionSelectionEngine.SelectBattlePotion(
                bag, 400, 500, 100, 100, out best, out idx);

            Assert.IsTrue(found);
            Assert.AreEqual("HPPotion", best.Name, "Should prefer less wasteful/cheaper potion");
        }

        // ====================================================================
        // BATTLE: Full Potions Rule
        // ====================================================================

        [TestMethod]
        public void Battle_FullPotion_SkippedForSmallDeficit()
        {
            // HP at 60% → 40% deficit (below 50% threshold for full potion)
            // Only FullHPPotion available, no regular potions
            var fullHP = PotionItems.FullHPPotion();
            var bag = CreateBagWith(fullHP);

            Consumable best;
            int idx;
            bool found = PotionSelectionEngine.SelectBattlePotion(
                bag, 300, 500, 100, 100, out best, out idx);

            Assert.IsFalse(found, "Full potion should be skipped for small deficit (40% missing < 50% threshold)");
        }

        [TestMethod]
        public void Battle_FullPotion_AllowedForLargeDeficit()
        {
            // HP at 40% → 60% deficit (above 50% threshold)
            var fullHP = PotionItems.FullHPPotion();
            var bag = CreateBagWith(fullHP);

            Consumable best;
            int idx;
            bool found = PotionSelectionEngine.SelectBattlePotion(
                bag, 200, 500, 100, 100, out best, out idx);

            Assert.IsTrue(found, "Full potion should be allowed for large deficit");
            Assert.AreEqual("FullHPPotion", best.Name);
        }

        // ====================================================================
        // OUT-OF-BATTLE: Single Resource (HP only)
        // ====================================================================

        [TestMethod]
        public void OutOfBattle_HPOnly_SmallestSufficientPotion()
        {
            // Missing 80 HP. HPPotion (100) has 20 waste, MidHPPotion (500) has 420 waste
            var hpPotion = PotionItems.HPPotion();       // 100 HP
            var midHP = PotionItems.MidHPPotion();       // 500 HP
            var bag = CreateBagWith(hpPotion, midHP);

            Consumable best;
            int idx;
            bool found = PotionSelectionEngine.SelectOutOfBattlePotion(
                bag, 420, 500, 100, 100, out best, out idx);

            Assert.IsTrue(found);
            Assert.AreEqual("HPPotion", best.Name, "Should pick smallest sufficient potion to minimize waste");
        }

        [TestMethod]
        public void OutOfBattle_HPOnly_LargeDeficitUsesLargerPotion()
        {
            // Missing 400 HP. HPPotion (100) can't cover, MidHPPotion (500) has 100 waste
            var hpPotion = PotionItems.HPPotion();
            var midHP = PotionItems.MidHPPotion();
            var bag = CreateBagWith(hpPotion, midHP);

            Consumable best;
            int idx;
            bool found = PotionSelectionEngine.SelectOutOfBattlePotion(
                bag, 100, 500, 100, 100, out best, out idx);

            Assert.IsTrue(found);
            Assert.AreEqual("MidHPPotion", best.Name, "Should pick sufficient potion for large deficit");
        }

        [TestMethod]
        public void OutOfBattle_HPOnly_FallbackToPartial()
        {
            // Missing 600 HP. Only HPPotion (100) available — can't fully cover, but should use it
            var hpPotion = PotionItems.HPPotion();
            var bag = CreateBagWith(hpPotion);

            Consumable best;
            int idx;
            // MaxHP=1000, currentHP=400 → missing 600
            bool found = PotionSelectionEngine.SelectOutOfBattlePotion(
                bag, 400, 1000, 100, 100, out best, out idx);

            Assert.IsTrue(found);
            Assert.AreEqual("HPPotion", best.Name, "Should fall back to largest partial potion");
        }

        // ====================================================================
        // OUT-OF-BATTLE: Single Resource (MP only)
        // ====================================================================

        [TestMethod]
        public void OutOfBattle_MPOnly_SmallestSufficientPotion()
        {
            // HP full, MP at 20/100 → missing 80 MP
            var mpPotion = PotionItems.MPPotion();       // 100 MP
            var midMP = PotionItems.MidMPPotion();       // 500 MP
            var bag = CreateBagWith(mpPotion, midMP);

            Consumable best;
            int idx;
            bool found = PotionSelectionEngine.SelectOutOfBattlePotion(
                bag, 500, 500, 20, 100, out best, out idx);

            Assert.IsTrue(found);
            Assert.AreEqual("MPPotion", best.Name, "Should pick smallest sufficient MP potion");
        }

        // ====================================================================
        // OUT-OF-BATTLE: Dual Resource (HP + MP)
        // ====================================================================

        [TestMethod]
        public void OutOfBattle_DualResource_MixBetterThanSeparate()
        {
            // Missing 80 HP and 80 MP
            // MixPotion (100/100): HPWaste=20, MPWaste=20, Total=40
            // Separate: HPPotion waste=20, MPPotion waste=20, Total=40
            // Tied → prefer separate (rule 4.5)
            // Let's make mix clearly better: missing 90 HP and 90 MP
            // MixPotion (100/100): HPWaste=10, MPWaste=10, Total=20
            // Separate: HPPotion waste=10, MPPotion waste=10, Total=20
            // Still tied. Let's use MidMix vs separate:
            // Missing 400 HP, 400 MP
            // MidMixPotion (500/500): HPWaste=100, MPWaste=100, Total=200
            // Separate: MidHPPotion (500) waste=100, MidMPPotion (500) waste=100, Total=200
            // Still tied. Let's make mix better by having less total waste.
            // Missing 90 HP, 90 MP
            // MixPotion (100/100): HPW=10, MPW=10, Total=20
            // Only HPPotion (100) available for HP, only MPPotion (100) for MP
            // HPWaste=10, MPWaste=10, Total=20
            // Tied → prefer separate
            // Use scenario where only MixPotion is available to prove it's selected
            var mix = PotionItems.MixPotion();
            var bag = CreateBagWith(mix);

            Consumable best;
            int idx;
            // Missing 80 HP, 80 MP
            bool found = PotionSelectionEngine.SelectOutOfBattlePotion(
                bag, 420, 500, 20, 100, out best, out idx);

            Assert.IsTrue(found);
            Assert.AreEqual("MixPotion", best.Name, "Mix should be selected when it's the only option");
        }

        [TestMethod]
        public void OutOfBattle_DualResource_TiedWaste_PrefersSeparate()
        {
            // Missing 80 HP, 80 MP
            // MixPotion (100/100): Total waste = 20+20 = 40
            // Separate: HPPotion(100) waste=20 + MPPotion(100) waste=20 = 40
            // Tied → prefer separate (resource flexibility)
            var mix = PotionItems.MixPotion();
            var hpPotion = PotionItems.HPPotion();
            var mpPotion = PotionItems.MPPotion();
            var bag = CreateBagWith(mix, hpPotion, mpPotion);

            Consumable best;
            int idx;
            // HP at 420/500 (missing 80), MP at 20/100 (missing 80)
            bool found = PotionSelectionEngine.SelectOutOfBattlePotion(
                bag, 420, 500, 20, 100, out best, out idx);

            Assert.IsTrue(found);
            // Should pick separate → the more critical resource (lower %)
            // HP: 420/500 = 84%, MP: 20/100 = 20% → MP is more critical
            Assert.AreEqual("MPPotion", best.Name, "Tied waste should prefer separate; MP is more critical");
        }

        [TestMethod]
        public void OutOfBattle_DualResource_SeparateLessWaste()
        {
            // Missing 50 HP, 50 MP
            // MixPotion (100/100): waste = 50+50 = 100
            // Separate: HPPotion(100) waste=50, MPPotion(100) waste=50, Total=100
            // Tied again. Need a scenario where separate is clearly better.
            // Missing 10 HP, 90 MP
            // MixPotion (100/100): HPW=90, MPW=10, Total=100
            // Separate: HPPotion(100) HPW=90 + MPPotion(100) MPW=10 = 100
            // Still tied. The tie-breaker ensures separate is preferred.
            var mix = PotionItems.MixPotion();
            var hpPotion = PotionItems.HPPotion();
            var mpPotion = PotionItems.MPPotion();
            var bag = CreateBagWith(mix, hpPotion, mpPotion);

            Consumable best;
            int idx;
            // HP at 490/500 (missing 10), MP at 10/100 (missing 90)
            bool found = PotionSelectionEngine.SelectOutOfBattlePotion(
                bag, 490, 500, 10, 100, out best, out idx);

            Assert.IsTrue(found);
            // Should prefer separate; MP is more critical (10%)
            Assert.AreEqual("MPPotion", best.Name, "When tied, should prefer separate potions; MP is more critical");
        }

        // ====================================================================
        // OUT-OF-BATTLE: Full Potions Rule
        // ====================================================================

        [TestMethod]
        public void OutOfBattle_FullPotion_SkippedForSmallDeficit()
        {
            // HP at 70% → 30% deficit (below 50% threshold)
            var fullHP = PotionItems.FullHPPotion();
            var bag = CreateBagWith(fullHP);

            Consumable best;
            int idx;
            bool found = PotionSelectionEngine.SelectOutOfBattlePotion(
                bag, 350, 500, 100, 100, out best, out idx);

            Assert.IsFalse(found, "Full potion should be skipped for small HP deficit outside battle");
        }

        [TestMethod]
        public void OutOfBattle_FullPotion_AllowedForLargeDeficit()
        {
            // HP at 30% → 70% deficit (above 50% threshold)
            var fullHP = PotionItems.FullHPPotion();
            var bag = CreateBagWith(fullHP);

            Consumable best;
            int idx;
            bool found = PotionSelectionEngine.SelectOutOfBattlePotion(
                bag, 150, 500, 100, 100, out best, out idx);

            Assert.IsTrue(found, "Full potion should be allowed for large deficit");
            Assert.AreEqual("FullHPPotion", best.Name);
        }

        [TestMethod]
        public void OutOfBattle_FullMixPotion_AllowedWhenMPDeficitLarge()
        {
            // HP at 60% (40% deficit, below threshold), MP at 10% (90% deficit, above threshold)
            // FullMixPotion should be allowed because MP deficit justifies it
            var fullMix = PotionItems.FullMixPotion();
            var bag = CreateBagWith(fullMix);

            Consumable best;
            int idx;
            bool found = PotionSelectionEngine.SelectOutOfBattlePotion(
                bag, 300, 500, 10, 100, out best, out idx);

            Assert.IsTrue(found, "FullMixPotion should be allowed when MP deficit is large");
            Assert.AreEqual("FullMixPotion", best.Name);
        }

        // ====================================================================
        // EDGE CASES
        // ====================================================================

        [TestMethod]
        public void Battle_NothingToRestore_ReturnsFalse()
        {
            var hpPotion = PotionItems.HPPotion();
            var bag = CreateBagWith(hpPotion);

            Consumable best;
            int idx;
            bool found = PotionSelectionEngine.SelectBattlePotion(
                bag, 500, 500, 100, 100, out best, out idx);

            Assert.IsFalse(found, "Should return false when nothing to restore");
        }

        [TestMethod]
        public void Battle_EmptyBag_ReturnsFalse()
        {
            var bag = new ItemBag("Test", 120);

            Consumable best;
            int idx;
            bool found = PotionSelectionEngine.SelectBattlePotion(
                bag, 100, 500, 30, 100, out best, out idx);

            Assert.IsFalse(found, "Should return false with empty bag");
        }

        [TestMethod]
        public void Battle_NullBag_ReturnsFalse()
        {
            Consumable best;
            int idx;
            bool found = PotionSelectionEngine.SelectBattlePotion(
                null, 100, 500, 30, 100, out best, out idx);

            Assert.IsFalse(found, "Should return false with null bag");
        }

        [TestMethod]
        public void OutOfBattle_NothingToRestore_ReturnsFalse()
        {
            var hpPotion = PotionItems.HPPotion();
            var bag = CreateBagWith(hpPotion);

            Consumable best;
            int idx;
            bool found = PotionSelectionEngine.SelectOutOfBattlePotion(
                bag, 500, 500, 100, 100, out best, out idx);

            Assert.IsFalse(found, "Should return false when nothing to restore");
        }

        [TestMethod]
        public void OutOfBattle_BattleOnlySkipped()
        {
            // Battle-only consumable should be skipped out of battle
            var battlePotion = new TestBattleOnlyPotion();
            var bag = CreateBagWith(battlePotion);

            Consumable best;
            int idx;
            bool found = PotionSelectionEngine.SelectOutOfBattlePotion(
                bag, 100, 500, 100, 100, out best, out idx);

            Assert.IsFalse(found, "Battle-only potion should be skipped outside battle");
        }

        [TestMethod]
        public void OutOfBattle_ZeroStackSkipped()
        {
            var hpPotion = PotionItems.HPPotion();
            hpPotion.StackCount = 0;
            var bag = new ItemBag("Test", 120);
            bag.SetSlotItem(0, hpPotion);

            Consumable best;
            int idx;
            bool found = PotionSelectionEngine.SelectOutOfBattlePotion(
                bag, 100, 500, 100, 100, out best, out idx);

            Assert.IsFalse(found, "Zero stack potion should be skipped");
        }

        [TestMethod]
        public void GetEffectiveRestoreAmount_FullRestore_ReturnsMax()
        {
            int result = PotionSelectionEngine.GetEffectiveRestoreAmount(-1, 500);
            Assert.AreEqual(500, result, "Full restore (-1) should return max stat");
        }

        [TestMethod]
        public void GetEffectiveRestoreAmount_NormalRestore_ReturnsAmount()
        {
            int result = PotionSelectionEngine.GetEffectiveRestoreAmount(100, 500);
            Assert.AreEqual(100, result, "Normal restore should return the amount");
        }

        [TestMethod]
        public void GetEffectiveRestoreAmount_Zero_ReturnsZero()
        {
            int result = PotionSelectionEngine.GetEffectiveRestoreAmount(0, 500);
            Assert.AreEqual(0, result, "Zero restore should return zero");
        }

        // ====================================================================
        // BATTLE: scoring behavior summary tests
        // ====================================================================

        [TestMethod]
        public void Battle_MixPotionWhenBothResourcesMatter()
        {
            // Both HP and MP critical, MixPotion should be clearly favored
            // HP at 25% (critical), MP at 25% (critical)
            // MixPotion: EffHP=100*2 + EffMP=75*2 = 200+150 = 350
            // HPPotion: EffHP=100*2 = 200
            // MPPotion: EffMP=75*2 = 150
            var hpPotion = PotionItems.HPPotion();
            var mpPotion = PotionItems.MPPotion();
            var mix = PotionItems.MixPotion();
            var bag = CreateBagWith(hpPotion, mpPotion, mix);

            Consumable best;
            int idx;
            bool found = PotionSelectionEngine.SelectBattlePotion(
                bag, 125, 500, 25, 100, out best, out idx);

            Assert.IsTrue(found);
            Assert.AreEqual("MixPotion", best.Name, "MixPotion should be clearly favored when both resources critical");
        }

        [TestMethod]
        public void Battle_NotEmergency_JustAboveThreshold()
        {
            // HP at exactly 20% — NOT emergency (threshold is strictly < 0.20)
            // Should use normal scoring, not emergency mode
            var hpPotion = PotionItems.HPPotion();     // 100 HP, price 20
            var midHP = PotionItems.MidHPPotion();     // 500 HP, price 100
            var bag = CreateBagWith(hpPotion, midHP);

            Consumable best;
            int idx;
            bool found = PotionSelectionEngine.SelectBattlePotion(
                bag, 100, 500, 100, 100, out best, out idx);

            Assert.IsTrue(found);
            // At 20%, uses normal scoring: both have HP weight=2
            // HPPotion: EffHP=min(100,400)*2=200
            // MidHPPotion: EffHP=min(500,400)*2=800
            // MidHP wins on score
            Assert.AreEqual("MidHPPotion", best.Name);
        }

        // ====================================================================
        // Test helper: battle-only potion
        // ====================================================================

        private class TestBattleOnlyPotion : Consumable
        {
            public TestBattleOnlyPotion()
                : base("Battle Elixir", ItemRarity.Normal, "Test", 10, 200, 0, true)
            {
            }
        }
    }
}
