namespace RolePlayingFramework.Equipment
{
    /// <summary>Common interface for all inventory items.</summary>
    public interface IItem
    {
        /// <summary>
        /// Stable identity string: the localization key with Inv_/_Name stripped, plus any "+N" tier
        /// suffix (e.g. "RustyBlade", "RustyBlade+2"). Used by the item registry, saves, replay hashes
        /// and command payloads. Never shown to the player and never localized.
        /// </summary>
        string Name { get; }

        /// <summary>Localized, player-facing name (tier suffix included), e.g. "Rusty Blade+2". UI and console only.</summary>
        string DisplayName { get; }

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
