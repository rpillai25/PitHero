using RolePlayingFramework.Mercenaries;
using RolePlayingFramework.Skills;

namespace PitHero.UI
{
    /// <summary>Data for a shortcut slot - can reference either an item or a skill.</summary>
    public class ShortcutSlotData
    {
        /// <summary>Type of content in this shortcut slot.</summary>
        public ShortcutSlotType SlotType { get; set; }

        /// <summary>Referenced inventory slot (if SlotType is Item).</summary>
        public InventorySlot ReferencedSlot { get; set; }

        /// <summary>Referenced skill (if SlotType is Skill).</summary>
        public ISkill ReferencedSkill { get; set; }

        /// <summary>Mercenary that owns the referenced skill (null when the hero owns it).</summary>
        public Mercenary OwnerMercenary { get; set; }

        /// <summary>Creates an empty shortcut slot.</summary>
        public ShortcutSlotData()
        {
            SlotType = ShortcutSlotType.Empty;
            ReferencedSlot = null;
            ReferencedSkill = null;
            OwnerMercenary = null;
        }

        /// <summary>Creates a shortcut slot referencing an item.</summary>
        public static ShortcutSlotData CreateItemReference(InventorySlot slot)
        {
            return new ShortcutSlotData
            {
                SlotType = ShortcutSlotType.Item,
                ReferencedSlot = slot,
                ReferencedSkill = null
            };
        }

        /// <summary>Creates a shortcut slot referencing a hero-owned skill.</summary>
        public static ShortcutSlotData CreateSkillReference(ISkill skill)
        {
            return CreateSkillReference(skill, null);
        }

        /// <summary>Creates a shortcut slot referencing a skill owned by a mercenary (null owner = hero).</summary>
        public static ShortcutSlotData CreateSkillReference(ISkill skill, Mercenary ownerMercenary)
        {
            return new ShortcutSlotData
            {
                SlotType = ShortcutSlotType.Skill,
                ReferencedSlot = null,
                ReferencedSkill = skill,
                OwnerMercenary = ownerMercenary
            };
        }

        /// <summary>Clears the shortcut slot.</summary>
        public void Clear()
        {
            SlotType = ShortcutSlotType.Empty;
            ReferencedSlot = null;
            ReferencedSkill = null;
            OwnerMercenary = null;
        }
    }

    /// <summary>Type of content in a shortcut slot.</summary>
    public enum ShortcutSlotType
    {
        Empty,
        Item,
        Skill
    }
}
