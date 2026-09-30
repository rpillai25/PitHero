using System;
using System.Collections.Generic;
using System.IO;
using Nez;
using PitHero.Services.Replay.Frames;

namespace PitHero.Services.Replay
{
    /// <summary>
    /// The two halves of the replay autosave (issue #444). <see cref="GatherRecoverySnapshot"/> is the
    /// autosave's main-thread gather: the recording as it stands plus the console lines, written to
    /// <c>replay_recovery_&lt;hero&gt;.bin</c> on the autosave worker. <see cref="Run"/> is the launch-time
    /// recovery: every recovery file left on disk belongs to a session that never reached the quit path,
    /// so it is promoted to that hero's auto replay and its <c>session_*.frames</c> file, footer-less
    /// since the crash, is finished with the recording's console lines and moved beside it as the cache.
    /// Headless: runs before any scene exists and never throws out of <see cref="Run"/>.
    /// </summary>
    public static class ReplayCrashRecovery
    {
        /// <summary>
        /// The recording to write as the hero's recovery file, or null when there is nothing to protect:
        /// no session, a replay playing back, tick 0, or a hero without an id. Called on the main
        /// thread by the autosave; the snapshot is a detached copy the worker may serialize freely.
        /// Never releases pauses on the record (the quit path does, because its scene is ending).
        /// </summary>
        public static ReplayData GatherRecoverySnapshot()
        {
            if (ReplayPlaybackService.IsPlaybackActive)
                return null;
            var recorder = ReplayRecorder.Current;
            if (recorder == null || !recorder.IsInitialized || recorder.HeroId == 0)
                return null;
            long tick = SimulationClock.CurrentTick;
            if (tick <= 0)
                return null;
            var data = recorder.Snapshot(tick);
            data.ConsoleLog = FrameRecorder.Current?.ConsoleLog?.Clone();
            return data;
        }

        /// <summary>
        /// Promotes every recovery recording on disk (see the class summary) and pairs the promoted
        /// recording with its session frame file when one is there. Returns the number of recordings
        /// promoted. Each file is handled on its own; a failure is logged and the next file is tried.
        /// </summary>
        public static int Run(ReplayFileService files)
        {
            if (files == null)
                return 0;
            int promoted = 0;
            List<string> recoveries;
            try
            {
                recoveries = files.RecoveryFileNames();
            }
            catch (Exception ex)
            {
                Debug.Warn("[ReplayCrashRecovery] Could not list recovery replays: " + ex.Message);
                return 0;
            }
            for (int i = 0; i < recoveries.Count; i++)
            {
                try
                {
                    if (RecoverOne(files, recoveries[i]))
                        promoted++;
                }
                catch (Exception ex)
                {
                    Debug.Warn("[ReplayCrashRecovery] Could not recover " + recoveries[i] + ": " + ex.Message);
                }
            }
            return promoted;
        }

        /// <summary>
        /// One recovery file: settles it against the hero's auto replay, promotes the winner to the auto
        /// name and attaches its session frames. True when the recovery recording became the auto replay.
        /// </summary>
        private static bool RecoverOne(ReplayFileService files, string recoveryName)
        {
            var recovery = files.Load(recoveryName);
            if (recovery == null)
            {
                // Unreadable: nothing to promote, and the file must not keep protecting a session file forever
                files.Delete(recoveryName);
                Debug.Warn("[ReplayCrashRecovery] Deleted unreadable recovery replay " + recoveryName);
                return false;
            }

            string autoName = ReplayFileService.AutoFileName(recovery.HeroId);
            var auto = files.Load(autoName);
            if (auto != null && !RecoveryWins(recovery, auto))
            {
                // The auto replay is the same timeline and at least as long (a clean quit whose recovery
                // delete failed): keep it, and pair it with a session file if its cache went missing
                files.Delete(recoveryName);
                AttachSessionFrames(files, autoName, auto, allowTrim: false);
                Debug.Log($"[ReplayCrashRecovery] Recovery replay for hero {recovery.HeroId:X8} superseded by the auto replay ({auto.TotalTicks} ticks vs {recovery.TotalTicks}); deleted");
                return false;
            }

            // Promote: the previous auto replay and its cache go, exactly as a normal quit overwrites them
            files.Delete(autoName);
            File.Move(Path.Combine(files.Directory_, recoveryName), Path.Combine(files.Directory_, autoName), overwrite: true);
            bool cached = AttachSessionFrames(files, autoName, recovery, allowTrim: true);
            Debug.Log($"[ReplayCrashRecovery] Promoted the recovery replay of hero {recovery.HeroId:X8} to {autoName}: {recovery.TotalTicks} ticks, {recovery.Commands.Count} commands, frame cache {(cached ? "attached" : "not available")}");
            return true;
        }

        /// <summary>
        /// The owner's tiebreak, refined for branches: the same recording time means the same session,
        /// where the longer file wins — unless the shorter one is not a prefix of the longer one, which
        /// only a Time Travel branch inside the hero's own auto replay produces (the recorder keeps the
        /// replay's recording time); the branch is the player's real timeline, so the recovery wins.
        /// A different recording time is a different, crashed session: the recovery wins.
        /// </summary>
        public static bool RecoveryWins(ReplayData recovery, ReplayData auto)
        {
            if (recovery.RecordedAtUtcTicks != auto.RecordedAtUtcTicks)
                return true;
            var shorter = recovery.TotalTicks <= auto.TotalTicks ? recovery : auto;
            var longer = ReferenceEquals(shorter, recovery) ? auto : recovery;
            if (!IsPrefixOf(shorter, longer))
                return true;
            return recovery.TotalTicks > auto.TotalTicks;
        }

        /// <summary>True when every command and state sample of <paramref name="shorter"/> matches the start of <paramref name="longer"/>.</summary>
        private static bool IsPrefixOf(ReplayData shorter, ReplayData longer)
        {
            if (shorter.Commands.Count > longer.Commands.Count || shorter.StateHashes.Count > longer.StateHashes.Count)
                return false;
            for (int i = 0; i < shorter.Commands.Count; i++)
            {
                var a = shorter.Commands[i];
                var b = longer.Commands[i];
                if (a.Tick != b.Tick || a.Command.Type != b.Command.Type || a.Command.A != b.Command.A || a.Command.B != b.Command.B
                    || a.Command.C != b.Command.C || a.Command.D != b.Command.D || a.Command.L != b.Command.L
                    || a.Command.F != b.Command.F || !string.Equals(a.Command.S, b.Command.S, StringComparison.Ordinal))
                    return false;
            }
            for (int i = 0; i < shorter.StateHashes.Count; i++)
            {
                if (shorter.StateHashes[i].Tick != longer.StateHashes[i].Tick || shorter.StateHashes[i].Hash != longer.StateHashes[i].Hash)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Gives the recording at <paramref name="fileName"/> its frame cache from the session file the
        /// crash left behind, when it has no valid cache yet: the file is re-indexed (the reopen scans a
        /// footer-less file and cuts a partial tail, or an old footer), chunks past the recording's end
        /// are cut, the recording is trimmed to the frames on disk when they stop short (at most the
        /// partial chunk the crash swallowed; only with <paramref name="allowTrim"/>, a promoted
        /// recovery — a complete auto replay is never shortened to fit a shorter session file), and the
        /// footer is written with the recording's console lines before the file moves beside the
        /// recording. True when a cache was attached.
        /// </summary>
        private static bool AttachSessionFrames(ReplayFileService files, string fileName, ReplayData data, bool allowTrim)
        {
            if (files.HasValidFrameCache(fileName, data))
                return false;
            string sessionPath = files.SessionFilePath(data.RecordedAtUtcTicks);
            if (!File.Exists(sessionPath))
                return false;

            var result = FrameSidecarWriter.Reopen(sessionPath, out var writer);
            if (result != FrameSidecarFile.OpenResult.Ok)
            {
                Debug.Warn($"[ReplayCrashRecovery] Session frames {Path.GetFileName(sessionPath)} unusable ({result}); {fileName} plays by re-simulation");
                return false;
            }
            try
            {
                if (!writer.Identity.MatchesRecording(data.MasterSeed, data.RecordedAtUtcTicks, data.SimulationVersion) || writer.ChunkTicks != GameConfig.ReplayFrameChunkTicks)
                {
                    Debug.Warn($"[ReplayCrashRecovery] Session frames {Path.GetFileName(sessionPath)} belong to another recording; {fileName} plays by re-simulation");
                    writer.Dispose();
                    return false;
                }
                // Chunks are contiguous from tick 0, so the ones that start before the recording's end are the first ceil(TotalTicks / ChunkTicks)
                long wanted = (data.TotalTicks + writer.ChunkTicks - 1) / writer.ChunkTicks;
                int keep = wanted < writer.ChunkCount ? (int)wanted : writer.ChunkCount;
                writer.TruncateChunks(keep);
                long endTick = writer.EndTick;
                if (endTick < 0)
                {
                    writer.Dispose();
                    return false;
                }
                if (endTick < data.TotalTicks - 1)
                {
                    if (!allowTrim)
                    {
                        Debug.Warn($"[ReplayCrashRecovery] Session frames {Path.GetFileName(sessionPath)} stop at tick {endTick}, before the end of {fileName}; left uncached");
                        writer.Dispose();
                        return false;
                    }
                    // The crash swallowed the partial last chunk: end the recording where the frames end
                    // so the replay opens in the frame viewer instead of re-simulating for a two-second tail
                    Debug.Log($"[ReplayCrashRecovery] Trimming {fileName} from {data.TotalTicks} to {endTick + 1} ticks to match its frames");
                    data.TruncateAfter(endTick);
                    files.Overwrite(fileName, data);
                }
                writer.FinishAndMove(files.FrameCachePath(fileName), data.TotalTicks, data.ConsoleLog);
                files.EnforceFrameCacheBudget(GameConfig.ReplayFrameCacheDiskBudgetBytes);
                return true;
            }
            catch (Exception)
            {
                writer.Dispose();
                throw;
            }
        }
    }
}
