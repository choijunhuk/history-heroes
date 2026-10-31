using System;
using System.IO;
using System.Text.RegularExpressions;
using HistoryHeroes.Common;
using HistoryHeroes.Economy;
using HistoryHeroes.Meta;
using HistoryHeroes.Missions;
using HistoryHeroes.Player;
using HistoryHeroes.Records;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HistoryHeroes.Tests
{
    public sealed class SaveEntryIntegrityTests
    {
        private string _folder;
        private SaveStore _store;
        private MetaTable _table;

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Path.GetTempPath(), "hh-save-entry-" + Guid.NewGuid().ToString("N"));
            _store = new SaveStore(_folder);
            SaveSystem.Use(_store);
            GameSession.PrepareDungeon(1);
            _table = ScriptableObject.CreateInstance<MetaTable>();
            GameSession.ConfigureMetaTable(_table);
            SaveData data = SaveSystem.Data;
            data.hearts = 5;
            data.heartsUpdatedAt = Clock.UnixSeconds;
            data.firstPlayDate = "2020-01-01";
            data.freeRunsUsed = 3;
            data.selectedHero = "guan";
            data.Hero("guan").level = 8;
        }

        [TearDown]
        public void TearDown()
        {
            GameSession.PrepareLobby();
            GameSession.ConfigureMetaTable(null);
            MissionEvents.Table = null;
            MissionEvents.Context = null;
            SaveSystem.Use(null);
            Object.DestroyImmediate(_table);
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
            if (File.Exists(_folder)) File.Delete(_folder);
        }

        [TestCase(BattleMode.Dungeon)]
        [TestCase(BattleMode.Survival)]
        [TestCase(BattleMode.Boss)]
        public void PaidEntry_SavesHeartAndInitialSnapshotTogether(BattleMode mode)
        {
            Assert.IsTrue(GameSession.TrySaveRunEntry(mode, 3, 1, "record"));
            SaveData loaded = new SaveStore(_folder).Load();
            Assert.AreEqual(4, loaded.hearts);
            Assert.IsTrue(loaded.run.valid);
            Assert.AreEqual((int)mode, loaded.run.mode);
            Assert.AreEqual(3, loaded.run.chapter);
            Assert.IsTrue(loaded.run.heartSpent);
            if (mode == BattleMode.Boss) Assert.AreEqual("record", loaded.run.recordId);
            else
            {
                Assert.AreEqual("guan", loaded.run.heroId);
                Assert.AreEqual(8, loaded.run.meta.heroLevel);
                Assert.IsTrue(loaded.run.hasMeta);
                Assert.AreNotEqual(0, loaded.run.seed);
                Assert.AreEqual(1, loaded.run.room);
                Assert.AreEqual(0f, loaded.run.elapsed);
                if (mode == BattleMode.Survival) Assert.AreEqual(1, loaded.run.survivalMap);
            }
        }

        [Test]
        public void PendingCandidate_BlocksPaidEntryWithoutSpendingHeart()
        {
            SaveSystem.Data.pendingRecord = new RunRecord { id = "keep-until-decided" };
            Assert.IsFalse(GameSession.TrySaveRunEntry(BattleMode.Dungeon, 2));
            Assert.AreEqual(5, SaveSystem.Data.hearts);
            Assert.AreEqual("keep-until-decided", SaveSystem.Data.pendingRecord.id);
        }

        [Test]
        public void HeroPanel_FocusesTrialHeroOrCurrentSelection()
        {
            var roster = ScriptableObject.CreateInstance<HistoryHeroes.Heroes.HeroRoster>();
            var khan = ScriptableObject.CreateInstance<HistoryHeroes.Heroes.HeroData>();
            var guan = ScriptableObject.CreateInstance<HistoryHeroes.Heroes.HeroData>();
            khan.id = "khan";
            guan.id = "guan";
            roster.heroes.AddRange(new[] { khan, guan });
            SaveSystem.Data.unlockedChapter = 5;
            try
            {
                Assert.AreSame(guan, HistoryHeroes.UI.HeroPanel.HeroToFocus(roster, SaveSystem.Data));
                Assert.AreSame(khan, HistoryHeroes.UI.HeroPanel.HeroToFocus(roster, SaveSystem.Data, "khan"));
                Assert.AreSame(guan, HistoryHeroes.UI.HeroPanel.HeroToFocus(roster, SaveSystem.Data, "missing"));
                Assert.AreEqual("guan", SaveSystem.Data.selectedHero);
            }
            finally
            {
                Object.DestroyImmediate(khan);
                Object.DestroyImmediate(guan);
                Object.DestroyImmediate(roster);
            }
        }

        [Test]
        public void RetryEntry_ReplacesPreviousSnapshotBeforeLoading()
        {
            Assert.IsTrue(GameSession.TrySaveRunEntry(BattleMode.Dungeon, 2));
            SaveSystem.Data.run.room = 10;
            SaveSystem.Data.run.reviveUsed = true;
            Assert.IsTrue(GameSession.TrySaveRunEntry(GameSession.Mode, GameSession.Chapter));
            SaveData loaded = new SaveStore(_folder).Load();
            Assert.AreEqual(3, loaded.hearts);
            Assert.AreEqual(1, loaded.run.room);
            Assert.IsFalse(loaded.run.reviveUsed);
        }

        [Test]
        public void FailedEntry_RollsBackHeartAndExistingSnapshot()
        {
            SaveSystem.Data.run = new RunSnapshot { valid = true, room = 9, heroId = "khan" };
            File.WriteAllText(_folder, "저장 폴더 자리를 막아 쓰기 실패를 재현한다");
            LogAssert.Expect(LogType.Error, new Regex(@"\[SaveSystem\] 저장 실패:"));
            Assert.IsFalse(GameSession.TrySaveRunEntry(BattleMode.Dungeon, 3));
            Assert.AreEqual(5, SaveSystem.Data.hearts);
            Assert.AreEqual(9, SaveSystem.Data.run.room);
            Assert.AreEqual("khan", SaveSystem.Data.run.heroId);
        }

        [Test]
        public void LobbyReturn_ClearsPracticeAndRestoresGrowthMissionProgress()
        {
            SaveData data = SaveSystem.Data;
            data.records.Add(new RunRecord { id = "record", heroId = "guan" });
            data.defeatedBosses.Add("oblivion_king");
            Assert.IsTrue(GameSession.PrepareGodTrial(data, "thor_trial", "record", true));
            data.missions.daily.Add(new MissionProgress { id = "level", eventKey = MissionEvents.HeroLevel, target = 2 });
            data.missions.daily.Add(new MissionProgress { id = "equip", eventKey = MissionEvents.Equip, target = 2 });
            MissionEvents.Table = null;
            MissionEvents.Raise(MissionEvents.HeroLevel);
            Assert.AreEqual(0, data.missions.daily[0].progress);
            GameSession.PrepareLobby();
            MissionEvents.Raise(MissionEvents.HeroLevel);
            MissionEvents.Raise(MissionEvents.Equip);
            Assert.IsFalse(GameSession.Practice);
            Assert.IsFalse(GameSession.IsGodTrial);
            Assert.AreEqual(1, data.missions.daily[0].progress);
            Assert.AreEqual(1, data.missions.daily[1].progress);
        }

        [Test]
        public void TrialHeroLobbyFocus_IsConsumedOnceWithoutChangingSelectedHero()
        {
            GameSession.PrepareHeroTrial(SaveSystem.Data, "thor");
            GameSession.PrepareLobby(GameSession.TrialHeroId);
            Assert.AreEqual("thor", GameSession.ConsumeLobbyHero());
            Assert.IsNull(GameSession.ConsumeLobbyHero());
            Assert.AreEqual("guan", SaveSystem.Data.selectedHero);
            Assert.IsFalse(GameSession.Practice);
            Assert.IsFalse(GameSession.IsHeroTrial);
        }

        [Test]
        public void RunAndRiftCheckpoint_RestoreUsedReviveAfterRestart()
        {
            var before = new SaveData();
            before.run.reviveUsed = true;
            before.rift.checkpoint.reviveUsed = true;
            SaveData loaded = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(before));
            var go = new GameObject("ReviveCheckpoint");
            go.SetActive(false);
            try
            {
                PlayerController player = go.AddComponent<PlayerController>();
                player.Attack.Set(this, AttackModifierKind.Undying, 1);
                player.RestoreUndyingUsed(loaded.run.reviveUsed);
                Assert.IsTrue(player.UndyingUsed);
                AssertUsedUndyingRejected(player);
                player.RestoreUndyingUsed(false);
                player.RestoreUndyingUsed(loaded.rift.checkpoint.reviveUsed);
                Assert.IsTrue(player.UndyingUsed);
                // 불굴 효과를 다시 받아도 사용 여부를 지우지 않는다.
                AssertUsedUndyingRejected(player);
            }
            finally { Object.DestroyImmediate(go); }
        }

        private static void AssertUsedUndyingRejected(PlayerController player)
        {
            var method = typeof(PlayerController).GetMethod("TryUndying", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            Assert.IsFalse((bool)method.Invoke(player, null), "사용한 판에서는 체력·효과를 건드리기 전에 거절한다");
        }
    }
}
