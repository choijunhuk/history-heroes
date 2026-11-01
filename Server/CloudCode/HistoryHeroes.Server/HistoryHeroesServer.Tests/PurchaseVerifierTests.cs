using HistoryHeroes.Economy;
using HistoryHeroes.Server;
using Newtonsoft.Json;

namespace HistoryHeroesServer.Tests;

/// <summary>서버 결제 검증 · 지급 판정(머리 lock) · 확정 · 환불 정리 · 계정 닫기 · 정리 작업 (가짜 구글 + 가짜 Cloud Save)</summary>
public sealed class PurchaseVerifierTests
{
    private FakeStore _store = null!;
    private FakeGoogle _google = null!;
    private PurchaseVerifier _verifier = null!;
    private DateTime _now;
    private readonly FakeContext _player = new();
    private readonly FakeContext _scheduler = new(null);

    [SetUp]
    public void SetUp()
    {
        _store = new FakeStore();
        _google = new FakeGoogle();
        _now = Make.Now;
        _verifier = Make.Verifier(_store, _google, () => _now);
    }

    private Task<VerifyResult> Verify(string productId, string token) => _verifier.VerifyAsync(_player, productId, token);

    private PurchaseRecord Stored(string token, string player = Make.Player) => _store.Purchases(player).Single(p => p.TokenHash == PurchaseVerifier.Hash(token));

    // ------------------------------------------------------------------ 지급

    [Test]
    public async Task Valid_GrantsFromServerCatalog_WithFirstBuyDouble()
    {
        VerifyResult r = await Verify("gems_t1", "tok-1");
        Assert.That(r.Status, Is.EqualTo(VerifyResult.Granted));
        PurchaseRecord p = r.Purchase!;
        Assert.That((p.PaidGems, p.FreeGems, p.FirstBonus, p.Test, p.Acknowledged), Is.EqualTo((100, 100, true, false, true)));
        Assert.That(p.TokenHash, Is.EqualTo(PurchaseVerifier.Hash("tok-1")));
        Assert.That(p.Utc, Is.EqualTo(Make.Now.ToString("o")));
        Assert.That(_store.Purchases(), Has.Count.EqualTo(1));
        Assert.That(_store.IndexOf(PurchaseVerifier.Hash("tok-1"))!.Player, Is.EqualTo(Make.Player), "게임 전체 색인");
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
        Assert.That((r.Status, r.Purchase!.Test, r.Purchase.PaidGems), Is.EqualTo((VerifyResult.Granted, true, 500)));
    }

    [TestCase(1, "not_paid_promo")]
    [TestCase(2, "not_paid_rewarded")]
    public async Task PromoOrRewarded_NotPaid_NoGrant(int purchaseType, string reason)
    {
        _google.Next.Found!.PurchaseType = purchaseType;
        VerifyResult r = await Verify("gems_t2", "t-1");
        Assert.That((r.Status, r.Reason), Is.EqualTo((VerifyResult.Invalid, reason)));
        Assert.That(_store.Purchases(), Is.Empty);
        Assert.That((_google.Acks.Count, _store.Acks.Count), Is.EqualTo((0, 0)));
    }

    [TestCase(1, VerifyResult.Invalid, "state_1")]
    [TestCase(2, VerifyResult.Retry, "pending")]
    [TestCase(null, VerifyResult.Retry, "state_missing")]
    public async Task NotPurchasedState_NoGrant(int? state, string status, string reason)
    {
        _google.Next.Found!.PurchaseState = state;
        VerifyResult r = await Verify("gems_t1", "tok-1");
        Assert.That((r.Status, r.Reason, r.Purchase), Is.EqualTo((status, reason, (PurchaseRecord?)null)));
        Assert.That(_store.Purchases(), Is.Empty);
    }

    [Test]
    public async Task WrongProduct_FromGoogle_Invalid()
    {
        _google.Next.Found!.ProductId = "gems_t6";
        Assert.That((await Verify("gems_t1", "tok-1")).Reason, Is.EqualTo("product_mismatch"));
        Assert.That(_store.Purchases(), Is.Empty);
    }

    [Test]
    public async Task GoogleRejectsToken_WrongPackageOrFake_Invalid()
    {
        _google.Next = GoogleCheck.Of(GoogleCheck.Kind.Invalid, "google_404:purchaseTokenNotFound");
        VerifyResult r = await Verify("gems_t1", "fake");
        Assert.That((r.Status, r.Reason), Is.EqualTo((VerifyResult.Invalid, "google_404:purchaseTokenNotFound")));
        Assert.That((_store.Purchases().Count, _store.Index.Count), Is.EqualTo((0, 0)));
    }

    [Test]
    public async Task GoogleUnavailable_Retry()
    {
        _google.Next = GoogleCheck.Of(GoogleCheck.Kind.Retry, "google_503");
        Assert.That((await Verify("gems_t1", "tok-1")).Status, Is.EqualTo(VerifyResult.Retry));
        Assert.That(_store.Purchases(), Is.Empty);
    }

    [Test]
    public async Task OtherPlayersPurchase_AccountMismatch()
    {
        _google.Next.Found!.ObfuscatedAccountId = PurchaseVerifier.Hash("someone-else");
        Assert.That((await Verify("gems_t1", "tok-1")).Reason, Is.EqualTo("account_mismatch"));
        _google.Next.Found!.ObfuscatedAccountId = null;
        Assert.That((await Verify("gems_t1", "tok-1")).Reason, Is.EqualTo("account_mismatch"), "계정 표시 없이 결제한 토큰도 받지 않는다");
        Assert.That(_store.Purchases(), Is.Empty);
    }

    [Test]
    public async Task TokenAlreadyInGameIndexForAnotherPlayer_NeverGranted_NoGoogleCall()
    {
        Assert.That((await Verify("gems_t3", "tok-1")).Status, Is.EqualTo(VerifyResult.Granted));
        _google.Next = new GoogleCheck { Result = GoogleCheck.Kind.Ok, Found = FakeGoogle.Paid("player-2") }; // 구글 쪽이 통과시켜도
        int calls = _google.Calls;
        VerifyResult r = await _verifier.VerifyAsync(new FakeContext("player-2"), "gems_t3", "tok-1");
        Assert.That((r.Status, r.Reason), Is.EqualTo((VerifyResult.Invalid, "token_used")));
        Assert.That(_google.Calls, Is.EqualTo(calls));
        Assert.That(_store.Purchases("player-2"), Is.Empty);
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
        Assert.That((await _verifier.VerifyAsync(new FakeContext(null), "gems_t1", "tok")).Reason, Is.EqualTo("no_player"));
    }

    [Test]
    public async Task DuplicateToken_Already_SameRecord_NoSecondGrant()
    {
        VerifyResult first = await Verify("gems_t3", "tok-1");
        VerifyResult again = await Verify("gems_t3", "tok-1");
        Assert.That(again.Status, Is.EqualTo(VerifyResult.Already));
        Assert.That(again.Purchase!.FreeGems, Is.EqualTo(first.Purchase!.FreeGems));
        Assert.That(_store.Purchases(), Has.Count.EqualTo(1));
        Assert.That(_google.Calls, Is.EqualTo(1), "이미 지급한 토큰은 구글에 다시 묻지 않는다");
    }

    [Test]
    public async Task StarterTwice_SecondAlreadyClaimed_NoGrant_NoAck()
    {
        Assert.That((await Verify(IapCatalog.StarterPack, "s-1")).Status, Is.EqualTo(VerifyResult.Granted));
        _google.Acks.Clear();
        VerifyResult second = await Verify(IapCatalog.StarterPack, "s-2");
        Assert.That((second.Status, second.Purchase), Is.EqualTo((VerifyResult.AlreadyClaimed, (PurchaseRecord?)null)));
        Assert.That(_store.Purchases(), Has.Count.EqualTo(1));
        Assert.That((_google.Acks.Count, _store.Acks.Count), Is.EqualTo((0, 0)), "확정하지 않는다 → 구글이 환불 (토큰도 안 남김)");
    }

    [Test]
    public async Task FirstBuyDouble_OncePerProduct()
    {
        Assert.That((await Verify("gems_t1", "a")).Purchase!.FreeGems, Is.EqualTo(100));
        PurchaseRecord second = (await Verify("gems_t1", "b")).Purchase!;
        Assert.That((second.FreeGems, second.FirstBonus), Is.EqualTo((0, false)));
        PurchaseRecord other = (await Verify("gems_t2", "c")).Purchase!;
        Assert.That((other.FreeGems, other.FirstBonus), Is.EqualTo((550, true)));
    }

    // ------------------------------------------------------------------ 머리 lock으로 판정 한 줄 세우기 (2차 B)

    [Test]
    public async Task ConcurrentFirstPurchase_TwoTokens_OtherWinsFirstBonus()
    {
        // 우리가 머리를 읽은 뒤 다른 호출이 같은 상품의 첫 구매 2배를 먼저 더했다 (머리 lock이 바뀜) → 우리 batch는 통째로 거절 → 다시 판정
        bool injected = false;
        _store.BeforeAdd = s =>
        {
            if (injected) return false;
            injected = true;
            s.AddDirect(Make.Record("other", "gems_t1", firstBonus: true));
            return false;
        };

        VerifyResult r = await Verify("gems_t1", "mine");
        Assert.That(r.Status, Is.EqualTo(VerifyResult.Granted));
        Assert.That((r.Purchase!.FreeGems, r.Purchase.FirstBonus), Is.EqualTo((0, false)), "2배는 먼저 쓴 쪽 것");
        Assert.That(_store.Purchases().Select(p => p.TokenHash), Is.EquivalentTo(new[] { PurchaseVerifier.Hash("other"), PurchaseVerifier.Hash("mine") }));
    }

    [Test]
    public async Task ConcurrentStarterTwice_TwoTokens_OnlyOneGranted()
    {
        bool injected = false;
        _store.BeforeAdd = s =>
        {
            if (injected) return false;
            injected = true;
            s.AddDirect(Make.Record("s-other", IapCatalog.StarterPack));
            return false;
        };

        Assert.That((await Verify(IapCatalog.StarterPack, "s-mine")).Status, Is.EqualTo(VerifyResult.AlreadyClaimed));
        Assert.That(_store.Purchases().Select(p => p.ProductId), Is.EqualTo(new[] { IapCatalog.StarterPack }));
    }

    [Test]
    public async Task ConflictEveryTime_RetryBusy_NothingWritten()
    {
        _store.BeforeAdd = _ => true;
        VerifyResult r = await Verify("gems_t1", "tok");
        Assert.That((r.Status, r.Reason), Is.EqualTo((VerifyResult.Retry, "busy")));
        Assert.That(_store.Purchases(), Is.Empty);
    }

    [Test]
    public async Task EveryPurchaseWrite_IsGuardedByALock_OnlyConstantHeadIsWrittenWithout()
    {
        await Verify("gems_t1", "a");
        await Verify(IapCatalog.StarterPack, "b");
        await Verify("gems_t1", "a");
        Assert.That(_store.Writes.Where(w => !w.guarded).Select(w => w.key), Is.EqualTo(new[] { "iap_head" }), "lock 없이 쓴 건 처음 머리 하나");
        Assert.That(_store.Writes.Where(w => w.key.StartsWith("p_")).All(w => w.guarded), Is.True);
    }

    [Test]
    public async Task LateHeadRecreation_LosesNothing()
    {
        // 다른 호출이 늦게 머리를 처음 값으로 다시 만들었다 (lock 없이) — 결제 키는 그대로, 판정은 결제 키로 하니 2배를 두 번 주지 않는다
        await Verify("gems_t1", "a");
        _store.ResetHead();
        Assert.That(_store.Purchases(), Has.Count.EqualTo(1));
        PurchaseRecord second = (await Verify("gems_t1", "b")).Purchase!;
        Assert.That(second.FirstBonus, Is.False);

        bool reset = false;
        _store.BeforeAdd = s =>
        {
            if (reset) return false;
            reset = true;
            s.ResetHead(); // 판정 도중 머리가 바뀜 → 이번 batch는 거절, 다시 하면 통과
            return false;
        };
        Assert.That((await Verify("gems_t2", "c")).Status, Is.EqualTo(VerifyResult.Granted));
        Assert.That(_store.Purchases(), Has.Count.EqualTo(3));
    }

    // ------------------------------------------------------------------ 구글 확정 (R1 · 2차 F)

    [Test]
    public async Task Grant_AcknowledgesAfterDurableWrite_TokenNeverInProtectedData()
    {
        VerifyResult r = await Verify("gems_t1", "tok-1");
        Assert.That(_google.Acks, Is.EqualTo(new[] { "tok-1" }));
        Assert.That((Stored("tok-1").Acknowledged, r.Purchase!.Acknowledged), Is.EqualTo((true, true)));
        Assert.That(_store.Acks, Is.Empty, "확정이 끝나면 토큰 원문을 지운다");
        Assert.That(_store.ProtectedText(), Does.Not.Contain("tok-1"));
    }

    [Test]
    public async Task AlreadySettledAtGoogle_NoAckCall_NoTokenKept()
    {
        _google.Next.Found!.ConsumptionState = 1;
        await Verify("gems_t1", "tok-1");
        Assert.That((_google.Acks.Count, _store.Acks.Count, Stored("tok-1").Acknowledged), Is.EqualTo((0, 0, true)));
    }

    [Test]
    public async Task AckFails_StillGranted_TokenOnlyInServerOnlyStorage_GetPurchasesRetries()
    {
        _google.AckResult = false;
        VerifyResult r = await Verify("gems_t1", "tok-1");
        Assert.That(r.Status, Is.EqualTo(VerifyResult.Granted), "지급은 이미 장부에 남았다");
        Assert.That(Stored("tok-1").Acknowledged, Is.False);
        Assert.That(_store.ProtectedText(), Does.Not.Contain("tok-1"), "플레이어가 읽는 장부에는 토큰이 없다");
        Assert.That(JsonConvert.SerializeObject(r), Does.Not.Contain("tok-1"));
        Assert.That(_store.Acks.Values.Single(), Does.Contain("tok-1"), "서버 전용 확정 대기에만");

        _google.AckResult = true;
        PurchaseList list = await _verifier.GetPurchasesAsync(_player);
        Assert.That(_google.Acks, Is.EqualTo(new[] { "tok-1", "tok-1" }));
        Assert.That((Stored("tok-1").Acknowledged, _store.Acks.Count), Is.EqualTo((true, 0)));
        Assert.That(JsonConvert.SerializeObject(list), Does.Not.Contain("tok-1"));
    }

    [Test]
    public async Task AckFails_RetriedOnAlready_NotForever()
    {
        _google.AckResult = false;
        await Verify("gems_t1", "tok-1");
        _google.AckResult = true;
        Assert.That((await Verify("gems_t1", "tok-1")).Status, Is.EqualTo(VerifyResult.Already));
        Assert.That(Stored("tok-1").Acknowledged, Is.True);
        await Verify("gems_t1", "tok-1");
        await _verifier.GetPurchasesAsync(_player);
        await _verifier.RunMaintenanceAsync(_scheduler);
        Assert.That(_google.Acks, Has.Count.EqualTo(2), "확정된 뒤로는 다시 하지 않는다");
    }

    [Test]
    public async Task Maintenance_AcksPendingWithoutPlayerCalling_AndDropsOldOrOrphanTokens()
    {
        _google.AckResult = false;
        await Verify("gems_t1", "tok-1");                         // 확정 대기 (결제 기록 있음)
        _store.Acks[PurchaseVerifier.Hash("orphan")] = JsonConvert.SerializeObject(new PendingAck { Player = Make.Player, ProductId = "gems_t1", Token = "orphan", Utc = Make.Now.ToString("o") });

        _google.AckResult = true;
        _now = Make.Now.AddHours(2);
        MaintenanceResult m = await _verifier.RunMaintenanceAsync(_scheduler); // 플레이어가 접속하지 않아도
        Assert.That(m.Status, Is.EqualTo(PurchaseList.Ok));
        Assert.That(Stored("tok-1").Acknowledged, Is.True);
        Assert.That(_store.Acks, Is.Empty, "확정된 것 · 1시간 넘게 결제 기록이 없는 것 모두 지움");
        Assert.That(_google.Acks, Does.Not.Contain("orphan"), "지급하지 않은 결제는 확정하지 않는다 (구글 환불)");
    }

    [Test]
    public async Task Maintenance_DropsTokenAfterThreeDays_WithoutAck()
    {
        _google.AckResult = false;
        await Verify("gems_t1", "tok-1");
        _now = Make.Now.AddDays(3).AddMinutes(1);
        await _verifier.RunMaintenanceAsync(_scheduler);
        Assert.That(_google.Acks, Has.Count.EqualTo(1), "3일이 지나면 구글이 이미 환불했다 — 더 확정하지 않는다");
        Assert.That((_store.Acks.Count, Stored("tok-1").Acknowledged), Is.EqualTo((0, false)));
    }

    // ------------------------------------------------------------------ 환불 정리 (R2 · 2차 D)

    private static VoidedPurchase Void(string token, DateTime at) => new() { PurchaseToken = token, OrderId = "GPA.0000-0000-0000-00000", VoidedTimeMillis = Make.Ms(at) };

    [Test]
    public async Task Voided_MarkedByMaintenance_AlreadyBecomesVoided_ListReturnsVoided_NeverRegranted()
    {
        await Verify("gems_t2", "tok-1");
        _google.Voided = new List<VoidedPurchase> { Void("tok-1", Make.Now.AddHours(1)) };
        _now = Make.Now.AddHours(2);

        Assert.That((await _verifier.RunMaintenanceAsync(_scheduler)).Voided, Is.EqualTo(1));
        PurchaseList list = await _verifier.GetPurchasesAsync(_player);
        Assert.That(list.Purchases.Single().Voided, Is.True);
        Assert.That(Stored("tok-1").VoidedUtc, Is.EqualTo(_now.ToString("o")));

        VerifyResult again = await Verify("gems_t2", "tok-1");
        Assert.That((again.Status, again.Purchase!.Voided), Is.EqualTo((VerifyResult.Voided, true)));
        Assert.That(_store.Purchases(), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Voided_FindsOtherPlayersPurchaseThroughIndex()
    {
        _google.Next = new GoogleCheck { Result = GoogleCheck.Kind.Ok, Found = FakeGoogle.Paid("player-2") };
        await _verifier.VerifyAsync(new FakeContext("player-2"), "gems_t4", "tok-2");
        _google.Voided = new List<VoidedPurchase> { Void("tok-2", Make.Now), Void("unknown-token", Make.Now) };

        await _verifier.GetPurchasesAsync(_player); // 대비 경로: 1번 플레이어의 호출이 정리 작업을 돌린다
        Assert.That(Stored("tok-2", "player-2").Voided, Is.True);
        Assert.That(_store.Purchases(), Is.Empty);
    }

    [Test]
    public async Task VoidedMarkFails_CheckpointStaysAtUnappliedItem_ThenAppliedNextRun()
    {
        await Verify("gems_t2", "tok-1");
        DateTime voidedAt = Make.Now.AddHours(1);
        _google.Voided = new List<VoidedPurchase> { Void("tok-1", voidedAt), Void("x", Make.Now.AddHours(5)) };
        _store.UpdateConflict = hash => hash == PurchaseVerifier.Hash("tok-1"); // 표시가 계속 충돌
        _now = Make.Now.AddHours(6);

        await _verifier.RunMaintenanceAsync(_scheduler);
        Assert.That(Stored("tok-1").Voided, Is.False);
        Assert.That(_store.Job!.VoidedCheckpointMs, Is.EqualTo(Make.Ms(voidedAt)), "못 붙인 환불보다 뒤로 넘어가지 않는다");

        _store.UpdateConflict = null;
        _now = _now.AddMinutes(2);
        await _verifier.RunMaintenanceAsync(_scheduler);
        Assert.That(_google.VoidedStarts.Last(), Is.LessThanOrEqualTo(Make.Ms(voidedAt)), "다음에 그것부터 다시 본다");
        Assert.That(Stored("tok-1").Voided, Is.True);
        Assert.That(_store.Job!.VoidedCheckpointMs, Is.EqualTo(Make.Ms(Make.Now.AddHours(5))));
    }

    [Test]
    public async Task FallbackJobInGetPurchases_RunsAtMostEvery10Minutes_AndAdvancesCheckpoint()
    {
        _google.Voided = new List<VoidedPurchase> { Void("x", Make.Now.AddMinutes(-30)) };
        await _verifier.GetPurchasesAsync(_player);
        Assert.That(_google.VoidedStarts, Is.EqualTo(new[] { Make.Ms(Make.Now.AddDays(-29)) }));
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
        await _verifier.RunMaintenanceAsync(_scheduler);
        Assert.That(_store.Job!.VoidedCheckpointMs, Is.Zero);
    }

    // ------------------------------------------------------------------ 정리 작업 (2차 G · H)

    [Test]
    public async Task RunMaintenance_RefusesPlayers_ServerCallsThrottledToOneMinute()
    {
        Assert.That((await _verifier.RunMaintenanceAsync(_player)).Status, Is.EqualTo("forbidden"));
        Assert.That(_google.VoidedStarts, Is.Empty);
        Assert.That((await _verifier.RunMaintenanceAsync(_scheduler)).Status, Is.EqualTo(PurchaseList.Ok));
        _now = Make.Now.AddSeconds(30);
        Assert.That((await _verifier.RunMaintenanceAsync(_scheduler)).Status, Is.EqualTo("skipped"));
        _now = Make.Now.AddMinutes(10);
        Assert.That((await _verifier.RunMaintenanceAsync(_scheduler)).Status, Is.EqualTo(PurchaseList.Ok), "10분 예약 간격이면 늘 돈다");
        Assert.That(_google.VoidedStarts, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task JobStateFirstCreation_ConcurrentCaller_OnlyOneRuns()
    {
        // 작업 상태가 없어 처음 값을 만든 직후, 다른 호출도 만들고 먼저 잡았다 → 우리는 그 잡기를 보고 돌지 않는다 (구글을 두 번 부르지 않음)
        _store.AfterCreateJob = s =>
        {
            s.AfterCreateJob = null;
            s.PutJob(new JobState { LastRunMs = Make.Ms(Make.Now) });
        };
        MaintenanceResult r = await _verifier.RunMaintenanceAsync(_scheduler);
        Assert.That(r.Status, Is.EqualTo("skipped"));
        Assert.That(_google.VoidedStarts, Is.Empty);
    }

    // ------------------------------------------------------------------ 계정 닫기 · 보관 (2차 A · C)

    [Test]
    public async Task ArchiveAndDelete_ArchivesPerPlayer_KeepsIndexAnonymized_ClosesAccount()
    {
        _google.AckResult = false;
        await Verify("gems_t1", "tok-1");
        _google.AckResult = true;
        await Verify(IapCatalog.StarterPack, "s-1");

        ArchiveResult r = await _verifier.ArchiveAndDeleteAsync(_player);
        Assert.That((r.Status, r.Archived, r.AlreadyArchived), Is.EqualTo((PurchaseList.Ok, 2, false)));
        Assert.That(_store.Protected.ContainsKey(Make.Player), Is.False, "장부를 지웠다");
        Assert.That(_store.Closed, Does.Contain(Make.Player));
        string archiveId = PurchaseVerifier.ArchiveId(Make.Player);
        ArchivedPlayer archived = JsonConvert.DeserializeObject<ArchivedPlayer>(_store.Archives[archiveId])!;
        Assert.That(archived.Id, Does.Not.Contain(Make.Player));
        Assert.That(DateTime.Parse(archived.DeleteAfterUtc).ToUniversalTime(), Is.EqualTo(Make.Now.AddYears(5)));
        Assert.That(archived.Purchases.Select(p => p.ProductId), Is.EquivalentTo(new[] { "gems_t1", IapCatalog.StarterPack }));
        IndexEntry index = _store.IndexOf(PurchaseVerifier.Hash("tok-1"))!;
        Assert.That((index.Player, index.ArchiveId), Is.EqualTo(((string?)null, archiveId)), "색인은 지우지 않고 지운 계정 표시로 (플레이어 ID 없음)");
        Assert.That(_store.Acks, Is.Empty);
        Assert.That(JsonConvert.SerializeObject(_store.Archives) + JsonConvert.SerializeObject(_store.Index), Does.Not.Contain("tok-1"));

        ArchiveResult again = await _verifier.ArchiveAndDeleteAsync(_player);
        Assert.That((again.Status, again.AlreadyArchived, again.Archived), Is.EqualTo((PurchaseList.Ok, true, 0)), "두 번 보관하지 않는다");
        Assert.That(_store.Archives, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task ReplayAfterArchive_NeverGrants_ClosedPlayerRejected()
    {
        await Verify("gems_t2", "tok-1");
        await _verifier.ArchiveAndDeleteAsync(_player);

        // 같은 플레이어: 장부를 지웠어도 닫혀 있어 다시 받을 수 없다 (2배 · 패키지 초기화도 불가)
        VerifyResult same = await Verify("gems_t2", "tok-1");
        Assert.That((same.Status, same.Reason), Is.EqualTo((VerifyResult.Invalid, "account_closed")));
        Assert.That((await Verify(IapCatalog.StarterPack, "s-new")).Reason, Is.EqualTo("account_closed"));
        Assert.That((await _verifier.GetPurchasesAsync(_player)).Status, Is.EqualTo(PurchaseList.Closed));

        // 다른 플레이어가 그 토큰을 들고 와도 (구글 쪽이 통과시켜도) 색인이 지운 계정 것이라 거절
        _google.Next = new GoogleCheck { Result = GoogleCheck.Kind.Ok, Found = FakeGoogle.Paid("player-2") };
        VerifyResult other = await _verifier.VerifyAsync(new FakeContext("player-2"), "gems_t2", "tok-1");
        Assert.That((other.Status, other.Reason), Is.EqualTo((VerifyResult.Invalid, "token_used")));
        Assert.That(_store.Purchases(), Is.Empty);
        Assert.That(_store.Purchases("player-2"), Is.Empty);
    }

    [Test]
    public async Task ConcurrentSameDayArchives_EachPlayerHasOwnKey()
    {
        await Verify("gems_t1", "a");
        _google.Next = new GoogleCheck { Result = GoogleCheck.Kind.Ok, Found = FakeGoogle.Paid("player-2") };
        var second = new FakeContext("player-2");
        await _verifier.VerifyAsync(second, "gems_t2", "b");

        await _verifier.ArchiveAndDeleteAsync(_player);
        await _verifier.ArchiveAndDeleteAsync(second);
        Assert.That(_store.Archives.Keys, Is.EquivalentTo(new[] { PurchaseVerifier.ArchiveId(Make.Player), PurchaseVerifier.ArchiveId("player-2") }));
        Assert.That(_store.Archives.Values.Select(a => JsonConvert.DeserializeObject<ArchivedPlayer>(a)!.Purchases.Single().ProductId),
            Is.EquivalentTo(new[] { "gems_t1", "gems_t2" }), "같은 날 지워도 서로 덮지 않는다");
    }

    [Test]
    public async Task ArchivePurge_DeletesOnlyAfterDeleteAfter()
    {
        await Verify("gems_t1", "tok-1");
        await _verifier.ArchiveAndDeleteAsync(_player);
        string id = PurchaseVerifier.ArchiveId(Make.Player);
        int shard = Convert.ToInt32(id[..2], 16);

        _now = Make.Now.AddYears(5).AddDays(-1);
        _store.PutJob(new JobState { PurgeShard = shard });
        await _verifier.RunMaintenanceAsync(_scheduler);
        Assert.That(_store.Archives.ContainsKey(id), Is.True, "5년 전에는 남는다");

        _now = Make.Now.AddYears(5).AddMinutes(1);
        _store.PutJob(new JobState { PurgeShard = shard, LastRunMs = 1 });
        MaintenanceResult m = await _verifier.RunMaintenanceAsync(_scheduler);
        Assert.That((m.ArchivesDeleted, _store.Archives.ContainsKey(id)), Is.EqualTo((1, false)));
        Assert.That(_store.Job!.PurgeShard, Is.EqualTo((shard + PurchaseVerifier.ArchiveShardsPerRun) % PurchaseVerifier.ArchiveShards));
    }

    // ------------------------------------------------------------------ 오류 · 시간 한도

    [Test]
    public async Task NotConfigured_NoGrant()
    {
        _google.Configured = false;
        Assert.That((await Verify("gems_t1", "tok")).Status, Is.EqualTo(VerifyResult.NotConfigured));
        Assert.That(_store.Purchases(), Is.Empty);
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
        _now = Make.Now.AddMinutes(1);
        await Verify(IapCatalog.StarterPack, "b");
        PurchaseList list = await _verifier.GetPurchasesAsync(_player);
        Assert.That((list.Status, list.Configured), Is.EqualTo((PurchaseList.Ok, true)));
        Assert.That(list.Purchases.Select(p => p.ProductId), Is.EqualTo(new[] { "gems_t1", IapCatalog.StarterPack }));
    }

    [Test]
    public void Hash_IsLowercaseSha256Hex()
    {
        Assert.That(PurchaseVerifier.Hash("abc"), Is.EqualTo("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad"));
    }
}
