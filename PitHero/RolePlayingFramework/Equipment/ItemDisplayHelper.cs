using Microsoft.Xna.Framework;

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
                ItemKind.WeaponKnuckle => "Weapon",
                ItemKind.WeaponStaff => "Weapon",
                ItemKind.WeaponRod => "Weapon",
                ItemKind.WeaponDagger => "Weapon",
                ItemKind.WeaponBow => "Weapon",
                ItemKind.WeaponAxe => "Weapon",
                ItemKind.WeaponFist => "Weapon",
                ItemKind.WeaponClaw => "Weapon",
                ItemKind.ArmorMail => "Armor",
                ItemKind.ArmorGi => "Armor",
                ItemKind.ArmorRobe => "Armor",
                ItemKind.ArmorMedium => "Armor",
                ItemKind.ArmorLight => "Armor",
                ItemKind.ArmorHeavy => "Armor",
                ItemKind.HatHelm => "Helm",
                ItemKind.HatHeadband => "Helm",
                ItemKind.HatWizard => "Helm",
                ItemKind.HatPriest => "Helm",
                ItemKind.Shield => "Shield",
                ItemKind.Accessory => "Accessory",
                ItemKind.AccessoryBoots => "Accessory",
                ItemKind.AccessoryGloves => "Accessory",
                ItemKind.AccessoryRing => "Accessory",
                ItemKind.OrbFire => "Orb",
                ItemKind.OrbWater => "Orb",
                ItemKind.OrbEarth => "Orb",
                ItemKind.OrbWind => "Orb",
                ItemKind.OrbLight => "Orb",
                ItemKind.OrbDark => "Orb",
                ItemKind.OrbNeutral => "Orb",
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
                ItemRarity.Normal => Color.White,
                ItemRarity.Uncommon => new Color(30, 255, 0), // Green
                ItemRarity.Rare => new Color(0, 112, 221), // Blue
                ItemRarity.Epic => new Color(163, 53, 238), // Purple
                ItemRarity.Legendary => new Color(255, 128, 0), // Orange
                _ => Color.White
            };
        }
    }
}
