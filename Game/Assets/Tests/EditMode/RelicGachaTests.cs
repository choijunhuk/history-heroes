using System;
using System.Collections.Generic;
using System.IO;
using HistoryHeroes.Economy;
using HistoryHeroes.EditorTools;
using HistoryHeroes.Records;
using HistoryHeroes.Relics;
using NUnit.Framework;
using UnityEngine;

namespace HistoryHeroes.Tests
{
    /// <summary>유물 뽑기: 실제 빌더의 24종·가격표로 재화와 보장 경계를 확인한다.</summary>
    public sealed class RelicGachaTests
    {
        private RelicTable _table;
        private Func<DateTimeOffset> _originalClock;

        [SetUp]
        public void SetUp()
        {
            _originalClock = Clock.Now;
            _table = ScriptableObject.CreateInstance<RelicTable>();
            SceneBuilder.ConfigureRelics(_table, id => ScriptableObject.CreateInstance<RelicData>(), key => null);
        }

        [TearDown]
        public void TearDown()
        {
            Clock.Now = _originalClock;
            foreach (RelicData relic in _table.relics) UnityEngine.Object.DestroyImmediate(relic);
            UnityEngine.Object.DestroyImmediate(_table);
        }

        private SaveData Ready() => new SaveData { unlockedChapter = 3 };

        private static float Zero() => 0f;

        private void FillExceptLast(SaveData save)
        {
            for (int i = 0; i < _table.relics.Count - 1; i++)
                save.relics.owned.Add(new RelicStar { id = _table.relics[i].id, stars = 1 });
        }

        [Test]
        public void Rates_EachRelicHasOneTwentyFourthAndTotalsOne()
        {
            var save = Ready();
            float total = 0f;
            foreach (RelicData relic in _table.relics)
            {
                float rate = RelicGachaService.Rate(save, _table, relic.id);
                Assert.AreEqual(1f / 24f, rate, 1e-7f, relic.id);
                total += rate;
            }
            Assert.AreEqual(1f, total, 1e-6f);
        }

        [Test]
        public void Table_PricesAndDuplicateRulesMatchPublishedDesign()
        {
            Assert.AreEqual(100, _table.pullShardCost);
            Assert.AreEqual(900, _table.tenPullShardCost);
            Assert.AreEqual(100, _table.pullGemCost);
            Assert.AreEqual(900, _table.tenPullGemCost);
            Assert.AreEqual(20, _table.newRelicPity);
            CollectionAssert.AreEqual(new[] { 1, 2 }, _table.duplicateCosts);
            Assert.AreEqual(5, _table.overflowShards);
            Assert.AreEqual(20, _table.sourceShards);
        }

        [Test]
        public void Rates_GuaranteeDistributesOnlyAcrossMissingRelics()
        {
            var save = Ready();
            for (int i = 0; i < 22; i++)
                save.relics.owned.Add(new RelicStar { id = _table.relics[i].id, stars = 1 });
            save.relicGacha.newRelicPity = 19;

            Assert.AreEqual(0f, RelicGachaService.Rate(save, _table, _table.relics[0].id, true));
            Assert.AreEqual(0.5f, RelicGachaService.Rate(save, _table, _table.relics[22].id, true));
            Assert.AreEqual(0.5f, RelicGachaService.Rate(save, _table, _table.relics[23].id, true));
        }

        [Test]
        public void CanOpen_RejectsIncompleteCatalogWithoutSpending()
        {
            var save = Ready();
            save.AddItem(RelicTable.AncientShard, _table.pullShardCost);
            RelicData last = _table.relics[23];
            _table.relics.RemoveAt(23);
            try
            {
                string before = JsonUtility.ToJson(save);
                Assert.IsFalse(RelicGachaService.CanOpen(_table));
                Assert.IsFalse(RelicGachaService.TryPull(save, _table, 1, RelicGachaService.Pay.Shards, "2026-10-29", 0, out _, Zero));
                Assert.AreEqual(before, JsonUtility.ToJson(save));
            }
            finally { _table.relics.Add(last); }
        }

        [Test]
        public void CanOpen_RejectsDuplicateIdWithoutSpending()
        {
            var save = Ready();
            save.gemsFree = 100;
            string original = _table.relics[23].id;
            _table.relics[23].id = _table.relics[0].id;
            try
            {
                Assert.IsFalse(RelicGachaService.CanOpen(_table));
                Assert.IsFalse(RelicGachaService.TryPull(save, _table, 1, RelicGachaService.Pay.Gems,
                    "2026-10-29", 0, out _, Zero));
                Assert.AreEqual(100, save.gemsFree);
            }
            finally { _table.relics[23].id = original; }
        }

        [Test]
        public void NewRelicPity_DrawNineteenDuplicatesThenGuaranteesTwentiethNew()
        {
            var save = Ready();
            FillExceptLast(save);
            save.AddItem(RelicTable.AncientShard, 20 * _table.pullShardCost);
            string day = "2026-10-29";
            for (int i = 0; i < 19; i++)
            {
                Assert.IsTrue(RelicGachaService.TryPull(save, _table, 1, RelicGachaService.Pay.Shards, day, i, out var pulls, Zero));
                Assert.IsFalse(pulls[0].IsNew, $"{i + 1}회");
            }
            Assert.AreEqual(1, RelicGachaService.PullsUntilPity(save, _table));
            Assert.IsTrue(RelicGachaService.TryPull(save, _table, 1, RelicGachaService.Pay.Shards, day, 20, out var last, Zero));
            Assert.AreEqual(_table.relics[23].id, last[0].Data.id);
            Assert.IsTrue(last[0].FromPity);
            Assert.IsTrue(last[0].IsNew);
            Assert.AreEqual(0, save.relicGacha.newRelicPity);
        }

        [Test]
        public void NewRelicPity_NaturalNewDrawResetsCounter()
        {
            var save = Ready();
            FillExceptLast(save);
            save.relicGacha.newRelicPity = 12;
            save.AddItem(RelicTable.AncientShard, _table.pullShardCost);
            Assert.IsTrue(RelicGachaService.TryPull(save, _table, 1, RelicGachaService.Pay.Shards,
                "2026-10-29", 0, out var pulls, () => 0.999f));
            Assert.IsTrue(pulls[0].IsNew);
            Assert.IsFalse(pulls[0].FromPity);
            Assert.AreEqual(0, save.relicGacha.newRelicPity);
        }

        [Test]
        public void TenPull_PityAppliesToFifthDrawWhenCounterStartsAtFifteen()
        {
            var save = Ready();
            FillExceptLast(save);
            save.relicGacha.newRelicPity = 15;
            save.AddItem(RelicTable.AncientShard, _table.tenPullShardCost);
            Assert.IsTrue(RelicGachaService.TryPull(save, _table, 10, RelicGachaService.Pay.Shards,
                "2026-10-29", 0, out var pulls, Zero));
            Assert.AreEqual(10, pulls.Count);
            Assert.IsTrue(pulls[4].FromPity);
            Assert.IsTrue(pulls[4].IsNew);
            Assert.AreEqual(0, save.relicGacha.newRelicPity, "전종 보유 뒤에는 보장이 쉰다");
        }

        [Test]
        public void NewRelicPity_StopsWhenAllRelicsOwned()
        {
            var save = Ready();
            foreach (RelicData relic in _table.relics)
                save.relics.owned.Add(new RelicStar { id = relic.id, stars = 1 });
            save.relicGacha.newRelicPity = 19;
            save.AddItem(RelicTable.AncientShard, _table.pullShardCost);
            Assert.AreEqual(0, RelicGachaService.PullsUntilPity(save, _table));
            Assert.IsTrue(RelicGachaService.TryPull(save, _table, 1, RelicGachaService.Pay.Shards,
                "2026-10-29", 0, out var pulls, Zero));
            Assert.IsFalse(pulls[0].FromPity);
            Assert.AreEqual(0, save.relicGacha.newRelicPity);
        }

        [Test]
        public void Duplicates_FirstRaisesTwoAndThirdAfterTwoMore()
        {
            var save = Ready();
            RelicData relic = _table.Find("khan_paiza");
            RelicService.Add(save, _table, relic);
            var second = RelicService.Add(save, _table, relic);
            Assert.AreEqual(2, second.Stars);
            Assert.IsTrue(second.StarUp);
            var third = RelicService.Add(save, _table, relic);
            Assert.AreEqual(2, third.Stars);
            Assert.AreEqual(1, third.Duplicates);
            Assert.IsFalse(third.StarUp);
            var fourth = RelicService.Add(save, _table, relic);
            Assert.AreEqual(3, fourth.Stars);
            Assert.IsTrue(fourth.StarUp);
        }

        [Test]
        public void Duplicates_AfterThreeStarsConvertToFiveShards()
        {
            var save = Ready();
            RelicData relic = _table.Find("khan_paiza");
            for (int i = 0; i < 4; i++) RelicService.Add(save, _table, relic);
            var fifth = RelicService.Add(save, _table, relic);
            Assert.AreEqual(3, fifth.Stars);
            Assert.AreEqual(_table.overflowShards, fifth.Shards);
            Assert.AreEqual(_table.overflowShards, RelicService.Shards(save));
        }

        [Test]
        public void FreePull_OnePerLocalDayAndNextDayResets()
        {
            var save = Ready();
            Clock.Now = () => new DateTimeOffset(2026, 10, 29, 23, 59, 0, TimeSpan.FromHours(9));
            Assert.IsTrue(RelicGachaService.TryPull(save, _table, 1, RelicGachaService.Pay.Free,
                Clock.Today, Clock.UnixSeconds, out _, Zero));
            Assert.IsFalse(RelicGachaService.CanFree(save, Clock.Today));
            string afterFirst = JsonUtility.ToJson(save);
            Assert.IsFalse(RelicGachaService.TryPull(save, _table, 1, RelicGachaService.Pay.Free,
                Clock.Today, Clock.UnixSeconds, out _, Zero));
            Assert.AreEqual(afterFirst, JsonUtility.ToJson(save));
            Clock.Now = () => new DateTimeOffset(2026, 10, 30, 0, 1, 0, TimeSpan.FromHours(9));
            Assert.IsTrue(RelicGachaService.CanFree(save, Clock.Today));
            Assert.IsTrue(RelicGachaService.TryPull(save, _table, 1, RelicGachaService.Pay.Free,
                Clock.Today, Clock.UnixSeconds, out _, Zero));
            Assert.AreEqual(2, save.relicGacha.totalPulls);
        }

        [Test]
        public void TenPull_SpendsNineHundredShardsForTenResults()
        {
            var save = Ready();
            save.AddItem(RelicTable.AncientShard, 1000);
            Assert.AreEqual(10 * _table.pullShardCost * 9 / 10, _table.tenPullShardCost);
            Assert.IsTrue(RelicGachaService.TryPull(save, _table, 10, RelicGachaService.Pay.Shards,
                "2026-10-29", 0, out var pulls, Zero));
            Assert.AreEqual(10, pulls.Count);
            int overflowRefund = 0;
            foreach (var pull in pulls) overflowRefund += pull.Shards;
            Assert.AreEqual(100 + overflowRefund, RelicService.Shards(save), "구매값 900과 ★3 초과 환급을 따로 센다");
            Assert.AreEqual(10, save.relicGacha.totalPulls);
        }

        [Test]
        public void TenPull_SpendsNineHundredGemsForTenResults()
        {
            var save = Ready();
            save.gemsFree = 1000;
            Assert.AreEqual(10 * _table.pullGemCost * 9 / 10, _table.tenPullGemCost);
            Assert.IsTrue(RelicGachaService.TryPull(save, _table, 10, RelicGachaService.Pay.Gems,
                "2026-10-29", 0, out var pulls, Zero));
            Assert.AreEqual(10, pulls.Count);
            Assert.AreEqual(100, save.gemsFree);
        }

        [TestCase(RelicGachaService.Pay.Shards)]
        [TestCase(RelicGachaService.Pay.Gems)]
        public void InsufficientCurrency_LeavesEverySaveFieldAndRngUntouched(RelicGachaService.Pay pay)
        {
            var save = Ready();
            save.AddItem(RelicTable.AncientShard, _table.tenPullShardCost - 1);
            save.gemsFree = _table.tenPullGemCost - 1;
            string before = JsonUtility.ToJson(save);
            bool rolled = false;
            Assert.IsFalse(RelicGachaService.TryPull(save, _table, 10, pay, "2026-10-29", 0,
                out _, () => { rolled = true; return 0f; }));
            Assert.IsFalse(rolled);
            Assert.AreEqual(before, JsonUtility.ToJson(save));
        }

        [Test]
        public void Gems_FreeBalanceIsSpentBeforePaidBalance()
        {
            var save = Ready();
            save.gemsFree = 60;
            save.gemsPaid = 70;
            Assert.IsTrue(RelicGachaService.TryPull(save, _table, 1, RelicGachaService.Pay.Gems,
                "2026-10-29", 0, out _, Zero));
            Assert.AreEqual(0, save.gemsFree);
            Assert.AreEqual(30, save.gemsPaid);
        }

        [Test]
        public void HeroFirstClear_GachaDuplicateRaisesStar()
        {
            var save = new SaveData();
            RelicService.MarkHeroClear(save, "khan");
            Assert.AreEqual(1, RelicService.CheckAll(save, _table).Count);
            save.unlockedChapter = 3;
            save.AddItem(RelicTable.AncientShard, _table.pullShardCost);
            int index = _table.relics.FindIndex(r => r.id == "khan_paiza");
            Assert.IsTrue(RelicGachaService.TryPull(save, _table, 1, RelicGachaService.Pay.Shards,
                "2026-10-29", 0, out var pulls, () => (index + 0.5f) / _table.relics.Count));
            Assert.AreEqual("khan_paiza", pulls[0].Data.id);
            Assert.IsTrue(pulls[0].StarUp);
            Assert.AreEqual(2, RelicService.Stars(save, "khan_paiza"));
        }

        [Test]
        public void HeroFirstClear_AfterDrawAddsOneCopyForEachOfEightHeroes()
        {
            int heroes = 0;
            foreach (RelicData relic in _table.relics)
            {
                if (relic.source != RelicSource.HeroFirstClear) continue;
                var save = new SaveData();
                RelicService.Add(save, _table, relic);
                RelicService.MarkHeroClear(save, relic.sourceValue);
                Assert.AreEqual(1, RelicService.CheckAll(save, _table).Count);
                Assert.AreEqual(2, RelicService.Stars(save, relic.id));
                Assert.IsEmpty(RelicService.CheckAll(save, _table));
                Assert.AreEqual(2, RelicService.Stars(save, relic.id));
                heroes++;
            }
            Assert.AreEqual(8, heroes);
        }

        [Test]
        public void AllRelicsAtThreeStarsKeepTenPercentAttackHpAndFourPercentReduction()
        {
            var save = Ready();
            foreach (RelicData relic in _table.relics)
                for (int copy = 0; copy < 4; copy++) RelicService.Add(save, _table, relic);
            RelicService.Bonus bonus = RelicService.Calculate(save.relics.owned, _table);
            Assert.AreEqual(6, bonus.CompletedSets);
            Assert.AreEqual(0.10f, bonus.AttackHp, 1e-6f);
            Assert.AreEqual(0.04f, bonus.DamageReduction, 1e-6f);
        }

        [Test]
        public void Load_SchemaSixWithoutGachaFieldsStartsAtZeroAndKeepsStars()
        {
            string folder = Path.Combine(Path.GetTempPath(), "hh_relic_gacha_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                var store = new SaveStore(folder);
                var old = new SaveData { schemaVersion = 6, unlockedChapter = 3 };
                old.relics.owned.Add(new RelicStar { id = "khan_paiza", stars = 2 });
                string json = JsonUtility.ToJson(old, true).Replace("\"relicGacha\"", "\"relicGacha_old\"");
                json = json.Replace("\"duplicates\"", "\"duplicates_old\"");
                File.WriteAllText(store.MainPath, SaveStore.Sign(json));
                SaveData loaded = store.Load();
                Assert.AreEqual(SaveData.CurrentSchemaVersion, loaded.schemaVersion);
                Assert.AreEqual(2, RelicService.Stars(loaded, "khan_paiza"));
                Assert.AreEqual(0, loaded.relics.owned[0].duplicates);
                Assert.NotNull(loaded.relicGacha);
                Assert.AreEqual(0, loaded.relicGacha.newRelicPity);
                Assert.AreEqual(0, loaded.relicGacha.totalPulls);
                Assert.IsTrue(string.IsNullOrEmpty(loaded.relicGacha.freeDate));
            }
            finally { Directory.Delete(folder, true); }
        }

        [Test]
        public void TryPull_InvalidCountOrDayNeverCharges()
        {
            var save = Ready();
            save.AddItem(RelicTable.AncientShard, 1000);
            string before = JsonUtility.ToJson(save);
            Assert.IsFalse(RelicGachaService.TryPull(save, _table, 2, RelicGachaService.Pay.Shards,
                "2026-10-29", 0, out _, Zero));
            Assert.IsFalse(RelicGachaService.TryPull(save, _table, 1, RelicGachaService.Pay.Free,
                null, 0, out _, Zero));
            Assert.AreEqual(before, JsonUtility.ToJson(save));
        }

        [Test]
        public void TryPull_BeforeRelicUnlockNeverCharges()
        {
            var save = new SaveData { unlockedChapter = 2 };
            save.AddItem(RelicTable.AncientShard, 100);
            string before = JsonUtility.ToJson(save);
            Assert.IsFalse(RelicGachaService.TryPull(save, _table, 1, RelicGachaService.Pay.Shards,
                "2026-10-29", 0, out _, Zero));
            Assert.AreEqual(before, JsonUtility.ToJson(save));
        }

        [Test]
        public void TenPull_InvalidLastRandomValueLeavesWholeSaveUntouched()
        {
            var save = Ready();
            save.gemsFree = 1000;
            string before = JsonUtility.ToJson(save);
            int calls = 0;
            Assert.IsFalse(RelicGachaService.TryPull(save, _table, 10, RelicGachaService.Pay.Gems,
                "2026-10-29", 0, out _, () => ++calls == 10 ? float.NaN : 0f));
            Assert.AreEqual(10, calls);
            Assert.AreEqual(before, JsonUtility.ToJson(save));
        }

        [Test]
        public void RandomOne_SelectsLastRelicAndPaidDrawDoesNotUseDailyFree()
        {
            var save = Ready();
            save.gemsFree = 100;
            Assert.IsTrue(RelicGachaService.TryPull(save, _table, 1, RelicGachaService.Pay.Gems,
                "2026-10-29", 1, out var pulls, () => 1f));
            Assert.AreSame(_table.relics[23], pulls[0].Data);
            Assert.IsTrue(RelicGachaService.CanFree(save, "2026-10-29"));
            Assert.AreEqual(1, save.relicGacha.log.Count);
            Assert.AreEqual("relic", save.relicGacha.log[0].box);
            Assert.AreEqual(pulls[0].Data.id, save.relicGacha.log[0].dataId);
        }

        [TestCase(RelicGachaService.Pay.Shards)]
        [TestCase(RelicGachaService.Pay.Gems)]
        [TestCase(RelicGachaService.Pay.Free)]
        public void Commit_SaveFailureRestoresCurrencyRelicsPityFreeDateAndLog(RelicGachaService.Pay pay)
        {
            string blocker = Path.Combine(Path.GetTempPath(), "hh_relic_block_" + Guid.NewGuid().ToString("N"));
            File.WriteAllText(blocker, "x");
            SaveStore previous = SaveSystem.Store;
            try
            {
                SaveSystem.Use(new SaveStore(Path.Combine(blocker, "sub")));
                SaveData save = SaveSystem.Data;
                save.unlockedChapter = 3;
                save.gemsFree = 1000;
                save.AddItem(RelicTable.AncientShard, 1000);
                save.relicGacha.newRelicPity = 19;
                string before = JsonUtility.ToJson(save);
                UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("저장 실패"));
                Assert.AreEqual(SaveSystem.CommitResult.SaveFailed, SaveSystem.Commit(() =>
                    RelicGachaService.TryPull(SaveSystem.Data, _table, 1, pay, "2026-10-29", 0, out _, Zero)));
                Assert.AreEqual(before, JsonUtility.ToJson(SaveSystem.Data));
            }
            finally
            {
                SaveSystem.Use(previous);
                File.Delete(blocker);
            }
        }

        [Test]
        public void RatesText_ListsAllRelicsAndBothNormalAndGuaranteedRules()
        {
            var save = Ready();
            string text = RelicGachaService.RatesText(save, _table);
            foreach (RelicData relic in _table.relics) StringAssert.Contains(relic.displayName, text);
            StringAssert.Contains("20", text);
            StringAssert.Contains("1/24", text);
            StringAssert.Contains("★3", text);
        }
    }
}
