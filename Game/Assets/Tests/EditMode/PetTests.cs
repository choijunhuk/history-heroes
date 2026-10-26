using System.Collections.Generic;
using System.IO;
using HistoryHeroes.Meta;
using HistoryHeroes.Pets;
using HistoryHeroes.Records;
using HistoryHeroes.Stats;
using NUnit.Framework;
using UnityEngine;

namespace HistoryHeroes.Tests
{
    /// <summary>v0.5 펫 (Pets 2~5장)</summary>
    public sealed class PetTests
    {
        private PetTable _table;

        private PetData Add(string id, PetKind kind, string bond, PetSource source, int chapter = 0)
        {
            var p = ScriptableObject.CreateInstance<PetData>();
            p.id = id;
            p.displayName = id;
            p.kind = kind;
            p.bondHeroId = bond;
            p.source = source;
            p.sourceChapter = chapter;
            _table.pets.Add(p);
            return p;
        }

        [SetUp]
        public void SetUp()
        {
            _table = ScriptableObject.CreateInstance<PetTable>();
            Add("hawk", PetKind.Hawk, "khan", PetSource.FirstDungeonClear);
            Add("red_hare", PetKind.RedHare, "guan", PetSource.BossFirstClear, 1);
            Add("marengo", PetKind.Marengo, "napoleon", PetSource.BossFirstClear, 3);
            Add("dove", PetKind.Dove, "tesla", PetSource.SurvivalFirstClear);
        }

        [Test]
        public void Bonus_MaxLevelWithBond_IsFivePercent()
        {
            Assert.AreEqual(0f, _table.Bonus(0, true), "없는 펫");
            Assert.AreEqual(0.003f, _table.Bonus(1, false), 1e-6f);
            Assert.AreEqual(0.03f, _table.Bonus(10, false), 1e-6f, "Lv10 = +3%");
            Assert.AreEqual(0.05f, _table.Bonus(10, true), 1e-6f, "힘 예산: 3% + 인연 2% = 5%");
            Assert.AreEqual(0.05f, _table.Bonus(99, true), 1e-6f, "최대 레벨에서 자른다");
        }

        [Test]
        public void LevelCost_IsFifteenHundredTimesLevel()
        {
            Assert.AreEqual(1500, _table.LevelCost(1));
            Assert.AreEqual(13500, _table.LevelCost(9));
            Assert.AreEqual(-1, _table.LevelCost(10), "최대");
            int total = 0;
            for (int lv = 1; lv < 10; lv++) total += _table.LevelCost(lv);
            Assert.AreEqual(67500, total, "Pets 4장: 펫 하나 67,500");
        }

        [Test]
        public void CheckAll_GrantsByProgressOnceAndEquipsSelectedHero()
        {
            var save = new SaveData { selectedHero = "khan", unlockedChapter = 1 };
            Assert.AreEqual(0, PetService.CheckAll(save, _table).Count);
            Assert.IsFalse(PetService.Unlocked(save), "첫 펫 전에는 펫 칸이 없다");

            save.tutorialDone = true; // v1.0: 첫 펫(사냥매)은 튜토리얼 보스 격파 (Pets 7-1)
            Assert.AreEqual(1, PetService.CheckAll(save, _table).Count);
            Assert.IsTrue(PetService.Unlocked(save));
            Assert.AreEqual("hawk", PetService.Equipped(save, "khan"), "처음 받은 펫은 고른 영웅에게 바로");
            Assert.AreEqual(1, PetService.Level(save, "hawk"));

            save.unlockedChapter = 2; // 1구간 보스 격파
            PetService.CheckAll(save, _table);
            Assert.IsTrue(PetService.Owns(save, "red_hare"));
            Assert.AreEqual("hawk", PetService.Equipped(save, "khan"), "이미 낀 펫은 바꾸지 않는다");
            Assert.IsFalse(PetService.Owns(save, "marengo"), "3구간 보스는 아직");

            save.survival.clears = 1;
            PetService.CheckAll(save, _table);
            Assert.IsTrue(PetService.Owns(save, "dove"));
            Assert.AreEqual(0, PetService.CheckAll(save, _table).Count, "한 번만");
        }

        [Test]
        public void LevelUp_SpendsGoldAndStopsAtMax()
        {
            var save = new SaveData { gold = 1000 };
            save.pets.owned.Add(new PetLevel { id = "hawk", level = 1 });
            Assert.IsFalse(PetService.TryLevelUp(save, _table, "hawk"), "골드 모자람");
            save.gold = 100000;
            Assert.IsTrue(PetService.TryLevelUp(save, _table, "hawk"));
            Assert.AreEqual(2, PetService.Level(save, "hawk"));
            Assert.AreEqual(100000 - 1500, save.gold);
            Assert.IsFalse(PetService.TryLevelUp(save, _table, "dove"), "없는 펫");

            save.pets.owned[0].level = 10;
            Assert.IsFalse(PetService.TryLevelUp(save, _table, "hawk"), "최대 레벨");
        }

        [Test]
        public void Equip_OnlyOwnedPetsAndSharedBetweenHeroes()
        {
            var save = new SaveData();
            Assert.IsFalse(PetService.Equip(save, _table, "khan", "hawk"), "안 가진 펫");
            save.pets.owned.Add(new PetLevel { id = "hawk", level = 3 });
            Assert.IsTrue(PetService.Equip(save, _table, "khan", "hawk"));
            Assert.IsTrue(PetService.Equip(save, _table, "guan", "hawk"), "같은 펫을 여러 영웅이 같이 써도 된다");
            Assert.AreEqual("hawk", PetService.Equipped(save, "guan"));
            Assert.IsTrue(PetService.Equip(save, _table, "khan", null));
            Assert.IsNull(PetService.Equipped(save, "khan"), "빼기");
            Assert.IsFalse(PetService.Equip(save, _table, "khan", "nope"), "없는 펫 ID");
        }

        [Test]
        public void Snapshot_CarriesPetAndMetaStatsAddsAttackAndHpOnly()
        {
            var meta = ScriptableObject.CreateInstance<MetaTable>();
            meta.pets = _table;
            var save = new SaveData();
            save.pets.owned.Add(new PetLevel { id = "hawk", level = 10 });
            PetService.Equip(save, _table, "khan", "hawk");
            PetService.Equip(save, _table, "guan", "hawk");

            MetaSnapshot khan = MetaStats.FromAccount(save, meta, "khan");
            Assert.AreEqual("hawk", khan.petId);
            Assert.AreEqual(10, khan.petLevel);
            Assert.IsTrue(khan.petBond, "사냥매 ↔ 칭기즈 칸");
            Assert.IsFalse(MetaStats.FromAccount(save, meta, "guan").petBond);

            List<StatModifier> mods = MetaStats.Build(khan, meta, this);
            Assert.AreEqual(0.05f, MetaStats.Sum(mods, StatType.Attack, ModifierKind.Percent), 1e-6f);
            Assert.AreEqual(0.05f, MetaStats.Sum(mods, StatType.MaxHp, ModifierKind.Percent), 1e-6f);
            foreach (StatModifier m in mods)
            {
                Assert.IsTrue(m.Stat == StatType.Attack || m.Stat == StatType.MaxHp, "펫은 보조 능력치를 주지 않는다 (Pets 2장)");
                Assert.AreEqual(ModifierSource.Pet, m.Source);
            }

            // 기록 당시 값: 계정에서 펫을 키워도 스냅샷은 그대로
            save.pets.owned[0].level = 1;
            Assert.AreEqual(10, khan.petLevel);
        }

        [Test]
        public void HawkGauge_NeedsTwoSecondsStillAndCooldown()
        {
            float gauge = 0f;
            Assert.IsFalse(HawkBehaviour.UpdateDiveGauge(ref gauge, true, 1.5f, true));
            Assert.IsFalse(HawkBehaviour.UpdateDiveGauge(ref gauge, false, 0.1f, true), "움직이면 처음부터");
            Assert.AreEqual(0f, gauge);
            Assert.IsFalse(HawkBehaviour.UpdateDiveGauge(ref gauge, true, 1.9f, true));
            Assert.IsTrue(HawkBehaviour.UpdateDiveGauge(ref gauge, true, 0.2f, true), "2초");
            Assert.AreEqual(0f, gauge, "급강하하면 다시 2초");
            Assert.IsFalse(HawkBehaviour.UpdateDiveGauge(ref gauge, true, 3f, false), "재사용 중이면 기다린다");
            Assert.IsTrue(HawkBehaviour.UpdateDiveGauge(ref gauge, true, 0.01f, true), "재사용이 끝나면 바로");
        }

        [Test]
        public void Marengo_CooldownShortWhenLowHp()
        {
            Assert.AreEqual(7f, MarengoBehaviour.CooldownFor(0.8f));
            Assert.AreEqual(7f, MarengoBehaviour.CooldownFor(0.3f), "30% 아래부터");
            Assert.AreEqual(4f, MarengoBehaviour.CooldownFor(0.29f));
        }

        [Test]
        public void Dove_TreasureFruitChanceAndDoubleGold()
        {
            Assert.IsTrue(DoveBehaviour.RollTreasure(1, false, false, 0.05f, 0.5f).fruit, "10%는 회복 열매");
            Assert.AreEqual(15, DoveBehaviour.RollTreasure(1, false, false, 0.5f, 0.0f).gold, "1구간 10 + 5, Lv5 전이면 2배 없음");
            Assert.AreEqual(30, DoveBehaviour.RollTreasure(1, true, false, 0.5f, 0.01f).gold, "Lv5: 5% 확률 2배");
            Assert.IsTrue(DoveBehaviour.RollTreasure(3, true, true, 0.2f, 0.5f).fruit, "Lv10: 열매 25%");
            Assert.AreEqual(25, DoveBehaviour.RollTreasure(3, true, true, 0.3f, 0.5f).gold);
        }

        [Test]
        public void Load_OldSaveWithoutPets_GetsEmptyPets()
        {
            string folder = Path.Combine(Path.GetTempPath(), "hh_pets_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                var store = new SaveStore(folder);
                var old = new SaveData { gold = 7 };
                old.records.Add(new RunRecord { id = "r1", heroId = "khan" });
                string json = JsonUtility.ToJson(old, true).Replace("\"pets\"", "\"pets_old\"");
                File.WriteAllText(store.MainPath, json);

                SaveData loaded = store.Load();
                Assert.IsNotNull(loaded.pets);
                Assert.IsNotNull(loaded.pets.owned);
                Assert.IsNotNull(loaded.pets.loadouts);
                Assert.IsTrue(string.IsNullOrEmpty(loaded.records[0].meta.petId), "옛 기록 = 펫 없음");
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }
    }
}
