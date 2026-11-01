using HistoryHeroes.Server;
using Microsoft.Extensions.Logging.Abstractions;
using Unity.Services.CloudCode.Core;

namespace HistoryHeroesServer.Tests;

/// <summary>Cloud Code 실행 정보 (테스트는 플레이어 ID만 쓴다)</summary>
internal sealed class FakeContext : IExecutionContext
{
    public FakeContext(string? playerId = "player-1") => PlayerId = playerId!;

    public string ProjectId { get; set; } = "project";
    public string PlayerId { get; set; }
    public string EnvironmentId { get; set; } = "env";
    public string EnvironmentName { get; set; } = "production";
    public string AccessToken { get; set; } = "";
    public string UserId { get; set; } = "";
    public string Issuer { get; set; } = "";
    public string ServiceToken { get; set; } = "";
    public string AnalyticsUserId { get; set; } = "";
    public string UnityInstallationId { get; set; } = "";
    public string CorrelationId { get; set; } = "";
    public string ScopeId { get; set; } = "";
    public int CallDepth { get; set; }
    public ISession Session { get; set; } = null!;
}

/// <summary>Cloud Save protected 데이터 흉내: 쓸 때마다 lock이 바뀌고, 다른 lock으로 쓰면 충돌</summary>
internal sealed class FakeStore : IPurchaseStore
{
    public List<PurchaseRecord>? Saved;
    public string? Lock;
    public int Saves;
    /// <summary>쓰기 직전에 끼어드는 다른 호출 (true를 돌려주면 그 쓰기는 충돌로 거절)</summary>
    public Func<FakeStore, bool>? BeforeSave;
    /// <summary>쓰기 직후 (lock 없는 처음 만들기를 다른 호출이 덮는 흉내)</summary>
    public Action<FakeStore>? AfterSave;
    public Exception? LoadError;

    public Task<(List<PurchaseRecord> purchases, string? writeLock)> LoadAsync(IExecutionContext context)
    {
        if (LoadError != null) throw LoadError;
        return Task.FromResult((Saved == null ? new List<PurchaseRecord>() : new List<PurchaseRecord>(Saved), Saved == null ? null : Lock));
    }

    public Task<bool> TrySaveAsync(IExecutionContext context, List<PurchaseRecord> purchases, string? writeLock)
    {
        if (BeforeSave != null && BeforeSave(this)) return Task.FromResult(false);
        if (writeLock != null && writeLock != Lock) return Task.FromResult(false);
        Put(purchases);
        AfterSave?.Invoke(this);
        return Task.FromResult(true);
    }

    /// <summary>다른 호출의 쓰기 (lock 확인 없이)</summary>
    public void Put(List<PurchaseRecord> purchases)
    {
        Saved = new List<PurchaseRecord>(purchases);
        Lock = "lock-" + ++Saves;
    }
}

internal sealed class FakeGoogle : IGooglePlay
{
    public bool Configured = true;
    public GoogleCheck Next = new() { Result = GoogleCheck.Kind.Ok, Found = Paid("player-1") };
    public int Calls;

    public static GoogleCheck.Purchase Paid(string playerId, string? productId = null) => new()
    {
        PurchaseState = 0,
        OrderId = "GPA.0000-0000-0000-00000",
        ProductId = productId,
        ObfuscatedAccountId = PurchaseVerifier.Hash(playerId),
    };

    public Task<bool> IsConfiguredAsync(IExecutionContext context) => Task.FromResult(Configured);

    public Task<GoogleCheck> GetPurchaseAsync(IExecutionContext context, string productId, string purchaseToken)
    {
        Calls++;
        return Task.FromResult(Configured ? Next : GoogleCheck.Of(GoogleCheck.Kind.NotConfigured));
    }
}

internal static class Make
{
    public static readonly DateTime Now = new(2026, 11, 1, 3, 0, 0, DateTimeKind.Utc);

    public static PurchaseVerifier Verifier(FakeStore store, FakeGoogle google) => new(store, google, NullLogger.Instance, () => Now);
}
