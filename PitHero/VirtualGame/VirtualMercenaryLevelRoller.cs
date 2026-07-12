using RolePlayingFramework.Jobs;
using RolePlayingFramework.Jobs.Primary;

namespace PitHero.VirtualGame
{
    /// <summary>
    /// Static helpers that mirror <see cref="PitHero.Services.MercenaryManager"/> internals
    /// for use in the headless gold-economy simulation.
    ///
    /// <para>
    /// Both methods use <c>Nez.Random</c> (seeded per run by
    /// <see cref="VirtualGameSimulation(int)"/>) so results are fully reproducible.
    /// The logic is a verbatim copy of the live methods, extracted here so the virtual
    /// layer does not depend on <c>MercenaryManager</c> which has heavy graphics/Nez-service
    /// dependencies.
    /// </para>
    /// </summary>
    public static class VirtualMercenaryLevelRoller
    {
        /// <summary>
        /// Mirrors <c>MercenaryManager.DetermineMercenaryLevel</c> exactly:
        /// <list type="table">
        ///   <item>20 % — level 1</item>
        ///   <item>30 % — random [1, heroLevel/3]</item>
        ///   <item>20 % — random [heroLevel/3, heroLevel/2]</item>
        ///   <item>20 % — random [heroLevel/2, heroLevel]</item>
        ///   <item>10 % — heroLevel</item>
        /// </list>
        /// </summary>
        /// <param name="heroLevel">Current hero level (distribution anchor).</param>
        /// <param name="minLevel">Minimum level floor — tier base level when tier ≥ 2, otherwise 1.</param>
        public static int DetermineMercenaryLevel(int heroLevel, int minLevel = 1)
        {
            if (heroLevel < 1) heroLevel = 1;

            float roll = Nez.Random.NextFloat();
            int level;

            if (roll < 0.20f)
            {
                level = 1;
            }
            else if (roll < 0.50f)
            {
                int max = heroLevel / 3;
                level = max < 1 ? 1 : Nez.Random.Range(1, max + 1);
            }
            else if (roll < 0.70f)
            {
                int min = heroLevel / 3;
                int max = heroLevel / 2;
                if (min < 1) min = 1;
                if (max < min) max = min;
                level = Nez.Random.Range(min, max + 1);
            }
            else if (roll < 0.90f)
            {
                int min = heroLevel / 2;
                if (min < 1) min = 1;
                level = Nez.Random.Range(min, heroLevel + 1);
            }
            else
            {
                level = heroLevel;
            }

            int raw = level < 1 ? 1 : level;
            return raw < minLevel ? minLevel : raw;
        }

        /// <summary>
        /// Mirrors <c>MercenaryManager.GetRandomJob</c>: picks uniformly from
        /// Knight, Monk, Thief, Archer, Mage, Priest via <c>Nez.Random.Range(0, 6)</c>.
        /// Uses a switch instead of reflection (<c>Activator.CreateInstance</c>) for AOT
        /// compliance.
        /// </summary>
        public static IJob GetRandomJob()
        {
            int idx = Nez.Random.Range(0, 6); // [0, 5] inclusive
            switch (idx)
            {
                case 0:  return new Knight();
                case 1:  return new Monk();
                case 2:  return new Thief();
                case 3:  return new Archer();
                case 4:  return new Mage();
                case 5:  return new Priest();
                default: return new Knight();
            }
        }
    }
}
