using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HistoryHeroes.Economy;
using HistoryHeroes.Records;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.TestTools;

namespace HistoryHeroes.Tests
{
    /// <summary>
    /// 기기 상점의 서버 검증 길 (IapService.ApplyVerdict: 서버가 정한 수량 그대로 지급 · 저장 → 확정, 묻는 사이 주인이 바뀌면 안 씀)과
    /// 로그인 뒤 서버 장부 맞추기 (IapLedger.MergeServer: 없는 결제 넣기 · 환불된 결제 회수 · 음수 보석 · 상점 잠금). 서버 · 상점 없이 답과 확정 호출 수로 본다.
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

        private static PurchaseServer.Purchase Server(string productId, string token, int paid, int free, bool firstBonus = false, int tickets = 0, int gold = 0, bool voided = false) =>
            new PurchaseServer.Purchase
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
                voided = voided,
            };

        private static PurchaseServer.Verdict Verdict(string status, PurchaseServer.Purchase p = null, string reason = null) =>
            new PurchaseServer.Verdict { status = status, purchase = p, reason = reason };

        private static PurchaseEntry Apply(PurchaseServer.Verdict v, string token, out int confirms, out string notice, out bool retry, bool justBought = false)
        {
            IapService.Owner owner = IapService.Owner.Now; // 물을 때의 주인 (서버 답을 기다리지 않으니 바로 지금)
            int count = 0;
            PurchaseEntry granted = IapService.ApplyVerdict(v, token, owner, justBought, () => count++, out notice, out retry);
            confirms = count;
            return granted;
        }

        // ------------------------------------------------------------------ 서버 답 → 지급 · 확정

        [Test]
        public void Granted_UsesServerAmounts_SavesThenConfirms_StoresOnlyHashedId()
        {
            // 이 기기는 아직 첫 구매 2배가 남았다고 보지만, 서버가 2배 없이 정했다 → 서버 값 그대로
            PurchaseEntry granted = Apply(Verdict(PurchaseServer.Granted, Server("gems_t1", "tok-1", 100, 0)), "tok-1", out int confirms, out string notice, out bool retry);
            Assert.IsNotNull(granted);
            Assert.AreEqual((1, false), (confirms, retry));
            Assert.IsNull(notice);
            Assert.AreEqual((100, 0), (SaveSystem.Data.gemsPaid, SaveSystem.Data.gemsFree));
            Assert.AreEqual(IapLedger.HashedPrefix + IapLedger.Hash("tok-1"), granted.transactionId, "토큰 원문은 저장하지 않는다 (서버 리뷰 Y1)");
            CollectionAssert.DoesNotContain(SaveSystem.Data.purchases.firstBought, "gems_t1");
            string disk = File.ReadAllText(SaveSystem.Store.MainPath);
            StringAssert.DoesNotContain("tok-1", disk);
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
            PurchaseServer.Purchase p = Server("gems_t3", "tok-1", 1000, 1150, firstBonus: true);
            Assert.IsNotNull(Apply(Verdict(PurchaseServer.Already, p), "tok-1", out int confirms, out _, out _));
            Assert.AreEqual(1, confirms);
            Assert.IsNull(Apply(Verdict(PurchaseServer.Already, p), "tok-1", out confirms, out string notice, out bool retry), "두 번 주지 않는다");
            Assert.AreEqual((1, false), (confirms, retry));
            Assert.IsNull(notice);
            Assert.AreEqual(2150, Wallet.Gems(SaveSystem.Data));
        }

        [TestCase(PurchaseServer.AlreadyClaimed, null, "1번만")]
        [TestCase(PurchaseServer.Invalid, "account_mismatch", "확인하지 못해서")]
        [TestCase(PurchaseServer.Invalid, "not_paid_promo", "프로모션")]
        [TestCase(PurchaseServer.Invalid, "google_404:purchaseTokenNotFound", "확인하지 못해서")]
        [TestCase(PurchaseServer.NotConfigured, null, "준비 중")]
        public void Refused_NoGrantNoConfirm(string status, string reason, string noticePart)
        {
            PurchaseEntry granted = Apply(Verdict(status, reason: reason), "tok-1", out int confirms, out string notice, out bool retry);
            Assert.IsNull(granted);
            Assert.AreEqual((0, false), (confirms, retry), "확정하지 않으면 구글이 3일 안에 환불한다");
            StringAssert.Contains(noticePart, notice);
            Assert.AreEqual(0, Wallet.Gems(SaveSystem.Data));
            Assert.IsEmpty(SaveSystem.Data.purchases.ledger);
        }

        [Test]
        public void JustBought_Google404_KeepsAsking()
        {
            Assert.IsNull(Apply(Verdict(PurchaseServer.Invalid, reason: "google_404:purchaseTokenNotFound"), "tok-1", out int confirms, out string notice, out bool retry, justBought: true));
            Assert.AreEqual((0, true), (confirms, retry), "막 산 결제는 구글 반영을 기다린다 (10분)");
            Assert.AreEqual(IapService.CheckingText, notice);
        }

        [Test]
        public void RetryOrNoAnswer_KeepsPending_PendingHasOwnNotice()
        {
            foreach (PurchaseServer.Verdict v in new[] { Verdict(PurchaseServer.Retry), null, Verdict(PurchaseServer.Granted) /* 지급 내역 없음 */ })
            {
                Assert.IsNull(Apply(v, "tok-1", out int confirms, out string notice, out bool retry));
                Assert.AreEqual((0, true), (confirms, retry));
                Assert.AreEqual(IapService.CheckingText, notice);
            }

            Apply(Verdict(PurchaseServer.Retry, reason: "pending"), "tok-1", out _, out string pending, out bool again);
            Assert.AreEqual((IapService.PendingText, true), (pending, again));
            StringAssert.Contains("아직 끝나지 않았어요", pending);
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
        public void OwnerChangedWhileAsking_NoGrantNoConfirm_AskAgain()
        {
            // 서버에 묻는 사이 클라우드 진행으로 바꿨다 (SaveSystem.Replace → 저장 세대가 바뀜): A의 답을 B 진행에 넣지 않고 확정도 하지 않는다
            IapService.Owner askedAs = IapService.Owner.Now;
            Assert.IsTrue(SaveSystem.Replace(new SaveData { unlockedChapter = 3 }));
            LogAssert.Expect(LogType.Warning, new Regex("계정 · 저장이 바뀜"));
            int confirms = 0;
            PurchaseEntry granted = IapService.ApplyVerdict(Verdict(PurchaseServer.Granted, Server("gems_t4", "tok-1", 2750, 3300, true)), "tok-1",
                askedAs, false, () => confirms++, out string notice, out bool retry);
            Assert.IsNull(granted);
            Assert.AreEqual((0, true), (confirms, retry), "결제는 남겨 두고 지금 주인으로 다시 묻는다");
            Assert.IsNull(notice);
            Assert.AreEqual(0, Wallet.Gems(SaveSystem.Data));
            Assert.IsEmpty(SaveSystem.Data.purchases.ledger);
        }

        [Test]
        public void OwnerCompare_PlayerOrGeneration()
        {
            var a = new IapService.Owner("player-a", 5);
            Assert.IsTrue(a.Same(new IapService.Owner("player-a", 5)));
            Assert.IsFalse(a.Same(new IapService.Owner("player-b", 5)), "다른 플레이어로 바뀜");
            Assert.IsFalse(a.Same(new IapService.Owner("player-a", 6)), "저장이 통째로 바뀜");
            Assert.IsFalse(a.Same(new IapService.Owner(null, 5)), "로그아웃 · 계정 삭제");
        }

        [Test]
        public void Voided_Verdict_ClawsBackLocal_NoConfirm()
        {
            Apply(Verdict(PurchaseServer.Granted, Server("gems_t2", "tok-1", 500, 550, true)), "tok-1", out _, out _, out _);
            Wallet.TrySpendGems(SaveSystem.Data, 800);
            Assert.IsNull(Apply(Verdict(PurchaseServer.Voided, Server("gems_t2", "tok-1", 500, 550, true, voided: true)), "tok-1", out int confirms, out string notice, out bool retry));
            Assert.AreEqual((0, false), (confirms, retry));
            StringAssert.Contains("환불", notice);
            StringAssert.Contains("0 이상이 될 때까지", notice);
            Assert.AreEqual(-800, Wallet.Gems(SaveSystem.Data), "쓴 만큼 빚");
            Assert.AreEqual(-800, new SaveStore(_folder).Load().gemsPaid + new SaveStore(_folder).Load().gemsFree, "저장까지");
        }

        [Test]
        public void ServerJson_ParsesIntoVerdict()
        {
            const string json = "{\n  \"status\": \"granted\",\n  \"reason\": null,\n  \"purchase\": {\"tokenHash\": \"ab\", \"orderId\": \"GPA.1\", \"productId\": \"gems_t1\", \"utc\": \"u\", " +
                                "\"paidGems\": 100, \"freeGems\": 100, \"tickets\": 0, \"gold\": 0, \"firstBonus\": true, \"test\": true, \"voided\": false, \"acknowledged\": true}\n}";
            var v = JsonUtility.FromJson<PurchaseServer.Verdict>(json);
            Assert.AreEqual((PurchaseServer.Granted, "gems_t1", 100, true, true, false), (v.status, v.purchase.productId, v.purchase.freeGems, v.purchase.firstBonus, v.purchase.test, v.purchase.voided));
            Assert.IsTrue(v.HasPurchase);
            Assert.IsFalse(JsonUtility.FromJson<PurchaseServer.Verdict>("{\"status\":\"invalid\",\"reason\":\"google_404\",\"purchase\":null}").HasPurchase);

            var ledger = JsonUtility.FromJson<PurchaseServer.Ledger>("{\"status\":\"ok\",\"configured\":false,\"purchases\":[{\"tokenHash\":\"x\",\"productId\":\"gems_t1\",\"voided\":true}]}");
            Assert.AreEqual(("ok", false, 1, true), (ledger.status, ledger.configured, ledger.purchases.Count, ledger.purchases[0].voided));
        }

        // ------------------------------------------------------------------ 서버 장부 맞추기 · 회수

        [Test]
        public void MergeServer_AddsOnlyMissing_Idempotent_SetsFlags()
        {
            var d = new SaveData();
            IapLedger.ApplyServer(d, Server("gems_t1", "local", 100, 100, true), "local", out _); // 이 기기에도 있는 결제
            var server = new List<PurchaseServer.Purchase>
            {
                Server("gems_t1", "local", 100, 100, true),
                Server(IapCatalog.StarterPack, "lost", 100, 200, tickets: 2, gold: 10000),
                Server("gems_t4", "other-device", 2750, 3300, true),
            };

            Assert.AreEqual(2, IapLedger.MergeServer(d, server).Added);
            Assert.AreEqual(0, IapLedger.MergeServer(d, server).Added, "몇 번 맞춰도 두 번 주지 않는다");
            Assert.AreEqual((100 + 100 + 2750, 100 + 200 + 3300), (d.gemsPaid, d.gemsFree));
            Assert.IsTrue(d.purchases.starterClaimed);
            Assert.IsFalse(IapLedger.Available(d, IapCatalog.Find(IapCatalog.StarterPack)));
            CollectionAssert.AreEquivalent(new[] { "gems_t1", "gems_t4" }, d.purchases.firstBought);
            Assert.AreEqual(IapLedger.HashedPrefix + IapLedger.Hash("lost"), d.purchases.ledger[1].transactionId);
        }

        [Test]
        public void MergeServer_Voided_ClawsBack_GoesNegative_NeverRestores()
        {
            var d = new SaveData { gold = 500 };
            IapLedger.ApplyServer(d, Server(IapCatalog.StarterPack, "s-1", 100, 200, tickets: 2, gold: 10000), "s-1", out _);
            Wallet.TrySpendGems(d, 250);                    // 300 중 250을 썼다
            Wallet.TrySpendGold(d, 10000);                  // 골드도 대부분 썼다
            d.AddItem(Wallet.Ticket, -1);                   // 소환권 1장 썼다

            IapLedger.ServerMerge m = IapLedger.MergeServer(d, new[] { Server(IapCatalog.StarterPack, "s-1", 100, 200, tickets: 2, gold: 10000, voided: true) });
            Assert.AreEqual((1, 300), (m.Voided, m.GemsTaken));
            Assert.AreEqual(-250, Wallet.Gems(d), "쓴 보석만큼 음수");
            Assert.AreEqual((0, 0), (d.gold, d.ItemCount(Wallet.Ticket)), "골드 · 소환권은 남은 만큼만 (음수 없음)");
            Assert.IsTrue(Wallet.InDebt(d));

            // 다시 맞추거나 같은 결제가 와도 회수 · 지급을 반복하지 않는다
            Assert.AreEqual(0, IapLedger.MergeServer(d, new[] { Server(IapCatalog.StarterPack, "s-1", 100, 200, voided: true) }).Voided);
            Assert.AreEqual(IapLedger.Result.Invalid, IapLedger.ApplyServer(d, Server(IapCatalog.StarterPack, "s-1", 100, 200, voided: true), "s-1", out _));
            Assert.AreEqual(-250, Wallet.Gems(d));

            // 새로 얻는 보석으로 빚부터 메운다
            Wallet.AddFreeGems(d, 300);
            Assert.AreEqual((50, false), (Wallet.Gems(d), Wallet.InDebt(d)));
            Assert.IsTrue(d.gemsPaid >= 0 && d.gemsFree >= 0);
        }

        [Test]
        public void MergeServer_VoidedButNeverHere_RecordedAsVoided_NotGranted()
        {
            var d = new SaveData();
            IapLedger.ServerMerge m = IapLedger.MergeServer(d, new[] { Server("gems_t5", "gone", 4900, 6000, true, voided: true) });
            Assert.AreEqual((0, 0), (m.Added, m.Voided));
            Assert.AreEqual(0, Wallet.Gems(d));
            Assert.IsTrue(d.purchases.ledger.Single().voided, "회수됨으로만 적는다 → 나중에 같은 결제가 와도 넣지 않는다");
            Assert.AreEqual(IapLedger.Result.Invalid, IapLedger.ApplyServer(d, Server("gems_t5", "gone", 4900, 6000, true, voided: true), "gone", out _));
            Assert.AreEqual(0, Wallet.Gems(d));
        }

        [Test]
        public void CloudSwap_VoidedOnOneSide_ClawsBackOnTheOther()
        {
            var cloud = new SaveData();
            IapLedger.ApplyServer(cloud, Server("gems_t2", "tok", 500, 550, true), "tok", out _);
            var device = new SaveData();
            IapLedger.ApplyServer(device, Server("gems_t2", "tok", 500, 550, true), "tok", out _);
            IapLedger.MergeServer(device, new[] { Server("gems_t2", "tok", 500, 550, true, voided: true) });

            IapLedger.MergeInto(cloud, device); // 클라우드 진행을 골랐다: 이 기기에서 회수한 결제를 되살리지 않는다
            Assert.AreEqual(0, Wallet.Gems(cloud));
            Assert.IsTrue(cloud.purchases.ledger.Single().voided);
        }

        [Test]
        public void InDebt_BlocksBoxesAndRelicGacha_UntilRepaid()
        {
            var d = new SaveData { gold = 100000, gemsFree = -10, unlockedChapter = 3 };
            d.AddItem(Wallet.Ticket, 3);
            var gacha = ScriptableObject.CreateInstance<Equipment.GachaTable>();
            gacha.normal = new Equipment.BoxRates { goldCost = 100 };
            gacha.premium = new Equipment.BoxRates { gemCost = 1 };
            var relics = ScriptableObject.CreateInstance<Relics.RelicTable>();
            EditorTools.SceneBuilder.ConfigureRelics(relics, id => ScriptableObject.CreateInstance<Relics.RelicData>(), key => null);
            try
            {
                Assert.IsTrue(Wallet.InDebt(d));
                Assert.IsFalse(Equipment.GachaService.TryPay(d, gacha, gacha.normal, 1, Equipment.GachaService.Pay.Gold, "2026-11-01"), "골드 상자도 잠김");
                Assert.IsFalse(Equipment.GachaService.TryPay(d, gacha, gacha.premium, 1, Equipment.GachaService.Pay.Ticket, "2026-11-01"), "소환권도 잠김");
                Assert.IsFalse(Relics.RelicGachaService.TryPull(d, relics, 1, Relics.RelicGachaService.Pay.Free, "2026-11-01", 0, out _, () => 0f), "유물 무료 뽑기도 잠김");
                Assert.IsFalse(Wallet.TrySpendGems(d, 1));
                Assert.AreEqual((100000, 3), (d.gold, d.ItemCount(Wallet.Ticket)));

                Wallet.AddFreeGems(d, 10); // 빚을 다 메웠다
                Assert.IsFalse(Wallet.InDebt(d));
                Assert.IsTrue(Equipment.GachaService.TryPay(d, gacha, gacha.normal, 1, Equipment.GachaService.Pay.Gold, "2026-11-01"));
                Assert.IsTrue(Relics.RelicGachaService.TryPull(d, relics, 1, Relics.RelicGachaService.Pay.Free, "2026-11-01", 0, out _, () => 0f));
            }
            finally
            {
                foreach (Relics.RelicData relic in relics.relics) UnityEngine.Object.DestroyImmediate(relic);
                UnityEngine.Object.DestroyImmediate(relics);
                UnityEngine.Object.DestroyImmediate(gacha);
            }
        }

        [Test]
        public void KeepDevice_ClawbackOnlyMerge_IsSavedBeforeUpload_UploadedFileHasClawback()
        {
            // "이 기기 진행"을 골랐다: 다른 쪽(클라우드)에서 회수된 결제가 이 기기에는 아직 그대로 → 회수만 일어나도 저장해야 올리는 파일에 들어간다 (2차 리뷰 E)
            PurchaseServer.Purchase bought = Server("gems_t2", "tok", 500, 550, true);
            IapLedger.ApplyServer(SaveSystem.Data, bought, "tok", out _);
            Assert.IsTrue(SaveSystem.Save());
            var cloud = new SaveData();
            IapLedger.ApplyServer(cloud, bought, "tok", out _);
            IapLedger.MergeServer(cloud, new[] { Server("gems_t2", "tok", 500, 550, true, voided: true) });

            Assert.IsTrue(Account.AccountService.KeepPurchases(cloud));
            Assert.IsTrue(Account.AccountService.ReadUploadFile(out string json, out SaveData uploaded));
            Assert.AreEqual(0, Wallet.Gems(uploaded), "올리는 파일에 회수가 들어 있다");
            Assert.IsTrue(uploaded.purchases.ledger.Single().voided);
            StringAssert.Contains("\"voided\": true", json);
            Assert.AreEqual(0, IapLedger.MergeInto(SaveSystem.Data, cloud), "한 번 회수한 뒤로는 바뀐 게 없다");
        }

        [Test]
        public void Upload_RawTokenFile_IsMigratedAndSavedFirst_NeverUploadsRawToken()
        {
            // 옛 저장 파일(구매 토큰 원문 거래 번호): 읽을 때 메모리에서만 바뀐다 → 올리기 전에 바꾼 모양으로 저장하고 그 파일을 올린다 (2차 리뷰 Y1)
            var old = new SaveData { gemsPaid = 100 };
            old.purchases.ledger.Add(new PurchaseEntry { transactionId = "raw-token", productId = "gems_t1", paidGems = 100 });
            Directory.CreateDirectory(_folder);
            var store = new SaveStore(_folder);
            File.WriteAllText(store.MainPath, SaveStore.Serialize(old));
            SaveSystem.Use(store);
            Assert.AreEqual(IapLedger.IdOf("raw-token"), SaveSystem.Data.purchases.ledger[0].transactionId, "메모리는 이미 바뀌었다");
            StringAssert.Contains("raw-token", File.ReadAllText(store.MainPath), "파일은 아직 옛 모양");

            Assert.IsTrue(Account.AccountService.ReadUploadFile(out string json, out SaveData uploaded));
            StringAssert.DoesNotContain("raw-token", json);
            StringAssert.Contains(IapLedger.IdOf("raw-token"), json);
            StringAssert.DoesNotContain("raw-token", File.ReadAllText(store.MainPath), "올리기 전에 디스크에도 저장됐다");
            Assert.AreEqual(100, uploaded.gemsPaid);
        }

        [Test]
        public void Upload_MixedRawAndHashedSameToken_DecidedStructurally_ResavedFirst()
        {
            // 같은 토큰이 원문 칸과 해시 칸으로 둘 다 있는 파일: 글자 찾기로는 "다 바뀜"처럼 보인다 → 파싱한 칸마다 보고 다시 저장해야 한다 (3차 리뷰 3)
            var mixed = new SaveData { gemsPaid = 200 };
            mixed.purchases.ledger.Add(new PurchaseEntry { transactionId = "raw-token", productId = "gems_t1", paidGems = 100 });
            mixed.purchases.ledger.Add(new PurchaseEntry { transactionId = IapLedger.IdOf("raw-token"), productId = "gems_t1", paidGems = 100 });
            Directory.CreateDirectory(_folder);
            var store = new SaveStore(_folder);
            File.WriteAllText(store.MainPath, SaveStore.Serialize(mixed));
            SaveSystem.Use(store);
            Assert.IsTrue(SaveSystem.Data.purchaseIdsMigrated, "읽을 때 바꾼 칸이 있었다");

            Assert.IsTrue(Account.AccountService.ReadUploadFile(out string json, out SaveData uploaded));
            StringAssert.DoesNotContain("raw-token", json);
            StringAssert.DoesNotContain("raw-token", File.ReadAllText(store.MainPath));
            Assert.IsFalse(uploaded.purchaseIdsMigrated, "올리는 파일은 이미 바꾼 모양");
            Assert.IsTrue(uploaded.purchases.ledger.All(e => e.transactionId == IapLedger.IdOf("raw-token")));
            StringAssert.DoesNotContain("purchaseIdsMigrated", json, "표시는 저장하지 않는다");
        }

        [Test]
        public void SaveLoad_MigratesRawTokenIdsToHashes()
        {
            var d = new SaveData();
            d.purchases.ledger.Add(new PurchaseEntry { transactionId = "raw-token", productId = "gems_t1", paidGems = 100 });
            Assert.AreEqual(SaveStore.ParseResult.Ok, SaveStore.Parse(SaveStore.Serialize(d), out SaveData loaded, out _));
            Assert.AreEqual(IapLedger.HashedPrefix + IapLedger.Hash("raw-token"), loaded.purchases.ledger[0].transactionId);
            Assert.IsNotNull(IapLedger.Find(loaded, "raw-token"), "상점이 원문 토큰으로 다시 보내도 찾는다");
        }

        [Test]
        public void Hash_MatchesServer()
        {
            // 서버 PurchaseVerifierTests.Hash_IsLowercaseSha256Hex와 같은 기대값 (토큰 해시 · 계정 표시가 양쪽에서 같아야 한다)
            Assert.AreEqual("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", IapLedger.Hash("abc"));
        }

        [Test]
        public void PackageName_MatchesAndroidIdentifier()
        {
            // 서버가 구글에 묻는 패키지 이름(GooglePlayClient.PackageName)이 이 게임의 안드로이드 ID와 같아야 한다 (다르면 모든 결제가 404)
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "../../Server/CloudCode/HistoryHeroes.Server/HistoryHeroesServer/GooglePlayClient.cs"));
            Match m = Regex.Match(source, "PackageName = \"([^\"]+)\"");
            Assert.IsTrue(m.Success);
            Assert.AreEqual(PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android), m.Groups[1].Value);
        }

        [Test]
        public void AskRetry_BacksOff()
        {
            CollectionAssert.AreEqual(new[] { 5, 10, 20, 40, 80, 160, 300, 300 }, new[] { 1, 2, 3, 4, 5, 6, 7, 20 }.Select(IapService.AskRetrySeconds));
        }
    }
}
