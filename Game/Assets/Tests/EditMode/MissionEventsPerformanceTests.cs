using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HistoryHeroes.Common;
using HistoryHeroes.Economy;
using HistoryHeroes.Missions;
using HistoryHeroes.Records;
using NUnit.Framework;
using UnityEngine;

namespace HistoryHeroes.Tests
{
    public sealed class MissionEventsPerformanceTests
    {
        private static readonly PropertyInfo PracticeProperty = typeof(GameSession).GetProperty(nameof(GameSession.Practice));
        private readonly TimeSpan _kst = TimeSpan.FromHours(9);
        private Func<DateTimeOffset> _originalClock;
        private bool _originalPractice;
        private MissionTable _table;
        private string _folder;
        private DateTimeOffset _now;

        [SetUp]
        public void SetUp()
        {
            _originalClock = Clock.Now;
            _originalPractice = GameSession.Practice;
            SetPractice(false);
            _folder = Path.Combine(Path.GetTempPath(), "hh_mission_event_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);
            SaveSystem.Use(new SaveStore(_folder));
            _table = ScriptableObject.CreateInstance<MissionTable>();
            _table.dailyFixed.Add(new MissionDef { id = "kill", title = "처치 {0}", eventKey = MissionEvents.Kill, target = 1, points = 10 });
            _table.dailyFixed.Add(new MissionDef { id = "elite", title = "정예 {0}", eventKey = MissionEvents.Elite, target = 1, points = 10 });
            _table.weekly.Add(new MissionDef { id = "weekly_kill", title = "주간 처치 {0}", eventKey = MissionEvents.Kill, target = 1, points = 20 });
            MissionEvents.Table = _table;
            MissionEvents.Context = null;
            _now = new DateTimeOffset(2026, 10, 26, 4, 59, 0, _kst);
            Clock.Now = () => _now;
        }

        [TearDown]
        public void TearDown()
        {
            MissionEvents.Table = null;
            MissionEvents.Context = null;
            Clock.Now = _originalClock;
            SetPractice(_originalPractice);
            SaveSystem.Use(null);
            UnityEngine.Object.DestroyImmediate(_table);
            Directory.Delete(_folder, true);
        }

        [Test]
        public void KillEvents_RollDailyAndWeeklyAtKstFiveWithoutEarlyReset()
        {
            var notices = new List<string>();
            MissionEvents.Completed += notices.Add;
            try
            {
                MissionEvents.Raise(MissionEvents.Kill);
                Assert.AreEqual("2026-10-25", SaveSystem.Data.missions.dayKey);
                Assert.AreEqual("2026-10-19", SaveSystem.Data.missions.weekKey);
                Assert.AreEqual(2, notices.Count);
                string dayKey = SaveSystem.Data.missions.dayKey;
                string weekKey = SaveSystem.Data.missions.weekKey;

                notices.Clear();
                _now = _now.AddSeconds(59);
                MissionEvents.Raise(MissionEvents.Kill);
                Assert.AreEqual("2026-10-25", SaveSystem.Data.missions.dayKey);
                Assert.AreEqual("2026-10-19", SaveSystem.Data.missions.weekKey);
                Assert.AreSame(dayKey, SaveSystem.Data.missions.dayKey);
                Assert.AreSame(weekKey, SaveSystem.Data.missions.weekKey);
                Assert.IsEmpty(notices, "경계 전 같은 날의 완료 알림은 되풀이되지 않는다");

                _now = _now.AddSeconds(1);
                MissionEvents.Raise(MissionEvents.Kill);
                Assert.AreEqual("2026-10-26", SaveSystem.Data.missions.dayKey);
                Assert.AreEqual("2026-10-26", SaveSystem.Data.missions.weekKey);
                Assert.AreEqual(2, notices.Count, "새 날과 새 주의 미션이 각각 한 번 완료된다");
            }
            finally { MissionEvents.Completed -= notices.Add; }
        }

        [Test]
        public void CompletionListener_RaisingAnotherEvent_DeliversEachNoticeOnce()
        {
            var notices = new List<string>();
            Action<string> listener = line =>
            {
                notices.Add(line);
                if (notices.Count == 1) MissionEvents.Raise(MissionEvents.Elite);
            };
            MissionEvents.Completed += listener;
            try
            {
                MissionEvents.Raise(MissionEvents.Kill);
                Assert.AreEqual(3, notices.Count);
                StringAssert.StartsWith("미션 완료  처치", notices[0]);
                StringAssert.StartsWith("미션 완료  정예", notices[1]);
                StringAssert.StartsWith("주간 미션 완료", notices[2]);
                Assert.AreEqual(20, SaveSystem.Data.missions.dailyPoints);
                Assert.AreEqual(20, SaveSystem.Data.missions.weeklyPoints);
                MissionEvents.Raise(MissionEvents.Kill);
                Assert.AreEqual(3, notices.Count);
            }
            finally { MissionEvents.Completed -= listener; }
        }

        [Test]
        public void CachedDate_RechecksResetHourClockRollbackAndSaveReplacement()
        {
            MissionEvents.Raise(MissionEvents.Kill);
            Assert.AreEqual("2026-10-25", SaveSystem.Data.missions.dayKey);

            _table.resetHour = 4;
            MissionEvents.Raise(MissionEvents.Kill);
            Assert.AreEqual("2026-10-26", SaveSystem.Data.missions.dayKey);
            Assert.AreEqual("2026-10-26", SaveSystem.Data.missions.weekKey);

            _table.resetHour = 5;
            _now = _now.AddHours(-1);
            MissionEvents.Raise(MissionEvents.Kill);
            Assert.AreEqual("2026-10-25", SaveSystem.Data.missions.dayKey);
            Assert.AreEqual("2026-10-19", SaveSystem.Data.missions.weekKey);

            SaveData oldSave = SaveSystem.Data;
            SaveSystem.Use(new SaveStore(_folder));
            MissionEvents.Raise(MissionEvents.Kill);
            Assert.AreNotSame(oldSave, SaveSystem.Data);
            Assert.AreEqual("2026-10-25", SaveSystem.Data.missions.dayKey);
            Assert.AreEqual(10, SaveSystem.Data.missions.dailyPoints);
        }

        private static void SetPractice(bool value)
        {
            Assert.IsNotNull(PracticeProperty);
            MethodInfo setter = PracticeProperty.GetSetMethod(true);
            Assert.IsNotNull(setter);
            setter.Invoke(null, new object[] { value });
        }
    }
}
