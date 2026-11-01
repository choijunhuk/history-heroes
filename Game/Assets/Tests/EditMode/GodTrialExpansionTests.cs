using System;
using System.IO;
using HistoryHeroes.Common;
using HistoryHeroes.Economy;
using HistoryHeroes.Heroes;
using HistoryHeroes.Modes;
using HistoryHeroes.Records;
using NUnit.Framework;
using UnityEngine;

namespace HistoryHeroes.Tests
{
    /// <summary>후속 시련은 토르의 저장·보상 상한을 함께 쓴다.</summary>
    public sealed class GodTrialExpansionTests
    {
        private static readonly DateTimeOffset Sunday = new DateTimeOffset(2026, 11, 1, 23, 59, 0, TimeSpan.FromHours(9));
        private static readonly DateTimeOffset Monday = new DateTimeOffset(2026, 11, 2, 5, 0, 0, TimeSpan.FromHours(9));

        [TearDown]
        public void ResetSession() => GameSession.PrepareDungeon(1);

        private static SaveData Save()
        {
            var save = new SaveData();
            save.defeatedBosses.Add("oblivion_king");
            save.records.Add(new RunRecord { id = "first", heroId = "khan" });
            save.records.Add(new RunRecord { id = "second", heroId = "guan" });
            return save;
        }

        private static GodTrialResult Win(SaveData save, string trial, string record, string active) =>
            GodTrialRules.Complete(save, trial, save.FindRecord(record), new[] { active }, true, false, true, Sunday);

        [Test]
        public void TrialTableKeepsOrderSourceAndPlayableGods()
        {
            Assert.AreEqual(5, GodTrialRules.Trials.Count);
            Assert.AreEqual(GodTrialRules.ThorTrialId, GodTrialRules.Trials[0].Id);
            Assert.AreEqual("rift_warden", GodTrialRules.Trials[0].SourceBossId);
            Assert.AreEqual("thor", GodTrialRules.Trials[0].HeroId);
            Assert.AreEqual(GodTrialRules.AnubisTrialId, GodTrialRules.Trials[1].Id);
            Assert.AreEqual("wraith_lord", GodTrialRules.Trials[1].SourceBossId);
            Assert.AreEqual("anubis", GodTrialRules.Trials[1].HeroId);
            Assert.AreEqual(GodTrialRules.ZeusTrialId, GodTrialRules.Trials[2].Id);
            Assert.AreEqual("steel_colossus", GodTrialRules.Trials[2].SourceBossId);
            Assert.IsNull(GodTrialRules.Trials[2].HeroId);
        }

        [Test]
        public void UnlockChainNeedsOwnFirstClearAndUsesEachArena()
        {
            SaveData save = Save();
            Assert.IsTrue(GodTrialRules.IsUnlocked(save, GodTrialRules.ThorTrialId));
            Assert.IsFalse(GodTrialRules.IsUnlocked(save, GodTrialRules.AnubisTrialId));
            Assert.IsFalse(GodTrialRules.IsUnlocked(save, GodTrialRules.ZeusTrialId));
            Assert.IsFalse(GodTrialRules.Complete(save, GodTrialRules.ThorTrialId,
                new RunRecord { id = "borrowed" }, Array.Empty<string>(), true, false, true, Sunday).FirstClear);
            Assert.IsFalse(GodTrialRules.IsUnlocked(save, GodTrialRules.AnubisTrialId));
            Win(save, GodTrialRules.ThorTrialId, "first", "blade");
            Assert.IsTrue(GodTrialRules.IsUnlocked(save, GodTrialRules.AnubisTrialId));
            Assert.IsFalse(GodTrialRules.IsUnlocked(save, GodTrialRules.ZeusTrialId));
            Assert.IsTrue(GameSession.PrepareGodTrial(save, GodTrialRules.AnubisTrialId, "first", true));
            Assert.AreEqual(2, GameSession.Chapter);
            Win(save, GodTrialRules.AnubisTrialId, "first", "blade");
            Assert.IsTrue(GodTrialRules.IsUnlocked(save, GodTrialRules.ZeusTrialId));
            Assert.IsTrue(GameSession.PrepareGodTrial(save, GodTrialRules.ZeusTrialId, "first", true));
            Assert.AreEqual(3, GameSession.Chapter);
        }

        [Test]
        public void AnubisGivesTenOnceAndZeusGivesTitleOnly()
        {
            SaveData save = Save();
            Win(save, GodTrialRules.ThorTrialId, "first", "blade");
            Assert.AreEqual(10, Win(save, GodTrialRules.AnubisTrialId, "first", "blade").Reward.HeroShards);
            Assert.AreEqual(10, save.ItemCount(Wallet.HeroShard("anubis")));
            Assert.AreEqual(0, Win(save, GodTrialRules.AnubisTrialId, "first", "blade").Reward.HeroShards);
            GodTrialResult zeus = Win(save, GodTrialRules.ZeusTrialId, "first", "blade");
            Assert.IsTrue(zeus.FirstClear);
            Assert.IsTrue(zeus.Reward.IsEmpty);
            Assert.AreEqual(0, save.ItemCount(Wallet.HeroShard("zeus")));
            CollectionAssert.Contains(save.titles, "저울의 증인");
            CollectionAssert.Contains(save.titles, "하늘의 증인");
        }

        [Test]
        public void WeeklyGoalCanSwitchBeforeFirstWinButCapIsShared()
        {
            SaveData save = Save();
            Win(save, GodTrialRules.ThorTrialId, "first", "blade");
            Assert.IsTrue(GodTrialRules.TryChooseGoal(save, GodTrialRules.ThorTrialId, Sunday));
            Assert.IsTrue(GodTrialRules.TryChooseGoal(save, GodTrialRules.AnubisTrialId, Sunday));
            Win(save, GodTrialRules.AnubisTrialId, "first", "blade");
            Assert.IsFalse(GodTrialRules.TryChooseGoal(save, GodTrialRules.ThorTrialId, Sunday));
            Assert.AreEqual(5, Win(save, GodTrialRules.AnubisTrialId, "second", "fire").WeeklyShards);
            Assert.AreEqual(15, save.ItemCount(Wallet.HeroShard("anubis")));
            Assert.AreEqual(5, save.godTrials.weeklyShards);
            Assert.IsTrue(GodTrialRules.EnsureWeek(save, Monday));
            Assert.AreEqual(0, save.godTrials.weeklyShards);
            Assert.AreEqual(15, save.ItemCount(Wallet.HeroShard("anubis")));
        }

        [Test]
        public void UnlockingWeeklyTargetDoesNotOpenAnotherFiveShardsInSameWeek()
        {
            SaveData save = Save();
            Win(save, GodTrialRules.ThorTrialId, "first", "blade");
            Assert.IsTrue(GodTrialRules.TryChooseGoal(save, GodTrialRules.AnubisTrialId, Sunday));
            Win(save, GodTrialRules.AnubisTrialId, "first", "blade");
            Assert.AreEqual(5, Win(save, GodTrialRules.AnubisTrialId, "second", "fire").WeeklyShards);
            save.AddItem(Wallet.HeroShard("anubis"), 15);
            Assert.IsTrue(GodTrialRules.TryUnlockHero(save, "anubis"));
            Assert.IsNull(save.godTrials.goalTrialId);
            Assert.IsFalse(GodTrialRules.TryChooseGoal(save, GodTrialRules.ThorTrialId, Sunday));
            Assert.AreEqual(0, Win(save, GodTrialRules.ThorTrialId, "second", "fire").WeeklyShards);
            Assert.AreEqual(5, save.godTrials.weeklyShards);
        }

        [Test]
        public void UtcClockUsesMondayFiveKstBoundary()
        {
            SaveData save = Save();
            Assert.IsTrue(GodTrialRules.EnsureWeek(save, Sunday));
            Assert.IsFalse(GodTrialRules.EnsureWeek(save, Monday.AddMinutes(-1).ToUniversalTime()));
            Assert.IsTrue(GodTrialRules.EnsureWeek(save, Monday.ToUniversalTime()));
        }

        [Test]
        public void BorrowedAndPracticeRecordsCannotOpenLaterTrials()
        {
            SaveData save = Save();
            Win(save, GodTrialRules.ThorTrialId, "first", "blade");
            var borrowed = new RunRecord { id = "borrowed", heroId = "guan" };
            Assert.IsFalse(GodTrialRules.Complete(save, GodTrialRules.AnubisTrialId, borrowed,
                Array.Empty<string>(), true, false, true, Sunday).FirstClear);
            Assert.IsFalse(GodTrialRules.Complete(save, GodTrialRules.AnubisTrialId, save.records[0],
                Array.Empty<string>(), true, true, false, Sunday).FirstClear);
            Assert.IsFalse(GodTrialRules.IsUnlocked(save, GodTrialRules.ZeusTrialId));
            Win(save, GodTrialRules.AnubisTrialId, "first", "blade");
            Assert.IsFalse(GodTrialRules.Complete(save, GodTrialRules.ZeusTrialId, borrowed,
                Array.Empty<string>(), true, false, true, Sunday).FirstClear);
            Assert.IsFalse(GodTrialRules.Complete(save, GodTrialRules.ZeusTrialId, save.records[0],
                Array.Empty<string>(), true, true, false, Sunday).FirstClear);
            CollectionAssert.DoesNotContain(save.godTrials.cleared, GodTrialRules.ZeusTrialId);
        }

        [Test]
        public void LockedAnubisIsExcludedFromNormalShardAndFreeHeroSources()
        {
            SaveData save = Save();
            save.dungeonRuns = HeroUnlock.FreePickRuns;
            save.gold = 500;
            save.dailyChallenge.dayKey = "2026-11-01";
            var hero = ScriptableObject.CreateInstance<HeroData>();
            try
            {
                hero.id = "anubis";
                hero.gemsOnly = true;
                hero.unlockGems = 2000;
                Assert.IsFalse(HeroUnlock.TryClaimFree(save, hero));
                Assert.IsFalse(HeroUnlock.TryBuy(save, hero));
                Assert.IsFalse(Meta.HeroGrowth.TryBuyHeroShard(save, "anubis", "2026-11-01"));
                Wallet.Grant(save, new Reward { HeroId = "anubis", HeroShards = 5 });
                Assert.AreEqual(0, save.ItemCount(Wallet.HeroShard("anubis")));
                DailyRules.RecordOfficial(save, "anubis", 100, true);
                Assert.AreEqual(0, save.ItemCount(Wallet.HeroShard("anubis")));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hero);
            }
        }

        [Test]
        public void SchemaNineKeepsThorWeeklyProgressAndRepairsAnubisShardCap()
        {
            string folder = Path.Combine(Path.GetTempPath(), "hh-god-trials-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new SaveStore(folder);
                SaveData old = Save();
                Assert.IsTrue(GodTrialRules.TryChooseGoal(old, GodTrialRules.ThorTrialId, Sunday));
                Win(old, GodTrialRules.ThorTrialId, "first", "blade");
                old.AddItem(Wallet.HeroShard("anubis"), 30);
                old.items.Find(item => item.id == Wallet.HeroShard("anubis")).count = 45;
                Directory.CreateDirectory(folder);
                File.WriteAllText(store.MainPath, SaveStore.Sign(JsonUtility.ToJson(old, true)));
                SaveData loaded = store.Load();
                Assert.AreEqual(SaveData.CurrentSchemaVersion, loaded.schemaVersion);
                Assert.AreEqual(GodTrialRules.ThorTrialId, loaded.godTrials.goalTrialId);
                Assert.AreEqual("first", loaded.godTrials.firstRecordId);
                Assert.AreEqual(1, loaded.godTrials.weeklyWins.Count);
                Assert.AreEqual(10, loaded.ItemCount(Wallet.HeroShard("thor")));
                Assert.AreEqual(30, loaded.ItemCount(Wallet.HeroShard("anubis")));
            }
            finally
            {
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
            }
        }

        [Test]
        public void AnubisShardsCapAtThirtyAndUnlockAtLevelOne()
        {
            SaveData save = Save();
            Win(save, GodTrialRules.ThorTrialId, "first", "blade");
            save.AddItem(Wallet.HeroShard("anubis"), 40);
            Assert.AreEqual(30, save.ItemCount(Wallet.HeroShard("anubis")));
            Assert.IsFalse(GodTrialRules.CanReceiveNormalShards(save, "anubis"));
            Assert.IsTrue(GodTrialRules.TryUnlockHero(save, "anubis"));
            Assert.AreEqual(1, save.Hero("anubis").level);
            Assert.AreEqual(1, save.Hero("anubis").stars);
            Assert.AreEqual(0, save.ItemCount(Wallet.HeroShard("anubis")));
            Assert.IsTrue(GodTrialRules.CanReceiveNormalShards(save, "anubis"));
        }

        [Test]
        public void RepeatGoldAndLossRefundAreSharedOrPerTrialAsSpecified()
        {
            SaveData save = Save();
            Win(save, GodTrialRules.ThorTrialId, "first", "blade");
            save.hearts = 0;
            for (int i = 0; i < 5; i++)
                Assert.IsTrue(GodTrialRules.Complete(save, GodTrialRules.AnubisTrialId, save.records[0],
                    Array.Empty<string>(), false, false, true, Sunday).HeartRefunded);
            Assert.IsFalse(GodTrialRules.Complete(save, GodTrialRules.AnubisTrialId, save.records[0],
                Array.Empty<string>(), false, false, true, Sunday).HeartRefunded);
            Win(save, GodTrialRules.AnubisTrialId, "first", "blade");
            Assert.AreEqual(300, Win(save, GodTrialRules.ThorTrialId, "first", "blade").Reward.Gold);
            Assert.AreEqual(300, Win(save, GodTrialRules.AnubisTrialId, "first", "blade").Reward.Gold);
            Assert.AreEqual(300, RunRewards.BossRepeat(save, Sunday.ToString("yyyy-MM-dd")).Gold);
            Assert.IsTrue(Win(save, GodTrialRules.AnubisTrialId, "first", "blade").Reward.IsEmpty);
            save.hearts = 0;
            for (int i = 0; i < 5; i++)
                Assert.IsTrue(GodTrialRules.Complete(save, GodTrialRules.ZeusTrialId, save.records[0],
                    Array.Empty<string>(), false, false, true, Sunday).HeartRefunded);
            Assert.IsFalse(GodTrialRules.Complete(save, GodTrialRules.ZeusTrialId, save.records[0],
                Array.Empty<string>(), false, false, true, Sunday).HeartRefunded);
        }
    }
}
