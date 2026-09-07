namespace PitHero.Artifacts
{
    /// <summary>
    /// System-level artifacts: one-time purchases that, once owned, are available forever across every
    /// hero and save slot (persisted in the system save file, not the game save). Values are persisted;
    /// append only, never renumber.
    /// </summary>
    public enum ArtifactType
    {
        /// <summary>Unlocks the future-simulation region of the replay timeline.</summary>
        SphereOfForesight = 0,

        /// <summary>Unlocks Time Travel Here in replays. Requires the Sphere of Foresight first.</summary>
        ChronosTimepiece = 1,
    }

    /// <summary>Static facts about each artifact: sprite, text keys, price and purchase prerequisite.</summary>
    public static class ArtifactCatalog
    {
        /// <summary>Number of artifact kinds (the enum is dense from 0).</summary>
        public const int Count = 2;

        /// <summary>Sprite name in the Items atlas.</summary>
        public static string GetSpriteName(ArtifactType type)
        {
            switch (type)
            {
                case ArtifactType.SphereOfForesight: return "SphereOfForesight";
                case ArtifactType.ChronosTimepiece: return "ChronosTimepiece";
                default: return string.Empty;
            }
        }

        /// <summary>UI.txt key of the display name.</summary>
        public static string GetNameKey(ArtifactType type)
        {
            switch (type)
            {
                case ArtifactType.SphereOfForesight: return UITextKey.ArtifactSphereOfForesightName;
                case ArtifactType.ChronosTimepiece: return UITextKey.ArtifactChronosTimepieceName;
                default: return string.Empty;
            }
        }

        /// <summary>UI.txt key of the description shown in the artifact card.</summary>
        public static string GetDescriptionKey(ArtifactType type)
        {
            switch (type)
            {
                case ArtifactType.SphereOfForesight: return UITextKey.ArtifactSphereOfForesightDesc;
                case ArtifactType.ChronosTimepiece: return UITextKey.ArtifactChronosTimepieceDesc;
                default: return string.Empty;
            }
        }

        /// <summary>Gold price in the Second Chance shop.</summary>
        public static int GetPrice(ArtifactType type)
        {
            switch (type)
            {
                case ArtifactType.SphereOfForesight: return GameConfig.ArtifactSphereOfForesightPrice;
                case ArtifactType.ChronosTimepiece: return GameConfig.ArtifactChronosTimepiecePrice;
                default: return 0;
            }
        }

        /// <summary>
        /// Artifact that must be owned before this one is offered, or null. The timepiece builds on
        /// the sphere: seeing the future comes before changing it.
        /// </summary>
        public static ArtifactType? GetPrerequisite(ArtifactType type)
        {
            switch (type)
            {
                case ArtifactType.ChronosTimepiece: return ArtifactType.SphereOfForesight;
                default: return null;
            }
        }

        /// <summary>True for a value the catalog knows.</summary>
        public static bool IsValid(int ordinal)
        {
            return ordinal >= 0 && ordinal < Count;
        }
    }
}
