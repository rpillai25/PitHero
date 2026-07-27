namespace RolePlayingFramework.Equipment
{
    /// <summary>
    /// Ordered catalog of every consumable the player can own, used by the auto-purchase options
    /// dialog, <c>AutoItemPurchaseService</c> and the save file (issue #345).
    /// The index is a persisted identity: <b>append new consumables to the end only</b>, never
    /// reorder or remove entries, or existing saves will map their selections onto the wrong item.
    /// </summary>
    public static class ConsumableCatalog
    {
        /// <summary>Number of catalog entries.</summary>
        public const int Count = 9;

        // Template instances, created lazily once. Templates are never handed out to the bag —
        // callers use CreateFresh so each purchased stack owns its own StackCount.
        private static Consumable[] _templates;

        private static Consumable[] Templates
        {
            get
            {
                if (_templates == null)
                {
                    _templates = new Consumable[Count];
                    for (int i = 0; i < Count; i++)
                        _templates[i] = CreateFresh(i);
                }
                return _templates;
            }
        }

        /// <summary>Creates a brand new instance of the catalog entry at the given index (StackCount = 1).</summary>
        public static Consumable CreateFresh(int index)
        {
            switch (index)
            {
                case 0: return PotionItems.HPPotion();
                case 1: return PotionItems.MPPotion();
                case 2: return PotionItems.MixPotion();
                case 3: return PotionItems.MidHPPotion();
                case 4: return PotionItems.MidMPPotion();
                case 5: return PotionItems.MidMixPotion();
                case 6: return PotionItems.FullHPPotion();
                case 7: return PotionItems.FullMPPotion();
                case 8: return PotionItems.FullMixPotion();
                default: return null;
            }
        }

        /// <summary>Shared read-only template for the catalog entry at the given index. Do not mutate.</summary>
        public static Consumable GetTemplate(int index)
        {
            if (index < 0 || index >= Count)
                return null;
            return Templates[index];
        }

        /// <summary>Atlas sprite name for the catalog entry at the given index.</summary>
        public static string GetSpriteName(int index)
        {
            var template = GetTemplate(index);
            return template != null ? template.SpriteName : null;
        }

        /// <summary>Localized display name for the catalog entry at the given index.</summary>
        public static string GetDisplayName(int index)
        {
            var template = GetTemplate(index);
            return template != null ? template.Name : null;
        }

        /// <summary>Catalog index of the consumable with the given sprite name, or -1 when unknown.</summary>
        public static int IndexOfSpriteName(string spriteName)
        {
            if (string.IsNullOrEmpty(spriteName))
                return -1;
            for (int i = 0; i < Count; i++)
            {
                if (Templates[i].SpriteName == spriteName)
                    return i;
            }
            return -1;
        }
    }
}
