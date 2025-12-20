namespace RolePlayingFramework.Equipment
{
    /// <summary>Base class for bag upgrade consumables.</summary>
    public abstract class Bag : Consumable
    {
        protected Bag(string name, ItemRarity rarity, string description, int price)
            : base(name, rarity, description, price)
        {
            StackSize = 1; // Bags don't stack
        }
        /// <summary>Consume and attempt ItemBag upgrade using context.</summary>
        public override bool Consume(object context)
        {
            // Bag upgrades are no longer used - inventory has fixed capacity
            return false;
        }
    }
}
