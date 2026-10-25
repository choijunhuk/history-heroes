using System;
using System.Collections.Generic;
using System.IO;
using HistoryHeroes.Records;
using HistoryHeroes.Skills;
using NUnit.Framework;
using UnityEngine;

namespace HistoryHeroes.Tests
{
    /// <summary>기록 비용·용량·저장 화면 규칙·검사·저장 파일 (GameDesign 7장, 12-3 저장 테스트 체크리스트)</summary>
    public sealed class RecordTests
    {
        private PassiveSkillData _atk, _def, _legendPassive;
        private ActiveSkillData _blade, _lightning, _flame, _whirl, _thunder;
        private FusionRecipe _evolution, _merge;
        private SkillDatabase _db;
        private string _folder;

        [SetUp]
        public void SetUp()
        {
            _atk = Make<PassiveSkillData>("atk_up", Rarity.Common, 5, 1, 1);
            _def = Make<PassiveSkillData>("def_up", Rarity.Common, 5, 1, 1);
            _legendPassive = Make<PassiveSkillData>("legend_p", Rarity.Legendary, 5, 6, 6);
            _blade = Make<ActiveSkillData>("spin_blade", Rarity.Common, 5, 2, 2);
            _lightning = Make<ActiveSkillData>("lightning", Rarity.Rare, 5, 3, 3);
            _flame = Make<ActiveSkillData>("flame_field", Rarity.Common, 5, 2, 2);
            _whirl = Make<ActiveSkillData>("steel_whirl", Rarity.Rare, 1, 10, 0);
            _thunder = Make<ActiveSkillData>("thunder_fire", Rarity.Rare, 3, 15, 3);

            _evolution = ScriptableObject.CreateInstance<FusionRecipe>();
            _evolution.id = "steel_whirl";
            _evolution.type = FusionType.Evolution;
            _evolution.materialA = _blade;
            _evolution.materialB = _def;
            _evolution.result = _whirl;

            _merge = ScriptableObject.CreateInstance<FusionRecipe>();
            _merge.id = "thunder_fire";
            _merge.type = FusionType.Merge;
            _merge.materialA = _lightning;
            _merge.materialB = _flame;
            _merge.result = _thunder;

            _db = ScriptableObject.CreateInstance<SkillDatabase>();
            _db.skills = new List<SkillData> { _atk, _def, _legendPassive, _blade, _lightning, _flame, _whirl, _thunder };
            _db.recipes = new List<FusionRecipe> { _evolution, _merge };

            _folder = Path.Combine(Path.GetTempPath(), "hh-save-test-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
        }

        private static T Make<T>(string id, Rarity rarity, int maxLevel, int first, int perLevel) where T : SkillData
        {
            var s = ScriptableObject.CreateInstance<T>();
            s.id = id;
            s.displayName = id;
            s.rarity = rarity;
            s.maxLevel = maxLevel;
            s.saveCostFirstLevel = first;
            s.saveCostPerLevel = perLevel;
            s.recordable = true;
            return s;
        }

        private RecordDraft.Entry Entry(SkillData skill, int level, FusionRecipe recipe = null)
        {
            return new RecordDraft.Entry(skill, level, recipe != null, recipe);
        }

        // ---------------- 용량 (7-2) ----------------

        [Test]
        public void Capacity_IsRatioOfReferenceBuild()
        {
            Assert.AreEqual(27, RecordCapacity.For(10, 10, cleared: true));  // 34 × 0.8
            Assert.AreEqual(27, RecordCapacity.For(3, 10, cleared: true));   // 클리어면 비율 1
            Assert.AreEqual(13, RecordCapacity.For(5, 10, cleared: false));  // 27.2 × 0.5 = 13.6
            Assert.AreEqual(0, RecordCapacity.For(0, 10, cleared: false));   // 바로 죽으면 못 남김
            Assert.AreEqual(30, RecordCapacity.For(10, 10, cleared: true, bonusRatio: 0.1f)); // 90%
        }

        // ---------------- 저장 화면 규칙 (7-2) ----------------

        [Test]
        public void Draft_CostIsPerSkillPerLevel()
        {
            var draft = new RecordDraft(new[] { Entry(_blade, 5), Entry(_atk, 3) }, 27);
            Assert.AreEqual(10 + 3, draft.TotalCost);

            draft.SetLevel(draft.Entries[0], 3); // 회전 칼날 Lv5 중 Lv3만
            Assert.AreEqual(6 + 3, draft.TotalCost);

            draft.SetLevel(draft.Entries[1], 0); // 뺌
            Assert.AreEqual(6, draft.TotalCost);
            Assert.AreEqual(1, draft.ToSkills().Count);
        }

        [Test]
        public void Draft_LevelCannotExceedOwned()
        {
            var draft = new RecordDraft(new[] { Entry(_atk, 2) }, 27);
            draft.Step(draft.Entries[0], +1);
            Assert.AreEqual(2, draft.Entries[0].Level);
            draft.SetLevel(draft.Entries[0], -3);
            Assert.AreEqual(0, draft.Entries[0].Level);
        }

        [Test]
        public void Draft_EvolutionIsAllOrNothing()
        {
            var draft = new RecordDraft(new[] { Entry(_whirl, 1, _evolution), Entry(_def, 1) }, 27);
            RecordDraft.Entry evo = draft.Entries[0];

            draft.Step(evo, -1);
            Assert.AreEqual(0, evo.Level);
            draft.Step(evo, +1);
            Assert.AreEqual(1, evo.Level);
            Assert.AreEqual(10 + 1, draft.TotalCost); // 진화 10 + 방어력 Lv1 1 (이중 계산 안 함)
        }

        [Test]
        public void Draft_DroppingEvolutionPassive_RevertsToBaseActiveMax()
        {
            var draft = new RecordDraft(new[] { Entry(_whirl, 1, _evolution), Entry(_def, 2) }, 27);
            draft.SetLevel(draft.Entries[1], 0); // 방어력을 뺌

            Assert.IsTrue(draft.Reverts(draft.Entries[0]));
            Assert.AreEqual(10, draft.TotalCost); // 회전 칼날 Lv5 = 2 + 2×4

            List<RecordSkill> skills = draft.ToSkills();
            Assert.AreEqual(1, skills.Count);
            Assert.AreEqual("spin_blade", skills[0].id);
            Assert.AreEqual(5, skills[0].level);
            Assert.IsFalse(skills[0].fusion);
        }

        [Test]
        public void Draft_MergeCanKeepLowerLevel()
        {
            var draft = new RecordDraft(new[] { Entry(_thunder, 3, _merge) }, 27);
            Assert.AreEqual(15 + 3 * 2, draft.TotalCost);
            draft.SetLevel(draft.Entries[0], 1);
            Assert.AreEqual(15, draft.TotalCost);
            Assert.IsTrue(draft.ToSkills()[0].fusion);
        }

        [Test]
        public void Draft_AutoFit_FitsAndCutsCommonPassivesFirst_KeepsEvolutionPassive()
        {
            var draft = new RecordDraft(new[]
            {
                Entry(_whirl, 1, _evolution), // 10
                Entry(_def, 3),               // 3
                Entry(_lightning, 5),         // 15
                Entry(_atk, 5),               // 5
                Entry(_legendPassive, 1),     // 6
            }, 27);
            Assert.AreEqual(39, draft.TotalCost);

            draft.AutoFit();

            Assert.IsTrue(draft.Fits, $"비용 {draft.TotalCost}");
            Assert.AreEqual(1, draft.Entries[0].Level, "진화는 남는다");
            Assert.GreaterOrEqual(draft.Entries[1].Level, 1, "진화의 필수 패시브는 Lv1 아래로 안 깎는다");
            Assert.AreEqual(1, draft.Entries[4].Level, "전설은 마지막에 깎는다");
            Assert.IsFalse(draft.Reverts(draft.Entries[0]));
        }

        // ---------------- 검사 (7-5) ----------------

        [Test]
        public void Validator_DropsMissingSkills_ClampsLevels_RemovesDuplicates()
        {
            var record = new RunRecord
            {
                capacity = 27,
                skills = new List<RecordSkill>
                {
                    new RecordSkill("atk_up", 9, false),       // 최대 5로
                    new RecordSkill("deleted_skill", 2, false), // 없어진 스킬
                    new RecordSkill("atk_up", 1, false),       // 중복
                    new RecordSkill("thunder_fire", 2, true),
                },
            };

            RecordValidator.Result result = RecordValidator.Validate(record, _db);

            Assert.AreEqual(2, result.Skills.Count);
            Assert.AreEqual(5, result.Skills[0].level);
            Assert.AreEqual("thunder_fire", result.Skills[1].id);
            Assert.AreEqual(3, result.Notes.Count);
            Assert.AreEqual(5 + 18, result.Cost);
            Assert.IsFalse(result.OverCapacity);
        }

        [Test]
        public void Validator_EvolutionWithDeletedPassive_RevertsToBase()
        {
            _evolution.materialB = null; // 방어력 스킬이 게임에서 지워졌다
            var record = new RunRecord { skills = new List<RecordSkill> { new RecordSkill("steel_whirl", 1, true) } };

            RecordValidator.Result result = RecordValidator.Validate(record, _db);

            Assert.AreEqual("spin_blade", result.Skills[0].id);
            Assert.AreEqual(5, result.Skills[0].level);
        }

        [Test]
        public void Validator_FlagsOverCapacityAfterPatch()
        {
            var record = new RunRecord { capacity = 10, skills = new List<RecordSkill> { new RecordSkill("lightning", 3, false) } };
            Assert.IsFalse(RecordValidator.Validate(record, _db).OverCapacity); // 3 + 3×2 = 9

            _lightning.saveCostPerLevel = 5; // 패치로 비용이 오름 → 13
            Assert.IsTrue(RecordValidator.Validate(record, _db).OverCapacity);
        }

        [Test]
        public void Validator_SameResultTwice()
        {
            var record = new RunRecord { skills = new List<RecordSkill> { new RecordSkill("atk_up", 2, false), new RecordSkill("steel_whirl", 1, true) } };
            RecordValidator.Result a = RecordValidator.Validate(record, _db);
            RecordValidator.Result b = RecordValidator.Validate(record, _db);
            Assert.AreEqual(a.Skills.Count, b.Skills.Count);
            Assert.AreEqual(a.Cost, b.Cost);
            Assert.AreEqual(2, record.skills.Count, "검사는 원본 기록을 바꾸지 않는다");
        }

        // ---------------- 저장 파일 (12-3 체크리스트) ----------------

        private SaveData SampleSave()
        {
            var draft = new RecordDraft(new[] { Entry(_whirl, 1, _evolution), Entry(_def, 2), Entry(_thunder, 2, _merge), Entry(_atk, 3) }, 40);
            RunRecord record = draft.ToRecord("khan", 1, 10, 10, true, 1);
            var data = new SaveData();
            data.records.Add(record);
            data.discoveredRecipes.Add("steel_whirl");
            return data;
        }

        [Test]
        public void Save_ThenLoad_RestoresSameBuild_IncludingFusions()
        {
            var store = new SaveStore(_folder);
            SaveData saved = SampleSave();
            store.Save(saved);

            SaveData loaded = new SaveStore(_folder).Load();

            Assert.AreEqual(1, loaded.records.Count);
            List<RecordSkill> a = saved.records[0].skills, b = loaded.records[0].skills;
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].id, b[i].id);
                Assert.AreEqual(a[i].level, b[i].level);
                Assert.AreEqual(a[i].fusion, b[i].fusion);
            }

            Assert.AreEqual("steel_whirl", loaded.discoveredRecipes[0]);
            Assert.AreEqual(SaveData.CurrentSchemaVersion, loaded.schemaVersion);
        }

        [Test]
        public void Save_KeepsOneBackup()
        {
            var store = new SaveStore(_folder);
            SaveData first = SampleSave();
            store.Save(first);
            first.unlockedChapter = 2;
            store.Save(first);

            Assert.IsTrue(File.Exists(store.MainPath));
            Assert.IsTrue(File.Exists(store.BackupPath));
            Assert.IsFalse(File.Exists(store.TempPath));
            StringAssert.Contains("\"unlockedChapter\": 1", File.ReadAllText(store.BackupPath));
        }

        [Test]
        public void Load_BrokenMain_UsesBackup_AndPreservesBrokenFile()
        {
            var store = new SaveStore(_folder);
            store.Save(SampleSave());
            store.Save(SampleSave()); // 백업 생김
            File.WriteAllText(store.MainPath, "{ 이건 망가진 JSON");

            SaveData loaded = store.Load();

            Assert.AreEqual(1, loaded.records.Count);
            Assert.IsTrue(store.LastLoad.FromBackup);
            Assert.IsTrue(File.Exists(store.MainPath), "망가진 원본도 지우지 않는다");
            Assert.AreEqual(1, store.LastLoad.PreservedCopies.Count);
            Assert.IsTrue(File.Exists(store.LastLoad.PreservedCopies[0]));
        }

        [Test]
        public void Load_NewerSchema_IsNotRead_AndOriginalPreserved()
        {
            var store = new SaveStore(_folder);
            Directory.CreateDirectory(_folder);
            string future = "{\"schemaVersion\": 99, \"records\": []}";
            File.WriteAllText(store.MainPath, future);

            SaveData loaded = store.Load();

            Assert.IsTrue(store.LastLoad.IsFresh);
            Assert.AreEqual(0, loaded.records.Count);
            Assert.AreEqual(future, File.ReadAllText(store.MainPath), "원본은 그대로");
        }

        [Test]
        public void Load_CrashDuringTempWrite_KeepsPreviousSave()
        {
            var store = new SaveStore(_folder);
            store.Save(SampleSave());
            File.WriteAllText(store.TempPath, "{\"schemaVersion\": 1, \"records\": [ 반쯤 쓰다가 꺼짐"); // 임시 파일 쓰는 도중에 꺼짐

            SaveData loaded = store.Load();

            Assert.AreEqual(1, loaded.records.Count);
            Assert.AreEqual(store.MainPath, store.LastLoad.Source);
        }

        [Test]
        public void Load_CrashBetweenRenames_ReadsCompletedTemp()
        {
            var store = new SaveStore(_folder);
            store.Save(SampleSave());
            SaveData newer = SampleSave();
            newer.unlockedChapter = 2;

            // 교체 도중에 꺼진 상태 흉내: 본 파일은 백업으로 옮겨졌고, 임시 파일은 완성본
            File.Move(store.MainPath, store.BackupPath);
            File.WriteAllText(store.TempPath, JsonUtility.ToJson(newer, true));

            SaveData loaded = store.Load();

            Assert.AreEqual(2, loaded.unlockedChapter);
        }

        [Test]
        public void Load_NothingSaved_StartsFresh()
        {
            SaveData loaded = new SaveStore(_folder).Load();
            Assert.AreEqual(3, loaded.recordSlots);
            Assert.AreEqual(1, loaded.unlockedChapter);
            Assert.AreEqual(0, loaded.FirstFreeSlot());
        }
    }
}
