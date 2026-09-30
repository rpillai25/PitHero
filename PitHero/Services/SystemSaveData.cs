using Nez.Persistence.Binary;
using PitHero.Artifacts;
using System.Collections.Generic;

namespace PitHero.Services
{
    /// <summary>
    /// The system save: state that belongs to the player rather than to any hero or save slot, so it is
    /// never reset by New Game or overwritten by loading a slot. The owned artifacts and (v2) the
    /// player-level preferences. Its own format version, independent of SaveData; new fields are
    /// appended and read conditionally.
    /// </summary>
    public sealed class SystemSaveData : IPersistable
    {
        /// <summary>Current system save format version (2: AutoSaveReplays).</summary>
        public const int CurrentVersion = 2;

        /// <summary>Version read from the file (CurrentVersion for a fresh instance).</summary>
        public int FormatVersion = CurrentVersion;

        /// <summary>Owned artifacts by enum ordinal, in the order they were granted.</summary>
        public List<int> OwnedArtifacts = new List<int>(ArtifactCatalog.Count);

        /// <summary>
        /// The Replay tab's "Autosave Replays" preference (issue #444): the quit-time auto replay and the
        /// autosave's recovery recording are written only while it is on. On by default; a v1 file reads as on.
        /// </summary>
        public bool AutoSaveReplays = true;

        void IPersistable.Persist(IPersistableWriter writer)
        {
            writer.Write(CurrentVersion);
            writer.Write(OwnedArtifacts.Count);
            for (int i = 0; i < OwnedArtifacts.Count; i++)
                writer.Write(OwnedArtifacts[i]);
            writer.Write(AutoSaveReplays);
        }

        void IPersistable.Recover(IPersistableReader reader)
        {
            FormatVersion = reader.ReadInt();
            if (FormatVersion < 1 || FormatVersion > CurrentVersion)
                throw new System.IO.InvalidDataException("Unsupported system save version " + FormatVersion);

            int count = reader.ReadInt();
            OwnedArtifacts = new List<int>(count > ArtifactCatalog.Count ? count : ArtifactCatalog.Count);
            for (int i = 0; i < count; i++)
            {
                int ordinal = reader.ReadInt();
                // A newer build may have written artifacts this one does not know: keep them so a
                // save/load cycle on the old build never loses a purchase
                if (!OwnedArtifacts.Contains(ordinal))
                    OwnedArtifacts.Add(ordinal);
            }
            AutoSaveReplays = FormatVersion >= 2 ? reader.ReadBool() : true;
        }
    }
}
