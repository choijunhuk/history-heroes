using HistoryHeroes.Economy;
using HistoryHeroes.Server;
using Newtonsoft.Json;

namespace HistoryHeroesServer.Tests;

/// <summary>서버 결제 검증 · 지급 판정 · 확정 · 환불 정리 · 계정 삭제 보관 (가짜 구글 + 가짜 Cloud Save)</summary>
public sealed class PurchaseVerifierTests
{
    private FakeStore _store = null!;
    private FakeGoogle _google = null!;
    private PurchaseVerifier _verifier = null!;
    private DateTime _now;
    private readonly FakeContext _player = new();

    [SetUp]
    public void SetUp()
    {
        _store = new FakeStore();
        _google = new FakeGoogle();
        _now = Make.Now;
        _verifier = Make.Verifier(_store, _google, () => _now);
    }

    private Task<VerifyResult> Verify(string productId, string token) => _verifier.VerifyAsync(_player, productId, token);

    private PurchaseRecord Stored(string token, string player = Make.Player) => _store.Get(player)!.Single(p => p.TokenHash == PurchaseVerifier.Hash(token));

    // ------------------------------------------------------------------ 지급

    [Test]
    public async Task Valid_GrantsFromServerCatalog_WithFirstBuyDouble()
    {
        VerifyResult r = await Verify("gems_t1", "tok-1");
        Assert.That(r.Status, Is.EqualTo(VerifyResult.Granted));
        PurchaseRecord p = r.Purchase!;
        Assert.That((p.PaidGems, p.FreeGems, p.FirstBonus, p.Test), Is.EqualTo((100, 100, true, false)));
        Assert.That(p.TokenHash, Is.EqualTo(PurchaseVerifier.Hash("tok-1")));
        Assert.That(p.OrderId, Is.EqualTo("GPA.0000-0000-0000-00000"));
        Assert.That(p.Utc, Is.EqualTo(Make.Now.ToString("o")));
        Assert.That(_store.Saved!, Has.Count.EqualTo(1));
        Assert.That(JsonConvert.SerializeObject(_store.Saved), Does.Not.Contain("tok-1"), "확정이 끝나면 토큰 원문은 장부에 없다");
        Assert.That(_store.Index[PurchaseVerifier.Hash("tok-1")], Is.EqualTo(Make.Player), "환불 목록으로 찾을 색인");
    }

    [Test]
    public async Task StarterPack_GrantsWholePack()
    {
        VerifyResult r = await Verify(IapCatalog.StarterPack, "s-1");
        PurchaseRecord p = r.Purchase!;
        Assert.That((r.Status, p.PaidGems, p.FreeGems, p.Tickets, p.Gold, p.FirstBonus), Is.EqualTo((VerifyResult.Granted, 100, 200, 2, 10000, false)));
    }

    [Test]
    public async Task TestPurchase_IsValid_MarkedTest()
    {
        _google.Next.Found!.PurchaseType = 0;
        VerifyResult r = await Verify("gems_t2", "t-1");
        Assert.That(r.Status, Is.EqualTo(VerifyResult.Granted));
        Assert.That(r.Purchase!.Test, Is.True);
        Assert.That(r.Purchase.PaidGems, Is.EqualTo(500), "테스트 결제도 지급은 같다");
    }

    [TestCase(1, "not_paid_promo")]
    [TestCase(2, "not_paid_rewarded")]
    public async Task PromoOrRewarded_NotPaid_NoGrant(int purchaseType, string reason)
    {
        _google.Next.Found!.PurchaseType = purchaseType;
        VerifyResult r = await Verify("gems_t2", "t-1");
        Assert.That((r.Status, r.Reason), Is.EqualTo((VerifyResult.Invalid, reason)));
        Assert.That(_store.Saved, Is.Empty);
        Assert.That(_google.Acks, Is.Empty);
    }

    [TestCase(1, VerifyResult.Invalid, "state_1")]
    [TestCase(2, VerifyResult.Retry, "pending")]
    [TestCase(null, VerifyResult.Retry, "state_missing")]
    public async Task NotPurchasedState_NoGrant(int? state, string status, string reason)
    {
        _google.Next.Found!.PurchaseState = state;
        VerifyResult r = await Verify("gems_t1", "tok-1");
        Assert.That((r.Status, r.Reason), Is.EqualTo((status, reason)));
        Assert.That(r.Purchase, Is.Null);
        Assert.That(_store.Saved, Is.Empty);
    }

    [Test]
    public async Task WrongProduct_FromGoogle_Invalid()
    {
        _google.Next.Found!.ProductId = "gems_t6";
        VerifyResult r = await Verify("gems_t1", "tok-1");
        Assert.That((r.Status, r.Reason), Is.EqualTo((VerifyResult.Invalid, "product_mismatch")));
        Assert.That(_store.Saved, Is.Empty);
    }

    [Test]
    public async Task GoogleRejectsToken_WrongPackageOrFake_Invalid()
    {
        // 다른 앱(패키지)의 토큰 · 가짜 토큰은 구글이 400/404로 답한다 (GooglePlayClient.KindOf)
        _google.Next = GoogleCheck.Of(GoogleCheck.Kind.Invalid, "google_404:purchaseTokenNotFound");
        VerifyResult r = await Verify("gems_t1", "fake");
        Assert.That((r.Status, r.Reason), Is.EqualTo((VerifyResult.Invalid, "google_404:purchaseTokenNotFound")));
        Assert.That(_store.Saved, Is.Empty);
        Assert.That(_store.Index, Is.Empty);
    }

    [Test]
    public async Task GoogleUnavailable_Retry()
    {
        _google.Next = GoogleCheck.Of(GoogleCheck.Kind.Retry, "google_503");
        Assert.That((await Verify("gems_t1", "tok-1")).Status, Is.EqualTo(VerifyResult.Retry));
        Assert.That(_store.Saved, Is.Empty);
    }

    [Test]
    public async Task OtherPlayersToken_Invalid()
    {
        _google.Next.Found!.ObfuscatedAccountId = PurchaseVerifier.Hash("someone-else");
        VerifyResult r = await Verify("gems_t1", "tok-1");
        Assert.That((r.Status, r.Reason), Is.EqualTo((VerifyResult.Invalid, "account_mismatch")));

        _google.Next.Found!.ObfuscatedAccountId = null;
        r = await Verify("gems_t1", "tok-1");
        Assert.That(r.Reason, Is.EqualTo("account_mismatch"), "계정 표시 없이 결제한 토큰도 받지 않는다");
        Assert.That(_store.Saved, Is.Empty);
    }

    [Test]
    public async Task PlayerChange_SecondPlayerCannotTakeFirstPlayersPurchase()
    {
        // 1번 플레이어가 산 결제(계정 표시 = 1번)를 2번 플레이어가 들고 와도 2번 장부에 들어가지 않고, 1번 장부 · 색인은 그대로
        Assert.That((await Verify("gems_t3", "tok-1")).Status, Is.EqualTo(VerifyResult.Granted));
        var second = new FakeContext("player-2");
        VerifyResult r = await _verifier.VerifyAsync(second, "gems_t3", "tok-1");
        Assert.That((r.Status, r.Reason), Is.EqualTo((VerifyResult.Invalid, "account_mismatch")));
        Assert.That(_store.Get("player-2"), Is.Empty);
        Assert.That(_store.Saved!, Has.Count.EqualTo(1));
        Assert.That(_store.Index[PurchaseVerifier.Hash("tok-1")], Is.EqualTo(Make.Player));
    }

    [Test]
    public async Task MultiQuantity_Invalid()
    {
        _google.Next.Found!.Quantity = 3;
        Assert.That((await Verify("gems_t1", "tok-1")).Reason, Is.EqualTo("quantity"));
    }

    [TestCase("gems_t9", "tok")]
    [TestCase("", "tok")]
    [TestCase("gems_t1", "")]
    public async Task BadInput_InvalidWithoutAskingGoogle(string productId, string token)
    {
        Assert.That((await Verify(productId, token)).Status, Is.EqualTo(VerifyResult.Invalid));
        Assert.That(_google.Calls, Is.Zero);
    }

    [Test]
    public async Task NoPlayer_Invalid()
    {
        VerifyResult r = await _verifier.VerifyAsync(new FakeContext(null), "gems_t1", "tok");
        Assert.That((r.Status, r.Reason), Is.EqualTo((VerifyResult.Invalid, "no_player")));
    }

    [Test]
    public async Task DuplicateToken_Already_SameRecord_NoSecondGrant()
    {
        VerifyResult first = await Verify("gems_t3", "tok-1");
        VerifyResult again = await Verify("gems_t3", "tok-1");
        Assert.That(again.Status, Is.EqualTo(VerifyResult.Already));
        Assert.That(again.Purchase!.FreeGems, Is.EqualTo(first.Purchase!.FreeGems), "처음 지급 내역 그대로");
        Assert.That(_store.Saved!, Has.Count.EqualTo(1));
        Assert.That(_google.Calls, Is.EqualTo(1), "이미 지급한 토큰은 구글에 다시 묻지 않는다");
    }

    [Test]
    public async Task StarterTwice_SecondAlreadyClaimed_NoGrant()
    {
        Assert.That((await Verify(IapCatalog.StarterPack, "s-1")).Status, Is.EqualTo(VerifyResult.Granted));
        VerifyResult second = await Verify(IapCatalog.StarterPack, "s-2");
        Assert.That(second.Status, Is.EqualTo(VerifyResult.AlreadyClaimed));
        Assert.That(second.Purchase, Is.Null);
        Assert.That(_store.Saved!, Has.Count.EqualTo(1));
        Assert.That((await Verify(IapCatalog.StarterPack, "s-1")).Status, Is.EqualTo(VerifyResult.Already), "처음 토큰은 여전히 already");
    }

    [Test]
    public async Task FirstBuyDouble_OncePerProduct()
    {
        Assert.That((await Verify("gems_t1", "a")).Purchase!.FreeGems, Is.EqualTo(100));
        PurchaseRecord second = (await Verify("gems_t1", "b")).Purchase!;
        Assert.That((second.FreeGems, second.FirstBonus), Is.EqualTo((0, false)));
        PurchaseRecord other = (await Verify("gems_t2", "c")).Purchase!;
        Assert.That((other.FreeGems, other.FirstBonus), Is.EqualTo((550, true)), "다른 상품의 2배는 따로 남아 있다");
    }

    // ------------------------------------------------------------------ 쓰기 충돌 · 장부 만들기 (R3)

    [Test]
    public async Task ConcurrentWrite_Conflict_RereadsAndDecidesAgain()
    {
        // 우리가 읽은 뒤 쓰기 전에 다른 기기의 같은 상품 결제가 첫 구매 2배를 먼저 가져갔다
        _store.Put(new List<PurchaseRecord>());
        bool injected = false;
        _store.BeforeSave = s =>
        {
            if (injected) return false;
            injected = true;
            s.Put(new List<PurchaseRecord> { new() { TokenHash = PurchaseVerifier.Hash("other"), ProductId = "gems_t1", FirstBonus = true, PaidGems = 100, FreeGems = 100 } });
            return false; // lock이 바뀌어 우리 쓰기는 충돌
        };

        VerifyResult r = await Verify("gems_t1", "mine");
        Assert.That(r.Status, Is.EqualTo(VerifyResult.Granted));
        Assert.That((r.Purchase!.FreeGems, r.Purchase.FirstBonus), Is.EqualTo((0, false)), "2배는 먼저 쓴 쪽 것");
        Assert.That(_store.Saved!.Select(p => p.TokenHash), Is.EquivalentTo(new[] { PurchaseVerifier.Hash("other"), PurchaseVerifier.Hash("mine") }));
    }

    [Test]
    public async Task ConcurrentStarter_OnTwoCalls_OnlyOneGranted()
    {
        _store.Put(new List<PurchaseRecord>());
        bool injected = false;
        _store.BeforeSave = s =>
        {
            if (injected) return false;
            injected = true;
            s.Put(new List<PurchaseRecord> { new() { TokenHash = PurchaseVerifier.Hash("s-other"), ProductId = IapCatalog.StarterPack } });
            return false;
        };

        Assert.That((await Verify(IapCatalog.StarterPack, "s-mine")).Status, Is.EqualTo(VerifyResult.AlreadyClaimed));
        Assert.That(_store.Saved!, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task ConflictEveryTime_RetryBusy_NothingWritten()
    {
        _store.Put(new List<PurchaseRecord>());
        _store.BeforeSave = _ => true;
        VerifyResult r = await Verify("gems_t1", "tok");
        Assert.That((r.Status, r.Reason), Is.EqualTo((VerifyResult.Retry, "busy")));
        Assert.That(_store.Saved, Is.Empty);
    }

    [Test]
    public async Task MissingLedger_CreatesEmptyThenLockedAppend_NeverWritesPurchaseWithoutLock()
    {
        VerifyResult r = await Verify("gems_t1", "tok");
        Assert.That(r.Status, Is.EqualTo(VerifyResult.Granted));
        Assert.That(_store.Writes[0], Is.EqualTo((Make.Player, 0, false)), "빈 장부만 lock 없이 만든다");
        Assert.That(_store.Writes.Skip(1).All(w => w.locked), Is.True, "결제가 든 쓰기는 모두 lock을 붙인다");
        Assert.That(_store.Writes.Any(w => w.count > 0 && !w.locked), Is.False);
    }

    [Test]
    public async Task EmptyCreationOverwrittenBeforeAppend_AppendConflicts_ThenSucceeds()
    {
        // 빈 장부를 만든 뒤 다른 호출이 빈 장부를 또 썼다 (lock이 바뀜) → 우리 지급 쓰기는 충돌 → 다시 읽고 lock으로 더한다
        bool injected = false;
        _store.BeforeSave = s =>
        {
            if (injected || s.Saved == null) return false;
            injected = true;
            s.Put(new List<PurchaseRecord>());
            return false;
        };
        Assert.That((await Verify("gems_t1", "tok")).Status, Is.EqualTo(VerifyResult.Granted));
        Assert.That(_store.Saved!, Has.Count.EqualTo(1));
        Assert.That(_store.Writes.Any(w => w.count > 0 && !w.locked), Is.False);
    }

    [Test]
    public async Task GetPurchases_CreatesEmptyLedgerWhenMissing()
    {
        PurchaseList list = await _verifier.GetPurchasesAsync(_player);
        Assert.That((list.Status, list.Configured, list.Purchases.Count), Is.EqualTo((PurchaseList.Ok, true, 0)));
        Assert.That(_store.Saved, Is.Empty, "장부 키가 생겼다 (결제 때는 lock이 있다)");
        Assert.That(_store.Writes, Is.EqualTo(new[] { (Make.Player, 0, false) }));
    }

    // ------------------------------------------------------------------ 구글 확정 (R1)

    [Test]
    public async Task Grant_AcknowledgesAfterDurableWrite()
    {
        VerifyResult r = await Verify("gems_t1", "tok-1");
        Assert.That(_google.Acks, Is.EqualTo(new[] { "tok-1" }));
        PurchaseRecord stored = Stored("tok-1");
        Assert.That((stored.Acknowledged, stored.AckToken), Is.EqualTo((true, (string?)null)));
        Assert.That((r.Purchase!.Acknowledged, r.Purchase.AckToken), Is.EqualTo((true, (string?)null)));
    }

    [Test]
    public async Task AlreadySettledAtGoogle_NoAckCall()
    {
        _google.Next.Found!.ConsumptionState = 1; // 게임이 먼저 확정(소비)했다
        await Verify("gems_t1", "tok-1");
        Assert.That(_google.Acks, Is.Empty);
        Assert.That(Stored("tok-1").Acknowledged, Is.True);
    }

    [Test]
    public async Task AckFails_StillGranted_TokenKeptServerSideOnly_GetPurchasesRetries()
    {
        _google.AckResult = false;
        VerifyResult r = await Verify("gems_t1", "tok-1");
        Assert.That(r.Status, Is.EqualTo(VerifyResult.Granted), "지급은 이미 장부에 남았다");
        Assert.That(r.Purchase!.AckToken, Is.Null, "게임에는 토큰을 보내지 않는다");
        Assert.That((Stored("tok-1").Acknowledged, Stored("tok-1").AckToken), Is.EqualTo((false, "tok-1")));

        _google.AckResult = true;
        PurchaseList list = await _verifier.GetPurchasesAsync(_player);
        Assert.That(_google.Acks, Is.EqualTo(new[] { "tok-1", "tok-1" }));
        Assert.That((Stored("tok-1").Acknowledged, Stored("tok-1").AckToken), Is.EqualTo((true, (string?)null)));
        Assert.That(JsonConvert.SerializeObject(list), Does.Not.Contain("ackToken").And.Not.Contain("tok-1"));
    }

    [Test]
    public async Task AckFails_RetriedOnAlready_NotForever()
    {
        _google.AckResult = false;
        await Verify("gems_t1", "tok-1");
        _google.AckResult = true;
        VerifyResult again = await Verify("gems_t1", "tok-1");
        Assert.That(again.Status, Is.EqualTo(VerifyResult.Already));
        Assert.That(Stored("tok-1").Acknowledged, Is.True);
        await Verify("gems_t1", "tok-1");
        await _verifier.GetPurchasesAsync(_player);
        Assert.That(_google.Acks, Has.Count.EqualTo(2), "확정된 뒤로는 다시 하지 않는다");
    }

    [Test]
    public async Task AckRetry_GivesUpAfterThreeDays_DropsToken()
    {
        _google.AckResult = false;
        await Verify("gems_t1", "tok-1");
        _now = Make.Now.AddDays(3).AddMinutes(1);
        await _verifier.GetPurchasesAsync(_player);
        Assert.That(_google.Acks, Has.Count.EqualTo(1), "3일이 지나면 구글이 이미 환불했다 — 더 확정하지 않는다");
        Assert.That((Stored("tok-1").Acknowledged, Stored("tok-1").AckToken), Is.EqualTo((false, (string?)null)));
    }

    // ------------------------------------------------------------------ 환불 정리 (R2)

    private static VoidedPurchase Void(string token, DateTime at) => new() { PurchaseToken = token, OrderId = "GPA.0000-0000-0000-00000", VoidedTimeMillis = Make.Ms(at) };

    [Test]
    public async Task Voided_MarkedByJob_AlreadyBecomesVoided_ListReturnsVoided_NeverRegranted()
    {
        await Verify("gems_t2", "tok-1");
        _google.Voided = new List<VoidedPurchase> { Void("tok-1", Make.Now.AddHours(1)) };
        _now = Make.Now.AddHours(2);

        PurchaseList list = await _verifier.GetPurchasesAsync(_player);
        Assert.That(list.Purchases.Single().Voided, Is.True, "게임이 회수하도록 voided로 돌려준다");
        Assert.That(Stored("tok-1").VoidedUtc, Is.EqualTo(_now.ToString("o")));

        VerifyResult again = await Verify("gems_t2", "tok-1");
        Assert.That((again.Status, again.Purchase!.Voided), Is.EqualTo((VerifyResult.Voided, true)));
        Assert.That(_store.Saved!, Has.Count.EqualTo(1), "환불된 결제는 다시 지급하지 않는다");
        Assert.That((await Verify(IapCatalog.StarterPack, "s-1")).Status, Is.EqualTo(VerifyResult.Granted));
    }

    [Test]
    public async Task Voided_FindsOtherPlayersLedgerThroughIndex()
    {
        var other = new FakeContext("player-2");
        _google.Next = new GoogleCheck { Result = GoogleCheck.Kind.Ok, Found = FakeGoogle.Paid("player-2") };
        await _verifier.VerifyAsync(other, "gems_t4", "tok-2");
        _google.Voided = new List<VoidedPurchase> { Void("tok-2", Make.Now), Void("unknown-token", Make.Now) };

        await _verifier.GetPurchasesAsync(_player); // 1번 플레이어의 호출이 게임 전체 정리를 돌린다
        Assert.That(Stored("tok-2", "player-2").Voided, Is.True);
        Assert.That(_store.Saved, Is.Empty, "1번 장부는 그대로");
    }

    [Test]
    public async Task VoidedJob_RunsAtMostEvery10Minutes_AndAdvancesCheckpoint()
    {
        _google.Voided = new List<VoidedPurchase> { Void("x", Make.Now.AddMinutes(-30)) };
        await _verifier.GetPurchasesAsync(_player);
        Assert.That(_google.VoidedStarts, Is.EqualTo(new[] { Make.Ms(Make.Now.AddDays(-29)) }), "처음은 구글이 받는 가장 오래된 구간부터");
        Assert.That(_store.Job!.VoidedCheckpointMs, Is.EqualTo(Make.Ms(Make.Now.AddMinutes(-30))));

        _now = Make.Now.AddMinutes(5);
        await _verifier.GetPurchasesAsync(_player);
        Assert.That(_google.VoidedStarts, Has.Count.EqualTo(1), "10분 안에는 다시 묻지 않는다 (API 한도)");

        _now = Make.Now.AddMinutes(11);
        await _verifier.GetPurchasesAsync(_player);
        Assert.That(_google.VoidedStarts[1], Is.EqualTo(Make.Ms(Make.Now.AddMinutes(-90))), "지난번 본 곳보다 1시간 앞부터");
    }

    [Test]
    public async Task VoidedJob_NotConfigured_KeepsCheckpoint()
    {
        _google.Configured = false;
        await _verifier.GetPurchasesAsync(_player);
        Assert.That(_store.Job!.VoidedCheckpointMs, Is.Zero);
    }

    // ------------------------------------------------------------------ 계정 삭제 · 보관 (Y2)

    [Test]
    public async Task ArchiveAndDelete_MovesLedgerToArchive_DeletesLedgerAndIndex()
    {
        _google.AckResult = false; // 확정 전 토큰이 남아 있어도 보관본에는 들어가지 않는다
        await Verify("gems_t1", "tok-1");
        await Verify(IapCatalog.StarterPack, "s-1");

        ArchiveResult r = await _verifier.ArchiveAndDeleteAsync(_player);
        Assert.That((r.Status, r.Archived), Is.EqualTo((PurchaseList.Ok, 2)));
        Assert.That(_store.Saved, Is.Null, "장부 키를 지웠다");
        Assert.That(_store.Index, Is.Empty);
        ArchivedPlayer archived = _store.Archives["202611"].Single();
        Assert.That(archived.Id, Is.EqualTo(PurchaseVerifier.ArchiveId(Make.Player)).And.Not.Contain(Make.Player));
        Assert.That(DateTime.Parse(archived.DeleteAfterUtc).ToUniversalTime(), Is.EqualTo(Make.Now.AddYears(5)));
        Assert.That(archived.Purchases.Select(p => p.ProductId), Is.EquivalentTo(new[] { "gems_t1", IapCatalog.StarterPack }));
        Assert.That(JsonConvert.SerializeObject(_store.Archives), Does.Not.Contain("tok-1"));

        Assert.That((await _verifier.ArchiveAndDeleteAsync(_player)).Archived, Is.Zero, "다시 불러도 할 일 없음");
    }

    [Test]
    public async Task ArchivePurge_DeletesMonthOnlyAfterFiveYears()
    {
        await Verify("gems_t1", "tok-1");
        await _verifier.ArchiveAndDeleteAsync(_player);

        _now = new DateTime(2031, 11, 30, 0, 0, 0, DateTimeKind.Utc);
        await _verifier.GetPurchasesAsync(_player);
        Assert.That(_store.Archives.ContainsKey("202611"), Is.True, "2026-11 보관본은 2031-12-01 전에는 남는다");

        _now = new DateTime(2031, 12, 1, 0, 1, 0, DateTimeKind.Utc);
        await _verifier.GetPurchasesAsync(_player);
        Assert.That(_store.DeletedMonths, Is.EqualTo(new[] { "202611" }));
        Assert.That(_store.Job!.PurgedThrough, Is.EqualTo("202611"));
    }

    // ------------------------------------------------------------------ 오류 · 시간 한도

    [Test]
    public async Task NotConfigured_NoGrant()
    {
        _google.Configured = false;
        Assert.That((await Verify("gems_t1", "tok")).Status, Is.EqualTo(VerifyResult.NotConfigured));
        Assert.That(_store.Saved, Is.Empty);
        Assert.That((await _verifier.GetPurchasesAsync(_player)).Configured, Is.False);
    }

    [Test]
    public async Task StoreError_Retry_AndGetPurchasesCleanRetry()
    {
        _store.LoadError = new HttpRequestException("down");
        Assert.That((await Verify("gems_t1", "tok")).Status, Is.EqualTo(VerifyResult.Retry));
        PurchaseList list = await _verifier.GetPurchasesAsync(_player);
        Assert.That((list.Status, list.Purchases.Count), Is.EqualTo((VerifyResult.Retry, 0)));
    }

    [Test]
    public async Task Budget_HangingStore_RetryTimeout()
    {
        _store.Hang = true;
        _verifier = Make.Verifier(_store, _google, () => _now, TimeSpan.FromMilliseconds(200));
        VerifyResult r = await Verify("gems_t1", "tok");
        Assert.That((r.Status, r.Reason), Is.EqualTo((VerifyResult.Retry, "timeout")));
        Assert.That((await _verifier.GetPurchasesAsync(_player)).Status, Is.EqualTo(VerifyResult.Retry));
    }

    [Test]
    public async Task GetPurchases_ReturnsLedgerAndConfigured()
    {
        await Verify("gems_t1", "a");
        await Verify(IapCatalog.StarterPack, "b");
        PurchaseList list = await _verifier.GetPurchasesAsync(_player);
        Assert.That(list.Configured, Is.True);
        Assert.That(list.Purchases.Select(p => p.ProductId), Is.EqualTo(new[] { "gems_t1", IapCatalog.StarterPack }));
    }

    [Test]
    public void Hash_IsLowercaseSha256Hex()
    {
        // 게임 쪽 IapLedger.Hash와 같은 값이어야 한다 (EditMode 시험이 같은 기대값을 본다)
        Assert.That(PurchaseVerifier.Hash("abc"), Is.EqualTo("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad"));
    }
}
