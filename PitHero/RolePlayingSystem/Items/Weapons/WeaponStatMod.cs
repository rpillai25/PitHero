using PitHero.RolePlayingSystem.BattleActors.Characters;

namespace PitHero.RolePlayingSystem.Items.Weapons
{
    public class WeaponStatMod : IStatContainer
    {
        int strengthMod;
        int agilityMod;
        int vitalityMod;
        int magicMod;

        public WeaponStatMod(int strength, int agility, int vitality, int magic)
        {
            agilityMod = agility;
            magicMod = magic;
            strengthMod = strength;
            vitalityMod = vitality;
        }

        public int GetStrength()
        {
            return strengthMod;
        }

        public int GetAgility()
        {
            return agilityMod;
        }
        public int GetVitality()
        {
            return vitalityMod;
        }
        public int GetMagic()
        {
            return magicMod;
        }
    }
}
