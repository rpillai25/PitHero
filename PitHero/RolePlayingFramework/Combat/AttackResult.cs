namespace RolePlayingFramework.Combat
{
    /// <summary>Result of a single attack resolution.</summary>
    public readonly struct AttackResult
    {
        public readonly bool Hit;
        public readonly int Damage;

        public AttackResult(bool hit, int damage)
        {
            Hit = hit;
            Damage = damage < 0 ? 0 : damage;
        }
    }
}
