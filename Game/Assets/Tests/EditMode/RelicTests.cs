using System.Collections.Generic;
using HistoryHeroes.Meta;
using HistoryHeroes.Records;
using HistoryHeroes.Relics;
using HistoryHeroes.Stats;
using NUnit.Framework;
using UnityEngine;

namespace HistoryHeroes.Tests
{
    /// <summary>v0.4 유물 (Relics 2·4·5장)</summary>
    public sealed class RelicTests
    {
        private RelicTable _table;

        private RelicData Add(string id, int set, RelicSource source, int chapter = 0, string value = null)
        {
            var r = ScriptableObject.CreateInstance<RelicData>();
            r.id = id;
            r.displayName = id;
            r.set = set;
            r.source = source;
            r.sourceChapter = chapter;
            r.sourceValue = value;
            _table.relics.Add(r);
            return r;
        }

        [SetUp]
        public void SetUp()
        {
            _table = ScriptableObject.CreateInstance<RelicTable>();
            Add("paiza", 3, RelicSource.HeroFirstClear, value: "khan");
            Add("rain_gauge", 4, RelicSource.BossFirstClear, chapter: 1);
            Add("turtle", 2, RelicSource.ChapterStars, chapter: 1);
            Add("davinci", 0, RelicSource.Achievement, value: "recipes5");
            Add("magna", 1, RelicSource.Achievement, value: "heroes4");
            Add("gutenberg", 1, RelicSource.Achievement, value: "records10");
            Add("sundial", 4, RelicSource.Survival, value: "survive5");
            // 나머지 13개를 세트마다 4개가 되게 채운다
            int[] fill = { 3, 2, 3, 3, 2 };
            for (int set = 0; set < 5; set++)
            {
                for (int i = 0; i < fill[set]; i++) Add($"extra_{set}_{i}", set, RelicSource.BossFirstClear, chapter: 9);
            }
        }

        [Test]
        public void Calculate_AllTwentyAtThreeStars_IsTenPercentAndFourPercentReduction()
        {
            Assert.AreEqual(20, _table.relics.Count);
            var owned = new List<RelicStar>();
            foreach (RelicData r in _table.relics) owned.Add(new RelicStar { id = r.id, stars = 3 });
            RelicService.Bonus bonus = RelicService.Calculate(owned, _table);
            Assert.AreEqual(5, bonus.CompletedSets);
            Assert.AreEqual(0.10f, bonus.AttackHp, 1e-5f, "★60 × 0.1% + 세트 5 × 0.8%");
            Assert.AreEqual(0.04f, bonus.DamageReduction, 1e-5f, "유물 몫 상한 4%");

            owned.RemoveAt(0);
            Assert.AreEqual(4, RelicService.Calculate(owned, _table).CompletedSets, "하나 빠지면 그 세트는 미완성");
        }

        [Test]
        public void CheckAll_GrantsEachSourceOnce()
        {
            var save = new SaveData { unlockedChapter = 2 };
            RelicService.MarkHeroClear(save, "khan");
            for (int s = 1; s <= 3; s++)
            {
                save.stars.Add(Economy.Stars.Key(1, Economy.Stars.Dungeon, s));
                save.stars.Add(Economy.Stars.Key(1, Economy.Stars.Boss, s));
            }

            for (int i = 0; i < 5; i++) save.discoveredRecipes.Add("r" + i);
            List<string> got = RelicService.CheckAll(save, _table, heroesUnlocked: 2);
            Assert.AreEqual(4, got.Count, "파이자·측우기·거북선·다빈치");
            Assert.AreEqual(1, RelicService.Stars(save, "paiza"));
            Assert.AreEqual(0, RelicService.Stars(save, "magna"), "영웅 4명 전에는 없음");
            Assert.AreEqual(0, RelicService.Stars(save, "sundial"), "서바이벌은 v0.5");
            Assert.AreEqual(0, RelicService.CheckAll(save, _table, 2).Count, "한 번만");

            save.relics.recordsSavedTotal = 10;
            Assert.AreEqual(2, RelicService.CheckAll(save, _table, 4).Count, "구텐베르크·마그나 카르타");
        }

        [Test]
        public void Exchange_ThenOriginalSourceRefunds120()
        {
            var save = new SaveData();
            save.AddItem(RelicTable.AncientShard, 120);
            Assert.IsTrue(RelicService.TryExchange(save, _table, "rain_gauge"));
            Assert.AreEqual(0, RelicService.Shards(save));
            Assert.IsFalse(RelicService.TryExchange(save, _table, "rain_gauge"), "이미 가짐");

            save.unlockedChapter = 2; // 원래 획득처(1구간 보스)를 나중에 채움
            List<string> lines = RelicService.CheckAll(save, _table);
            Assert.AreEqual(1, lines.Count);
            StringAssert.Contains("고대 파편 +120", lines[0]);
            Assert.AreEqual(120, RelicService.Shards(save), "중복 대신 돌려받음");
            Assert.AreEqual(1, RelicService.Stars(save, "rain_gauge"), "별은 그대로");
        }

        [Test]
        public void Star_Costs30Then80_AndActiveOnlyAfterTabUnlocks()
        {
            var save = new SaveData { unlockedChapter = 2 };
            save.relics.owned.Add(new RelicStar { id = "paiza", stars = 1 });
            save.AddItem(RelicTable.AncientShard, 110);
            Assert.IsTrue(RelicService.TryStar(save, _table, "paiza"));
            Assert.IsTrue(RelicService.TryStar(save, _table, "paiza"));
            Assert.IsFalse(RelicService.TryStar(save, _table, "paiza"), "★3이 끝");
            Assert.AreEqual(0, RelicService.Shards(save));

            Assert.AreEqual(0, RelicService.Active(save).Count, "유물 탭(2구간 보스) 전에는 보관만");
            save.unlockedChapter = 3;
            Assert.AreEqual(1, RelicService.Active(save).Count);

            var meta = ScriptableObject.CreateInstance<MetaTable>();
            meta.relics = _table;
            MetaSnapshot snap = MetaStats.FromAccount(save, meta, "khan");
            Assert.AreEqual(0.003f, MetaStats.Sum(MetaStats.Build(snap, meta, this), StatType.Attack, ModifierKind.Percent), 1e-5f, "★3 = +0.3%");
        }
    }
}
