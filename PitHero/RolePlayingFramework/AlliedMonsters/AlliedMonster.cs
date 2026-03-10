namespace RolePlayingFramework.AlliedMonsters
{
    /// <summary>A monster that has joined the party after being defeated in battle.</summary>
    public sealed class AlliedMonster
    {
        /// <summary>Random first name assigned upon joining.</summary>
        public string Name { get; }

        /// <summary>The original monster type name (e.g. "Slime").</summary>
        public string MonsterTypeName { get; }

        /// <summary>Battle proficiency rating, 1–9.</summary>
        public int BattleProficiency { get; }

        /// <summary>Cooking proficiency rating, 1–9.</summary>
        public int CookingProficiency { get; }

        /// <summary>Farming proficiency rating, 1–9.</summary>
        public int FarmingProficiency { get; }

        /// <summary>Creates a new AlliedMonster with the given name, type, and proficiencies.</summary>
        public AlliedMonster(string name, string monsterTypeName, int battle, int cooking, int farming)
        {
            Name = name;
            MonsterTypeName = monsterTypeName;
            BattleProficiency  = System.Math.Clamp(battle,  1, 9);
            CookingProficiency = System.Math.Clamp(cooking, 1, 9);
            FarmingProficiency = System.Math.Clamp(farming, 1, 9);
        }
    }
}
