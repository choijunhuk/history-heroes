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
        _store.AfterCreateGate = s =>
        {
            s.AfterCreateGate = null;
            s.PutJobState(new JobState { LastRunMs = Make.Ms(Make.Now), VoidedCheckpointMs = 123 });
        };
        MaintenanceResult r = await _verifier.RunMaintenanceAsync(_scheduler);
        Assert.That(r.Status, Is.EqualTo("skipped"));
        Assert.That(_google.VoidedStarts, Is.Empty);
        Assert.That(_store.Job!.VoidedCheckpointMs, Is.EqualTo(123), "먼저 잡은 쪽의 상태를 덮지 않는다");
    }

    [Test]
    public async Task JobGateLateRecreation_NeverResetsState_ResultStillSaved()
    {
        // 다 돈 뒤에 다른 호출이 문지기를 늦게 다시 만들어도(lock 없이) 상태는 그대로
        _google.Voided = new List<VoidedPurchase> { Void("x", Make.Now.AddMinutes(-5)) };
        await _verifier.RunMaintenanceAsync(_scheduler);
        long checkpoint = _store.Job!.VoidedCheckpointMs;
        await _store.CreateJobGateAsync(new FakeContext(null), default);
        Assert.That(_store.Job!.VoidedCheckpointMs, Is.EqualTo(checkpoint));

        // 도는 도중(잡기 뒤 · 결과 쓰기 전) 문지기가 다시 만들어져도, 그사이 아무도 안 잡았으면 결과를 다시 써서 남긴다
        _now = Make.Now.AddMinutes(5);
        _google.Voided = new List<VoidedPurchase> { Void("y", Make.Now.AddMinutes(4)) };
        var seen = new List<int>();
        _store.BeforeJobSave = (s, n) =>
        {
            seen.Add(n);
            if (seen.Count != 2) return; // 이번 차례: 1번째 쓰기 = 잡기, 2번째 = 결과 → 결과 쓰기 직전에
            s.GateLock = "late-gate"; // 늦은 문지기 만들기 (lock만 바뀐다)
        };
        MaintenanceResult second = await _verifier.RunMaintenanceAsync(_scheduler);
        Assert.That(second.Status, Is.EqualTo(PurchaseList.Ok));
        Assert.That(seen, Has.Count.EqualTo(3), "잡기 · 결과(거절) · 결과 다시 쓰기");
        Assert.That(_store.Job!.VoidedCheckpointMs, Is.EqualTo(Make.Ms(Make.Now.AddMinutes(4))));
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
        Assert.That(_store.Purchases(), Is.Empty, "장부의 결제 키를 지웠다");
        Assert.That(_store.HeadClosed(), Is.True, "머리는 닫힌 채로 남는다 (지우지 않는다)");
        Assert.That(_store.Closed, Does.Contain(Make.Player));
        string archiveId = PurchaseVerifier.ArchiveId(Make.Player);
        ArchivedPlayer archived = _store.ArchiveOf(archiveId)!;
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
        Assert.That(_store.Archives.Values.Select(a => JsonConvert.DeserializeObject<ArchivedPlayer>(a.json)!.Purchases.Single().ProductId),
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
        _store.PutJobState(new JobState { PurgeShard = shard });
        await _verifier.RunMaintenanceAsync(_scheduler);
        Assert.That(_store.Archives.ContainsKey(id), Is.True, "5년 전에는 남는다");

        _now = Make.Now.AddYears(5).AddMinutes(1);
        _store.PutJobState(new JobState { PurgeShard = shard, LastRunMs = 1 });
        MaintenanceResult m = await _verifier.RunMaintenanceAsync(_scheduler);
        Assert.That((m.ArchivesDeleted, _store.Archives.ContainsKey(id)), Is.EqualTo((1, false)));
        Assert.That(_store.Job!.PurgeShard, Is.EqualTo((shard + PurchaseVerifier.ArchiveShardsPerRun) % PurchaseVerifier.ArchiveShards));
    }

    // ------------------------------------------------------------------ 닫기와 결제가 겹칠 때 (3차 1)

    [Test]
    public async Task PurchaseCommittedJustBeforeArchiveCloses_IsArchived_NotLost()
    {
        await Verify("gems_t1", "early");
        bool injected = false;
        _store.BeforeCloseHead = s =>
        {
            if (injected) return;
            injected = true;
            s.AddDirect(Make.Record("racing", "gems_t2", utc: "2026-11-01T03:00:01.0000000Z")); // 닫기가 머리를 바꾸기 직전에 다른 판정의 batch가 들어왔다
        };

        ArchiveResult r = await _verifier.ArchiveAndDeleteAsync(_player);
        Assert.That(r.Archived, Is.EqualTo(2), "닫은 뒤 찍은 목록에 들어 있다");
        Assert.That(_store.ArchiveOf(PurchaseVerifier.ArchiveId(Make.Player))!.Purchases.Select(p => p.TokenHash),
            Is.EquivalentTo(new[] { PurchaseVerifier.Hash("early"), PurchaseVerifier.Hash("racing") }));
        Assert.That(_store.Purchases(), Is.Empty);
    }

    [Test]
    public async Task ArchiveClosesBetweenDecisionAndCommit_PurchaseRefused_NothingLost()
    {
        await Verify("gems_t1", "early");
        bool archived = false;
        _store.BeforeAdd = s =>
        {
            if (archived) return false;
            archived = true;
            // 판정이 머리 lock을 읽은 뒤 · batch 전에 계정 삭제가 끝났다 → 이 batch의 lock은 옛것이라 거절된다
            Assert.That(_verifier.ArchiveAndDeleteAsync(_player).GetAwaiter().GetResult().Archived, Is.EqualTo(1));
            return false;
        };

        VerifyResult r = await Verify("gems_t2", "late");
        Assert.That((r.Status, r.Reason), Is.EqualTo((VerifyResult.Invalid, "account_closed")));
        Assert.That(_store.Purchases(), Is.Empty, "닫힌 계정에 결제가 생기지 않는다");
        Assert.That(_store.ArchiveOf(PurchaseVerifier.ArchiveId(Make.Player))!.Purchases.Single().TokenHash, Is.EqualTo(PurchaseVerifier.Hash("early")));
        IndexEntry late = _store.IndexOf(PurchaseVerifier.Hash("late"))!;
        Assert.That((late.Player, late.ArchiveId), Is.EqualTo(((string?)null, PurchaseVerifier.ArchiveId(Make.Player))), "지급 못 한 토큰의 색인도 익명으로");
        Assert.That(_store.Acks, Is.Empty);
    }

    [Test]
    public async Task VerifyRetryAfterArchive_HeadStaysClosed_NeverRevived()
    {
        await Verify("gems_t1", "a");
        await _verifier.ArchiveAndDeleteAsync(_player);
        _store.ResetHead(); // 머리가 늦게 처음 값으로 다시 만들어져도
        VerifyResult r = await Verify("gems_t2", "b");
        Assert.That((r.Status, r.Reason), Is.EqualTo((VerifyResult.Invalid, "account_closed")), "닫힘 표시가 지킨다");
        Assert.That(_store.Purchases(), Is.Empty);
        Assert.That((await _verifier.GetPurchasesAsync(_player)).Status, Is.EqualTo(PurchaseList.Closed));
    }

    [Test]
    public async Task Archive_AnonymizesReservedButNotGrantedTokens()
    {
        // 계정당 1번 상품을 두 번 사서 두 번째는 지급하지 않았다 → 그 토큰의 색인 칸도 계정을 지울 때 플레이어 ID를 지운다 (3차 4)
        await Verify(IapCatalog.StarterPack, "s-1");
        Assert.That((await Verify(IapCatalog.StarterPack, "s-2")).Status, Is.EqualTo(VerifyResult.AlreadyClaimed));
        Assert.That(_store.IndexOf(PurchaseVerifier.Hash("s-2"))!.Player, Is.EqualTo(Make.Player));

        await _verifier.ArchiveAndDeleteAsync(_player);
        foreach (string token in new[] { "s-1", "s-2" })
            Assert.That(_store.IndexOf(PurchaseVerifier.Hash(token))!.Player, Is.Null, token);
        Assert.That(JsonConvert.SerializeObject(_store.Index), Does.Not.Contain(Make.Player));
        Assert.That(_store.ReservedOf(), Has.Count.EqualTo(2), "예약 목록은 마무리 때까지 남는다");

        _now = Make.Now + PurchaseVerifier.ClosingGrace + TimeSpan.FromMinutes(1);
        Assert.That((await _verifier.RunMaintenanceAsync(_scheduler)).ClosingsDone, Is.EqualTo(1));
        Assert.That((_store.ReservedOf().Count, _store.Closing.Count), Is.EqualTo((0, 0)), "마무리가 예약 목록 · 할 일을 지운다");
    }

    // ------------------------------------------------------------------ 색인은 기대한 주인일 때만 lock으로 (4차 3 · 5)

    [Test]
    public async Task LateDrop_NeverOverwritesAnotherOwner()
    {
        // 이 플레이어가 예약 · 색인을 쓴 뒤 그 칸의 주인이 바뀌었고(다른 호출), 이 플레이어는 닫혔다 → 늦은 정리는 남의 칸을 덮지 않는다
        string hash = PurchaseVerifier.Hash("tok");
        _store.AfterIndexSet = (s, h) =>
        {
            s.AfterIndexSet = null;
            s.PutIndex(h, new IndexEntry { Player = "player-2" });
            s.Closed.Add(Make.Player);
        };
        VerifyResult r = await Verify("gems_t1", "tok");
        Assert.That((r.Status, r.Reason), Is.EqualTo((VerifyResult.Invalid, "account_closed")));
        Assert.That(_store.IndexOf(hash)!.Player, Is.EqualTo("player-2"));
    }

    [Test]
    public async Task LateDropAfterGrantAndArchive_KeepsAnonymizedEntry()
    {
        // 같은 토큰의 두 호출: 하나는 지급까지 끝났고 계정 삭제가 그 뒤 보관했다 → 늦게 닫힘을 본 다른 호출의 정리는 이미 익명인 칸을 건드리지 않는다
        bool once = false;
        _store.BeforeAdd = s =>
        {
            if (once) return false;
            once = true;
            s.AddDirect(Make.Record("tok", "gems_t1")); // 다른 호출이 같은 토큰을 먼저 지급했다
            Assert.That(_verifier.ArchiveAndDeleteAsync(_player).GetAwaiter().GetResult().Archived, Is.EqualTo(1));
            return false;
        };
        VerifyResult r = await Verify("gems_t1", "tok");
        Assert.That(r.Reason, Is.EqualTo("account_closed"));
        IndexEntry entry = _store.IndexOf(PurchaseVerifier.Hash("tok"))!;
        Assert.That((entry.Player, entry.ArchiveId), Is.EqualTo(((string?)null, PurchaseVerifier.ArchiveId(Make.Player))));
        Assert.That(_store.ArchiveOf(PurchaseVerifier.ArchiveId(Make.Player))!.Purchases.Single().TokenHash, Is.EqualTo(PurchaseVerifier.Hash("tok")));
    }

    [Test]
    public async Task NoReservationForClosingAccount()
    {
        // 닫힌 계정: 예약 · 색인을 만들지 않는다
        _store.Closed.Add(Make.Player);
        Assert.That((await Verify("gems_t1", "a")).Reason, Is.EqualTo("account_closed"));
        // 머리를 읽은 뒤(구글 확인 중) 계정 삭제가 끝났다 → 예약 batch의 머리 lock이 옛것이라 거절 → 다시 읽으면 닫힘
        var other = new FakeContext("player-3");
        _google.Next = new GoogleCheck { Result = GoogleCheck.Kind.Ok, Found = FakeGoogle.Paid("player-3") };
        bool closedOnce = false;
        var google = new ClosingGoogle(_google, () =>
        {
            if (closedOnce) return;
            closedOnce = true;
            _verifier.ArchiveAndDeleteAsync(other).GetAwaiter().GetResult();
        });
        var verifier = new PurchaseVerifier(_store, google, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, () => _now);
        VerifyResult r = await verifier.VerifyAsync(other, "gems_t1", "b");
        Assert.That(r.Reason, Is.EqualTo("account_closed"));
        Assert.That(_store.ReservedOf("player-3"), Is.Empty, "닫힌 뒤 예약 없음");
        Assert.That(_store.IndexOf(PurchaseVerifier.Hash("b")), Is.Null, "색인 칸도 만들지 않았다");
    }

    /// <summary>구글 확인을 돌려주기 직전에 다른 일을 끼워 넣는 가짜</summary>
    private sealed class ClosingGoogle : IGooglePlay
    {
        private readonly FakeGoogle _inner;
        private readonly Action _before;

        public ClosingGoogle(FakeGoogle inner, Action before)
        {
            _inner = inner;
            _before = before;
        }

        public Task<bool> IsConfiguredAsync(Unity.Services.CloudCode.Core.IExecutionContext context, CancellationToken ct) => _inner.IsConfiguredAsync(context, ct);

        public Task<GoogleCheck> GetPurchaseAsync(Unity.Services.CloudCode.Core.IExecutionContext context, string productId, string purchaseToken, CancellationToken ct)
        {
            _before();
            return _inner.GetPurchaseAsync(context, productId, purchaseToken, ct);
        }

        public Task<bool> AcknowledgeAsync(Unity.Services.CloudCode.Core.IExecutionContext context, string productId, string purchaseToken, CancellationToken ct) =>
            _inner.AcknowledgeAsync(context, productId, purchaseToken, ct);

        public Task<List<VoidedPurchase>?> ListVoidedAsync(Unity.Services.CloudCode.Core.IExecutionContext context, long startTimeMillis, CancellationToken ct) =>
            _inner.ListVoidedAsync(context, startTimeMillis, ct);
    }

    [Test]
    public async Task VerifyTimesOutAfterLateIndexWrite_ArchiveMaintenanceStillRemovesPlayerId()
    {
        // 예약 뒤 · 색인 쓰기 전에 계정 삭제가 끝났고(그때는 칸이 없어 건너뜀), 이 호출은 색인을 쓰자마자 시간 초과로 끝났다 → 칸에 플레이어 ID가 남는다
        // → 닫은 지 1시간 뒤 정리 작업의 마무리가 예약 목록으로 찾아 익명으로 바꾼다
        string hash = PurchaseVerifier.Hash("tok");
        _store.BeforeIndexSet = (s, h) =>
        {
            s.BeforeIndexSet = null;
            _verifier.ArchiveAndDeleteAsync(_player).GetAwaiter().GetResult();
        };
        _store.AfterIndexSet = (s, h) =>
        {
            s.AfterIndexSet = null;
            throw new OperationCanceledException("시간 초과 흉내");
        };
        Assert.That((await Verify("gems_t1", "tok")).Status, Is.EqualTo(VerifyResult.Retry));
        Assert.That(_store.IndexOf(hash)!.Player, Is.EqualTo(Make.Player), "남은 칸 (정리 전)");

        _now = Make.Now.AddMinutes(30);
        await _verifier.RunMaintenanceAsync(_scheduler);
        Assert.That(_store.IndexOf(hash)!.Player, Is.EqualTo(Make.Player), "1시간 안에는 진행 중일 수 있어 기다린다");

        _now = Make.Now + PurchaseVerifier.ClosingGrace + TimeSpan.FromMinutes(1);
        await _verifier.RunMaintenanceAsync(_scheduler);
        Assert.That(_store.IndexOf(hash)!.Player, Is.Null);
        Assert.That(JsonConvert.SerializeObject(_store.Index), Does.Not.Contain(Make.Player));
        Assert.That(_store.ReservedOf(), Is.Empty);
    }

    [Test]
    public async Task ClosingSweep_CreatesAnonymousEntryForReservedTokenWithoutIndex()
    {
        // 예약만 하고 색인을 쓰기 전에 끝난 호출 → 마무리가 익명 칸을 만든다 (같은 토큰을 다른 계정이 받지 못하게, 플레이어 ID 없이)
        _store.BeforeIndexSet = (s, h) =>
        {
            s.BeforeIndexSet = null;
            throw new OperationCanceledException("색인 쓰기 전에 끝남");
        };
        await Verify("gems_t1", "tok");
        Assert.That(_store.IndexOf(PurchaseVerifier.Hash("tok")), Is.Null);
        await _verifier.ArchiveAndDeleteAsync(_player);
        _now = Make.Now + PurchaseVerifier.ClosingGrace + TimeSpan.FromMinutes(1);
        await _verifier.RunMaintenanceAsync(_scheduler);
        IndexEntry entry = _store.IndexOf(PurchaseVerifier.Hash("tok"))!;
        Assert.That((entry.Player, entry.ArchiveId), Is.EqualTo(((string?)null, PurchaseVerifier.ArchiveId(Make.Player))));
    }

    // ------------------------------------------------------------------ 이미 있는 카카오 계정으로 바꾸기 (5차 1 · 4, 6차 1 · 2)

    [Test]
    public async Task CloseForSwitch_EmptyGuest_ClosesHead_ThenVerifyRefused()
    {
        Assert.That((await _verifier.CloseForSwitchAsync(_player)).Status, Is.EqualTo(PurchaseList.Ok));
        Assert.That((_store.HeadClosed(), _store.Closed.Contains(Make.Player)), Is.EqualTo((true, true)));
        Assert.That((await Verify("gems_t1", "late")).Reason, Is.EqualTo("account_closed"), "닫은 뒤 온 결제는 예약도 지급도 안 됨");
        Assert.That((_store.ReservedOf().Count, _store.Purchases().Count), Is.EqualTo((0, 0)));
        Assert.That((await _verifier.CloseForSwitchAsync(_player)).Status, Is.EqualTo(PurchaseList.Ok), "다시 불러도 같은 결과");
    }

    [Test]
    public async Task CloseForSwitch_GuestWithPurchaseOrReservation_HasPurchases_NothingWritten()
    {
        await Verify("gems_t1", "paid");
        Assert.That((await _verifier.CloseForSwitchAsync(_player)).Status, Is.EqualTo(SwitchResult.HasPurchases));
        Assert.That((_store.Closed.Contains(Make.Player), _store.HeadClosed()), Is.EqualTo((false, false)), "닫지 않았다 (다시 열 일도 없다)");
        Assert.That((await Verify("gems_t2", "next")).Status, Is.EqualTo(VerifyResult.Granted), "게스트는 그대로 결제할 수 있다");

        var reservedOnly = new FakeContext("player-r");
        _store.LegacyReserved["player-r"] = new HashSet<string> { PurchaseVerifier.Hash("old") }; // 옛 배포의 private 예약만 있어도
        Assert.That((await _verifier.CloseForSwitchAsync(reservedOnly)).Status, Is.EqualTo(SwitchResult.HasPurchases));
        Assert.That(_store.Closed, Does.Not.Contain("player-r"));
    }

    [Test]
    public async Task CloseForSwitch_ReservationLandsBeforeHeadClose_Refused_NotLost()
    {
        // 진행 중이던 VerifyPurchase의 예약 batch가 확인 뒤 · 머리 닫기 전에 들어왔다 → 머리 lock이 바뀌어 닫기 거절 → 다시 확인 → has_purchases
        bool injected = false;
        _store.BeforeCloseHead = s =>
        {
            if (injected) return;
            injected = true;
            string headLock = s.LoadHeadAsync(_player, Make.Player, default).GetAwaiter().GetResult().writeLock!;
            Assert.That(s.TryReserveAsync(_player, Make.Player, headLock, PurchaseVerifier.Hash("racing"), default).GetAwaiter().GetResult(), Is.True);
        };
        Assert.That((await _verifier.CloseForSwitchAsync(_player)).Status, Is.EqualTo(SwitchResult.HasPurchases));
        Assert.That((_store.Closed.Contains(Make.Player), _store.HeadClosed()), Is.EqualTo((false, false)));
        Assert.That(_store.ReservedOf(), Is.EqualTo(new[] { PurchaseVerifier.Hash("racing") }));
    }

    [Test]
    public async Task CloseForSwitch_IsFinal_InterruptedOrConcurrentCalls_NeverReopen()
    {
        // 머리만 닫고 표시를 못 쓴 채 끊김 → 다시 부르면 표시를 마저 쓰고 ok (결제가 있었어도 has_purchases로 열리지 않는다)
        await _verifier.CloseForSwitchAsync(_player);
        _store.Closed.Remove(Make.Player);
        Assert.That((await _verifier.CloseForSwitchAsync(_player)).Status, Is.EqualTo(PurchaseList.Ok));
        Assert.That(_store.Closed, Does.Contain(Make.Player));

        // 바꾸기 닫기 · 보관이 몇 번이든 어떤 순서로 겹쳐도 닫힌 채 (다시 여는 함수가 없다) → 빈 장부로 패키지 · 2배를 다시 받을 길이 없다
        await _verifier.ArchiveAndDeleteAsync(_player);
        Assert.That((await _verifier.CloseForSwitchAsync(_player)).Status, Is.EqualTo(PurchaseList.Ok));
        await _verifier.ArchiveAndDeleteAsync(_player);
        Assert.That((_store.HeadClosed(), _store.Closed.Contains(Make.Player)), Is.EqualTo((true, true)));
        Assert.That((await Verify(IapCatalog.StarterPack, "again")).Reason, Is.EqualTo("account_closed"));
        Assert.That((await _verifier.GetPurchasesAsync(_player)).Status, Is.EqualTo(PurchaseList.Closed));

        // 늦은 처음 만들기가 머리를 열린 처음 값으로 되돌려도 표시가 지킨다
        _store.ResetHead();
        Assert.That((await _verifier.CloseForSwitchAsync(_player)).Status, Is.EqualTo(PurchaseList.Ok));
        Assert.That((await Verify("gems_t1", "after-reset")).Reason, Is.EqualTo("account_closed"));
    }

    [Test]
    public void Module_HasNoReopenFunction()
    {
        // 게임이 부를 수 있는 함수 목록 — 닫힌 계정을 다시 여는 함수(옛 CancelSwitch)가 없다
        string[] names = typeof(PurchaseModule).GetMethods()
            .SelectMany(m => m.GetCustomAttributes(typeof(Unity.Services.CloudCode.Core.CloudCodeFunctionAttribute), false))
            .Cast<Unity.Services.CloudCode.Core.CloudCodeFunctionAttribute>().Select(a => a.Name).OrderBy(n => n).ToArray();
        Assert.That(names, Is.EqualTo(new[] { "ArchiveAndDeletePurchases", "CloseForSwitch", "GetPurchases", "RunMaintenance", "VerifyPurchase" }));
        Assert.That(typeof(IPurchaseStore).GetMethods().Select(m => m.Name), Has.None.Contains("Open").And.None.Contains("Reopen"));
    }

    [Test]
    public async Task Archive_CloseMarkerFails_NoClosingWork_LaterPurchasesSafe()
    {
        // 6차 2: 닫힘 표시 쓰기가 실패하면 보관은 retry — 마무리 할 일을 남기지 않는다 (할 일은 닫은 뒤에만)
        _store.CloseError = new HttpRequestException("down");
        Assert.That((await _verifier.ArchiveAndDeleteAsync(_player)).Status, Is.EqualTo(VerifyResult.Retry));
        Assert.That((_store.Closing.Count, _store.Closed.Contains(Make.Player)), Is.EqualTo((0, false)));

        // 열린 계정으로 계속 결제 → 1시간 뒤 정리 작업이 그 결제를 보관 · 익명화하지 않는다
        _store.CloseError = null;
        Assert.That((await Verify("gems_t1", "kept")).Status, Is.EqualTo(VerifyResult.Granted));
        _now = Make.Now + PurchaseVerifier.ClosingGrace + TimeSpan.FromMinutes(1);
        await _verifier.RunMaintenanceAsync(_scheduler);
        Assert.That(_store.Purchases().Count, Is.EqualTo(1));
        Assert.That(_store.IndexOf(PurchaseVerifier.Hash("kept"))!.Player, Is.EqualTo(Make.Player));

        // 옛 배포가 열린 계정에 남긴 할 일도 건드리지 않는다
        _store.Closing[PurchaseVerifier.ArchiveId(Make.Player)] = JsonConvert.SerializeObject(new ClosingPlayer { Player = Make.Player, Utc = "2026-10-01T00:00:00.0000000Z" });
        _now += TimeSpan.FromMinutes(5);
        await _verifier.RunMaintenanceAsync(_scheduler);
        Assert.That(_store.Purchases().Count, Is.EqualTo(1));
        Assert.That(_store.Closing, Is.Not.Empty, "건너뛰었다 (지우지도 않음)");
    }

    [Test]
    public async Task LegacyPrivateReservations_AnonymizedOnArchive_DeletedOnFinishing()
    {
        // 옛 배포: 예약을 private r_*에 적고 색인에 플레이어 ID를 적었는데 지급은 안 됐다
        string hash = PurchaseVerifier.Hash("legacy");
        _store.LegacyReserved[Make.Player] = new HashSet<string> { hash };
        _store.PutIndex(hash, new IndexEntry { Player = Make.Player });
        await _verifier.ArchiveAndDeleteAsync(_player);
        Assert.That(_store.IndexOf(hash)!.Player, Is.Null, "보관 때 옛 예약도 익명으로");
        Assert.That(_store.LegacyReserved[Make.Player], Is.Not.Empty, "마무리 전에는 남는다");
        _now = Make.Now + PurchaseVerifier.ClosingGrace + TimeSpan.FromMinutes(1);
        await _verifier.RunMaintenanceAsync(_scheduler);
        Assert.That(_store.LegacyReserved[Make.Player], Is.Empty, "마무리가 옛 예약도 지운다");
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
