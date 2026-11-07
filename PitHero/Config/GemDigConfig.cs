using Microsoft.Xna.Framework;
using RolePlayingFramework.Equipment;

namespace PitHero.Config
{
    /// <summary>Gems the Digging Field can turn up (issue #452), cheapest first.</summary>
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

    /// <summary>Worthless junk the Digging Field can turn up (issue #452): worth 0 gold.</summary>
    public enum DigJunkType
    {
        Shell = 0,
        Cup = 1,
        Plate = 2,
        Spoon = 3,
        Bottle = 4,
        YarnBall = 5,
    }

    /// <summary>What one dig turned up.</summary>
    public enum DigFindKind
    {
        Nothing = 0,
        Junk = 1,
        Gem = 2,
    }

    /// <summary>The result of one dig: nothing, a piece of junk, or a gem.</summary>
    public readonly struct DigFind
    {
        /// <summary>Nothing, junk or a gem.</summary>
        public readonly DigFindKind Kind;
        /// <summary>The DigJunkType or GemType ordinal (0 for nothing).</summary>
        public readonly int Index;

        /// <summary>Creates a find.</summary>
        public DigFind(DigFindKind kind, int index)
        {
            Kind = kind;
            Index = index;
        }

        /// <summary>An empty dig.</summary>
        public static DigFind Nothing => new DigFind(DigFindKind.Nothing, 0);
        /// <summary>A gem find.</summary>
        public static DigFind OfGem(GemType gem) => new DigFind(DigFindKind.Gem, (int)gem);
        /// <summary>A junk find.</summary>
        public static DigFind OfJunk(DigJunkType junk) => new DigFind(DigFindKind.Junk, (int)junk);

        /// <summary>True for a gem.</summary>
        public bool IsGem => Kind == DigFindKind.Gem;
        /// <summary>The gem (valid when <see cref="IsGem"/>).</summary>
        public GemType Gem => (GemType)Index;
        /// <summary>The junk item (valid when Kind is Junk).</summary>
        public DigJunkType Junk => (DigJunkType)Index;

        /// <summary>Index into the outcome table: 0 nothing, 1..6 junk, 7..19 gems.</summary>
        public int OutcomeIndex => Kind == DigFindKind.Nothing ? 0
            : Kind == DigFindKind.Junk ? 1 + Index
            : 1 + GemDigConfig.JunkCount + Index;

        /// <summary>The find at an outcome-table index.</summary>
        public static DigFind FromOutcomeIndex(int i)
        {
            if (i <= 0) return Nothing;
            if (i <= GemDigConfig.JunkCount) return new DigFind(DigFindKind.Junk, i - 1);
            return new DigFind(DigFindKind.Gem, i - 1 - GemDigConfig.JunkCount);
        }

        /// <summary>Readable form for tests and logs.</summary>
        public override string ToString() => Kind == DigFindKind.Nothing ? "Nothing"
            : Kind == DigFindKind.Junk ? ((DigJunkType)Index).ToString() : ((GemType)Index).ToString();
    }

    /// <summary>
    /// What a Digging Field dig turns up (issue #452): nothing, worthless junk, or a gem.
    ///
    /// Odds are exact integer weights over <see cref="TotalWeight"/> (1,000,000):
    /// - 35% of digs turn up nothing, 30% a piece of junk worth 0 gold (Shell 7% down to Yarn Ball 3%),
    ///   and 35% a gem, so a gem is a real event between misses.
    /// - Inside the gem share each gem is about 1.485x rarer than the one listed before it
    ///   (weight = round(1000 x 1.485^(12 - ordinal)), Agate takes the rounding remainder), which puts
    ///   the Diamond at exactly 1 in 1,000 digs and keeps it the rarest gem.
    ///
    /// Per dig: Agate 1 in 9, Amethyst 1 in 13, Peridot 1 in 19, Garnet 1 in 28, Aquamarine 1 in 42,
    /// Spinel 1 in 63, Tourmaline 1 in 93, Tanzanite 1 in 138, Sapphire 1 in 206, Ruby 1 in 305,
    /// Emerald 1 in 454, Alexandrite 1 in 673, Diamond 1 in 1,000; about 43.5 gold per dig before
    /// pity (see PitHero/docs/EconomyBalanceGuide.md).
    /// </summary>
    public static class GemDigConfig
    {
        /// <summary>Number of gem types.</summary>
        public const int GemCount = 13;

        /// <summary>Number of junk types.</summary>
        public const int JunkCount = 6;

        /// <summary>Outcome table size: nothing, the junk, then the gems.</summary>
        public const int OutcomeCount = 1 + JunkCount + GemCount;

        /// <summary>Sum of every outcome weight; a dig's base roll is an int in [0, TotalWeight).</summary>
        public const int TotalWeight = 1_000_000;

        /// <summary>Weight of an empty dig (35%).</summary>
        public const int NothingWeight = 350_000;

        // 30% junk in all, the last listed rarest: Shell 7%, Cup 6%, Plate 5%, Spoon 5%, Bottle 4%, Yarn Ball 3%
        private static readonly int[] JunkWeights = { 70_000, 60_000, 50_000, 50_000, 40_000, 30_000 };

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

        // 35% gems, geometric with ratio 1.485 between neighbours; Diamond 1,000 of 1,000,000
        private static readonly int[] GemWeights =
        {
            114_937, 77_445, 52_151,
            35_119, 23_649,
            15_925, 10_724, 7_222,
            4_863, 3_275, 2_205, 1_485,
            1_000,
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

        private static readonly string[] JunkSpriteNames =
        {
            "JunkShell", "JunkCup", "JunkPlate", "JunkSpoon", "JunkBottle", "JunkYarnBall",
        };

        private static readonly string[] JunkNameKeys =
        {
            UITextKey.DigJunkShell, UITextKey.DigJunkCup, UITextKey.DigJunkPlate,
            UITextKey.DigJunkSpoon, UITextKey.DigJunkBottle, UITextKey.DigJunkYarnBall,
        };

        /// <summary>Label color for junk and empty digs: a dull DB32 gray, so gems stand out.</summary>
        public static readonly Color NoValueTextColor = new Color(0x84, 0x7e, 0x87);

        // Outcome table (0 nothing, 1..6 junk, 7..19 gems): weight and pity tier. Nothing and junk sit in
        // the base tier with the common gems, so pity on the better tiers draws its share from them.
        private static readonly int[] OutcomeWeights = BuildOutcomeWeights();
        private static readonly int[] OutcomeTiers = BuildOutcomeTiers();

        private static int[] BuildOutcomeWeights()
        {
            var w = new int[OutcomeCount];
            w[0] = NothingWeight;
            for (int i = 0; i < JunkCount; i++) w[1 + i] = JunkWeights[i];
            for (int i = 0; i < GemCount; i++) w[1 + JunkCount + i] = GemWeights[i];
            return w;
        }

        private static int[] BuildOutcomeTiers()
        {
            var t = new int[OutcomeCount];
            for (int i = 0; i < GemCount; i++) t[1 + JunkCount + i] = (int)Rarities[i];
            return t;
        }

        /// <summary>Gold the gem is worth.</summary>
        public static int GetValue(GemType gem) => Values[(int)gem];

        /// <summary>Gold a find is worth: the gem's value, 0 for junk or nothing.</summary>
        public static int GetValue(DigFind find) => find.IsGem ? Values[find.Index] : 0;

        /// <summary>Rarity tier of the gem (Normal is the issue's COMMON).</summary>
        public static ItemRarity GetRarity(GemType gem) => Rarities[(int)gem];

        /// <summary>Integer drop weight of the gem out of <see cref="TotalWeight"/>.</summary>
        public static int GetWeight(GemType gem) => GemWeights[(int)gem];

        /// <summary>Integer drop weight of the junk item out of <see cref="TotalWeight"/>.</summary>
        public static int GetWeight(DigJunkType junk) => JunkWeights[(int)junk];

        /// <summary>Exact per-dig probability of the gem.</summary>
        public static double GetProbability(GemType gem) => (double)GemWeights[(int)gem] / TotalWeight;

        /// <summary>Exact per-dig probability of a find (any kind), from the base table.</summary>
        public static double GetProbability(DigFind find) => (double)OutcomeWeights[find.OutcomeIndex] / TotalWeight;

        /// <summary>Sprite name of the gem in the CropsProps atlas.</summary>
        public static string GetSpriteName(GemType gem) => SpriteNames[(int)gem];

        /// <summary>Sprite name of the junk item in the CropsProps atlas.</summary>
        public static string GetSpriteName(DigJunkType junk) => JunkSpriteNames[(int)junk];

        /// <summary>UI localization key of the gem's display name.</summary>
        public static string GetNameKey(GemType gem) => NameKeys[(int)gem];

        /// <summary>UI localization key of the junk item's display name.</summary>
        public static string GetNameKey(DigJunkType junk) => JunkNameKeys[(int)junk];

        /// <summary>
        /// The floating label shown for a find, e.g. "Agate (+1)" or "Shell (+0)": the localized
        /// GemDigLabelFormat filled with the localized name and the gold value.
        /// </summary>
        public static string FormatLabel(string labelFormat, string name, int value)
            => string.Format(labelFormat, name, value);

        /// <summary>Color of a gem's floating label: its rarity color, so rare finds stand out.</summary>
        public static Color GetTextColor(GemType gem) => RarityUtils.GetRarityColor(Rarities[(int)gem]);

        /// <summary>Maps a base-table roll in [0, TotalWeight) to its find; out-of-range rolls clamp to the ends.</summary>
        public static DigFind FromRoll(int roll)
        {
            if (roll < 0)
                return DigFind.Nothing;
            for (int i = 0; i < OutcomeCount; i++)
            {
                roll -= OutcomeWeights[i];
                if (roll < 0)
                    return DigFind.FromOutcomeIndex(i);
            }
            return DigFind.OfGem(GemType.Diamond);
        }

        /// <summary>Rolls a find on the base table, ignoring pity (tests and tools).</summary>
        public static DigFind Roll(System.Random random) => FromRoll(random.Next(TotalWeight));

        // ── Pity (bad-luck protection) ──────────────────────────────────────────────
        //
        // Each tier above Common keeps a counter: digs since that tier or a better one last came up
        // (empty and junk digs count as digs without it). Until the counter passes the tier's soft start
        // the tier keeps its base odds; after that its odds multiplier climbs by PityGrowthPerDig x
        // PityStrength every dig, up to PityMaxMultiplier. A find resets the counter of its own tier and
        // of every tier below it (a Diamond resets all four). The counters are simulation state: saved
        // with the hero, read by the DigGem handler. Changing any knob below changes what a recorded dig
        // pays out, so bump GameConfig.SimulationVersion with it.
        //
        // Tuning (PityStrength 1, measured over 20M digs; GemDigPityTests pins the ranges): each tier's
        // odds start climbing at about its average wait, so pity mostly cuts off long dry streaks. A Rare
        // or better every ~18 digs (base 21), an Epic or better every ~67 (base 78), a Diamond every ~840
        // (base 1,000), still rarer than the Alexandrite (~1 in 580); about 51 gold per dig (base 43.5).
        // Empty digs stay ~35% and junk ~30%: pity only moves odds between the gem tiers and that pool.

        /// <summary>
        /// Global pity knob: scales every tier's growth. 0 = pure base odds (no pity), 1 = the tuned
        /// default, 2 = the odds climb twice as fast. The per-tier caps still apply.
        /// </summary>
        public const float PityStrength = 1f;

        /// <summary>Number of pity counters (Uncommon, Rare, Epic, Legendary).</summary>
        public const int PityTierCount = 4;

        /// <summary>
        /// Per tier (Uncommon, Rare, Epic, Legendary): digs without that tier before its odds start
        /// to climb. Uncommon is already 1 in 17, so its pity is off by default (growth 0).
        /// </summary>
        public static readonly int[] PitySoftStartDigs = { 0, 20, 75, 1_000 };

        /// <summary>Per tier: odds multiplier added per dig past the soft start (before PityStrength).</summary>
        public static readonly float[] PityGrowthPerDig = { 0f, 0.05f, 0.015f, 0.002f };

        /// <summary>Per tier: the highest the odds multiplier can climb (5x on Legendary = a Diamond 1 in 200).</summary>
        public static readonly float[] PityMaxMultiplier = { 1f, 3f, 4f, 5f };

        /// <summary>Cap on the combined chance of the better tiers, so the base tier never drops below 5%.</summary>
        public const double MaxNonCommonChance = 0.95;

        /// <summary>A dig's roll is a uniform integer in [0, 2^53), carried in the command's L.</summary>
        public const long RollRange = 1L << 53;

        // Base (no pity) chance of each tier, indexed by ItemRarity ordinal (tier 0 includes nothing and junk)
        private static readonly double[] BaseTierChance = BuildBaseTierChance();
        // Each tier's total base weight
        private static readonly long[] TierWeight = BuildTierWeight();

        private static double[] BuildBaseTierChance()
        {
            var chance = new double[5];
            for (int i = 0; i < OutcomeCount; i++)
                chance[OutcomeTiers[i]] += (double)OutcomeWeights[i] / TotalWeight;
            return chance;
        }

        private static long[] BuildTierWeight()
        {
            var w = new long[5];
            for (int i = 0; i < OutcomeCount; i++)
                w[OutcomeTiers[i]] += OutcomeWeights[i];
            return w;
        }

        /// <summary>Base (no pity) chance of a tier, by ItemRarity (Normal includes empty and junk digs).</summary>
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
        /// counters (Uncommon..Legendary). The better tiers are scaled down together if they would
        /// exceed <see cref="MaxNonCommonChance"/>; the base tier (common gems, junk, nothing) takes the rest.
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
        /// Maps a roll in [0, RollRange) to a find under the given pity counters. The roll picks the
        /// tier (best tier first) and its remainder picks the outcome inside the tier by the base
        /// weights, so one uniform number decides the whole dig.
        /// </summary>
        public static DigFind Resolve(long roll, int[] pity)
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
            return DigFind.Nothing;
        }

        private static DigFind PickInTier(int tier, double v)
        {
            long w = (long)(v * TierWeight[tier]);
            int last = 0;
            for (int i = 0; i < OutcomeCount; i++)
            {
                if (OutcomeTiers[i] != tier)
                    continue;
                last = i;
                w -= OutcomeWeights[i];
                if (w < 0)
                    return DigFind.FromOutcomeIndex(i);
            }
            return DigFind.FromOutcomeIndex(last);
        }

        /// <summary>
        /// Advances the pity counters after a dig: tiers at or below the find's tier reset to 0 (an
        /// empty or junk dig is in the base tier, so it resets none), better tiers count one more dig.
        /// </summary>
        public static void AdvancePity(int[] pity, DigFind found)
        {
            int foundTier = found.IsGem ? (int)Rarities[found.Index] : 0;
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
