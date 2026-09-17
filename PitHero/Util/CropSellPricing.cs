using Nez;
using PitHero.Farming;
using PitHero.Services;

namespace PitHero.Util
{
    /// <summary>
    /// The one place a crop sale is priced and recorded (issue #417). Live sessions resolve the
    /// scene's <see cref="CropMarketService"/> and apply its demand multiplier; headless hosts
    /// without one pay base price so pure farm tests are unaffected. Every path that pays the
    /// player for crops must go through here — a direct <c>CropConfig.GetHarvestStackSellPrice</c>
    /// call in a sell path would pay base price and skip the demand drop.
    /// </summary>
    public static class CropSellPricing
    {
        /// <summary>The live market, or null headlessly / before the scene registers one.</summary>
        public static CropMarketService Resolve()
            => Core.Instance != null ? Core.Services?.GetService<CropMarketService>() : null;

        /// <summary>Gold a stack of <paramref name="count"/> units sells for right now.</summary>
        public static int GetStackSellPrice(CropType crop, int count)
        {
            var market = Resolve();
            return market != null
                ? market.GetStackSellPrice(crop, count)
                : CropConfig.GetHarvestStackSellPrice(crop, count);
        }

        /// <summary>Gold a stack sells for using an explicit market (null = base price).</summary>
        public static int GetStackSellPrice(CropMarketService market, CropType crop, int count)
        {
            return market != null
                ? market.GetStackSellPrice(crop, count)
                : CropConfig.GetHarvestStackSellPrice(crop, count);
        }

        /// <summary>Records a completed sale against the live market (no-op headlessly).</summary>
        public static void RecordSale(CropType crop, int units)
        {
            Resolve()?.RecordSale(crop, units);
        }

        /// <summary>Current demand percentage for display (100 headlessly).</summary>
        public static int GetDemandPercent(CropType crop)
        {
            var market = Resolve();
            return market != null ? market.GetDemandPercent(crop) : 100;
        }
    }
}
