namespace PitHero.RolePlayingSystem.Jobs
{
    public class Knight : Vocation
    {
        public override int GetStrength()
        {
            return 47;
        }
        public override int GetAgility()
        {
            return 25;
        }
        public override int GetVitality()
        {
            return 44;
        }
        public override int GetMagic()
        {
            return 10;
        }
        public override string GetDisplayName()
        {
            return "Knight";
        }
    }
}
