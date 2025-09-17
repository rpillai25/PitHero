using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Combat
{
    /// <summary>Resolves to-hit and damage between two stat blocks.</summary>
    public interface IAttackResolver
    {
        /// <summary>Computes an attack from attacker to defender.</summary>
        AttackResult Resolve(in StatBlock attackerStats, in StatBlock defenderStats, DamageKind kind, int attackerLevel, int defenderLevel);
    }
}
