using Nez;
using Nez.Persistence.Binary;

namespace PitHero.Services
{
    /// <summary>
    /// Lifetime totals shown by the Stats window (issue #456): game, farm and fishing counters for the
    /// current hero. Only ever increases. Written by the simulation at the same choke points that log
    /// analytics, saved with the hero (save v44), restored by ApplyLoadedState (replay start included)
    /// and reset for a new hero. Nothing in the simulation reads it back, so it never affects replays.
    /// </summary>
    public sealed class LifetimeStats
    {
        /// <summary>Gold credited to Funds, refunds and building sales excluded.</summary>
        public long GoldEarned;

        /// <summary>Simulation ticks played in earlier sessions; the current session adds SimulationClock.Tick.</summary>
        public long PlayedTicksBase;

        public int MonstersDefeated;
        public int TreasuresFound;
        public int MercenariesHired;
        public int HeroDefeats;
        public int MercenaryDeaths;
        public int PitLevelsCleared;

        /// <summary>Crop units sold, indexed by (int)CropType.</summary>
        public readonly int[] CropUnitsSold = new int[Farming.CropTypeInfo.Count];
        /// <summary>Gold from crop sales, indexed by (int)CropType.</summary>
        public readonly int[] CropSaleGold = new int[Farming.CropTypeInfo.Count];
        /// <summary>Gold spent on seeds, indexed by (int)CropType.</summary>
        public readonly int[] CropSeedGold = new int[Farming.CropTypeInfo.Count];
        /// <summary>Gold patrons paid for dishes (price + tips), indexed by (int)DishType.</summary>
        public readonly int[] DishGold = new int[Dining.DishTypeInfo.Count];
        /// <summary>Catches landed, indexed by (int)FishCatchType.</summary>
        public readonly int[] FishCaught = new int[Config.FishingConfig.CatchCount];
        /// <summary>Gold from catches, indexed by (int)FishCatchType.</summary>
        public readonly int[] FishGold = new int[Config.FishingConfig.CatchCount];
        /// <summary>Gems dug up, indexed by (int)GemType (save v45).</summary>
        public readonly int[] GemsFound = new int[Config.GemDigConfig.GemCount];
        /// <summary>Gold from dug gems, indexed by (int)GemType (save v45).</summary>
        public readonly int[] GemGold = new int[Config.GemDigConfig.GemCount];

        /// <summary>The live session's stats, or null headless (no Core) or before GameStateService exists.</summary>
        public static LifetimeStats Live
            => Core.Instance != null ? Core.Services?.GetService<GameStateService>()?.Stats : null;

        /// <summary>Lifetime ticks played: earlier sessions plus the running session's clock.</summary>
        public long TotalPlayedTicks => PlayedTicksBase + SimulationClock.CurrentTick;

        /// <summary>Lifetime gold per hour of simulated play (0 before any time has passed).</summary>
        public float GoldPerHour
        {
            get
            {
                double hours = TotalPlayedTicks * (double)GameConfig.SimulationFixedStepSeconds / 3600.0;
                return hours > 0 ? (float)(GoldEarned / hours) : 0f;
            }
        }

        /// <summary>Counts gold credited to Funds toward the earn rate, skipping money that only comes back.</summary>
        public void RecordGold(int amount, string source)
        {
            if (amount <= 0 || source == "refund" || source == "sell_building")
                return;
            GoldEarned += amount;
        }

        public void RecordCropSale(Farming.CropType crop, int units, int gold)
        {
            int i = (int)crop;
            if (i < 0 || i >= CropUnitsSold.Length) return;
            if (units > 0) CropUnitsSold[i] += units;
            if (gold > 0) CropSaleGold[i] += gold;
        }

        public void RecordSeedPurchase(Farming.CropType crop, int gold)
        {
            int i = (int)crop;
            if (gold <= 0 || i < 0 || i >= CropSeedGold.Length) return;
            CropSeedGold[i] += gold;
        }

        public void RecordDishGold(Dining.DishType dish, int gold)
        {
            int i = (int)dish;
            if (gold <= 0 || i < 0 || i >= DishGold.Length) return;
            DishGold[i] += gold;
        }

        public void RecordFishCatch(Config.FishCatchType type, int gold)
        {
            int i = (int)type;
            if (i < 0 || i >= FishCaught.Length) return;
            FishCaught[i]++;
            if (gold > 0) FishGold[i] += gold;
        }

        public void RecordGemFind(Config.GemType gem, int gold)
        {
            int i = (int)gem;
            if (i < 0 || i >= GemsFound.Length) return;
            GemsFound[i]++;
            if (gold > 0) GemGold[i] += gold;
        }

        /// <summary>Gold from every crop sale (before seed costs).</summary>
        public long TotalCropSaleGold => Sum(CropSaleGold);
        /// <summary>Gold patrons paid for every dish, tips included.</summary>
        public long TotalDishGold => Sum(DishGold);
        /// <summary>Gold from every catch.</summary>
        public long TotalFishGold => Sum(FishGold);
        /// <summary>Gold from every gem dug up.</summary>
        public long TotalGemGold => Sum(GemGold);

        private static long Sum(int[] values)
        {
            long total = 0;
            for (int i = 0; i < values.Length; i++)
                total += values[i];
            return total;
        }

        /// <summary>Crop profit: sale gold minus seed gold (the Seed Cost column); negative while seeds outpace sales.</summary>
        public int GetCropProfit(Farming.CropType crop)
        {
            int i = (int)crop;
            return i >= 0 && i < CropSaleGold.Length ? CropSaleGold[i] - CropSeedGold[i] : 0;
        }

        /// <summary>Zeroes everything (a new hero).</summary>
        public void Clear() => CopyFrom(null);

        /// <summary>Replaces every counter from another instance (null clears).</summary>
        public void CopyFrom(LifetimeStats other)
        {
            GoldEarned = other?.GoldEarned ?? 0;
            PlayedTicksBase = other?.PlayedTicksBase ?? 0;
            MonstersDefeated = other?.MonstersDefeated ?? 0;
            TreasuresFound = other?.TreasuresFound ?? 0;
            MercenariesHired = other?.MercenariesHired ?? 0;
            HeroDefeats = other?.HeroDefeats ?? 0;
            MercenaryDeaths = other?.MercenaryDeaths ?? 0;
            PitLevelsCleared = other?.PitLevelsCleared ?? 0;
            Copy(other?.CropUnitsSold, CropUnitsSold);
            Copy(other?.CropSaleGold, CropSaleGold);
            Copy(other?.CropSeedGold, CropSeedGold);
            Copy(other?.DishGold, DishGold);
            Copy(other?.FishCaught, FishCaught);
            Copy(other?.FishGold, FishGold);
            Copy(other?.GemsFound, GemsFound);
            Copy(other?.GemGold, GemGold);
        }

        /// <summary>
        /// Snapshot for the save gather: the running session's ticks are folded into the base, so the
        /// loaded copy (whose session clock restarts at zero) continues the same total.
        /// </summary>
        public LifetimeStats CloneForSave()
        {
            var copy = new LifetimeStats();
            copy.CopyFrom(this);
            copy.PlayedTicksBase = TotalPlayedTicks;
            return copy;
        }

        private static void Copy(int[] source, int[] target)
        {
            for (int i = 0; i < target.Length; i++)
            {
                int v = source != null && i < source.Length ? source[i] : 0;
                target[i] = v < 0 ? 0 : v;
            }
        }

        /// <summary>Writes the stats section (save v44).</summary>
        public void Write(IPersistableWriter writer)
        {
            WriteLong(writer, GoldEarned);
            WriteLong(writer, PlayedTicksBase);
            writer.Write(MonstersDefeated);
            writer.Write(TreasuresFound);
            writer.Write(MercenariesHired);
            writer.Write(HeroDefeats);
            writer.Write(MercenaryDeaths);
            writer.Write(PitLevelsCleared);
            WriteArray(writer, CropUnitsSold);
            WriteArray(writer, CropSaleGold);
            WriteArray(writer, CropSeedGold);
            WriteArray(writer, DishGold);
            WriteArray(writer, FishCaught);
            WriteArray(writer, FishGold);
        }

        /// <summary>Reads the stats section written by <see cref="Write"/>.</summary>
        public void Read(IPersistableReader reader)
        {
            GoldEarned = ReadLong(reader);
            PlayedTicksBase = ReadLong(reader);
            MonstersDefeated = reader.ReadInt();
            TreasuresFound = reader.ReadInt();
            MercenariesHired = reader.ReadInt();
            HeroDefeats = reader.ReadInt();
            MercenaryDeaths = reader.ReadInt();
            PitLevelsCleared = reader.ReadInt();
            ReadArray(reader, CropUnitsSold);
            ReadArray(reader, CropSaleGold);
            ReadArray(reader, CropSeedGold);
            ReadArray(reader, DishGold);
            ReadArray(reader, FishCaught);
            ReadArray(reader, FishGold);
        }

        /// <summary>Writes the digging section (save v45): gems found, then gem gold.</summary>
        public void WriteDigging(IPersistableWriter writer)
        {
            WriteArray(writer, GemsFound);
            WriteArray(writer, GemGold);
        }

        /// <summary>Reads the digging section written by <see cref="WriteDigging"/>.</summary>
        public void ReadDigging(IPersistableReader reader)
        {
            ReadArray(reader, GemsFound);
            ReadArray(reader, GemGold);
        }

        private static void WriteLong(IPersistableWriter writer, long value)
        {
            writer.Write((int)(value & 0xFFFFFFFFL));
            writer.Write((int)(value >> 32));
        }

        private static long ReadLong(IPersistableReader reader)
        {
            long low = (uint)reader.ReadInt();
            long high = reader.ReadInt();
            return (high << 32) | low;
        }

        private static void WriteArray(IPersistableWriter writer, int[] values)
        {
            writer.Write(values.Length);
            for (int i = 0; i < values.Length; i++)
                writer.Write(values[i]);
        }

        /// <summary>Count-prefixed read: missing entries stay zero, extra entries (a newer build) are dropped.</summary>
        private static void ReadArray(IPersistableReader reader, int[] target)
        {
            for (int i = 0; i < target.Length; i++)
                target[i] = 0;
            int count = reader.ReadInt();
            for (int i = 0; i < count; i++)
            {
                int v = reader.ReadInt();
                if (i < target.Length)
                    target[i] = v < 0 ? 0 : v;
            }
        }
    }
}
