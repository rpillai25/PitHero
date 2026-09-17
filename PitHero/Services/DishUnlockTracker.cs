using Microsoft.Xna.Framework;
using Nez;
using PitHero.Dining;
using PitHero.Services.Analytics;

namespace PitHero.Services
{
    /// <summary>
    /// Records served dishes into the lifetime totals and announces dishes that just became fully
    /// unlocked (issue #417). Also the one place UI and the kitchen ask whether a dish is unlocked
    /// right now, so the Food tab, patron orders and party orders can never disagree. Headless
    /// hosts without a game state see nothing locked, so pure kitchen tests are unaffected.
    /// </summary>
    public static class DishUnlockTracker
    {
        private static readonly Color UnlockColor = new Color(120, 255, 140);

        /// <summary>Live game state, or null headlessly.</summary>
        private static GameStateService GameState
            => Core.Instance != null ? Core.Services?.GetService<GameStateService>() : null;

        /// <summary>True when the dish is orderable by patrons and the party (nothing is locked headlessly).</summary>
        public static bool IsFullyUnlocked(DishType dish)
        {
            return IsFullyUnlocked(GameState, dish);
        }

        /// <summary>True when the dish is orderable given an explicit game state (null = nothing locked).</summary>
        public static bool IsFullyUnlocked(GameStateService gameState, DishType dish)
        {
            return gameState == null
                || DishUnlockConfig.IsFullyUnlocked(dish, gameState.CropHarvestedTotals, gameState.DishesServedTotals);
        }

        /// <summary>True when the dish shows on the menu at all: every recipe crop is unlocked.</summary>
        public static bool IsSoftUnlocked(DishType dish)
        {
            var gameState = GameState;
            return gameState == null || DishUnlockConfig.IsSoftUnlocked(dish, gameState.CropHarvestedTotals);
        }

        /// <summary>Lifetime servings of a dish (0 headlessly).</summary>
        public static int GetServedTotal(DishType dish)
        {
            var gameState = GameState;
            return gameState != null ? DishUnlockConfig.GetTotal(gameState.DishesServedTotals, dish) : 0;
        }

        /// <summary>
        /// Counts one served dish and announces any dish that fully unlocked as a result. Safe to
        /// call with a null game state (no-op) and without a scene (counts, no announcement).
        /// </summary>
        public static void RecordDishServed(GameStateService gameState, DishType dish)
        {
            if (gameState == null)
                return;
            int before = DishUnlockConfig.GetFullyUnlockedMask(gameState.CropHarvestedTotals, gameState.DishesServedTotals);
            gameState.RecordDishServed(dish);
            int after = DishUnlockConfig.GetFullyUnlockedMask(gameState.CropHarvestedTotals, gameState.DishesServedTotals);
            int gained = after & ~before;
            if (gained == 0)
                return;

            var events = Core.Instance != null ? Core.Services?.GetService<GameEventService>() : null;
            for (int i = 0; i < DishTypeInfo.Count; i++)
            {
                if ((gained & (1 << i)) == 0)
                    continue;
                var unlocked = (DishType)i;
                AnalyticsService.LogDishUnlocked(unlocked.ToString());
                events?.EmitLocalized(EventPriority.High, UITextKey.ConsoleDishUnlocked,
                    (events.LocalizeUI(DishConfig.GetDefinition(unlocked).NameKey), UnlockColor));
            }
        }
    }
}
