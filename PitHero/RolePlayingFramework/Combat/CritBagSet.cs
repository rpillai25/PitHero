using RolePlayingFramework.Balance;
using RolePlayingFramework.Utils;

namespace RolePlayingFramework.Combat
{
    /// <summary>
    /// Per-combatant critical-hit shuffle bags (issue #382). Lives on Hero/Mercenary so
    /// pity progress persists across battles: it must survive ClearBattleState (which
    /// only clears buffs) and CombatantPassiveApplier.ResetAndApply (which only zeroes
    /// scalar passive fields).
    /// Two bags: the base bag advances on every attack (exactly one crit per
    /// <see cref="BalanceConfig.CritBagSize"/> attacks at 5%); the quickdraw bag covers
    /// the Quickdraw first-attack bonus and rebuilds lazily whenever the observed
    /// FirstAttackCritChance differs from the chance it was built for (the passive
    /// applier resets and re-adds that field on every equip/level/skill change).
    /// Draws consume no RNG — callers feed in already-consumed Nez.Random floats so the
    /// battle RNG call sequence stays a fixed two floats per ally attack.
    /// </summary>
    public sealed class CritBagSet
    {
        private ShuffleBag<bool> _baseBag;
        private ShuffleBag<bool> _quickdrawBag;
        private float _builtQuickdrawChance;

        /// <summary>Draws the base-crit bag using a caller-supplied [0,1) roll.</summary>
        public bool RollBase(float roll01)
        {
            if (_baseBag == null)
                _baseBag = BuildBag(BalanceConfig.BaseCritChance);
            return _baseBag.NextFromRoll(roll01);
        }

        /// <summary>
        /// Draws the quickdraw bag using a caller-supplied [0,1) roll. Returns false without
        /// touching the bag when the combatant has no first-attack crit chance (the caller's
        /// RNG consumption, not bag advancement, is what the battle RNG contract fixes).
        /// </summary>
        public bool RollQuickdraw(float roll01, float currentChance)
        {
            if (currentChance <= 0f)
                return false;

            if (_quickdrawBag == null || currentChance != _builtQuickdrawChance)
            {
                _quickdrawBag = BuildBag(currentChance);
                _builtQuickdrawChance = currentChance;
            }
            return _quickdrawBag.NextFromRoll(roll01);
        }

        private static ShuffleBag<bool> BuildBag(float chance)
        {
            var size = BalanceConfig.CritBagSize;
            var crits = (int)System.Math.Round(chance * size);
            if (crits < 1) crits = 1;
            else if (crits > size) crits = size;

            var bag = new ShuffleBag<bool>(size);
            bag.Add(true, crits);
            if (size - crits > 0)
                bag.Add(false, size - crits);
            return bag;
        }
    }
}
