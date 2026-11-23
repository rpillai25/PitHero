using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Services.Replay.Frames;
using RolePlayingFramework.Combat;
using RolePlayingFramework.Equipment;
using RolePlayingFramework.Stats;

namespace PitHero.Tests
{
    /// <summary>Issue #458: which attack visual each weapon plays, and the RotSprite frame op that records it.</summary>
    [TestClass]
    public class AttackVisualTests
    {
        [TestMethod]
        public void For_MapsEachWeaponKind()
        {
            Assert.AreEqual(AttackVisual.Punch, AttackVisuals.For(null), "unarmed");
            Assert.AreEqual(AttackVisual.Slash, AttackVisuals.For(GearItems.ShortSword()));
            Assert.AreEqual(AttackVisual.Stab, AttackVisuals.For(GearItems.RustyDagger()));
            Assert.AreEqual(AttackVisual.Hammer, AttackVisuals.For(GearItems.Mallet()));
            Assert.AreEqual(AttackVisual.Rod, AttackVisuals.For(GearItems.WalkingStick()));
            Assert.AreEqual(AttackVisual.Punch, AttackVisuals.For(MakeGear(ItemKind.WeaponKnuckle)));
            Assert.AreEqual(AttackVisual.Arrow, AttackVisuals.For(MakeGear(ItemKind.WeaponBow)));
            Assert.AreEqual(AttackVisual.Staff, AttackVisuals.For(MakeGear(ItemKind.WeaponStaff)));
            Assert.AreEqual(AttackVisual.Rod, AttackVisuals.For(MakeGear(ItemKind.WeaponRod)));
        }

        [TestMethod]
        public void SpearsAndAxes_OverrideTheirSwordKind_AndKeepItWhenTierScaled()
        {
            var spear = GearItems.WoodenSpear();
            var axe = GearItems.MinersAxe();
            Assert.AreEqual(ItemKind.WeaponSword, spear.Kind, "kind is unchanged");
            Assert.AreEqual(AttackVisual.Thrust, AttackVisuals.For(spear));
            Assert.AreEqual(AttackVisual.AxeSwing, AttackVisuals.For(axe));
            Assert.AreEqual(AttackVisual.Thrust, AttackVisuals.For(Gear.CreateTierScaledCopy(spear, 3, 50)));
            Assert.AreEqual(AttackVisual.AxeSwing, AttackVisuals.For(Gear.CreateTierScaledCopy(axe, 2, 25)));
        }

        [TestMethod]
        public void OnlyArrowAndRodAreRanged()
        {
            for (int v = 0; v <= (int)AttackVisual.Staff; v++)
                Assert.AreEqual(v == (int)AttackVisual.Arrow || v == (int)AttackVisual.Rod, AttackVisuals.IsRanged((AttackVisual)v), ((AttackVisual)v).ToString());
        }

        [TestMethod]
        public void RotSpriteOp_RoundTrip_AndSkip()
        {
            var w = new FrameWriter(new byte[8]);
            w.WriteRotSprite(42, 10.4f, -3.6f, System.MathF.PI / 2f, 0.25f, 0, 0x11223344u, FrameOpFlags.FlipY);
            w.WriteRect(1f, 2f, 3f, 4f, 0x55667788u, FrameOpFlags.None);
            Assert.AreEqual(1 + FrameOpCode.RotSpritePayload + 1 + FrameOpCode.RectPayload, w.Length);

            var r = new FrameReader(w.Buffer, 0, w.Length);
            Assert.AreEqual(FrameOpCode.RotSprite, r.ReadOpCode());
            r.ReadRotSprite(out var op);
            Assert.AreEqual((ushort)42, op.SpriteId);
            Assert.AreEqual((short)10, op.X);
            Assert.AreEqual((short)-4, op.Y);
            Assert.AreEqual((ushort)16384, op.Rotation, "a quarter turn");
            Assert.AreEqual(System.MathF.PI / 2f, op.Radians, 1e-4f);
            Assert.AreEqual(0.25f, op.LayerDepth);
            Assert.AreEqual((short)0, op.RenderLayer);
            Assert.AreEqual(0x11223344u, op.Color);
            Assert.AreEqual(FrameOpFlags.FlipY, op.Flags);

            var skip = new FrameReader(w.Buffer, 0, w.Length);
            skip.SkipOp();
            Assert.AreEqual(FrameOpCode.Rect, skip.ReadOpCode());
        }

        [TestMethod]
        public void QuantizeRotation_WrapsNegativeAndFullTurns()
        {
            Assert.AreEqual((ushort)0, FrameWriter.QuantizeRotation(0f));
            Assert.AreEqual((ushort)0, FrameWriter.QuantizeRotation(System.MathF.PI * 2f));
            Assert.AreEqual((ushort)32768, FrameWriter.QuantizeRotation(System.MathF.PI));
            Assert.AreEqual((ushort)32768, FrameWriter.QuantizeRotation(-System.MathF.PI));
            Assert.AreEqual((ushort)49152, FrameWriter.QuantizeRotation(-System.MathF.PI / 2f));
        }

        /// <summary>
        /// The attack effects are cropped to their content, so each sprite's origin is its anchor (where the
        /// game places it), not the center. A repack without -originsMap resets every origin to 0.5,0.5 and
        /// silently shifts every effect; this fails first. Repack recipe: docs/ParticleEffects.md.
        /// </summary>
        [TestMethod]
        public void SkillsStencilsAtlas_AttackEffects_ExistWithAnchorOrigins()
        {
            var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
            string atlasPath = null;
            while (dir != null && atlasPath == null)
            {
                var candidate = System.IO.Path.Combine(dir.FullName, "PitHero", "Content", "Atlases", "SkillsStencils.atlas");
                if (System.IO.File.Exists(candidate))
                    atlasPath = candidate;
                dir = dir.Parent;
            }
            Assert.IsNotNull(atlasPath, "Could not locate PitHero/Content/Atlases/SkillsStencils.atlas");

            var lines = System.IO.File.ReadAllLines(atlasPath);
            var anims = new System.Collections.Generic.List<string>();
            for (int v = 1; v <= (int)AttackVisual.Staff; v++)
                anims.Add(PitHero.Util.AttackEffects.AnimationName((AttackVisual)v));
            anims.Add(PitHero.Util.AttackEffects.RodBoltAnimation);
            anims.Add(PitHero.Util.AttackEffects.RodBurstAnimation);
            int attackCount = anims.Count;
            foreach (PitHero.Util.SkillEffect e in System.Enum.GetValues(typeof(PitHero.Util.SkillEffect)))
            {
                if (e == PitHero.Util.SkillEffect.None) continue;
                var name = PitHero.Util.AttackEffects.AnimationName(e);
                Assert.IsNotNull(name, e + " has no animation name in AttackEffects.SkillNames");
                anims.Add(name);
            }

            for (int a = 0; a < anims.Count; a++)
            {
                Assert.IsNotNull(anims[a], "attack visual " + (a + 1) + " has no animation name");
                bool animFound = false;
                int sprites = 0;
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (line == anims[a])
                        animFound = true;
                    if (line.StartsWith(anims[a] + "_") && i + 2 < lines.Length)
                    {
                        sprites++;
                        // Attack effects are all anchored off-center, so a default origin means the map was reset.
                        // (Some skill effects are centered on their anchor, so 0.5,0.5 is correct for them.)
                        if (a < attackCount)
                            Assert.AreNotEqual("0.5,0.5", lines[i + 2].Trim(),
                                line + " has the default origin: repack SkillsStencils with -originsMap (see docs/ParticleEffects.md)");
                    }
                }
                Assert.IsTrue(animFound, anims[a] + " animation missing from SkillsStencils.atlas");
                Assert.IsTrue(sprites > 0, anims[a] + " has no sprites in SkillsStencils.atlas");
            }
        }

        private static Gear MakeGear(ItemKind kind)
            => new Gear("Inv_TestWeapon_Name", kind, ItemRarity.Normal, "Inv_TestWeapon_Desc", 1, new StatBlock(0, 0, 0, 0));
    }
}
