using RolePlayingFramework.Heroes;

namespace PitHero.Services
{
    /// <summary>Pure fee math for crystal creation and forging (no Core dependencies).</summary>
    public static class CrystalFeeCalculator
    {
        /// <summary>Forge fee is half of what the combined crystal would cost to buy back
        /// in the Second Chance Shop. Combine is a pure factory, so previewing is side-effect free.</summary>
        public static int GetForgeFee(HeroCrystal a, HeroCrystal b)
        {
            if (a == null || b == null)
                return 0;
            var preview = HeroCrystal.Combine("Combo Crystal", a, b);
            return preview.CalculateBuyBackPrice() / GameConfig.CrystalForgeFeeDivisor;
        }
    }
}
