using PitHero.Farming;
using PitHero.Util;

namespace PitHero.Services
{
    /// <summary>
    /// Per-crop market demand (issue #417). Every unit the player sells lowers that crop's demand
    /// in proportion to its base gold value; demand then recovers toward full at a constant rate.
    /// A fixed farm therefore settles into a steady state — nothing here is random or
    /// time-varying on its own, so automation planned once keeps working untouched. Selling
    /// N plots' worth of one crop settles its demand near <c>1 - N / MarketSaturationPlots</c>
    /// (floored), whatever the crop's tier: a wall of one crop pays a fraction of base price while
    /// a mixed farm sells near full price. Crops the kitchen consumes never touch demand.
    ///
    /// Simulation state: registered per scene, persisted (save v37), ticked from the fixed step,
    /// and mutated only inside sell paths (command handlers and the auto-sell pass).
    /// </summary>
    public class CropMarketService
    {
        private readonly float[] _demand = new float[CropTypeInfo.Count];

        /// <summary>Starts every crop at full demand.</summary>
        public CropMarketService()
        {
            Reset();
        }

        /// <summary>
        /// Multiplies every crop's saturation depth. The scene sets it to the active fertilizer
        /// growth multiplier every fixed step (like <c>CropGrowthService.GrowthSpeedMultiplier</c>),
        /// so a farm growing 3x faster faces a market 3x deeper and the artifact keeps its full
        /// income multiplier; a monoculture is still penalized relative to a mixed farm. Derived
        /// from session artifact state, never saved.
        /// </summary>
        public float SaturationScale { get; set; } = 1f;

        /// <summary>Current demand multiplier for a crop, in [MarketDemandFloor, 1].</summary>
        public float GetDemand(CropType crop)
        {
            int i = (int)crop;
            return i >= 0 && i < _demand.Length ? _demand[i] : 1f;
        }

        /// <summary>Demand as a whole percentage (0-100) for display.</summary>
        public int GetDemandPercent(CropType crop)
        {
            return (int)System.Math.Round(GetDemand(crop) * 100f);
        }

        /// <summary>Sell value of one harvested unit at current demand.</summary>
        public float GetUnitSellPrice(CropType crop)
        {
            return CropConfig.GetHarvestUnitSellPrice(crop) * GetDemand(crop);
        }

        /// <summary>Gold paid for selling <paramref name="count"/> units at current demand (ceiling, min 1 per unit).</summary>
        public int GetStackSellPrice(CropType crop, int count)
        {
            if (count <= 0)
                return 0;
            int gold = (int)System.Math.Ceiling(GetUnitSellPrice(crop) * count);
            return gold < count ? count : gold;
        }

        /// <summary>
        /// Base gold sold of a crop that would take its demand from full to zero (before the floor).
        /// Chosen so N plots of the crop settle at <c>1 - N / MarketSaturationPlots</c>: one plant
        /// grosses roughly its profit-per-growth-hour every in-game hour.
        /// </summary>
        public static float GetDepthGold(CropType crop)
        {
            return GameConfig.MarketSaturationPlots * CropConfig.GetProfitPerGrowthHour(crop)
                   / GameConfig.MarketRecoveryPerHour;
        }

        /// <summary>Records a sale of <paramref name="units"/> units: demand drops by the base gold sold over the crop's depth.</summary>
        public void RecordSale(CropType crop, int units)
        {
            int i = (int)crop;
            if (units <= 0 || i < 0 || i >= _demand.Length)
                return;
            float baseGold = CropConfig.GetHarvestUnitSellPrice(crop) * units;
            float scale = SaturationScale < 1f ? 1f : SaturationScale;
            float next = _demand[i] - baseGold / (GetDepthGold(crop) * scale);
            _demand[i] = next < GameConfig.MarketDemandFloor ? GameConfig.MarketDemandFloor : next;
        }

        /// <summary>Recovers every crop's demand toward full over <paramref name="inGameHours"/> of simulated time.</summary>
        public void Update(float inGameHours)
        {
            if (inGameHours <= 0f)
                return;
            float rate = GameConfig.MarketRecoveryPerHour * inGameHours;
            if (rate > 1f) rate = 1f;
            for (int i = 0; i < _demand.Length; i++)
                _demand[i] += (1f - _demand[i]) * rate;
        }

        /// <summary>Restores full demand for every crop (new game).</summary>
        public void Reset()
        {
            for (int i = 0; i < _demand.Length; i++)
                _demand[i] = 1f;
        }

        /// <summary>Restores demand from a save; a null or short array leaves the missing crops at full demand.</summary>
        public void SetDemand(float[] demand)
        {
            for (int i = 0; i < _demand.Length; i++)
            {
                float v = demand != null && i < demand.Length ? demand[i] : 1f;
                if (float.IsNaN(v) || v > 1f) v = 1f;
                if (v < GameConfig.MarketDemandFloor) v = GameConfig.MarketDemandFloor;
                _demand[i] = v;
            }
        }

        /// <summary>Copies the demand array (for saving).</summary>
        public float[] CopyDemand()
        {
            var copy = new float[_demand.Length];
            for (int i = 0; i < _demand.Length; i++)
                copy[i] = _demand[i];
            return copy;
        }
    }
}
