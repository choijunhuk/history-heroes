using System;
using System.IO;
using HistoryHeroes.Progress;
using HistoryHeroes.Records;
using NUnit.Framework;

namespace HistoryHeroes.Tests
{
    /// <summary>첫 실행 동의 (LiveOps 19장): 새 저장 · 동의 기록 · 버전 올림 · 옛 저장 이전</summary>
    public sealed class ConsentTests
    {
        private static readonly DateTime Now = new DateTime(2026, 11, 1, 3, 0, 0, DateTimeKind.Utc);

        [Test]
        public void NewSave_NeedsConsent()
        {
            Assert.IsTrue(Consent.NeedsConsent(new SaveData()));
            Assert.IsTrue(Consent.NeedsConsent(null));
        }

        [Test]
        public void Accept_RecordsVersionsTimeAndAge_AndStopsAsking()
        {
            var save = new SaveData();
            Consent.Accept(save, Now);
            Assert.AreEqual(Consent.TermsVersion, save.consent.termsVersion);
            Assert.AreEqual(Consent.PrivacyVersion, save.consent.privacyVersion);
            Assert.IsTrue(save.consent.age14);
            Assert.AreEqual("2026-11-01T03:00:00.0000000Z", save.consent.agreedAtUtc);
            Assert.IsFalse(Consent.NeedsConsent(save));
            StringAssert.StartsWith("약관 동의: 2026-11-0", Consent.AgreedDateText(save));
        }

        [Test]
        public void AgreedDateText_BeforeConsent_SaysNotYet()
        {
            StringAssert.Contains("아직", Consent.AgreedDateText(new SaveData()));
        }

        [Test]
        public void RaisedVersion_AsksAgain()
        {
            var save = new SaveData();
            Consent.Accept(save, Now);
            save.consent.termsVersion = Consent.TermsVersion - 1; // 지금 버전이 한 단계 올랐다고 보는 것과 같다
            Assert.IsTrue(Consent.NeedsConsent(save));
            Consent.Accept(save, Now);
            save.consent.privacyVersion = Consent.PrivacyVersion - 1;
            Assert.IsTrue(Consent.NeedsConsent(save));
        }

        [Test]
        public void Age14Unchecked_AsksAgain()
        {
            var save = new SaveData();
            Consent.Accept(save, Now);
            save.consent.age14 = false;
            Assert.IsTrue(Consent.NeedsConsent(save));
        }

        [Test]
        public void OldSaveWithoutRecord_MigratesAndNeedsConsent_AndKeepsProgress()
        {
            string folder = Path.Combine(Path.GetTempPath(), "hh-consent-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                var store = new SaveStore(folder);
                File.WriteAllText(store.MainPath, SaveStore.Sign("{\"schemaVersion\":11,\"gold\":55,\"tutorialDone\":true}"));
                SaveData save = store.Load();
                Assert.AreEqual(SaveData.CurrentSchemaVersion, save.schemaVersion);
                Assert.AreEqual(55, save.gold);
                Assert.IsTrue(Consent.NeedsConsent(save));
                Consent.Accept(save, Now);
                store.Save(save);
                Assert.IsFalse(Consent.NeedsConsent(new SaveStore(folder).Load()), "동의 기록은 저장을 다시 읽어도 남는다");
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }
    }
}
