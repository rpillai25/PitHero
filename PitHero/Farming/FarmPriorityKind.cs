namespace PitHero.Farming
{
    /// <summary>
    /// The four kinds of farm work whose claim order is orderable: by the game in "Monsters Decide"
    /// mode, or by the player. Pickup and DestroyCrop are deliberately absent — Pickup is always the
    /// first priority (a dropped crop is finished work sitting unbanked) and DestroyCrop always runs
    /// immediately before Plant, wherever Plant sits, because it frees the tile the swap-plant needs.
    /// Ordinals are persisted in the save file and in PlayerCommand payloads: append only, never
    /// renumber.
    /// </summary>
    public enum FarmPriorityKind
    {
        Water   = 0,
        Till    = 1,
        Plant   = 2,
        Harvest = 3,
    }
}
