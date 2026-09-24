using System;
using System.Collections.Generic;
using Nez;
using Nez.Sprites;
using PitHero.ECS.Components;
using PitHero.Services.Replay.Frames;

namespace PitHero.Tests
{
    /// <summary>
    /// The rule for feature authors (design doc §3.1): every RenderableComponent in PitHero is stock
    /// (SpriteRenderer / SpriteAnimator lineage, or a SpriteCompositorBase composite), IFrameCapturable,
    /// or ILiveOnlyRenderable. The hand-maintained list below is compared against the assembly, so a new
    /// renderable fails here by name until it is classified and added.
    /// </summary>
    [TestClass]
    public class FrameCaptureCoverageTests
    {
        /// <summary>Every RenderableComponent subclass in the PitHero assembly, with how the capture handles it.</summary>
        private static readonly (string TypeName, string Handling)[] Known =
        {
            ("PitHero.ECS.Components.TextRenderComponent", "capturable"),
            ("PitHero.ECS.Components.RisingTextComponent", "capturable"),
            ("PitHero.ECS.Components.BouncyTextComponent", "capturable"),
            ("PitHero.ECS.Components.BouncyDigitComponent", "capturable"),
            ("PitHero.ECS.Components.SpeechBubbleComponent", "capturable"),
            ("PitHero.ECS.Components.MonsterHPBarComponent", "capturable"),
            ("PitHero.ECS.Components.BuildingOutlineRenderComponent", "capturable"),
            ("PitHero.ECS.Components.SelectBoxRenderComponent", "capturable"),
            ("PitHero.ECS.Components.CloudOverlayComponent", "live-only"),
            ("PitHero.ECS.Components.TreeBandComponent", "live-only"),
            ("PitHero.UI.GraphicalHUD", "live-only"),
            ("PitHero.ECS.Components.ActionQueueVisualizationComponent", "live-only"),
            ("PitHero.ECS.Components.SpriteCompositorBase", "stock"),
            ("PitHero.ECS.Components.MultiSpriteAnimator", "stock"),
            ("PitHero.ECS.Components.StaticSpriteCompositor", "stock"),
            ("PitHero.ECS.Components.YSortSpriteRenderer", "stock"),
            ("PitHero.ECS.Components.PausableSpriteAnimator", "stock"),
            ("PitHero.ECS.Components.HeroAnimationComponent", "stock"),
            ("PitHero.ECS.Components.HeroHeadAnimationComponent", "stock"),
            ("PitHero.ECS.Components.HeroHand1AnimationComponent", "stock"),
            ("PitHero.ECS.Components.HeroHand2AnimationComponent", "stock"),
            ("PitHero.ECS.Components.HeroHairAnimationComponent", "stock"),
            ("PitHero.ECS.Components.HeroShirtAnimationComponent", "stock"),
            ("PitHero.ECS.Components.HeroEyesAnimationComponent", "stock"),
            ("PitHero.ECS.Components.HeroPantsAnimationComponent", "stock"),
            ("PitHero.ECS.Components.HeroBodyAnimationComponent", "stock"),
            ("PitHero.ECS.Components.EnemyAnimationComponent", "stock"),
            ("PitHero.ECS.Components.SlimeAnimationComponent", "stock"),
            ("PitHero.ECS.Components.PlaceholderMonsterAnimationComponent", "stock"),
            ("PitHero.ECS.Components.NamedMonsterAnimationComponent", "stock"),
        };

        private static string Classify(Type t)
        {
            if (typeof(ILiveOnlyRenderable).IsAssignableFrom(t)) return "live-only";
            if (typeof(IFrameCapturable).IsAssignableFrom(t)) return "capturable";
            if (typeof(SpriteRenderer).IsAssignableFrom(t) || typeof(SpriteCompositorBase).IsAssignableFrom(t)) return "stock";
            return "unclassified";
        }

        private static List<Type> RenderablesInPitHero()
        {
            var result = new List<Type>();
            var types = typeof(GameConfig).Assembly.GetTypes();
            for (int i = 0; i < types.Length; i++)
            {
                var t = types[i];
                if (typeof(RenderableComponent).IsAssignableFrom(t) && t != typeof(RenderableComponent))
                    result.Add(t);
            }
            return result;
        }

        [TestMethod]
        public void EveryRenderable_IsStock_Capturable_OrLiveOnly()
        {
            var unclassified = new List<string>();
            var renderables = RenderablesInPitHero();
            for (int i = 0; i < renderables.Count; i++)
            {
                if (Classify(renderables[i]) == "unclassified")
                    unclassified.Add(renderables[i].FullName);
            }
            Assert.AreEqual(0, unclassified.Count,
                "New RenderableComponent(s) must be stock, IFrameCapturable or ILiveOnlyRenderable (design doc §3.1): " + string.Join(", ", unclassified));
        }

        [TestMethod]
        public void HandMaintainedList_MatchesTheAssembly()
        {
            var renderables = RenderablesInPitHero();
            var expected = new Dictionary<string, string>();
            for (int i = 0; i < Known.Length; i++)
                expected[Known[i].TypeName] = Known[i].Handling;

            var missing = new List<string>();
            var wrong = new List<string>();
            var seen = new HashSet<string>();
            for (int i = 0; i < renderables.Count; i++)
            {
                var t = renderables[i];
                seen.Add(t.FullName);
                if (!expected.TryGetValue(t.FullName, out var handling))
                {
                    missing.Add(t.FullName + " (" + Classify(t) + ")");
                    continue;
                }
                if (Classify(t) != handling)
                    wrong.Add(t.FullName + ": listed " + handling + ", is " + Classify(t));
            }
            var gone = new List<string>();
            for (int i = 0; i < Known.Length; i++)
            {
                if (!seen.Contains(Known[i].TypeName))
                    gone.Add(Known[i].TypeName);
            }
            Assert.AreEqual(0, missing.Count, "Add to FrameCaptureCoverageTests.Known: " + string.Join(", ", missing));
            Assert.AreEqual(0, wrong.Count, "Classification changed: " + string.Join(", ", wrong));
            Assert.AreEqual(0, gone.Count, "No longer in the assembly: " + string.Join(", ", gone));
        }

        [TestMethod]
        public void CompositeLayers_AreStockButHiddenWhenOwned()
        {
            // Hero paperdoll layers are captured through their MultiSpriteAnimator, never on their own
            Assert.IsTrue(typeof(ICompositeLayer).IsAssignableFrom(typeof(HeroAnimationComponent)));
            Assert.AreEqual("stock", Classify(typeof(HeroAnimationComponent)));
            Assert.AreEqual("stock", Classify(typeof(MultiSpriteAnimator)));
        }
    }
}
