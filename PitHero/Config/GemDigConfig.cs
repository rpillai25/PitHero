using Microsoft.Xna.Framework;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Utils;

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
    /// Every dig draws one marble from a 500-marble shuffle bag (<see cref="BagSize"/>) without
    /// replacement, so every 500 digs hold exactly the composition below: half empty digs and junk
    /// worth 0 gold, half gems, with exactly one Diamond. The bag lives on
    /// <see cref="PitHero.Services.DiggingFieldService"/>.
    ///
    /// No gold (250): nothing 135; junk 115 = Shell 27, Cup 23, Plate 19, Spoon 19, Bottle 15, Yarn Ball 12.
    /// Gems (250): Agate 100, Amethyst 60, Peridot 36 (Common 196); Garnet 18, Aquamarine 12
    /// (Uncommon 30); Spinel 8, Tourmaline 5, Tanzanite 4 (Rare 17); Sapphire 2, Ruby 2, Emerald 1,
    /// Alexandrite 1 (Epic 6); Diamond 1 (Legendary). 28,280 gold per bag: about 56.6 gold per dig
    /// (see PitHero/docs/EconomyBalanceGuide.md). Changing a marble count changes what a recorded dig
    /// pays out, so bump GameConfig.SimulationVersion with it.
    /// </summary>
    public static class GemDigConfig
    {
        /// <summary>Number of gem types.</summary>
        public const int GemCount = 13;

        /// <summary>Number of junk types.</summary>
        public const int JunkCount = 6;

        /// <summary>Outcome table size: nothing, the junk, then the gems.</summary>
        public const int OutcomeCount = 1 + JunkCount + GemCount;

        /// <summary>Marbles in one full dig bag; every 500 digs contain exactly the composition below.</summary>
        public const int BagSize = 500;

        /// <summary>Marbles for an empty dig: 135 of the 250 non-gem marbles (the old 35:30 nothing-to-junk split).</summary>
        public const int NothingMarbles = 135;

        // 115 junk, the last listed rarest (the old 7:6:5:5:4:3 split)
        private static readonly int[] JunkMarbles = { 27, 23, 19, 19, 15, 12 };

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

        // 250 gems, none more common than the gem listed before it; exactly one Diamond
        private static readonly int[] GemMarbles =
        {
            100, 60, 36,
            18, 12,
            8, 5, 4,
            2, 2, 1, 1,
            1,
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

        // Outcome table (0 nothing, 1..6 junk, 7..19 gems): marbles per outcome
        private static readonly int[] OutcomeMarbles = BuildOutcomeMarbles();

        private static int[] BuildOutcomeMarbles()
        {
            var m = new int[OutcomeCount];
            m[0] = NothingMarbles;
            for (int i = 0; i < JunkCount; i++) m[1 + i] = JunkMarbles[i];
            for (int i = 0; i < GemCount; i++) m[1 + JunkCount + i] = GemMarbles[i];
            return m;
        }

        /// <summary>Gold the gem is worth.</summary>
        public static int GetValue(GemType gem) => Values[(int)gem];

        /// <summary>Gold a find is worth: the gem's value, 0 for junk or nothing.</summary>
        public static int GetValue(DigFind find) => find.IsGem ? Values[find.Index] : 0;

        /// <summary>Rarity tier of the gem (Normal is the issue's COMMON).</summary>
        public static ItemRarity GetRarity(GemType gem) => Rarities[(int)gem];

        /// <summary>Marbles the gem has in the 500-marble dig bag.</summary>
        public static int GetMarbles(GemType gem) => GemMarbles[(int)gem];

        /// <summary>Marbles the junk item has in the 500-marble dig bag.</summary>
        public static int GetMarbles(DigJunkType junk) => JunkMarbles[(int)junk];

        /// <summary>Marbles a find (any kind) has in the 500-marble dig bag.</summary>
        public static int GetMarbles(DigFind find) => OutcomeMarbles[find.OutcomeIndex];

        /// <summary>Long-run share of digs that turn up the gem (marbles / bag size).</summary>
        public static double GetProbability(GemType gem) => (double)GemMarbles[(int)gem] / BagSize;

        /// <summary>Long-run share of digs that turn up a find (any kind).</summary>
        public static double GetProbability(DigFind find) => (double)OutcomeMarbles[find.OutcomeIndex] / BagSize;

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

        /// <summary>Uncommon gem label color: DB32 green (10), lighter than the item rarity green so it reads on the field.</summary>
        public static readonly Color UncommonLabelColor = new Color(0x6a, 0xbe, 0x30);

        /// <summary>Rare gem label color: DB32 sky blue (18), lighter than the item rarity blue so it reads on the field.</summary>
        public static readonly Color RareLabelColor = new Color(0x63, 0x9b, 0xff);

        /// <summary>
        /// Color of a gem's floating label: its rarity color, so rare finds stand out. Uncommon and Rare
        /// use lighter shades of the item rarity green and blue, which are too dark to read over the
        /// field; the item rarity colors elsewhere are unchanged.
        /// </summary>
        public static Color GetTextColor(GemType gem)
        {
            var rarity = Rarities[(int)gem];
            if (rarity == ItemRarity.Uncommon)
                return UncommonLabelColor;
            if (rarity == ItemRarity.Rare)
                return RareLabelColor;
            return RarityUtils.GetRarityColor(rarity);
        }

        /// <summary>
        /// Builds a full dig bag of outcome indices (see <see cref="DigFind.OutcomeIndex"/>) in outcome
        /// order. The order is part of the draw contract: a recorded dig replays to the same find.
        /// </summary>
        public static ShuffleBag<int> BuildBag()
        {
            var bag = new ShuffleBag<int>(BagSize);
            for (int i = 0; i < OutcomeCount; i++)
                if (OutcomeMarbles[i] > 0)
                    bag.Add(i, OutcomeMarbles[i]);
            return bag;
        }

        /// <summary>A dig's roll is a uniform integer in [0, 2^53), carried in the command's L.</summary>
        public const long RollRange = 1L << 53;

        /// <summary>Maps a dig roll in [0, RollRange) to the bag's [0, 1) draw roll; out-of-range rolls clamp.</summary>
        public static float RollToUnit(long roll)
        {
            if (roll <= 0)
                return 0f;
            if (roll >= RollRange)
                roll = RollRange - 1;
            return (float)(roll / (double)RollRange);
        }

        /// <summary>Draws one dig's find from the bag with a dig roll in [0, RollRange).</summary>
        public static DigFind Draw(ShuffleBag<int> bag, long roll)
            => DigFind.FromOutcomeIndex(bag.NextFromRoll(RollToUnit(roll)));
    }
}
