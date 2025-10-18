using RolePlayingFramework.Stats;

namespace RolePlayingFramework.Equipment
{
    /// <summary>Factory for creating concrete potion items.</summary>
    public static class PotionItems
    {
        /// <summary>Create HP Potion.</summary>
        public static HPPotion HPPotion() => new HPPotion();
        /// <summary>Create MP Potion.</summary>
        public static MPPotion MPPotion() => new MPPotion();
        /// <summary>Create Mix Potion.</summary>
        public static MixPotion MixPotion() => new MixPotion();
        /// <summary>Create Mid HP Potion.</summary>
        public static MidHPPotion MidHPPotion() => new MidHPPotion();
        /// <summary>Create Mid MP Potion.</summary>
        public static MidMPPotion MidMPPotion() => new MidMPPotion();
        /// <summary>Create Mid Mix Potion.</summary>
        public static MidMixPotion MidMixPotion() => new MidMixPotion();
        /// <summary>Create Full HP Potion.</summary>
        public static FullHPPotion FullHPPotion() => new FullHPPotion();
        /// <summary>Create Full MP Potion.</summary>
        public static FullMPPotion FullMPPotion() => new FullMPPotion();
        /// <summary>Create Full Mix Potion.</summary>
        public static FullMixPotion FullMixPotion() => new FullMixPotion();
    }
}