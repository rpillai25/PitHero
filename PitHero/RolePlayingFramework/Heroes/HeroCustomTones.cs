using Microsoft.Xna.Framework;

namespace RolePlayingFramework.Heroes
{
    /// <summary>
    /// Shadow and Highlight colors the player picked for one paperdoll part on the hero creation screen's
    /// Custom option (issue #467). The part's chosen color still plays the Midtone. When <see cref="IsSet"/>
    /// is false (the default) the part's tones come from its ramp or profile as before.
    /// </summary>
    public readonly struct HeroCustomTones
    {
        /// <summary>True when the player picked this part's tones.</summary>
        public readonly bool IsSet;

        /// <summary>The Shadow tone's color.</summary>
        public readonly Color Shadow;

        /// <summary>The Highlight tone's color (only hair has a Highlight tone).</summary>
        public readonly Color Highlight;

        public HeroCustomTones(Color shadow, Color highlight)
        {
            IsSet = true;
            Shadow = shadow;
            Highlight = highlight;
        }

        /// <summary>No custom tones: every tone follows the part's ramp.</summary>
        public static readonly HeroCustomTones None = default;

        public bool Equals(HeroCustomTones other)
        {
            if (IsSet != other.IsSet)
                return false;
            return !IsSet || (Shadow == other.Shadow && Highlight == other.Highlight);
        }
    }
}
