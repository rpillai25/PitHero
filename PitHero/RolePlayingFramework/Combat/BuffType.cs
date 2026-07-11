namespace RolePlayingFramework.Combat
{
    /// <summary>Types of temporary battle-scoped buffs applied to a combatant.</summary>
    public enum BuffType
    {
        /// <summary>Raises the combatant's effective defense stat.</summary>
        DefenseUp,

        /// <summary>Raises the combatant's effective evasion stat.</summary>
        EvasionUp,

        /// <summary>Adds MP restored per end-of-round regen tick.</summary>
        MPRegen,

        /// <summary>Combatant cannot be targeted by enemy attacks (Phase 4 — seam only in Phase 3).</summary>
        Untargetable
    }
}
