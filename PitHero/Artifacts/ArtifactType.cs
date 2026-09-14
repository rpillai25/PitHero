namespace PitHero.Artifacts
{
    /// <summary>
    /// Artifacts are one-time purchases. Global artifacts belong to the player and are available forever
    /// across every hero and save slot (system save file); Local artifacts belong to the current hero and
    /// live in the regular session save (issue #411). Values are persisted; append only, never renumber.
    /// </summary>
    public enum ArtifactType
    {
        /// <summary>Unlocks the future-simulation region of the replay timeline.</summary>
        SphereOfForesight = 0,

        /// <summary>Unlocks Time Travel Here in replays. Requires the Sphere of Foresight first.</summary>
        ChronosTimepiece = 1,

        /// <summary>Unlocks the 4X and 8X fast-forward rungs.</summary>
        KairosMetronome = 2,

        /// <summary>Local: crops grow twice as fast.</summary>
        FastGrowFertilizer = 3,

        /// <summary>Local: crops grow three times as fast. Requires the Fast Grow Fertilizer first.</summary>
        LightningGrowFertilizer = 4,

        /// <summary>Local: farm and kitchen workers move twice as fast.</summary>
        HermesBoots = 5,
    }

    /// <summary>
    /// Where an artifact's ownership lives. Global = the player (system save, every hero, proof of wealth,
    /// nothing deducted). Local = this hero only (session save, a real purchase that deducts gold).
    /// </summary>
    public enum ArtifactScope
    {
        Global = 0,
        Local = 1,
    }

    /// <summary>Static facts about each artifact: sprite, text keys, price, scope and purchase prerequisite.</summary>
    public static class ArtifactCatalog
    {
        /// <summary>Number of artifact kinds (the enum is dense from 0).</summary>
        public const int Count = 6;

        /// <summary>Sprite name in the Items atlas.</summary>
        public static string GetSpriteName(ArtifactType type)
        {
            switch (type)
            {
                case ArtifactType.SphereOfForesight: return "SphereOfForesight";
                case ArtifactType.ChronosTimepiece: return "ChronosTimepiece";
                case ArtifactType.KairosMetronome: return "KairosMetronome";
                case ArtifactType.FastGrowFertilizer: return "FastGrowFertilizer";
                case ArtifactType.LightningGrowFertilizer: return "LightningGrowFertilizer";
                case ArtifactType.HermesBoots: return "HermesBoots";
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
                case ArtifactType.FastGrowFertilizer: return UITextKey.ArtifactFastGrowFertilizerName;
                case ArtifactType.LightningGrowFertilizer: return UITextKey.ArtifactLightningGrowFertilizerName;
                case ArtifactType.HermesBoots: return UITextKey.ArtifactHermesBootsName;
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
                case ArtifactType.FastGrowFertilizer: return UITextKey.ArtifactFastGrowFertilizerDesc;
                case ArtifactType.LightningGrowFertilizer: return UITextKey.ArtifactLightningGrowFertilizerDesc;
                case ArtifactType.HermesBoots: return UITextKey.ArtifactHermesBootsDesc;
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

        /// <summary>Gold price in the Second Chance shop (wealth to show for Global, the cost for Local).</summary>
        public static int GetPrice(ArtifactType type)
        {
            switch (type)
            {
                case ArtifactType.SphereOfForesight: return GameConfig.ArtifactSphereOfForesightPrice;
                case ArtifactType.ChronosTimepiece: return GameConfig.ArtifactChronosTimepiecePrice;
                case ArtifactType.KairosMetronome: return GameConfig.ArtifactKairosMetronomePrice;
                case ArtifactType.FastGrowFertilizer: return GameConfig.ArtifactFastGrowFertilizerPrice;
                case ArtifactType.LightningGrowFertilizer: return GameConfig.ArtifactLightningGrowFertilizerPrice;
                case ArtifactType.HermesBoots: return GameConfig.ArtifactHermesBootsPrice;
                default: return 0;
            }
        }

        /// <summary>Whether the artifact belongs to the player (Global) or to the current hero (Local).</summary>
        public static ArtifactScope GetScope(ArtifactType type)
        {
            switch (type)
            {
                case ArtifactType.FastGrowFertilizer:
                case ArtifactType.LightningGrowFertilizer:
                case ArtifactType.HermesBoots:
                    return ArtifactScope.Local;
                default:
                    return ArtifactScope.Global;
            }
        }

        /// <summary>True for a hero-specific artifact (session save, price deducted).</summary>
        public static bool IsLocal(ArtifactType type)
        {
            return GetScope(type) == ArtifactScope.Local;
        }

        private static readonly ArtifactType[] NoPrerequisites = new ArtifactType[0];

        private static readonly ArtifactType[] TimepiecePrerequisites =
            { ArtifactType.SphereOfForesight, ArtifactType.KairosMetronome };

        private static readonly ArtifactType[] LightningPrerequisites =
            { ArtifactType.FastGrowFertilizer };

        /// <summary>
        /// Every artifact that must be owned before this one is offered, or an empty array. The
        /// timepiece is the capstone: seeing the future and hurrying it along both come before
        /// changing it. The lightning fertilizer builds on the fast one.
        /// </summary>
        public static ArtifactType[] GetPrerequisites(ArtifactType type)
        {
            switch (type)
            {
                case ArtifactType.ChronosTimepiece: return TimepiecePrerequisites;
                case ArtifactType.LightningGrowFertilizer: return LightningPrerequisites;
                default: return NoPrerequisites;
            }
        }

        /// <summary>
        /// The artifact that is a straight upgrade of this one, or null. Once the upgrade is owned the
        /// weaker piece stays owned (it is still the prerequisite) but drops out of the owned grid —
        /// the lightning fertilizer is the fast one, only better.
        /// </summary>
        public static ArtifactType? GetSupersededBy(ArtifactType type)
        {
            switch (type)
            {
                case ArtifactType.FastGrowFertilizer: return ArtifactType.LightningGrowFertilizer;
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
