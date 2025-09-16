using PitHero.RolePlayingSystem.GameData;

namespace PitHero.RolePlayingSystem.Abilities.Enemy
{
    public class OozeWhip : Ability
    {
       public OozeWhip()
        {
            HitPercent = -1;
            TargettingType = TargettingType.SingleTargetDefaultEnemy;
        }
    }
}
