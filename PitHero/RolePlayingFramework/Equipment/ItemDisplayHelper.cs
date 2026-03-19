using Microsoft.Xna.Framework;
using RolePlayingFramework.Jobs;
using System.Collections.Generic;

namespace RolePlayingFramework.Equipment
{
    /// <summary>Helper methods for item display.</summary>
    public static class ItemDisplayHelper
    {
        /// <summary>Gets the display type string for an item kind.</summary>
        public static string GetItemTypeString(ItemKind kind)
        {
            return kind switch
            {
                ItemKind.Consumable => "Consumable",
                ItemKind.WeaponSword => "Weapon",
                ItemKind.WeaponKnife => "Weapon",
                ItemKind.WeaponKnuckle => "Weapon",
                ItemKind.WeaponStaff => "Weapon",
                ItemKind.WeaponRod => "Weapon",
                ItemKind.WeaponHammer => "Weapon",
                ItemKind.ArmorMail => "Armor",
                ItemKind.ArmorGi => "Armor",
                ItemKind.ArmorRobe => "Armor",
                ItemKind.HatHelm => "Helm",
                ItemKind.HatHeadband => "Helm",
                ItemKind.HatWizard => "Helm",
                ItemKind.HatPriest => "Helm",
                ItemKind.Shield => "Shield",
                ItemKind.Accessory => "Accessory",
                _ => "Unknown"
            };
        }

        /// <summary>Gets the display rarity string.</summary>
        public static string GetRarityString(ItemRarity rarity)
        {
            return rarity switch
            {
                ItemRarity.Normal => "Common",
                ItemRarity.Uncommon => "Uncommon",
                ItemRarity.Rare => "Rare",
                ItemRarity.Epic => "Epic",
                ItemRarity.Legendary => "Legendary",
                _ => "Unknown"
            };
        }

        /// <summary>Gets the color for an item rarity.</summary>
        public static Color GetRarityColor(ItemRarity rarity)
        {
            return rarity switch
            {
                ItemRarity.Normal => new Color(71, 36, 7),
                ItemRarity.Uncommon => new Color(42, 153, 28), // Green
                ItemRarity.Rare => new Color(0, 112, 221), // Blue
                ItemRarity.Epic => new Color(163, 53, 238), // Purple
                ItemRarity.Legendary => new Color(255, 128, 0), // Orange
                _ => Color.White
            };
        }

        /// <summary>Formats a JobType bitflag into a comma-separated list of job names.</summary>
        public static string FormatAllowedJobs(JobType jobs)
        {
            var parts = new List<string>(6);
            if ((jobs & JobType.Knight) != 0) parts.Add("Knight");
            if ((jobs & JobType.Monk) != 0) parts.Add("Monk");
            if ((jobs & JobType.Mage) != 0) parts.Add("Mage");
            if ((jobs & JobType.Priest) != 0) parts.Add("Priest");
            if ((jobs & JobType.Thief) != 0) parts.Add("Thief");
            if ((jobs & JobType.Archer) != 0) parts.Add("Archer");
            return string.Join(", ", parts);
        }
    }
}
