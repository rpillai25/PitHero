using PitHero.Farming;
using RolePlayingFramework.Combat;

namespace PitHero.Dining
{
    /// <summary>One crop ingredient of a dish recipe.</summary>
    public readonly struct RecipeEntry
    {
        public readonly CropType Crop;
        public readonly int Qty;

        public RecipeEntry(CropType crop, int qty)
        {
            Crop = crop;
            Qty = qty;
        }
    }

    /// <summary>One day-long buff granted by a dish.</summary>
    public readonly struct DishBuffEntry
    {
        public readonly BuffType Type;
        public readonly int Magnitude;

        public DishBuffEntry(BuffType type, int magnitude)
        {
            Type = type;
            Magnitude = magnitude;
        }
    }

    /// <summary>How long a dish takes to cook (base in-game minutes = real seconds).</summary>
    public enum CookTimeClass
    {
        Simple,
        Standard,
        Complex,
    }

    /// <summary>How long a dish takes to eat (in-game minutes = real seconds).</summary>
    public enum EatTimeClass
    {
        Snack,
        Meal,
        Feast,
    }

    /// <summary>
    /// Static metadata for one dish: recipe, effects, timing classes and sprite naming.
    /// Milk/cheese are free pre-cow ingredients — they are display-only flags, never
    /// recipe entries, and contribute nothing to price or storage checks.
    /// </summary>
    public sealed class DishDefinition
    {
        public readonly DishType Type;
        public readonly RecipeEntry[] Recipe;
        public readonly bool UsesMilk;
        public readonly bool UsesCheese;
        public readonly DishBuffEntry[] Buffs;
        public readonly CookTimeClass CookClass;
        public readonly EatTimeClass EatClass;

        /// <summary>Base sprite name in the CropsProps atlas ("_Large" for UI, "_Small" for world).</summary>
        public readonly string BaseSpriteName;

        /// <summary>UITextKey constant for the localized dish name.</summary>
        public readonly string NameKey;

        public DishDefinition(DishType type, RecipeEntry[] recipe, bool usesMilk, bool usesCheese,
            DishBuffEntry[] buffs,
            CookTimeClass cookClass, EatTimeClass eatClass, string baseSpriteName, string nameKey)
        {
            Type = type;
            Recipe = recipe;
            UsesMilk = usesMilk;
            UsesCheese = usesCheese;
            Buffs = buffs;
            CookClass = cookClass;
            EatClass = eatClass;
            BaseSpriteName = baseSpriteName;
            NameKey = nameKey;
        }
    }
}
