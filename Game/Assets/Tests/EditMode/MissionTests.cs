using System;
using System.Collections.Generic;
using System.IO;
using HistoryHeroes.Missions;
using HistoryHeroes.Records;
using NUnit.Framework;
using UnityEngine;

namespace HistoryHeroes.Tests
{
    /// <summary>v0.5 미션·업적 (Missions 2·3·7장)</summary>
    public sealed class MissionTests
    {
        private MissionTable _table;
        private static readonly TimeSpan Kst = TimeSpan.FromHours(9);

        private static MissionDef M(string id, string key, int target, int points, MissionGate gate = MissionGate.None,
            bool mode = false, MissionDays days = MissionDays.All, bool distinct = false, bool heroOfDay = false, string fallback = null)
        {
            return new MissionDef
            {
                id = id, title = id + " {0}", eventKey = key, target = target, points = points, gate = gate,
                modeMission = mode, days = days, distinct = distinct, heroOfDay = heroOfDay, fallbackId = fallback,
            };
        }

        [SetUp]
        public void SetUp()
        {
            _table = ScriptableObject.CreateInstance<MissionTable>();
            _table.dailyFixed.Add(M("login", MissionEvents.Login, 1, 10));
            _table.dailyFixed.Add(M("run2", MissionEvents.Run, 2, 20));
            _table.dailyPool.Add(M("elite3", MissionEvents.Elite, 3, 15));
            _table.dailyPool.Add(M("kill300", MissionEvents.Kill, 300, 15));
            _table.dailyPool.Add(M("nohit3", MissionEvents.NoHitRoom, 3, 20));
            _table.dailyPool.Add(M("basic150", MissionEvents.BasicKill, 150, 15));
            _table.dailyPool.Add(M("fusion1", MissionEvents.Fusion, 1, 20));
            _table.dailyPool.Add(M("heroday", MissionEvents.HeroDungeon, 1, 20, mode: true, days: MissionDays.Weekdays, heroOfDay: true));
            _table.dailyPool.Add(M("boss1", MissionEvents.BossTry, 1, 20, MissionGate.BossRecord, mode: true));
            _table.dailyPool.Add(M("survival5", MissionEvents.Survival5, 1, 20, MissionGate.Survival, mode: true, days: MissionDays.Sat));
            _table.dailyPool.Add(M("rift5", "rift_floor5", 1, 20, MissionGate.Never, mode: true, days: MissionDays.Sun));
            _table.weekly.Add(M("box4", MissionEvents.DailyBox100, 4, 20));
            _table.weekly.Add(M("twoheroes", MissionEvents.DungeonClear, 2, 20, distinct: true));
            _table.weekly.Add(M("recipe1", MissionEvents.RecipeNew, 1, 20, fallback: "fusion3"));
            _table.weekly.Add(M("survival20", MissionEvents.SurvivalSeconds, 1200, 20, MissionGate.Survival, fallback: "dungeon3"));
            _table.weeklyFallbacks.Add(M("fusion3", MissionEvents.Fusion, 3, 20));
            _table.weeklyFallbacks.Add(M("dungeon3", MissionEvents.DungeonClear, 3, 20));
            _table.dailyChests.Add(new ChestDef { points = 20, gold = 200 });
            _table.dailyChests.Add(new ChestDef { points = 100, gems = 25, ancientShards = 5 });
            _table.weeklyChests.Add(new ChestDef { points = 40, gold = 2000, gems = 10 });
            _table.achievements.Add(new AchievementDef { id = "recipes", kind = AchievementKind.Recipes, targets = new[] { 1, 3, -1 }, gems = new[] { 30, 60, 200 } });
            _table.achievements.Add(new AchievementDef { id = "basic", kind = AchievementKind.BasicKills, targets = new[] { 2 }, gems = new[] { 30 } });
        }

        private static DateTimeOffset At(int year, int month, int day, int hour, int minute = 0) => new DateTimeOffset(year, month, day, hour, minute, 0, Kst);

        [Test]
        public void Keys_ResetAtFiveAndMonday()
        {
            // 2026-10-26은 월요일
            Assert.AreEqual("2026-10-25", MissionService.DayKey(At(2026, 10, 26, 4, 59), 5), "05:00 전은 어제");
            Assert.AreEqual("2026-10-26", MissionService.DayKey(At(2026, 10, 26, 5, 0), 5));
            Assert.AreEqual("2026-10-19", MissionService.WeekKey(At(2026, 10, 26, 4, 59), 5), "월요일 05:00 전은 지난주");
            Assert.AreEqual("2026-10-26", MissionService.WeekKey(At(2026, 10, 26, 5, 0), 5));
            Assert.AreEqual("2026-10-26", MissionService.WeekKey(At(2026, 11, 1, 23, 0), 5), "일요일 밤도 같은 주");
            Assert.AreEqual(MissionDays.Sun, MissionService.DayFlag(At(2026, 10, 26, 4, 0), 5), "월요일 새벽 4시 = 일요일");
        }

        [Test]
        public void NormalizePoints_AlwaysHitsTotalInFives()
        {
            CollectionAssert.AreEqual(new[] { 20, 20, 15, 15 }, MissionService.NormalizePoints(new[] { 15, 15, 15, 15 }, 70));
            CollectionAssert.AreEqual(new[] { 20, 20, 15, 15 }, MissionService.NormalizePoints(new[] { 20, 20, 15, 15 }, 70));
            int[] r = MissionService.NormalizePoints(new[] { 20, 20, 20, 20 }, 70);
            Assert.AreEqual(70, r[0] + r[1] + r[2] + r[3]);
            foreach (int p in r) Assert.AreEqual(0, p % 5);
        }

        [Test]
        public void GenerateDaily_SixMissionsHundredPointsOneModeMissionGatesRespected()
        {
            var save = new SaveData { unlockedChapter = 1 };
            for (int day = 0; day < 14; day++)
            {
                DateTimeOffset now = At(2026, 10, 26, 12).AddDays(day);
                MissionService.GenerateDaily(save, _table, now, null);
                List<MissionProgress> d = save.missions.daily;
                Assert.AreEqual(6, d.Count);
                int sum = 0, modes = 0;
                foreach (MissionProgress m in d)
                {
                    sum += m.points;
                    MissionDef def = _table.FindDaily(m.id);
                    if (def.modeMission) modes++;
                    Assert.AreNotEqual("survival5", m.id, "서바이벌은 2구간 보스 뒤");
                    Assert.AreNotEqual("rift5", m.id, "없는 모드");
                    Assert.AreNotEqual("boss1", m.id, "기록이 없으면 보스맵 미션 없음");
                }

                Assert.AreEqual(100, sum, "하루 합계는 늘 100");
                Assert.LessOrEqual(modes, 1, "모드 미션은 하루 최대 1개");
            }

            // 같은 날은 같은 미션 (다시 만들어도)
            MissionService.GenerateDaily(save, _table, At(2026, 10, 27, 9), null);
            var first = save.missions.daily.ConvertAll(m => m.id);
            MissionService.GenerateDaily(save, _table, At(2026, 10, 27, 20), null);
            CollectionAssert.AreEqual(first, save.missions.daily.ConvertAll(m => m.id));
        }

        [Test]
        public void Raise_CompletesOnceAndAddsPoints()
        {
            var save = new SaveData();
            MissionService.Ensure(save, _table, At(2026, 10, 26, 12), null);
            Assert.AreEqual(1, MissionService.Raise(save, MissionEvents.Login, 1, null).Count);
            Assert.AreEqual(10, save.missions.dailyPoints);
            Assert.AreEqual(0, MissionService.Raise(save, MissionEvents.Login, 1, null).Count, "한 번만");
            MissionService.Raise(save, MissionEvents.Run, 1, null);
            Assert.AreEqual(10, save.missions.dailyPoints, "2판 중 1판");
            MissionService.Raise(save, MissionEvents.Run, 1, null);
            Assert.AreEqual(30, save.missions.dailyPoints);
        }

        [Test]
        public void Weekly_DistinctHeroesAndFallbacks()
        {
            var save = new SaveData { unlockedChapter = 1 };
            MissionService.GenerateWeekly(save, _table, At(2026, 10, 26, 12), new MissionService.Context { TotalRecipes = 2 });
            Assert.IsTrue(save.missions.weekly.Exists(m => m.id == "dungeon3"), "서바이벌이 안 열렸으면 던전 3판");
            Assert.IsTrue(save.missions.weekly.Exists(m => m.id == "recipe1"));

            MissionProgress two = save.missions.weekly.Find(m => m.id == "twoheroes");
            MissionService.Raise(save, MissionEvents.DungeonClear, 1, "khan");
            MissionService.Raise(save, MissionEvents.DungeonClear, 1, "khan");
            Assert.AreEqual(1, two.progress, "같은 영웅은 한 번만");
            MissionService.Raise(save, MissionEvents.DungeonClear, 1, "guan");
            Assert.IsTrue(two.done);
            Assert.AreEqual(3, save.missions.weekly.Find(m => m.id == "dungeon3").progress, "대체 미션은 판마다 센다");

            save.discoveredRecipes.AddRange(new[] { "a", "b" });
            save.unlockedChapter = 3;
            MissionService.GenerateWeekly(save, _table, At(2026, 11, 2, 12), new MissionService.Context { TotalRecipes = 2 });
            Assert.IsTrue(save.missions.weekly.Exists(m => m.id == "fusion3"), "조합을 다 찾았으면 조합 3회");
            Assert.IsTrue(save.missions.weekly.Exists(m => m.id == "survival20"));
        }

        [Test]
        public void HeroOfDay_OnlyThatHeroAndAnyHeroWhenLocked()
        {
            var save = new SaveData();
            MissionService.GenerateDaily(save, _table, At(2026, 10, 26, 12), null);
            var m = new MissionProgress { id = "heroday", eventKey = MissionEvents.HeroDungeon, target = 1, points = 20, param = "guan" };
            save.missions.daily.Add(m);
            MissionService.Raise(save, MissionEvents.HeroDungeon, 1, "khan");
            Assert.IsFalse(m.done, "오늘의 영웅이 아니면 안 센다");
            MissionService.Raise(save, MissionEvents.HeroDungeon, 1, "guan");
            Assert.IsTrue(m.done);

            var ctx = new MissionService.Context { HeroUnlocked = id => id == "khan" };
            string hero = MissionService.HeroOfDay(_table, At(2026, 10, 26, 12), ctx);
            Assert.IsTrue(hero == null || hero == "khan", "안 연 영웅이면 아무 영웅 (대체 규칙 a)");
        }

        [Test]
        public void Chests_NeedPointsOnceAndHundredCountsForWeekly()
        {
            var save = new SaveData();
            MissionService.Ensure(save, _table, At(2026, 10, 26, 12), null);
            ChestDef small = _table.dailyChests[0];
            ChestDef big = _table.dailyChests[1];
            Assert.IsFalse(MissionService.ClaimDaily(save, _table, small, "khan"), "포인트 모자람");
            save.missions.dailyPoints = 100;
            int gold = save.gold;
            Assert.IsTrue(MissionService.ClaimDaily(save, _table, small, "khan"));
            Assert.AreEqual(gold + 200, save.gold);
            Assert.IsFalse(MissionService.ClaimDaily(save, _table, small, "khan"), "한 번만");
            Assert.IsTrue(MissionService.ClaimDaily(save, _table, big, "khan"));
            Assert.AreEqual(5, save.ItemCount(Relics.RelicTable.AncientShard));
            Assert.AreEqual(1, save.missions.weekly.Find(m => m.id == "box4").progress, "100 상자 = 주간 '상자 4일' 1일");
        }

        [Test]
        public void Reroll_OncePerDayKeepsPointsNotFixed()
        {
            var save = new SaveData();
            DateTimeOffset now = At(2026, 10, 27, 12);
            MissionService.Ensure(save, _table, now, null);
            Assert.IsFalse(MissionService.Reroll(save, _table, 0, now, null), "고정 미션은 못 바꾼다");
            string before = save.missions.daily[2].id;
            int points = save.missions.daily[2].points;
            Assert.IsTrue(MissionService.Reroll(save, _table, 2, now, null));
            Assert.AreNotEqual(before, save.missions.daily[2].id);
            Assert.AreEqual(points, save.missions.daily[2].points, "포인트는 그대로 (합계 100 유지)");
            var ids = save.missions.daily.ConvertAll(m => m.id);
            Assert.AreEqual(ids.Count, new HashSet<string>(ids).Count, "겹치지 않는다");
            Assert.IsFalse(MissionService.Reroll(save, _table, 3, now, null), "하루 1번");
        }

        [Test]
        public void Achievements_TiersAndAllRecipesTarget()
        {
            var save = new SaveData();
            var ctx = new MissionService.Context { TotalRecipes = 5 };
            AchievementDef recipes = _table.FindAchievement("recipes");
            Assert.IsFalse(MissionService.CanClaim(save, recipes, ctx));
            save.discoveredRecipes.Add("a");
            int gems = Economy.Wallet.Gems(save);
            Assert.IsTrue(MissionService.ClaimAchievement(save, recipes, ctx));
            Assert.AreEqual(gems + 30, Economy.Wallet.Gems(save));
            Assert.IsFalse(MissionService.CanClaim(save, recipes, ctx), "다음 단계는 3개");
            save.discoveredRecipes.AddRange(new[] { "b", "c" });
            Assert.IsTrue(MissionService.ClaimAchievement(save, recipes, ctx));
            Assert.AreEqual(5, MissionService.Target(recipes, 2, ctx), "마지막 단계 = 전부");
            Assert.IsFalse(MissionService.CanClaim(save, recipes, ctx));
            save.discoveredRecipes.AddRange(new[] { "d", "e" });
            Assert.IsTrue(MissionService.ClaimAchievement(save, recipes, ctx));
            Assert.IsFalse(MissionService.CanClaim(save, recipes, ctx), "다 받음");
            Assert.AreEqual(3, MissionService.ClaimedTiers(save, "recipes"));

            save.missions.counters.basicKills = 2;
            Assert.AreEqual(1, MissionService.ClaimableCount(save, _table, ctx), "멈춤의 달인 1단계");
        }

        [Test]
        public void Load_OldSaveWithoutMissions_GetsEmptyMissions()
        {
            string folder = Path.Combine(Path.GetTempPath(), "hh_miss_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                var store = new SaveStore(folder);
                string json = JsonUtility.ToJson(new SaveData { gold = 3 }, true).Replace("\"missions\"", "\"missions_old\"");
                File.WriteAllText(store.MainPath, json);
                SaveData loaded = store.Load();
                Assert.IsNotNull(loaded.missions);
                Assert.IsNotNull(loaded.missions.counters);
                Assert.IsNotNull(loaded.missions.counters.bossWinRecords);
                Assert.IsNotNull(loaded.missions.daily);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }
    }
}
