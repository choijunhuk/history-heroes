using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HistoryHeroes.Economy;
using HistoryHeroes.Records;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HistoryHeroes.Tests
{
    /// <summary>
    /// 기기 상점의 서버 검증 길 (IapService.ApplyVerdict: 서버가 정한 수량 그대로 지급 · 저장 → 확정)과
    /// 로그인 뒤 서버 장부 맞추기 (IapLedger.MergeServer). 서버 · 상점 없이 답 JSON과 확정 호출 수로 본다.
    /// </summary>
    public sealed class IapServerFlowTests
    {
        private SaveStore _previous;
        private string _folder;

        [SetUp]
        public void UseTempSave()
        {
            _previous = SaveSystem.Store;
            _folder = Path.Combine(Path.GetTempPath(), "hh-iapserver-" + Guid.NewGuid().ToString("N"));
            SaveSystem.Use(new SaveStore(_folder));
        }

        [TearDown]
        public void RestoreSave()
        {
            SaveSystem.Use(_previous);
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
        }

        private static PurchaseServer.Purchase Server(string productId, string token, int paid, int free, bool firstBonus = false, int tickets = 0, int gold = 0) => new PurchaseServer.Purchase
        {
            tokenHash = IapLedger.Hash(token),
            orderId = "GPA.1",
            productId = productId,
            utc = "2026-11-01T03:00:00.0000000Z",
            paidGems = paid,
            freeGems = free,
            tickets = tickets,
            gold = gold,
            firstBonus = firstBonus,
        };

        private static PurchaseServer.Verdict Verdict(string status, PurchaseServer.Purchase p = null) => new PurchaseServer.Verdict { status = status, purchase = p };

        private static PurchaseEntry Apply(PurchaseServer.Verdict v, string token, out int confirms, out string notice, out bool retry)
        {
            int count = 0;
            PurchaseEntry granted = IapService.ApplyVerdict(v, token, () => count++, out notice, out retry);
            confirms = count;
            return granted;
        }

        [Test]
        public void Granted_UsesServerAmounts_SavesThenConfirms()
        {
            // 이 기기는 아직 첫 구매 2배가 남았다고 보지만, 서버가 2배 없이 정했다 → 서버 값 그대로
            PurchaseEntry granted = Apply(Verdict(PurchaseServer.Granted, Server("gems_t1", "tok-1", 100, 0)), "tok-1", out int confirms, out string notice, out bool retry);
            Assert.IsNotNull(granted);
            Assert.AreEqual((1, false), (confirms, retry));
            Assert.IsNull(notice);
            Assert.AreEqual((100, 0), (SaveSystem.Data.gemsPaid, SaveSystem.Data.gemsFree));
            Assert.AreEqual("tok-1", granted.transactionId, "토큰 원문으로 적는다 (상점이 다시 보내면 바로 찾게)");
            CollectionAssert.DoesNotContain(SaveSystem.Data.purchases.firstBought, "gems_t1");
            Assert.AreEqual(1, new SaveStore(_folder).Load().purchases.ledger.Count, "확정 전에 디스크에 적혀 있다");
        }

        [Test]
        public void GrantedStarter_MarksClaimed_AndFirstBonusRecorded()
        {
            Apply(Verdict(PurchaseServer.Granted, Server(IapCatalog.StarterPack, "s-1", 100, 200, tickets: 2, gold: 10000)), "s-1", out _, out _, out _);
            Apply(Verdict(PurchaseServer.Granted, Server("gems_t2", "g-1", 500, 550, firstBonus: true)), "g-1", out _, out _, out _);
            SaveData d = SaveSystem.Data;
            Assert.IsTrue(d.purchases.starterClaimed);
            CollectionAssert.AreEqual(new[] { "gems_t2" }, d.purchases.firstBought);
            Assert.AreEqual((600, 750, 2, 10000), (d.gemsPaid, d.gemsFree, d.ItemCount(Wallet.Ticket), d.gold));
        }

        [Test]
        public void Already_NotInLocalLedger_GrantsRecordedEntry_ThenAlreadyGranted_ConfirmsOnly()
        {
            // 서버는 지급을 적었는데 이 기기 저장이 그 전에 꺼졌다 → already로 같은 지급을 받아 넣는다
            PurchaseServer.Purchase p = Server("gems_t3", "tok-1", 1000, 1150, firstBonus: true);
            Assert.IsNotNull(Apply(Verdict(PurchaseServer.Already, p), "tok-1", out int confirms, out _, out _));
            Assert.AreEqual(1, confirms);
            Assert.IsNull(Apply(Verdict(PurchaseServer.Already, p), "tok-1", out confirms, out string notice, out bool retry), "두 번 주지 않는다");
            Assert.AreEqual((1, false), (confirms, retry));
            Assert.IsNull(notice);
            Assert.AreEqual(2150, Wallet.Gems(SaveSystem.Data));
        }

        [TestCase(PurchaseServer.AlreadyClaimed, "1번만")]
        [TestCase(PurchaseServer.Invalid, "확인하지 못해서")]
        [TestCase(PurchaseServer.NotConfigured, "준비 중")]
        public void Refused_NoGrantNoConfirm(string status, string noticePart)
        {
            PurchaseEntry granted = Apply(Verdict(status), "tok-1", out int confirms, out string notice, out bool retry);
            Assert.IsNull(granted);
            Assert.AreEqual((0, false), (confirms, retry), "확정하지 않으면 구글이 3일 안에 환불한다");
            StringAssert.Contains(noticePart, notice);
            Assert.AreEqual(0, Wallet.Gems(SaveSystem.Data));
            Assert.IsEmpty(SaveSystem.Data.purchases.ledger);
        }

        [Test]
        public void RetryOrNoAnswer_KeepsPending()
        {
            foreach (PurchaseServer.Verdict v in new[] { Verdict(PurchaseServer.Retry), null, Verdict(PurchaseServer.Granted) /* 지급 내역 없음 */ })
            {
                Assert.IsNull(Apply(v, "tok-1", out int confirms, out string notice, out bool retry));
                Assert.AreEqual((0, true), (confirms, retry));
                Assert.AreEqual(IapService.CheckingText, notice);
            }

            Assert.AreEqual(0, Wallet.Gems(SaveSystem.Data));
        }

        [Test]
        public void SaveFailure_NoConfirm_RetryLater()
        {
            string blocker = Path.GetTempFileName();
            try
            {
                SaveSystem.Use(new SaveStore(Path.Combine(blocker, "blocked")));
                LogAssert.Expect(LogType.Error, new Regex("저장 실패"));
                LogAssert.Expect(LogType.Warning, new Regex("서버가 정한 지급"));
                Assert.IsNull(Apply(Verdict(PurchaseServer.Granted, Server("gems_t1", "tok-1", 100, 100, true)), "tok-1", out int confirms, out string notice, out bool retry));
                Assert.AreEqual((0, true), (confirms, retry));
                Assert.AreEqual(IapService.SaveFailedText, notice);
                Assert.AreEqual(0, Wallet.Gems(SaveSystem.Data));
            }
            finally
            {
                File.Delete(blocker);
            }
        }

        [Test]
        public void ServerJson_ParsesIntoVerdict()
        {
            // 서버(Newtonsoft, JsonProperty 이름)가 보내는 모양 그대로
            const string json = "{\n  \"status\": \"granted\",\n  \"reason\": null,\n  \"purchase\": {\"tokenHash\": \"ab\", \"orderId\": \"GPA.1\", \"productId\": \"gems_t1\", \"utc\": \"u\", " +
                                "\"paidGems\": 100, \"freeGems\": 100, \"tickets\": 0, \"gold\": 0, \"firstBonus\": true, \"test\": true}\n}";
            var v = JsonUtility.FromJson<PurchaseServer.Verdict>(json);
            Assert.AreEqual((PurchaseServer.Granted, "gems_t1", 100, true, true), (v.status, v.purchase.productId, v.purchase.freeGems, v.purchase.firstBonus, v.purchase.test));
            Assert.IsTrue(v.HasPurchase);
            Assert.IsFalse(JsonUtility.FromJson<PurchaseServer.Verdict>("{\"status\":\"invalid\",\"reason\":\"google_404\",\"purchase\":null}").HasPurchase);

            var ledger = JsonUtility.FromJson<PurchaseServer.Ledger>("{\"configured\":false,\"purchases\":[]}");
            Assert.AreEqual((false, 0), (ledger.configured, ledger.purchases.Count));
        }

        // ------------------------------------------------------------------ 서버 장부 맞추기

        [Test]
        public void MergeServer_AddsOnlyMissing_Idempotent_SetsFlags()
        {
            var d = new SaveData();
            IapLedger.ApplyServer(d, Server("gems_t1", "local", 100, 100, true), "local", out _); // 이 기기에도 있는 결제 (토큰 원문)
            var server = new List<PurchaseServer.Purchase>
            {
                Server("gems_t1", "local", 100, 100, true),
                Server(IapCatalog.StarterPack, "lost", 100, 200, tickets: 2, gold: 10000),
                Server("gems_t4", "other-device", 2750, 3300, true),
            };

            Assert.AreEqual(2, IapLedger.MergeServer(d, server));
            Assert.AreEqual(0, IapLedger.MergeServer(d, server), "몇 번 맞춰도 두 번 주지 않는다");
            Assert.AreEqual((100 + 100 + 2750, 100 + 200 + 3300), (d.gemsPaid, d.gemsFree));
            Assert.IsTrue(d.purchases.starterClaimed);
            Assert.IsFalse(IapLedger.Available(d, IapCatalog.Find(IapCatalog.StarterPack)));
            CollectionAssert.AreEquivalent(new[] { "gems_t1", "gems_t4" }, d.purchases.firstBought);
            Assert.IsFalse(IapLedger.FirstBonusLeft(d, IapCatalog.Find("gems_t4")));
            Assert.AreEqual(IapLedger.HashedPrefix + IapLedger.Hash("lost"), d.purchases.ledger[1].transactionId, "토큰 원문은 서버에도 없다");
        }

        [Test]
        public void RestoredEntry_MatchesStoreRedelivery_AndCloudMerge()
        {
            var d = new SaveData();
            IapLedger.MergeServer(d, new[] { Server("gems_t2", "tok-9", 500, 550, true) });
            // 상점이 같은 결제(토큰 원문)를 다시 보내면 되살린 칸을 찾는다 → 확정만
            Assert.IsNotNull(IapLedger.Find(d, "tok-9"));
            Assert.AreEqual(IapLedger.Result.AlreadyGranted, IapLedger.ApplyServer(d, Server("gems_t2", "tok-9", 500, 550, true), "tok-9", out _));

            // 다른 기기 진행(토큰 원문으로 적힘)과 합쳐도 같은 결제로 본다
            var other = new SaveData();
            IapLedger.ApplyServer(other, Server("gems_t2", "tok-9", 500, 550, true), "tok-9", out _);
            Assert.AreEqual(0, IapLedger.MergeInto(other, d));
            Assert.AreEqual(0, IapLedger.MergeInto(d, other));
            Assert.AreEqual(1050, Wallet.Gems(d));
        }

        [Test]
        public void ApplyServer_RejectsEmpty()
        {
            var d = new SaveData();
            Assert.AreEqual(IapLedger.Result.Invalid, IapLedger.ApplyServer(d, new PurchaseServer.Purchase(), "t", out _));
            Assert.AreEqual(IapLedger.Result.Invalid, IapLedger.ApplyServer(d, null, "t", out _));
            Assert.IsEmpty(d.purchases.ledger);
        }

        [Test]
        public void Hash_MatchesServer()
        {
            // 서버 PurchaseVerifierTests.Hash_IsLowercaseSha256Hex와 같은 기대값 (토큰 해시 · 계정 표시가 양쪽에서 같아야 한다)
            Assert.AreEqual("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", IapLedger.Hash("abc"));
        }

        [Test]
        public void AskRetry_BacksOff()
        {
            CollectionAssert.AreEqual(new[] { 5, 10, 20, 40, 80, 160, 300, 300 }, new[] { 1, 2, 3, 4, 5, 6, 7, 20 }.Select(IapService.AskRetrySeconds));
        }
    }
}
