using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment
{
    /// <summary>Factory for creating different types of potion items.</summary>
    public static class PotionItems
    {
        // Normal Potions (Common rarity)
        /// <summary>Creates an HP Potion that recovers 100 HP.</summary>
        public static Consumable HPPotion()
        {
            return new Consumable("HPPotion", ItemRarity.Normal, 100, 0);
        }

        /// <summary>Creates an AP Potion that recovers 100 AP.</summary>
        public static Consumable APPotion()
        {
            return new Consumable("APPotion", ItemRarity.Normal, 0, 100);
        }

        /// <summary>Creates a Mix Potion that recovers 100 HP and AP.</summary>
        public static Consumable MixPotion()
        {
            return new Consumable("MixPotion", ItemRarity.Normal, 100, 100);
        }

        // Mid Potions (Rare rarity)
        /// <summary>Creates a Mid HP Potion that recovers 500 HP.</summary>
        public static Consumable MidHPPotion()
        {
            return new Consumable("MidHPPotion", ItemRarity.Rare, 500, 0);
        }

        /// <summary>Creates a Mid AP Potion that recovers 500 AP.</summary>
        public static Consumable MidAPPotion()
        {
            return new Consumable("MidAPPotion", ItemRarity.Rare, 0, 500);
        }

        /// <summary>Creates a Mid Mix Potion that recovers 500 HP and AP.</summary>
        public static Consumable MidMixPotion()
        {
            return new Consumable("MidMixPotion", ItemRarity.Rare, 500, 500);
        }

        // Full Potions (Epic rarity)
        /// <summary>Creates a Full HP Potion that recovers all HP.</summary>
        public static Consumable FullHPPotion()
        {
            return new Consumable("FullHPPotion", ItemRarity.Epic, -1, 0); // -1 indicates full restore
        }

        /// <summary>Creates a Full AP Potion that recovers all AP.</summary>
        public static Consumable FullAPPotion()
        {
            return new Consumable("FullAPPotion", ItemRarity.Epic, 0, -1); // -1 indicates full restore
        }

        /// <summary>Creates a Full Mix Potion that recovers all HP and AP.</summary>
        public static Consumable FullMixPotion()
        {
            return new Consumable("FullMixPotion", ItemRarity.Epic, -1, -1); // -1 indicates full restore
        }
    }
}