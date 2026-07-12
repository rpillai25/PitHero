using System;

namespace RolePlayingFramework.Enemies
{
    /// <summary>
    /// Central factory turning an <see cref="EnemyId"/> into a live <see cref="IEnemy"/> instance.
    /// Single source of truth for the id → concrete-type mapping, shared by pit generation and the
    /// "Add Monsters" flow (issue #283).
    /// </summary>
    public static class EnemyFactory
    {
        /// <summary>
        /// Creates an enemy instance for the given id.
        /// When <paramref name="level"/> is 0 or negative the constructor falls back to its
        /// configured preset level.  Positive values are clamped to 1–99 and honoured directly.
        /// </summary>
        public static IEnemy Create(EnemyId enemyId, int level = 0)
        {
            // Pass 0 through unchanged so constructors can detect "no level specified" and
            // fall back to their preset.  Only clamp positive requests.
            int levelToPass = level > 0 ? Math.Clamp(level, 1, 99) : 0;

            return enemyId switch
            {
                EnemyId.Slime => new Slime(levelToPass),
                EnemyId.Bat => new Bat(levelToPass),
                EnemyId.Rat => new Rat(levelToPass),
                EnemyId.CaveMushroom => new CaveMushroom(levelToPass),
                EnemyId.StoneBeetle => new StoneBeetle(levelToPass),
                EnemyId.Goblin => new Goblin(levelToPass),
                EnemyId.Spider => new Spider(levelToPass),
                EnemyId.Snake => new Snake(levelToPass),
                EnemyId.ShadowImp => new ShadowImp(levelToPass),
                EnemyId.TunnelWorm => new TunnelWorm(levelToPass),
                EnemyId.FireLizard => new FireLizard(levelToPass),
                EnemyId.Skeleton => new Skeleton(levelToPass),
                EnemyId.Orc => new Orc(levelToPass),
                EnemyId.Wraith => new Wraith(levelToPass),
                EnemyId.MagmaOoze => new MagmaOoze(levelToPass),
                EnemyId.CrystalGolem => new CrystalGolem(levelToPass),
                EnemyId.CaveTroll => new CaveTroll(levelToPass),
                EnemyId.GhostMiner => new GhostMiner(levelToPass),
                EnemyId.ShadowBeast => new ShadowBeast(levelToPass),
                EnemyId.LavaDrake => new LavaDrake(levelToPass),
                EnemyId.StoneWyrm => new StoneWyrm(levelToPass),
                EnemyId.StoneGuardian => new StoneGuardian(levelToPass),
                EnemyId.EarthElemental => new EarthElemental(levelToPass),
                EnemyId.MoltenTitan => new MoltenTitan(levelToPass),
                EnemyId.AncientWyrm => new AncientWyrm(levelToPass),
                EnemyId.PitLord => new PitLord(levelToPass),
                _ => new Slime(levelToPass)
            };
        }
    }
}
