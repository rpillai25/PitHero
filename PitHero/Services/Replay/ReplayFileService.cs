using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Nez;
using Nez.Persistence.Binary;

namespace PitHero.Services.Replay
{
    /// <summary>Header-only description of a saved replay file for list views.</summary>
    public sealed class ReplayFileInfo
    {
        public string FileName;
        public string HeroName;
        public string JobName;
        public int PitLevelAtStart;
        public DateTime RecordedAtUtc;
        public long TotalTicks;
        public ReplayKind Kind;
        public string BuildId;

        /// <summary>Duration in seconds implied by TotalTicks.</summary>
        public float DurationSeconds => TotalTicks * GameConfig.SimulationFixedStepSeconds;
    }

    /// <summary>
    /// Saves, lists, loads and deletes replay files under the persistent data folder
    /// (%LOCALAPPDATA%\&lt;exe&gt;\replays, alongside the save slots). Global service.
    /// </summary>
    public sealed class ReplayFileService
    {
        private readonly string _directory;
        private readonly FileDataStore _store;

        /// <summary>Creates the service rooted at the default replays directory.</summary>
        public ReplayFileService() : this(DefaultDirectory())
        {
        }

        /// <summary>Creates the service rooted at an explicit directory (tests).</summary>
        public ReplayFileService(string directory)
        {
            _directory = directory;
            Directory.CreateDirectory(_directory);
            _store = new FileDataStore(_directory);
        }

        /// <summary>The directory replay files live in.</summary>
        public string Directory_ => _directory;

        /// <summary>Same derivation as Nez's FileDataStore default, plus the replays sub-folder.</summary>
        public static string DefaultDirectory()
        {
            var exeName = Path.GetFileNameWithoutExtension(AppDomain.CurrentDomain.FriendlyName);
            var baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), exeName);
            return Path.Combine(baseDir, GameConfig.ReplayDirectoryName);
        }

        /// <summary>Builds the auto-generated file name for a recording: replay_&lt;hero&gt;_&lt;yyyyMMdd_HHmmss&gt;.bin.</summary>
        public static string BuildFileName(string heroName, DateTime whenLocal)
        {
            return GameConfig.ReplayFilePrefix + SanitizeName(heroName) + "_" + whenLocal.ToString("yyyyMMdd_HHmmss") + GameConfig.ReplayFileExtension;
        }

        /// <summary>Keeps letters, digits and underscores (spaces become underscores), max 24 chars, "Hero" if empty.</summary>
        public static string SanitizeName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "Hero";
            var sb = new StringBuilder(name.Length);
            for (int i = 0; i < name.Length && sb.Length < 24; i++)
            {
                char c = name[i];
                if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_')
                    sb.Append(c);
                else if (c == ' ' || c == '-')
                    sb.Append('_');
            }
            return sb.Length == 0 ? "Hero" : sb.ToString();
        }

        /// <summary>Writes the recording to a new file and returns its file name.</summary>
        public string Save(ReplayData data)
        {
            if (data == null)
                return null;
            string fileName = BuildFileName(data.HeroName, DateTime.Now);
            // Avoid clobbering a file saved within the same second
            int suffix = 1;
            while (File.Exists(Path.Combine(_directory, fileName)))
            {
                fileName = GameConfig.ReplayFilePrefix + SanitizeName(data.HeroName) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + suffix + GameConfig.ReplayFileExtension;
                suffix++;
            }
            _store.Save(fileName, data);
            Debug.Log($"[ReplayFileService] Saved replay {fileName} ({data.Commands.Count} commands, {data.TotalTicks} ticks)");
            return fileName;
        }

        /// <summary>Loads a full recording by file name, or null if missing/unreadable.</summary>
        public ReplayData Load(string fileName)
        {
            var path = Path.Combine(_directory, fileName);
            if (!File.Exists(path))
                return null;
            try
            {
                var data = new ReplayData();
                _store.Load(fileName, data);
                return data;
            }
            catch (Exception ex)
            {
                Debug.Warn($"[ReplayFileService] Could not load {fileName}: {ex.Message}");
                return null;
            }
        }

        /// <summary>Deletes a replay file. Returns true if a file was removed.</summary>
        public bool Delete(string fileName)
        {
            var path = Path.Combine(_directory, fileName);
            if (!File.Exists(path))
                return false;
            File.Delete(path);
            return true;
        }

        /// <summary>Lists saved replays (header only), newest first. Unreadable files are skipped.</summary>
        public List<ReplayFileInfo> Enumerate()
        {
            var result = new List<ReplayFileInfo>();
            if (!Directory.Exists(_directory))
                return result;
            var files = Directory.GetFiles(_directory, GameConfig.ReplayFilePrefix + "*" + GameConfig.ReplayFileExtension);
            for (int i = 0; i < files.Length; i++)
            {
                var info = ReadHeader(files[i]);
                if (info != null)
                    result.Add(info);
            }
            result.Sort(CompareNewestFirst);
            return result;
        }

        private static int CompareNewestFirst(ReplayFileInfo a, ReplayFileInfo b)
        {
            int c = b.RecordedAtUtc.CompareTo(a.RecordedAtUtc);
            return c != 0 ? c : string.CompareOrdinal(b.FileName, a.FileName);
        }

        private static ReplayFileInfo ReadHeader(string path)
        {
            try
            {
                var data = new ReplayData { HeaderOnly = true };
                using (var stream = File.OpenRead(path))
                {
                    var reader = new ReuseableBinaryReader(stream);
                    reader.ReadPersistableInto(data);
                }
                return new ReplayFileInfo
                {
                    FileName = Path.GetFileName(path),
                    HeroName = data.HeroName,
                    JobName = data.JobName,
                    PitLevelAtStart = data.PitLevelAtStart,
                    RecordedAtUtc = new DateTime(data.RecordedAtUtcTicks, DateTimeKind.Utc),
                    TotalTicks = data.TotalTicks,
                    Kind = data.Kind,
                    BuildId = data.BuildId,
                };
            }
            catch (Exception ex)
            {
                Debug.Warn($"[ReplayFileService] Skipping unreadable replay {path}: {ex.Message}");
                return null;
            }
        }
    }
}
