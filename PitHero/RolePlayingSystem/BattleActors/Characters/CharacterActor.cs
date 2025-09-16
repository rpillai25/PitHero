namespace PitHero.RolePlayingSystem.BattleActors.Characters
{
    public class CharacterActor : BattleActor
    {
        public int BattleMaxHP;
        public int BattleCurrentHP;
        public int BattleMaxMP;
        public int BattleCurrentMP;

        public int BattleStrength;
        public int BattleAgility;
        public int BattleVitality;
        public int BattleMagicPower;


        public CharacterActor(Character character)
        {
            Init(character);
        }

        public void Init(Character character)
        {
            BattleActorData = character;

            BattleMaxHP = character.BaseHP;
            BattleCurrentHP = character.CurrentHP;
            BattleMaxMP = character.BaseMP;
            BattleCurrentMP = character.CurrentMP;

            BattleStrength = character.Strength;
            BattleAgility = character.Agility;
            BattleVitality = character.Vitality;
            BattleMagicPower = character.MagicPower;
        }
    }
}
