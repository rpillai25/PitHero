namespace RolePlayingFramework.Equipment
{
    /// <summary>Common interface for all inventory items.</summary>
    public interface IItem
    {
        /// <summary>Display name.</summary>
        string Name { get; }

        /// <summary>Sprite name used to look up the item's sprite in the Items atlas.</summary>
        string SpriteName { get; }

        /// <summary>Item category.</summary>
        ItemKind Kind { get; }

        /// <summary>Item rarity level.</summary>
        ItemRarity Rarity { get; }

        /// <summary>Item description.</summary>
        string Description { get; }

        /// <summary>Buy price in gold.</summary>
        int Price { get; }
    }
}
