using System;
using System.Collections.Generic;
using System.IO;
using HistoryHeroes.Records;
using HistoryHeroes.Skills;
using HistoryHeroes.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HistoryHeroes.Tests
{
    public sealed class PendingRecordTests
    {
        private string _folder;

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Path.GetTempPath(), "hh-pending-record-" + Guid.NewGuid().ToString("N"));
            SaveSystem.Use(new SaveStore(_folder));
        }

        [TearDown]
        public void TearDown()
        {
            SaveSystem.Use(null);
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
            if (File.Exists(_folder)) File.Delete(_folder);
        }

        [Test]
        public void PendingPreview_IsPure_AndTutorialRewardIsCommittedOnce()
        {
            var skill = ScriptableObject.CreateInstance<PassiveSkillData>();
            skill.id = "atk_up";
            skill.displayName = "공격력";
            skill.recordable = true;
            skill.maxLevel = 5;
            skill.saveCostFirstLevel = 1;
            skill.saveCostPerLevel = 1;
            var draft = new RecordDraft(new[] { new RecordDraft.Entry(skill, 2, false, null) }, 3);
            SaveData data = SaveSystem.Data;
            data.pendingRecord = new RunRecord
            {
                id = "pending-tutorial", heroId = "khan", capacity = 3,
                skills = new List<RecordSkill> { new RecordSkill("atk_up", 2, false) },
                meta = new MetaSnapshot { heroLevel = 12 },
            };
            data.pendingTutorialRecord = true;
            Assert.IsTrue(SaveSystem.Save());

            for (int i = 0; i < 5; i++)
            {
                RunRecord preview = RecordSaveScreen.PreviewPending(data.pendingRecord, draft);
                Assert.AreEqual("pending-tutorial", preview.id);
                Assert.AreEqual(12, preview.meta.heroLevel);
                Assert.AreEqual(0, data.gold);
                Assert.IsNull(data.tutorialRecordId);
            }

            draft.SetLevel(draft.Entries[0], 1);
            Assert.AreEqual(1, RecordSaveScreen.PreviewPending(data.pendingRecord, draft).skills[0].level);
            Assert.AreEqual(2, data.pendingRecord.skills[0].level);
            Assert.AreEqual(SaveSystem.CommitResult.Saved, RecordSaveScreen.SavePendingToSlot(draft, 0));
            Assert.AreEqual(300, data.gold);
            Assert.AreEqual("pending-tutorial", data.tutorialRecordId);
            Assert.IsNull(data.pendingRecord);
            Assert.IsFalse(data.pendingTutorialRecord);
            Assert.AreEqual(SaveSystem.CommitResult.Rejected, RecordSaveScreen.SavePendingToSlot(draft, 0));
            Assert.AreEqual(300, data.gold);
            SaveSystem.Use(new SaveStore(_folder));
            Assert.AreEqual(300, SaveSystem.Data.gold);
            Assert.AreEqual("pending-tutorial", SaveSystem.Data.tutorialRecordId);
            Assert.AreEqual("pending-tutorial", SaveSystem.Data.RecordInSlot(0).id);
            Assert.AreEqual(1, SaveSystem.Data.RecordInSlot(0).skills[0].level);
            Assert.IsNull(SaveSystem.Data.pendingRecord);
            UnityEngine.Object.DestroyImmediate(skill);
        }

        [Test]
        public void PendingRecord_SurvivesReload_AndDiscardRemovesIt()
        {
            SaveSystem.Data.pendingRecord = new RunRecord
            {
                id = "unfinished", heroId = "khan", capacity = 5,
                skills = new List<RecordSkill> { new RecordSkill("atk_up", 2, false) },
            };
            Assert.IsTrue(SaveSystem.Save());
            SaveSystem.Use(new SaveStore(_folder));
            Assert.AreEqual("unfinished", SaveSystem.Data.pendingRecord.id);
            Assert.AreEqual(2, SaveSystem.Data.pendingRecord.skills[0].level);

            Assert.AreEqual(SaveSystem.CommitResult.Saved, RecordSaveScreen.DiscardPending());
            SaveSystem.Use(new SaveStore(_folder));
            Assert.IsNull(SaveSystem.Data.pendingRecord);
        }

        [Test]
        public void FailedTutorialRecordSave_RollsBackRecordIdRewardAndCandidate()
        {
            File.WriteAllText(_folder, "block directory creation");
            SaveSystem.Use(new SaveStore(_folder));
            SaveData data = SaveSystem.Data;
            data.pendingRecord = new RunRecord
            {
                id = "retry", heroId = "khan", capacity = 5,
                skills = new List<RecordSkill> { new RecordSkill("atk_up", 1, false) },
            };
            data.pendingTutorialRecord = true;
            var skill = ScriptableObject.CreateInstance<PassiveSkillData>();
            skill.id = "atk_up";
            skill.recordable = true;
            skill.maxLevel = 5;
            skill.saveCostFirstLevel = 1;
            var draft = new RecordDraft(new[] { new RecordDraft.Entry(skill, 1, false, null) }, 5);

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("저장 실패"));
            Assert.AreEqual(SaveSystem.CommitResult.SaveFailed, RecordSaveScreen.SavePendingToSlot(draft, 0));
            Assert.AreEqual(0, data.gold);
            Assert.IsNull(data.tutorialRecordId);
            Assert.AreEqual("retry", data.pendingRecord.id);
            Assert.IsTrue(data.pendingTutorialRecord);
            Assert.AreEqual(0, data.records.Count);
            UnityEngine.Object.DestroyImmediate(skill);
        }
    }
}
