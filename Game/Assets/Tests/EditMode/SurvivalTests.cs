using System.Collections.Generic;
using System.IO;
using HistoryHeroes.Common;
using HistoryHeroes.Records;
using HistoryHeroes.Relics;
using HistoryHeroes.Runes;
using HistoryHeroes.Skills;
using HistoryHeroes.Survival;
using NUnit.Framework;
using UnityEngine;

namespace HistoryHeroes.Tests
{
    /// <summary>v0.5 서바이벌 (GameDesign 8장, Modes 2-10, Relics 5장, Runes 6장)</summary>
    public sealed class SurvivalTests
    {
        [Test]
        public void TargetAlive_FollowsCurveAndCap()
        {
            var data = ScriptableObject.CreateInstance<SurvivalData>();
            Assert.AreEqual(8, data.TargetAlive(0f));
            Assert.AreEqual(15, data.TargetAlive(30f), "0분 8 ~ 1분 22 사이 절반");
            Assert.AreEqual(250, data.TargetAlive(600f));
            Assert.AreEqual(250, data.TargetAlive(900f), "마지막 값 유지");
            data.maxAlive = 100;
            Assert.AreEqual(100, data.TargetAlive(600f), "동시 상한을 넘지 않는다");
        }

        [Test]
        public void EnemyScaling_StartsWeakAndGrowsPerMinute()
        {
            var data = ScriptableObject.CreateInstance<SurvivalData>();
            Assert.AreEqual(0.6f, data.HpMultiplier(0f), 1e-5f, "처음엔 한두 대에 죽는다");
            Assert.AreEqual(0.6f + 0.25f * 10f, data.HpMultiplier(600f), 1e-4f);
            Assert.AreEqual(0.8f + 0.1f * 10f, data.AttackMultiplier(600f), 1e-4f, "처음엔 약하게, 10분에는 그대로 ×1.8");
        }

        [Test]
        public void SurvivalSave_SecondsOfDoesNotAddAndBestOfDoes()
        {
            var save = new SaveData();
            Assert.AreEqual(0f, save.survival.SecondsOf("khan"));
            Assert.AreEqual(0, save.survival.best.Count, "화면 표시용 읽기는 목록을 바꾸지 않는다");
            save.survival.BestOf("khan").seconds = 321f;
            Assert.AreEqual(1, save.survival.best.Count);
            Assert.AreEqual(321f, save.survival.SecondsOf("khan"));
            Assert.AreSame(save.survival.BestOf("khan"), save.survival.BestOf("khan"));
        }

        [Test]
        public void Load_OldSaveWithoutSurvival_GetsEmptySurvival()
        {
            string folder = Path.Combine(Path.GetTempPath(), "hh_surv_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                var store = new SaveStore(folder);
                var old = new SaveData { gold = 55 };
                string json = JsonUtility.ToJson(old, true).Replace("\"survival\"", "\"survival_old\"");
                File.WriteAllText(store.MainPath, json);

                SaveData loaded = store.Load();
                Assert.AreEqual(55, loaded.gold);
                Assert.IsNotNull(loaded.survival);
                Assert.IsNotNull(loaded.survival.best);
                Assert.IsNotNull(loaded.survival.clearHeroes);
                Assert.AreEqual(0, loaded.survival.clears);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [Test]
        public void Unlock_AfterChapterTwoBoss()
        {
            Assert.IsFalse(GameSession.SurvivalUnlockedByProgress(new SaveData { unlockedChapter = 2 }));
            Assert.IsTrue(GameSession.SurvivalUnlockedByProgress(new SaveData { unlockedChapter = 3 }), "2구간 보스 격파 = 3구간 열림");
            Assert.AreEqual(2, GameSession.SurvivalHearts, "Modes 8장: 하트 2개");
        }

        [Test]
        public void Relics_SurvivalMilestones()
        {
            var table = ScriptableObject.CreateInstance<RelicTable>();
            RelicData Add(string id, string value)
            {
                var r = ScriptableObject.CreateInstance<RelicData>();
                r.id = id;
                r.displayName = id;
                r.source = RelicSource.Survival;
                r.sourceValue = value;
                table.relics.Add(r);
                return r;
            }

            Add("sundial", "survive5");
            Add("astrolabe", "clear10");
            Add("galileo", "two_heroes");

            var save = new SaveData();
            save.survival.longestSeconds = 299f;
            Assert.AreEqual(0, RelicService.CheckAll(save, table).Count, "5분이 안 됨");

            save.survival.longestSeconds = 300f;
            RelicService.CheckAll(save, table);
            Assert.Greater(RelicService.Stars(save, "sundial"), 0);
            Assert.AreEqual(0, RelicService.Stars(save, "astrolabe"));

            save.survival.clears = 1;
            save.survival.clearHeroes.Add("khan");
            RelicService.CheckAll(save, table);
            Assert.Greater(RelicService.Stars(save, "astrolabe"), 0);
            Assert.AreEqual(0, RelicService.Stars(save, "galileo"), "영웅 1명");

            save.survival.clearHeroes.Add("guan");
            RelicService.CheckAll(save, table);
            Assert.Greater(RelicService.Stars(save, "galileo"), 0);
        }

        [Test]
        public void Runes_BondRuneOnFirstSurvivalClear()
        {
            var table = ScriptableObject.CreateInstance<RuneTable>();
            var bond = ScriptableObject.CreateInstance<RuneData>();
            bond.id = "rune_bond";
            bond.displayName = "인연의 룬";
            bond.source = RuneSource.Survival;
            bond.bond = true;
            table.runes.Add(bond);

            var save = new SaveData();
            Assert.AreEqual(0, RuneService.GrantSurvivalRunes(save, table).Count, "아직 클리어 못 함");
            Assert.IsFalse(RuneService.Owns(save, "rune_bond"));

            save.survival.clears = 1;
            Assert.AreEqual(1, RuneService.GrantSurvivalRunes(save, table).Count);
            Assert.AreEqual(1, RuneService.Level(save, "rune_bond"));
            Assert.AreEqual(0, RuneService.GrantSurvivalRunes(save, table).Count, "한 번만");
        }

        [Test]
        public void SkillSlots_ConfigurableForSurvival()
        {
            var go = new GameObject("skills");
            try
            {
                var skills = go.AddComponent<SkillManager>();
                skills.ConfigureSlots(6, 6);
                Assert.AreEqual(6, skills.Loadout.ActiveSlots);
                Assert.AreEqual(6, skills.Loadout.PassiveSlots);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
