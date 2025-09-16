using PitHero.RolePlayingSystem.BattleActors.Characters;

namespace PitHero.RolePlayingSystem.Jobs
{
    public abstract class Vocation : IStatContainer
    {
        public abstract int GetStrength();
        public abstract int GetAgility();
        public abstract int GetVitality();
        public abstract int GetMagic();
        public abstract string GetDisplayName();
    }
}
