using System;
using HistoryHeroes.Economy;
using HistoryHeroes.Missions;
using HistoryHeroes.Records;
using NUnit.Framework;

namespace HistoryHeroes.Tests
{
    /// <summary>v1.0 출석부 (Missions 5장) · 신규 7일 미션 (6장) · 순찰 (LiveOps 8장)</summary>
    public sealed class NewcomerTests
    {
        private static readonly TimeSpan Kst = TimeSpan.FromHours(9);
        private static DateTimeOffset At(int day, int hour = 12) => new DateTimeOffset(2026, 11, day, hour, 0, 0, Kst);

        private static SaveData Fresh(bool tutorial = true) => new SaveData { tutorialDone = tutorial };

        // ---------------------------------------------------------------- 출석부

        [Test]
        public void Attendance_LockedUntilTutorial()
        {
            SaveData s = Fresh(false);
            Assert.AreEqual(0, Attendance.CheckIn(s, At(1)).Count);
            Assert.AreEqual(0, s.attendance.days);
        }

        [Test]
        public void Attendance_OncePerDay_At0500()
        {
            SaveData s = Fresh();
            Assert.AreEqual(1, Attendance.CheckIn(s, At(1, 6)).Count);
            Assert.AreEqual(0, Attendance.CheckIn(s, At(2, 4)).Count, "다음 날 04:00은 아직 같은 날");
            Assert.AreEqual(1, Attendance.CheckIn(s, At(2, 5)).Count, "05:00에 새 날");
            Assert.AreEqual(2, s.attendance.days);
            Assert.AreEqual(1000, s.gold);
        }

        [Test]
        public void Attendance_CountsDaysNotStreak_SeventhGivesTicket()
        {
            SaveData s = Fresh();
            int[] days = { 1, 3, 4, 8, 10, 11, 20 }; // 빠진 날이 있어도 이어서
            foreach (int d in days) Attendance.CheckIn(s, At(d));
            Assert.AreEqual(7, s.attendance.days);
            Assert.AreEqual(7, Attendance.CycleDay(s));
            Assert.AreEqual(1, s.ItemCount(Wallet.Ticket), "7일째 🎫1");
            Attendance.CheckIn(s, At(21));
            Assert.AreEqual(1, Attendance.CycleDay(s), "다시 1일째부터");
        }

        [Test]
        public void Attendance_Cumulative14And28()
        {
            SaveData s = Fresh();
            for (int d = 1; d <= 14; d++) Attendance.CheckIn(s, At(d));
            Assert.AreEqual(20, s.ItemCount(Wallet.GearShard), "14일 🔩20");
            for (int d = 15; d <= 28; d++) Attendance.CheckIn(s, At(d));
            Assert.AreEqual(60, s.ItemCount(Wallet.GearShard), "28일 🔩40 더");
            Assert.Contains(Attendance.SteadyTitle, s.titles);
        }

        // ---------------------------------------------------------------- 신규 7일 미션

        [Test]
        public void Newcomer_DaysOpenOneByOne_AndExpireAfter14()
        {
            SaveData s = Fresh();
            Assert.IsTrue(Newcomer.EnsureStarted(s, At(1)));
            Assert.IsFalse(Newcomer.EnsureStarted(s, At(2)), "한 번만 시작");
            Assert.IsTrue(Newcomer.DayOpen(s, 1, At(1)));
            Assert.IsFalse(Newcomer.DayOpen(s, 2, At(1)));
            Assert.IsTrue(Newcomer.DayOpen(s, 2, At(2)));
            Assert.IsTrue(Newcomer.DayOpen(s, 1, At(9)), "지난 칸도 계속");
            Assert.IsTrue(Newcomer.Visible(s, At(14)));
            Assert.IsFalse(Newcomer.Visible(s, At(15)), "14일이 지나면 끝");
        }

        [Test]
        public void Newcomer_Day1_ClaimWhenAllDone()
        {
            SaveData s = Fresh();
            Newcomer.EnsureStarted(s, At(1));
            Assert.IsFalse(Newcomer.CanClaim(s, 1, At(1)));
            s.stars.Add(Stars.Key(1, Stars.Dungeon, 1));
            s.records.Add(new RunRecord { id = "r1" });
            s.unlockedChapter = 2;
            Assert.IsTrue(Newcomer.DayComplete(s, 1));
            Assert.IsNotNull(Newcomer.ClaimDay(s, 1, At(1)));
            Assert.AreEqual(3000, s.gold);
            Assert.IsNull(Newcomer.ClaimDay(s, 1, At(1)), "두 번 못 받는다");
        }

        [Test]
        public void Newcomer_EventCounters()
        {
            SaveData s = Fresh();
            Newcomer.OnEvent(s, MissionEvents.Fusion, 1, null, At(1));
            Assert.AreEqual(0, Newcomer.Count(s, MissionEvents.Fusion), "시작 전에는 세지 않는다");
            Newcomer.EnsureStarted(s, At(1));
            Newcomer.OnEvent(s, MissionEvents.Fusion, 1, null, At(1));
            Newcomer.OnEvent(s, MissionEvents.HeroDungeon, 1, "guan", At(2));
            Newcomer.OnEvent(s, MissionEvents.Kill, 5, null, At(2));
            Assert.AreEqual(1, Newcomer.Count(s, MissionEvents.Fusion));
            Assert.AreEqual(1, Newcomer.Count(s, "hero_dungeon:guan"));
            Assert.AreEqual(0, Newcomer.Count(s, MissionEvents.Kill), "7일 미션에 없는 사건은 세지 않는다");
        }

        [Test]
        public void Newcomer_LockedModeUsesDungeonFallback()
        {
            SaveData s = Fresh();
            Newcomer.EnsureStarted(s, At(1));
            NewcomerMission survival = Array.Find(Newcomer.Missions, m => m.Id == "d5_survival");
            s.unlockedChapter = 2; // 서바이벌은 3구간부터
            Assert.IsTrue(Newcomer.UsesFallback(s, survival));
            Assert.AreEqual(Newcomer.FallbackTitle, Newcomer.TitleOf(s, survival));
            Assert.IsFalse(Newcomer.Done(s, survival));
            Newcomer.OnEvent(s, MissionEvents.DungeonClear, 1, "khan", At(5));
            Assert.IsTrue(Newcomer.Done(s, survival), "대신 던전 1판 클리어");
            s.unlockedChapter = 3;
            Assert.IsFalse(Newcomer.Done(s, survival), "열리면 원래 미션");
        }

        [Test]
        public void Newcomer_BonusAfterAllSevenDays()
        {
            SaveData s = Fresh();
            Newcomer.EnsureStarted(s, At(1));
            for (int d = 1; d <= 6; d++) s.newcomer.claimedDays.Add(d);
            Assert.IsFalse(Newcomer.CanClaimBonus(s, At(8)));
            s.newcomer.claimedDays.Add(7);
            Assert.IsNotNull(Newcomer.ClaimBonus(s, At(8)));
            Assert.AreEqual(3, s.ItemCount(Wallet.Ticket));
            Assert.IsFalse(Newcomer.Visible(s, At(8)), "다 받으면 로비에서 사라진다");
        }

        [Test]
        public void Newcomer_TotalsMatchDesign()
        {
            int gems = Newcomer.Bonus.Gems, tickets = Newcomer.Bonus.Tickets, gold = 0, shards = Newcomer.Bonus.GearShards;
            foreach (Reward r in Newcomer.DayRewards)
            {
                gems += r.Gems;
                tickets += r.Tickets;
                gold += r.Gold;
                shards += r.GearShards;
            }

            Assert.AreEqual(700, gems);
            Assert.AreEqual(5, tickets);
            Assert.AreEqual(15000, gold);
            Assert.AreEqual(60, shards);
        }

        // ---------------------------------------------------------------- 순찰

        [Test]
        public void Patrol_AccumulatesAndCapsAt12Hours()
        {
            var s = new SaveData { unlockedChapter = 2 }; // 1구간 보스 격파
            const long t0 = 1_800_000_000;
            Assert.IsTrue(Patrol.Ensure(s, t0));
            Assert.AreEqual(170, Patrol.GoldPerHour(s));
            Assert.AreEqual(170, Patrol.Pending(s, t0 + 3600));
            Assert.AreEqual(170 * 12, Patrol.Pending(s, t0 + 3600 * 30), "12시간까지만");
            Assert.IsTrue(Patrol.Full(s, t0 + 3600 * 12));
            Assert.AreEqual(170 * 12, Patrol.Claim(s, t0 + 3600 * 30));
            Assert.AreEqual(0, Patrol.Pending(s, t0 + 3600 * 30), "받으면 다시 0부터");
        }

        [Test]
        public void Patrol_LockedBeforeChapter1Boss_AndClockRollbackIsZero()
        {
            var s = new SaveData { unlockedChapter = 1 };
            Assert.IsFalse(Patrol.Ensure(s, 1000));
            Assert.AreEqual(0, Patrol.Pending(s, 99999));

            s.unlockedChapter = 6; // 5구간까지 깸
            Patrol.Ensure(s, 100_000);
            Assert.AreEqual(250, Patrol.GoldPerHour(s));
            Assert.IsTrue(Patrol.Ensure(s, 50_000), "시계를 되돌리면 그 자리부터 다시");
            Assert.AreEqual(0, Patrol.Pending(s, 50_000));
            Assert.AreEqual(250, Patrol.Pending(s, 53_600));
        }

        // ---------------------------------------------------------------- 로컬 알림 (LiveOps 9장)

        [Test]
        public void Notices_QuietHoursMoveTo0800()
        {
            Assert.AreEqual(new DateTimeOffset(2026, 11, 3, 8, 0, 0, Kst), Common.LocalNotices.OutOfQuiet(new DateTimeOffset(2026, 11, 2, 22, 30, 0, Kst)));
            Assert.AreEqual(new DateTimeOffset(2026, 11, 2, 8, 0, 0, Kst), Common.LocalNotices.OutOfQuiet(new DateTimeOffset(2026, 11, 2, 3, 0, 0, Kst)));
            var noon = new DateTimeOffset(2026, 11, 2, 12, 0, 0, Kst);
            Assert.AreEqual(noon, Common.LocalNotices.OutOfQuiet(noon), "낮에는 그대로");
        }

        [Test]
        public void Notices_PlanHeartsAndPatrol_RespectToggles()
        {
            DateTimeOffset now = new DateTimeOffset(2026, 11, 2, 10, 0, 0, Kst);
            long unix = now.ToUnixTimeSeconds();
            var s = new SaveData { hearts = 3, heartsUpdatedAt = unix, unlockedChapter = 2 };
            s.patrol.lastClaimAt = unix;
            var plan = Common.LocalNotices.Plan(s, now);
            Assert.AreEqual(2, plan.Count, "하루 최대 2번 (종류마다 하나)");
            var hearts = plan.Find(p => p.Id == Common.LocalNotices.HeartsId);
            Assert.AreEqual(now.AddSeconds(2 * Hearts.RegenSeconds), hearts.At, "하트 2개 = 40분 뒤");
            var patrol = plan.Find(p => p.Id == Common.LocalNotices.PatrolId);
            Assert.AreEqual(new DateTimeOffset(2026, 11, 3, 8, 0, 0, Kst), patrol.At, "12시간 뒤 22:00 → 다음 날 08:00");

            s.settings.notifyHearts = false;
            s.settings.notifyPatrol = false;
            Assert.AreEqual(0, Common.LocalNotices.Plan(s, now).Count);
        }
    }
}
