using Microsoft.Xna.Framework;
using RolePlayingFramework.Equipment;

namespace PitHero.Config
{
    /// <summary>
    /// Gems the gem dig site can turn up (issue #452). The ordinal travels in the DigGem command
    /// payload and is recorded in replays: append only, never renumber.
    /// </summary>
    public enum GemType
    {
        Agate = 0,
        Amethyst = 1,
        Peridot = 2,
        Garnet = 3,
        Aquamarine = 4,
        Spinel = 5,
        Tourmaline = 6,
        Tanzanite = 7,
        Sapphire = 8,
        Ruby = 9,
        Emerald = 10,
        Alexandrite = 11,
        Diamond = 12,
    }

    /// <summary>
    /// Gem values, rarities and drop odds for the gem dig site (issue #452).
    ///
    /// Odds are exact integer weights over <see cref="TotalWeight"/>, built in two steps:
    /// 1. Each rarity tier is 20x rarer than the one before it: Uncommon 1 in 20 digs, Rare 1 in 400,
    ///    Epic 1 in 8,000, Legendary 1 in 160,000. Common takes the rest (~94.7%).
    /// 2. Inside a tier, each gem is half as likely as the gem listed before it (weights 4:2:1,
    ///    2:1, 8:4:2:1), so the last gem of a tier is always the rarest of its tier.
    /// TotalWeight = 160,000 x 105 (105 = lcm of the in-tier denominators 7, 3 and 15) keeps every
    /// weight a whole number, so the table is exact rather than float-rounded.
    ///
    /// Resulting odds: Agate 54.1%, Amethyst 27.1%, Peridot 13.5%, Garnet 1 in 30, Aquamarine 1 in 60,
    /// Spinel 1 in 700, Tourmaline 1 in 1,400, Tanzanite 1 in 2,800, Sapphire 1 in 15,000,
    /// Ruby 1 in 30,000, Emerald 1 in 60,000, Alexandrite 1 in 120,000, Diamond 1 in 160,000.
    /// Expected value is about 3.8 gold per dig (see PitHero/docs/EconomyBalanceGuide.md).
    /// </summary>
    public static class GemDigConfig
    {
        /// <summary>Number of gem types.</summary>
        public const int GemCount = 13;

        /// <summary>Sum of <see cref="Weights"/>; a dig rolls an int in [0, TotalWeight).</summary>
        public const int TotalWeight = 16_800_000;

        private static readonly int[] Values =
        {
            1, 2, 5,                // Common
            10, 50,                 // Uncommon
            100, 300, 700,          // Rare
            1000, 1500, 2000, 5000, // Epic
            10000,                  // Legendary
        };

        private static readonly ItemRarity[] Rarities =
        {
            ItemRarity.Normal, ItemRarity.Normal, ItemRarity.Normal,
            ItemRarity.Uncommon, ItemRarity.Uncommon,
            ItemRarity.Rare, ItemRarity.Rare, ItemRarity.Rare,
            ItemRarity.Epic, ItemRarity.Epic, ItemRarity.Epic, ItemRarity.Epic,
            ItemRarity.Legendary,
        };

        // Common 15,915,795 (4:2:1) | Uncommon 840,000 (2:1) | Rare 42,000 (4:2:1)
        // Epic 2,100 (8:4:2:1) | Legendary 105
        private static readonly int[] Weights =
        {
            9_094_740, 4_547_370, 2_273_685,
            560_000, 280_000,
            24_000, 12_000, 6_000,
            1_120, 560, 280, 140,
            105,
        };

        private static readonly string[] SpriteNames =
        {
            "GemAgate", "GemAmethyst", "GemPeridot", "GemGarnet", "GemAquamarine", "GemSpinel",
            "GemTourmaline", "GemTanzanite", "GemSapphire", "GemRuby", "GemEmerald", "GemAlexandrite",
            "GemDiamond",
        };

        private static readonly string[] NameKeys =
        {
            UITextKey.GemAgate, UITextKey.GemAmethyst, UITextKey.GemPeridot, UITextKey.GemGarnet,
            UITextKey.GemAquamarine, UITextKey.GemSpinel, UITextKey.GemTourmaline, UITextKey.GemTanzanite,
            UITextKey.GemSapphire, UITextKey.GemRuby, UITextKey.GemEmerald, UITextKey.GemAlexandrite,
            UITextKey.GemDiamond,
        };

        /// <summary>True for a defined gem ordinal (commands are re-validated with this).</summary>
        public static bool IsValid(int ordinal) => ordinal >= 0 && ordinal < GemCount;

        /// <summary>Gold the gem is worth.</summary>
        public static int GetValue(GemType gem) => Values[(int)gem];

        /// <summary>Rarity tier of the gem (Normal is the issue's COMMON).</summary>
        public static ItemRarity GetRarity(GemType gem) => Rarities[(int)gem];

        /// <summary>Integer drop weight out of <see cref="TotalWeight"/>.</summary>
        public static int GetWeight(GemType gem) => Weights[(int)gem];

        /// <summary>Exact drop probability of the gem.</summary>
        public static double GetProbability(GemType gem) => (double)Weights[(int)gem] / TotalWeight;

        /// <summary>Sprite name of the gem in the CropsProps atlas.</summary>
        public static string GetSpriteName(GemType gem) => SpriteNames[(int)gem];

        /// <summary>UI localization key of the gem's display name.</summary>
        public static string GetNameKey(GemType gem) => NameKeys[(int)gem];

        /// <summary>
        /// The floating label shown when the gem is dug up, e.g. "Agate (+1)": the localized
        /// GemDigLabelFormat filled with the localized name and the gold value.
        /// </summary>
        public static string FormatLabel(string labelFormat, string gemName, GemType gem)
            => string.Format(labelFormat, gemName, Values[(int)gem]);

        /// <summary>Color of the floating gold text: the rarity color, so rare finds stand out.</summary>
        public static Color GetTextColor(GemType gem) => RarityUtils.GetRarityColor(Rarities[(int)gem]);

        /// <summary>Maps a roll in [0, TotalWeight) to its gem; out-of-range rolls clamp to the ends.</summary>
        public static GemType FromRoll(int roll)
        {
            if (roll < 0)
                return GemType.Agate;
            for (int i = 0; i < GemCount; i++)
            {
                roll -= Weights[i];
                if (roll < 0)
                    return (GemType)i;
            }
            return GemType.Diamond;
        }

        /// <summary>Rolls a gem on the given random source (callers pass GameRandom.Ui).</summary>
        public static GemType Roll(System.Random random) => FromRoll(random.Next(TotalWeight));
    }
}
