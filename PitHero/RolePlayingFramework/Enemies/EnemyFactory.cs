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
        /// <summary>Creates an enemy instance for the given id. Level is clamped to 1–99.</summary>
        public static IEnemy Create(EnemyId enemyId, int level = 1)
        {
            int clampedLevel = Math.Clamp(level, 1, 99);

            return enemyId switch
            {
                EnemyId.Slime => new Slime(clampedLevel),
                EnemyId.Bat => new Bat(clampedLevel),
                EnemyId.Rat => new Rat(clampedLevel),
                EnemyId.CaveMushroom => new CaveMushroom(clampedLevel),
                EnemyId.StoneBeetle => new StoneBeetle(clampedLevel),
                EnemyId.Goblin => new Goblin(clampedLevel),
                EnemyId.Spider => new Spider(clampedLevel),
                EnemyId.Snake => new Snake(clampedLevel),
                EnemyId.ShadowImp => new ShadowImp(clampedLevel),
                EnemyId.TunnelWorm => new TunnelWorm(clampedLevel),
                EnemyId.FireLizard => new FireLizard(clampedLevel),
                EnemyId.Skeleton => new Skeleton(clampedLevel),
                EnemyId.Orc => new Orc(clampedLevel),
                EnemyId.Wraith => new Wraith(clampedLevel),
                EnemyId.MagmaOoze => new MagmaOoze(clampedLevel),
                EnemyId.CrystalGolem => new CrystalGolem(clampedLevel),
                EnemyId.CaveTroll => new CaveTroll(clampedLevel),
                EnemyId.GhostMiner => new GhostMiner(clampedLevel),
                EnemyId.ShadowBeast => new ShadowBeast(clampedLevel),
                EnemyId.LavaDrake => new LavaDrake(clampedLevel),
                EnemyId.StoneWyrm => new StoneWyrm(clampedLevel),
                EnemyId.StoneGuardian => new StoneGuardian(clampedLevel),
                EnemyId.EarthElemental => new EarthElemental(clampedLevel),
                EnemyId.MoltenTitan => new MoltenTitan(clampedLevel),
                EnemyId.AncientWyrm => new AncientWyrm(clampedLevel),
                EnemyId.PitLord => new PitLord(clampedLevel),
                _ => new Slime(clampedLevel)
            };
        }
    }
}
