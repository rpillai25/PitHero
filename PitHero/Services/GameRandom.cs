using System;
using PitHero.Services.Replay;

namespace PitHero.Services
{
    /// <summary>
    /// Named random streams. <see cref="Sim"/> is installed as <c>Nez.Random.RNG</c> for one session so
    /// every gameplay roll (battle, pit generation, loot, mercenaries, kitchen, farm, names) derives from
    /// <see cref="MasterSeed"/> and replays exactly; <see cref="Loot"/> feeds the mid-battle epic-chest
    /// draw that must never touch the battle stream. <see cref="Audio"/> and <see cref="Ui"/> serve
    /// consumers that fire from UI input or sound playback at wall-clock times and therefore must be kept
    /// away from the simulation stream entirely.
    /// </summary>
    public static class GameRandom
    {
        private const int LootStreamSalt = 0x4C4F4F54;    // "LOOT"
        private const int ProcessStreamSalt = 0x50524F43; // "PROC"
        private const int UiStreamSalt = 0x5A5A5A5A;

        /// <summary>Simulation stream (== Nez.Random.RNG after <see cref="InitializeSession"/>).</summary>
        public static SeedableRandom Sim { get; private set; }

        /// <summary>Loot stream for the epic-chest draw (safe to consume mid-battle).</summary>
        public static SeedableRandom Loot { get; private set; }

        /// <summary>Audio variant stream. Never influences the simulation.</summary>
        public static SeedableRandom Audio { get; private set; }

        /// <summary>UI-time stream (hero creation randomize, crystal dialog rolls). Never influences the simulation.</summary>
        public static SeedableRandom Ui { get; private set; }

        /// <summary>Seed the current session's Sim and Loot streams were derived from.</summary>
        public static int MasterSeed { get; private set; }

        /// <summary>True once <see cref="InitializeSession"/> has run at least once this process.</summary>
        public static bool IsSessionInitialized => Sim != null;

        /// <summary>
        /// Derives the Sim and Loot streams from <paramref name="masterSeed"/> and installs Sim as
        /// <c>Nez.Random.RNG</c>. Call at the very top of MainGameScene.Begin, before any world content
        /// is generated. The Audio/Ui streams are created on demand and are not tied to the seed.
        /// </summary>
        public static void InitializeSession(int masterSeed)
        {
            MasterSeed = masterSeed;
            if (Sim == null)
                Sim = new SeedableRandom(masterSeed);
            else
                Sim.Reseed(masterSeed);

            int lootSeed = masterSeed ^ LootStreamSalt;
            if (Loot == null)
                Loot = new SeedableRandom(lootSeed);
            else
                Loot.Reseed(lootSeed);

            Nez.Random.RNG = Sim;
            EnsureNonSimStreams();
        }

        /// <summary>Creates the Audio and Ui streams if they do not exist yet (seeded from the wall clock).</summary>
        public static void EnsureNonSimStreams()
        {
            if (Audio != null && Ui != null)
                return;

            int processSeed = GenerateMasterSeed() ^ ProcessStreamSalt;
            if (Audio == null)
                Audio = new SeedableRandom(processSeed);
            if (Ui == null)
                Ui = new SeedableRandom(processSeed ^ UiStreamSalt);
        }

        /// <summary>Generates a fresh master seed from wall-clock entropy (never called inside the simulation).</summary>
        public static int GenerateMasterSeed()
        {
            return Environment.TickCount ^ Guid.NewGuid().GetHashCode();
        }

        /// <summary>Audio-stream int in [min, max).</summary>
        public static int AudioRange(int min, int max)
        {
            EnsureNonSimStreams();
            return Audio.Next(min, max);
        }

        /// <summary>UI-stream int in [min, max).</summary>
        public static int UiRange(int min, int max)
        {
            EnsureNonSimStreams();
            return Ui.Next(min, max);
        }
    }
}
