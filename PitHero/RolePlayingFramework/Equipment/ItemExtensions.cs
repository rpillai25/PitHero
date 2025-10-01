namespace RolePlayingFramework.Equipment
{
    /// <summary>Extension methods for IItem.</summary>
    public static class ItemExtensions
    {
        /// <summary>Gets the sell price (half of buy price).</summary>
        public static int GetSellPrice(this IItem item)
        {
            return item.Price / 2;
        }
    }
}
