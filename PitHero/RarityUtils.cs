using Microsoft.Xna.Framework;
using RolePlayingFramework.Equipment;

namespace PitHero
{
    /// <summary>Utility methods for item rarity and color handling.</summary>
    public static class RarityUtils
    {
        /// <summary>Gets the display color for an item rarity.</summary>
        public static Color GetRarityColor(ItemRarity rarity)
        {
            return rarity switch
            {
                ItemRarity.Normal => GameConfig.RARITY_NORMAL,
                ItemRarity.Uncommon => GameConfig.RARITY_UNCOMMON,
                ItemRarity.Rare => GameConfig.RARITY_RARE,
                ItemRarity.Epic => GameConfig.RARITY_EPIC,
                ItemRarity.Legendary => GameConfig.RARITY_LEGENDARY,
                _ => GameConfig.RARITY_NORMAL
            };
        }

        /// <summary>Gets the treasure level (1-5) that corresponds to an item rarity.</summary>
        public static int GetTreasureLevelForRarity(ItemRarity rarity)
        {
            return rarity switch
            {
                ItemRarity.Normal => 1,      // Brown chest
                ItemRarity.Uncommon => 2,    // Green chest
                ItemRarity.Rare => 3,        // Blue chest
                ItemRarity.Epic => 4,        // Purple chest
                ItemRarity.Legendary => 5,   // Gold chest
                _ => 1
            };
        }

        /// <summary>Gets the item rarity that corresponds to a treasure level (1-5).</summary>
        public static ItemRarity GetRarityForTreasureLevel(int treasureLevel)
        {
            return treasureLevel switch
            {
                1 => ItemRarity.Normal,
                2 => ItemRarity.Uncommon,
                3 => ItemRarity.Rare,
                4 => ItemRarity.Epic,
                5 => ItemRarity.Legendary,
                _ => ItemRarity.Normal
            };
        }

        /// <summary>
        /// Gets the rarity for gear at the given pit level, anchored to the biome it belongs to.
        /// Every 25 pit levels is a new biome; rarity resets each biome.
        /// Distribution: Normal (biome levels 1-16), Uncommon (17-21), Rare (22-24), Epic (25).
        /// </summary>
        public static ItemRarity GetRarityForBiomeLevel(int pitLevel)
        {
            int biomeLevel = ((pitLevel - 1) % 25) + 1;
            if (biomeLevel <= 16) return ItemRarity.Normal;
            if (biomeLevel <= 21) return ItemRarity.Uncommon;
            if (biomeLevel <= 24) return ItemRarity.Rare;
            return ItemRarity.Epic;
        }
    }
}