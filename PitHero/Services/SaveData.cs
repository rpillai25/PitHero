using Microsoft.Xna.Framework;
using Nez.Persistence.Binary;
using System.Collections.Generic;

namespace PitHero.Services
{
    /// <summary>Lightweight struct representing a saved inventory item.</summary>
    public struct SavedItem
    {
        public string Name;
        public bool IsConsumable;
        public int StackCount;
        public int SlotIndex;
    }

    /// <summary>Lightweight struct representing a saved shortcut bar slot.</summary>
    public struct SavedShortcutSlot
    {
        /// <summary>0 = Empty, 1 = Item, 2 = Skill.</summary>
        public int SlotType;
        /// <summary>Bag index of the referenced item (only valid when SlotType == 1).</summary>
        public int ItemBagIndex;
        /// <summary>Skill ID string (only valid when SlotType == 2).</summary>
        public string SkillId;
    }

    /// <summary>Lightweight struct representing a saved allied monster.</summary>
    public struct SavedAlliedMonster
    {
        public string Name;
        public string MonsterTypeName;
        public int FishingProficiency;
        public int CookingProficiency;
        public int FarmingProficiency;
    }

    /// <summary>Lightweight struct representing a saved hired mercenary.</summary>
    public struct SavedMercenary
    {
        public string Name;
        public string JobName;
        public int Level;
        public int BaseStrength;
        public int BaseAgility;
        public int BaseVitality;
        public int BaseMagic;
        public int CurrentHP;
        public int CurrentMP;
        public string[] EquipmentNames;
        public Color SkinColor;
        public Color HairColor;
        public int HairstyleIndex;
        public Color ShirtColor;
    }

    /// <summary>Central save data container implementing IPersistable for binary persistence.</summary>
    public class SaveData : IPersistable
    {
        /// <summary>Current save file version.</summary>
        public const int CurrentVersion = 4;

        // Total Time
        /// <summary>Total time played in seconds.</summary>
        public float TotalTimePlayed;

        // Hero Design
        /// <summary>The hero's display name.</summary>
        public string HeroName;

        /// <summary>Skin color of the hero.</summary>
        public Color SkinColor;

        /// <summary>Hair color of the hero.</summary>
        public Color HairColor;

        /// <summary>Index of the selected hairstyle.</summary>
        public int HairstyleIndex;

        /// <summary>Shirt color of the hero.</summary>
        public Color ShirtColor;

        // Hero RPG State
        /// <summary>Current job name (e.g. "Knight", "Mage", or "Knight-Mage").</summary>
        public string JobName;

        /// <summary>Current hero level.</summary>
        public int Level;

        /// <summary>Current experience points.</summary>
        public int Experience;

        /// <summary>Base Strength stat.</summary>
        public int BaseStrength;

        /// <summary>Base Agility stat.</summary>
        public int BaseAgility;

        /// <summary>Base Vitality stat.</summary>
        public int BaseVitality;

        /// <summary>Base Magic stat.</summary>
        public int BaseMagic;

        /// <summary>Current hit points.</summary>
        public int CurrentHP;

        /// <summary>Current magic points.</summary>
        public int CurrentMP;

        // Equipment (6 slots: WeaponShield1, Armor, Hat, WeaponShield2, Accessory1, Accessory2)
        /// <summary>Equipment item names by slot index. Null or empty means no item equipped.</summary>
        public string[] EquipmentNames;

        // Inventory
        /// <summary>Saved inventory items.</summary>
        public List<SavedItem> InventoryItems;

        // Hero Crystal
        /// <summary>Whether the hero has a crystal.</summary>
        public bool HasCrystal;

        /// <summary>Crystal job name.</summary>
        public string CrystalJobName;

        /// <summary>Crystal level.</summary>
        public int CrystalLevel;

        /// <summary>Crystal base Strength.</summary>
        public int CrystalBaseStrength;

        /// <summary>Crystal base Agility.</summary>
        public int CrystalBaseAgility;

        /// <summary>Crystal base Vitality.</summary>
        public int CrystalBaseVitality;

        /// <summary>Crystal base Magic.</summary>
        public int CrystalBaseMagic;

        /// <summary>Total job points accumulated.</summary>
        public int TotalJP;

        /// <summary>Current unspent job points.</summary>
        public int CurrentJP;

        /// <summary>Skill IDs learned through the crystal.</summary>
        public List<string> LearnedSkillIds;

        /// <summary>Synergy points keyed by synergy identifier.</summary>
        public Dictionary<string, int> SynergyPoints;

        /// <summary>Synergy skill IDs learned through the crystal.</summary>
        public List<string> LearnedSynergySkillIds;

        /// <summary>Synergy IDs the player has discovered.</summary>
        public List<string> DiscoveredSynergyIds;

        // Game State
        /// <summary>Current gold funds.</summary>
        public int Funds;

        /// <summary>Discovered stencils keyed by stencil name, value is StencilDiscoverySource cast to int.</summary>
        public Dictionary<string, int> DiscoveredStencils;

        // Pit State
        /// <summary>Current pit level.</summary>
        public int PitLevel;

        // Priorities (stored as ints, cast from HeroPitPriority / HeroHealPriority)
        /// <summary>First pit priority.</summary>
        public int Priority1;

        /// <summary>Second pit priority.</summary>
        public int Priority2;

        /// <summary>Third pit priority.</summary>
        public int Priority3;

        /// <summary>First heal priority.</summary>
        public int HealPriority1;

        /// <summary>Second heal priority.</summary>
        public int HealPriority2;

        /// <summary>Third heal priority.</summary>
        public int HealPriority3;

        // Behavior Settings (added in version 4)
        /// <summary>Current battle tactic (cast from BattleTactic enum).</summary>
        public int BattleTacticValue;

        /// <summary>Whether the hero uses consumable items on mercenaries.</summary>
        public bool UseConsumablesOnMercenaries = true;

        /// <summary>Whether mercenaries can use consumable items.</summary>
        public bool MercenariesCanUseConsumables = true;

        // Allied Monsters
        /// <summary>Saved allied monsters.</summary>
        public List<SavedAlliedMonster> AlliedMonsters;

        // Shortcut Bar
        /// <summary>Saved shortcut bar slots (8 slots).</summary>
        public List<SavedShortcutSlot> ShortcutSlots;

        // Hired Mercenaries
        /// <summary>Saved hired mercenaries.</summary>
        public List<SavedMercenary> HiredMercenaries;

        /// <summary>Initializes a new SaveData with default empty collections.</summary>
        public SaveData()
        {
            EquipmentNames = new string[6];
            InventoryItems = new List<SavedItem>();
            LearnedSkillIds = new List<string>();
            SynergyPoints = new Dictionary<string, int>();
            LearnedSynergySkillIds = new List<string>();
            DiscoveredSynergyIds = new List<string>();
            DiscoveredStencils = new Dictionary<string, int>();
            AlliedMonsters = new List<SavedAlliedMonster>();
            ShortcutSlots = new List<SavedShortcutSlot>();
            HiredMercenaries = new List<SavedMercenary>();
        }

        /// <summary>Writes all game state to the persistence writer.</summary>
        void IPersistable.Persist(IPersistableWriter writer)
        {
            // 1. File Version
            writer.Write(CurrentVersion);

            // 2. Total Time Played
            writer.Write(TotalTimePlayed);

            // 3. Hero Design
            writer.Write(HeroName ?? string.Empty);
            WriteColor(writer, SkinColor);
            WriteColor(writer, HairColor);
            writer.Write(HairstyleIndex);
            WriteColor(writer, ShirtColor);

            // 4. Hero RPG State
            writer.Write(JobName ?? string.Empty);
            writer.Write(Level);
            writer.Write(Experience);
            writer.Write(BaseStrength);
            writer.Write(BaseAgility);
            writer.Write(BaseVitality);
            writer.Write(BaseMagic);
            writer.Write(CurrentHP);
            writer.Write(CurrentMP);

            // 5. Equipment (6 slots)
            for (int i = 0; i < 6; i++)
            {
                bool hasItem = !string.IsNullOrEmpty(EquipmentNames[i]);
                writer.Write(hasItem);
                if (hasItem)
                {
                    writer.Write(EquipmentNames[i]);
                }
            }

            // 6. Inventory
            writer.Write(InventoryItems.Count);
            for (int i = 0; i < InventoryItems.Count; i++)
            {
                SavedItem item = InventoryItems[i];
                writer.Write(item.Name ?? string.Empty);
                writer.Write(item.IsConsumable);
                if (item.IsConsumable)
                {
                    writer.Write(item.StackCount);
                }
                writer.Write(item.SlotIndex);
            }

            // 7. Hero Crystal
            writer.Write(HasCrystal);
            if (HasCrystal)
            {
                writer.Write(CrystalJobName ?? string.Empty);
                writer.Write(CrystalLevel);
                writer.Write(CrystalBaseStrength);
                writer.Write(CrystalBaseAgility);
                writer.Write(CrystalBaseVitality);
                writer.Write(CrystalBaseMagic);
                writer.Write(TotalJP);
                writer.Write(CurrentJP);

                writer.Write(LearnedSkillIds.Count);
                for (int i = 0; i < LearnedSkillIds.Count; i++)
                {
                    writer.Write(LearnedSkillIds[i]);
                }

                writer.Write(SynergyPoints.Count);
                var synergyEnumerator = SynergyPoints.GetEnumerator();
                while (synergyEnumerator.MoveNext())
                {
                    writer.Write(synergyEnumerator.Current.Key);
                    writer.Write(synergyEnumerator.Current.Value);
                }
                synergyEnumerator.Dispose();

                writer.Write(LearnedSynergySkillIds.Count);
                for (int i = 0; i < LearnedSynergySkillIds.Count; i++)
                {
                    writer.Write(LearnedSynergySkillIds[i]);
                }

                writer.Write(DiscoveredSynergyIds.Count);
                for (int i = 0; i < DiscoveredSynergyIds.Count; i++)
                {
                    writer.Write(DiscoveredSynergyIds[i]);
                }
            }

            // 8. Game State
            writer.Write(Funds);

            writer.Write(DiscoveredStencils.Count);
            var stencilEnumerator = DiscoveredStencils.GetEnumerator();
            while (stencilEnumerator.MoveNext())
            {
                writer.Write(stencilEnumerator.Current.Key);
                writer.Write(stencilEnumerator.Current.Value);
            }
            stencilEnumerator.Dispose();

            // 9. Pit State
            writer.Write(PitLevel);

            // 10. Priorities
            writer.Write(Priority1);
            writer.Write(Priority2);
            writer.Write(Priority3);
            writer.Write(HealPriority1);
            writer.Write(HealPriority2);
            writer.Write(HealPriority3);

            // 10b. Behavior Settings (added in version 4)
            writer.Write(BattleTacticValue);
            writer.Write(UseConsumablesOnMercenaries);
            writer.Write(MercenariesCanUseConsumables);

            // 11. Allied Monsters
            writer.Write(AlliedMonsters.Count);
            for (int i = 0; i < AlliedMonsters.Count; i++)
            {
                SavedAlliedMonster monster = AlliedMonsters[i];
                writer.Write(monster.Name ?? string.Empty);
                writer.Write(monster.MonsterTypeName ?? string.Empty);
                writer.Write(monster.FishingProficiency);
                writer.Write(monster.CookingProficiency);
                writer.Write(monster.FarmingProficiency);
            }

            // 12. Shortcut Bar (added in version 2)
            writer.Write(ShortcutSlots.Count);
            for (int i = 0; i < ShortcutSlots.Count; i++)
            {
                SavedShortcutSlot slot = ShortcutSlots[i];
                writer.Write(slot.SlotType);
                if (slot.SlotType == 1) // Item
                {
                    writer.Write(slot.ItemBagIndex);
                }
                else if (slot.SlotType == 2) // Skill
                {
                    writer.Write(slot.SkillId ?? string.Empty);
                }
            }

            // 13. Hired Mercenaries (added in version 3)
            writer.Write(HiredMercenaries.Count);
            for (int i = 0; i < HiredMercenaries.Count; i++)
            {
                SavedMercenary merc = HiredMercenaries[i];
                writer.Write(merc.Name ?? string.Empty);
                writer.Write(merc.JobName ?? string.Empty);
                writer.Write(merc.Level);
                writer.Write(merc.BaseStrength);
                writer.Write(merc.BaseAgility);
                writer.Write(merc.BaseVitality);
                writer.Write(merc.BaseMagic);
                writer.Write(merc.CurrentHP);
                writer.Write(merc.CurrentMP);
                for (int e = 0; e < 6; e++)
                {
                    bool hasEquip = merc.EquipmentNames != null && e < merc.EquipmentNames.Length && !string.IsNullOrEmpty(merc.EquipmentNames[e]);
                    writer.Write(hasEquip);
                    if (hasEquip)
                    {
                        writer.Write(merc.EquipmentNames[e]);
                    }
                }
                WriteColor(writer, merc.SkinColor);
                WriteColor(writer, merc.HairColor);
                writer.Write(merc.HairstyleIndex);
                WriteColor(writer, merc.ShirtColor);
            }
        }

        /// <summary>Reads all game state from the persistence reader.</summary>
        void IPersistable.Recover(IPersistableReader reader)
        {
            // 1. File Version (reserved for future migration logic)
            int version = reader.ReadInt();

            // 2. Total Time Played
            TotalTimePlayed = reader.ReadFloat();

            // 3. Hero Design
            HeroName = reader.ReadString();
            SkinColor = ReadColor(reader);
            HairColor = ReadColor(reader);
            HairstyleIndex = reader.ReadInt();
            ShirtColor = ReadColor(reader);

            // 4. Hero RPG State
            JobName = reader.ReadString();
            Level = reader.ReadInt();
            Experience = reader.ReadInt();
            BaseStrength = reader.ReadInt();
            BaseAgility = reader.ReadInt();
            BaseVitality = reader.ReadInt();
            BaseMagic = reader.ReadInt();
            CurrentHP = reader.ReadInt();
            CurrentMP = reader.ReadInt();

            // 5. Equipment (6 slots)
            for (int i = 0; i < 6; i++)
            {
                bool hasItem = reader.ReadBool();
                EquipmentNames[i] = hasItem ? reader.ReadString() : null;
            }

            // 6. Inventory
            int itemCount = reader.ReadInt();
            InventoryItems = new List<SavedItem>(itemCount);
            for (int i = 0; i < itemCount; i++)
            {
                SavedItem item;
                item.Name = reader.ReadString();
                item.IsConsumable = reader.ReadBool();
                item.StackCount = item.IsConsumable ? reader.ReadInt() : 0;
                item.SlotIndex = reader.ReadInt();
                InventoryItems.Add(item);
            }

            // 7. Hero Crystal
            HasCrystal = reader.ReadBool();
            if (HasCrystal)
            {
                CrystalJobName = reader.ReadString();
                CrystalLevel = reader.ReadInt();
                CrystalBaseStrength = reader.ReadInt();
                CrystalBaseAgility = reader.ReadInt();
                CrystalBaseVitality = reader.ReadInt();
                CrystalBaseMagic = reader.ReadInt();
                TotalJP = reader.ReadInt();
                CurrentJP = reader.ReadInt();

                int skillCount = reader.ReadInt();
                LearnedSkillIds = new List<string>(skillCount);
                for (int i = 0; i < skillCount; i++)
                {
                    LearnedSkillIds.Add(reader.ReadString());
                }

                int synergyPointCount = reader.ReadInt();
                SynergyPoints = new Dictionary<string, int>(synergyPointCount);
                for (int i = 0; i < synergyPointCount; i++)
                {
                    string key = reader.ReadString();
                    int value = reader.ReadInt();
                    SynergyPoints[key] = value;
                }

                int synSkillCount = reader.ReadInt();
                LearnedSynergySkillIds = new List<string>(synSkillCount);
                for (int i = 0; i < synSkillCount; i++)
                {
                    LearnedSynergySkillIds.Add(reader.ReadString());
                }

                int discSynCount = reader.ReadInt();
                DiscoveredSynergyIds = new List<string>(discSynCount);
                for (int i = 0; i < discSynCount; i++)
                {
                    DiscoveredSynergyIds.Add(reader.ReadString());
                }
            }

            // 8. Game State
            Funds = reader.ReadInt();

            int stencilCount = reader.ReadInt();
            DiscoveredStencils = new Dictionary<string, int>(stencilCount);
            for (int i = 0; i < stencilCount; i++)
            {
                string key = reader.ReadString();
                int value = reader.ReadInt();
                DiscoveredStencils[key] = value;
            }

            // 9. Pit State
            PitLevel = reader.ReadInt();

            // 10. Priorities
            Priority1 = reader.ReadInt();
            Priority2 = reader.ReadInt();
            Priority3 = reader.ReadInt();
            HealPriority1 = reader.ReadInt();
            HealPriority2 = reader.ReadInt();
            HealPriority3 = reader.ReadInt();

            // 10b. Behavior Settings (added in version 4)
            if (version >= 4)
            {
                BattleTacticValue = reader.ReadInt();
                UseConsumablesOnMercenaries = reader.ReadBool();
                MercenariesCanUseConsumables = reader.ReadBool();
            }

            // 11. Allied Monsters
            int monsterCount = reader.ReadInt();
            AlliedMonsters = new List<SavedAlliedMonster>(monsterCount);
            for (int i = 0; i < monsterCount; i++)
            {
                SavedAlliedMonster monster;
                monster.Name = reader.ReadString();
                monster.MonsterTypeName = reader.ReadString();
                monster.FishingProficiency = reader.ReadInt();
                monster.CookingProficiency = reader.ReadInt();
                monster.FarmingProficiency = reader.ReadInt();
                AlliedMonsters.Add(monster);
            }

            // 12. Shortcut Bar (added in version 2)
            if (version >= 2)
            {
                int shortcutCount = reader.ReadInt();
                ShortcutSlots = new List<SavedShortcutSlot>(shortcutCount);
                for (int i = 0; i < shortcutCount; i++)
                {
                    SavedShortcutSlot slot;
                    slot.SlotType = reader.ReadInt();
                    slot.ItemBagIndex = 0;
                    slot.SkillId = null;
                    if (slot.SlotType == 1) // Item
                    {
                        slot.ItemBagIndex = reader.ReadInt();
                    }
                    else if (slot.SlotType == 2) // Skill
                    {
                        slot.SkillId = reader.ReadString();
                    }
                    ShortcutSlots.Add(slot);
                }
            }

            // 13. Hired Mercenaries (added in version 3)
            if (version >= 3)
            {
                int mercCount = reader.ReadInt();
                HiredMercenaries = new List<SavedMercenary>(mercCount);
                for (int i = 0; i < mercCount; i++)
                {
                    SavedMercenary merc;
                    merc.Name = reader.ReadString();
                    merc.JobName = reader.ReadString();
                    merc.Level = reader.ReadInt();
                    merc.BaseStrength = reader.ReadInt();
                    merc.BaseAgility = reader.ReadInt();
                    merc.BaseVitality = reader.ReadInt();
                    merc.BaseMagic = reader.ReadInt();
                    merc.CurrentHP = reader.ReadInt();
                    merc.CurrentMP = reader.ReadInt();
                    merc.EquipmentNames = new string[6];
                    for (int e = 0; e < 6; e++)
                    {
                        bool hasEquip = reader.ReadBool();
                        merc.EquipmentNames[e] = hasEquip ? reader.ReadString() : null;
                    }
                    merc.SkinColor = ReadColor(reader);
                    merc.HairColor = ReadColor(reader);
                    merc.HairstyleIndex = reader.ReadInt();
                    merc.ShirtColor = ReadColor(reader);
                    HiredMercenaries.Add(merc);
                }
            }
        }

        /// <summary>Writes a Color as four individual int components (R, G, B, A).</summary>
        private static void WriteColor(IPersistableWriter writer, Color color)
        {
            writer.Write((int)color.R);
            writer.Write((int)color.G);
            writer.Write((int)color.B);
            writer.Write((int)color.A);
        }

        /// <summary>Reads a Color from four individual int components (R, G, B, A).</summary>
        private static Color ReadColor(IPersistableReader reader)
        {
            int r = reader.ReadInt();
            int g = reader.ReadInt();
            int b = reader.ReadInt();
            int a = reader.ReadInt();
            return new Color(r, g, b, a);
        }
    }
}
