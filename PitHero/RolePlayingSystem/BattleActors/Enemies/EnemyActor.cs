namespace PitHero.RolePlayingSystem.BattleActors.Enemies
{
    public class EnemyActor : BattleActor
    {
        public int BattleMaxHP;
        public int BattleCurrentHP;
        public int BattleMaxMP;
        public int BattleCurrentMP;

        public int BattleSpeed;
        public int BattleDefense;
        public int BattleAttackPower;

        public EnemyActor(Enemy enemy)
        {
            BattleActorData = enemy;

            BattleMaxHP = enemy.BaseHP;
            BattleCurrentHP = enemy.BaseHP;
            BattleMaxMP = enemy.BaseMP;
            BattleCurrentMP = enemy.BaseMP;

            BattleSpeed = enemy.Speed;
            BattleDefense = enemy.Defense;
            BattleAttackPower = enemy.AttackPower;
        }
    }
}
