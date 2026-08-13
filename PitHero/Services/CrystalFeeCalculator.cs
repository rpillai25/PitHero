using RolePlayingFramework.Heroes;

namespace PitHero.Services
{
    /// <summary>Pure fee math for crystal creation and forging (no Core dependencies).</summary>
    public static class CrystalFeeCalculator
    {
        /// <summary>Forge fee is the Second Chance Shop's purchase-price algorithm for the
        /// combined crystal, doubled, with the skill premium UNCAPPED — a 16-skill Legend
        /// combo must cost more to forge than a 6-skill one at any crystal level (the shop's
        /// cap exists only to keep recovering a lost crystal affordable). Combine is a pure
        /// factory, so previewing is side-effect free.</summary>
        public static int GetForgeFee(HeroCrystal a, HeroCrystal b)
        {
            if (a == null || b == null)
                return 0;
            var preview = HeroCrystal.Combine("Combo Crystal", a, b);
            return preview.CalculateBuyBackPrice(capPremium: false) * GameConfig.CrystalForgeFeeMultiplier;
        }
    }
}
