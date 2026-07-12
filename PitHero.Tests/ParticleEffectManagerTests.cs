using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero.Util;

namespace PitHero.Tests
{
    [TestClass]
    public class ParticleEffectManagerTests
    {
        [TestMethod]
        public void SpawnEffect_BeforeInit_ReturnsNull()
        {
            var manager = new ParticleEffectManager();

            var emitter = manager.SpawnEffect(ParticleEffectType.Heal, new Nez.Entity("target"));

            Assert.IsNull(emitter);
        }

        [TestMethod]
        public void SpawnEffect_NullTarget_ReturnsNull()
        {
            var manager = new ParticleEffectManager();

            var emitter = manager.SpawnEffect(ParticleEffectType.Heal, null);

            Assert.IsNull(emitter);
        }

        [TestMethod]
        public void SpawnEffectAtPosition_BeforeInit_ReturnsNull()
        {
            var manager = new ParticleEffectManager();

            var emitter = manager.SpawnEffectAtPosition(
                ParticleEffectType.Heal, Microsoft.Xna.Framework.Vector2.Zero, null);

            Assert.IsNull(emitter);
        }
    }
}
