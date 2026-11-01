using HistoryHeroes.Common;
using HistoryHeroes.Economy;
using HistoryHeroes.Heroes;
using HistoryHeroes.Modes;
using HistoryHeroes.Records;
using NUnit.Framework;
using UnityEngine;

namespace HistoryHeroes.Tests
{
    /// <summary>시련 입장과 기록 복원은 장면을 열지 않고 검사한다.</summary>
    public sealed class GodTrialSessionTests
    {
        [TearDown]
        public void ResetSession() => GameSession.PrepareDungeon(1);

        private static SaveData OpenTrial()
        {
            var save = new SaveData { hearts = 3, selectedHero = "khan" };
            save.defeatedBosses.Add("oblivion_king");
            save.records.Add(new RunRecord { id = "own", heroId = "guan", meta = new MetaSnapshot { heroLevel = 12, heroStars = 3 } });
            return save;
        }

        [Test]
        public void TrialCannotPrepareBeforeOwnKingClearOrWithoutOwnRecord()
        {
            SaveData save = OpenTrial();
            save.defeatedBosses.Clear();
            Assert.IsFalse(GameSession.PrepareGodTrial(save, "thor_trial", "own", false));
            save.defeatedBosses.Add("oblivion_king");
            Assert.IsFalse(GameSession.PrepareGodTrial(save, "thor_trial", "borrowed", false));
            Assert.IsFalse(GameSession.PrepareGodTrial(save, "future_trial", "own", false));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TrialReusesBossArenaAndKeepsSave(bool practice)
        {
            SaveData save = OpenTrial();
            string before = JsonUtility.ToJson(save);
            Assert.IsTrue(GameSession.PrepareGodTrial(save, "thor_trial", "own", practice));
            Assert.IsTrue(GameSession.IsGodTrial);
            Assert.AreEqual("thor_trial", GameSession.GodTrialId);
            Assert.AreEqual(BattleMode.Boss, GameSession.Mode);
            Assert.AreEqual(1, GameSession.Chapter, "수문장의 경기장을 재사용한다");
            Assert.AreEqual(practice, GameSession.Practice);
            Assert.AreEqual("own", GameSession.BossRecordId);
            Assert.IsFalse(GameSession.IsHeroTrial);
            Assert.AreEqual(before, JsonUtility.ToJson(save));
        }

        [Test]
        public void TempAndLockedRecordAreUsableAndSnapshotStaysFixed()
        {
            SaveData save = OpenTrial();
            RunRecord record = save.records[0];
            record.locked = true;
            save.records.Clear();
            save.tempRecords.Add(record);
            save.Hero("guan").level = 30;
            Assert.IsTrue(GameSession.PrepareGodTrial(save, "thor_trial", "own", false));
            Assert.AreSame(record.meta, BattleBootstrap.MetaForThisBattle(null, "guan", save));
            Assert.AreEqual(12, BattleBootstrap.MetaForThisBattle(null, "guan", save).heroLevel);
            Assert.AreEqual(3, BattleBootstrap.MetaForThisBattle(null, "guan", save).heroStars);
        }

        [Test]
        public void RealAttemptSpendsOneEvenWhenFirstDayFreeRunsRemain()
        {
            SaveData save = OpenTrial();
            var now = new System.DateTimeOffset(2026, 10, 29, 12, 0, 0, System.TimeSpan.FromHours(9));
            Assert.IsTrue(GameSession.TryPrepareGodTrialAttempt(save, "thor_trial", "own", now));
            Assert.AreEqual(2, save.hearts);
            Assert.AreEqual(0, save.freeRunsUsed);
            Assert.IsTrue(GameSession.HeartSpent);
            Assert.IsFalse(GameSession.Practice);
        }

        [Test]
        public void RealAttempt_SnapshotCarriesTrialAndHeart_BeforeSceneLoads()
        {
            // 하트를 쓴 저장과 이어하기가 따로 저장되면, 장면 로딩 중 앱이 꺼질 때 하트만 사라졌다 (Codex 리뷰)
            SaveData save = OpenTrial();
            Assert.IsTrue(GameSession.TryPrepareGodTrialAttempt(save, Modes.GodTrialRules.ThorTrialId, "own", System.DateTimeOffset.FromUnixTimeSeconds(1_800_000_000)));
            RunSnapshot run = GameSession.BossSnapshot(1_800_000_000);
            Assert.IsTrue(run.valid);
            Assert.AreEqual((int)BattleMode.Boss, run.mode);
            Assert.AreEqual(Modes.GodTrialRules.ThorTrialId, run.godTrialId);
            Assert.AreEqual("own", run.recordId);
            Assert.IsTrue(run.heartSpent);
            Assert.IsFalse(run.practice);
        }

        [Test]
        public void FailedAttemptNeverSpendsHeartOrReplacesSession()
        {
            SaveData save = OpenTrial();
            save.hearts = 0;
            GameSession.PrepareDungeon(3);
            Assert.IsFalse(GameSession.TryPrepareGodTrialAttempt(save, "thor_trial", "own", Clock.Now()));
            Assert.AreEqual(BattleMode.Dungeon, GameSession.Mode);
            Assert.AreEqual(3, GameSession.Chapter);
            Assert.AreEqual(0, save.hearts);
            save.hearts = 2;
            Assert.IsFalse(GameSession.TryPrepareGodTrialAttempt(save, "thor_trial", "missing", Clock.Now()));
            Assert.AreEqual(2, save.hearts);
        }

        [Test]
        public void OtherRunClearsTrialAndThorCanUseExistingHeroTrial()
        {
            SaveData save = OpenTrial();
            GameSession.PrepareGodTrial(save, "thor_trial", "own", true);
            GameSession.PrepareHeroTrial(save, "thor");
            Assert.IsFalse(GameSession.IsGodTrial);
            Assert.IsNull(GameSession.GodTrialId);
            Assert.IsTrue(GameSession.IsHeroTrial);
            Assert.AreEqual("thor", GameSession.TrialHeroId);
            Assert.AreEqual(1, BattleBootstrap.MetaForThisBattle(null, "thor", save).heroLevel);
            Assert.AreEqual(1, BattleBootstrap.MetaForThisBattle(null, "thor", save).heroStars);
            GameSession.PrepareDungeon(2);
            Assert.IsFalse(GameSession.IsGodTrial);
            Assert.IsFalse(GameSession.IsHeroTrial);
        }
    }
}
