using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using HistoryHeroes.Economy;
using HistoryHeroes.Records;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HistoryHeroes.Tests
{
    /// <summary>실제 결제 지급 장부 (Monetization 3-1 · 3-2 · 7장): 수량 · 첫 구매 2배 · 계정당 1번 · 같은 거래 다시 받기 · 유료/무료 · 저장 구조 13</summary>
    public sealed class IapLedgerTests
    {
        private static readonly DateTime Now = new DateTime(2026, 11, 1, 3, 0, 0, DateTimeKind.Utc);

        private static IapLedger.Result Buy(SaveData d, string productId, string transactionId) =>
            IapLedger.Apply(d, productId, transactionId, Now, out _);

        [Test]
        public void Catalog_MatchesDesign_AndIdsArePlayConsoleSafe()
        {
            Assert.AreEqual(7, IapCatalog.All.Count);
            Assert.AreEqual(IapCatalog.StarterPack, IapCatalog.All[0].Id, "첫 결제 패키지가 맨 앞");
            var expected = new Dictionary<string, (int price, int paid, int free)>
            {
                ["gems_t1"] = (1200, 100, 0),
                ["gems_t2"] = (5900, 500, 50),
                ["gems_t3"] = (12000, 1000, 150),
                ["gems_t4"] = (33000, 2750, 550),
                ["gems_t5"] = (59000, 4900, 1100),
                ["gems_t6"] = (119000, 9900, 2600),
            };
            var ids = new HashSet<string>();
            foreach (IapCatalog.Item item in IapCatalog.All)
            {
                Assert.IsTrue(ids.Add(item.Id), item.Id);
                Assert.IsTrue(Regex.IsMatch(item.Id, "^[a-z0-9][a-z0-9_.]*$"), "플레이 콘솔 상품 ID 규칙: " + item.Id);
                Assert.LessOrEqual(item.Description.Length, 200, item.Id);
                if (item.OncePerAccount) continue;
                Assert.AreEqual(expected[item.Id], (item.PriceKrw, item.PaidGems, item.FreeGems), item.Id);
            }

            IapCatalog.Item starter = IapCatalog.Find(IapCatalog.StarterPack);
            Assert.AreEqual((1200, 100, 200, 2, 10000), (starter.PriceKrw, starter.PaidGems, starter.FreeGems, starter.Tickets, starter.Gold));
            Assert.IsFalse(starter.FirstDouble, "패키지는 첫 구매 2배가 없다");
        }

        [Test]
        public void FirstPurchase_AddsBaseAgainAsFree_OncePerProduct()
        {
            var d = new SaveData();
            Assert.AreEqual(IapLedger.Result.Granted, Buy(d, "gems_t2", "t-1"));
            Assert.AreEqual(500, d.gemsPaid);
            Assert.AreEqual(550, d.gemsFree, "보너스 50 + 첫 구매 500");
            Assert.AreEqual(1050, Wallet.Gems(d));
            Assert.IsTrue(d.purchases.ledger[0].firstBonus);

            Assert.AreEqual(IapLedger.Result.Granted, Buy(d, "gems_t2", "t-2"));
            Assert.AreEqual(1000, d.gemsPaid);
            Assert.AreEqual(600, d.gemsFree, "두 번째는 보너스만");
            Assert.IsFalse(d.purchases.ledger[1].firstBonus);

            // 다른 상품의 첫 구매 2배는 따로 남아 있다
            Assert.IsTrue(IapLedger.FirstBonusLeft(d, IapCatalog.Find("gems_t1")));
            Assert.AreEqual(IapLedger.Result.Granted, Buy(d, "gems_t1", "t-3"));
            Assert.AreEqual(1100, d.gemsPaid);
            Assert.AreEqual(700, d.gemsFree);
            CollectionAssert.AreEquivalent(new[] { "gems_t2", "gems_t1" }, d.purchases.firstBought);
        }

        [Test]
        public void Preview_EqualsWhatApplyGrants()
        {
            var d = new SaveData();
            IapCatalog.Item t6 = IapCatalog.Find("gems_t6");
            PurchaseEntry preview = IapLedger.Preview(d, t6);
            Assert.AreEqual(IapLedger.Result.Granted, IapLedger.Apply(d, t6.Id, "t-6", Now, out PurchaseEntry granted));
            Assert.AreEqual((preview.paidGems, preview.freeGems, preview.firstBonus), (granted.paidGems, granted.freeGems, granted.firstBonus));
            Assert.AreEqual((9900, 12500), (granted.paidGems, granted.freeGems), "보너스 2,600 + 첫 구매 9,900");
            Assert.AreEqual("2026-11-01T03:00:00.0000000Z", granted.utc);
            Assert.AreEqual(IapLedger.IdOf("t-6"), granted.transactionId, "거래 번호는 해시로만 적는다 (서버 리뷰 Y1)");
        }

        [Test]
        public void StarterPack_GrantsOnce_ThenHiddenAndRefused()
        {
            var d = new SaveData();
            IapCatalog.Item starter = IapCatalog.Find(IapCatalog.StarterPack);
            Assert.IsTrue(IapLedger.Available(d, starter));
            Assert.AreEqual(IapLedger.Result.Granted, Buy(d, IapCatalog.StarterPack, "s-1"));
            Assert.AreEqual((100, 200, 2, 10000), (d.gemsPaid, d.gemsFree, d.ItemCount(Wallet.Ticket), d.gold));
            Assert.IsTrue(d.purchases.starterClaimed);
            Assert.IsFalse(IapLedger.Available(d, starter), "받은 뒤에는 상점에서 숨긴다");

            // 다른 기기 · 경쟁으로 또 결제가 와도 두 번 주지 않는다 (확정하지 않아 구글이 환불)
            Assert.AreEqual(IapLedger.Result.AlreadyClaimed, Buy(d, IapCatalog.StarterPack, "s-2"));
            Assert.AreEqual((100, 200, 2, 10000), (d.gemsPaid, d.gemsFree, d.ItemCount(Wallet.Ticket), d.gold));
            Assert.AreEqual(1, d.purchases.ledger.Count);
        }

        [Test]
        public void SameTransactionRedelivered_GrantsOnlyOnce()
        {
            var d = new SaveData();
            Assert.AreEqual(IapLedger.Result.Granted, Buy(d, "gems_t3", "dup"));
            int paid = d.gemsPaid, free = d.gemsFree;
            Assert.AreEqual(IapLedger.Result.AlreadyGranted, IapLedger.Apply(d, "gems_t3", "dup", Now, out PurchaseEntry again));
            Assert.AreEqual(IapLedger.IdOf("dup"), again.transactionId, "이미 준 내역을 돌려준다 (확정만 하면 된다)");
            Assert.AreEqual((paid, free), (d.gemsPaid, d.gemsFree));
            Assert.AreEqual(1, d.purchases.ledger.Count);

            // 첫 결제 패키지도 같은 거래면 "이미 지급" (확정해야 할 것) — "계정당 1번"과 헷갈리지 않는다
            Assert.AreEqual(IapLedger.Result.Granted, Buy(d, IapCatalog.StarterPack, "s-1"));
            Assert.AreEqual(IapLedger.Result.AlreadyGranted, Buy(d, IapCatalog.StarterPack, "s-1"));
        }

        [Test]
        public void UnknownProductOrMissingTransaction_GrantsNothing()
        {
            var d = new SaveData();
            Assert.AreEqual(IapLedger.Result.Invalid, Buy(d, "gems_t9", "x"));
            Assert.AreEqual(IapLedger.Result.Invalid, Buy(d, "gems_t1", ""));
            Assert.AreEqual(IapLedger.Result.Invalid, Buy(d, "gems_t1", null));
            Assert.AreEqual(0, Wallet.Gems(d));
            Assert.IsEmpty(d.purchases.ledger);
        }

        [Test]
        public void SpendingUsesFreeGemsFirst_PaidStaysForWithdrawal()
        {
            var d = new SaveData { gemsFree = 30 };
            Buy(d, "gems_t1", "t-1"); // 유료 100 + 첫 구매 무료 100
            Assert.AreEqual((100, 130), (d.gemsPaid, d.gemsFree));
            Assert.IsTrue(Wallet.TrySpendGems(d, 150));
            Assert.AreEqual((80, 0), (d.gemsPaid, d.gemsFree), "무료 130을 먼저 다 쓰고 유료에서 20");
            Assert.IsFalse(Wallet.TrySpendGems(d, 81));
            Assert.AreEqual(80, d.gemsPaid);
        }

        [Test]
        public void Text_ShowsEverythingGranted()
        {
            var d = new SaveData();
            IapLedger.Apply(d, IapCatalog.StarterPack, "s-1", Now, out PurchaseEntry e);
            string text = IapLedger.Text(e);
            StringAssert.Contains("보석 +300", text);
            StringAssert.Contains("고급 소환권 +2", text);
            StringAssert.Contains("골드 +10,000", text);
        }

        [Test]
        public void PrivacyVersion4_ServerPurchaseVerification_AsksVersion3PlayersAgain()
        {
            Assert.AreEqual(4, Progress.Consent.PrivacyVersion, "처리방침 4판 (서버 결제 검증 · 결제 기록 분리 보관)");
            SaveData save = SaveStore.NewGame();
            Progress.Consent.Accept(save, Now);
            save.consent.privacyVersion = 3; // 서버 검증 전에 동의한 사람
            Assert.IsTrue(Progress.Consent.NeedsConsent(save));
        }

        [Test]
        public void PaymentPolicyVersions_AskPlayersWhoAgreedBeforePaymentsAgain()
        {
            Assert.AreEqual(2, Progress.Consent.TermsVersion, "약관 4장 (유료 상품)");
            Assert.GreaterOrEqual(Progress.Consent.PrivacyVersion, 3, "처리방침 3판 (구글 플레이 결제 · 구매 기록)");
            SaveData save = SaveStore.NewGame();
            Progress.Consent.Accept(save, Now);
            save.consent.termsVersion = 1;
            save.consent.privacyVersion = 2; // 결제 전에 동의한 사람
            Assert.IsTrue(Progress.Consent.NeedsConsent(save));
        }

        [Test]
        public void Schema12Save_MigratesToEmptyLedger_AndLedgerSurvivesReload()
        {
            string folder = Path.Combine(Path.GetTempPath(), "hh-iap-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                var store = new SaveStore(folder);
                File.WriteAllText(store.MainPath, SaveStore.Sign("{\"schemaVersion\":12,\"gold\":55,\"gemsFree\":40,\"gemsPaid\":0,\"tutorialDone\":true}"));
                SaveData save = store.Load();
                Assert.GreaterOrEqual(SaveData.CurrentSchemaVersion, 13, "결제 장부는 저장 구조 13부터 있다 (14 = 우편함, 2026-11-03)");
                Assert.AreEqual(SaveData.CurrentSchemaVersion, save.schemaVersion);
                Assert.AreEqual((55, 40), (save.gold, save.gemsFree), "옛 진행은 그대로");
                Assert.IsNotNull(save.purchases);
                Assert.IsEmpty(save.purchases.ledger);
                Assert.IsEmpty(save.purchases.firstBought);
                Assert.IsFalse(save.purchases.starterClaimed);

                Buy(save, "gems_t2", "t-1");
                Buy(save, IapCatalog.StarterPack, "s-1");
                store.Save(save);
                SaveData back = new SaveStore(folder).Load();
                Assert.AreEqual(2, back.purchases.ledger.Count);
                Assert.IsTrue(back.purchases.starterClaimed);
                CollectionAssert.Contains(back.purchases.firstBought, "gems_t2");
                Assert.AreEqual(IapLedger.Result.AlreadyGranted, Buy(back, "gems_t2", "t-1"), "다시 읽은 저장도 같은 거래를 막는다");
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [Test]
        public void GrantAndSave_WritesLedgerToDisk_AndRollsBackWhenSaveFails()
        {
            SaveStore previous = SaveSystem.Store;
            string folder = Path.Combine(Path.GetTempPath(), "hh-iap-" + Guid.NewGuid().ToString("N"));
            string blocker = Path.GetTempFileName();
            try
            {
                SaveSystem.Use(new SaveStore(folder));
                Assert.AreEqual(IapLedger.Result.Granted, IapService.GrantAndSave("gems_t1", "t-1", out PurchaseEntry entry, out SaveSystem.CommitResult saved));
                Assert.AreEqual(SaveSystem.CommitResult.Saved, saved);
                Assert.AreEqual(200, entry.paidGems + entry.freeGems);
                Assert.AreEqual(1, new SaveStore(folder).Load().purchases.ledger.Count, "확정 전에 디스크에 적혀 있다");
                Assert.AreEqual(IapLedger.Result.AlreadyGranted, IapService.GrantAndSave("gems_t1", "t-1", out _, out _));
                Assert.AreEqual(200, Wallet.Gems(SaveSystem.Data));

                // 저장을 못 하면 지급까지 되돌린다 → 확정하지 않으니 상점이 다음에 다시 보낸다
                SaveSystem.Use(new SaveStore(Path.Combine(blocker, "blocked")));
                LogAssert.Expect(LogType.Error, new Regex("저장 실패"));
                Assert.AreEqual(IapLedger.Result.Granted, IapService.GrantAndSave("gems_t2", "t-2", out _, out saved));
                Assert.AreEqual(SaveSystem.CommitResult.SaveFailed, saved);
                Assert.AreEqual(0, Wallet.Gems(SaveSystem.Data));
                Assert.IsEmpty(SaveSystem.Data.purchases.ledger);
                Assert.IsEmpty(SaveSystem.Data.purchases.firstBought);
            }
            finally
            {
                SaveSystem.Use(previous);
                File.Delete(blocker);
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
            }
        }
    }
}
