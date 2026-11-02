using Microsoft.Xna.Framework;
using Nez;
using RolePlayingFramework;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Manages the female hero's armor and shoes, drawn above Body, using the Actors.atlas. Keeps its
    /// painted colors, so the skin tint on Body reaches only skin. Male armor is painted into Body.
    /// </summary>
    public class HeroArmorAnimationComponent : HeroAnimationComponent
    {
        public HeroArmorAnimationComponent()
            : base(Color.White, HeroSpriteNames.Get(Gender.Female, HeroLayer.Armor))
        {
        }

        /// <summary>Adds the layer to a female entity at the given offset; returns null for a male.</summary>
        public static HeroArmorAnimationComponent AddTo(Entity entity, Gender gender, Vector2 offset)
        {
            if (!HeroSpriteNames.HasArmorLayer(gender))
                return null;
            var armor = entity.AddComponent(new HeroArmorAnimationComponent());
            armor.SetLocalOffset(offset);
            return armor;
        }
    }
}
