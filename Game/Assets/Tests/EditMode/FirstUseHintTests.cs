using System;
using System.IO;
using HistoryHeroes.Economy;
using HistoryHeroes.Equipment;
using HistoryHeroes.Heroes;
using HistoryHeroes.Meta;
using HistoryHeroes.Missions;
using HistoryHeroes.Progress;
using HistoryHeroes.Records;
using NUnit.Framework;
using UnityEngine;

namespace HistoryHeroes.Tests
{
    public sealed class FirstUseHintTests
    {
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.FromHours(9));

        private static SaveData Started()
        {
            var save = new SaveData { tutorialDone = true };
            Newcomer.EnsureStarted(save, Now);
            return save;
        }

        [TestCase(false, false, false)]
        [TestCase(true, true, false)]
        [TestCase(true, false, true)]
        public void Next_BlocksBeforeTutorialDuringCombatAndBehindPanels(bool tutorialDone, bool combat, bool panel)
        {
            SaveData save = Started();
            save.tutorialDone = tutorialDone;
            Assert.IsNull(FirstUseHints.Next(save, combat, panel, Now, true, true, 1));
            Assert.IsEmpty(save.seenFirstUseHints);
        }

        [Test]
        public void Next_OffersOneRelevantHintInOrderWithoutChangingSave()
        {
            SaveData save = Started();
            save.AddItem(Wallet.Ticket, 1);
            string[] order = { FirstUseHints.Newcomer, FirstUseHints.HeroLevel, FirstUseHints.Gear, FirstUseHints.Missions, FirstUseHints.Shop };
            foreach (string expected in order)
            {
                int before = save.seenFirstUseHints.Count;
                Assert.AreEqual(expected, FirstUseHints.Next(save, false, false, Now, true, true, 1));
                Assert.AreEqual(before, save.seenFirstUseHints.Count);
                Assert.AreEqual(1, save.ItemCount(Wallet.Ticket));
                Assert.IsEmpty(save.heroProgress);
                Assert.IsTrue(FirstUseHints.MarkShown(save, expected));
                Assert.IsFalse(FirstUseHints.MarkShown(save, expected));
            }
            Assert.IsNull(FirstUseHints.Next(save, false, false, Now, true, true, 1));
        }

        [Test]
        public void Next_WaitsForRelevantResourcesAndDoesNotPromotePaidPurchases()
        {
            SaveData save = Started();
            FirstUseHints.MarkShown(save, FirstUseHints.Newcomer);
            save.gold = 999999;
            save.gemsFree = 999999;
            Assert.IsNull(FirstUseHints.Next(save, false, false, Now, false, false, 0));
            Assert.AreEqual(FirstUseHints.HeroLevel, FirstUseHints.Next(save, false, false, Now, true, false, 0));
            Assert.AreEqual(FirstUseHints.Gear, FirstUseHints.Next(save, false, false, Now, false, true, 0));
            Assert.AreEqual(FirstUseHints.Missions, FirstUseHints.Next(save, false, false, Now, false, false, 1));
            save.AddItem(Wallet.Ticket, 1);
            Assert.AreEqual(FirstUseHints.Shop, FirstUseHints.Next(save, false, false, Now, false, false, 0));
            Assert.IsFalse(FirstUseHints.MarkShown(save, "unknown"));
            Assert.IsFalse(FirstUseHints.MarkShown(save, null));
        }

        [Test]
        public void HeroLevel_UsesExactCostUnlockedHeroesAndCapWithoutCreatingProgress()
        {
            var roster = ScriptableObject.CreateInstance<HeroRoster>();
            var hero = ScriptableObject.CreateInstance<HeroData>();
            var table = ScriptableObject.CreateInstance<MetaTable>();
            try
            {
                hero.id = "khan";
                hero.gemsOnly = false;
                hero.unlockAfterBossChapter = 0;
                roster.heroes.Add(hero);
                var save = new SaveData { gold = table.HeroLevelCost(1) - 1 };
                Assert.IsFalse(FirstUseHints.CanLevelUp(save, table, roster));
                save.gold++;
                Assert.IsTrue(FirstUseHints.CanLevelUp(save, table, roster));
                Assert.IsEmpty(save.heroProgress);
                hero.gemsOnly = true;
                Assert.IsFalse(FirstUseHints.CanLevelUp(save, table, roster));
                save.unlockedHeroes.Add(hero.id);
                save.heroProgress.Add(new HeroProgress { heroId = hero.id, level = table.maxHeroLevel });
                Assert.IsFalse(FirstUseHints.CanLevelUp(save, table, roster));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(table);
                UnityEngine.Object.DestroyImmediate(hero);
                UnityEngine.Object.DestroyImmediate(roster);
            }
        }

        [Test]
        public void Gear_RequiresUnlockAndKnownOwnedEquipmentWithoutCreatingLoadouts()
        {
            var table = ScriptableObject.CreateInstance<EquipmentTable>();
            var gear = ScriptableObject.CreateInstance<EquipmentData>();
            try
            {
                gear.id = "test_weapon";
                table.items.Add(gear);
                var save = new SaveData();
                save.equipment.items.Add(new EquipmentItem { uid = "e1", dataId = gear.id });
                Assert.IsFalse(FirstUseHints.HasGear(save, table));
                save.unlockedChapter = 2;
                Assert.IsTrue(FirstUseHints.HasGear(save, table));
                save.equipment.items[0].dataId = "missing";
                Assert.IsFalse(FirstUseHints.HasGear(save, table));
                Assert.IsEmpty(save.equipment.loadouts);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gear);
                UnityEngine.Object.DestroyImmediate(table);
            }
        }

        [TestCase(1, "7일 미션\n<size=75%>1/7일</size>")]
        [TestCase(7, "7일 미션\n<size=75%>7/7일</size>")]
        [TestCase(20, "7일 미션\n<size=75%>7/7일</size>")]
        public void NewcomerHomeLabel_ShowsOpenedDayAndCapsAtSeven(int days, string expected)
        {
            SaveData save = Started();
            save.newcomer.activeDays = days;
            Assert.AreEqual(expected, Newcomer.HomeLabel(save, Now));
            save.newcomer.bonusClaimed = true;
            Assert.AreEqual("출석부", Newcomer.HomeLabel(save, Now));
            Assert.IsNull(FirstUseHints.Next(save, false, false, Now, false, false, 0));
        }

        [Test]
        public void Hints_PersistAcrossReloadAndVersionTenMigrationKeepsProgress()
        {
            string folder = Path.Combine(Path.GetTempPath(), "hh-hints-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                var store = new SaveStore(folder);
                File.WriteAllText(store.MainPath, SaveStore.Sign("{\"schemaVersion\":10,\"gold\":77,\"tutorialDone\":true,\"seenFirstUseHints\":null}"));
                SaveData save = store.Load();
                Assert.AreEqual(SaveData.CurrentSchemaVersion, save.schemaVersion);
                Assert.AreEqual(77, save.gold);
                Assert.IsTrue(save.tutorialDone);
                Assert.IsEmpty(save.seenFirstUseHints);
                Newcomer.EnsureStarted(save, Now);
                Assert.IsTrue(FirstUseHints.MarkShown(save, FirstUseHints.Newcomer));
                store.Save(save);
                SaveData loaded = new SaveStore(folder).Load();
                CollectionAssert.AreEqual(new[] { FirstUseHints.Newcomer }, loaded.seenFirstUseHints);
                Assert.IsNull(FirstUseHints.Next(loaded, false, false, Now, false, false, 0));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [Test]
        public void NewcomerHomeLabel_UsesActiveDaysAndBadgeOnlyWhenRewardIsClaimable()
        {
            SaveData save = Started();
            Assert.AreEqual("7일 미션\n<size=75%>1/7일</size>", Newcomer.HomeLabel(save, Now.AddDays(50)));
            Assert.IsFalse(UI.UpgradeNotice.HasNewcomerReward(save, Now));
            save.stars.Add(Stars.Key(1, Stars.Dungeon, 1));
            save.unlockedChapter = 2;
            save.relics.recordsSavedTotal = 1;
            Assert.IsTrue(UI.UpgradeNotice.HasNewcomerReward(save, Now));
            save.newcomer.claimedDays.Add(1);
            Assert.IsFalse(UI.UpgradeNotice.HasNewcomerReward(save, Now));
        }

        [Test]
        public void HintSaveFailure_RollsBackSeenState()
        {
            SaveStore previous = SaveSystem.Store;
            string blocker = Path.GetTempFileName();
            try
            {
                SaveSystem.Use(new SaveStore(Path.Combine(blocker, "blocked")));
                SaveData save = SaveSystem.Data;
                save.tutorialDone = true;
                UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("저장 실패"));
                Assert.AreEqual(SaveSystem.CommitResult.SaveFailed,
                    SaveSystem.Commit(() => FirstUseHints.MarkShown(save, FirstUseHints.Newcomer)));
                Assert.IsEmpty(SaveSystem.Data.seenFirstUseHints);
            }
            finally
            {
                SaveSystem.Use(previous);
                File.Delete(blocker);
            }
        }
    }
}
