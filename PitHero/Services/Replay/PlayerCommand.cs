namespace PitHero.Services.Replay
{
    /// <summary>
    /// Every way the player can change simulation state. UI code never mutates the simulation
    /// directly: it enqueues one of these on the <see cref="PlayerCommandService"/>, which applies it
    /// at a fixed point in the simulation tick and records it for replay. Values are persisted in
    /// replay files, so append new members at the end and never renumber.
    /// </summary>
    public enum PlayerCommandType
    {
        None = 0,

        // Pause
        SetManualPause = 1,          // A = 0/1
        SetFarmModePause = 2,        // A = 0/1

        // Hero control
        SetStoppedAdventure = 10,    // A = 0/1
        Replenish = 11,
        SetPitPriorities = 12,       // A,B,C = PitPriority ordinals in order
        SetHealPriorities = 13,      // A,B,C = HealPriority ordinals in order
        SetBattleTactic = 14,        // A = BattleTactic ordinal
        SetUseConsumablesOnMercs = 15,   // A = 0/1
        SetMercsCanUseConsumables = 16,  // A = 0/1
        SetAutoEquipHero = 17,       // A = 0/1
        SetAutoEquipMercs = 18,      // A = 0/1
        SetReplenishThresholds = 19, // A = HP percent, B = MP percent
        RequestManualJobChange = 20,
        PurchaseSkill = 21,          // S = skill name

        // Shortcut bar / consumables
        UseShortcut = 30,            // A = shortcut index
        UseBagConsumable = 31,       // A = bag index
        SetShortcutItem = 32,        // A = shortcut index, B = bag index
        SetShortcutSkill = 33,       // A = shortcut index, S = skill name, B = hired merc index or -1
        ClearShortcut = 34,          // A = shortcut index
        SwapShortcuts = 35,          // A, B = shortcut indices

        // Inventory / equipment
        SwapSlots = 40,              // A,B = packed SlotRef a; C,D = packed SlotRef b; L = grid owner id
        SellBagItem = 41,            // A = bag index, B = quantity (0 = whole stack)
        PlaceStencil = 42,           // S = pattern id, A = x, B = y
        RemoveStencil = 43,          // S = pattern id, L = grid owner id
        MoveStencil = 44,            // S = pattern id, A = x, B = y, L = grid owner id

        // Mercenaries / monsters
        HireMercenary = 50,          // A = tavern index, S = merc name
        DismissTavernMercenary = 51, // A = tavern index, S = merc name
        DismissPartyMercenary = 52,  // A = hired index, S = merc name
        SetMonsterJob = 53,          // A = allied monster index, B = MonsterJob ordinal, S = monster name
        PurchaseMonster = 54,        // A = house id, S = enemy type name, B = cost

        // Automation
        SetAutomation = 60,          // A = AutomationKind ordinal, B = 0/1
        SetGoldBuffer = 61,          // A = gold
        SetAutoLearnMode = 62,       // A = mode ordinal
        SetAutoHireJobSlot = 63,     // A = slot, B = job index

        // Farm / construction / storage
        PlaceBuilding = 70,          // A = building type, B = tile x, C = tile y
        MoveBuilding = 71,           // A = building id, B = tile x, C = tile y
        RemoveBuilding = 72,         // A = building id
        TillTile = 73,               // A = x, B = y
        RestoreGrassTile = 74,       // A = x, B = y
        RestoreAllTilled = 75,
        AddCropPlan = 76,            // A = crop type, B = x, C = y
        RemoveCropPlan = 77,         // A = x, B = y
        SellAllStorageCrops = 78,
        SellStorageCrops = 79,       // A = building id
        MoveAllCropsToOtherStorages = 80, // A = building id
        FridgeReturnSlot = 81,       // A = slot, B = CropType shown when the player clicked
        FridgeSellSlot = 82,         // A = slot, B = CropType shown when the player clicked
        UnmarkTillTile = 83,         // A = x, B = y (clears a ReadyToTill mark)
        FarmRescan = 84,             // farm menu closed: coordinator rescans plans
        AutoHirePass = 85,           // settings closed with auto-hire on: immediate hire pass

        // Shop
        BuyVaultItem = 90,           // A = vault stack index, B = quantity, C,D = packed dest SlotRef, S = item name
        BuyVaultCrystal = 91,        // A = vault crystal index, B = dest slot type, C = dest index
        BuySeeds = 92,               // A = crop type, B = quantity

        // Crystals
        CreateCrystal = 100,         // A = job index, B = STR, C = AGI, D = VIT, L = MAG
        ForgeCrystals = 101,
        SwapCrystalSlots = 102,      // A = src type, B = src index, C = dst type, D = dst index
        EnqueueCrystal = 103,        // A = inventory index
        ClearCrystalQueueSlot = 104, // A = queue slot
        RemoveCrystalFromInventory = 105, // A = inventory index

        // Automation option dialogs
        SetAutoPurchaseSelected = 110,   // A = consumable index, B = 0/1
        SetConsumableStackTarget = 111,  // A = consumable index, B = target
        SetConsumableSellAllowed = 112,  // A = consumable index, B = 0/1
        SetConsumableMinStacks = 113,    // A = consumable index, B = min stacks
        SetGearFilterFlag = 114,         // A = owner (0 sell, 1 purchase), B = kind (0 rarity, 1 type), C = index, D = 0/1
        SetCropDesignation = 115,        // A = crop index, B = 0/1
        SetCropKeepStacks = 116,         // A = stacks

        // Debug
        DebugQueuePitLevel = 200,    // A = level
    }

    /// <summary>
    /// Packs an inventory-grid slot identity (slot type + grid x/y) into one int so a command can
    /// name the exact cell the player dragged from/to. Grid coordinates are stable across sessions
    /// (the grid layout is fixed), unlike slot object references.
    /// </summary>
    public static class SlotRefCodec
    {
        /// <summary>Packs (type, x, y) into an int: type in bits 0-7, x in bits 8-19, y in bits 20-31.</summary>
        public static int Pack(int slotType, int x, int y)
        {
            return (slotType & 0xFF) | ((x & 0xFFF) << 8) | ((y & 0xFFF) << 20);
        }

        /// <summary>Unpacks an int produced by <see cref="Pack"/>.</summary>
        public static void Unpack(int packed, out int slotType, out int x, out int y)
        {
            slotType = packed & 0xFF;
            x = (packed >> 8) & 0xFFF;
            y = (packed >> 20) & 0xFFF;
        }
    }

    /// <summary>
    /// One recorded player intent. Small fixed payload so commands serialize compactly; the meaning of
    /// A/B/C/D/L/F/S per type is documented on <see cref="PlayerCommandType"/>.
    /// </summary>
    public struct PlayerCommand
    {
        public PlayerCommandType Type;
        public int A;
        public int B;
        public int C;
        public int D;
        public long L;
        public float F;
        public string S;

        /// <summary>Creates a command with up to four int arguments.</summary>
        public PlayerCommand(PlayerCommandType type, int a = 0, int b = 0, int c = 0, int d = 0)
        {
            Type = type; A = a; B = b; C = c; D = d; L = 0; F = 0f; S = null;
        }

        /// <summary>Creates a command carrying a string plus up to three ints.</summary>
        public static PlayerCommand WithString(PlayerCommandType type, string s, int a = 0, int b = 0, int c = 0)
        {
            var cmd = new PlayerCommand(type, a, b, c);
            cmd.S = s;
            return cmd;
        }

        /// <summary>Creates a flag command (A = 1 for true).</summary>
        public static PlayerCommand Flag(PlayerCommandType type, bool value)
        {
            return new PlayerCommand(type, value ? 1 : 0);
        }

        /// <summary>A as a bool.</summary>
        public bool ABool => A != 0;
    }
}
