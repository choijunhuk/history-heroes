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
    /// <summary>v0.4 유물 (Relics 2·4·5장)</summary>
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
            Assert.AreEqual(1, RelicService.Stars(save, "khan_paiza"));
            Assert.AreEqual(0, RelicService.Stars(save, "magna_carta"), "영웅 4명 전에는 없음");
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
            save.relics.owned.Add(new RelicStar { id = "khan_paiza", stars = 1 });
            save.AddItem(RelicTable.AncientShard, 110);
            Assert.IsTrue(RelicService.TryStar(save, _table, "khan_paiza"));
            Assert.IsTrue(RelicService.TryStar(save, _table, "khan_paiza"));
            Assert.IsFalse(RelicService.TryStar(save, _table, "khan_paiza"), "★3이 끝");
            Assert.AreEqual(0, RelicService.Shards(save));

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
            Assert.AreEqual(iconKey, _iconKeys[relicId]);

            float[] expected = { 0.0008333333f, 0.0016666667f, 0.0025f };
            var save = new SaveData { unlockedChapter = 3 };
            save.relics.owned.Add(new RelicStar { id = relicId, stars = 1 });
            save.AddItem(RelicTable.AncientShard, 110);
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
                    if (stars < 3) Assert.IsTrue(RelicService.TryStar(save, _table, relicId));
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
        public void Catalog_HasTwentyFourUniqueRelics_SixEqualSets_AndCosts2640Shards()
        {
            Assert.AreEqual(24, _table.relics.Count);
            Assert.AreEqual(6, _table.setNames.Length);
            var ids = new HashSet<string>();
            var heroes = new HashSet<string>();
            var save = new SaveData();
            int[] sets = new int[6];
            foreach (RelicData relic in _table.relics)
            {
                Assert.IsTrue(ids.Add(relic.id), "저장 ID는 겹치면 안 된다");
                Assert.That(relic.set, Is.InRange(0, 5));
                sets[relic.set]++;
                if (relic.source == RelicSource.HeroFirstClear) Assert.IsTrue(heroes.Add(relic.sourceValue));
                save.relics.owned.Add(new RelicStar { id = relic.id, stars = 1 });
            }
            Assert.AreEqual(8, heroes.Count);
            foreach (int count in sets) Assert.AreEqual(4, count);
            Assert.AreEqual(30, _table.StarCost(1));
            Assert.AreEqual(80, _table.StarCost(2));
            save.AddItem(RelicTable.AncientShard, 2640);
            foreach (RelicData relic in _table.relics)
            {
                Assert.IsTrue(RelicService.TryStar(save, _table, relic.id));
                Assert.IsTrue(RelicService.TryStar(save, _table, relic.id));
                Assert.AreEqual(3, RelicService.Stars(save, relic.id));
                Assert.IsFalse(RelicService.TryStar(save, _table, relic.id));
            }
            Assert.AreEqual(0, RelicService.Shards(save), "24 × (30 + 80) = 2,640");
        }

        [TestCase("yisunsin", "yisunsin_diary")]
        [TestCase("yiseonggye", "yiseonggye_bow")]
        [TestCase("davinci", "davinci_gears")]
        [TestCase("zhuge", "zhuge_memorial")]
        public void NewHeroClear_AfterExchangeRefundsOnce_AndPreservesStars(string heroId, string relicId)
        {
            var save = new SaveData();
            save.AddItem(RelicTable.AncientShard, 230);
            Assert.IsTrue(RelicService.TryExchange(save, _table, relicId));
            Assert.IsTrue(RelicService.TryStar(save, _table, relicId));
            Assert.IsTrue(RelicService.TryStar(save, _table, relicId));
            RelicService.MarkHeroClear(save, heroId);
            Assert.AreEqual(1, RelicService.CheckAll(save, _table).Count);
            Assert.AreEqual(120, RelicService.Shards(save));
            Assert.AreEqual(3, RelicService.Stars(save, relicId));
            RelicService.MarkHeroClear(save, heroId);
            Assert.IsEmpty(RelicService.CheckAll(save, _table));
            Assert.AreEqual(120, RelicService.Shards(save));
            Assert.AreEqual(1, save.relics.owned.Count);
        }
    }
}
