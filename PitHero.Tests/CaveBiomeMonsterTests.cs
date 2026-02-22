using Microsoft.VisualStudio.TestTools.UnitTesting;
using RolePlayingFramework.Enemies;
using PitHero.Config;

namespace PitHero.Tests
{
    [TestClass]
    public class CaveBiomeMonsterTests
    {
        [TestMethod]
        public void AllCaveBiomeMonsters_CanBeCreated()
        {
            // Act - Create all new Cave Biome monsters
            var caveMushroom = new CaveMushroom();
            var stoneBeetle = new StoneBeetle();
            var shadowImp = new ShadowImp();
            var tunnelWorm = new TunnelWorm();
            var fireLizard = new FireLizard();
            var magmaOoze = new MagmaOoze();
            var crystalGolem = new CrystalGolem();
            var caveTroll = new CaveTroll();
            var ghostMiner = new GhostMiner();
            var shadowBeast = new ShadowBeast();
            var lavaDrake = new LavaDrake();
            var stoneWyrm = new StoneWyrm();
            var stoneGuardian = new StoneGuardian();
            var earthElemental = new EarthElemental();
            var moltenTitan = new MoltenTitan();
            var ancientWyrm = new AncientWyrm();

            // Assert - All monsters should be created successfully
            Assert.IsNotNull(caveMushroom);
            Assert.IsNotNull(stoneBeetle);
            Assert.IsNotNull(shadowImp);
            Assert.IsNotNull(tunnelWorm);
            Assert.IsNotNull(fireLizard);
            Assert.IsNotNull(magmaOoze);
            Assert.IsNotNull(crystalGolem);
            Assert.IsNotNull(caveTroll);
            Assert.IsNotNull(ghostMiner);
            Assert.IsNotNull(shadowBeast);
            Assert.IsNotNull(lavaDrake);
            Assert.IsNotNull(stoneWyrm);
            Assert.IsNotNull(stoneGuardian);
            Assert.IsNotNull(earthElemental);
            Assert.IsNotNull(moltenTitan);
            Assert.IsNotNull(ancientWyrm);
        }

        [TestMethod]
        public void CaveBiomeMonsters_HaveCorrectLevels()
        {
            // Act - Create monsters
            var caveMushroom = new CaveMushroom();
            var stoneBeetle = new StoneBeetle();
            var shadowImp = new ShadowImp();
            var tunnelWorm = new TunnelWorm();
            var fireLizard = new FireLizard();
            var magmaOoze = new MagmaOoze();
            var crystalGolem = new CrystalGolem();
            var caveTroll = new CaveTroll();
            var ghostMiner = new GhostMiner();
            var shadowBeast = new ShadowBeast();
            var lavaDrake = new LavaDrake();
            var stoneWyrm = new StoneWyrm();
            var stoneGuardian = new StoneGuardian();
            var earthElemental = new EarthElemental();
            var moltenTitan = new MoltenTitan();
            var ancientWyrm = new AncientWyrm();

            // Assert - Check levels match config
            Assert.AreEqual(2, caveMushroom.Level, "Cave Mushroom should be level 2");
            Assert.AreEqual(4, stoneBeetle.Level, "Stone Beetle should be level 4");
            Assert.AreEqual(7, shadowImp.Level, "Shadow Imp should be level 7");
            Assert.AreEqual(8, tunnelWorm.Level, "Tunnel Worm should be level 8");
            Assert.AreEqual(9, fireLizard.Level, "Fire Lizard should be level 9");
            Assert.AreEqual(11, magmaOoze.Level, "Magma Ooze should be level 11");
            Assert.AreEqual(12, crystalGolem.Level, "Crystal Golem should be level 12");
            Assert.AreEqual(13, caveTroll.Level, "Cave Troll should be level 13");
            Assert.AreEqual(14, ghostMiner.Level, "Ghost Miner should be level 14");
            Assert.AreEqual(16, shadowBeast.Level, "Shadow Beast should be level 16");
            Assert.AreEqual(17, lavaDrake.Level, "Lava Drake should be level 17");
            Assert.AreEqual(18, stoneWyrm.Level, "Stone Wyrm should be level 18");
            Assert.AreEqual(7, stoneGuardian.Level, "Stone Guardian should be level 7");
            Assert.AreEqual(17, earthElemental.Level, "Earth Elemental should be level 17");
            Assert.AreEqual(22, moltenTitan.Level, "Molten Titan should be level 22");
            Assert.AreEqual(27, ancientWyrm.Level, "Ancient Wyrm should be level 27");
        }

        [TestMethod]
        public void CaveBiomeMonsters_HaveCorrectStats()
        {
            // Act - Create monsters
            var caveMushroom = new CaveMushroom();
            var stoneBeetle = new StoneBeetle();
            var shadowImp = new ShadowImp();
            var stoneGuardian = new StoneGuardian();
            var earthElemental = new EarthElemental();
            var moltenTitan = new MoltenTitan();
            var ancientWyrm = new AncientWyrm();

            // Assert - Check HP values for sample monsters
            // Formula: (10 + level * 5) * archetype_multiplier
            Assert.AreEqual(20, caveMushroom.MaxHP, "Cave Mushroom HP (L2, Balanced): 10+2*5=20");
            Assert.AreEqual(45, stoneBeetle.MaxHP, "Stone Beetle HP (L4, Tank): (10+4*5)*1.5=45");
            Assert.AreEqual(33, shadowImp.MaxHP, "Shadow Imp HP (L7, FastFragile): (10+7*5)*0.7=31.5→33");
            Assert.AreEqual(86, stoneGuardian.MaxHP, "Stone Guardian HP (L7, Tank): (10+7*5)*1.5=67.5→86");
            Assert.AreEqual(193, earthElemental.MaxHP, "Earth Elemental HP (L17, Tank): (10+17*5)*1.5=142.5→193");
            Assert.AreEqual(268, moltenTitan.MaxHP, "Molten Titan HP (L22, Tank): (10+22*5)*1.5=180→268");
            Assert.AreEqual(338, ancientWyrm.MaxHP, "Ancient Wyrm HP (L27, Tank): (10+27*5)*1.5=217.5→338");

            // Assert - Check element types
            Assert.AreEqual(RolePlayingFramework.Combat.ElementType.Earth, caveMushroom.Element);
            Assert.AreEqual(RolePlayingFramework.Combat.ElementType.Dark, shadowImp.Element);
            Assert.AreEqual(RolePlayingFramework.Combat.ElementType.Fire, ancientWyrm.Element);
        }

        [TestMethod]
        public void CaveBiomeMonsters_InEnemyLevelConfig()
        {
            // Assert - All new monsters should be in the config
            Assert.IsTrue(EnemyLevelConfig.HasPresetLevel("Cave Mushroom"));
            Assert.IsTrue(EnemyLevelConfig.HasPresetLevel("Stone Beetle"));
            Assert.IsTrue(EnemyLevelConfig.HasPresetLevel("Shadow Imp"));
            Assert.IsTrue(EnemyLevelConfig.HasPresetLevel("Tunnel Worm"));
            Assert.IsTrue(EnemyLevelConfig.HasPresetLevel("Fire Lizard"));
            Assert.IsTrue(EnemyLevelConfig.HasPresetLevel("Magma Ooze"));
            Assert.IsTrue(EnemyLevelConfig.HasPresetLevel("Crystal Golem"));
            Assert.IsTrue(EnemyLevelConfig.HasPresetLevel("Cave Troll"));
            Assert.IsTrue(EnemyLevelConfig.HasPresetLevel("Ghost Miner"));
            Assert.IsTrue(EnemyLevelConfig.HasPresetLevel("Shadow Beast"));
            Assert.IsTrue(EnemyLevelConfig.HasPresetLevel("Lava Drake"));
            Assert.IsTrue(EnemyLevelConfig.HasPresetLevel("Stone Wyrm"));
            Assert.IsTrue(EnemyLevelConfig.HasPresetLevel("Stone Guardian"));
            Assert.IsTrue(EnemyLevelConfig.HasPresetLevel("Earth Elemental"));
            Assert.IsTrue(EnemyLevelConfig.HasPresetLevel("Molten Titan"));
            Assert.IsTrue(EnemyLevelConfig.HasPresetLevel("Ancient Wyrm"));
        }

        [TestMethod]
        public void BossMonsters_HaveCorrectDamageKind()
        {
            // Act - Create boss monsters
            var stoneGuardian = new StoneGuardian();
            var earthElemental = new EarthElemental();
            var moltenTitan = new MoltenTitan();
            var ancientWyrm = new AncientWyrm();

            // Assert - All bosses should do Physical damage
            Assert.AreEqual(RolePlayingFramework.Combat.DamageKind.Physical, stoneGuardian.AttackKind);
            Assert.AreEqual(RolePlayingFramework.Combat.DamageKind.Physical, earthElemental.AttackKind);
            Assert.AreEqual(RolePlayingFramework.Combat.DamageKind.Physical, moltenTitan.AttackKind);
            Assert.AreEqual(RolePlayingFramework.Combat.DamageKind.Physical, ancientWyrm.AttackKind);
        }

        [TestMethod]
        public void MagicUserMonsters_DoMagicDamage()
        {
            // Act - Create magic user monsters
            var magmaOoze = new MagmaOoze();
            var ghostMiner = new GhostMiner();
            var lavaDrake = new LavaDrake();

            // Assert - Magic users should do Magical damage
            Assert.AreEqual(RolePlayingFramework.Combat.DamageKind.Magical, magmaOoze.AttackKind);
            Assert.AreEqual(RolePlayingFramework.Combat.DamageKind.Magical, ghostMiner.AttackKind);
            Assert.AreEqual(RolePlayingFramework.Combat.DamageKind.Magical, lavaDrake.AttackKind);
        }

        [TestMethod]
        public void CaveBiomeMonsters_TakeDamageCorrectly()
        {
            // Arrange
            var monster = new CaveMushroom();
            int initialHP = monster.CurrentHP;

            // Act
            bool died = monster.TakeDamage(10);

            // Assert
            Assert.IsFalse(died, "Low damage should not kill monster");
            Assert.AreEqual(initialHP - 10, monster.CurrentHP, "HP should be reduced by damage amount");

            // Act - Kill the monster
            died = monster.TakeDamage(monster.CurrentHP);

            // Assert
            Assert.IsTrue(died, "Full HP damage should kill monster");
            Assert.AreEqual(0, monster.CurrentHP, "HP should be 0 when dead");
        }
    }
}
