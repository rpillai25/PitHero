namespace RolePlayingFramework.Equipment
{
    /// <summary>Extension methods for IItem.</summary>
    public static class ItemExtensions
    {
        /// <summary>
        /// Gets the sell price. Consumables sell for half their buy price (preserves the potion
        /// buyback economy). Gear sells for a rarity-scaled fraction of buy price so common pit
        /// loot doesn't flood the player with gold. See issue #287.
        /// </summary>
        public static int GetSellPrice(this IItem item)
        {
            if (item.Kind == ItemKind.Consumable)
                return item.Price / 2;

            int percent = item.Rarity switch
            {
                ItemRarity.Normal    => 20,
                ItemRarity.Uncommon  => 35,
                ItemRarity.Rare      => 50,
                ItemRarity.Epic      => 60,
                ItemRarity.Legendary => 75,
                _                    => 20,
            };
            return item.Price * percent / 100;
        }
    }
}
