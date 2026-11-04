using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HistoryHeroes.Records;
using HistoryHeroes.Skills;
using HistoryHeroes.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HistoryHeroes.Tests
{
    public sealed class RecordArchivePresentationTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private Scene _scene;
        private RecordArchivePanel _screen;
        private SaveStore _previous;
        private string _folder;

        [SetUp]
        public void OpenArchiveWithIsolatedSave()
        {
            _previous = SaveSystem.Store;
            _folder = Path.Combine(Path.GetTempPath(), "hh-archive-present-" + Guid.NewGuid().ToString("N"));
            SaveSystem.Use(new SaveStore(_folder));
            _scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Lobby.unity");
            foreach (GameObject root in _scene.GetRootGameObjects())
            {
                _screen = root.GetComponentInChildren<RecordArchivePanel>(true);
                if (_screen != null) break;
            }
            Assert.IsNotNull(_screen);
        }

        [TearDown]
        public void RestoreStoreAndCloseScene()
        {
            if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
            SaveSystem.Use(_previous);
            if (_folder != null && Directory.Exists(_folder)) Directory.Delete(_folder, true);
        }

        [Test]
        public void BuiltArchiveConnectsFourIconRowsPerSlotAndScrollsVertically()
        {
            Assert.AreEqual(12, Field<Image[]>("slotSkillIcons").Length);
            Assert.AreEqual(12, Field<TMP_Text[]>("slotSkillLabels").Length);
            Assert.AreEqual(4, Field<Image[]>("tempSkillIcons").Length);
            Assert.AreEqual(4, Field<TMP_Text[]>("tempSkillLabels").Length);
            var scroll = Field<ScrollRect>("scroll");
            Assert.IsNotNull(scroll);
            Assert.IsTrue(scroll.vertical);
            Assert.IsFalse(scroll.horizontal);
            Assert.AreEqual("ArchiveViewport", scroll.viewport.name);
            Assert.AreEqual("ArchiveContent", scroll.content.name);
        }

        [Test]
        public void OccupiedSlotShowsValidatedSkillsInDescendingLevelOrder()
        {
            var record = Record(0);
            SaveSystem.Data.records.Add(record);
            _screen.Open(null);
            var db = Field<SkillDatabase>("database");
            var icons = Field<Image[]>("slotSkillIcons");
            var labels = Field<TMP_Text[]>("slotSkillLabels");
            var expected = RecordFormat.RepresentativeSkills(record, db);
            Assert.AreEqual(3, expected.Count);
            Assert.AreEqual("def_up", expected[0].id);
            for (int i = 0; i < expected.Count; i++)
            {
                SkillData skill = db.Find(expected[i].id);
                Assert.IsNotNull(skill.icon, expected[i].id + " 아이콘 필요");
                Assert.AreEqual(skill.icon, icons[i].sprite);
                Assert.IsTrue(icons[i].transform.parent.gameObject.activeSelf);
                StringAssert.Contains(skill.displayName, labels[i].text);
                StringAssert.Contains("Lv" + expected[i].level, labels[i].text);
            }
            Assert.IsFalse(icons[3].transform.parent.gameObject.activeSelf);
        }

        [Test]
        public void EmptySlotAfterRecordClearedHidesOldPreviewAndActions()
        {
            SaveData save = SaveSystem.Data;
            save.records.Add(Record(0));
            _screen.Open(null);
            save.records.Clear();
            _screen.Open(null);
            var icons = Field<Image[]>("slotSkillIcons");
            var labels = Field<TMP_Text[]>("slotSkillLabels");
            Assert.IsFalse(icons[0].transform.parent.parent.gameObject.activeSelf);
            Assert.IsNull(icons[0].sprite);
            Assert.IsEmpty(labels[0].text);
            Assert.IsFalse(Field<Button[]>("deleteButtons")[0].gameObject.activeSelf);
            Assert.IsFalse(Field<Button[]>("lockButtons")[0].gameObject.activeSelf);
        }

        [Test]
        public void LockedRecordHidesDeleteButKeepsUnlockAction()
        {
            var record = Record(0);
            record.locked = true;
            SaveSystem.Data.records.Add(record);
            _screen.Open(null);
            Assert.IsFalse(Field<Button[]>("deleteButtons")[0].gameObject.activeSelf);
            Assert.IsTrue(Field<Button[]>("lockButtons")[0].gameObject.activeSelf);
            Assert.AreEqual("잠금 풀기", Field<TMP_Text[]>("lockLabels")[0].text);
        }

        [Test]
        public void TemporaryRecordUsesSamePreviewAndMoveAppearsOnlyWhenSlotIsFree()
        {
            SaveData save = SaveSystem.Data;
            var temp = Record(-1);
            save.tempRecords.Add(temp);
            _screen.Open(null);
            var icons = Field<Image[]>("tempSkillIcons");
            Assert.AreEqual(Field<SkillDatabase>("database").Find("def_up").icon, icons[0].sprite);
            Assert.IsTrue(Field<Button>("moveTempButton").gameObject.activeSelf);
            for (int i = 0; i < save.recordSlots; i++) save.records.Add(Record(i));
            _screen.Open(null);
            Assert.IsFalse(Field<Button>("moveTempButton").gameObject.activeSelf);
            StringAssert.Contains("임시 칸", Field<TMP_Text>("tempTitle").text);
        }

        private T Field<T>(string name) => (T)typeof(RecordArchivePanel).GetField(name, Private).GetValue(_screen);

        private static RunRecord Record(int slot) => new RunRecord
        {
            id = "qa-record-" + slot,
            slot = slot,
            heroId = "khan",
            heroName = "칭기즈 칸",
            chapter = 1,
            roomReached = 10,
            roomCount = 10,
            cleared = true,
            capacity = 100,
            skills = new List<RecordSkill>
            {
                new RecordSkill("atk_up", 1, false),
                new RecordSkill("def_up", 3, false),
                new RecordSkill("hp_up", 2, false),
            },
        };
    }
}
