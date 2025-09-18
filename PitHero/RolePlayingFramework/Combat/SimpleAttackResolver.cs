using RolePlayingFramework.Stats;
using Nez;

namespace RolePlayingFramework.Combat
{
    /// <summary>Simple linear to-hit and damage model (original, not FF-derived).</summary>
    public sealed class SimpleAttackResolver : IAttackResolver
    {
        /// <summary>Computes an attack from attacker to defender.</summary>
        public AttackResult Resolve(in StatBlock attackerStats, in StatBlock defenderStats, DamageKind kind, int attackerLevel, int defenderLevel)
        {
            // Accuracy: base 75% + 1% per level advantage + Agi differential * 0.5
            var levelDelta = attackerLevel - defenderLevel;
            var acc = 75 + levelDelta + (attackerStats.Agility - defenderStats.Agility) / 2;
            if (acc < 5) acc = 5; if (acc > 95) acc = 95;
            var roll = Nez.Random.Range(0, 100);
            if (roll >= acc) return new AttackResult(false, 0);

            // Damage: base from Strength or Magic, plus level factor, minus a portion of Vitality, plus equipment modifiers
            int raw;
            if (kind == DamageKind.Physical)
            {
                raw = attackerStats.Strength * 2 + attackerLevel * 2;
                var mitigation = defenderStats.Vitality;
                raw -= mitigation;
            }
            else
            {
                raw = attackerStats.Magic * 3 + attackerLevel;
                var mitigation = defenderStats.Magic / 2;
                raw -= mitigation;
            }

            if (raw < 1) raw = 1;

            // Small variance +/-10%
            var variance = (raw * 10) / 100;
            var final = raw + Nez.Random.Range(-variance, variance + 1);
            if (final < 1) final = 1;
            return new AttackResult(true, final);
        }
    }
}
