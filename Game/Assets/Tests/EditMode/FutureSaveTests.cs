using System;
using System.IO;
using HistoryHeroes.Records;
using NUnit.Framework;
using UnityEngine;

namespace HistoryHeroes.Tests
{
    public sealed class FutureSaveTests
    {
        private string _folder;
        private SaveStore _previousStore;

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Path.GetTempPath(), "hh-future-save-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);
            _previousStore = SaveSystem.Store;
        }

        [TearDown]
        public void TearDown()
        {
            SaveSystem.Use(_previousStore);
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
        }

        [Test]
        public void NewerMainSave_BlocksBackupFallbackAndAllWrites()
        {
            var store = new SaveStore(_folder);
            var backup = new SaveData { gold = 100 };
            File.WriteAllText(store.BackupPath, SaveStore.Sign(JsonUtility.ToJson(backup)));
            var future = new SaveData { schemaVersion = SaveData.CurrentSchemaVersion + 1, gold = 999 };
            string futureText = SaveStore.Sign(JsonUtility.ToJson(future));
            File.WriteAllText(store.MainPath, futureText);
            string backupText = File.ReadAllText(store.BackupPath);

            SaveSystem.Use(store);
            SaveData loaded = SaveSystem.Data;

            Assert.IsTrue(store.LastLoad.UnsupportedNewerSchema);
            Assert.IsTrue(SaveSystem.UnsupportedNewerSave);
            Assert.IsFalse(store.LastLoad.IsFresh);
            Assert.IsFalse(store.LastLoad.FromBackup);
            Assert.AreEqual(0, loaded.gold);
            Assert.IsFalse(SaveSystem.Save());
            SaveSystem.MarkDirty();
            Assert.IsFalse(SaveSystem.SaveIfDirty());
            bool changed = false;
            Assert.AreEqual(SaveSystem.CommitResult.Rejected, SaveSystem.Commit(() => { changed = true; return true; }));
            Assert.IsFalse(changed);
            Assert.AreEqual(futureText, File.ReadAllText(store.MainPath));
            Assert.AreEqual(backupText, File.ReadAllText(store.BackupPath));
            Assert.IsFalse(File.Exists(store.TempPath));
            Assert.IsNotEmpty(SaveSystem.UnsupportedNewerSaveText);
        }

        [Test]
        public void NewerTemporarySave_StopsRecoveryBeforeOldBackup()
        {
            var store = new SaveStore(_folder);
            var future = new SaveData { schemaVersion = SaveData.CurrentSchemaVersion + 1, gold = 999 };
            string futureText = SaveStore.Sign(JsonUtility.ToJson(future));
            File.WriteAllText(store.TempPath, futureText);
            File.WriteAllText(store.BackupPath, SaveStore.Sign(JsonUtility.ToJson(new SaveData { gold = 100 })));

            store.Load();

            Assert.IsTrue(store.LastLoad.UnsupportedNewerSchema);
            Assert.AreEqual(store.TempPath, store.LastLoad.Source);
            Assert.IsTrue(File.Exists(store.TempPath));
            Assert.AreEqual(futureText, File.ReadAllText(store.TempPath));
            Assert.IsFalse(File.Exists(store.MainPath));
        }

        [Test]
        public void DirectSave_CannotReplaceUnloadedNewerMainFile()
        {
            var store = new SaveStore(_folder);
            var future = new SaveData { schemaVersion = SaveData.CurrentSchemaVersion + 1 };
            string futureText = SaveStore.Sign(JsonUtility.ToJson(future));
            File.WriteAllText(store.MainPath, futureText);

            Assert.Throws<InvalidOperationException>(() => store.Save(new SaveData { gold = 12 }));
            Assert.AreEqual(futureText, File.ReadAllText(store.MainPath));
        }

        [Test]
        public void VersionNineSave_MigratesWithoutLosingProgress()
        {
            var store = new SaveStore(_folder);
            const string versionNine = "{\"schemaVersion\":9,\"integrity\":\"\",\"gold\":77,"
                + "\"records\":[{\"id\":\"old-record\",\"slot\":0,\"heroId\":\"khan\",\"meta\":{\"heroLevel\":5}}],"
                + "\"run\":{\"valid\":true,\"room\":3,\"hasMeta\":true,\"meta\":{\"heroLevel\":4}},"
                + "\"rift\":{\"checkpoint\":{\"valid\":true,\"floor\":20,\"heroId\":\"khan\"}}}";
            File.WriteAllText(store.MainPath, SaveStore.Sign(versionNine));

            SaveData loaded = store.Load();

            Assert.AreEqual(SaveData.CurrentSchemaVersion, loaded.schemaVersion);
            Assert.AreEqual(77, loaded.gold);
            Assert.AreEqual("old-record", loaded.records[0].id);
            Assert.AreEqual(5, loaded.records[0].meta.heroLevel);
            Assert.IsTrue(loaded.run.valid);
            Assert.AreEqual(3, loaded.run.room);
            Assert.AreEqual(4, loaded.run.meta.heroLevel);
            Assert.IsFalse(loaded.run.reviveUsed);
            Assert.IsFalse(loaded.run.meta.hasRunes);
            Assert.IsEmpty(loaded.run.meta.runes);
            Assert.IsTrue(loaded.rift.checkpoint.valid);
            Assert.AreEqual(20, loaded.rift.checkpoint.floor);
            Assert.IsFalse(loaded.rift.checkpoint.reviveUsed);
            Assert.IsFalse(loaded.rift.checkpoint.hasMeta);
            Assert.IsNull(loaded.pendingRecord);
            Assert.IsFalse(loaded.pendingTutorialRecord);
        }
    }
}
