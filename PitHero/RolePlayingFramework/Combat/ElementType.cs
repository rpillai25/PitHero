namespace RolePlayingFramework.Combat
{
    /// <summary>
    /// Represents the elemental type of an entity, gear, or skill.
    /// Elements have opposing relationships that affect damage calculations.
    /// </summary>
    public enum ElementType
    {
        /// <summary>Neutral element with no advantages or disadvantages.</summary>
        Neutral,

        /// <summary>Fire element. Opposes Water.</summary>
        Fire,

        /// <summary>Water element. Opposes Fire.</summary>
        Water,

        /// <summary>Earth element. Opposes Wind.</summary>
        Earth,

        /// <summary>Wind element. Opposes Earth.</summary>
        Wind,

        /// <summary>Light element. Opposes Dark.</summary>
        Light,

        /// <summary>Dark element. Opposes Light.</summary>
        Dark
    }
}
