using System;
using System.Text;
using Nez;
using PitHero.Dining;
using PitHero.Services;
using RolePlayingFramework.Combat;

namespace PitHero.UI
{
    /// <summary>
    /// Formats dish buffs (Food tab) and a combatant's active meal (Hero Info and Mercenaries tabs).
    /// Read-only: consumes no RNG and writes no state.
    /// </summary>
    public static class MealBuffDisplay
    {
        /// <summary>Buffs per line in the info tabs, so Harvest Feast Platter's six buffs stay narrow.</summary>
        public const int InfoBuffsPerLine = 2;

        /// <summary>
        /// Builds a dish's effect list ("ATK +2, DEF +2"). Deluxe applies the deluxe magnitude; a line
        /// break replaces the comma after every <paramref name="buffsPerLine"/> entries.
        /// </summary>
        public static string BuildEffectsText(DishDefinition def, bool deluxe, int buffsPerLine)
        {
            if (buffsPerLine <= 0)
                buffsPerLine = int.MaxValue;

            var sb = new StringBuilder(64);
            int count = 0;
            for (int b = 0; b < def.Buffs.Length; b++)
            {
                var buff = def.Buffs[b];
                int magnitude = deluxe ? DishConfig.GetDeluxeMagnitude(buff.Magnitude) : buff.Magnitude;
                string text = GetBuffText(buff.Type, magnitude);
                if (text == null)
                    continue;

                if (count > 0)
                    sb.Append(count % buffsPerLine == 0 ? "\n" : ", ");
                sb.Append(text);
                count++;
            }
            return sb.Length > 0 ? sb.ToString() : "-";
        }

        /// <summary>
        /// Formats one active meal: "Dish (Deluxe) - Xh Ym" followed by its effects on the next line(s).
        /// </summary>
        public static string FormatEntry(DishType dish, bool deluxe, float remainingSeconds, Func<string, string> getText)
        {
            var def = DishConfig.GetDefinition(dish);
            string name = getText(def.NameKey);
            if (deluxe)
                name = string.Format(getText(UITextKey.MealBuffDeluxeName), name);

            // 1 real second = 1 in-game minute (InGameTimeService). Round up so a live buff never
            // reads 0m; clamp to the buff duration so an unstamped (float.MaxValue) expiry can't overflow.
            int totalMinutes = (int)Math.Ceiling(Math.Min(remainingSeconds, GameConfig.MealBuffDurationSeconds));
            string title = string.Format(getText(UITextKey.MealBuffDishRemaining), name, totalMinutes / 60, totalMinutes % 60);
            return title + "\n" + BuildEffectsText(def, deluxe, InfoBuffsPerLine);
        }

        /// <summary>
        /// Text for the combatant's active meal buff, or the "None" text when there is none (or the
        /// scene services are absent, e.g. headless tests).
        /// </summary>
        public static string BuildText(ICombatant combatant, Func<string, string> getText)
        {
            string none = getText(UITextKey.MealBuffsNone);
            if (combatant == null || Core.Instance == null)
                return none;

            var mealBuffs = Core.Services.GetService<MealBuffService>();
            var time = Core.Services.GetService<InGameTimeService>();
            if (mealBuffs == null || time == null
                || !mealBuffs.TryGetMeal(combatant, out var dish, out var deluxe, out var expiresAtSeconds))
                return none;

            // Records only prune while unpaused, so an elapsed one can still be present
            float remaining = expiresAtSeconds - time.AccumulatedSeconds;
            return remaining > 0f ? FormatEntry(dish, deluxe, remaining, getText) : none;
        }

        private static string GetBuffText(BuffType type, int magnitude)
        {
            switch (type)
            {
                case BuffType.AttackUp: return "ATK +" + magnitude;
                case BuffType.DefenseUp: return "DEF +" + magnitude;
                case BuffType.AgilityUp: return "AGI +" + magnitude;
                case BuffType.MagicUp: return "MAG +" + magnitude;
                case BuffType.EvasionUp: return "EVA +" + magnitude;
                case BuffType.HPRegen: return "HP +" + magnitude + "/round";
                case BuffType.MPRegen: return "MP +" + magnitude + "/round";
                default: return null;
            }
        }
    }
}
