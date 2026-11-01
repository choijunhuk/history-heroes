using System;
using System.IO;
using System.Text.RegularExpressions;
using HistoryHeroes.Economy;
using HistoryHeroes.Records;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HistoryHeroes.Tests
{
    /// <summary>
    /// 결제 처리 순서 (IapService.Process: 영수증 → 지급 · 저장 → 확정)와 클라우드 진행 고르기 때 결제 옮겨 담기 (IapLedger.MergeInto).
    /// 상점(PendingOrder) 없이 확정 호출을 세는 것으로 본다.
    /// </summary>
    public sealed class IapPurchaseFlowTests
    {
        private static readonly DateTime Now = new DateTime(2026, 11, 1, 3, 0, 0, DateTimeKind.Utc);
        private SaveStore _previous;
        private string _folder;

        [SetUp]
        public void UseTempSave()
        {
            _previous = SaveSystem.Store;
            _folder = Path.Combine(Path.GetTempPath(), "hh-iapflow-" + Guid.NewGuid().ToString("N"));
            SaveSystem.Use(new SaveStore(_folder));
        }

        [TearDown]
        public void RestoreSave()
        {
            SaveSystem.Use(_previous);
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
        }

        private static PurchaseEntry Run(string productId, string transactionId, string receiptProblem, out int confirms, out string notice)
        {
            int count = 0;
            PurchaseEntry granted = IapService.Process(productId, transactionId, receiptProblem, () => count++, out notice);
            confirms = count;
            return granted;
        }

        // ------------------------------------------------------------------ 처리 순서

        [Test]
        public void NewPurchase_GrantsSavesThenConfirms_WithNoScreenListening()
        {
            // 로비 · 상점이 없어도 (아무도 이벤트를 듣지 않아도) 지급 · 저장 · 확정이 끝난다
            PurchaseEntry granted = Run("gems_t1", "t-1", null, out int confirms, out string notice);
            Assert.IsNotNull(granted);
            Assert.AreEqual(1, confirms);
            Assert.IsNull(notice);
            Assert.AreEqual(200, Wallet.Gems(SaveSystem.Data));
            Assert.AreEqual(1, new SaveStore(_folder).Load().purchases.ledger.Count, "확정 전에 디스크에 적혀 있다");
        }

        [Test]
        public void ReceiptProblem_NoGrantNoConfirm()
        {
            PurchaseEntry granted = Run("gems_t3", "t-1", IapService.NotReadyText, out int confirms, out string notice);
            Assert.IsNull(granted);
            Assert.AreEqual(0, confirms, "확정하지 않으면 구글이 3일 안에 환불한다");
            Assert.AreEqual(IapService.NotReadyText, notice);
            Assert.AreEqual(0, Wallet.Gems(SaveSystem.Data));
            Assert.IsEmpty(SaveSystem.Data.purchases.ledger);
        }

        [Test]
        public void AlreadyGranted_ConfirmsOnly()
        {
            Run("gems_t2", "t-1", null, out _, out _);
            int gems = Wallet.Gems(SaveSystem.Data);
            PurchaseEntry again = Run("gems_t2", "t-1", null, out int confirms, out string notice);
            Assert.IsNull(again, "다시 온 결제는 새로 준 게 없다");
            Assert.AreEqual(1, confirms, "확정만 다시");
            Assert.IsNull(notice);
            Assert.AreEqual(gems, Wallet.Gems(SaveSystem.Data));
            Assert.AreEqual(1, SaveSystem.Data.purchases.ledger.Count);
        }

        [Test]
        public void SaveFailure_NoConfirm_AndRollsBack()
        {
            string blocker = Path.GetTempFileName();
            try
            {
                SaveSystem.Use(new SaveStore(Path.Combine(blocker, "blocked")));
                LogAssert.Expect(LogType.Error, new Regex("저장 실패"));
                PurchaseEntry granted = Run("gems_t4", "t-1", null, out int confirms, out string notice);
                Assert.IsNull(granted);
                Assert.AreEqual(0, confirms);
                StringAssert.Contains("저장하지 못해서", notice);
                Assert.AreEqual(0, Wallet.Gems(SaveSystem.Data));
                Assert.IsEmpty(SaveSystem.Data.purchases.ledger);
            }
            finally
            {
                File.Delete(blocker);
            }
        }

        [Test]
        public void SecondStarterPayment_NoConfirm_SaysRefundPending()
        {
            Run(IapCatalog.StarterPack, "s-1", null, out _, out _);
            PurchaseEntry granted = Run(IapCatalog.StarterPack, "s-2", null, out int confirms, out string notice);
            Assert.IsNull(granted);
            Assert.AreEqual(0, confirms);
            StringAssert.Contains("환불 대기", notice);
            Assert.AreEqual(300, Wallet.Gems(SaveSystem.Data));
        }

        // ------------------------------------------------------------------ 클라우드 진행 고르기

        [Test]
        public void CloudSwap_KeepsPurchasesMadeOnlyOnThisDevice()
        {
            var local = new SaveData { gemsFree = 10 };
            IapLedger.Apply(local, "gems_t2", "local-1", Now, out _);   // 유료 500 + 무료 550
            IapLedger.Apply(local, IapCatalog.StarterPack, "local-2", Now, out _);
            var cloud = new SaveData { unlockedChapter = 4, gemsFree = 70, gold = 5 };
            IapLedger.Apply(cloud, "gems_t1", "cloud-1", Now, out _);   // 유료 100 + 무료 100

            Assert.AreEqual(2, IapLedger.MergeInto(cloud, local));
            Assert.AreEqual(4, cloud.unlockedChapter, "고른 쪽 진행은 그대로");
            Assert.AreEqual(100 + 500 + 100, cloud.gemsPaid);
            Assert.AreEqual(70 + 100 + 550 + 200, cloud.gemsFree, "이 기기 무료 보석 10(게임 보상)은 옮기지 않는다");
            Assert.AreEqual(5 + 10000, cloud.gold);
            Assert.AreEqual(2, cloud.ItemCount(Wallet.Ticket));
            Assert.IsTrue(cloud.purchases.starterClaimed);
            CollectionAssert.AreEquivalent(new[] { "gems_t1", "gems_t2" }, cloud.purchases.firstBought);
            Assert.AreEqual(3, cloud.purchases.ledger.Count);

            // 두 번 합쳐도 두 번 주지 않는다
            Assert.AreEqual(0, IapLedger.MergeInto(cloud, local));
            Assert.AreEqual(700, cloud.gemsPaid);

            // 바꾼 뒤 같은 결제가 다시 와도 이미 지급
            Assert.AreEqual(IapLedger.Result.AlreadyGranted, IapLedger.Apply(cloud, "gems_t2", "local-1", Now, out _));
            Assert.AreEqual(IapLedger.Result.AlreadyClaimed, IapLedger.Apply(cloud, IapCatalog.StarterPack, "new", Now, out _));
            Assert.IsFalse(IapLedger.FirstBonusLeft(cloud, IapCatalog.Find("gems_t2")), "첫 구매 2배를 다시 받을 수 없다");
        }

        [Test]
        public void CloudSwapThroughReplace_SavesMergedPurchases()
        {
            // 계정 전환 길 (AccountService.ApplyCloud): 합친 뒤 SaveSystem.Replace
            IapService.GrantAndSave("gems_t3", "local-1", out _, out _);
            var cloud = new SaveData { unlockedChapter = 3 };
            IapLedger.MergeInto(cloud, SaveSystem.Data);
            Assert.IsTrue(SaveSystem.Replace(cloud));
            SaveData disk = new SaveStore(_folder).Load();
            Assert.AreEqual(3, disk.unlockedChapter);
            Assert.AreEqual((1000, 1150), (disk.gemsPaid, disk.gemsFree));
            Assert.AreEqual(IapLedger.Result.AlreadyGranted, IapService.GrantAndSave("gems_t3", "local-1", out _, out _));
        }

        [Test]
        public void StarterBoughtOnTwoDevices_BothPaymentsKept_PackStaysClaimed()
        {
            // 두 기기가 서로 모른 채 첫 결제 패키지를 하나씩 샀다: 돈은 두 번 냈으니 둘 다 남긴다
            var a = new SaveData();
            var b = new SaveData();
            Assert.AreEqual(IapLedger.Result.Granted, IapLedger.Apply(a, IapCatalog.StarterPack, "s-a", Now, out _));
            Assert.AreEqual(IapLedger.Result.Granted, IapLedger.Apply(b, IapCatalog.StarterPack, "s-b", Now, out _));
            Assert.AreEqual(1, IapLedger.MergeInto(b, a));
            Assert.AreEqual((200, 400, 4, 20000), (b.gemsPaid, b.gemsFree, b.ItemCount(Wallet.Ticket), b.gold));
            Assert.IsTrue(b.purchases.starterClaimed);
            Assert.IsFalse(IapLedger.Available(b, IapCatalog.Find(IapCatalog.StarterPack)));
            Assert.AreEqual(IapLedger.Result.AlreadyGranted, IapLedger.Apply(b, IapCatalog.StarterPack, "s-a", Now, out _));
        }

        [Test]
        public void RefundedEntry_MovesWithoutRegranting()
        {
            var local = new SaveData();
            IapLedger.Apply(local, "gems_t1", "r-1", Now, out PurchaseEntry e);
            e.refunded = true;
            var cloud = new SaveData();
            Assert.AreEqual(1, IapLedger.MergeInto(cloud, local));
            Assert.AreEqual(0, Wallet.Gems(cloud));
            Assert.IsTrue(cloud.purchases.ledger[0].refunded);
            Assert.AreNotSame(e, cloud.purchases.ledger[0], "장부 칸은 복사본");
        }
    }
}
