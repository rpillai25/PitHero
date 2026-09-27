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
        /// <summary>Hero the recording belongs to (0 = unknown, pre-v3 file).</summary>
        public int HeroId;
        public DateTime RecordedAtUtc;
        public long TotalTicks;
        public ReplayKind Kind;
        public string BuildId;
        /// <summary>Simulation logic version that recorded it (0 = pre-v4 file).</summary>
        public int SimulationVersion;
        /// <summary>Seed the session started from (part of the frame cache identity).</summary>
        public int MasterSeed;
        /// <summary>True when a matching, complete <c>.frames</c> cache sits next to the file: it opens in the frame viewer at once (issue #429).</summary>
        public bool HasFrameCache;
        /// <summary>True for the quit-time recording of a hero (one per hero id, overwritten every session).</summary>
        public bool IsAutoSave => ReplayFileService.IsAutoFileName(FileName);

        /// <summary>False when the recording predates a simulation change: it may diverge and cannot time-travel.</summary>
        public bool IsCurrentSimulation => SimulationVersion == GameConfig.SimulationVersion;

        /// <summary>Duration in seconds implied by TotalTicks.</summary>
        public float DurationSeconds => TotalTicks * GameConfig.SimulationFixedStepSeconds;
    }

    /// <summary>
    /// Saves, lists, loads and deletes replay files under the persistent data folder
    /// (%LOCALAPPDATA%\&lt;exe&gt;\replays, alongside the save slots), and the <c>.frames</c> caches
    /// beside them (issue #429): a cache is written with the save, deleted with the recording, flagged
    /// in the list when it matches, and kept under a disk budget that only ever deletes caches. Global service.
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

        /// <summary>The quit-time recording's file name for a hero: replay_auto_&lt;HeroId as 8 hex digits&gt;.bin (the autosave's naming).</summary>
        public static string AutoFileName(int heroId)
        {
            return GameConfig.ReplayAutoFilePrefix + ((uint)heroId).ToString("X8") + GameConfig.ReplayFileExtension;
        }

        /// <summary>True when the file name is a hero's quit-time recording (see <see cref="AutoFileName"/>).</summary>
        public static bool IsAutoFileName(string fileName)
        {
            return !string.IsNullOrEmpty(fileName) && fileName.StartsWith(GameConfig.ReplayAutoFilePrefix, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Writes the recording as the hero's quit-time replay, replacing the previous session's (one
        /// per hero id, so the automatic saves never pile up; a stale frame cache beside it goes first).
        /// A recording without a hero id falls back to a new dated file. Returns the file name.
        /// </summary>
        public string SaveAuto(ReplayData data)
        {
            if (data == null)
                return null;
            if (data.HeroId == 0)
                return Save(data);
            string fileName = AutoFileName(data.HeroId);
            var staleCache = FrameCachePath(fileName);
            if (File.Exists(staleCache))
                File.Delete(staleCache); // the previous session's frames would only mismatch the new recording
            _store.Save(fileName, data);
            Debug.Log($"[ReplayFileService] Saved quit-time replay {fileName} ({data.Commands.Count} commands, {data.TotalTicks} ticks)");
            return fileName;
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

        /// <summary>
        /// Saves the recording (as the hero's single quit-time replay with <paramref name="autoSave"/>,
        /// else as a new dated file) and, when <paramref name="frames"/> holds the session's frame
        /// stream, its <c>.frames</c> cache next to it (the sidecar is moved there when the session is
        /// ending, copied otherwise), then trims the caches to the disk budget. Returns the replay file name, or null.
        /// </summary>
        public string SaveWithFrameCache(ReplayData data, Frames.FrameRecorder frames, bool endSession, bool autoSave = false)
        {
            string fileName = autoSave ? SaveAuto(data) : Save(data);
            if (fileName == null)
                return null;
            if (frames != null && frames.IsInitialized)
            {
                bool cached = frames.ExportSidecar(FrameCachePath(fileName), data.TotalTicks, endSession);
                if (cached)
                    EnforceFrameCacheBudget(GameConfig.ReplayFrameCacheDiskBudgetBytes);
            }
            return fileName;
        }

        /// <summary>Name of the frame cache that belongs to a replay file: the same name with the <c>.frames</c> extension.</summary>
        public static string FrameCacheFileName(string replayFileName)
        {
            return Path.ChangeExtension(replayFileName, GameConfig.ReplayFrameFileExtension);
        }

        /// <summary>Full path of the frame cache that belongs to a replay file.</summary>
        public string FrameCachePath(string replayFileName)
        {
            return Path.Combine(_directory, FrameCacheFileName(replayFileName));
        }

        /// <summary>
        /// Opens the frame cache of a replay when it is valid for that recording: same identity
        /// (seed, recording time, simulation version, frame format), finished with a footer, the same
        /// tick count and frames up to the last tick. The caller owns the reader. Any other result
        /// leaves <paramref name="reader"/> null (the replay then plays by re-simulation).
        /// </summary>
        public Frames.FrameSidecarFile.OpenResult TryOpenFrameCache(string replayFileName, int masterSeed, long recordedAtUtcTicks,
            int simulationVersion, long totalTicks, out Frames.FrameSidecarReader reader)
        {
            var result = Frames.FrameSidecarReader.Open(FrameCachePath(replayFileName), masterSeed, recordedAtUtcTicks, simulationVersion, out reader);
            if (result != Frames.FrameSidecarFile.OpenResult.Ok)
                return result;
            if (reader.HasFooter && reader.TotalTicks != totalTicks)
                result = Frames.FrameSidecarFile.OpenResult.IdentityMismatch;
            else if (!reader.HasFooter || reader.EndTick < totalTicks - 1)
                result = Frames.FrameSidecarFile.OpenResult.Incomplete;
            if (result != Frames.FrameSidecarFile.OpenResult.Ok)
            {
                reader.Dispose();
                reader = null;
            }
            return result;
        }

        /// <summary>True when a valid frame cache exists for the listed replay (see <see cref="TryOpenFrameCache"/>).</summary>
        public bool HasValidFrameCache(ReplayFileInfo info)
        {
            if (info == null)
                return false;
            var result = TryOpenFrameCache(info.FileName, info.MasterSeed, info.RecordedAtUtc.Ticks, info.SimulationVersion, info.TotalTicks, out var reader);
            reader?.Dispose();
            return result == Frames.FrameSidecarFile.OpenResult.Ok;
        }

        /// <summary>
        /// Deletes the oldest frame caches (by last write time) until the <c>replay_*.frames</c> files
        /// under the directory fit <paramref name="budgetBytes"/>. Recordings are never touched; a cache
        /// that cannot be deleted (open elsewhere) is skipped. Returns the number of files deleted.
        /// </summary>
        public int EnforceFrameCacheBudget(long budgetBytes)
        {
            if (!Directory.Exists(_directory))
                return 0;
            string[] paths;
            try
            {
                paths = Directory.GetFiles(_directory, GameConfig.ReplayFilePrefix + "*" + GameConfig.ReplayFrameFileExtension);
            }
            catch (IOException)
            {
                return 0;
            }
            var caches = new List<FileInfo>(paths.Length);
            long total = 0;
            for (int i = 0; i < paths.Length; i++)
            {
                var fi = new FileInfo(paths[i]);
                caches.Add(fi);
                total += fi.Length;
            }
            if (total <= budgetBytes)
                return 0;
            caches.Sort(CompareOldestFirst);
            int deleted = 0;
            for (int i = 0; i < caches.Count && total > budgetBytes; i++)
            {
                // Read before Delete: a FileInfo forgets its length once the file is gone
                long length = caches[i].Length;
                string name = caches[i].Name;
                try
                {
                    caches[i].Delete();
                    total -= length;
                    deleted++;
                    Debug.Log($"[ReplayFileService] Frame cache budget: deleted {name} ({length / 1024} KB)");
                }
                catch (IOException ex)
                {
                    Debug.Warn($"[ReplayFileService] Frame cache budget: could not delete {name}: {ex.Message}");
                }
                catch (UnauthorizedAccessException ex)
                {
                    Debug.Warn($"[ReplayFileService] Frame cache budget: could not delete {name}: {ex.Message}");
                }
            }
            return deleted;
        }

        private static int CompareOldestFirst(FileInfo a, FileInfo b)
        {
            int c = a.LastWriteTimeUtc.CompareTo(b.LastWriteTimeUtc);
            return c != 0 ? c : string.CompareOrdinal(a.Name, b.Name);
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

        /// <summary>Deletes a replay file and its frame cache. Returns true if the recording was removed.</summary>
        public bool Delete(string fileName)
        {
            var cache = FrameCachePath(fileName);
            if (File.Exists(cache))
                File.Delete(cache);
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
                if (info == null)
                    continue;
                info.HasFrameCache = HasValidFrameCache(info);
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
                    HeroId = data.HeroId,
                    PitLevelAtStart = data.PitLevelAtStart,
                    RecordedAtUtc = new DateTime(data.RecordedAtUtcTicks, DateTimeKind.Utc),
                    TotalTicks = data.TotalTicks,
                    Kind = data.Kind,
                    BuildId = data.BuildId,
                    SimulationVersion = data.SimulationVersion,
                    MasterSeed = data.MasterSeed,
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
