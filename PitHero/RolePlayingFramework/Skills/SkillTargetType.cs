namespace RolePlayingFramework.Skills
{
    /// <summary>Targets a skill can apply to.</summary>
    public enum SkillTargetType
    {
        Self,
        SingleEnemy,
        SurroundingEnemies,
        /// <summary>Targets a single ally (hero or mercenary)</summary>
        SingleAlly,
        /// <summary>Targets all allies (group heal/buff)</summary>
        AllAllies
    }
}
