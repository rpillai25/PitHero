using System;
using System.Collections.Generic;
using Nez.AI.GOAP;

namespace PitHero.Services.Replay
{
    /// <summary>
    /// Single reporting point for the divergence tripwires. While recording, samples go to the
    /// <see cref="ReplayRecorder"/>; while a replay plays, the playback service installs
    /// <see cref="PlaybackDecisionCheck"/> / <see cref="PlaybackStateHashCheck"/> and compares each
    /// sample against the recording at the same tick.
    /// </summary>
    public static class ReplayTripwire
    {
        /// <summary>Installed by the playback service: (tick, hash) for a hero decision.</summary>
        public static Action<long, ulong> PlaybackDecisionCheck;

        /// <summary>Installed by the playback service: a periodic state sample with its part hashes.</summary>
        public static System.Action<ReplayHashSample> PlaybackStateHashCheck;

        /// <summary>Hashes a hero GOAP plan (action names in execution order) plus the hero tile.</summary>
        public static ulong HashPlan(Stack<Nez.AI.GOAP.Action> plan, int tileX, int tileY)
        {
            ulong h = ReplayIO.HashSeed;
            h = ReplayIO.Hash(h, tileX);
            h = ReplayIO.Hash(h, tileY);
            if (plan == null)
                return ReplayIO.Hash(h, -1);
            h = ReplayIO.Hash(h, plan.Count);
            // Stack enumeration walks from the top (next action) down, which is the execution order
            var e = plan.GetEnumerator();
            while (e.MoveNext())
                h = ReplayIO.Hash(h, e.Current.Name);
            e.Dispose();
            return h;
        }

        /// <summary>Reports a hero decision at the current tick.</summary>
        public static void ReportDecision(ulong hash)
        {
            long tick = SimulationClock.CurrentTick;
            var recorder = ReplayRecorder.Current;
            if (recorder != null && recorder.IsRecording)
            {
                recorder.RecordDecision(tick, hash);
                return;
            }
            PlaybackDecisionCheck?.Invoke(tick, hash);
        }

        /// <summary>Reports a periodic state sample.</summary>
        public static void ReportStateHash(in ReplayHashSample sample)
        {
            var recorder = ReplayRecorder.Current;
            if (recorder != null && recorder.IsRecording)
            {
                recorder.RecordStateHash(in sample);
                return;
            }
            PlaybackStateHashCheck?.Invoke(sample);
        }
    }
}
