using Microsoft.VisualStudio.TestTools.UnitTesting;
using PitHero;
using PitHero.Config;
using PitHero.Services;

namespace PitHero.Tests
{
    [TestClass]
    public class MonsterScheduleConfigTests
    {
        private static InGameTimeService TimeAtHour(int hour)
        {
            var time = new InGameTimeService();
            time.SetAccumulatedTime(hour * 60f); // 60 real seconds per in-game hour
            return time;
        }

        [TestMethod]
        [TestCategory("MonsterSchedule")]
        public void IsNocturnal_NightShiftMonsters_ReturnTrue()
        {
            Assert.IsTrue(MonsterScheduleConfig.IsNocturnal(MonsterTextKey.Monster_Orc));
            Assert.IsTrue(MonsterScheduleConfig.IsNocturnal(MonsterTextKey.Monster_Skeleton));
            Assert.IsTrue(MonsterScheduleConfig.IsNocturnal(MonsterTextKey.Monster_GhostMiner));
        }

        [TestMethod]
        [TestCategory("MonsterSchedule")]
        public void IsNocturnal_DayShiftMonsters_ReturnFalse()
        {
            Assert.IsFalse(MonsterScheduleConfig.IsNocturnal(MonsterTextKey.Monster_Slime));
            Assert.IsFalse(MonsterScheduleConfig.IsNocturnal(MonsterTextKey.Monster_CaveMushroom));
            Assert.IsFalse(MonsterScheduleConfig.IsNocturnal(MonsterTextKey.Monster_Goblin));
            Assert.IsFalse(MonsterScheduleConfig.IsNocturnal(MonsterTextKey.Monster_Rat));
            Assert.IsFalse(MonsterScheduleConfig.IsNocturnal(MonsterTextKey.Monster_CrystalGolem));
        }

        [TestMethod]
        [TestCategory("MonsterSchedule")]
        public void IsNocturnal_AcceptsBareName()
        {
            Assert.IsTrue(MonsterScheduleConfig.IsNocturnal("Orc"));
            Assert.IsFalse(MonsterScheduleConfig.IsNocturnal("Slime"));
        }

        [TestMethod]
        [TestCategory("MonsterSchedule")]
        public void IsAsleep_DaytimeMonster_SleepsAtNight()
        {
            // Daytime monster (Slime): awake at noon, asleep at 11 PM
            Assert.IsFalse(MonsterScheduleConfig.IsAsleep(MonsterTextKey.Monster_Slime, TimeAtHour(12)));
            Assert.IsTrue(MonsterScheduleConfig.IsAsleep(MonsterTextKey.Monster_Slime, TimeAtHour(23)));
        }

        [TestMethod]
        [TestCategory("MonsterSchedule")]
        public void IsAsleep_NocturnalMonster_SleepsDuringDay()
        {
            // Nocturnal monster (Orc): asleep at noon, awake at 11 PM
            Assert.IsTrue(MonsterScheduleConfig.IsAsleep(MonsterTextKey.Monster_Orc, TimeAtHour(12)));
            Assert.IsFalse(MonsterScheduleConfig.IsAsleep(MonsterTextKey.Monster_Orc, TimeAtHour(23)));
        }

        [TestMethod]
        [TestCategory("MonsterSchedule")]
        public void IsAsleep_NullTime_TreatedAsAwake()
        {
            Assert.IsFalse(MonsterScheduleConfig.IsAsleep(MonsterTextKey.Monster_Slime, null));
            Assert.IsFalse(MonsterScheduleConfig.IsAsleep(MonsterTextKey.Monster_Orc, null));
        }
    }
}
