using Microsoft.Xna.Framework;
using Nez;
using PitHero.Farming;
using PitHero.Services.Analytics;
using PitHero.Util;

namespace PitHero.Services
{
    /// <summary>
    /// Records harvests into the lifetime crop totals and announces crops that just became
    /// unlocked (issue #413). Also the one place UI and command handlers ask whether a crop is
    /// unlocked right now, so the shop, the planting palette, auto-purchase and the BuySeeds
    /// handler can never disagree.
    /// </summary>
    public static class CropUnlockTracker
    {
        private static readonly Color UnlockColor = new Color(120, 255, 140);

        /// <summary>Live game state, or null headlessly.</summary>
        private static GameStateService GameState
            => Core.Instance != null ? Core.Services?.GetService<GameStateService>() : null;

        /// <summary>
        /// True when the crop can be bought and planned. Without a game state (headless hosts that
        /// never created one) nothing is locked, so pure farm tests are unaffected.
        /// </summary>
        public static bool IsUnlocked(CropType crop)
        {
            var gameState = GameState;
            return gameState == null || CropUnlockConfig.IsUnlocked(crop, gameState.CropHarvestedTotals);
        }

        /// <summary>
        /// How far along the crop's unlock requirements are, 0-1 (1 headlessly, matching IsUnlocked).
        /// Drives how much colour a locked crop's sprite shows in the seed shop and planting palette.
        /// </summary>
        public static float GetUnlockProgress(CropType crop)
        {
            var gameState = GameState;
            return gameState == null ? 1f : CropUnlockConfig.GetUnlockProgress(crop, gameState.CropHarvestedTotals);
        }

        /// <summary>Lifetime harvested units of a crop (0 headlessly).</summary>
        public static int GetHarvestedTotal(CropType crop)
        {
            var gameState = GameState;
            return gameState != null ? CropUnlockConfig.GetTotal(gameState.CropHarvestedTotals, crop) : 0;
        }

        /// <summary>Adds harvested units to the lifetime total and announces any crop that unlocked as a result.</summary>
        public static void RecordHarvest(CropType crop, int units)
        {
            var gameState = GameState;
            if (gameState == null)
                return;
            int before = CropUnlockConfig.GetUnlockedMask(gameState.CropHarvestedTotals);
            gameState.RecordHarvest(crop, units);
            int after = CropUnlockConfig.GetUnlockedMask(gameState.CropHarvestedTotals);
            int gained = after & ~before;
            if (gained == 0)
                return;

            var events = Core.Services?.GetService<GameEventService>();
            for (int i = 0; i < CropTypeInfo.Count; i++)
            {
                if ((gained & (1 << i)) == 0)
                    continue;
                var unlocked = (CropType)i;
                AnalyticsService.LogCropUnlocked(unlocked.ToString());
                events?.EmitLocalized(EventPriority.High, UITextKey.ConsoleCropUnlocked,
                    (events.LocalizeUI(CropConfig.GetDisplayNameKey(unlocked)), UnlockColor));
            }
        }
    }
}
