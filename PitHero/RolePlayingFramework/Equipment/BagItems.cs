using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment
{
    /// <summary>Factory for creating different types of bag items.</summary>
    public static class BagItems
    {
        /// <summary>Creates a Standard Bag (size 8).</summary>
        public static Consumable StandardBag()
        {
            return new Consumable("Standard Bag", ItemRarity.Normal, new StatBlock(0, 0, 0, 0));
        }

        /// <summary>Creates a Forager's Bag (size 12).</summary>
        public static Consumable ForagersBag()
        {
            return new Consumable("Forager's Bag", ItemRarity.Uncommon, new StatBlock(0, 0, 0, 0));
        }

        /// <summary>Creates a Traveller's Bag (size 16).</summary>
        public static Consumable TravellersBag()
        {
            return new Consumable("Traveller's Bag", ItemRarity.Rare, new StatBlock(0, 0, 0, 0));
        }

        /// <summary>Creates an Adventurer's Bag (size 24).</summary>
        public static Consumable AdventurersBag()
        {
            return new Consumable("Adventurer's Bag", ItemRarity.Epic, new StatBlock(0, 0, 0, 0));
        }

        /// <summary>Creates a Merchant's Bag (size 32).</summary>
        public static Consumable MerchantsBag()
        {
            return new Consumable("Merchant's Bag", ItemRarity.Legendary, new StatBlock(0, 0, 0, 0));
        }
    }
}