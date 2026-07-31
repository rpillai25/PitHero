using System.Collections.Generic;
using RolePlayingFramework.AlliedMonsters;

namespace PitHero.Services.AutoJob
{
    /// <summary>How many workers a job wants, and whether its current workers are protected from demotion.</summary>
    public struct JobDemandEntry
    {
        /// <summary>The job this demand entry staffs.</summary>
        public MonsterJob Job;

        /// <summary>Workers filled before any job's desired slots.</summary>
        public int MinWorkers;

        /// <summary>Workers filled after all jobs' minimums, in demand-list order.</summary>
        public int DesiredWorkers;

        /// <summary>When true, monsters currently on this job are never demoted (kitchen rule).</summary>
        public bool Sticky;
    }

    /// <summary>Immutable view of one roster monster used by the solver.</summary>
    public struct MonsterJobSnapshot
    {
        /// <summary>Index of this monster in the roster list; used as the deterministic tie-breaker.</summary>
        public int RosterIndex;

        /// <summary>The monster's job before this solve.</summary>
        public MonsterJob CurrentJob;

        /// <summary>Farming proficiency rating, 1–9.</summary>
        public int FarmingProficiency;

        /// <summary>Cooking proficiency rating, 1–9.</summary>
        public int CookingProficiency;

        /// <summary>Fishing proficiency rating, 1–9.</summary>
        public int FishingProficiency;
    }

    /// <summary>
    /// Pure, deterministic assignment solver: fills each job's minimum staffing first (by proficiency),
    /// then desired staffing, honoring sticky jobs whose workers are never demoted — except that a
    /// higher-priority job left with zero workers may pull sticky workers from lower-priority jobs
    /// (StarvationReleasePass). Monsters left unassigned get MonsterJob.None so the coordinators
    /// send them home.
    /// </summary>
    public static class JobAssignmentSolver
    {
        /// <summary>Returns the monster's proficiency for the given job (0 for None or unknown jobs).</summary>
        public static int GetProficiency(in MonsterJobSnapshot m, MonsterJob job)
        {
            switch (job)
            {
                case MonsterJob.Farming: return m.FarmingProficiency;
                case MonsterJob.Cooking: return m.CookingProficiency;
                case MonsterJob.Fishing: return m.FishingProficiency;
                default: return 0;
            }
        }

        /// <summary>
        /// Computes new assignments. resultJobs is cleared and refilled parallel to monsters.
        /// Deterministic: candidate ties are broken by lowest RosterIndex. Allocation-free beyond
        /// growth of the caller's result list.
        /// </summary>
        public static void Solve(List<MonsterJobSnapshot> monsters, List<JobDemandEntry> demands,
            List<MonsterJob> resultJobs)
        {
            resultJobs.Clear();
            for (int i = 0; i < monsters.Count; i++)
                resultJobs.Add(MonsterJob.None);

            // Sticky pass: workers on a sticky job keep it unconditionally, even above desired count.
            for (int i = 0; i < monsters.Count; i++)
            {
                var current = monsters[i].CurrentJob;
                if (current == MonsterJob.None)
                    continue;
                for (int d = 0; d < demands.Count; d++)
                {
                    if (demands[d].Sticky && demands[d].Job == current)
                    {
                        resultJobs[i] = current;
                        break;
                    }
                }
            }

            FillPass(monsters, demands, resultJobs, true);
            FillPass(monsters, demands, resultJobs, false);
            StarvationReleasePass(monsters, demands, resultJobs);
            SwapPass(monsters, demands, resultJobs);
        }

        /// <summary>Fills each demand up to MinWorkers (minPass) or DesiredWorkers by best proficiency.</summary>
        private static void FillPass(List<MonsterJobSnapshot> monsters, List<JobDemandEntry> demands,
            List<MonsterJob> resultJobs, bool minPass)
        {
            for (int d = 0; d < demands.Count; d++)
            {
                var demand = demands[d];
                int target = minPass ? demand.MinWorkers : demand.DesiredWorkers;
                int assigned = CountAssigned(resultJobs, demand.Job);
                while (assigned < target)
                {
                    int best = -1;
                    int bestProficiency = -1;
                    for (int i = 0; i < monsters.Count; i++)
                    {
                        if (resultJobs[i] != MonsterJob.None)
                            continue;
                        int proficiency = GetProficiency(monsters[i], demand.Job);
                        if (proficiency > bestProficiency)
                        {
                            best = i;
                            bestProficiency = proficiency;
                        }
                    }
                    if (best < 0)
                        break;
                    resultJobs[best] = demand.Job;
                    assigned++;
                }
            }
        }

        /// <summary>
        /// The one exception to stickiness: a demand with MinWorkers > 0 that ends the fill passes with
        /// ZERO workers proves every candidate is sticky-locked elsewhere (fill exhausts free monsters
        /// first). Pull sticky-held workers from LOWER-priority demands, up to the starved job's
        /// DesiredWorkers, never dropping a raided job below its own MinWorkers.
        /// </summary>
        private static void StarvationReleasePass(List<MonsterJobSnapshot> monsters,
            List<JobDemandEntry> demands, List<MonsterJob> resultJobs)
        {
            for (int d = 0; d < demands.Count; d++)
            {
                var demand = demands[d];
                if (demand.MinWorkers <= 0)
                    continue;
                if (CountAssigned(resultJobs, demand.Job) > 0)
                    continue;

                int assigned = 0;
                while (assigned < demand.DesiredWorkers)
                {
                    int best = -1;
                    int bestProficiency = -1;
                    for (int i = 0; i < monsters.Count; i++)
                    {
                        var job = resultJobs[i];
                        if (job == MonsterJob.None || job == demand.Job)
                            continue;
                        int otherIndex = IndexOfDemand(demands, job);
                        if (otherIndex <= d)
                            continue;
                        if (!demands[otherIndex].Sticky || monsters[i].CurrentJob != job)
                            continue;
                        if (CountAssigned(resultJobs, job) <= demands[otherIndex].MinWorkers)
                            continue;
                        int proficiency = GetProficiency(monsters[i], demand.Job);
                        if (proficiency > bestProficiency)
                        {
                            best = i;
                            bestProficiency = proficiency;
                        }
                    }
                    if (best < 0)
                        break;
                    resultJobs[best] = demand.Job;
                    assigned++;
                }
            }
        }

        /// <summary>
        /// One deterministic improvement pass: a sticky worker swaps jobs with a non-sticky worker only
        /// when BOTH jobs strictly gain proficiency, so assignments never oscillate between solves.
        /// </summary>
        private static void SwapPass(List<MonsterJobSnapshot> monsters, List<JobDemandEntry> demands,
            List<MonsterJob> resultJobs)
        {
            for (int d = 0; d < demands.Count; d++)
            {
                if (!demands[d].Sticky)
                    continue;
                var stickyJob = demands[d].Job;
                for (int w = 0; w < monsters.Count; w++)
                {
                    if (resultJobs[w] != stickyJob || monsters[w].CurrentJob != stickyJob)
                        continue;
                    int best = -1;
                    int bestGain = 0;
                    for (int f = 0; f < monsters.Count; f++)
                    {
                        var otherJob = resultJobs[f];
                        if (otherJob == MonsterJob.None || otherJob == stickyJob)
                            continue;
                        if (IsSticky(demands, otherJob) && monsters[f].CurrentJob == otherJob)
                            continue;
                        int stickyGain = GetProficiency(monsters[f], stickyJob) - GetProficiency(monsters[w], stickyJob);
                        int otherGain = GetProficiency(monsters[w], otherJob) - GetProficiency(monsters[f], otherJob);
                        if (stickyGain <= 0 || otherGain <= 0)
                            continue;
                        if (stickyGain + otherGain > bestGain)
                        {
                            best = f;
                            bestGain = stickyGain + otherGain;
                        }
                    }
                    if (best >= 0)
                    {
                        var tmp = resultJobs[w];
                        resultJobs[w] = resultJobs[best];
                        resultJobs[best] = tmp;
                    }
                }
            }
        }

        private static int IndexOfDemand(List<JobDemandEntry> demands, MonsterJob job)
        {
            for (int d = 0; d < demands.Count; d++)
            {
                if (demands[d].Job == job)
                    return d;
            }
            return -1;
        }

        private static bool IsSticky(List<JobDemandEntry> demands, MonsterJob job)
        {
            for (int d = 0; d < demands.Count; d++)
            {
                if (demands[d].Job == job)
                    return demands[d].Sticky;
            }
            return false;
        }

        private static int CountAssigned(List<MonsterJob> resultJobs, MonsterJob job)
        {
            int count = 0;
            for (int i = 0; i < resultJobs.Count; i++)
            {
                if (resultJobs[i] == job)
                    count++;
            }
            return count;
        }
    }
}
