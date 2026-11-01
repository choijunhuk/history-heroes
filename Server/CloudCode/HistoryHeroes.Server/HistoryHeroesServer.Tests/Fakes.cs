using HistoryHeroes.Server;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Unity.Services.CloudCode.Core;

namespace HistoryHeroesServer.Tests;

/// <summary>Cloud Code 실행 정보 (테스트는 플레이어 ID만 쓴다. null = 예약 실행 같은 서버 호출)</summary>
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
/// Cloud Save 흉내: 값은 JSON으로 넣고 꺼내고(진짜처럼 매번 새 객체), 쓸 때마다 lock이 바뀌고, 다른 lock으로 쓰면 충돌.
/// 결제 더하기는 진짜처럼 [머리 lock 확인 + 머리 · 결제 키 쓰기]가 한 번에 되거나 안 된다
/// </summary>
internal sealed class FakeStore : IPurchaseStore
{
    private int _locks;
    /// <summary>플레이어 → protected 키 → (글, lock)</summary>
    public readonly Dictionary<string, Dictionary<string, (string json, string writeLock)>> Protected = new();
    public readonly HashSet<string> Closed = new();
    public readonly Dictionary<string, string> Index = new();
    public readonly Dictionary<string, string> Acks = new();
    public readonly Dictionary<string, string> Archives = new();
    public string? JobJson;
    public string? JobLock;
    /// <summary>protected 쓰기 기록: (플레이어, 키, 읽은 lock으로 지켜졌나)</summary>
    public readonly List<(string player, string key, bool guarded)> Writes = new();
    /// <summary>결제 더하기 직전에 끼어드는 다른 호출 (true를 돌려주면 그 쓰기는 충돌로 거절)</summary>
    public Func<FakeStore, bool>? BeforeAdd;
    /// <summary>이 토큰 해시의 결제 고치기를 늘 충돌로 (환불 표시 실패 흉내)</summary>
    public Func<string, bool>? UpdateConflict;
    /// <summary>작업 상태를 처음 만든 직전에 다른 호출이 끼어든다</summary>
    public Action<FakeStore>? AfterCreateJob;
    public Exception? LoadError;
    /// <summary>읽기가 끝나지 않는다 (시간 한도 시험)</summary>
    public bool Hang;

    private string NewLock() => "lock-" + ++_locks;

    private static string Ser(object value) => JsonConvert.SerializeObject(value);

    private static T Deser<T>(string json) => JsonConvert.DeserializeObject<T>(json)!;

    private static string HeadJson(string marker) => Ser(new { v = 1, n = marker });

    private Dictionary<string, (string json, string writeLock)> P(string player) =>
        Protected.TryGetValue(player, out var d) ? d : Protected[player] = new Dictionary<string, (string, string)>();

    private async Task Gate(CancellationToken ct)
    {
        if (Hang) await Task.Delay(Timeout.Infinite, ct);
        if (LoadError != null) throw LoadError;
    }

    public List<PurchaseRecord> Purchases(string player = Make.Player) =>
        P(player).Where(kv => kv.Key.StartsWith("p_")).Select(kv => Deser<PurchaseRecord>(kv.Value.json)).OrderBy(p => p.Utc).ToList();

    /// <summary>이 플레이어 protected 데이터 전부 (게임이 읽을 수 있는 것)</summary>
    public string ProtectedText(string player = Make.Player) => string.Join("\n", P(player).Values.Select(v => v.json));

    /// <summary>다른 호출의 결제 더하기 (머리 lock이 바뀐다)</summary>
    public void AddDirect(PurchaseRecord record, string player = Make.Player)
    {
        P(player)["iap_head"] = (HeadJson(record.TokenHash), NewLock());
        P(player)["p_" + record.TokenHash] = (Ser(record), NewLock());
    }

    /// <summary>다른 호출의 늦은 머리 만들기 (처음 값으로, lock 없이)</summary>
    public void ResetHead(string player = Make.Player) => P(player)["iap_head"] = (HeadJson(""), NewLock());

    public async Task<string?> LoadHeadLockAsync(IExecutionContext context, string playerId, CancellationToken ct)
    {
        await Gate(ct);
        return P(playerId).TryGetValue("iap_head", out var head) ? head.writeLock : null;
    }

    public Task CreateHeadAsync(IExecutionContext context, string playerId, CancellationToken ct)
    {
        ResetHead(playerId);
        Writes.Add((playerId, "iap_head", false));
        return Task.CompletedTask;
    }

    public async Task<List<PurchaseRecord>> LoadPurchasesAsync(IExecutionContext context, string playerId, CancellationToken ct)
    {
        await Gate(ct);
        return Purchases(playerId);
    }

    public async Task<(PurchaseRecord? record, string? writeLock)> LoadPurchaseAsync(IExecutionContext context, string playerId, string tokenHash, CancellationToken ct)
    {
        await Gate(ct);
        return P(playerId).TryGetValue("p_" + tokenHash, out var v) ? (Deser<PurchaseRecord>(v.json), v.writeLock) : (null, null);
    }

    public Task<bool> TryAddPurchaseAsync(IExecutionContext context, string playerId, string headLock, PurchaseRecord record, CancellationToken ct)
    {
        if (BeforeAdd != null && BeforeAdd(this)) return Task.FromResult(false);
        if (!P(playerId).TryGetValue("iap_head", out var head) || head.writeLock != headLock) return Task.FromResult(false);
        P(playerId)["iap_head"] = (HeadJson(record.TokenHash), NewLock());
        P(playerId)["p_" + record.TokenHash] = (Ser(record), NewLock());
        Writes.Add((playerId, "p_" + record.TokenHash, true));
        return Task.FromResult(true);
    }

    public Task<bool> TryUpdatePurchaseAsync(IExecutionContext context, string playerId, PurchaseRecord record, string writeLock, CancellationToken ct)
    {
        string key = "p_" + record.TokenHash;
        if (UpdateConflict?.Invoke(record.TokenHash) == true) return Task.FromResult(false);
        if (!P(playerId).TryGetValue(key, out var v) || v.writeLock != writeLock) return Task.FromResult(false);
        P(playerId)[key] = (Ser(record), NewLock());
        Writes.Add((playerId, key, true));
        return Task.FromResult(true);
    }

    public Task DeletePlayerPurchasesAsync(IExecutionContext context, string playerId, CancellationToken ct)
    {
        Protected.Remove(playerId);
        return Task.CompletedTask;
    }

    public async Task<bool> IsClosedAsync(IExecutionContext context, string playerId, CancellationToken ct)
    {
        await Gate(ct);
        return Closed.Contains(playerId);
    }

    public Task CloseAsync(IExecutionContext context, string playerId, DateTime utcNow, CancellationToken ct)
    {
        Closed.Add(playerId);
        return Task.CompletedTask;
    }

    public IndexEntry? IndexOf(string tokenHash) => Index.TryGetValue(tokenHash, out string? json) ? Deser<IndexEntry>(json) : null;

    public Task<Dictionary<string, IndexEntry>> FindIndexAsync(IExecutionContext context, IReadOnlyCollection<string> tokenHashes, CancellationToken ct) =>
        Task.FromResult(tokenHashes.Distinct().Where(Index.ContainsKey).ToDictionary(h => h, h => IndexOf(h)!));

    public Task SetIndexAsync(IExecutionContext context, string tokenHash, IndexEntry entry, CancellationToken ct)
    {
        Index[tokenHash] = Ser(entry);
        return Task.CompletedTask;
    }

    public Task SetAckAsync(IExecutionContext context, string tokenHash, PendingAck ack, CancellationToken ct)
    {
        Acks[tokenHash] = Ser(ack);
        return Task.CompletedTask;
    }

    public Task<Dictionary<string, PendingAck>> FindAcksAsync(IExecutionContext context, IReadOnlyCollection<string> tokenHashes, CancellationToken ct) =>
        Task.FromResult(tokenHashes.Distinct().Where(Acks.ContainsKey).ToDictionary(h => h, h => Deser<PendingAck>(Acks[h])));

    public Task<Dictionary<string, PendingAck>> ListAcksAsync(IExecutionContext context, CancellationToken ct) =>
        Task.FromResult(Acks.ToDictionary(kv => kv.Key, kv => Deser<PendingAck>(kv.Value)));

    public Task DeleteAckAsync(IExecutionContext context, string tokenHash, CancellationToken ct)
    {
        Acks.Remove(tokenHash);
        return Task.CompletedTask;
    }

    public JobState? Job => JobJson == null ? null : Deser<JobState>(JobJson);

    public Task<(JobState? state, string? writeLock)> LoadJobAsync(IExecutionContext context, CancellationToken ct) =>
        Task.FromResult(JobJson == null ? ((JobState?)null, (string?)null) : (Deser<JobState>(JobJson), JobLock));

    public Task CreateJobAsync(IExecutionContext context, CancellationToken ct)
    {
        JobJson = Ser(new JobState());
        JobLock = NewLock();
        AfterCreateJob?.Invoke(this);
        return Task.CompletedTask;
    }

    public Task<string?> TrySaveJobAsync(IExecutionContext context, JobState state, string writeLock, CancellationToken ct)
    {
        if (writeLock != JobLock) return Task.FromResult<string?>(null);
        JobJson = Ser(state);
        JobLock = NewLock();
        return Task.FromResult<string?>(JobLock);
    }

    /// <summary>다른 호출이 작업 상태를 바꿨다 (lock이 바뀐다)</summary>
    public void PutJob(JobState state)
    {
        JobJson = Ser(state);
        JobLock = NewLock();
    }

    public Task SetArchiveAsync(IExecutionContext context, ArchivedPlayer archive, CancellationToken ct)
    {
        Archives[archive.Id] = Ser(archive);
        return Task.CompletedTask;
    }

    public Task<List<ArchivedPlayer>> LoadArchiveShardAsync(IExecutionContext context, int shard, CancellationToken ct) =>
        Task.FromResult(Archives.Where(kv => kv.Key.StartsWith(shard.ToString("x2"))).Select(kv => Deser<ArchivedPlayer>(kv.Value)).ToList());

    public Task DeleteArchiveAsync(IExecutionContext context, string archiveId, CancellationToken ct)
    {
        Archives.Remove(archiveId);
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

    public static PurchaseRecord Record(string token, string productId, bool firstBonus = false, string utc = "2026-10-31T00:00:00.0000000Z") => new()
    {
        TokenHash = PurchaseVerifier.Hash(token), ProductId = productId, FirstBonus = firstBonus, Utc = utc, Acknowledged = true,
    };
}
