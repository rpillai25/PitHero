using RolePlayingFramework.Heroes;

namespace PitHero.Services
{
    /// <summary>Pure fee math for crystal creation and forging (no Core dependencies).</summary>
    public static class CrystalFeeCalculator
    {
        /// <summary>Forge fee is the Second Chance Shop's purchase-price algorithm for the
        /// combined crystal, doubled — forging a many-skill crystal must cost more than
        /// buying one back. Combine is a pure factory, so previewing is side-effect free.</summary>
        public static int GetForgeFee(HeroCrystal a, HeroCrystal b)
        {
            if (a == null || b == null)
                return 0;
            var preview = HeroCrystal.Combine("Combo Crystal", a, b);
            return preview.CalculateBuyBackPrice() * GameConfig.CrystalForgeFeeMultiplier;
        }
    }
}
