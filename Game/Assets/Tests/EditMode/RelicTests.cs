using System.Collections.Generic;
using HistoryHeroes.EditorTools;
using HistoryHeroes.Meta;
using HistoryHeroes.Records;
using HistoryHeroes.Relics;
using HistoryHeroes.Stats;
using NUnit.Framework;
using UnityEngine;

namespace HistoryHeroes.Tests
{
    /// <summary>유물 도감·영웅 첫 클리어·힘 예산 (Relics 2~5장)</summary>
    public sealed class RelicTests
    {
        private RelicTable _table;

        private readonly Dictionary<string, string> _iconKeys = new Dictionary<string, string>();
        private Texture2D _texture;
        private Sprite _icon;

        [SetUp]
        public void SetUp()
        {
            _table = ScriptableObject.CreateInstance<RelicTable>();
            _texture = new Texture2D(1, 1);
            _icon = Sprite.Create(_texture, new Rect(0f, 0f, 1f, 1f), Vector2.zero);
            _iconKeys.Clear();
            string currentId = null;
            // 장면·파일을 만들지 않고 실제 빌더의 목록을 검증한다.
            SceneBuilder.ConfigureRelics(_table, id =>
            {
                currentId = id;
                return ScriptableObject.CreateInstance<RelicData>();
            }, key =>
            {
                _iconKeys.Add(currentId, key);
                return _icon;
            });
        }

        [TearDown]
        public void TearDown()
        {
            foreach (RelicData relic in _table.relics) Object.DestroyImmediate(relic);
            Object.DestroyImmediate(_table);
            Object.DestroyImmediate(_icon);
            Object.DestroyImmediate(_texture);
        }

        [Test]
        public void ConfigureRelics_UpdatesExistingTable_AndKeepsOriginalRelicObjects()
        {
            var existing = new Dictionary<string, RelicData>();
            foreach (RelicData relic in _table.relics) existing.Add(relic.id, relic);
            _table.relics.RemoveAll(relic => relic.set == 5);
            _table.setNames = new[] { "발명의 시대", "기록과 문자", "바다와 항해", "장수의 유품", "하늘과 시간" };
            _table.perStar = 0.001f;
            _table.setBonus = _table.setDamageReduction = 0.008f;

            SceneBuilder.ConfigureRelics(_table, id => existing[id], key => _icon);

            Assert.AreEqual(24, _table.relics.Count);
            Assert.AreEqual(6, _table.setNames.Length);
            Assert.AreEqual(0.0008333333f, _table.perStar, 1e-7f);
            Assert.AreEqual(0.0066666667f, _table.setBonus, 1e-7f);
            Assert.AreEqual(0.0066666667f, _table.setDamageReduction, 1e-7f);
            foreach (RelicData relic in _table.relics)
                Assert.AreSame(existing[relic.id], relic, "기존 에셋을 다시 써서 참조를 지킨다");
        }

        [Test]
        public void Calculate_AllTwentyFourAtThreeStars_IsTenPercentAndFourPercentReduction()
        {
            Assert.AreEqual(24, _table.relics.Count);
            var owned = new List<RelicStar>();
            foreach (RelicData r in _table.relics) owned.Add(new RelicStar { id = r.id, stars = 3 });
            RelicService.Bonus bonus = RelicService.Calculate(owned, _table);
            Assert.AreEqual(6, bonus.CompletedSets);
            Assert.AreEqual(0.10f, bonus.AttackHp, 1e-5f, "★72 × 1/12% + 세트 6 × 2/3%");
            Assert.AreEqual(0.04f, bonus.DamageReduction, 1e-5f, "유물 몫 상한 4%");

            owned.RemoveAt(0);
            Assert.AreEqual(5, RelicService.Calculate(owned, _table).CompletedSets, "하나 빠지면 그 세트는 미완성");
        }

        [Test]
        public void CheckAll_GrantsHeroRelicAndShardsForOldSourcesOnce()
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
            Assert.AreEqual(4, got.Count, "파이자 1개·파편 보상 3개");
            Assert.AreEqual(1, RelicService.Stars(save, "khan_paiza"));
            Assert.AreEqual(0, RelicService.Stars(save, "rain_gauge"));
            Assert.AreEqual(0, RelicService.Stars(save, "turtle_ship_plan"));
            Assert.AreEqual(0, RelicService.Stars(save, "davinci_sketch"));
            Assert.AreEqual(3 * _table.sourceShards, RelicService.Shards(save));
            Assert.AreEqual(0, RelicService.Stars(save, "magna_carta"), "영웅 4명 전에는 없음");
            Assert.AreEqual(0, RelicService.Stars(save, "sundial"), "서바이벌은 v0.5");
            Assert.AreEqual(0, RelicService.CheckAll(save, _table, 2).Count, "한 번만");

            save.relics.recordsSavedTotal = 10;
            Assert.AreEqual(2, RelicService.CheckAll(save, _table, 4).Count, "구텐베르크·마그나 카르타");
            Assert.AreEqual(5 * _table.sourceShards, RelicService.Shards(save));
        }

        [Test]
        public void CheckAll_OldSourceAlreadyOwnedStillGrantsOnlyShardsOnce()
        {
            var save = new SaveData();
            RelicService.Add(save, _table, _table.Find("rain_gauge"));
            save.unlockedChapter = 2;

            Assert.AreEqual(1, RelicService.CheckAll(save, _table).Count);
            Assert.AreEqual(_table.sourceShards, RelicService.Shards(save));
            Assert.AreEqual(1, RelicService.Stars(save, "rain_gauge"), "기존 유물에 별을 더하지 않는다");
            Assert.IsEmpty(RelicService.CheckAll(save, _table));
            Assert.AreEqual(_table.sourceShards, RelicService.Shards(save));
        }

        [Test]
        public void Active_UsesCurrentStarsOnlyAfterTabUnlocks()
        {
            var save = new SaveData { unlockedChapter = 2 };
            var relic = _table.Find("khan_paiza");
            for (int i = 0; i < 4; i++) RelicService.Add(save, _table, relic);

            Assert.AreEqual(0, RelicService.Active(save).Count, "유물 탭(2구간 보스) 전에는 보관만");
            save.unlockedChapter = 3;
            Assert.AreEqual(1, RelicService.Active(save).Count);

            var meta = ScriptableObject.CreateInstance<MetaTable>();
            meta.relics = _table;
            MetaSnapshot snap = MetaStats.FromAccount(save, meta, "khan");
            Assert.AreEqual(0.0025f, MetaStats.Sum(MetaStats.Build(snap, meta, this), StatType.Attack, ModifierKind.Percent), 1e-5f, "★3 = +0.25%");
            Object.DestroyImmediate(meta);
        }

        [TestCase("yisunsin", "yisunsin_diary")]
        [TestCase("yiseonggye", "yiseonggye_bow")]
        [TestCase("davinci", "davinci_gears")]
        [TestCase("zhuge", "zhuge_memorial")]
        public void HeroFirstClear_GrantsOnlyLinkedRelicAtOneStarOnce(string heroId, string relicId)
        {
            RelicData relic = _table.Find(relicId);
            Assert.NotNull(relic);
            Assert.AreEqual(RelicSource.HeroFirstClear, relic.source);
            Assert.AreEqual(heroId, relic.sourceValue);

            var save = new SaveData();
            Assert.IsEmpty(RelicService.CheckAll(save, _table), "해금하거나 쓰기만 해서는 주지 않는다");
            RelicService.MarkHeroClear(save, heroId);
            Assert.AreEqual(1, RelicService.CheckAll(save, _table).Count);
            Assert.AreEqual(1, save.relics.owned.Count);
            Assert.AreEqual(relicId, save.relics.owned[0].id);
            Assert.AreEqual(1, RelicService.Stars(save, relicId));
            RelicService.MarkHeroClear(save, heroId);
            Assert.IsEmpty(RelicService.CheckAll(save, _table));
            Assert.AreEqual(1, save.relics.owned.Count);
            Assert.AreEqual(1, save.relics.heroDungeonClears.Count);
            Assert.AreEqual(0, RelicService.Shards(save), "두 번째 클리어는 중복 보상도 없다");

            var other = new SaveData();
            foreach (RelicData candidate in _table.relics)
            {
                if (candidate.source == RelicSource.HeroFirstClear && candidate.sourceValue != heroId)
                    RelicService.MarkHeroClear(other, candidate.sourceValue);
            }
            RelicService.CheckAll(other, _table);
            Assert.AreEqual(0, RelicService.Stars(other, relicId), "다른 일곱 영웅의 클리어로는 주지 않는다");
        }

        [TestCase("yisunsin_diary", "archive")]
        [TestCase("yiseonggye_bow", "heaven_bow")]
        [TestCase("davinci_gears", "dav_tank")]
        [TestCase("zhuge_memorial", "codex")]
        public void NewRelic_StarsMatchDesign_AndHasStoryAndExistingIcon(string relicId, string iconKey)
        {
            RelicData relic = _table.Find(relicId);
            Assert.AreEqual(5, relic.set);
            StringAssert.Contains("사실:", relic.fact);
            StringAssert.Contains("각색:", relic.fact);
            Assert.IsNotEmpty(relic.story);
            Assert.AreSame(_icon, relic.icon);
            // 2026-10-29: 유물마다 제 그림(relic_id)을 먼저 찾는다. 테스트 로더가 그림을 주므로 공용 아이콘(iconKey)까지 내려가지 않는다
            Assert.AreEqual("relic_" + relicId, _iconKeys[relicId], $"제 그림 먼저 (없으면 {iconKey})");

            float[] expected = { 0.0008333333f, 0.0016666667f, 0.0025f };
            var save = new SaveData { unlockedChapter = 3 };
            save.relics.owned.Add(new RelicStar { id = relicId, stars = 1 });
            var meta = ScriptableObject.CreateInstance<MetaTable>();
            meta.relics = _table;
            try
            {
                for (int stars = 1; stars <= 3; stars++)
                {
                    RelicService.Bonus bonus = RelicService.Calculate(save.relics.owned, _table);
                    Assert.AreEqual(expected[stars - 1], bonus.AttackHp, 1e-7f, $"★{stars}");
                    Assert.AreEqual(0f, bonus.DamageReduction);
                    Assert.AreEqual(0, bonus.CompletedSets);
                    var modifiers = MetaStats.Build(MetaStats.FromAccount(save, meta, relic.sourceValue), meta, this);
                    Assert.AreEqual(expected[stars - 1], MetaStats.Sum(modifiers, StatType.Attack, ModifierKind.Percent), 1e-7f);
                    Assert.AreEqual(expected[stars - 1], MetaStats.Sum(modifiers, StatType.MaxHp, ModifierKind.Percent), 1e-7f);
                    if (stars == 1) RelicService.Add(save, _table, relic);
                    if (stars == 2)
                    {
                        RelicService.Add(save, _table, relic);
                        RelicService.Add(save, _table, relic);
                    }
                }
                Assert.AreEqual(0, RelicService.Shards(save));
            }
            finally
            {
                Object.DestroyImmediate(meta);
            }
        }

        [Test]
        public void NewSet_RequiresEveryMemberAtOneStar_AndBonusDoesNotScaleWithStars()
        {
            var owned = new List<RelicStar>();
            foreach (RelicData relic in _table.relics)
                if (relic.set == 5) owned.Add(new RelicStar { id = relic.id, stars = 1 });
            Assert.AreEqual(4, owned.Count);
            RelicService.Bonus full = RelicService.Calculate(owned, _table);
            Assert.AreEqual(1, full.CompletedSets);
            Assert.AreEqual(0.01f, full.AttackHp, 1e-7f, "★4 + 세트 = 공격력·체력 +1%");
            Assert.AreEqual(0.0066666667f, full.DamageReduction, 1e-7f);
            foreach (RelicStar member in owned)
            {
                member.stars = 0;
                RelicService.Bonus missing = RelicService.Calculate(owned, _table);
                Assert.AreEqual(0, missing.CompletedSets, "어느 하나라도 없으면 미완성");
                Assert.AreEqual(0f, missing.DamageReduction);
                Assert.AreEqual(0.0025f, missing.AttackHp, 1e-7f);
                member.stars = 1;
            }
            foreach (RelicStar member in owned) member.stars = 3;
            RelicService.Bonus upgraded = RelicService.Calculate(owned, _table);
            Assert.AreEqual(1, upgraded.CompletedSets);
            Assert.AreEqual(full.DamageReduction, upgraded.DamageReduction);
            Assert.AreEqual(0.0166666667f, upgraded.AttackHp, 1e-7f);
        }

        [Test]
        public void Catalog_HasTwentyFourUniqueRelics_SixEqualSets_AndEightHeroLinks()
        {
            Assert.AreEqual(24, _table.relics.Count);
            Assert.AreEqual(6, _table.setNames.Length);
            var ids = new HashSet<string>();
            var heroes = new HashSet<string>();
            int[] sets = new int[6];
            foreach (RelicData relic in _table.relics)
            {
                Assert.IsTrue(ids.Add(relic.id), "저장 ID는 겹치면 안 된다");
                Assert.That(relic.set, Is.InRange(0, 5));
                sets[relic.set]++;
                if (relic.source == RelicSource.HeroFirstClear) Assert.IsTrue(heroes.Add(relic.sourceValue));
            }
            Assert.AreEqual(8, heroes.Count);
            foreach (int count in sets) Assert.AreEqual(4, count);
        }

        [TestCase("yisunsin", "yisunsin_diary")]
        [TestCase("yiseonggye", "yiseonggye_bow")]
        [TestCase("davinci", "davinci_gears")]
        [TestCase("zhuge", "zhuge_memorial")]
        public void NewHeroClear_AfterGachaDrawCountsAsDuplicateOnce(string heroId, string relicId)
        {
            var save = new SaveData();
            RelicService.Add(save, _table, _table.Find(relicId));
            RelicService.MarkHeroClear(save, heroId);
            Assert.AreEqual(1, RelicService.CheckAll(save, _table).Count);
            Assert.AreEqual(0, RelicService.Shards(save));
            Assert.AreEqual(2, RelicService.Stars(save, relicId));
            RelicService.MarkHeroClear(save, heroId);
            Assert.IsEmpty(RelicService.CheckAll(save, _table));
            Assert.AreEqual(0, RelicService.Shards(save));
            Assert.AreEqual(1, save.relics.owned.Count);
        }
    }
}
