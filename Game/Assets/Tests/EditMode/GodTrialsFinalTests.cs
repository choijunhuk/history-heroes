using System;
using HistoryHeroes.Common;
using HistoryHeroes.Economy;
using HistoryHeroes.Enemy;
using HistoryHeroes.Heroes;
using HistoryHeroes.Modes;
using HistoryHeroes.Records;
using NUnit.Framework;
using UnityEngine;

namespace HistoryHeroes.Tests
{
    public sealed class GodTrialsFinalTests
    {
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 11, 3, 12, 0, 0, TimeSpan.FromHours(9));

        private static SaveData Save()
        {
            var save = new SaveData();
            save.defeatedBosses.Add("oblivion_king");
            save.records.Add(new RunRecord { id = "first", heroId = "guan" });
            save.records.Add(new RunRecord { id = "second", heroId = "khan" });
            return save;
        }

        private static GodTrialResult Win(SaveData save, string trial, string record = "first", string active = "blade") =>
            GodTrialRules.Complete(save, trial, save.FindRecord(record), new[] { active }, true, false, true, Now);

        private static void ClearThree(SaveData save)
        {
            Win(save, GodTrialRules.ThorTrialId);
            Win(save, GodTrialRules.AnubisTrialId);
            Win(save, GodTrialRules.ZeusTrialId);
        }

        [Test]
        public void FourthAndFifthTrialsUseOwnRecordsAndSourceArenas()
        {
            Assert.AreEqual(5, GodTrialRules.Trials.Count);
            Assert.AreEqual(GodTrialRules.SusanooTrialId, GodTrialRules.Trials[3].Id);
            Assert.AreEqual("shadow_twins", GodTrialRules.Trials[3].SourceBossId);
            Assert.AreEqual(4, GodTrialRules.Trials[3].ArenaChapter);
            Assert.AreEqual(GodTrialRules.AthenaTrialId, GodTrialRules.Trials[4].Id);
            Assert.AreEqual("oblivion_king", GodTrialRules.Trials[4].SourceBossId);
            Assert.AreEqual(5, GodTrialRules.Trials[4].ArenaChapter);

            SaveData save = Save();
            Assert.IsFalse(GodTrialRules.IsUnlocked(save, GodTrialRules.SusanooTrialId));
            Assert.IsFalse(GodTrialRules.IsUnlocked(save, GodTrialRules.AthenaTrialId));
            ClearThree(save);
            Assert.IsTrue(GodTrialRules.IsUnlocked(save, GodTrialRules.SusanooTrialId));
            Assert.IsFalse(GodTrialRules.IsUnlocked(save, GodTrialRules.AthenaTrialId));
            Assert.IsTrue(GameSession.PrepareGodTrial(save, GodTrialRules.SusanooTrialId, "first", true));
            Assert.AreEqual(4, GameSession.Chapter);

            RunRecord borrowed = new RunRecord { id = "borrowed", heroId = "guan" };
            Assert.IsFalse(GodTrialRules.Complete(save, GodTrialRules.SusanooTrialId, borrowed,
                Array.Empty<string>(), true, false, true, Now).FirstClear);
            Assert.IsFalse(GodTrialRules.Complete(save, GodTrialRules.SusanooTrialId, save.records[0],
                Array.Empty<string>(), true, true, false, Now).FirstClear);
            Assert.IsFalse(GodTrialRules.IsUnlocked(save, GodTrialRules.AthenaTrialId));
            Win(save, GodTrialRules.SusanooTrialId);
            Assert.IsTrue(GodTrialRules.IsUnlocked(save, GodTrialRules.AthenaTrialId));
            Assert.IsTrue(GameSession.PrepareGodTrial(save, GodTrialRules.AthenaTrialId, "first", true));
            Assert.AreEqual(5, GameSession.Chapter);
        }

        [Test]
        public void FirstClearShardsTitlesAndMarksAreGivenOnce()
        {
            SaveData save = Save();
            ClearThree(save);
            foreach (string trialId in new[] { GodTrialRules.SusanooTrialId, GodTrialRules.AthenaTrialId })
            {
                string heroId = GodTrialRules.Find(trialId).HeroId;
                Assert.AreEqual(10, Win(save, trialId).Reward.HeroShards);
                Assert.AreEqual(10, save.ItemCount(Wallet.HeroShard(heroId)));
                Assert.IsFalse(Win(save, trialId).FirstClear);
                Assert.AreEqual(10, save.ItemCount(Wallet.HeroShard(heroId)));
                CollectionAssert.Contains(save.godTrials.cleared, trialId);
                CollectionAssert.Contains(save.titles, GodTrialRules.Find(trialId).Title);
            }
            Assert.IsEmpty(save.stars);
        }

        [Test]
        public void FourGodsShareOneWeeklyFiveShardLimit()
        {
            SaveData save = Save();
            ClearThree(save);
            Win(save, GodTrialRules.SusanooTrialId);
            Assert.IsTrue(GodTrialRules.TryChooseGoal(save, GodTrialRules.SusanooTrialId, Now));
            Assert.IsTrue(GodTrialRules.TryChooseGoal(save, GodTrialRules.AthenaTrialId, Now));
            Assert.IsTrue(GodTrialRules.TryChooseGoal(save, GodTrialRules.ThorTrialId, Now));
            Assert.IsTrue(GodTrialRules.TryChooseGoal(save, GodTrialRules.AnubisTrialId, Now));
            Assert.AreEqual(0, Win(save, GodTrialRules.AnubisTrialId).WeeklyShards);
            Assert.IsFalse(GodTrialRules.TryChooseGoal(save, GodTrialRules.AthenaTrialId, Now));
            Assert.AreEqual(5, Win(save, GodTrialRules.AnubisTrialId, "second", "fire").WeeklyShards);
            Assert.AreEqual(5, save.godTrials.weeklyShards);
            Assert.IsFalse(GodTrialRules.TryChooseGoal(save, GodTrialRules.SusanooTrialId, Now));
        }

        [Test]
        public void LossRefundIsFivePerUnclearedTrialAndRepeatGoldSharesBossCap()
        {
            SaveData save = Save();
            ClearThree(save);
            save.hearts = 0;
            foreach (string trialId in new[] { GodTrialRules.SusanooTrialId, GodTrialRules.AthenaTrialId })
            {
                if (trialId == GodTrialRules.AthenaTrialId) Win(save, GodTrialRules.SusanooTrialId);
                save.hearts = 0;
                for (int i = 0; i < 5; i++)
                    Assert.IsTrue(GodTrialRules.Complete(save, trialId, save.records[0], Array.Empty<string>(),
                        false, false, true, Now).HeartRefunded);
                Assert.IsFalse(GodTrialRules.Complete(save, trialId, save.records[0], Array.Empty<string>(),
                    false, false, true, Now).HeartRefunded);
            }
            Win(save, GodTrialRules.AthenaTrialId);
            Assert.AreEqual(300, Win(save, GodTrialRules.AthenaTrialId).Reward.Gold);
            Assert.AreEqual(300, Win(save, GodTrialRules.SusanooTrialId).Reward.Gold);
            Assert.AreEqual(300, RunRewards.BossRepeat(save, Now.ToString("yyyy-MM-dd")).Gold);
            Assert.IsTrue(Win(save, GodTrialRules.AthenaTrialId).Reward.IsEmpty);
        }

        [Test]
        public void BothGodsUseThirtyShardCapAndNormalRewardExclusion()
        {
            SaveData save = Save();
            ClearThree(save);
            Win(save, GodTrialRules.SusanooTrialId);
            foreach (string heroId in new[] { GodTrialRules.SusanooHeroId, GodTrialRules.AthenaHeroId })
            {
                Assert.IsFalse(GodTrialRules.CanReceiveNormalShards(save, heroId));
                Wallet.Grant(save, new Reward { HeroId = heroId, HeroShards = 20 });
                Assert.AreEqual(heroId == GodTrialRules.SusanooHeroId ? 10 : 0, save.ItemCount(Wallet.HeroShard(heroId)));
                save.AddItem(Wallet.HeroShard(heroId), 100);
                Assert.AreEqual(30, save.ItemCount(Wallet.HeroShard(heroId)));
                Assert.IsTrue(GodTrialRules.TryUnlockHero(save, heroId));
                Assert.AreEqual(0, save.ItemCount(Wallet.HeroShard(heroId)));
                Assert.AreEqual(1, save.Hero(heroId).level);
                Assert.AreEqual(1, save.Hero(heroId).stars);
                Assert.IsTrue(GodTrialRules.CanReceiveNormalShards(save, heroId));
            }
        }

        [Test]
        public void LockedGodsStayOutOfShopDailyAndTenRunFreePick()
        {
            foreach (string heroId in new[] { GodTrialRules.SusanooHeroId, GodTrialRules.AthenaHeroId })
            {
                SaveData save = Save();
                save.dungeonRuns = HeroUnlock.FreePickRuns;
                save.gold = 500;
                save.gemsFree = 2000;
                save.dailyChallenge.dayKey = "2026-11-03";
                HeroData hero = ScriptableObject.CreateInstance<HeroData>();
                try
                {
                    hero.id = heroId;
                    hero.gemsOnly = true;
                    hero.unlockGems = 2000;
                    Assert.IsFalse(HeroUnlock.TryClaimFree(save, hero));
                    Assert.IsFalse(HeroUnlock.TryBuy(save, hero));
                    Assert.IsFalse(Meta.HeroGrowth.TryBuyHeroShard(save, heroId, "2026-11-03"));
                    Assert.AreEqual(500, save.gold);
                    Assert.AreEqual(2000, save.gemsFree);
                    DailyRules.RecordOfficial(save, heroId, 100, true);
                    Assert.AreEqual(0, save.ItemCount(Wallet.HeroShard(heroId)));
                    Assert.AreEqual(DailyRules.RewardGold + 500, save.gold);
                    Assert.AreEqual(2000, save.gemsFree);
                }
                finally { UnityEngine.Object.DestroyImmediate(hero); }
            }

            string[] roster = { "khan", GodTrialRules.SusanooHeroId, "guan", GodTrialRules.AthenaHeroId };
            for (int day = 1; day <= 14; day++)
            {
                string selected = DailyRules.HeroFor($"2026-11-{day:00}", roster);
                Assert.AreNotEqual(GodTrialRules.SusanooHeroId, selected);
                Assert.AreNotEqual(GodTrialRules.AthenaHeroId, selected);
            }
        }

        [Test]
        public void TwoTrialFormsNeedTheirOwnHalfBeforeBossIsDefeated()
        {
            var state = new ShadowTwinTrialState();
            state.Reset(112800f);
            Assert.AreEqual(56400f, state.First);
            Assert.AreEqual(56400f, state.Second);
            state.Apply(true, 60000f);
            Assert.IsFalse(state.SecondAlive);
            Assert.IsFalse(state.Complete);
            Assert.AreEqual(56400f, state.Total);
            state.Apply(false, 56400f);
            Assert.IsTrue(state.Complete);
            state.Reset(112800f);
            state.Apply(false, 56400f);
            Assert.IsFalse(state.Complete);
            state.Apply(true, 56400f);
            Assert.IsTrue(state.Complete);
        }
    }
}
