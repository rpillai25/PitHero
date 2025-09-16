using System;

namespace PitHero.RolePlayingSystem.GameData
{
    [Flags]
    public enum AttackCategory
    {
        None = 0,
        Physical = 1 << 0,
        Aerial = 1 << 1,
        Black = 1 << 2,
        White = 1 << 3,
        Song = 1 << 4
    }

    [Flags]
    public enum Element
    {
        None = 0,
        Fire = 1 << 0,
        Ice = 1 << 1,
        Air = 1 << 2,
        Earth = 1 << 3,
        Lightning = 1 << 4,
        Holy = 1 << 5,
        Poison = 1 << 6
    }

    [Flags]
    public enum EnemyType
    {
        None = 0,
        Creature = 1 << 0,
        Heavy = 1 << 1,
        Human = 1 << 2,
        Undead = 1 << 3,
        Dragon = 1 << 4,
        Desert = 1 << 5,
        Avis = 1 << 6
    }

    [Flags]
    public enum WeaponSpecial
    {
        None = 0,
        DoubleGripEnabled = 1 << 0,
        DoubleGripOnly = 1 << 1,
        MagicSwordEnabled = 1 << 2,
        WeaponBlock = 1 << 3,
        DoubleJump = 1 << 4,
        Initiative = 1 << 5
    }

    [Flags]
    public enum ArmorSpecial
    {
        None = 0,
        EvadeMagic = 1 << 0,
        EvadePhysical = 1 << 1,
        CatchUp = 1 << 2,
        SwordDanceUp = 1 << 3,
        HalfMP = 1 << 4,
        StealUp = 1 << 5,
        BrawlUp = 1 << 6,
        ControlRateUp = 1 << 7
    }

    public enum ArmorType
    {
        Shield,
        Headgear,
        Bodywear,
        Accessory
    }

    [Flags]
    public enum EnemySpecialtyEffect
    {
        None = 0,
        Damage1_5x = 1 << 0,
        HitPercent100 = 1 << 1,
        PierceDefence = 1 << 2,
        AddPoison = 1 << 3,
        AddAging = 1 << 4,
        AddCharm = 1 << 5,
        AddParalyze = 1 << 6,
        AddBlind = 1 << 7,
        AddHPLeak = 1 << 8,

    }

    public enum TargettingType
    {
        None,
        Self,
        SingleTargetDefaultAlly,
        SingleTargetDefaultEnemy,
        SingleMultipleTargetDefaultAlly,
        SingleMultipleTargetDefaultEnemy,
        AllAllies,
        AllEnemies,
        SingleTargetOnlyEnemies,
        SingleTargetOnlyAllies
    }

    [Flags]
    public enum VocationType
    {
        None = 0,
        Knight = 1 << 0,
        Mage = 1 << 1,
        Monk = 1 << 2,
        Priest = 1 << 3,
        All = Knight | Mage | Monk | Priest
    }

    [Flags]
    public enum StatusEffect
    {
        None = 0,
        NearDeath = 1 << 0,
        Darkness = 1 << 1,
        Zombie = 1 << 2,
        Poison = 1 << 3,
        Float = 1 << 4,
        Mini = 1 << 5,
        Toad = 1 << 6,
        Stone = 1 << 7,
        Dead = 1 << 8,
        Image = 1 << 9,
        Mute = 1 << 10,
        Berserk = 1 << 11,
        Charm = 1 << 12,
        Paralyze = 1 << 13,
        Sleep = 1 << 14,
        Aging = 1 << 15,
        Regen = 1 << 16,
        Invulnerable = 1 << 17,
        Slow = 1 << 18,
        Haste = 1 << 19,
        Stop = 1 << 20,
        Shell = 1 << 21,
        Armor = 1 << 22,
        Wall = 1 << 23,
        Hidden = 1 << 24,
        Singing = 1 << 25,
        HPLeak = 1 << 26,
        Countdown = 1 << 27,
        Controlled = 1 << 28,
        FalseImage = 1 << 29,
        Erased = 1 <<30
    }

    public enum AttackType
    {
        None = 0x0,
        //Weapons
        Drain = 0x0D,
        Fist = 0x30,
        Sword = 0x31,
        Knife = 0x32,
        Spear = 0x33,
        Axe = 0x34,
        BowWithStatus = 0x35,
        BowWithElement = 0x36,
        Katana = 0x37,
        Whip = 0x38,
        Bell = 0x39,
        LongAxe = 0x3A,
        Rod = 0x3B,
        RunicWeapon = 0x3C,
        Charm = 0x49,
        RunAwayWeapon = 0x64,
        SpecialStrongVsCreature = 0x6C,
        BraveWeapon = 0x6E,
        BowStrongVsCreature = 0x72,
        SpearStrongVsCreature = 0x73,
        NoAction = 0x7F,
        //Monster
        MonsterFight = 0x01,
        MonsterSpecial = 0x02,
        MonsterStrongFight = 0x6F,
        //Magic
        Magic = 0x06,
        Gravity = 0x07,
        PierceMagicDefense = 0x08,
        RandomDamage = 0x09,
        PhysicalAttackMagic = 0x0A,
        DamageBasedOnLevel = 0x0B,
        HPLeak = 0x0C,
        Psyche = 0x0E,
        ReduceHPToCritical = 0x0F,
        Heal = 0x10,
        FullHeal = 0x11,
        StatusEffect1 = 0x12,
        StatusEffect2 = 0x13,
        StatusEffect3 = 0x14,
        ToggleStatus = 0x15,
        MutuallyExclusiveStatus = 0x16,
        FullHealToUndead = 0x17,
        Destroy = 0x18,
        HealStatus = 0x19,
        ReviveWithPartialLife = 0x1A,
        AllDrain = 0x1B,
        ResistElement = 0x1C,
        ScanEnemy = 0x1D,
        SpeedUpCaster = 0x1E,
        NullMagic = 0x1F,
        ExitBattle = 0x20,
        ResetBattle = 0x21,
        DoubleCommands = 0x22,
        DamageWall = 0x23,
        HealHPItem = 0x24,
        HealMPItem = 0x25,
        HealHPMPItem = 0x26,
        ForceInflictStatus = 0x27,
        IgnoreDefence = 0x28,
        Countdown = 0x29,
        MaxHPDamage = 0x2A,
        CasterCurrentHPDamage = 0x2B,
        FiftyFiftyStatus = 0x2C,
        GroundAttack = 0x2D,
        PhysicalMagicStatus = 0x2E,
        ReduceHPCriticalStatus = 0x3D,
        ReduceHPCriticalLeak = 0x3E,
        ZombieBreath = 0x3F,
        HealHPStatus = 0x42,
        EnemyEscape = 0x44,
        L5Doom = 0x4B,
        L2Old = 0x4C,
        L4Quarter = 0x4D,
        L3Flare = 0x4E,
        ReviveAndStatus = 0x4F,
        GoblinPunch = 0x50,
        ModifyLevelOrDefense = 0x51,
        HPLeakAndStatusMucus = 0x52,
        CurrentMPBasedDamage = 0x53,
        HPDiffDamage = 0x54,
        SacrificeHeal = 0x55,
        HPLeakAndStatusDark = 0x57,
        FlareAndHPLeakAndStatus = 0x58,
        DoubleHP = 0x59,
        DamageHealCurrentHP = 0x5A,
        FullHealAndStatus = 0x5C,
        ZombieDance = 0x5D,
        IncreaseStat = 0x5E,
        DamageCreatureType = 0x5F,
        GrandCross = 0x63,
        InterceptorRocket = 0x65,
        Pull = 0x67,
        StatusGranter = 0x6B,
        DrainBaseOnHP = 0x6D,
        Wormhole = 0x70,
        //Command Attack
        Steal = 0x43,
        Throw = 0x45,
        GoldToss = 0x46,
        Tame = 0x47,
        Catch = 0x48,
        Dance = 0x4A,
        Control = 0x69
    }

    public enum Commands
    {
        //Base
        Fight,
        Defend,
        Run,
        Item
    }
}
