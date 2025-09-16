using PitHero.RolePlayingSystem.Jobs;
using Nez;

namespace PitHero.RolePlayingSystem.BattleActors.Characters
{
    public class Character : BattleActorData
    {
        //These are stat bonuses on top of vocation stats
        public int BaseStrength;
        public int BaseAgility;
        public int BaseVitality;
        public int BaseMagicPower;

        public Vocation Vocation;
        public int CurrentHP;
        public int CurrentMP;

        /// <summary>
        /// Status effects that persist after battle (Flags)
        /// </summary>
        public int PersistentStatusEffects;

        public Character(string name, Vocation vocation)
        {
            Name = name;

            //Initialize random stat bonuses (+0 to 4 each)
            BaseStrength = Mathf.FastFloorToInt(Random.NextInt(4999) / 1000);
            BaseAgility = Mathf.FastFloorToInt(Random.NextInt(4999) / 1000);
            BaseVitality = Mathf.FastFloorToInt(Random.NextInt(4999) / 1000);
            BaseMagicPower = Mathf.FastFloorToInt(Random.NextInt(4999) / 1000);

            Level = 1;
            Vocation = vocation;
        }

        /// <summary>
        /// Add given experience and return true if Leveled up.
        /// Also apply level up stat updates.
        /// </summary>
        /// <param name="experience">Experience to add to character</param>
        /// <returns>true if a LevelUp happened</returns>
        public bool AddExperience(int experience)
        {
            Experience += experience;
            if (Experience > ExperienceTable.MaxExp)
            {
                //If we are in this situation, no more level ups are possible
                Experience = ExperienceTable.MaxExp;
                return false;
            }
            if (Experience >= ExperienceTable.GetLevelUpUnit(Level).Experience)
            {
                BaseHP = ExperienceTable.GetLevelUpUnit(Level).BaseHP;
                BaseMP = ExperienceTable.GetLevelUpUnit(Level).BaseMP;
                Level++;

                return true;
            }
            return false;
        }

        public int Strength
        {
            get
            {
                return BaseStrength + Vocation.GetStrength();
            }
        }

        public int Agility
        {
            get
            {
                return BaseAgility + Vocation.GetAgility();
            }
        }

        public int Vitality
        {
            get
            {
                return BaseVitality + Vocation.GetVitality();
            }
        }

        public int MagicPower
        {
            get
            {
                return BaseMagicPower + Vocation.GetMagic();
            }
        }
    }
}
