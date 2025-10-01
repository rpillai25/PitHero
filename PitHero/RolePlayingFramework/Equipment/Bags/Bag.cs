using PitHero.ECS.Components;
using RolePlayingFramework.Inventory;

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
            ItemBag target = null;
            if (context is HeroComponent heroComponent)
                target = heroComponent.Bag;
            else if (context is ItemBag ib)
                target = ib;
            if (target == null) return false;
            return target.TryUpgrade(this);
        }
    }
}
