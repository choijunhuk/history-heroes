using HistoryHeroes.Server;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Unity.Services.CloudCode.Core;

namespace HistoryHeroesServer.Tests;

/// <summary>Cloud Code 실행 정보 (테스트는 플레이어 ID만 쓴다)</summary>
internal sealed class FakeContext : IExecutionContext
{
    public FakeContext(string? playerId = Make.Player) => PlayerId = playerId!;

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

/// <summary>
/// Cloud Save 흉내: 값은 JSON으로 넣고 꺼내(진짜처럼 매번 새 객체), 쓸 때마다 lock이 바뀌고, 다른 lock으로 쓰면 충돌.
/// 플레이어 장부 · 색인 · 작업 상태 · 보관본을 따로 둔다
/// </summary>
internal sealed class FakeStore : IPurchaseStore
{
    private readonly Dictionary<string, (string json, string writeLock)> _ledgers = new();
    private int _locks;
    /// <summary>장부 쓰기 기록: (플레이어, 결제 수, lock을 붙였나)</summary>
    public readonly List<(string player, int count, bool locked)> Writes = new();
    public readonly Dictionary<string, string> Index = new();
    public string? JobJson;
    public string? JobLock;
    public readonly Dictionary<string, List<ArchivedPlayer>> Archives = new();
    public readonly List<string> DeletedMonths = new();
    /// <summary>쓰기 직전에 끼어드는 다른 호출 (true를 돌려주면 그 쓰기는 충돌로 거절)</summary>
    public Func<FakeStore, bool>? BeforeSave;
    public Exception? LoadError;
    /// <summary>읽기가 끝나지 않는다 (시간 한도 시험)</summary>
    public bool Hang;

    private static T Copy<T>(T value) => JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value))!;

    public List<PurchaseRecord>? Saved => Get(Make.Player);

    public List<PurchaseRecord>? Get(string player) =>
        _ledgers.TryGetValue(player, out var v) ? JsonConvert.DeserializeObject<List<PurchaseRecord>>(v.json) : null;

    /// <summary>다른 호출의 쓰기 (lock 확인 없이)</summary>
    public void Put(List<PurchaseRecord> purchases, string player = Make.Player) =>
        _ledgers[player] = (JsonConvert.SerializeObject(purchases), "lock-" + ++_locks);

    public async Task<(List<PurchaseRecord> purchases, string? writeLock)> LoadAsync(IExecutionContext context, string playerId, CancellationToken ct)
    {
        if (Hang) await Task.Delay(Timeout.Infinite, ct);
        if (LoadError != null) throw LoadError;
        return _ledgers.TryGetValue(playerId, out var v)
            ? (JsonConvert.DeserializeObject<List<PurchaseRecord>>(v.json)!, v.writeLock)
            : (new List<PurchaseRecord>(), null);
    }

    public Task<bool> TrySaveAsync(IExecutionContext context, string playerId, List<PurchaseRecord> purchases, string? writeLock, CancellationToken ct)
    {
        if (BeforeSave != null && BeforeSave(this)) return Task.FromResult(false);
        if (writeLock != null && (!_ledgers.TryGetValue(playerId, out var v) || v.writeLock != writeLock)) return Task.FromResult(false);
        Writes.Add((playerId, purchases.Count, writeLock != null));
        Put(purchases, playerId);
        return Task.FromResult(true);
    }

    public Task DeleteAsync(IExecutionContext context, string playerId, CancellationToken ct)
    {
        _ledgers.Remove(playerId);
        return Task.CompletedTask;
    }

    public Task SetIndexAsync(IExecutionContext context, string tokenHash, string playerId, CancellationToken ct)
    {
        Index[tokenHash] = playerId;
        return Task.CompletedTask;
    }

    public Task<Dictionary<string, string>> FindIndexAsync(IExecutionContext context, IReadOnlyCollection<string> tokenHashes, CancellationToken ct) =>
        Task.FromResult(tokenHashes.Where(Index.ContainsKey).Distinct().ToDictionary(h => h, h => Index[h]));

    public Task DeleteIndexAsync(IExecutionContext context, IReadOnlyCollection<string> tokenHashes, CancellationToken ct)
    {
        foreach (string h in tokenHashes) Index.Remove(h);
        return Task.CompletedTask;
    }

    public JobState? Job => JobJson == null ? null : JsonConvert.DeserializeObject<JobState>(JobJson);

    public Task<(JobState state, string? writeLock)> LoadJobAsync(IExecutionContext context, CancellationToken ct) =>
        Task.FromResult(JobJson == null ? (new JobState(), (string?)null) : (JsonConvert.DeserializeObject<JobState>(JobJson)!, JobLock));

    public Task<string?> TrySaveJobAsync(IExecutionContext context, JobState state, string? writeLock, CancellationToken ct)
    {
        if (writeLock != null && writeLock != JobLock) return Task.FromResult<string?>(null);
        JobJson = JsonConvert.SerializeObject(state);
        JobLock = "job-" + ++_locks;
        return Task.FromResult<string?>(JobLock);
    }

    public Task AppendArchiveAsync(IExecutionContext context, DateTime utcNow, ArchivedPlayer archive, CancellationToken ct)
    {
        string month = utcNow.ToString("yyyyMM");
        if (!Archives.TryGetValue(month, out List<ArchivedPlayer>? list)) Archives[month] = list = new List<ArchivedPlayer>();
        list.RemoveAll(a => a.Id == archive.Id);
        list.Add(Copy(archive));
        return Task.CompletedTask;
    }

    public Task DeleteArchiveMonthAsync(IExecutionContext context, string month, CancellationToken ct)
    {
        DeletedMonths.Add(month);
        Archives.Remove(month);
        return Task.CompletedTask;
    }
}

internal sealed class FakeGoogle : IGooglePlay
{
    public bool Configured = true;
    public GoogleCheck Next = new() { Result = GoogleCheck.Kind.Ok, Found = Paid(Make.Player) };
    public int Calls;
    public bool AckResult = true;
    public readonly List<string> Acks = new();
    public List<VoidedPurchase>? Voided = new();
    public readonly List<long> VoidedStarts = new();

    public static GoogleCheck.Purchase Paid(string playerId, string? productId = null) => new()
    {
        PurchaseState = 0,
        ConsumptionState = 0,
        AcknowledgementState = 0,
        OrderId = "GPA.0000-0000-0000-00000",
        ProductId = productId,
        ObfuscatedAccountId = PurchaseVerifier.Hash(playerId),
    };

    public Task<bool> IsConfiguredAsync(IExecutionContext context, CancellationToken ct) => Task.FromResult(Configured);

    public Task<GoogleCheck> GetPurchaseAsync(IExecutionContext context, string productId, string purchaseToken, CancellationToken ct)
    {
        Calls++;
        return Task.FromResult(Configured ? Next : GoogleCheck.Of(GoogleCheck.Kind.NotConfigured));
    }

    public Task<bool> AcknowledgeAsync(IExecutionContext context, string productId, string purchaseToken, CancellationToken ct)
    {
        Acks.Add(purchaseToken);
        return Task.FromResult(Configured && AckResult);
    }

    public Task<List<VoidedPurchase>?> ListVoidedAsync(IExecutionContext context, long startTimeMillis, CancellationToken ct)
    {
        VoidedStarts.Add(startTimeMillis);
        return Task.FromResult(Configured ? Voided : null);
    }
}

internal static class Make
{
    public const string Player = "player-1";
    public static readonly DateTime Now = new(2026, 11, 1, 3, 0, 0, DateTimeKind.Utc);

    public static PurchaseVerifier Verifier(FakeStore store, FakeGoogle google, Func<DateTime>? now = null, TimeSpan? budget = null) =>
        new(store, google, NullLogger.Instance, now ?? (() => Now), budget);

    public static long Ms(DateTime utc) => new DateTimeOffset(utc).ToUnixTimeMilliseconds();
}
