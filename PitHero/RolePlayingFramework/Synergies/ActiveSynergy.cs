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

        /// <summary>Checks if a specific slot is part of this synergy.</summary>
        public bool ContainsSlot(Point slot)
        {
            for (int i = 0; i < AffectedSlots.Count; i++)
            {
                if (AffectedSlots[i] == slot)
                    return true;
            }
            return false;
        }
        
        /// <summary>
        /// Checks if this synergy shares any inventory slots with another synergy.
        /// Used for overlap detection in stacking system.
        /// Issue #133 - Synergy Stacking System
        /// </summary>
        /// <param name="other">The other synergy to compare against.</param>
        /// <returns>True if any slots overlap, false otherwise.</returns>
        public bool SharesItems(ActiveSynergy other)
        {
            if (other == null)
                return false;
            
            // Check if any affected slots overlap
            for (int i = 0; i < AffectedSlots.Count; i++)
            {
                var slot = AffectedSlots[i];
                for (int j = 0; j < other.AffectedSlots.Count; j++)
                {
                    if (slot == other.AffectedSlots[j])
                        return true;
                }
            }
            return false;
        }
    }
}
