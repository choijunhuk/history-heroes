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

        [Test]
        public void Draft_AutoFit_WithDamageShare_CutsLowDamageActiveFirst()
        {
            // 같은 일반 액티브 Lv1 둘 (각 2pt), 용량 2 → 하나만 남는다
            var draft = new RecordDraft(new[] { Entry(_blade, 1), Entry(_flame, 1) }, 2);
            draft.AutoFit(id => id == "flame_field" ? 0.35f : 0.03f);
            Assert.IsTrue(draft.Fits);
            Assert.IsFalse(draft.Entries[0].Kept, "피해 3%인 회전 칼날을 먼저 뺀다");
            Assert.IsTrue(draft.Entries[1].Kept, "피해 35%인 불꽃 장판은 남긴다");

            var noShare = new RecordDraft(new[] { Entry(_blade, 1), Entry(_flame, 1) }, 2);
            noShare.AutoFit();
            Assert.IsTrue(noShare.Fits, "피해 정보가 없어도 맞춘다");
        }

        [Test]
        public void Draft_AutoFit_KeepsAttackPassiveOverWeakActive()
        {
            // 전체 검토 2번: 공격력(기본 공격 패시브)을 피해가 적은 액티브보다 늦게 깎는다
            var draft = new RecordDraft(new[] { Entry(_atk, 1), Entry(_blade, 1) }, 2); // 1 + 2 = 3 > 2
            draft.AutoFit(id => id == "spin_blade" ? 0.03f : 0f);
            Assert.IsTrue(draft.Fits);
            Assert.IsTrue(draft.Entries[0].Kept, "공격력은 남는다");
            Assert.IsFalse(draft.Entries[1].Kept, "피해 3%인 회전 칼날을 뺀다");
        }

        [Test]
        public void Draft_PinnedSkillIsCutLast_AndAtMostTwo()
        {
            var draft = new RecordDraft(new[] { Entry(_def, 3), Entry(_lightning, 1), Entry(_atk, 1) }, 4); // 3 + 3 + 1 = 7 > 4
            Assert.IsTrue(draft.TogglePin(draft.Entries[0]), "방어력 고정");
            draft.AutoFit();
            Assert.IsTrue(draft.Fits, $"비용 {draft.TotalCost}");
            Assert.AreEqual(3, draft.Entries[0].Level, "고정한 방어력은 그대로");
            Assert.IsFalse(draft.Entries[1].Kept, "대신 낙뢰를 뺀다");

            Assert.IsTrue(draft.TogglePin(draft.Entries[2]));
            Assert.IsFalse(draft.TogglePin(draft.Entries[1]), "세 번째 고정은 안 된다");
            Assert.AreEqual(2, draft.PinCount);
            Assert.IsTrue(draft.TogglePin(draft.Entries[2]), "다시 누르면 풀린다");
            Assert.AreEqual(1, draft.PinCount);
        }

        [Test]
        public void Strength_SumsSaveCostByRole_AndPicksMain()
        {
            _blade.role = SkillRole.Defense;   // 2 + 2×(Lv-1)
            _lightning.role = SkillRole.Area;  // 3 + 3×(Lv-1)
            _atk.role = SkillRole.None;
            _def.role = SkillRole.Defense;     // 1 + 1×(Lv-1)

            var skills = new List<RecordSkill>
            {
                new RecordSkill("spin_blade", 2, false), // 방어 4
                new RecordSkill("lightning", 3, false),  // 광역 9
                new RecordSkill("atk_up", 5, false),     // 없음 (세지 않음)
                new RecordSkill("def_up", 3, false),     // 방어 3
                new RecordSkill("gone", 3, false),       // 없어진 스킬은 무시
            };

            RecordStrength strength = RecordStrength.Of(skills, _db);
            Assert.AreEqual(0, strength.Single);
            Assert.AreEqual(9, strength.Area);
            Assert.AreEqual(7, strength.Defense);
            Assert.AreEqual(SkillRole.Area, strength.Main);
            Assert.AreEqual("광역형", RecordStrength.TypeName(strength.Main));
            StringAssert.Contains("광역 56%", strength.Bar());
            StringAssert.Contains("방어 44%", strength.Bar());
            StringAssert.DoesNotContain("단일", strength.Bar(), "0인 역할은 글자로 안 쓴다");

            Assert.AreEqual(SkillRole.None, RecordStrength.Of(new List<RecordSkill>(), _db).Main);
            StringAssert.Contains("강점 없음", default(RecordStrength).Bar());
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

        private static RunRecord NewRecord(string id, int slot = -1) =>
            new RunRecord { id = id, slot = slot, heroId = "khan", capacity = 27, createdAt = "2026-10-29T00:00:00Z" };

        [Test]
        public void Details_CardShowsTopFourSkillsByLevelPlusCount()
        {
            // 보관함 카드 (전체 UI 검토 9번): 레벨 높은 순 4개 + "외 N개"
            var db = UnityEditor.AssetDatabase.LoadAssetAtPath<SkillDatabase>("Assets/Data/Skills/SkillDatabase.asset");
            RunRecord r = NewRecord("many", 0);
            string[] ids = { "spin_blade", "lightning", "ice_shard", "atk_up", "aspd_up", "hp_up" };
            int[] levels = { 1, 5, 2, 4, 3, 1 };
            for (int i = 0; i < ids.Length; i++) r.skills.Add(new RecordSkill { id = ids[i], level = levels[i] });

            string card = RecordFormat.Details(r, db, 4);
            StringAssert.Contains("외 2개", card);
            StringAssert.Contains(db.Find("lightning").displayName, card); // Lv5는 남는다
            StringAssert.DoesNotContain(db.Find("spin_blade").displayName, card); // Lv1은 빠진다
            StringAssert.DoesNotContain("외 ", RecordFormat.Details(r, db)); // 기본은 전부
        }

        [Test]
        public void Load_OldJsonWithoutTempOrLock_KeepsNumberedRecordUnlocked()
        {
            var store = new SaveStore(_folder);
            var old = new SaveData();
            old.records.Add(NewRecord("old", 0));
            string json = JsonUtility.ToJson(old);
            json = json.Replace("\"tempRecords\":[],", "").Replace(",\"tempRecords\":[]", "");
            json = json.Replace("\"locked\":false,", "").Replace(",\"locked\":false", "");
            StringAssert.DoesNotContain("tempRecords", json);
            StringAssert.DoesNotContain("\"locked\"", json); // "unlockedChapter"에도 locked 글자가 있어서 필드 이름 그대로 본다
            Directory.CreateDirectory(_folder);
            File.WriteAllText(store.MainPath, SaveStore.Sign(json));

            SaveData loaded = store.Load();

            Assert.IsFalse(loaded.RecordInSlot(0).locked);
            Assert.IsNull(loaded.TempRecord);
            Assert.AreEqual(3, loaded.recordSlots);
        }

        [Test]
        public void Temp_WhenArchiveFull_ReplacesOnlyPreviousTemp()
        {
            var save = new SaveData();
            for (int i = 0; i < save.recordSlots; i++) save.records.Add(NewRecord("slot" + i, i));

            Assert.IsTrue(RecordService.SaveToTemp(save, NewRecord("first")));
            Assert.IsTrue(RecordService.SaveToTemp(save, NewRecord("second")));

            Assert.AreEqual("second", save.TempRecord.id);
            Assert.AreEqual(1, save.tempRecords.Count);
            Assert.AreEqual(3, save.records.Count);
            Assert.IsNull(save.FindRecord("first"));
        }

        [Test]
        public void Temp_TitleDoesNotShowZeroNumber()
        {
            string title = RecordFormat.Title(NewRecord("temp"));
            StringAssert.StartsWith("임시 기록", title);
            StringAssert.DoesNotContain("0번", title);
        }

        [Test]
        public void RecordService_RejectsInvalidSlotsAndDuplicateIdsWithoutChanges()
        {
            var save = new SaveData();
            Assert.IsTrue(RecordService.SaveToSlot(save, NewRecord("keep"), 0));
            Assert.IsFalse(RecordService.SaveToSlot(save, NewRecord("keep"), 1));
            Assert.IsFalse(RecordService.SaveToTemp(save, NewRecord("keep")));
            Assert.IsFalse(RecordService.SaveToSlot(save, NewRecord("new"), -1));
            Assert.IsFalse(RecordService.SaveToSlot(save, NewRecord("new"), save.recordSlots));
            Assert.AreEqual(1, save.records.Count);
            Assert.AreEqual(1, save.relics.recordsSavedTotal);
            Assert.IsNull(save.TempRecord);
        }

        [Test]
        public void Temp_IsFoundByIdButNotCountedAsOwnedOrFreeSlot()
        {
            var save = new SaveData();
            save.records.Add(NewRecord("slot", 0));
            Assert.IsTrue(RecordService.SaveToTemp(save, NewRecord("temp")));

            Assert.AreEqual(1, save.records.Count);
            Assert.AreEqual(1, save.FirstFreeSlot());
            Assert.AreSame(save.TempRecord, save.FindRecord("temp"));
            var mission = new Missions.NewcomerMission { Check = Missions.NewcomerCheck.RecordsOwned, Value = 2 };
            Assert.IsFalse(Missions.Newcomer.Done(save, mission));
        }

        [Test]
        public void Temp_MovesToFirstFreeSlotWithoutCreatingAnotherRecord()
        {
            var save = new SaveData();
            save.records.Add(NewRecord("slot", 0));
            RunRecord temp = NewRecord("temp");
            Assert.IsTrue(RecordService.SaveToTemp(save, temp));
            int savedTotal = save.relics.recordsSavedTotal;

            Assert.IsTrue(RecordService.MoveTempToArchive(save));

            Assert.IsNull(save.TempRecord);
            Assert.AreSame(temp, save.RecordInSlot(1));
            Assert.AreEqual(1, temp.slot);
            Assert.AreEqual(savedTotal, save.relics.recordsSavedTotal);
        }

        [Test]
        public void Temp_WhenFullCannotMoveOrBeLocked()
        {
            var save = new SaveData();
            for (int i = 0; i < save.recordSlots; i++) save.records.Add(NewRecord("slot" + i, i));
            RunRecord temp = NewRecord("temp");
            temp.locked = true;
            Assert.IsTrue(RecordService.SaveToTemp(save, temp));

            Assert.IsFalse(temp.locked);
            Assert.IsFalse(RecordService.MoveTempToArchive(save));
            Assert.IsFalse(RecordService.ToggleLock(save, -1));
            Assert.IsFalse(RecordService.Delete(save, -1));
            Assert.AreSame(temp, save.TempRecord);
            Assert.AreEqual(3, save.records.Count);
        }

        [Test]
        public void LockedRecord_CannotBeOverwrittenOrDeleted()
        {
            var save = new SaveData();
            RunRecord locked = NewRecord("keep", 0);
            locked.locked = true;
            save.records.Add(locked);

            Assert.IsFalse(RecordService.SaveToSlot(save, NewRecord("new"), 0));
            Assert.IsFalse(RecordService.Delete(save, 0));
            Assert.AreSame(locked, save.RecordInSlot(0));
        }

        [Test]
        public void Lock_ToggleAllowsDeleteAfterUnlock()
        {
            var save = new SaveData();
            save.records.Add(NewRecord("slot", 0));

            Assert.IsTrue(RecordService.ToggleLock(save, 0));
            Assert.IsTrue(save.RecordInSlot(0).locked);
            Assert.IsTrue(RecordService.ToggleLock(save, 0));
            Assert.IsTrue(RecordService.Delete(save, 0));
            Assert.IsNull(save.RecordInSlot(0));
        }

        [Test]
        public void Reselect_LockedRecordKeepsItsIdentityAndBossHistory()
        {
            var save = new SaveData();
            RunRecord old = NewRecord("keep", 0);
            old.locked = true;
            old.bossesDefeated.Add("boss1");
            save.records.Add(old);
            RunRecord changed = NewRecord("new");
            changed.capacity = 20;
            changed.skills.Add(new RecordSkill("atk_up", 1, false));

            Assert.IsTrue(RecordService.Reselect(save, old.id, changed));

            Assert.AreEqual("keep", save.RecordInSlot(0).id);
            Assert.IsTrue(save.RecordInSlot(0).locked);
            Assert.AreEqual("boss1", save.RecordInSlot(0).bossesDefeated[0]);
            Assert.AreEqual(20, save.RecordInSlot(0).capacity);
        }

        [Test]
        public void SaveLoad_PreservesLockAndTempRecord()
        {
            var store = new SaveStore(_folder);
            var save = new SaveData();
            save.records.Add(NewRecord("slot", 0));
            Assert.IsTrue(RecordService.ToggleLock(save, 0));
            Assert.IsTrue(RecordService.SaveToTemp(save, NewRecord("temp")));
            store.Save(save);

            SaveData loaded = new SaveStore(_folder).Load();

            Assert.IsTrue(loaded.RecordInSlot(0).locked);
            Assert.AreEqual("temp", loaded.TempRecord.id);
            Assert.AreSame(loaded.TempRecord, loaded.FindRecord("temp"));
        }

        [Test]
        public void Commit_WhenTempReplacementCannotBeSaved_RestoresPreviousRecord()
        {
            string blocker = Path.Combine(Path.GetTempPath(), "hh-record-block-" + Guid.NewGuid().ToString("N"));
            File.WriteAllText(blocker, "x");
            SaveStore previousStore = SaveSystem.Store;
            try
            {
                SaveSystem.Use(new SaveStore(Path.Combine(blocker, "sub")));
                SaveData data = SaveSystem.Data;
                data.tempRecords.Add(NewRecord("old"));
                int savedTotal = data.relics.recordsSavedTotal;

                UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("저장 실패"));
                SaveSystem.CommitResult result = SaveSystem.Commit(() => RecordService.SaveToTemp(data, NewRecord("new")));

                Assert.AreEqual(SaveSystem.CommitResult.SaveFailed, result);
                Assert.AreEqual("old", SaveSystem.Data.TempRecord.id);
                Assert.IsNull(SaveSystem.Data.FindRecord("new"));
                Assert.AreEqual(savedTotal, SaveSystem.Data.relics.recordsSavedTotal);
            }
            finally
            {
                SaveSystem.Use(previousStore);
                File.Delete(blocker);
            }
        }

        [Test]
        public void Load_CollidingTempId_GivesTempNewIdAndKeepsBothRecords()
        {
            var store = new SaveStore(_folder);
            var save = new SaveData();
            save.records.Add(NewRecord("duplicate", 0));
            save.tempRecords.Add(NewRecord("duplicate"));
            store.Save(save);

            SaveData loaded = new SaveStore(_folder).Load();

            Assert.AreEqual("duplicate", loaded.RecordInSlot(0).id);
            Assert.IsNotNull(loaded.TempRecord);
            Assert.AreNotEqual("duplicate", loaded.TempRecord.id);
            Assert.AreSame(loaded.TempRecord, loaded.FindRecord(loaded.TempRecord.id));
        }

        [Test]
        public void Load_CollidingNumberedSlots_MovesSecondToFreeSlot()
        {
            var store = new SaveStore(_folder);
            var save = new SaveData();
            save.records.Add(NewRecord("first", 0));
            save.records.Add(NewRecord("second", 0));
            store.Save(save);

            SaveData loaded = new SaveStore(_folder).Load();

            Assert.AreEqual("first", loaded.RecordInSlot(0).id);
            Assert.AreEqual("second", loaded.RecordInSlot(1).id);
            Assert.AreEqual(2, loaded.records.Count);
        }

        [Test]
        public void Load_InvalidSlotCannotDisplaceValidRecords()
        {
            var store = new SaveStore(_folder);
            var save = new SaveData();
            save.records.Add(NewRecord("invalid", 99));
            for (int i = 0; i < save.recordSlots; i++) save.records.Add(NewRecord("keep" + i, i));
            store.Save(save);

            SaveData loaded = new SaveStore(_folder).Load();

            Assert.AreEqual(3, loaded.records.Count);
            for (int i = 0; i < save.recordSlots; i++) Assert.AreEqual("keep" + i, loaded.RecordInSlot(i).id);
            // 칸이 다 차도 기록은 버리지 않는다: 임시 칸으로 (Claude 검토)
            Assert.IsNotNull(loaded.TempRecord);
            Assert.AreEqual("invalid", loaded.TempRecord.id);
        }

        [Test]
        public void Load_RepairsTempMetadataAndMissingNestedLists()
        {
            var store = new SaveStore(_folder);
            var save = new SaveData();
            save.tempRecords.Add(new RunRecord { id = "temp", slot = 0, locked = true, skills = null, meta = null, bossesDefeated = null });
            store.Save(save);

            RunRecord loaded = new SaveStore(_folder).Load().TempRecord;

            Assert.AreEqual(-1, loaded.slot);
            Assert.IsFalse(loaded.locked);
            Assert.IsNotNull(loaded.skills);
            Assert.IsNotNull(loaded.meta);
            Assert.IsNotNull(loaded.bossesDefeated);
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

        // ---------------------------------------------------------------- v1.0 해시 검사 · 판 백업 3개

        [Test]
        public void Save_WritesHash_AndLoadsCleanly()
        {
            var store = new SaveStore(_folder);
            store.Save(SampleSave());
            string text = File.ReadAllText(store.MainPath);
            Assert.IsTrue(SaveStore.IntegrityOk(text));
            StringAssert.DoesNotContain("\"integrity\": \"\"", text, "해시가 채워져 있다");
            store.Load();
            Assert.IsFalse(store.LastLoad.FromBackup);
            Assert.IsFalse(store.LastLoad.Tampered);
        }

        [Test]
        public void Load_EditedMain_UsesBackup()
        {
            var store = new SaveStore(_folder);
            SaveData data = SampleSave();
            data.gold = 100;
            store.Save(data);
            data.gold = 200;
            store.Save(data); // 백업 = 100
            File.WriteAllText(store.MainPath, File.ReadAllText(store.MainPath).Replace("\"gold\": 200", "\"gold\": 999999"));

            SaveData loaded = store.Load();

            Assert.AreEqual(100, loaded.gold, "손으로 고친 본 파일 대신 백업");
            Assert.IsTrue(store.LastLoad.FromBackup);
            Assert.IsFalse(store.LastLoad.Tampered);
            Assert.IsTrue(store.LastLoad.Problems.Exists(p => p.Contains("해시")));
        }

        [Test]
        public void Load_AllCopiesEdited_KeepsProgressButFlags()
        {
            var store = new SaveStore(_folder);
            SaveData data = SampleSave();
            data.gold = 300;
            store.Save(data);
            File.WriteAllText(store.MainPath, File.ReadAllText(store.MainPath).Replace("\"gold\": 300", "\"gold\": 777"));

            SaveData loaded = store.Load();

            Assert.AreEqual(777, loaded.gold, "정상본이 없으면 진행을 날리지 않는다");
            Assert.IsTrue(store.LastLoad.Tampered);
            Assert.AreEqual(1, loaded.integrityFailures);
            Assert.IsFalse(store.LastLoad.IsFresh);
        }

        [Test]
        public void Load_BlankedHashOnNewSchema_UsesBackup()
        {
            var store = new SaveStore(_folder);
            SaveData data = SampleSave();
            data.gold = 100;
            store.Save(data);
            data.gold = 200;
            store.Save(data); // 백업 = 100
            string text = File.ReadAllText(store.MainPath).Replace("\"gold\": 200", "\"gold\": 999999");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"(""integrity""\s*:\s*"")([^""]*)("")", "$1$3"); // 해시 칸만 비운다
            File.WriteAllText(store.MainPath, text);

            SaveData loaded = store.Load();

            Assert.IsFalse(SaveStore.IntegrityOk(text), "구조 5부터는 해시가 꼭 있어야 한다");
            Assert.AreEqual(100, loaded.gold, "해시를 지운 본 파일 대신 백업");
            Assert.IsTrue(store.LastLoad.FromBackup);
        }

        [Test]
        public void IntegrityOk_OldSchemaWithoutHash_Passes()
        {
            Assert.IsTrue(SaveStore.IntegrityOk("{ \"schemaVersion\": 4, \"gold\": 5 }"), "옛 파일(구조 1~4)은 해시가 없다");
            Assert.IsTrue(SaveStore.IntegrityOk("{ \"gold\": 5 }"), "구조 칸도 없는 아주 옛 파일");
            Assert.IsFalse(SaveStore.IntegrityOk("{ \"schemaVersion\": 5, \"integrity\": \"\", \"gold\": 5 }"));
            Assert.IsFalse(SaveStore.IntegrityOk("{ \"schemaVersion\": 6, \"gold\": 5 }"), "해시 칸을 통째로 지워도 안 된다");
        }

        [Test]
        public void RotateRunBackups_KeepsLastThree()
        {
            var store = new SaveStore(_folder);
            SaveData data = SampleSave();
            for (int i = 1; i <= 4; i++)
            {
                data.gold = i;
                store.Save(data);
                store.RotateRunBackups();
            }

            StringAssert.Contains("\"gold\": 4", File.ReadAllText(store.RunBackupPath(1)));
            StringAssert.Contains("\"gold\": 3", File.ReadAllText(store.RunBackupPath(2)));
            StringAssert.Contains("\"gold\": 2", File.ReadAllText(store.RunBackupPath(3)));
            Assert.IsFalse(File.Exists(store.MainPath + ".run4"));
        }

        [Test]
        public void Load_MainAndBackupBroken_UsesRunBackup()
        {
            var store = new SaveStore(_folder);
            SaveData data = SampleSave();
            data.gold = 50;
            store.Save(data);
            store.RotateRunBackups(); // 판 백업 1 = 50
            data.gold = 60;
            store.Save(data);
            File.WriteAllText(store.MainPath, "{ 망가짐");
            File.WriteAllText(store.BackupPath, "{ 망가짐");

            SaveData loaded = store.Load();

            Assert.AreEqual(50, loaded.gold);
            Assert.IsTrue(store.LastLoad.FromBackup);
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

            Assert.IsTrue(store.LastLoad.UnsupportedNewerSchema);
            Assert.IsFalse(store.LastLoad.IsFresh);
            Assert.AreEqual(0, loaded.records.Count);
            Assert.AreEqual(future, File.ReadAllText(store.MainPath), "원본은 그대로");
        }

        [Test]
        public void Load_OldSaveWithoutSettings_GetsDefaults()
        {
            var store = new SaveStore(_folder);
            store.Save(SampleSave());
            // 설정이 생기기 전 파일 흉내: settings 칸을 지운다
            string json = File.ReadAllText(store.MainPath);
            int at = json.IndexOf("\"settings\"", StringComparison.Ordinal);
            Assert.Greater(at, 0, "저장 파일에 설정이 들어간다");
            SaveData old = JsonUtility.FromJson<SaveData>(json);
            old.settings = null;
            File.WriteAllText(store.MainPath, JsonUtility.ToJson(old, true).Replace("\"settings\"", "\"settings_removed\""));

            SaveData loaded = store.Load();

            Assert.IsNotNull(loaded.settings);
            Assert.AreEqual(2, loaded.settings.shake, "흔들림 기본은 보통");
            Assert.IsTrue(loaded.settings.damageNumbers);
            Assert.IsFalse(loaded.settings.reduceFlashes);
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
            File.WriteAllText(store.TempPath, SaveStore.Sign(JsonUtility.ToJson(newer, true)));

            SaveData loaded = store.Load();

            Assert.AreEqual(2, loaded.unlockedChapter);
        }

        [Test]
        public void Save_AfterRecoveringFromBackup_KeepsGoodBackup()
        {
            var store = new SaveStore(_folder);
            SaveData good = SampleSave();
            good.unlockedChapter = 3;
            store.Save(good);
            store.Save(good); // 백업 = 구간 3
            File.WriteAllText(store.MainPath, "{ 망가짐");

            SaveData loaded = store.Load(); // 백업에서 읽음
            loaded.unlockedChapter = 4;
            store.Save(loaded);

            StringAssert.Contains("\"unlockedChapter\": 3", File.ReadAllText(store.BackupPath), "정상 백업이 망가진 파일로 바뀌면 안 된다");
            StringAssert.Contains("\"unlockedChapter\": 4", File.ReadAllText(store.MainPath));
        }

        [Test]
        public void Load_OnlyCompletedTemp_PromotesItToMain()
        {
            var store = new SaveStore(_folder);
            Directory.CreateDirectory(_folder);
            SaveData only = SampleSave();
            only.unlockedChapter = 2;
            File.WriteAllText(store.TempPath, SaveStore.Sign(JsonUtility.ToJson(only, true))); // 본 파일·백업 없이 임시 파일만

            store.Load();

            Assert.IsTrue(File.Exists(store.MainPath), "읽은 임시 파일은 바로 본 파일로 확정");
            Assert.IsFalse(File.Exists(store.TempPath));
        }

        [Test]
        public void Load_SchemaV1_MigratesEconomyDefaults()
        {
            var store = new SaveStore(_folder);
            Directory.CreateDirectory(_folder);
            File.WriteAllText(store.MainPath, "{\"schemaVersion\": 1, \"records\": [], \"unlockedChapter\": 2}");

            SaveData loaded = store.Load();

            Assert.AreEqual(SaveData.CurrentSchemaVersion, loaded.schemaVersion);
            Assert.AreEqual(2, loaded.unlockedChapter);
            Assert.AreEqual(5, loaded.hearts);
            Assert.AreEqual(1, loaded.accountLevel);
        }

        [Test]
        public void Load_OldSaveWithOnlyDungeonRuns_FinishesTutorial()
        {
            // 폰에서 찾음: v0.3에서 던전만 몇 판 한 저장 (기록·별·레벨 없음) → 튜토리얼도 안 뜨고 출석부도 안 열렸다
            var store = new SaveStore(_folder);
            Directory.CreateDirectory(_folder);
            File.WriteAllText(store.MainPath, "{\"schemaVersion\": 3, \"records\": [], \"unlockedChapter\": 1, \"dungeonRuns\": 3, \"gold\": 400}");

            SaveData loaded = store.Load();

            Assert.IsTrue(loaded.tutorialDone, "던전을 해 본 사람은 튜토리얼을 끝낸 것으로");
            Assert.IsFalse(Progress.Tutorial.Needed(loaded));
            Assert.IsTrue(Missions.Attendance.Unlocked(loaded));
        }

        [Test]
        public void Load_SaveAlreadyStuckOnSchema5_FinishesTutorial()
        {
            // 고치기 전 버전이 이미 구조 5로 옮겨 써 버린 저장
            var store = new SaveStore(_folder);
            Directory.CreateDirectory(_folder);
            var stuck = new SaveData { dungeonRuns = 2, tutorialDone = false };
            File.WriteAllText(store.MainPath, SaveStore.Sign(JsonUtility.ToJson(stuck, true)));

            Assert.IsTrue(store.Load().tutorialDone);
        }

        [Test]
        public void Load_FreshAndMidTutorialSaves_StayInTutorial()
        {
            var store = new SaveStore(_folder);
            Directory.CreateDirectory(_folder);

            File.WriteAllText(store.MainPath, SaveStore.Sign(JsonUtility.ToJson(new SaveData(), true)));
            SaveData fresh = store.Load();
            Assert.IsFalse(fresh.tutorialDone);
            Assert.IsTrue(Progress.Tutorial.Needed(fresh), "처음 깐 사람은 튜토리얼");

            var mid = new SaveData { tutorialRecordId = "tut" };
            mid.records.Add(new RunRecord { id = "tut" });
            File.WriteAllText(store.MainPath, SaveStore.Sign(JsonUtility.ToJson(mid, true)));
            SaveData midLoaded = store.Load();
            Assert.IsFalse(midLoaded.tutorialDone, "튜토리얼 기록만 있으면 아직 튜토리얼 중");
            Assert.IsTrue(Progress.Tutorial.BossPending(midLoaded));

            var dangling = new SaveData { tutorialRecordId = "gone" };
            File.WriteAllText(store.MainPath, SaveStore.Sign(JsonUtility.ToJson(dangling, true)));
            SaveData danglingLoaded = store.Load();
            Assert.IsNull(danglingLoaded.tutorialRecordId, "없는 기록을 가리키면 지운다");
            Assert.IsTrue(Progress.Tutorial.Needed(danglingLoaded), "→ 튜토리얼을 처음부터");
        }

        [Test]
        public void Reselect_SavedEvolution_DoesNotRevertWhenRecipeChanged()
        {
            // 레시피가 바뀌어 필요한 패시브가 기록에 없어도, 이미 만든 진화는 그대로 (GameDesign 7-5)
            _evolution.materialB = _atk;
            var evo = new RecordDraft.Entry(_whirl, 1, true, _evolution) { FromSavedRecord = true };
            var draft = new RecordDraft(new[] { evo, Entry(_def, 2) }, 27);

            Assert.IsFalse(draft.Reverts(evo));
            Assert.AreEqual("steel_whirl", draft.ToSkills()[0].id);
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
