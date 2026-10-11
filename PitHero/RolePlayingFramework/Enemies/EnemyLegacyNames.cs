namespace RolePlayingFramework.Enemies
{
    /// <summary>
    /// Maps monster type names written by older saves/replays onto their current names, so a
    /// renamed <see cref="EnemyId"/> keeps loading. Saves store these as text: bare enum names
    /// ("Rat") for defeated types and text keys ("Monster_Rat") for allied monsters.
    /// </summary>
    public static class EnemyLegacyNames
    {
        /// <summary>Returns the current name for a saved monster type name; unchanged if it was never renamed.</summary>
        public static string Normalize(string savedName)
        {
            // Rat was renamed to PitMouse (issue #446)
            if (savedName == "Rat") return nameof(EnemyId.PitMouse);
            if (savedName == "Monster_Rat") return "Monster_PitMouse";
            return savedName;
        }
    }
}
