using Nez;
using Nez.Persistence.Binary;
using PitHero.ECS.Components;
using PitHero.UI;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Heroes;
using RolePlayingFramework.Synergies;
using System.Collections.Generic;

namespace PitHero.Services
{
    /// <summary>Service that manages saving and loading game state across 5 save slots.</summary>
    public class SaveLoadService
    {
        /// <summary>Maximum number of save slots available.</summary>
        public const int MaxSlots = 5;

        private const string SaveFilePrefix = "save_slot_";
        private const string SaveFileExtension = ".bin";

        private readonly FileDataStore _fileDataStore;

        private readonly SaveData[] _slotPreviews;

        /// <summary>Pending save data to be applied when MainGameScene initializes.</summary>
        public static SaveData PendingLoadData { get; set; }

        /// <summary>Records when a save or load operation occurs (Time.TotalTime at that moment).</summary>
        public double TimeAtSaveLoad { get; private set; }

        /// <summary>Total time played from the loaded save file. Zero for a new game.</summary>
        public float LoadedTimePlayed { get; private set; }

        /// <summary>Time.TotalTime at the moment the save was loaded. Zero for a new game.</summary>
        public double TimeAtLoad { get; private set; }

        /// <summary>Creates a new SaveLoadService with the given FileDataStore.</summary>
        public SaveLoadService(FileDataStore fileDataStore)
        {
            _fileDataStore = fileDataStore;
            _slotPreviews = new SaveData[MaxSlots];
            RefreshSlotPreviews();
        }

        /// <summary>Gets the filename for a given slot index.</summary>
        private string GetFilename(int slotIndex)
        {
            return SaveFilePrefix + slotIndex + SaveFileExtension;
        }

        /// <summary>Checks if a save file exists for the given slot.</summary>
        public bool SlotHasData(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= MaxSlots)
                return false;

            return _slotPreviews[slotIndex] != null;
        }

        /// <summary>Gets cached save data preview for a slot (for UI display). Returns null if slot is empty.</summary>
        public SaveData GetSlotPreview(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= MaxSlots)
                return null;

            return _slotPreviews[slotIndex];
        }

        /// <summary>Refreshes the cached preview data for all slots by loading headers from files.</summary>
        public void RefreshSlotPreviews()
        {
            for (int i = 0; i < MaxSlots; i++)
            {
                var data = new SaveData();
                _fileDataStore.Load(GetFilename(i), data);

                // If HeroName was populated by Load, the file existed and had valid data
                if (data.HeroName != null)
                    _slotPreviews[i] = data;
                else
                    _slotPreviews[i] = null;
            }
        }

        /// <summary>Saves the current game state to the specified slot.</summary>
        public void SaveToSlot(int slotIndex, SaveData saveData)
        {
            if (slotIndex < 0 || slotIndex >= MaxSlots)
            {
                Debug.Log("SaveLoadService: Invalid slot index " + slotIndex);
                return;
            }

            TimeAtSaveLoad = Time.TotalTime;
            _fileDataStore.Save(GetFilename(slotIndex), saveData);
            _slotPreviews[slotIndex] = saveData;
            Debug.Log("SaveLoadService: Saved to slot " + slotIndex);
        }

        /// <summary>Loads game state from the specified slot. Returns null if slot is empty.</summary>
        public SaveData LoadFromSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= MaxSlots)
            {
                Debug.Log("SaveLoadService: Invalid slot index " + slotIndex);
                return null;
            }

            var data = new SaveData();
            _fileDataStore.Load(GetFilename(slotIndex), data);

            if (data.HeroName == null)
            {
                Debug.Log("SaveLoadService: Slot " + slotIndex + " is empty");
                return null;
            }

            TimeAtSaveLoad = Time.TotalTime;
            LoadedTimePlayed = data.TotalTimePlayed;
            TimeAtLoad = Time.TotalTime;
            Debug.Log("SaveLoadService: Loaded from slot " + slotIndex);
            return data;
        }

        /// <summary>Gathers all current game state into a SaveData object for saving.</summary>
        public static SaveData GatherCurrentState()
        {
            var data = new SaveData();

            // Time - accumulate previously saved time plus time elapsed since load
            var saveLoadService = Core.Services.GetService<SaveLoadService>();
            if (saveLoadService != null)
            {
                float previousTime = saveLoadService.LoadedTimePlayed;
                double timeAtLoad = saveLoadService.TimeAtLoad;
                data.TotalTimePlayed = previousTime + (float)(Time.TotalTime - timeAtLoad);
            }
            else
            {
                data.TotalTimePlayed = (float)Time.TotalTime;
            }

            // Hero Design
            var designService = Core.Services.GetService<HeroDesignService>();
            if (designService != null && designService.HasDesign)
            {
                var design = designService.GetDesign();
                data.HeroName = design.Name;
                data.SkinColor = design.SkinColor;
                data.HairColor = design.HairColor;
                data.HairstyleIndex = design.HairstyleIndex;
                data.ShirtColor = design.ShirtColor;
            }

            // Find hero entity
            var heroEntity = Core.Scene?.FindEntity("hero");
            if (heroEntity != null)
            {
                var heroComp = heroEntity.GetComponent<HeroComponent>();
                if (heroComp?.LinkedHero != null)
                {
                    var hero = heroComp.LinkedHero;
                    data.JobName = hero.Job.Name;
                    data.Level = hero.Level;
                    data.Experience = hero.Experience;
                    data.BaseStrength = hero.BaseStats.Strength;
                    data.BaseAgility = hero.BaseStats.Agility;
                    data.BaseVitality = hero.BaseStats.Vitality;
                    data.BaseMagic = hero.BaseStats.Magic;
                    data.CurrentHP = hero.CurrentHP;
                    data.CurrentMP = hero.CurrentMP;

                    // Equipment (6 slots)
                    data.EquipmentNames = new string[6];
                    data.EquipmentNames[0] = hero.WeaponShield1?.Name ?? "";
                    data.EquipmentNames[1] = hero.Armor?.Name ?? "";
                    data.EquipmentNames[2] = hero.Hat?.Name ?? "";
                    data.EquipmentNames[3] = hero.WeaponShield2?.Name ?? "";
                    data.EquipmentNames[4] = hero.Accessory1?.Name ?? "";
                    data.EquipmentNames[5] = hero.Accessory2?.Name ?? "";

                    // Inventory from HeroComponent.Bag
                    var bag = heroComp.Bag;
                    if (bag != null)
                    {
                        data.InventoryItems = new List<SavedItem>(bag.Capacity);
                        for (int i = 0; i < bag.Capacity; i++)
                        {
                            var item = bag.GetSlotItem(i);
                            if (item != null)
                            {
                                var savedItem = new SavedItem();
                                savedItem.Name = item.Name;
                                savedItem.SlotIndex = i;

                                if (item is Consumable consumable)
                                {
                                    savedItem.IsConsumable = true;
                                    savedItem.StackCount = consumable.StackCount;
                                }

                                data.InventoryItems.Add(savedItem);
                                Debug.Log("[SaveLoadService] Saving item '" + item.Name + "' at slot " + i);
                            }
                        }
                    }

                    // Crystal
                    if (hero.BoundCrystal != null)
                    {
                        var crystal = hero.BoundCrystal;
                        data.HasCrystal = true;
                        data.CrystalJobName = crystal.Job.Name;
                        data.CrystalLevel = crystal.Level;
                        data.CrystalBaseStrength = crystal.BaseStats.Strength;
                        data.CrystalBaseAgility = crystal.BaseStats.Agility;
                        data.CrystalBaseVitality = crystal.BaseStats.Vitality;
                        data.CrystalBaseMagic = crystal.BaseStats.Magic;
                        data.TotalJP = crystal.TotalJP;
                        data.CurrentJP = crystal.CurrentJP;

                        // Learned skill IDs
                        data.LearnedSkillIds = new List<string>(crystal.LearnedSkillIds.Count);
                        var skillEnumerator = crystal.LearnedSkillIds.GetEnumerator();
                        while (skillEnumerator.MoveNext())
                        {
                            data.LearnedSkillIds.Add(skillEnumerator.Current);
                        }
                        skillEnumerator.Dispose();

                        // Synergy points
                        data.SynergyPoints = new Dictionary<string, int>(crystal.SynergyPoints.Count);
                        var synergyEnumerator = crystal.SynergyPoints.GetEnumerator();
                        while (synergyEnumerator.MoveNext())
                        {
                            data.SynergyPoints[synergyEnumerator.Current.Key] = synergyEnumerator.Current.Value;
                        }
                        synergyEnumerator.Dispose();

                        // Learned synergy skill IDs
                        data.LearnedSynergySkillIds = new List<string>(crystal.LearnedSynergySkillIds.Count);
                        var synSkillEnumerator = crystal.LearnedSynergySkillIds.GetEnumerator();
                        while (synSkillEnumerator.MoveNext())
                        {
                            data.LearnedSynergySkillIds.Add(synSkillEnumerator.Current);
                        }
                        synSkillEnumerator.Dispose();

                        // Discovered synergy IDs
                        data.DiscoveredSynergyIds = new List<string>(crystal.DiscoveredSynergyIds.Count);
                        var discSynEnumerator = crystal.DiscoveredSynergyIds.GetEnumerator();
                        while (discSynEnumerator.MoveNext())
                        {
                            data.DiscoveredSynergyIds.Add(discSynEnumerator.Current);
                        }
                        discSynEnumerator.Dispose();
                    }

                    // Priorities
                    data.Priority1 = (int)heroComp.Priority1;
                    data.Priority2 = (int)heroComp.Priority2;
                    data.Priority3 = (int)heroComp.Priority3;
                    data.HealPriority1 = (int)heroComp.HealPriority1;
                    data.HealPriority2 = (int)heroComp.HealPriority2;
                    data.HealPriority3 = (int)heroComp.HealPriority3;

                    // Behavior settings
                    data.BattleTacticValue = (int)heroComp.CurrentBattleTactic;
                    data.UseConsumablesOnMercenaries = heroComp.UseConsumablesOnMercenaries;
                    data.MercenariesCanUseConsumables = heroComp.MercenariesCanUseConsumables;
                }
            }

            // Game state
            var gameState = Core.Services.GetService<GameStateService>();
            if (gameState != null)
            {
                data.Funds = gameState.Funds;

                // Copy stencils (enum to int)
                data.DiscoveredStencils = new Dictionary<string, int>(gameState.DiscoveredStencils.Count);
                var stencilEnumerator = gameState.DiscoveredStencils.GetEnumerator();
                while (stencilEnumerator.MoveNext())
                {
                    data.DiscoveredStencils[stencilEnumerator.Current.Key] = (int)stencilEnumerator.Current.Value;
                }
                stencilEnumerator.Dispose();
            }

            // Pit level
            var pitManager = Core.Services.GetService<PitWidthManager>();
            if (pitManager != null)
                data.PitLevel = pitManager.CurrentPitLevel;

            // Allied monsters
            var alliedManager = Core.Services.GetService<AlliedMonsterManager>();
            if (alliedManager != null)
            {
                var monsters = alliedManager.AlliedMonsters;
                data.AlliedMonsters = new List<SavedAlliedMonster>(monsters.Count);
                for (int i = 0; i < monsters.Count; i++)
                {
                    var monster = monsters[i];
                    var saved = new SavedAlliedMonster();
                    saved.Name = monster.Name;
                    saved.MonsterTypeName = monster.MonsterTypeName;
                    saved.FishingProficiency = monster.FishingProficiency;
                    saved.CookingProficiency = monster.CookingProficiency;
                    saved.FarmingProficiency = monster.FarmingProficiency;
                    data.AlliedMonsters.Add(saved);
                }
            }

            // Hired mercenaries
            var mercManager = Core.Services.GetService<MercenaryManager>();
            if (mercManager != null)
            {
                var hiredMercs = mercManager.GetHiredMercenaries();
                data.HiredMercenaries = new List<SavedMercenary>(hiredMercs.Count);
                for (int i = 0; i < hiredMercs.Count; i++)
                {
                    var mercComp = hiredMercs[i].GetComponent<MercenaryComponent>();
                    if (mercComp?.LinkedMercenary == null) continue;

                    var merc = mercComp.LinkedMercenary;
                    var savedMerc = new SavedMercenary();
                    savedMerc.Name = merc.Name;
                    savedMerc.JobName = merc.Job.Name;
                    savedMerc.Level = merc.Level;
                    savedMerc.Experience = merc.Experience;
                    savedMerc.BaseStrength = merc.BaseStats.Strength;
                    savedMerc.BaseAgility = merc.BaseStats.Agility;
                    savedMerc.BaseVitality = merc.BaseStats.Vitality;
                    savedMerc.BaseMagic = merc.BaseStats.Magic;
                    savedMerc.CurrentHP = merc.CurrentHP;
                    savedMerc.CurrentMP = merc.CurrentMP;
                    savedMerc.EquipmentNames = new string[6];
                    savedMerc.EquipmentNames[0] = merc.WeaponShield1?.Name ?? "";
                    savedMerc.EquipmentNames[1] = merc.Armor?.Name ?? "";
                    savedMerc.EquipmentNames[2] = merc.Hat?.Name ?? "";
                    savedMerc.EquipmentNames[3] = merc.WeaponShield2?.Name ?? "";
                    savedMerc.EquipmentNames[4] = merc.Accessory1?.Name ?? "";
                    savedMerc.EquipmentNames[5] = merc.Accessory2?.Name ?? "";
                    savedMerc.SkinColor = mercComp.SkinColor;
                    savedMerc.HairColor = mercComp.HairColor;
                    savedMerc.HairstyleIndex = mercComp.HairstyleIndex;
                    savedMerc.ShirtColor = mercComp.ShirtColor;
                    data.HiredMercenaries.Add(savedMerc);
                }
            }

            // Shortcut bar
            var shortcutBarService = Core.Services.GetService<ShortcutBarService>();
            if (shortcutBarService?.ShortcutBar != null)
            {
                var bar = shortcutBarService.ShortcutBar;
                const int ShortcutCount = 8;
                data.ShortcutSlots = new List<SavedShortcutSlot>(ShortcutCount);
                for (int i = 0; i < ShortcutCount; i++)
                {
                    var slotData = bar.GetShortcutSlotData(i);
                    var saved = new SavedShortcutSlot();
                    if (slotData == null || slotData.SlotType == ShortcutSlotType.Empty)
                    {
                        saved.SlotType = 0; // Empty
                    }
                    else if (slotData.SlotType == ShortcutSlotType.Item)
                    {
                        saved.SlotType = 1;
                        saved.ItemBagIndex = slotData.ReferencedSlot?.SlotData?.BagIndex ?? -1;
                        if (saved.ItemBagIndex >= 0)
                        {
                            Debug.Log("[SaveLoadService] Saving shortcut " + i + " as item at bag index " + saved.ItemBagIndex);
                        }
                        else
                        {
                            saved.SlotType = 0; // No valid bag index, treat as empty
                        }
                    }
                    else if (slotData.SlotType == ShortcutSlotType.Skill)
                    {
                        saved.SlotType = 2;
                        saved.SkillId = slotData.ReferencedSkill?.Id;
                        if (!string.IsNullOrEmpty(saved.SkillId))
                        {
                            Debug.Log("[SaveLoadService] Saving shortcut " + i + " as skill '" + saved.SkillId + "'");
                        }
                        else
                        {
                            saved.SlotType = 0; // No valid skill ID, treat as empty
                        }
                    }
                    data.ShortcutSlots.Add(saved);
                }
            }

            return data;
        }

        /// <summary>Applies loaded SaveData to restore the game state.</summary>
        public static void ApplyLoadedState(SaveData data)
        {
            if (data == null)
                return;

            // Restore hero design
            var designService = Core.Services.GetService<HeroDesignService>();
            if (designService != null && !string.IsNullOrEmpty(data.HeroName))
            {
                var design = new HeroDesign(
                    data.HeroName,
                    data.SkinColor,
                    data.HairColor,
                    data.HairstyleIndex,
                    data.ShirtColor,
                    data.JobName
                );
                designService.SetDesign(design);
            }

            // Restore game state (funds and stencils)
            var gameState = Core.Services.GetService<GameStateService>();
            if (gameState != null)
            {
                gameState.Funds = data.Funds;

                // Restore stencils (int back to enum)
                gameState.DiscoveredStencils.Clear();
                var stencilEnumerator = data.DiscoveredStencils.GetEnumerator();
                while (stencilEnumerator.MoveNext())
                {
                    gameState.DiscoveredStencils[stencilEnumerator.Current.Key] =
                        (StencilDiscoverySource)stencilEnumerator.Current.Value;
                }
                stencilEnumerator.Dispose();
            }

            // Store pending data for MainGameScene to apply during Begin()
            PendingLoadData = data;
            Debug.Log("SaveLoadService: State applied, pending data stored for scene initialization");
        }
    }
}
