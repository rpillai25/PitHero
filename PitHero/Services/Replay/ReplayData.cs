using System;
using System.Collections.Generic;
using System.IO;
using Nez.Persistence.Binary;

namespace PitHero.Services.Replay
{
    /// <summary>How the recorded session started.</summary>
    public enum ReplayKind
    {
        /// <summary>A brand-new game: the header state blob carries the hero design and global services; the scene runs the new-game path.</summary>
        NewGame = 0,
        /// <summary>A loaded save: the header state blob is the exact SaveData the session loaded.</summary>
        Load = 1,
    }

    /// <summary>
    /// A hero decision (GOAP plan) or state sample recorded at a tick, used to detect divergence during
    /// playback. State samples also carry the four part hashes the combined hash was built from so a
    /// mismatch can say WHAT drifted (RNG stream, hero, party/monsters, world/economy).
    /// </summary>
    public struct ReplayHashSample
    {
        public long Tick;
        public ulong Hash;
        public ulong Rng;
        public ulong Hero;
        public ulong Party;
        public ulong World;

        public ReplayHashSample(long tick, ulong hash)
        {
            Tick = tick;
            Hash = hash;
            Rng = 0; Hero = 0; Party = 0; World = 0;
        }

        public ReplayHashSample(long tick, ulong hash, ulong rng, ulong hero, ulong party, ulong world)
        {
            Tick = tick;
            Hash = hash;
            Rng = rng; Hero = hero; Party = party; World = world;
        }
    }

    /// <summary>A player command with the tick it was applied on.</summary>
    public struct ReplayCommandRecord
    {
        public long Tick;
        public PlayerCommand Command;

        public ReplayCommandRecord(long tick, in PlayerCommand command)
        {
            Tick = tick;
            Command = command;
        }
    }

    /// <summary>
    /// A complete recorded session: how it started (seed + state blob), every player command with its
    /// tick, and the divergence tripwires (hero decision hashes and periodic state hashes). Persisted
    /// with its own format version, independent of the save-file version.
    /// </summary>
    public class ReplayData : IPersistable
    {
        /// <summary>Current replay file format version (2: state samples carry part hashes).</summary>
        public const int CurrentVersion = 2;
        /// <summary>Oldest replay file format this build can read.</summary>
        public const int MinSupportedVersion = 2;

        public int FormatVersion = CurrentVersion;
        public ReplayKind Kind;
        public int MasterSeed;
        public string HeroName = string.Empty;
        public string JobName = string.Empty;
        public int PitLevelAtStart;
        public long RecordedAtUtcTicks;
        public long TotalTicks;
        /// <summary>Identifies the game build the recording was made with; a mismatch is a warning, not a block.</summary>
        public string BuildId = string.Empty;
        /// <summary>SaveData bytes the session started from (see <see cref="ReplayKind"/>).</summary>
        public byte[] StateBlob;

        public List<ReplayCommandRecord> Commands = new List<ReplayCommandRecord>();
        public List<ReplayHashSample> Decisions = new List<ReplayHashSample>();
        public List<ReplayHashSample> StateHashes = new List<ReplayHashSample>();

        /// <summary>When true, <see cref="Recover"/> stops after the header (list previews).</summary>
        public bool HeaderOnly;

        /// <summary>Duration in seconds implied by TotalTicks and the fixed step.</summary>
        public float DurationSeconds => TotalTicks * GameConfig.SimulationFixedStepSeconds;

        void IPersistable.Persist(IPersistableWriter writer)
        {
            writer.Write(CurrentVersion);
            writer.Write((int)Kind);
            writer.Write(MasterSeed);
            writer.Write(HeroName ?? string.Empty);
            writer.Write(JobName ?? string.Empty);
            writer.Write(PitLevelAtStart);
            ReplayIO.WriteLong(writer, RecordedAtUtcTicks);
            ReplayIO.WriteLong(writer, TotalTicks);
            writer.Write(BuildId ?? string.Empty);
            ReplayIO.WriteBytes(writer, StateBlob);

            writer.Write(Commands.Count);
            for (int i = 0; i < Commands.Count; i++)
            {
                var r = Commands[i];
                ReplayIO.WriteLong(writer, r.Tick);
                writer.Write((int)r.Command.Type);
                writer.Write(r.Command.A);
                writer.Write(r.Command.B);
                writer.Write(r.Command.C);
                writer.Write(r.Command.D);
                ReplayIO.WriteLong(writer, r.Command.L);
                writer.Write(r.Command.F);
                writer.Write(r.Command.S != null);
                writer.Write(r.Command.S ?? string.Empty);
            }

            WriteSamples(writer, Decisions);
            WriteSamples(writer, StateHashes);
        }

        private static void WriteSamples(IPersistableWriter writer, List<ReplayHashSample> samples)
        {
            writer.Write(samples.Count);
            for (int i = 0; i < samples.Count; i++)
            {
                var s = samples[i];
                ReplayIO.WriteLong(writer, s.Tick);
                ReplayIO.WriteULong(writer, s.Hash);
                ReplayIO.WriteULong(writer, s.Rng);
                ReplayIO.WriteULong(writer, s.Hero);
                ReplayIO.WriteULong(writer, s.Party);
                ReplayIO.WriteULong(writer, s.World);
            }
        }

        void IPersistable.Recover(IPersistableReader reader)
        {
            FormatVersion = reader.ReadInt();
            if (FormatVersion < MinSupportedVersion || FormatVersion > CurrentVersion)
                throw new InvalidDataException("Unsupported replay format version " + FormatVersion);

            Kind = (ReplayKind)reader.ReadInt();
            MasterSeed = reader.ReadInt();
            HeroName = reader.ReadString();
            JobName = reader.ReadString();
            PitLevelAtStart = reader.ReadInt();
            RecordedAtUtcTicks = ReplayIO.ReadLong(reader);
            TotalTicks = ReplayIO.ReadLong(reader);
            BuildId = reader.ReadString();
            if (HeaderOnly)
            {
                // Skip the blob without decoding it
                reader.ReadString();
                return;
            }
            StateBlob = ReplayIO.ReadBytes(reader);

            int count = reader.ReadInt();
            Commands = new List<ReplayCommandRecord>(Math.Max(count, 16));
            for (int i = 0; i < count; i++)
            {
                long tick = ReplayIO.ReadLong(reader);
                var cmd = new PlayerCommand((PlayerCommandType)reader.ReadInt());
                cmd.A = reader.ReadInt();
                cmd.B = reader.ReadInt();
                cmd.C = reader.ReadInt();
                cmd.D = reader.ReadInt();
                cmd.L = ReplayIO.ReadLong(reader);
                cmd.F = reader.ReadFloat();
                bool hasS = reader.ReadBool();
                string s = reader.ReadString();
                cmd.S = hasS ? s : null;
                Commands.Add(new ReplayCommandRecord(tick, in cmd));
            }

            Decisions = ReadSamples(reader);
            StateHashes = ReadSamples(reader);
        }

        private static List<ReplayHashSample> ReadSamples(IPersistableReader reader)
        {
            int count = reader.ReadInt();
            var list = new List<ReplayHashSample>(Math.Max(count, 16));
            for (int i = 0; i < count; i++)
            {
                long tick = ReplayIO.ReadLong(reader);
                ulong hash = ReplayIO.ReadULong(reader);
                ulong rng = ReplayIO.ReadULong(reader);
                ulong hero = ReplayIO.ReadULong(reader);
                ulong party = ReplayIO.ReadULong(reader);
                ulong world = ReplayIO.ReadULong(reader);
                list.Add(new ReplayHashSample(tick, hash, rng, hero, party, world));
            }
            return list;
        }
    }
}
