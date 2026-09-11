using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Dining;
using PitHero.UI;

namespace PitHero.Tests.UI
{
    /// <summary>
    /// Tests for MealBuffDisplay: effect formatting (deluxe magnitudes, line breaks) and the
    /// "Dish (Deluxe) - Xh Ym" meal entry shown in the Hero Info and Mercenaries tabs.
    /// </summary>
    [TestClass]
    public class MealBuffDisplayTests
    {
        private static readonly Dictionary<string, string> Text = new Dictionary<string, string>
        {
            { UITextKey.MealBuffsNone, "None" },
            { UITextKey.MealBuffDeluxeName, "{0} (Deluxe)" },
            { UITextKey.MealBuffDishRemaining, "{0} - {1}h {2}m" },
            { UITextKey.DishApplePie, "Apple Pie" },
        };

        private static string GetText(string key) => Text.TryGetValue(key, out var value) ? value : key;

        [TestMethod]
        [TestCategory("MealBuff")]
        public void BuildEffectsText_Normal_UsesBaseMagnitudes()
        {
            var def = DishConfig.GetDefinition(DishType.ApplePie);

            Assert.AreEqual("ATK +2, DEF +2", MealBuffDisplay.BuildEffectsText(def, false, int.MaxValue));
        }

        [TestMethod]
        [TestCategory("MealBuff")]
        public void BuildEffectsText_Deluxe_UsesDeluxeMagnitudes()
        {
            var def = DishConfig.GetDefinition(DishType.ApplePie);
            int deluxe = DishConfig.GetDeluxeMagnitude(2);

            Assert.AreEqual($"ATK +{deluxe}, DEF +{deluxe}", MealBuffDisplay.BuildEffectsText(def, true, int.MaxValue));
        }

        [TestMethod]
        [TestCategory("MealBuff")]
        public void BuildEffectsText_BuffsPerLine_BreaksLines()
        {
            var def = DishConfig.GetDefinition(DishType.HarvestFeastPlatter);

            string wrapped = MealBuffDisplay.BuildEffectsText(def, false, MealBuffDisplay.InfoBuffsPerLine);
            string singleLine = MealBuffDisplay.BuildEffectsText(def, false, int.MaxValue);

            Assert.AreEqual(3, wrapped.Split('\n').Length, "Six buffs at two per line should take three lines");
            Assert.IsFalse(singleLine.Contains("\n"), "No line breaks without a per-line limit");
        }

        [DataTestMethod]
        [TestCategory("MealBuff")]
        [DataRow(252.4f, "4h 13m")]
        [DataRow(59.2f, "1h 0m")]
        [DataRow(360f, "6h 0m")]
        [DataRow(0.5f, "0h 1m")]
        public void FormatEntry_RoundsUpRemainingMinutes(float remainingSeconds, string expectedTime)
        {
            string text = MealBuffDisplay.FormatEntry(DishType.ApplePie, false, remainingSeconds, GetText);

            Assert.AreEqual("Apple Pie - " + expectedTime + "\nATK +2, DEF +2", text);
        }

        [TestMethod]
        [TestCategory("MealBuff")]
        public void FormatEntry_Deluxe_TagsNameAndScalesBuffs()
        {
            int deluxe = DishConfig.GetDeluxeMagnitude(2);

            string text = MealBuffDisplay.FormatEntry(DishType.ApplePie, true, 120f, GetText);

            Assert.AreEqual($"Apple Pie (Deluxe) - 2h 0m\nATK +{deluxe}, DEF +{deluxe}", text);
        }

        [TestMethod]
        [TestCategory("MealBuff")]
        public void BuildText_NullCombatant_ReturnsNone()
        {
            Assert.AreEqual("None", MealBuffDisplay.BuildText(null, GetText));
        }
    }
}
