namespace PitHero.RolePlayingSystem.Items
{
    public class Item
    {
        /// <summary>
        /// Buying price.
        /// -1 means can't buy.
        /// </summary>
        public int BuyPrice;

        /// <summary>
        /// Selling price.
        /// -1 means can't sell.
        /// </summary>
        public int SellPrice;

        /// <summary>
        /// Display Name
        /// </summary>
        public string Name;

        /// <summary>
        /// Item description
        /// </summary>
        public string Description;
    }
}
