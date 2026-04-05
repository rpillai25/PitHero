using Nez;
using PitHero;
using PitHero.Services;

namespace RolePlayingFramework.Equipment
{
    /// <summary>Abstract base consumable with one-time effect.</summary>
    public abstract class Consumable : IItem
    {
        private string _nameKey;
        private string _descKey;

        public string Name => Core.Services.GetService<TextService>()?.DisplayText(TextType.Inventory, _nameKey) ?? _nameKey;

        /// <summary>Sprite name used to look up the item's sprite in the Items atlas. Returns the concrete class name.</summary>
        public string SpriteName => GetType().Name;

        public ItemKind Kind => ItemKind.Consumable;
        public ItemRarity Rarity { get; }
        public string Description
        {
            get => Core.Services.GetService<TextService>()?.DisplayText(TextType.Inventory, _descKey) ?? _descKey;
            protected set => _descKey = value;
        }
        public int Price { get; protected set; }
        public int HPRestoreAmount { get; protected set; }
        public int MPRestoreAmount { get; protected set; }
        public int StackSize { get; protected set; }
        public int StackCount { get; set; }

        /// <summary>True if this consumable can only be used during battle.</summary>
        public bool BattleOnly { get; protected set; }

        protected Consumable(string name, ItemRarity rarity, string description, int price, int hpRestoreAmount = 0, int mpRestoreAmount = 0, bool battleOnly = false)
        {
            _nameKey = name;
            _descKey = description;
            Rarity = rarity;
            Price = price;
            HPRestoreAmount = hpRestoreAmount;
            MPRestoreAmount = mpRestoreAmount;
            StackSize = 16;
            StackCount = 1;
            BattleOnly = battleOnly;
        }

        /// <summary>Consume this item and apply its effect.</summary>
        public virtual bool Consume(object context)
        {
            return true; // base does nothing
        }
    }
}
