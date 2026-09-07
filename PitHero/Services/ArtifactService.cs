using Nez;
using Nez.Persistence.Binary;
using PitHero.Artifacts;
using System;
using System.Collections.Generic;
using System.IO;

namespace PitHero.Services
{
    /// <summary>
    /// Owns the player's artifacts (see <see cref="ArtifactType"/>) and persists them in the system save
    /// file the moment one is granted. Global service: registered in Game1 and never reset by New Game
    /// or by loading a slot. Ownership is read by presentation code only (replay gates, shop and party
    /// tabs); the simulation touches it solely through the BuyArtifact command handler, whose grant is
    /// idempotent so replaying a purchase never differs from the live one.
    /// </summary>
    public sealed class ArtifactService
    {
        /// <summary>The global instance, or null before Game1 registers it (headless tests).</summary>
        public static ArtifactService Current { get; private set; }

        private readonly string _directory;
        private readonly string _fileName;
        private readonly FileDataStore _store;
        private readonly SystemSaveData _data = new SystemSaveData();
        private readonly bool[] _owned = new bool[ArtifactCatalog.Count];

        /// <summary>Incremented on every grant so UI that caches ownership can refresh cheaply.</summary>
        public int Version { get; private set; }

        /// <summary>Creates the service on the default persistent data folder.</summary>
        public ArtifactService() : this(PersistentPaths.BaseDirectory(), GameConfig.SystemSaveFileName)
        {
        }

        /// <summary>Creates the service rooted at an explicit directory and file (tests).</summary>
        public ArtifactService(string directory, string fileName)
        {
            _directory = directory;
            _fileName = fileName;
            Directory.CreateDirectory(_directory);
            _store = new FileDataStore(_directory);
            Load();
            Current = this;
        }

        /// <summary>Clears the static instance (tests).</summary>
        public void Detach()
        {
            if (Current == this)
                Current = null;
        }

        /// <summary>True when the player owns the artifact.</summary>
        public bool Owns(ArtifactType type)
        {
            int i = (int)type;
            return i >= 0 && i < _owned.Length && _owned[i];
        }

        /// <summary>Number of artifacts owned.</summary>
        public int OwnedCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _owned.Length; i++)
                    if (_owned[i]) n++;
                return n;
            }
        }

        /// <summary>Appends the owned artifacts to <paramref name="result"/> in the order they were granted.</summary>
        public void GetOwnedInOrder(List<ArtifactType> result)
        {
            for (int i = 0; i < _data.OwnedArtifacts.Count; i++)
            {
                int ordinal = _data.OwnedArtifacts[i];
                if (ArtifactCatalog.IsValid(ordinal))
                    result.Add((ArtifactType)ordinal);
            }
        }

        /// <summary>
        /// True when the shop should offer the artifact: not owned yet and every prerequisite owned.
        /// </summary>
        public bool IsAvailableInShop(ArtifactType type)
        {
            if (Owns(type))
                return false;
            var prerequisites = ArtifactCatalog.GetPrerequisites(type);
            for (int i = 0; i < prerequisites.Length; i++)
            {
                if (!Owns(prerequisites[i]))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Grants the artifact and writes the system save. Idempotent: granting an owned artifact
        /// changes nothing, which is what lets a replayed purchase apply safely.
        /// </summary>
        public bool Grant(ArtifactType type)
        {
            int i = (int)type;
            if (i < 0 || i >= _owned.Length || _owned[i])
                return false;
            _owned[i] = true;
            if (!_data.OwnedArtifacts.Contains(i))
                _data.OwnedArtifacts.Add(i);
            Version++;
            Save();
            return true;
        }

        private void Load()
        {
            string path = Path.Combine(_directory, _fileName);
            if (!File.Exists(path))
                return;
            try
            {
                _store.Load(_fileName, _data);
            }
            catch (Exception ex)
            {
                Debug.Warn($"[ArtifactService] Could not read the system save: {ex.Message}");
                return;
            }
            for (int i = 0; i < _data.OwnedArtifacts.Count; i++)
            {
                int ordinal = _data.OwnedArtifacts[i];
                if (ArtifactCatalog.IsValid(ordinal))
                    _owned[ordinal] = true;
            }
        }

        private void Save()
        {
            try
            {
                _store.Save(_fileName, _data);
            }
            catch (Exception ex)
            {
                Debug.Warn($"[ArtifactService] Could not write the system save: {ex.Message}");
            }
        }
    }
}
