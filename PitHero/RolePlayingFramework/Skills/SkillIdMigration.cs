namespace RolePlayingFramework.Skills
{
    /// <summary>
    /// Maps retired skill ids found in old saves to their replacements so a learned slot is never
    /// lost. Applied when a crystal re-learns ids at load and when shortcut slots are restored.
    /// </summary>
    public static class SkillIdMigration
    {
        /// <summary>Light Armor (passive, retired) became Provoke (active) on the same JP tier.</summary>
        public const string KnightLightArmor = "knight.light_armor";

        /// <summary>Returns the current id for <paramref name="skillId"/> (itself when nothing changed).</summary>
        public static string Resolve(string skillId)
        {
            if (skillId == KnightLightArmor) return ProvokeSkill.SkillId;
            return skillId;
        }
    }
}
