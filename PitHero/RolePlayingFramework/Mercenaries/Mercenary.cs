using RolePlayingFramework.Combat;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Jobs;
using RolePlayingFramework.Skills;
using RolePlayingFramework.Stats;
using System.Collections.Generic;

namespace RolePlayingFramework.Mercenaries
{
    /// <summary>Runtime mercenary instance with equipment, skills and derived stats (no crystal or synergies).</summary>
    public sealed class Mercenary
    {
        public string Name { get; }
        public IJob Job { get; }
        public int Level { get; private set; }
        public int Experience { get; internal set; }
        public StatBlock BaseStats { get; private set; }

        public int MaxHP { get; private set; }
        public int MaxMP { get; private set; }
        public int CurrentHP { get; private set; }
        public int CurrentMP { get; private set; }

        public int PassiveDefenseBonus { get; set; }
        public float DeflectChance { get; set; }
        public bool EnableCounter { get; set; }
        public int MPTickRegen { get; set; }
        public float HealPowerBonus { get; set; }
        public float FireDamageBonus { get; set; }
        public float MPCostReduction { get; set; }

        public IGear? WeaponShield1 { get; private set; }
        public IGear? Armor { get; private set; }
        public IGear? Hat { get; private set; }
        public IGear? WeaponShield2 { get; private set; }
        public IGear? Accessory1 { get; private set; }
        public IGear? Accessory2 { get; private set; }

        private readonly Dictionary<string, ISkill> _learnedSkills = new Dictionary<string, ISkill>(16);

        /// <summary>Skills learned by this mercenary, keyed by skill Id.</summary>
        public IReadOnlyDictionary<string, ISkill> LearnedSkills => _learnedSkills;

        public Mercenary(string name, IJob job, int level, in StatBlock baseStats)
        {
            Name = name;
            Job = job;
            Level = level < 1 ? 1 : level;
            BaseStats = baseStats;
            
            RecalculateDerived();
            CurrentHP = MaxHP;
            CurrentMP = MaxMP;
        }

        /// <summary>Adds experience and levels up, growing stats each level.</summary>
        public bool AddExperience(int amount)
        {
            if (amount <= 0) return false;
            Experience += amount;
            var leveled = false;
            while (Experience >= RequiredExpForNextLevel())
            {
                Experience -= RequiredExpForNextLevel();

                if (Level >= StatConstants.MaxLevel) break;

                Level++;
                leveled = true;

                BaseStats = StatConstants.ClampStatBlock(
                    new StatBlock(
                        BaseStats.Strength + 1,
                        BaseStats.Agility + 1,
                        BaseStats.Vitality + 1,
                        BaseStats.Magic + 1
                    )
                );

                RecalculateDerived();
            }
            return leveled;
        }

        /// <summary>Gets required XP for the next level.</summary>
        public int RequiredExpForNextLevel() => Level * 100;

        /// <summary>Equips an item in the appropriate slot.</summary>
        public bool Equip(IGear item)
        {
            if (item == null) return false;

            switch (item.Kind)
            {
                case ItemKind.WeaponSword:
                case ItemKind.WeaponKnife:
                case ItemKind.WeaponKnuckle:
                case ItemKind.WeaponStaff:
                case ItemKind.WeaponRod:
                case ItemKind.WeaponHammer:
                case ItemKind.Shield:
                    if (WeaponShield1 == null)
                        WeaponShield1 = item;
                    else if (WeaponShield2 == null)
                        WeaponShield2 = item;
                    else
                        return false;
                    break;
                case ItemKind.ArmorMail:
                case ItemKind.ArmorGi:
                case ItemKind.ArmorRobe:
                    if (Armor != null) return false;
                    Armor = item;
                    break;
                case ItemKind.HatHelm:
                case ItemKind.HatHeadband:
                case ItemKind.HatWizard:
                case ItemKind.HatPriest:
                    if (Hat != null) return false;
                    Hat = item;
                    break;
                case ItemKind.Accessory:
                    if (Accessory1 == null)
                        Accessory1 = item;
                    else if (Accessory2 == null)
                        Accessory2 = item;
                    else
                        return false;
                    break;
                default:
                    return false;
            }

            RecalculateDerived();
            return true;
        }

        /// <summary>Unequips an item from the specified slot.</summary>
        public IGear? Unequip(EquipmentSlot slot)
        {
            IGear? removed = null;
            switch (slot)
            {
                case EquipmentSlot.WeaponShield1:
                    removed = WeaponShield1;
                    WeaponShield1 = null;
                    break;
                case EquipmentSlot.Armor:
                    removed = Armor;
                    Armor = null;
                    break;
                case EquipmentSlot.Hat:
                    removed = Hat;
                    Hat = null;
                    break;
                case EquipmentSlot.WeaponShield2:
                    removed = WeaponShield2;
                    WeaponShield2 = null;
                    break;
                case EquipmentSlot.Accessory1:
                    removed = Accessory1;
                    Accessory1 = null;
                    break;
                case EquipmentSlot.Accessory2:
                    removed = Accessory2;
                    Accessory2 = null;
                    break;
            }

            if (removed != null)
                RecalculateDerived();

            return removed;
        }

        /// <summary>Gets total stats (base + job + equipment) clamped to maximums.</summary>
        public StatBlock GetTotalStats()
        {
            var jobStats = Job.GetJobContributionAtLevel(Level);
            var total = BaseStats.Add(jobStats);

            if (WeaponShield1 != null) total = total.Add(WeaponShield1.StatBonus);
            if (Armor != null) total = total.Add(Armor.StatBonus);
            if (Hat != null) total = total.Add(Hat.StatBonus);
            if (WeaponShield2 != null) total = total.Add(WeaponShield2.StatBonus);
            if (Accessory1 != null) total = total.Add(Accessory1.StatBonus);
            if (Accessory2 != null) total = total.Add(Accessory2.StatBonus);

            return StatConstants.ClampStatBlock(total);
        }

        /// <summary>Recalculates derived stats from base + job + equipment.</summary>
        private void RecalculateDerived()
        {
            var total = GetTotalStats();

            int baseHP = 25 + (total.Vitality * 5);
            int baseMP = 10 + (total.Magic * 3);

            MaxHP = StatConstants.ClampHP(baseHP);
            MaxMP = StatConstants.ClampMP(baseMP);

            if (CurrentHP > MaxHP) CurrentHP = MaxHP;
            if (CurrentMP > MaxMP) CurrentMP = MaxMP;
        }

        /// <summary>Computes battle stats for combat calculations.</summary>
        public BattleStats GetBattleStats()
        {
            var effectiveStats = GetTotalStats();

            int atk = effectiveStats.Strength;
            if (WeaponShield1 != null) atk += WeaponShield1.AttackBonus;
            if (WeaponShield2 != null) atk += WeaponShield2.AttackBonus;

            int def = effectiveStats.Vitality + PassiveDefenseBonus;
            if (Armor != null) def += Armor.DefenseBonus;
            if (Hat != null) def += Hat.DefenseBonus;
            if (WeaponShield1 != null) def += WeaponShield1.DefenseBonus;
            if (WeaponShield2 != null) def += WeaponShield2.DefenseBonus;

            int evasion = RolePlayingFramework.Balance.BalanceConfig.CalculateEvasion(effectiveStats.Agility, Level);

            return new BattleStats(atk, def, evasion);
        }

        /// <summary>Takes damage and returns true if mercenary died.</summary>
        public bool TakeDamage(int amount)
        {
            if (amount <= 0) return false;
            CurrentHP -= amount;
            if (CurrentHP < 0) CurrentHP = 0;
            return CurrentHP == 0;
        }

        /// <summary>Restores HP up to max. Returns true if HP was actually restored.</summary>
        public bool RestoreHP(int amount)
        {
            if (amount <= 0) return false;
            if (CurrentHP >= MaxHP) return false; // Already at max HP
            CurrentHP += amount;
            if (CurrentHP > MaxHP) CurrentHP = MaxHP;
            return true;
        }

        /// <summary>Restores MP for the specified amount.</summary>
        public void RestoreMP(int amount)
        {
            if (amount <= 0) return;
            CurrentMP += amount;
            if (CurrentMP > MaxMP) CurrentMP = MaxMP;
        }

        /// <summary>Uses MP for skill casting. Returns true if successful.</summary>
        public bool UseMP(int amount)
        {
            if (amount <= 0) return true;
            if (CurrentMP < amount) return false;
            CurrentMP -= amount;
            return true;
        }

        /// <summary>Teaches the mercenary a skill. Returns true if learned successfully.</summary>
        public bool LearnSkill(ISkill skill)
        {
            if (skill == null || _learnedSkills.ContainsKey(skill.Id))
                return false;
            _learnedSkills[skill.Id] = skill;
            return true;
        }

        /// <summary>Removes a learned skill by Id. Returns true if removed.</summary>
        public bool ForgetSkill(string skillId)
        {
            if (skillId == null) return false;
            return _learnedSkills.Remove(skillId);
        }

        /// <summary>Learns all skills from the mercenary's job and applies passive effects.</summary>
        public void LearnAllJobSkills()
        {
            var skills = Job.Skills;
            for (int i = 0; i < skills.Count; i++)
            {
                LearnSkill(skills[i]);
            }
            ApplyPassiveSkills();
        }

        /// <summary>Applies passive skill effects to mercenary properties.</summary>
        private void ApplyPassiveSkills()
        {
            PassiveDefenseBonus = 0;
            DeflectChance = 0f;
            EnableCounter = false;
            MPTickRegen = 0;
            HealPowerBonus = 0f;
            FireDamageBonus = 0f;
            MPCostReduction = 0f;

            var enumerator = _learnedSkills.GetEnumerator();
            while (enumerator.MoveNext())
            {
                var skill = enumerator.Current.Value;
                if (skill.Kind != SkillKind.Passive) continue;

                switch (skill.Id)
                {
                    case "knight.heavy_armor":
                        PassiveDefenseBonus += 2;
                        break;
                    case "mage.heart_fire":
                        FireDamageBonus += 0.25f;
                        break;
                    case "mage.economist":
                        MPCostReduction += 0.15f;
                        break;
                    case "priest.calm_spirit":
                        MPTickRegen += 1;
                        break;
                    case "priest.mender":
                        HealPowerBonus += 0.25f;
                        break;
                    case "monk.counter":
                        EnableCounter = true;
                        break;
                    case "monk.deflect":
                        DeflectChance = 0.15f;
                        break;
                }
            }
        }
    }
}
