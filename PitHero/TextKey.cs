namespace PitHero
{
    /// <summary>
    /// Strongly-typed keys for all localizable text strings in the game.
    /// Each value corresponds to a key in the localization files.
    /// </summary>
    public enum TextKey
    {
        ButtonYes, ButtonNo, ButtonCancel, ButtonClose, ButtonSave, ButtonLoad, ButtonNew, ButtonQuit, ButtonExit, ButtonReset,
        ButtonHire, ButtonReroll, ButtonCreateHero, ButtonQuitToTitle,
        ButtonActivateStencil, ButtonViewStencils, ButtonMoveStencils, ButtonRemoveStencil, ButtonExitMoveMode, ButtonExitRemoveMode,
        ButtonReplenish, ButtonFastForward, ButtonStopAdventuring, ButtonContinueAdventuring,
        WindowAppearance, WindowJobInfo, WindowMonsters, WindowSaveGame, WindowLoadGame,
        DialogReallyQuit, DialogReallyDiscard, DialogConfirmPurchase, DialogConfirmSave, DialogConfirmLoad,
        ConfirmQuitMessage, ConfirmExitMessage, ConfirmQuitToTitleMessage, ConfirmDiscardMessage, ConfirmOverwriteSaveSlot, ConfirmLoadSaveSlot,
        HudPitLevel, HudGold, HudLevelPrefix,
        TabWindow, TabSession, TabButtons,
        SettingsAlwaysOnTop, SettingsAutoScrollToHero, SettingsSwapMonitor, SettingsYOffset, SettingsZoom,
        SettingsWindowSize, SettingsWindowSizeNormal, SettingsWindowSizeHalf,
        SettingsDockTop, SettingsDockBottom, SettingsDockCenter,
        SettingsGameSession, SettingsReplenishLabel, SettingsHpThreshold, SettingsMpThreshold,
        TabInventory, TabBehavior, TabHeroInfo, TabMercenaries,
        HeroNameLabel, HeroJobLabel, HeroLevelLabel, HeroJobLevelLabel, HeroCurrentJpLabel, HeroTotalJpLabel, HeroStatsLabel,
        LabelJobSkills, LabelSynergySkills, LabelSynergyEffects,
        HeroNoCrystalBound, HeroNoJobSkillsAvailable, HeroNoSynergySkillsDiscovered, HeroNoActiveSynergyEffects,
        MercenaryNameLabel, MercenaryJobLevelLabel, MercenaryHpLabel, MercenaryMpLabel,
        MercenaryStrLabel, MercenaryAgiLabel, MercenaryVitLabel, MercenaryMagLabel, MercenaryCostLabel,
        MercenaryNoMercenariesHired, MercenaryNoJobSkills,
        BehaviorPitPriority, BehaviorHealPriority, BehaviorBattleTactics,
        BehaviorTacticBlitz, BehaviorTacticStrategic, BehaviorTacticDefensive,
        BehaviorTacticBlitzDesc, BehaviorTacticStrategicDesc, BehaviorTacticDefensiveDesc,
        BehaviorConsumableOptions, BehaviorUseConsumablesOnMerc, BehaviorMercCanUseConsumables,
        BehaviorAutoEquipOptions, BehaviorAutoEquipHero, BehaviorAutoEquipMercenaries,
        AppearanceNameLabel, AppearanceHairstyle, AppearanceSkin, AppearanceHairColor, AppearanceShirt,
        AppearanceRolePrefix, AppearanceSkillsLabel, AppearanceNoJobSkills,
        SaveLoadTimeHeader, SaveLoadEmptySlot, SaveLoadLevelLabel, SaveLoadUnknown,
        StencilSynergyStencils, StencilSelectPrompt,
        MonsterNoAlliedMonsters,
        ItemSellPrice, ItemRestoresHp, ItemFullyRestoresHp, ItemRestoresMp, ItemFullyRestoresMp, ItemClassesPrefix,
        StatBonusStrength, StatBonusAgility, StatBonusVitality, StatBonusMagic, StatBonusAttack, StatBonusDefense, StatBonusHp, StatBonusMp,
        StatDiffStrength, StatDiffAgility, StatDiffVitality, StatDiffMagic, StatDiffAttack, StatDiffDefense, StatDiffHp, StatDiffMp,
        SkillLearned, SkillInsufficientJp, SkillEffectsLabel, SkillActivePatternNote, SkillProgress, SkillJpCost, SkillActiveMultiplier,
        EquipPreviewChanges
    }
}
