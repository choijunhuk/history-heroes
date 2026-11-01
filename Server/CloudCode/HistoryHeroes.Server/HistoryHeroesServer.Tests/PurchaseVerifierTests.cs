using HistoryHeroes.Economy;
using HistoryHeroes.Server;

namespace HistoryHeroesServer.Tests;

/// <summary>서버 결제 검증 · 지급 판정 (가짜 구글 + 가짜 Cloud Save)</summary>
public sealed class PurchaseVerifierTests
{
    private FakeStore _store = null!;
    private FakeGoogle _google = null!;
    private PurchaseVerifier _verifier = null!;
    private readonly FakeContext _player = new();

    [SetUp]
    public void SetUp()
    {
        _store = new FakeStore();
        _google = new FakeGoogle();
        _verifier = Make.Verifier(_store, _google);
    }

    private Task<VerifyResult> Verify(string productId, string token) => _verifier.VerifyAsync(_player, productId, token);

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
        Assert.That(_store.Saved![0].TokenHash, Does.Not.Contain("tok-1"), "토큰 원문은 저장하지 않는다");
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

    [Test]
    public async Task PromoPurchase_IsValid_NotTest()
    {
        _google.Next.Found!.PurchaseType = 1;
        VerifyResult r = await Verify("gems_t2", "t-1");
        Assert.That((r.Status, r.Purchase!.Test), Is.EqualTo((VerifyResult.Granted, false)));
    }

    [TestCase(1, VerifyResult.Invalid, "state_1")]
    [TestCase(2, VerifyResult.Retry, "pending")]
    public async Task NotPurchasedState_NoGrant(int state, string status, string reason)
    {
        _google.Next.Found!.PurchaseState = state;
        VerifyResult r = await Verify("gems_t1", "tok-1");
        Assert.That((r.Status, r.Reason), Is.EqualTo((status, reason)));
        Assert.That(r.Purchase, Is.Null);
        Assert.That(_store.Saved, Is.Null);
    }

    [Test]
    public async Task WrongProduct_FromGoogle_Invalid()
    {
        _google.Next.Found!.ProductId = "gems_t6";
        VerifyResult r = await Verify("gems_t1", "tok-1");
        Assert.That((r.Status, r.Reason), Is.EqualTo((VerifyResult.Invalid, "product_mismatch")));
        Assert.That(_store.Saved, Is.Null);
    }

    [Test]
    public async Task GoogleRejectsToken_WrongPackageOrFake_Invalid()
    {
        // 다른 앱(패키지)의 토큰 · 가짜 토큰은 구글이 400/404로 답한다 (GooglePlayClient.KindOf)
        _google.Next = GoogleCheck.Of(GoogleCheck.Kind.Invalid, "google_404");
        VerifyResult r = await Verify("gems_t1", "fake");
        Assert.That((r.Status, r.Reason), Is.EqualTo((VerifyResult.Invalid, "google_404")));
        Assert.That(_store.Saved, Is.Null);
    }

    [Test]
    public async Task GoogleUnavailable_Retry()
    {
        _google.Next = GoogleCheck.Of(GoogleCheck.Kind.Retry, "google_503");
        VerifyResult r = await Verify("gems_t1", "tok-1");
        Assert.That(r.Status, Is.EqualTo(VerifyResult.Retry));
        Assert.That(_store.Saved, Is.Null);
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
        Assert.That(_store.Saved, Is.Null);
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
        VerifyResult r = await Verify(productId, token);
        Assert.That(r.Status, Is.EqualTo(VerifyResult.Invalid));
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

    [Test]
    public async Task ConcurrentWrite_Conflict_RereadsAndDecidesAgain()
    {
        // 장부가 이미 있다 (lock 있음). 우리가 읽은 뒤 쓰기 전에 다른 기기의 같은 상품 결제가 첫 구매 2배를 먼저 가져갔다
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
    public async Task FirstCreate_OverwrittenByOtherCall_WritesAgain()
    {
        // 장부 키가 없어 lock 없이 만들었는데, 같은 순간 다른 호출의 처음 만들기가 덮었다 → 다시 읽고 그 장부에 더한다
        bool overwritten = false;
        _store.AfterSave = s =>
        {
            if (overwritten) return;
            overwritten = true;
            s.Put(new List<PurchaseRecord> { new() { TokenHash = PurchaseVerifier.Hash("other"), ProductId = "gems_t2", FirstBonus = true } });
        };

        VerifyResult r = await Verify("gems_t1", "mine");
        Assert.That(r.Status, Is.EqualTo(VerifyResult.Granted));
        Assert.That(_store.Saved!.Select(p => p.ProductId), Is.EquivalentTo(new[] { "gems_t2", "gems_t1" }));
    }

    [Test]
    public async Task NotConfigured_NoGrant()
    {
        _google.Configured = false;
        VerifyResult r = await Verify("gems_t1", "tok");
        Assert.That(r.Status, Is.EqualTo(VerifyResult.NotConfigured));
        Assert.That(_store.Saved, Is.Null);
        Assert.That((await _verifier.GetPurchasesAsync(_player)).Configured, Is.False);
    }

    [Test]
    public async Task StoreError_Retry()
    {
        _store.LoadError = new HttpRequestException("down");
        Assert.That((await Verify("gems_t1", "tok")).Status, Is.EqualTo(VerifyResult.Retry));
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
