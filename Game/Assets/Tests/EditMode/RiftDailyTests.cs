using System;
using System.Collections.Generic;
using HistoryHeroes.Common;
using HistoryHeroes.Dungeon;
using HistoryHeroes.Modes;
using HistoryHeroes.Records;
using NUnit.Framework;
using UnityEngine;

namespace HistoryHeroes.Tests
{
    /// <summary>v0.6 무한의 균열 (Modes 4장) · 일일 도전 (Modes 5장) 규칙</summary>
    public sealed class RiftDailyTests
    {
        private static readonly TimeSpan Kst = TimeSpan.FromHours(9);

        // 2026-10-26은 월요일
        private static DateTimeOffset At(int month, int day, int hour, int minute = 0) => new DateTimeOffset(2026, month, day, hour, minute, 0, Kst);

        // ---------------------------------------------------------------- 무한의 균열

        [Test]
        public void Rift_FloorKinds()
        {
            Assert.IsTrue(RiftRules.IsEliteFloor(5));
            Assert.IsTrue(RiftRules.IsEliteFloor(15));
            Assert.IsFalse(RiftRules.IsEliteFloor(10), "10의 배수는 보스 층");
            Assert.IsTrue(RiftRules.IsBossFloor(10));
            Assert.IsTrue(RiftRules.IsBossFloor(50));
            Assert.IsFalse(RiftRules.IsBossFloor(0));

            Assert.AreEqual(RoomKind.Boss, RiftRules.PlanFor(10).kind);
            Assert.AreEqual(RoomKind.Elite, RiftRules.PlanFor(25).kind);
            Assert.AreEqual(RoomKind.Normal, RiftRules.PlanFor(7).kind);
            Assert.LessOrEqual(RiftRules.PlanFor(200).budget, 24, "적 수는 상한이 있다 (세기는 배율로)");
        }

        [Test]
        public void Rift_ChapterAndBossOrder()
        {
            Assert.AreEqual(1, RiftRules.ChapterFor(1));
            Assert.AreEqual(1, RiftRules.ChapterFor(10));
            Assert.AreEqual(2, RiftRules.ChapterFor(11));
            Assert.AreEqual(5, RiftRules.ChapterFor(41));
            Assert.AreEqual(5, RiftRules.ChapterFor(120), "41층부터는 늘 5구간 적");

            Assert.AreEqual(0, RiftRules.BossIndex(10));
            Assert.AreEqual(1, RiftRules.BossIndex(20));
            Assert.AreEqual(4, RiftRules.BossIndex(50));
            Assert.AreEqual(0, RiftRules.BossIndex(60), "50층 다음은 처음 보스부터");
        }

        [Test]
        public void Rift_ScalingGrowsEveryFloor()
        {
            Assert.AreEqual(Mathf.Pow(1.06f, 10f), RiftRules.HpScale(10), 1e-4f);
            Assert.AreEqual(Mathf.Pow(1.04f, 10f), RiftRules.AttackScale(10), 1e-4f);
            for (int f = 1; f < 80; f++)
            {
                Assert.Greater(RiftRules.HpScale(f + 1), RiftRules.HpScale(f));
                Assert.Greater(RiftRules.AttackScale(f + 1), RiftRules.AttackScale(f));
            }
        }

        [Test]
        public void Rift_FloorRewardsOncePerWeek()
        {
            var save = new SaveData();
            RiftRules.EnsureWeek(save, At(10, 27, 12));

            List<string> lines = RiftRules.ReachFloor(save, 25);
            Assert.AreEqual(2, lines.Count, "10층·20층 보상");
            Assert.AreEqual(2000, save.gold);
            Assert.AreEqual(10, save.ItemCount(Relics.RelicTable.AncientShard));
            Assert.AreEqual(25, save.rift.weeklyBest);

            Assert.AreEqual(0, RiftRules.ReachFloor(save, 25).Count, "같은 층 다시 = 보상 없음");
            Assert.AreEqual(0, RiftRules.ReachFloor(save, 12).Count, "낮은 층은 최고 층을 내리지 않는다");
            Assert.AreEqual(25, save.rift.weeklyBest);
            Assert.AreEqual(1, RiftRules.ReachFloor(save, 30).Count, "30층 보상만 새로");
            Assert.AreEqual(2000 + 4000, save.gold);
        }

        [Test]
        public void Rift_WeekResetsAtMonday0500()
        {
            var save = new SaveData();
            Assert.IsTrue(RiftRules.EnsureWeek(save, At(10, 27, 12)));
            RiftRules.ReachFloor(save, 22);
            save.rift.checkpoint = new RiftCheckpoint { valid = true, floor = 20, seed = 42, heroId = "khan" };

            Assert.IsFalse(RiftRules.EnsureWeek(save, At(11, 2, 4, 59)), "다음 주 월요일 04:59는 아직 이번 주");
            Assert.AreEqual(22, save.rift.weeklyBest);

            Assert.IsTrue(RiftRules.EnsureWeek(save, At(11, 2, 5, 0)), "월요일 05:00에 새 주");
            Assert.AreEqual(0, save.rift.weeklyBest);
            Assert.AreEqual(0, save.rift.rewardsClaimed.Count);
            Assert.IsFalse(save.rift.checkpoint.valid, "체크포인트도 주간 시즌과 함께 사라진다");
            Assert.AreEqual(22, save.rift.bestEver, "역대 최고는 남는다");
            Assert.AreEqual(1, RiftRules.ReachFloor(save, 10).Count, "새 주에는 층 보상을 다시 받는다");
        }

        [Test]
        public void Rift_WeeklyVariantIsStable()
        {
            string week = RiftRules.WeekKey(At(10, 28, 9));
            Assert.AreEqual(RiftRules.VariantFor(week), RiftRules.VariantFor(RiftRules.WeekKey(At(11, 1, 23))), "같은 주 = 같은 변형");
            var seen = new HashSet<RiftRules.Variant>();
            for (int w = 0; w < 40; w++) seen.Add(RiftRules.VariantFor(RiftRules.WeekKey(At(10, 26, 12).AddDays(7 * w))));
            Assert.GreaterOrEqual(seen.Count, 3, "주마다 변형이 바뀐다");
        }

        [Test]
        public void Rift_UnlocksAfterChapter3Boss()
        {
            Assert.IsFalse(RiftRules.Unlocked(new SaveData { unlockedChapter = 3 }));
            Assert.IsTrue(RiftRules.Unlocked(new SaveData { unlockedChapter = 4 }));
            Assert.IsFalse(DailyRules.Unlocked(new SaveData { unlockedChapter = 3 }));
            Assert.IsTrue(DailyRules.Unlocked(new SaveData { unlockedChapter = 4 }));
        }

        // ---------------------------------------------------------------- 일일 도전

        [Test]
        public void Daily_SameDaySameRun()
        {
            string morning = DailyRules.DayKey(At(10, 27, 6));
            string night = DailyRules.DayKey(At(10, 28, 4, 59));
            string next = DailyRules.DayKey(At(10, 28, 5));
            Assert.AreEqual(morning, night, "05:00 전까지는 같은 날");
            Assert.AreNotEqual(morning, next);

            Assert.AreEqual(DailyRules.Seed(morning), DailyRules.Seed(night));
            Assert.AreNotEqual(DailyRules.Seed(morning), DailyRules.Seed(next));
            CollectionAssert.AreEqual(DailyRules.ModifiersFor(morning), DailyRules.ModifiersFor(night));

            // 같은 시드 → 같은 방 난수 (모두 같은 판)
            RunRandom.Begin(DailyRules.Seed(morning));
            var a = new List<int>();
            for (int i = 0; i < 20; i++) a.Add(RunRandom.Rooms.Next(1000));
            RunRandom.Begin(DailyRules.Seed(night));
            for (int i = 0; i < 20; i++) Assert.AreEqual(a[i], RunRandom.Rooms.Next(1000));
        }

        [Test]
        public void Daily_TwoDifferentModifiersEachDay()
        {
            for (int d = 0; d < 60; d++)
            {
                List<DailyRules.Modifier> mods = DailyRules.ModifiersFor(DailyRules.DayKey(At(10, 26, 12).AddDays(d)));
                Assert.AreEqual(2, mods.Count);
                Assert.AreNotEqual(mods[0], mods[1]);
                Assert.IsFalse(DailyRules.Conflicts(mods[0], mods[1]), "서로 뒤집는 규칙은 같은 날 안 나온다");
            }
        }

        [Test]
        public void Daily_PlanHasSevenRooms()
        {
            List<RoomPlan> plan = DailyRules.Plan();
            Assert.AreEqual(7, plan.Count);
            Assert.AreEqual(4, plan.FindAll(r => r.kind == RoomKind.Normal).Count);
            Assert.AreEqual(1, plan.FindAll(r => r.kind == RoomKind.Elite).Count);
            Assert.AreEqual(1, plan.FindAll(r => r.kind == RoomKind.Reward).Count);
            Assert.AreEqual(RoomKind.MidBoss, plan[6].kind, "마지막 방 = 중간 보스");
        }

        [Test]
        public void Daily_HeroAndChapterRotate()
        {
            var heroes = new List<string> { "khan", "guan", "napoleon", "tesla" };
            var seen = new HashSet<string>();
            var chapters = new HashSet<int>();
            for (int d = 0; d < 8; d++)
            {
                string day = DailyRules.DayKey(At(10, 26, 12).AddDays(d));
                string hero = DailyRules.HeroFor(day, heroes);
                Assert.Contains(hero, heroes);
                seen.Add(hero);
                int chapter = DailyRules.ChapterFor(day);
                Assert.That(chapter, Is.InRange(1, 5));
                chapters.Add(chapter);
            }

            Assert.AreEqual(4, seen.Count, "4일 안에 영웅이 한 바퀴");
            Assert.AreEqual(5, chapters.Count, "5일 안에 구간이 한 바퀴");
        }

        [Test]
        public void Daily_Score()
        {
            // 7방 × 100 + 처치 50 + 체력 50% × 2 + 클리어 500 + (600 − 300) × 2
            Assert.AreEqual(700 + 50 + 100 + 500 + 600, DailyRules.Score(7, 50, 0.5f, true, 300f));
            // 죽으면 클리어·시간 보너스 없음
            Assert.AreEqual(300 + 20, DailyRules.Score(3, 20, 0f, false, 100f));
            // 10분 넘기면 시간 보너스 0
            Assert.AreEqual(700 + 500 + 200, DailyRules.Score(7, 0, 1f, true, 900f));
        }

        [Test]
        public void Daily_OfficialRecordedOnce()
        {
            var save = new SaveData();
            DailyRules.EnsureDay(save, At(10, 27, 12));
            List<string> first = DailyRules.RecordOfficial(save, "guan", 1234, true);
            Assert.AreEqual(1, first.Count);
            Assert.AreEqual(DailyRules.RewardGold, save.gold);
            Assert.AreEqual(1, save.ItemCount(Economy.Wallet.HeroShard("guan")));

            Assert.AreEqual(0, DailyRules.RecordOfficial(save, "guan", 9999, true).Count, "공식은 첫 시도만");
            Assert.AreEqual(1234, save.dailyChallenge.officialScore);
            Assert.AreEqual(1, save.dailyChallenge.participations);
            Assert.AreEqual(1, save.dailyChallenge.history.Count);

            Assert.IsTrue(DailyRules.EnsureDay(save, At(10, 28, 5)), "다음 날 05:00에 다시 열린다");
            Assert.IsFalse(save.dailyChallenge.officialDone);
            DailyRules.RecordOfficial(save, "tesla", 800, false);
            Assert.AreEqual(2, save.dailyChallenge.participations);
            Assert.AreEqual("tesla", save.dailyChallenge.history[0].heroId, "최근 기록이 앞");
        }

        [Test]
        public void Daily_InterruptedOfficialCountsAsZero()
        {
            var heroes = new List<string> { "khan", "guan", "napoleon", "tesla" };
            var save = new SaveData();
            DailyRules.EnsureDay(save, At(10, 27, 12));
            Assert.IsTrue(DailyRules.OfficialAvailable(save));
            Assert.AreEqual(0, DailyRules.SettleInterrupted(save, heroes).Count, "시작 안 했으면 정리할 것 없음");

            DailyRules.MarkOfficialStarted(save); // 시작 → 앱을 꺼 버림
            Assert.IsFalse(DailyRules.OfficialAvailable(save), "시작했으면 다시 못 한다");
            Assert.AreEqual(1, DailyRules.SettleInterrupted(save, heroes).Count);
            Assert.IsTrue(save.dailyChallenge.officialDone);
            Assert.AreEqual(0, save.dailyChallenge.officialScore);
            Assert.AreEqual(1, save.dailyChallenge.participations, "참여는 인정");
            Assert.AreEqual(DailyRules.HeroFor(save.dailyChallenge.dayKey, heroes), save.dailyChallenge.history[0].heroId);
            Assert.AreEqual(0, DailyRules.SettleInterrupted(save, heroes).Count, "두 번 정리하지 않는다");

            DailyRules.EnsureDay(save, At(10, 28, 6));
            Assert.IsTrue(DailyRules.OfficialAvailable(save), "다음 날 다시 열린다");
        }

        [Test]
        public void Daily_FixedSetIsFair()
        {
            MetaSnapshot set = DailyRules.FixedSet();
            Assert.AreEqual(15, set.heroLevel);
            Assert.AreEqual(1, set.heroStars);
            Assert.AreEqual(3, set.equipment.Count);
            Assert.IsTrue(set.equipment.TrueForAll(e => e.level == 0));
            Assert.AreEqual(0, set.training.Count);
            Assert.AreEqual(0, set.relics.Count);
            Assert.IsTrue(string.IsNullOrEmpty(set.petId), "펫 없음");
        }
    }
}
