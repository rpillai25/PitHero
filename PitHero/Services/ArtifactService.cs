using Nez;
using Nez.Persistence.Binary;
using PitHero.Artifacts;
using System;
using System.Collections.Generic;
using System.IO;

namespace PitHero.Services
{
    /// <summary>
    /// The single query surface for artifact ownership (see <see cref="ArtifactType"/>). Global
    /// artifacts belong to the player: they are persisted in the system save file the moment one is
    /// granted, and are never reset by New Game or by loading a slot. Local artifacts (issue #411)
    /// belong to the current hero: their ownership lives on the attached <see cref="GameStateService"/>
    /// (session save) and IS read by the simulation (crop growth, worker speed), so it travels with the
    /// save and with every replay's start state. Global service registered in Game1; grants arrive only
    /// through the GrantArtifact command handler and are idempotent, so a replayed purchase never
    /// differs from the live one.
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
        private GameStateService _local;
        private int _globalVersion;

        /// <summary>
        /// Changes on every grant, and on every local-artifact load/clear, so UI that caches ownership
        /// can refresh cheaply. Composite of the global counter and the local store's own version.
        /// </summary>
        public int Version => _globalVersion + (_local != null ? _local.LocalArtifactVersion : 0);

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

        /// <summary>Points Local-scope ownership at the session state (Game1 registration, tests).</summary>
        public void AttachLocalStore(GameStateService store)
        {
            _local = store;
        }

        /// <summary>Forgets the local store; Local artifacts then read as not owned.</summary>
        public void DetachLocalStore()
        {
            _local = null;
        }

        /// <summary>True when the player (Global) or the current hero (Local) owns the artifact.</summary>
        public bool Owns(ArtifactType type)
        {
            if (ArtifactCatalog.IsLocal(type))
                return _local != null && _local.OwnsLocalArtifact(type);
            int i = (int)type;
            return i >= 0 && i < _owned.Length && _owned[i];
        }

        /// <summary>Number of artifacts owned, Global and Local together.</summary>
        public int OwnedCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _owned.Length; i++)
                    if (Owns((ArtifactType)i)) n++;
                return n;
            }
        }

        /// <summary>
        /// True when the artifact is owned but a straight upgrade of it is owned too, so the owned
        /// grid shows only the upgrade (Fast Grow Fertilizer once the Lightning one is bought).
        /// </summary>
        public bool IsSuperseded(ArtifactType type)
        {
            var upgrade = ArtifactCatalog.GetSupersededBy(type);
            return upgrade.HasValue && Owns(upgrade.Value);
        }

        /// <summary>
        /// Appends the artifacts to show as owned to <paramref name="result"/>: the player's Global
        /// artifacts in grant order, then this hero's Local artifacts in grant order, minus any piece
        /// whose upgrade is also owned (<see cref="IsSuperseded"/>).
        /// </summary>
        public void GetOwnedInOrder(List<ArtifactType> result)
        {
            int start = result.Count;
            for (int i = 0; i < _data.OwnedArtifacts.Count; i++)
            {
                int ordinal = _data.OwnedArtifacts[i];
                if (ArtifactCatalog.IsValid(ordinal))
                    result.Add((ArtifactType)ordinal);
            }
            _local?.GetLocalArtifactsInOrder(result);

            for (int i = result.Count - 1; i >= start; i--)
            {
                if (IsSuperseded(result[i]))
                    result.RemoveAt(i);
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
        /// Grants the artifact. Global: records it and writes the system save. Local: records it on the
        /// session state (saved with the game, never the system file). Idempotent: granting an owned
        /// artifact changes nothing, which is what lets a replayed purchase apply safely.
        /// </summary>
        public bool Grant(ArtifactType type)
        {
            if (ArtifactCatalog.IsLocal(type))
                return _local != null && _local.GrantLocalArtifact(type);

            int i = (int)type;
            if (i < 0 || i >= _owned.Length || _owned[i])
                return false;
            _owned[i] = true;
            if (!_data.OwnedArtifacts.Contains(i))
                _data.OwnedArtifacts.Add(i);
            _globalVersion++;
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
                // Only Global ordinals belong in the system file; a Local one written by mistake stays
                // in the list (never dropped) but is not treated as owned
                if (ArtifactCatalog.IsValid(ordinal) && !ArtifactCatalog.IsLocal((ArtifactType)ordinal))
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
