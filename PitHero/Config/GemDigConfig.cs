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
    /// These are the base odds: expected value about 3.8 gold per dig. Pity (below) raises a tier's
    /// odds the longer it has not come up, which lifts the average to about 5.4 gold per dig
    /// (see PitHero/docs/EconomyBalanceGuide.md).
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

        /// <summary>Rolls a gem on the base table, ignoring pity (tests and tools).</summary>
        public static GemType Roll(System.Random random) => FromRoll(random.Next(TotalWeight));

        // ── Pity (bad-luck protection) ──────────────────────────────────────────────
        //
        // Each tier above Common keeps a counter: digs since that tier or a better one last came up.
        // Until the counter passes the tier's soft start the tier keeps its base odds; after that its
        // odds multiplier climbs by PityGrowthPerDig x PityStrength every dig, up to PityMaxMultiplier.
        // A find resets the counter of its own tier and of every tier below it (a Diamond resets
        // all four). The counters are simulation state: saved with the hero, read by the DigGem
        // handler. Changing any knob below changes what a recorded dig pays out, so bump
        // GameConfig.SimulationVersion with it.
        //
        // Tuning (PityStrength 1, measured over 20M digs; GemDigPityTests pins the ranges): a Rare or
        // better every ~160 digs (base 400), an Epic or better every ~1,740 (base 8,000), a Diamond
        // every ~35,000 (base 160,000), still rarer than Alexandrite (~1 in 27,700); about 5.4 gold
        // per dig (base 3.8). The Legendary cap is what keeps Diamond the rarest gem: raising it past
        // ~10x lets Diamond overtake Alexandrite. See PitHero/docs/EconomyBalanceGuide.md.

        /// <summary>
        /// Global pity knob: scales every tier's growth. 0 = pure base odds (no pity), 1 = the tuned
        /// default, 2 = the odds climb twice as fast. The per-tier caps still apply.
        /// </summary>
        public const float PityStrength = 1f;

        /// <summary>Number of pity counters (Uncommon, Rare, Epic, Legendary).</summary>
        public const int PityTierCount = 4;

        /// <summary>
        /// Per tier (Uncommon, Rare, Epic, Legendary): digs without that tier before its odds start
        /// to climb. Uncommon is already 1 in 20, so its pity is off by default (growth 0).
        /// </summary>
        public static readonly int[] PitySoftStartDigs = { 0, 100, 600, 10_000 };

        /// <summary>Per tier: odds multiplier added per dig past the soft start (before PityStrength).</summary>
        public static readonly float[] PityGrowthPerDig = { 0f, 0.04f, 0.006f, 0.0004f };

        /// <summary>Per tier: the highest the odds multiplier can climb (10x on Rare = 1 in 40).</summary>
        public static readonly float[] PityMaxMultiplier = { 1f, 10f, 25f, 8f };

        /// <summary>Cap on the combined chance of a non-Common gem, so Common never drops below 5%.</summary>
        public const double MaxNonCommonChance = 0.95;

        /// <summary>A dig's roll is a uniform integer in [0, 2^53), carried in the command's L.</summary>
        public const long RollRange = 1L << 53;

        // Base (no pity) chance of each tier, indexed by ItemRarity ordinal
        private static readonly double[] BaseTierChance = BuildBaseTierChance();
        // Each tier's first gem ordinal and gem count
        private static readonly int[] TierFirstGem = { 0, 3, 5, 8, 12 };
        private static readonly int[] TierGemCount = { 3, 2, 3, 4, 1 };

        private static double[] BuildBaseTierChance()
        {
            var chance = new double[5];
            for (int i = 0; i < GemCount; i++)
                chance[(int)Rarities[i]] += (double)Weights[i] / TotalWeight;
            return chance;
        }

        /// <summary>Base (no pity) chance of a tier, by ItemRarity.</summary>
        public static double GetBaseTierChance(ItemRarity tier) => BaseTierChance[(int)tier];

        /// <summary>Odds multiplier a tier (pity index 0..3 = Uncommon..Legendary) has after `digs` digs without it.</summary>
        public static double GetPityMultiplier(int pityIndex, int digs)
        {
            int past = digs - PitySoftStartDigs[pityIndex];
            if (past <= 0)
                return 1.0;
            double m = 1.0 + (double)PityStrength * PityGrowthPerDig[pityIndex] * past;
            double cap = PityMaxMultiplier[pityIndex];
            return m < cap ? m : cap;
        }

        /// <summary>
        /// Fills tierChance (5 entries, by ItemRarity) with this dig's tier odds under the given pity
        /// counters (Uncommon..Legendary). Non-Common odds are scaled down together if they would
        /// exceed <see cref="MaxNonCommonChance"/>; Common takes the rest.
        /// </summary>
        public static void GetTierChances(int[] pity, double[] tierChance)
        {
            double nonCommon = 0;
            for (int t = 1; t < 5; t++)
            {
                double c = BaseTierChance[t] * GetPityMultiplier(t - 1, pity != null ? pity[t - 1] : 0);
                tierChance[t] = c;
                nonCommon += c;
            }
            if (nonCommon > MaxNonCommonChance)
            {
                double scale = MaxNonCommonChance / nonCommon;
                for (int t = 1; t < 5; t++)
                    tierChance[t] *= scale;
                nonCommon = MaxNonCommonChance;
            }
            tierChance[0] = 1.0 - nonCommon;
        }

        [System.ThreadStatic] private static double[] _scratchChance;

        /// <summary>
        /// Maps a roll in [0, RollRange) to a gem under the given pity counters. The roll picks the
        /// tier (best tier first) and its remainder picks the gem inside the tier by the base in-tier
        /// weights, so one uniform number decides the whole dig.
        /// </summary>
        public static GemType Resolve(long roll, int[] pity)
        {
            var chance = _scratchChance ??= new double[5];
            GetTierChances(pity, chance);

            if (roll < 0) roll = 0;
            if (roll >= RollRange) roll = RollRange - 1;
            double u = roll / (double)RollRange;

            double lo = 0;
            for (int t = 4; t >= 0; t--)
            {
                double c = chance[t];
                if (u < lo + c || t == 0)
                {
                    double v = c > 0 ? (u - lo) / c : 0; // position inside the tier, [0, 1)
                    return PickInTier(t, v);
                }
                lo += c;
            }
            return GemType.Agate;
        }

        private static GemType PickInTier(int tier, double v)
        {
            int first = TierFirstGem[tier];
            int count = TierGemCount[tier];
            long tierWeight = 0;
            for (int i = 0; i < count; i++)
                tierWeight += Weights[first + i];
            long w = (long)(v * tierWeight);
            for (int i = 0; i < count; i++)
            {
                w -= Weights[first + i];
                if (w < 0)
                    return (GemType)(first + i);
            }
            return (GemType)(first + count - 1);
        }

        /// <summary>
        /// Advances the pity counters after a dig: tiers at or below the found gem's tier reset to 0,
        /// better tiers count one more dig without them.
        /// </summary>
        public static void AdvancePity(int[] pity, GemType found)
        {
            int foundTier = (int)Rarities[(int)found];
            for (int i = 0; i < PityTierCount; i++)
            {
                int tier = i + 1;
                if (tier <= foundTier)
                    pity[i] = 0;
                else if (pity[i] < int.MaxValue)
                    pity[i]++;
            }
        }
    }
}
