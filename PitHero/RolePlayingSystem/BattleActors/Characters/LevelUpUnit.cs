namespace PitHero.RolePlayingSystem.BattleActors.Characters
{
    public class LevelUpUnit
    {
        public int Experience;
        public int BaseHP;
        public int BaseMP;

        public LevelUpUnit(int experience, int baseHP, int baseMP)
        {
            Experience = experience;
            BaseHP = baseHP;
            BaseMP = baseMP;
        }
    }
}
