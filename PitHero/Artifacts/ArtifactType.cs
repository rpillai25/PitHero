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

        /// <summary>Unlocks the 4X and 8X fast-forward rungs.</summary>
        KairosMetronome = 2,
    }

    /// <summary>Static facts about each artifact: sprite, text keys, price and purchase prerequisite.</summary>
    public static class ArtifactCatalog
    {
        /// <summary>Number of artifact kinds (the enum is dense from 0).</summary>
        public const int Count = 3;

        /// <summary>Sprite name in the Items atlas.</summary>
        public static string GetSpriteName(ArtifactType type)
        {
            switch (type)
            {
                case ArtifactType.SphereOfForesight: return "SphereOfForesight";
                case ArtifactType.ChronosTimepiece: return "ChronosTimepiece";
                case ArtifactType.KairosMetronome: return "KairosMetronome";
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
                case ArtifactType.KairosMetronome: return UITextKey.ArtifactKairosMetronomeName;
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
                case ArtifactType.KairosMetronome: return UITextKey.ArtifactKairosMetronomeDesc;
                default: return string.Empty;
            }
        }

        /// <summary>
        /// UI.txt key of an optional second paragraph shown under the description — the mechanical
        /// effect, kept off the flavor line. Empty when the artifact's card is a single paragraph.
        /// </summary>
        public static string GetEffectKey(ArtifactType type)
        {
            switch (type)
            {
                case ArtifactType.KairosMetronome: return UITextKey.ArtifactKairosMetronomeEffect;
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
                case ArtifactType.KairosMetronome: return GameConfig.ArtifactKairosMetronomePrice;
                default: return 0;
            }
        }

        private static readonly ArtifactType[] NoPrerequisites = new ArtifactType[0];

        private static readonly ArtifactType[] TimepiecePrerequisites =
            { ArtifactType.SphereOfForesight, ArtifactType.KairosMetronome };

        /// <summary>
        /// Every artifact that must be owned before this one is offered, or an empty array. The
        /// timepiece is the capstone: seeing the future and hurrying it along both come before
        /// changing it.
        /// </summary>
        public static ArtifactType[] GetPrerequisites(ArtifactType type)
        {
            switch (type)
            {
                case ArtifactType.ChronosTimepiece: return TimepiecePrerequisites;
                default: return NoPrerequisites;
            }
        }

        /// <summary>True for a value the catalog knows.</summary>
        public static bool IsValid(int ordinal)
        {
            return ordinal >= 0 && ordinal < Count;
        }
    }
}
