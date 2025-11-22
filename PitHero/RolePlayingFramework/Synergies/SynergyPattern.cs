using System.Collections.Generic;
using Microsoft.Xna.Framework;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Skills;

namespace RolePlayingFramework.Synergies
{
    /// <summary>Defines a spatial pattern of items that creates a synergy effect.</summary>
    public sealed class SynergyPattern
    {
        /// <summary>Unique identifier for this synergy pattern.</summary>
        public string Id { get; }
        
        /// <summary>Display name of the synergy.</summary>
        public string Name { get; }
        
        /// <summary>Description of what this synergy does.</summary>
        public string Description { get; }
        
        /// <summary>Spatial pattern - relative coordinates from anchor point.</summary>
        public IReadOnlyList<Point> GridOffsets { get; }
        
        /// <summary>Required item kinds for each position (parallel to GridOffsets).</summary>
        public IReadOnlyList<ItemKind> RequiredKinds { get; }
        
        /// <summary>Effects granted when this synergy is active.</summary>
        public IReadOnlyList<ISynergyEffect> Effects { get; }
        
        /// <summary>Synergy points required to unlock the synergy skill.</summary>
        public int SynergyPointsRequired { get; }
        
        /// <summary>Skill unlocked when synergy is active and points requirement is met.</summary>
        public ISkill? UnlockedSkill { get; }
        
        /// <summary>Whether this pattern has a discoverable stencil.</summary>
        public bool HasStencil { get; }
        
        public SynergyPattern(
            string id,
            string name,
            string description,
            IReadOnlyList<Point> gridOffsets,
            IReadOnlyList<ItemKind> requiredKinds,
            IReadOnlyList<ISynergyEffect> effects,
            int synergyPointsRequired,
            ISkill? unlockedSkill = null,
            bool hasStencil = true)
        {
            Id = id;
            Name = name;
            Description = description;
            GridOffsets = gridOffsets;
            RequiredKinds = requiredKinds;
            Effects = effects;
            SynergyPointsRequired = synergyPointsRequired;
            UnlockedSkill = unlockedSkill;
            HasStencil = hasStencil;
        }
    }
}
