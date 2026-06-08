namespace RolePlayingFramework.AlliedMonsters
{
    /// <summary>A monster that has joined the party after being defeated in battle.</summary>
    public sealed class AlliedMonster
    {
        /// <summary>Random first name assigned upon joining.</summary>
        public string Name { get; }

        /// <summary>The original monster type name (e.g. "Slime").</summary>
        public string MonsterTypeName { get; }

        /// <summary>Fishing proficiency rating, 1–9.</summary>
        public int FishingProficiency { get; }

        /// <summary>Cooking proficiency rating, 1–9.</summary>
        public int CookingProficiency { get; }

        /// <summary>Farming proficiency rating, 1–9.</summary>
        public int FarmingProficiency { get; }

        /// <summary>Current job assignment for this monster.</summary>
        public MonsterJob Job { get; set; } = MonsterJob.None;

        /// <summary>UniqueId of the monster house this monster is assigned to (-1 if unassigned).</summary>
        public int MonsterHouseId { get; set; } = -1;

        /// <summary>Creates a new AlliedMonster with the given name, type, and proficiencies.</summary>
        public AlliedMonster(string name, string monsterTypeName, int fishing, int cooking, int farming,
            int monsterHouseId = -1)
        {
            Name = name;
            MonsterTypeName = monsterTypeName;
            FishingProficiency = System.Math.Clamp(fishing, 1, 9);
            CookingProficiency = System.Math.Clamp(cooking, 1, 9);
            FarmingProficiency = System.Math.Clamp(farming, 1, 9);
            MonsterHouseId = monsterHouseId;
        }
    }
}
