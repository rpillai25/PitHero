using Microsoft.Xna.Framework;

namespace PitHero.ECS.Components
{
    /// <summary>
    /// Manages hero Hair layer sprite animations based on movement direction using the Actors.atlas
    /// </summary>
    public class HeroHairAnimationComponent : HeroAnimationComponent
    {
        private readonly int _hairstyleIndex;

        /// <summary>
        /// Creates a new HeroHairAnimationComponent with the specified color and hairstyle
        /// </summary>
        /// <param name="color">The color tint for the hair</param>
        /// <param name="hairstyleIndex">The hairstyle index (1-based), where 1 is the default hairstyle</param>
        public HeroHairAnimationComponent(Color color, int hairstyleIndex = 1) : base(color)
        {
            _hairstyleIndex = hairstyleIndex;
        }

        /// <summary>Gets the hairstyle suffix for animation names (empty string for hairstyle 1, "2" for hairstyle 2, etc.)</summary>
        private string HairstyleSuffix => _hairstyleIndex == 1 ? "" : _hairstyleIndex.ToString();

        protected override string DefaultAnimation => $"MaleHeroHair{HairstyleSuffix}WalkDown";
        protected override string AnimDown => $"MaleHeroHair{HairstyleSuffix}WalkDown";
        protected override string AnimLeft => $"MaleHeroHair{HairstyleSuffix}WalkRight";   // Flipped in code
        protected override string AnimRight => $"MaleHeroHair{HairstyleSuffix}WalkRight";
        protected override string AnimUp => $"MaleHeroHair{HairstyleSuffix}WalkUp";
        protected override string JumpAnimDown => $"MaleHeroHair{HairstyleSuffix}JumpRight";
        protected override string JumpAnimLeft => $"MaleHeroHair{HairstyleSuffix}JumpRight";  // Flipped in code
        protected override string JumpAnimRight => $"MaleHeroHair{HairstyleSuffix}JumpRight";
        protected override string JumpAnimUp => $"MaleHeroHair{HairstyleSuffix}JumpRight";
    }
}