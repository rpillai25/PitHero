using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace RolePlayingFramework.Synergies
{
    /// <summary>Represents an active synergy instance detected in the inventory grid.</summary>
    public sealed class ActiveSynergy
    {
        /// <summary>The pattern that was matched.</summary>
        public SynergyPattern Pattern { get; }
        
        /// <summary>Grid position where the pattern was anchored.</summary>
        public Point AnchorSlot { get; }
        
        /// <summary>All grid slots affected by this synergy pattern.</summary>
        public IReadOnlyList<Point> AffectedSlots { get; }
        
        /// <summary>Synergy points earned for this specific pattern instance.</summary>
        public int PointsEarned { get; private set; }
        
        /// <summary>True if the synergy skill has been unlocked.</summary>
        public bool IsSkillUnlocked => 
            Pattern.UnlockedSkill != null && 
            PointsEarned >= Pattern.SynergyPointsRequired;
        
        public ActiveSynergy(SynergyPattern pattern, Point anchorSlot, IReadOnlyList<Point> affectedSlots)
        {
            Pattern = pattern;
            AnchorSlot = anchorSlot;
            AffectedSlots = affectedSlots;
            PointsEarned = 0;
        }
        
        /// <summary>Adds synergy points to this active synergy.</summary>
        public void EarnPoints(int amount)
        {
            if (amount < 0) return;
            PointsEarned += amount;
        }
    }
}
