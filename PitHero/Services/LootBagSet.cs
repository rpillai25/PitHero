using PitHero.Config;
using PitHero.Farming;
using RolePlayingFramework.Utils;

namespace PitHero.Services
{
    /// <summary>
    /// Shuffle bags for treasure-chest loot (issue #382). Rates mirror the pure-random
    /// constants they replace (CaveBiomeConfig rarity bands, BalanceConfig drop rates)
    /// but are enforced exactly over each bag cycle, so rarity streaks and droughts are
    /// bounded and accessories/seeds appear on a predictable cadence.
    /// All draws go through ShuffleBag.NextFromRoll with a caller-supplied [0,1) roll,
    /// so one instance serves both the live layer (Nez.Random rolls) and the virtual
    /// layer (per-run System.Random rolls) without owning any RNG itself.
    /// One instance should live per session (LootShuffleService) or per virtual sim run
    /// (VirtualPitGenerator) so pity carries across pit floors.
    /// </summary>
    public sealed class LootBagSet
    {
        // Cave rarity bands, denominator 20 — compositions mirror CaveBiomeConfig.DetermineCaveTreasureLevel.
        private readonly ShuffleBag<int> _cave11To14;     // 35% L2 / 65% L1
        private readonly ShuffleBag<int> _caveBoss15;     // 60% L2 / 40% L1
        private readonly ShuffleBag<int> _caveBoss20And25; // 20% L3 / 50% L2 / 30% L1
        private readonly ShuffleBag<int> _cave16To25;     // 10% L3 / 35% L2 / 55% L1

        // Chest content gates.
        private readonly ShuffleBag<bool> _seedGate;        // SeedChestDropRate 10% => 1/10
        private readonly ShuffleBag<int> _seedType;         // one marble per crop — full rotation
        private readonly ShuffleBag<bool> _consumableGate;  // CaveConsumableDropRate 60% => 3/5
        private readonly ShuffleBag<int> _potionType;       // HP/MP/Mix strict rotation

        // Accessory share per cave equipment pool (AccessoryLootShare 10% => 1/10).
        private readonly ShuffleBag<bool> _accessoryCommon;
        private readonly ShuffleBag<bool> _accessoryUncommon;
        private readonly ShuffleBag<bool> _accessoryRare;
        private readonly ShuffleBag<int> _uncommonAccessoryPick; // MagicChain / RingOfPower rotation

        // Epic pool rotation (PitLord set) — boss epic chest drops.
        private readonly ShuffleBag<int> _epicIndex;

        public LootBagSet()
        {
            _cave11To14 = BuildRarityBag(l3: 0, l2: 7);
            _caveBoss15 = BuildRarityBag(l3: 0, l2: 12);
            _caveBoss20And25 = BuildRarityBag(l3: 4, l2: 10);
            _cave16To25 = BuildRarityBag(l3: 2, l2: 7);

            _seedGate = BuildGateBag(trueMarbles: 1, totalMarbles: 10);
            _consumableGate = BuildGateBag(trueMarbles: 3, totalMarbles: 5);

            _seedType = new ShuffleBag<int>(CropTypeInfo.Count);
            for (var i = 0; i < CropTypeInfo.Count; i++)
                _seedType.Add(i);

            _potionType = new ShuffleBag<int>(3);
            for (var i = 0; i < 3; i++)
                _potionType.Add(i);

            _accessoryCommon = BuildGateBag(1, 10);
            _accessoryUncommon = BuildGateBag(1, 10);
            _accessoryRare = BuildGateBag(1, 10);

            _uncommonAccessoryPick = new ShuffleBag<int>(2);
            _uncommonAccessoryPick.Add(0);
            _uncommonAccessoryPick.Add(1);

            _epicIndex = new ShuffleBag<int>(4);
            for (var i = 0; i < 4; i++)
                _epicIndex.Add(i);
        }

        private static ShuffleBag<int> BuildRarityBag(int l3, int l2)
        {
            var bag = new ShuffleBag<int>(20);
            if (l3 > 0) bag.Add(3, l3);
            bag.Add(2, l2);
            bag.Add(1, 20 - l3 - l2);
            return bag;
        }

        private static ShuffleBag<bool> BuildGateBag(int trueMarbles, int totalMarbles)
        {
            var bag = new ShuffleBag<bool>(totalMarbles);
            bag.Add(true, trueMarbles);
            bag.Add(false, totalMarbles - trueMarbles);
            return bag;
        }

        /// <summary>
        /// Bag-driven counterpart of <see cref="CaveBiomeConfig.DetermineCaveTreasureLevel"/>:
        /// same per-band rates, enforced exactly per 20 chests of that band.
        /// </summary>
        public int DrawCaveTreasureLevel(int pitLevel, float roll01)
        {
            if (pitLevel <= 10)
                return 1;
            if (pitLevel == 15)
                return _caveBoss15.NextFromRoll(roll01);
            if (pitLevel < 16)
                return _cave11To14.NextFromRoll(roll01);
            if (CaveBiomeConfig.IsBossFloor(pitLevel))
                return _caveBoss20And25.NextFromRoll(roll01);
            return _cave16To25.NextFromRoll(roll01);
        }

        /// <summary>Whether an eligible level-2 chest becomes a seed chest (exactly 1 per 10).</summary>
        public bool DrawSeedGate(float roll01) => _seedGate.NextFromRoll(roll01);

        /// <summary>Which crop's seeds drop — every crop appears once per rotation.</summary>
        public CropType DrawSeedType(float roll01) => (CropType)_seedType.NextFromRoll(roll01);

        /// <summary>Whether a level-1 cave chest holds a consumable (exactly 3 per 5).</summary>
        public bool DrawConsumableGate(float roll01) => _consumableGate.NextFromRoll(roll01);

        /// <summary>Potion selector: 0 = HP, 1 = MP, 2 = Mix — strict rotation.</summary>
        public int DrawPotionType(float roll01) => _potionType.NextFromRoll(roll01);

        /// <summary>Whether a cave equipment roll of the given treasure level yields an accessory (exactly 1 per 10).</summary>
        public bool DrawAccessoryShare(int treasureLevel, float roll01)
        {
            switch (treasureLevel)
            {
                case 1: return _accessoryCommon.NextFromRoll(roll01);
                case 2: return _accessoryUncommon.NextFromRoll(roll01);
                case 3: return _accessoryRare.NextFromRoll(roll01);
                default: return false; // epic pool has no accessories
            }
        }

        /// <summary>Uncommon accessory selector: 0 = MagicChain, 1 = RingOfPower — strict rotation.</summary>
        public int DrawUncommonAccessoryIndex(float roll01) => _uncommonAccessoryPick.NextFromRoll(roll01);

        /// <summary>Epic pool selector (0–3, PitLord set) — all four cycle before any repeats.</summary>
        public int DrawEpicIndex(float roll01) => _epicIndex.NextFromRoll(roll01);
    }
}
