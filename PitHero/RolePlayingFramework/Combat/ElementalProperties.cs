using System.Collections.Generic;

namespace RolePlayingFramework.Combat
{
    /// <summary>
    /// Provides elemental properties including type and resistance/weakness mappings.
    /// Handles elemental matchup calculations for damage multipliers.
    /// </summary>
    public sealed class ElementalProperties
    {
        /// <summary>The element type of this entity, gear, or skill.</summary>
        public ElementType Element { get; }

        /// <summary>
        /// Dictionary of elemental resistances and weaknesses.
        /// Positive values indicate resistance (damage reduction), negative values indicate weakness (damage increase).
        /// </summary>
        public IReadOnlyDictionary<ElementType, float> Resistances { get; }

        /// <summary>
        /// Creates an elemental properties instance with the specified element type.
        /// </summary>
        /// <param name="element">The element type.</param>
        public ElementalProperties(ElementType element)
            : this(element, new Dictionary<ElementType, float>())
        {
        }

        /// <summary>
        /// Creates an elemental properties instance with the specified element type and custom resistances.
        /// </summary>
        /// <param name="element">The element type.</param>
        /// <param name="resistances">Dictionary of custom resistances/weaknesses.</param>
        public ElementalProperties(ElementType element, Dictionary<ElementType, float> resistances)
        {
            Element = element;
            Resistances = resistances;
        }

        /// <summary>
        /// Gets the opposing element for a given element type.
        /// </summary>
        /// <param name="element">The element to check.</param>
        /// <returns>The opposing element, or Neutral if no opposition exists.</returns>
        public static ElementType GetOpposingElement(ElementType element)
        {
            return element switch
            {
                ElementType.Fire => ElementType.Water,
                ElementType.Water => ElementType.Fire,
                ElementType.Earth => ElementType.Wind,
                ElementType.Wind => ElementType.Earth,
                ElementType.Light => ElementType.Dark,
                ElementType.Dark => ElementType.Light,
                _ => ElementType.Neutral
            };
        }

        /// <summary>
        /// Calculates the damage multiplier when an attack of the specified element hits a target with this element.
        /// </summary>
        /// <param name="attackElement">The element of the attacking skill or entity.</param>
        /// <param name="targetElement">The element of the defending entity.</param>
        /// <returns>
        /// 2.0 if attack element opposes target element (advantage),
        /// 0.5 if attack element matches target element (disadvantage),
        /// 1.0 otherwise (neutral).
        /// </returns>
        /// <remarks>
        /// This method provides base elemental matchup logic. For complete damage calculation
        /// with custom resistances, use BalanceConfig.GetElementalDamageMultiplier which wraps
        /// this method and applies custom resistance modifiers from ElementalProperties.
        /// </remarks>
        public static float GetElementalMultiplier(ElementType attackElement, ElementType targetElement)
        {
            // Neutral attacks or neutral targets have no modifier
            if (attackElement == ElementType.Neutral || targetElement == ElementType.Neutral)
            {
                return 1.0f;
            }

            // Check if attack element opposes target element (advantage)
            if (GetOpposingElement(attackElement) == targetElement)
            {
                return 2.0f;
            }

            // Check if attack element matches target element (disadvantage)
            if (attackElement == targetElement)
            {
                return 0.5f;
            }

            // No relationship
            return 1.0f;
        }
    }
}
