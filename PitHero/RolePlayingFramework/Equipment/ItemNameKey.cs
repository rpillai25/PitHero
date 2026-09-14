namespace RolePlayingFramework.Equipment
{
    /// <summary>
    /// Derives an item's stable identity string from its Inventory localization key (issue #413):
    /// "Inv_RustyBlade_Name" → "RustyBlade". This is what <see cref="IItem.Name"/> returns and what
    /// the registry, saves, replay hashes and command payloads use; the player-facing spaced text
    /// lives in <see cref="IItem.DisplayName"/>. Strings without the key affixes pass through unchanged.
    /// </summary>
    public static class ItemNameKey
    {
        private const string Prefix = "Inv_";
        private const string Suffix = "_Name";

        /// <summary>Strips the Inv_/_Name affixes from a localization key; other strings return as-is.</summary>
        public static string Strip(string key)
        {
            if (key == null)
                return null;
            if (key.Length > Prefix.Length + Suffix.Length && key.StartsWith(Prefix) && key.EndsWith(Suffix))
                return key.Substring(Prefix.Length, key.Length - Prefix.Length - Suffix.Length);
            return key;
        }
    }
}
