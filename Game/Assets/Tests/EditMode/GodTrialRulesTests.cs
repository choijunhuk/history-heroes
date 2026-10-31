using System;
using System.Collections.Generic;
using System.IO;
using HistoryHeroes.Economy;
using HistoryHeroes.Heroes;
using HistoryHeroes.Modes;
using HistoryHeroes.Records;
using NUnit.Framework;
using UnityEngine;

namespace HistoryHeroes.Tests
{
    public sealed class GodTrialRulesTests
    {
        private static readonly DateTimeOffset Sunday = new DateTimeOffset(2026, 11, 1, 23, 59, 0, TimeSpan.FromHours(9));
        private static readonly DateTimeOffset MondayBeforeReset = new DateTimeOffset(2026, 11, 2, 4, 59, 0, TimeSpan.FromHours(9));
        private static readonly DateTimeOffset MondayAfterReset = new DateTimeOffset(2026, 11, 2, 5, 0, 0, TimeSpan.FromHours(9));

        private static SaveData UnlockedSave()
        {
            var save = new SaveData();
            save.defeatedBosses.Add("oblivion_king");
            save.records.Add(new RunRecord { id = "one", heroId = "khan" });
            save.records.Add(new RunRecord { id = "two", heroId = "guan" });
            return save;
        }

        private static GodTrialResult Win(SaveData save, string recordId, string[] actives, DateTimeOffset now, bool practice = false)
        {
            return GodTrialRules.Complete(save, GodTrialRules.ThorTrialId, save.FindRecord(recordId), actives,
                true, practice, true, now);
        }

        [Test]
        public void TrialOpensOnlyAfterOwnKingFirstClear()
        {
            var save = new SaveData { unlockedChapter = 5 };
            Assert.IsFalse(GodTrialRules.IsUnlocked(save, GodTrialRules.ThorTrialId));
            save.defeatedBosses.Add("oblivion_king");
            Assert.IsTrue(GodTrialRules.IsUnlocked(save, GodTrialRules.ThorTrialId));
            Assert.IsFalse(GodTrialRules.IsUnlocked(save, "other_trial"));
        }

        [Test]
        public void PracticeAndUnownedRecordGiveNoRewardOrProgress()
        {
            SaveData save = UnlockedSave();
            Assert.IsTrue(GodTrialRules.TryChooseGoal(save, GodTrialRules.ThorTrialId, Sunday));
            var practice = Win(save, "one", new[] { "arrow" }, Sunday, true);
            Assert.IsFalse(practice.FirstClear);
            Assert.IsTrue(practice.Reward.IsEmpty);
            Assert.IsEmpty(save.godTrials.cleared);
            Assert.AreEqual(0, save.ItemCount(Wallet.HeroShard("thor")));

            var borrowed = GodTrialRules.Complete(save, GodTrialRules.ThorTrialId,
                new RunRecord { id = "borrowed" }, new[] { "arrow" }, true, false, true, Sunday);
            Assert.IsFalse(borrowed.FirstClear);
            Assert.IsEmpty(save.godTrials.cleared);
        }

        [Test]
        public void FirstClearGrantsTenShardsAndTitleOnlyOnce()
        {
            SaveData save = UnlockedSave();
            GodTrialResult first = Win(save, "one", new[] { "arrow" }, Sunday);
            Assert.IsTrue(first.FirstClear);
            Assert.AreEqual(10, first.Reward.HeroShards);
            Assert.AreEqual(10, save.ItemCount(Wallet.HeroShard("thor")));
            CollectionAssert.Contains(save.titles, "천둥의 증인");
            CollectionAssert.Contains(save.godTrials.cleared, GodTrialRules.ThorTrialId);

            GodTrialResult repeat = Win(save, "one", new[] { "arrow" }, Sunday);
            Assert.IsFalse(repeat.FirstClear);
            Assert.AreEqual(300, repeat.Reward.Gold);
            Assert.AreEqual(10, save.ItemCount(Wallet.HeroShard("thor")));
            Assert.AreEqual(1, save.titles.Count);
            Assert.IsEmpty(save.stars);
        }

        [Test]
        public void RepeatSharesExistingDailyBossCap()
        {
            SaveData save = UnlockedSave();
            Win(save, "one", new[] { "arrow" }, Sunday);
            string today = Sunday.ToString("yyyy-MM-dd");
            Assert.AreEqual(300, RunRewards.BossRepeat(save, today).Gold);
            Assert.AreEqual(300, RunRewards.BossRepeat(save, today).Gold);
            Assert.AreEqual(300, Win(save, "one", new[] { "arrow" }, Sunday).Reward.Gold);
            Assert.IsTrue(Win(save, "one", new[] { "arrow" }, Sunday).Reward.IsEmpty);
            Assert.AreEqual(3, save.daily.bossRepeatRewards);
        }

        [Test]
        public void UnbeatenTrialLossRefundsAtMostFiveTimesPerDay()
        {
            SaveData save = UnlockedSave();
            save.hearts = 0;
            for (int i = 0; i < Hearts.RefundsPerDay; i++)
            {
                var result = GodTrialRules.Complete(save, GodTrialRules.ThorTrialId, save.records[0],
                    Array.Empty<string>(), false, false, true, Sunday);
                Assert.IsTrue(result.HeartRefunded);
            }
            Assert.AreEqual(5, save.hearts);
            Assert.IsFalse(GodTrialRules.Complete(save, GodTrialRules.ThorTrialId, save.records[0],
                Array.Empty<string>(), false, false, true, Sunday).HeartRefunded);
            Assert.IsFalse(GodTrialRules.Complete(save, GodTrialRules.ThorTrialId, save.records[0],
                Array.Empty<string>(), false, false, false, MondayAfterReset).HeartRefunded);
        }

        [Test]
        public void GoalNeedsTwoDifferentOwnedRecordsAndTwoDifferentActives()
        {
            SaveData sameRecord = UnlockedSave();
            Assert.IsTrue(GodTrialRules.TryChooseGoal(sameRecord, GodTrialRules.ThorTrialId, Sunday));
            Win(sameRecord, "one", new[] { "blade", "fire" }, Sunday);
            Assert.IsFalse(GodTrialRules.TryChooseGoal(sameRecord, "other_trial", Sunday));
            Assert.AreEqual(0, Win(sameRecord, "one", new[] { "ice", "fire" }, Sunday).WeeklyShards);
            Assert.AreEqual(10, sameRecord.ItemCount(Wallet.HeroShard("thor")));

            SaveData sameBuild = UnlockedSave();
            Assert.IsTrue(GodTrialRules.TryChooseGoal(sameBuild, GodTrialRules.ThorTrialId, Sunday));
            Win(sameBuild, "one", new[] { "blade", "fire" }, Sunday);
            Assert.AreEqual(0, Win(sameBuild, "two", new[] { "blade", "fire" }, Sunday).WeeklyShards);
            Assert.AreEqual(5, Win(sameBuild, "two", new[] { "ice", "fire" }, Sunday).WeeklyShards);
            Assert.AreEqual(0, Win(sameBuild, "two", new[] { "ice", "fire" }, Sunday).WeeklyShards);
            Assert.AreEqual(15, sameBuild.ItemCount(Wallet.HeroShard("thor")));
        }

        [Test]
        public void WeekResetsAtMondayFiveKstButKeepsEarnedShards()
        {
            SaveData save = UnlockedSave();
            GodTrialRules.TryChooseGoal(save, GodTrialRules.ThorTrialId, Sunday);
            Win(save, "one", new[] { "blade" }, Sunday);
            Win(save, "two", new[] { "fire" }, Sunday);
            Assert.AreEqual(15, save.ItemCount(Wallet.HeroShard("thor")));
            Assert.IsFalse(GodTrialRules.EnsureWeek(save, MondayBeforeReset));
            Assert.IsTrue(GodTrialRules.EnsureWeek(save, MondayAfterReset));
            Assert.AreEqual(0, save.godTrials.weeklyShards);
            Assert.AreEqual(15, save.ItemCount(Wallet.HeroShard("thor")));
            Assert.IsTrue(GodTrialRules.TryChooseGoal(save, GodTrialRules.ThorTrialId, MondayAfterReset));
            Assert.AreEqual(0, Win(save, "one", new[] { "blade" }, MondayAfterReset).WeeklyShards);
            Assert.AreEqual(5, Win(save, "two", new[] { "fire" }, MondayAfterReset).WeeklyShards);
        }

        [Test]
        public void FirstWeeklyWinSurvivesRecordReplacement()
        {
            SaveData save = UnlockedSave();
            GodTrialRules.TryChooseGoal(save, GodTrialRules.ThorTrialId, Sunday);
            Win(save, "one", new[] { "blade" }, Sunday);
            save.records.RemoveAt(0);
            save.records.Add(new RunRecord { id = "one", heroId = "tesla" });
            Assert.AreEqual(5, Win(save, "two", new[] { "fire" }, Sunday).WeeklyShards);
        }

        [Test]
        public void AnyTwoDistinctWeeklyWinsCanCompleteGoal()
        {
            SaveData save = UnlockedSave();
            save.records.Add(new RunRecord { id = "three", heroId = "tesla" });
            GodTrialRules.TryChooseGoal(save, GodTrialRules.ThorTrialId, Sunday);
            Win(save, "one", new[] { "a", "b" }, Sunday);
            Assert.AreEqual(0, Win(save, "two", new[] { "a" }, Sunday).WeeklyShards);
            Assert.AreEqual(5, Win(save, "three", new[] { "b" }, Sunday).WeeklyShards);
        }

        [Test]
        public void LockedGodCannotReceiveNormalShardsAndUnlockSpendsThirty()
        {
            SaveData save = UnlockedSave();
            var thor = ScriptableObject.CreateInstance<HeroData>();
            thor.id = "thor";
            thor.gemsOnly = true;
            thor.unlockGems = 2000;
            save.gemsFree = 2000;
            save.gold = 500;
            Assert.IsFalse(HeroUnlock.IsUnlocked(save, thor));
            StringAssert.DoesNotContain("보석", HeroUnlock.LockText(thor));
            Assert.IsFalse(HeroUnlock.TryBuy(save, thor));
            Assert.IsFalse(HeroUnlock.TryClaimFree(save, thor));
            Assert.IsFalse(Meta.HeroGrowth.TryBuyHeroShard(save, "thor", "2026-11-01"));
            Assert.AreEqual(2000, save.gemsFree);
            Assert.AreEqual(500, save.gold);
            Wallet.Grant(save, new Reward { HeroId = "thor", HeroShards = 5 });
            Assert.AreEqual(0, save.ItemCount(Wallet.HeroShard("thor")));
            Assert.IsFalse(GodTrialRules.CanReceiveNormalShards(save, "thor"));
            save.AddItem(Wallet.HeroShard("thor"), 30);
            Assert.IsTrue(GodTrialRules.TryUnlockHero(save, "thor"));
            Assert.AreEqual(0, save.ItemCount(Wallet.HeroShard("thor")));
            Assert.AreEqual(1, save.Hero("thor").level);
            Assert.AreEqual(1, save.Hero("thor").stars);
            Assert.IsTrue(GodTrialRules.IsHeroUnlocked(save, "thor"));
            Assert.IsTrue(HeroUnlock.IsUnlocked(save, thor));
            Assert.IsTrue(GodTrialRules.CanReceiveNormalShards(save, "thor"));
            Wallet.Grant(save, new Reward { HeroId = "thor", HeroShards = 1 });
            Assert.AreEqual(1, save.ItemCount(Wallet.HeroShard("thor")));
            Assert.IsFalse(GodTrialRules.TryUnlockHero(save, "thor"));
            UnityEngine.Object.DestroyImmediate(thor);
        }

        [Test]
        public void LockedGodShardBalanceNeverExceedsThirty()
        {
            SaveData save = UnlockedSave();
            save.AddItem(Wallet.HeroShard("thor"), 29);
            save.AddItem(Wallet.HeroShard("thor"), 20);
            Assert.AreEqual(30, save.ItemCount(Wallet.HeroShard("thor")));
            Assert.IsFalse(GodTrialRules.TryUnlockHero(new SaveData { }, "thor"));
        }

        [Test]
        public void DailyHeroRotationSkipsGodEvenWhenRosterContainsThor()
        {
            var order = new[] { "khan", "thor", "guan" };
            for (int day = 1; day <= 14; day++)
            {
                string date = $"2026-11-{day:00}";
                Assert.AreNotEqual("thor", DailyRules.HeroFor(date, order));
            }
        }

        [Test]
        public void DailyResultWithLockedThorKeepsGoldButNoShard()
        {
            SaveData save = UnlockedSave();
            save.dailyChallenge.dayKey = "2026-11-01";
            List<string> lines = DailyRules.RecordOfficial(save, "thor", 100, true);
            Assert.AreEqual(1000, save.gold);
            Assert.AreEqual(0, save.ItemCount(Wallet.HeroShard("thor")));
            Assert.IsTrue(lines.Count > 0);
        }

        [Test]
        public void UnlockBeforeFirstClearStopsItsShardsAndWeeklyShards()
        {
            SaveData save = UnlockedSave();
            Assert.IsTrue(GodTrialRules.TryChooseGoal(save, GodTrialRules.ThorTrialId, Sunday));
            save.AddItem(Wallet.HeroShard("thor"), 30);
            Assert.IsTrue(GodTrialRules.TryUnlockHero(save, "thor"));
            GodTrialResult first = Win(save, "one", new[] { "blade" }, Sunday);
            Assert.IsTrue(first.FirstClear);
            Assert.AreEqual(0, first.Reward.HeroShards);
            Assert.AreEqual(0, save.ItemCount(Wallet.HeroShard("thor")));
            Assert.AreEqual(0, Win(save, "two", new[] { "fire" }, Sunday).WeeklyShards);
            CollectionAssert.Contains(save.titles, "천둥의 증인");
        }

        [Test]
        public void GoalLock_SurvivesReload_AfterGoalGodUnlocked()
        {
            // 목표 신을 해금하면 goalTrialId가 비워진다. 다시 불러와도 그 주 잠금은 남아야 한다 (다른 신으로 바꾸기 막기)
            string folder = Path.Combine(Path.GetTempPath(), "hh-goal-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new SaveStore(folder);
                var save = UnlockedSave();
                save.godTrials.weekKey = "2027-W01";
                save.godTrials.goalTrialId = null;
                save.godTrials.goalLocked = true;
                save.godTrials.firstRecordId = "first";
                Directory.CreateDirectory(folder);
                File.WriteAllText(store.MainPath, SaveStore.Sign(JsonUtility.ToJson(save, true)));
                SaveData loaded = store.Load();
                Assert.IsTrue(loaded.godTrials.goalLocked);
                Assert.AreEqual("first", loaded.godTrials.firstRecordId);
            }
            finally
            {
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
            }
        }

        [Test]
        public void SchemaEightLoadsWithEmptyTrialAndPreservedProgress()
        {
            string folder = Path.Combine(Path.GetTempPath(), "hh-thor-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new SaveStore(folder);
                var old = UnlockedSave();
                old.schemaVersion = 8;
                old.gold = 1234;
                old.godTrials = null;
                Directory.CreateDirectory(folder);
                File.WriteAllText(store.MainPath, SaveStore.Sign(JsonUtility.ToJson(old, true)));
                SaveData loaded = store.Load();
                Assert.AreEqual(SaveData.CurrentSchemaVersion, loaded.schemaVersion);
                Assert.AreEqual(1234, loaded.gold);
                CollectionAssert.Contains(loaded.defeatedBosses, "oblivion_king");
                Assert.IsNotNull(loaded.godTrials);
                Assert.IsEmpty(loaded.godTrials.cleared);
            }
            finally
            {
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
            }
        }
    }
}
