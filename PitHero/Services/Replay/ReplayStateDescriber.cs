using System.Text;
using Nez;
using PitHero.AI;
using PitHero.ECS.Components;

namespace PitHero.Services.Replay
{
    /// <summary>
    /// Human-readable dump of the simulation pieces the tripwire hashes plus the transient AI state
    /// around them (current GOAP action, enemy wander timers, mercenary FSM states). Written into
    /// replay_divergence.log for the last matching sample and the divergent one, so the two can be
    /// diffed to see what the replay did differently in that one-second window.
    /// </summary>
    public static class ReplayStateDescriber
    {
        private const int EnemyRadiusTiles = 10;

        public static string Describe()
        {
            var sb = new StringBuilder(1024);
            try
            {
                if (Core.Instance == null || Core.Scene == null || Core.Services == null)
                    return "    (no scene)";

                var heroEntity = Core.Scene.FindEntity("hero");
                var heroComp = heroEntity?.GetComponent<HeroComponent>();
                var hero = heroComp?.LinkedHero;
                var heroTile = heroComp != null ? heroComp.GetCurrentTilePosition() : new Microsoft.Xna.Framework.Point(-1, -1);
                if (hero != null)
                {
                    var fsm = heroEntity.GetComponent<HeroStateMachine>();
                    var mover = heroEntity.GetComponent<TileByTileMover>();
                    sb.Append("    hero: tile=").Append(heroTile.X).Append(',').Append(heroTile.Y)
                      .Append(" hp=").Append(hero.CurrentHP).Append('/').Append(hero.MaxHP)
                      .Append(" mp=").Append(hero.CurrentMP)
                      .Append(" lvl=").Append(hero.Level).Append(" xp=").Append(hero.Experience)
                      .Append(" insidePit=").Append(heroComp.InsidePit).Append(" stopped=").Append(heroComp.StoppedAdventure)
                      .Append(" moving=").Append(mover?.IsMoving ?? false)
                      .Append(" battle=").Append(HeroStateMachine.IsBattleInProgress)
                      .Append(" threat=").Append(HeroStateMachine.CurrentThreatTarget?.Name ?? "-")
                      .Append(" bag=").Append(heroComp.Bag?.Count ?? -1)
                      .Append(" deflect=").Append(hero.DeflectChance.ToString("0.00"))
                      .Append(" synergies=[");
                    var groups = hero.ActiveSynergyGroups;
                    for (int g = 0; g < groups.Count; g++)
                    {
                        if (g > 0) sb.Append(',');
                        sb.Append(groups[g].Pattern.Id).Append('x').Append(groups[g].InstanceCount);
                    }
                    sb.Append(']').AppendLine();
                    sb.Append("    hero gear: w1=").Append(hero.WeaponShield1?.Name ?? "-")
                      .Append(" w2=").Append(hero.WeaponShield2?.Name ?? "-")
                      .Append(" armor=").Append(hero.Armor?.Name ?? "-")
                      .Append(" hat=").Append(hero.Hat?.Name ?? "-")
                      .Append(" acc1=").Append(hero.Accessory1?.Name ?? "-")
                      .Append(" acc2=").Append(hero.Accessory2?.Name ?? "-")
                      .AppendLine();
                    sb.Append("    hero ai: ").AppendLine(fsm != null ? fsm.DescribeForDiagnostics() : "(no fsm)");
                }
                else
                {
                    sb.AppendLine("    hero: (none)");
                }

                var mercManager = Core.Services.GetService<MercenaryManager>();
                var hired = mercManager?.GetHiredMercenaries();
                if (hired != null)
                {
                    for (int i = 0; i < hired.Count; i++)
                    {
                        var e = hired[i];
                        var mc = e.GetComponent<MercenaryComponent>();
                        var m = mc?.LinkedMercenary;
                        if (m == null) continue;
                        var mover = e.GetComponent<TileByTileMover>();
                        var tile = mover != null ? mover.GetCurrentTileCoordinates() : new Microsoft.Xna.Framework.Point(-1, -1);
                        var msm = e.GetComponent<MercenaryStateMachine>();
                        sb.Append("    merc ").Append(m.Name).Append(": tile=").Append(tile.X).Append(',').Append(tile.Y)
                          .Append(" hp=").Append(m.CurrentHP).Append('/').Append(m.MaxHP).Append(" lvl=").Append(m.Level)
                          .Append(" insidePit=").Append(mc.InsidePit)
                          .Append(" moving=").Append(mover?.IsMoving ?? false)
                          .Append(" fsm=").Append(msm != null ? msm.CurrentState.ToString() : "-")
                          .AppendLine();
                    }
                }

                var monsters = Core.Scene.FindEntitiesWithTag(GameConfig.TAG_MONSTER);
                int living = 0, listed = 0;
                for (int i = 0; i < monsters.Count; i++)
                {
                    var e = monsters[i];
                    var ec = e.GetComponent<EnemyComponent>();
                    var enemy = ec?.Enemy;
                    if (enemy == null) continue;
                    if (enemy.CurrentHP > 0) living++;
                    var mover = e.GetComponent<TileByTileMover>();
                    var tile = mover != null ? mover.GetCurrentTileCoordinates() : new Microsoft.Xna.Framework.Point(-1, -1);
                    int dx = tile.X - heroTile.X, dy = tile.Y - heroTile.Y;
                    if (dx < -EnemyRadiusTiles || dx > EnemyRadiusTiles || dy < -EnemyRadiusTiles || dy > EnemyRadiusTiles)
                        continue;
                    listed++;
                    sb.Append("    enemy ").Append(enemy.Name).Append(": tile=").Append(tile.X).Append(',').Append(tile.Y)
                      .Append(" hp=").Append(enemy.CurrentHP).Append('/').Append(enemy.MaxHP).Append(" lvl=").Append(enemy.Level)
                      .Append(" moving=").Append(ec.IsMoving).Append(" stationary=").Append(ec.IsStationary)
                      .Append(" move=").Append(ec.MoveCounter).Append('/').Append(ec.MoveCooldown)
                      .Append(" boss=").Append(enemy.IsBoss)
                      .AppendLine();
                }
                sb.Append("    monsters: living=").Append(living).Append(" total=").Append(monsters.Count)
                  .Append(" listedWithin=").Append(EnemyRadiusTiles).Append("tiles:").Append(listed).AppendLine();
                sb.Append("    engine: cosmeticsSuspended=").Append(Core.CosmeticUpdatesSuspended)
                  .Append(" quietLog=").Append(Debug.QuietMode)
                  .Append(" paused=").Append(Core.Services.GetService<PauseService>()?.IsPaused ?? false)
                  .AppendLine();
                sb.AppendLine("    battle trace (tick event), oldest first:");
                sb.Append(ReplayBattleTrace.Dump());
            }
            catch (System.Exception ex)
            {
                sb.Append("    (describe failed: ").Append(ex.Message).AppendLine(")");
            }
            return sb.ToString();
        }
    }
}
