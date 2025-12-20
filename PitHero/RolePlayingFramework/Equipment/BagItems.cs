namespace RolePlayingFramework.Equipment
{
    /// <summary>Factory for creating different types of bag items.</summary>
    public static class BagItems
    {
        /// <summary>Create Standard Bag consumable (capacity 12).</summary>
        public static Bag StandardBag() => new StandardBag();
        /// <summary>Create Forager's Bag consumable (capacity 16).</summary>
        public static Bag ForagersBag() => new ForagersBag();
        /// <summary>Create Traveller's Bag consumable (capacity 20).</summary>
        public static Bag TravellersBag() => new TravellersBag();
        /// <summary>Create Adventurer's Bag consumable (capacity 24).</summary>
        public static Bag AdventurersBag() => new AdventurersBag();
        /// <summary>Create Merchant's Bag consumable (capacity 32).</summary>
        public static Bag MerchantsBag() => new MerchantsBag();
    }
}