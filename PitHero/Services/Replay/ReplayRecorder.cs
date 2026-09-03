using System;
using System.Collections.Generic;

namespace PitHero.Services.Replay
{
    /// <summary>
    /// Always-on, scene-scoped recorder of the current session. Captures the start state in
    /// <see cref="Initialize"/>, then every applied player command (via PlayerCommandService),
    /// hero decision hash and periodic state hash. <see cref="Snapshot"/> copies the recording into a
    /// <see cref="ReplayData"/> for saving or for "Replay current session". During playback the
    /// recorder is preloaded with the replay's own lists and paused; when the player exits at the
    /// end of the timeline it resumes appending so the session stays continuous.
    /// </summary>
    public sealed class ReplayRecorder
    {
        private const int InitialCapacity = 4096;

        private ReplayKind _kind;
        private int _masterSeed;
        private string _heroName = string.Empty;
        private string _jobName = string.Empty;
        private int _pitLevelAtStart;
        private long _recordedAtUtcTicks;
        private byte[] _stateBlob;
        private readonly List<ReplayCommandRecord> _commands = new List<ReplayCommandRecord>(InitialCapacity);
        private readonly List<ReplayHashSample> _decisions = new List<ReplayHashSample>(InitialCapacity);
        private readonly List<ReplayHashSample> _stateHashes = new List<ReplayHashSample>(InitialCapacity);

        /// <summary>The scene's recorder, or null outside a game session.</summary>
        public static ReplayRecorder Current { get; private set; }

        /// <summary>False while a replay plays back (its lists are the recording; nothing new is appended).</summary>
        public bool IsRecording { get; set; } = true;

        /// <summary>True once <see cref="Initialize"/> has captured a start state.</summary>
        public bool IsInitialized => _stateBlob != null || _kind == ReplayKind.NewGame && _recordedAtUtcTicks != 0;

        /// <summary>Recorded commands (read-only view for playback).</summary>
        public IReadOnlyList<ReplayCommandRecord> Commands => _commands;
        /// <summary>Recorded hero decision hashes.</summary>
        public IReadOnlyList<ReplayHashSample> Decisions => _decisions;
        /// <summary>Recorded periodic state hashes.</summary>
        public IReadOnlyList<ReplayHashSample> StateHashes => _stateHashes;

        /// <summary>Creates the recorder and makes it the current instance.</summary>
        public ReplayRecorder()
        {
            Current = this;
        }

        /// <summary>Clears the static instance when the owning scene unloads.</summary>
        public void Detach()
        {
            if (Current == this)
                Current = null;
        }

        /// <summary>
        /// Captures the session start. <paramref name="preload"/> (replay playback) copies the replay's
        /// existing lists so the recording is continuous if live play resumes at the end.
        /// </summary>
        public void Initialize(ReplayKind kind, int masterSeed, byte[] stateBlob, ReplayData preload)
        {
            _kind = kind;
            _masterSeed = masterSeed;
            _stateBlob = stateBlob;
            _recordedAtUtcTicks = preload != null ? preload.RecordedAtUtcTicks : DateTime.UtcNow.Ticks;
            _commands.Clear();
            _decisions.Clear();
            _stateHashes.Clear();
            if (preload != null)
            {
                _heroName = preload.HeroName;
                _jobName = preload.JobName;
                _pitLevelAtStart = preload.PitLevelAtStart;
                _commands.AddRange(preload.Commands);
                _decisions.AddRange(preload.Decisions);
                _stateHashes.AddRange(preload.StateHashes);
            }
        }

        /// <summary>Fills in the display metadata once the hero exists (called after Begin builds the world).</summary>
        public void SetSessionInfo(string heroName, string jobName, int pitLevel)
        {
            if (!string.IsNullOrEmpty(heroName))
                _heroName = heroName;
            if (!string.IsNullOrEmpty(jobName))
                _jobName = jobName;
            if (pitLevel > 0 && _pitLevelAtStart == 0)
                _pitLevelAtStart = pitLevel;
        }

        /// <summary>Records an applied command. Subscribed to PlayerCommandService.OnCommandApplied.</summary>
        public void RecordCommand(long tick, PlayerCommand command)
        {
            if (!IsRecording)
                return;
            _commands.Add(new ReplayCommandRecord(tick, in command));
        }

        /// <summary>Records a hero decision hash.</summary>
        public void RecordDecision(long tick, ulong hash)
        {
            if (!IsRecording)
                return;
            _decisions.Add(new ReplayHashSample(tick, hash));
        }

        /// <summary>Records a periodic state hash (combined only; parts zero).</summary>
        public void RecordStateHash(long tick, ulong hash)
        {
            if (!IsRecording)
                return;
            _stateHashes.Add(new ReplayHashSample(tick, hash));
        }

        /// <summary>Records a periodic state sample with its part hashes.</summary>
        public void RecordStateHash(in ReplayHashSample sample)
        {
            if (!IsRecording)
                return;
            _stateHashes.Add(sample);
        }

        /// <summary>Copies the recording into a new ReplayData ending at <paramref name="totalTicks"/>.</summary>
        public ReplayData Snapshot(long totalTicks)
        {
            var data = new ReplayData
            {
                Kind = _kind,
                MasterSeed = _masterSeed,
                HeroName = _heroName ?? string.Empty,
                JobName = _jobName ?? string.Empty,
                PitLevelAtStart = _pitLevelAtStart,
                RecordedAtUtcTicks = _recordedAtUtcTicks,
                TotalTicks = totalTicks,
                BuildId = BuildIdentity.Current,
                StateBlob = _stateBlob,
            };
            data.Commands.AddRange(_commands);
            data.Decisions.AddRange(_decisions);
            data.StateHashes.AddRange(_stateHashes);
            return data;
        }
    }

    /// <summary>Identifies the running build for replay headers (assembly version + informational version).</summary>
    public static class BuildIdentity
    {
        private static string _current;

        /// <summary>A stable string for this build.</summary>
        public static string Current
        {
            get
            {
                if (_current == null)
                {
                    var asm = typeof(BuildIdentity).Assembly;
                    _current = asm.GetName().Version?.ToString() ?? "0";
                }
                return _current;
            }
        }
    }
}
