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
/// 결제 더하기 · 작업 상태 쓰기는 진짜처럼 [문지기 lock 확인 + 쓰기]가 한 번에 되거나 안 된다
/// </summary>
internal sealed class FakeStore : IPurchaseStore
{
    private int _locks;
    /// <summary>플레이어 → protected 키 → (글, lock)</summary>
    public readonly Dictionary<string, Dictionary<string, (string json, string writeLock)>> Protected = new();
    /// <summary>닫힘 표시 (protected iap_closed — 머리와 한 번에 읽힌다)</summary>
    public readonly HashSet<string> Closed = new();
    /// <summary>옛 배포가 private에 쓴 닫힘 표시</summary>
    public readonly HashSet<string> LegacyClosed = new();
    /// <summary>플레이어당 protected 키 한도 (넘는 새 키 쓰기는 Cloud Save처럼 실패)</summary>
    public int KeyLimit = int.MaxValue;
    /// <summary>저장소 호출마다 (부른 호출의 컨텍스트) — 두 호출을 한 줄로 번갈아 돌리는 시험용</summary>
    public Action<IExecutionContext>? OnOp;
    /// <summary>닫힘 표시 쓰기가 실패한다 (서버 장애 흉내)</summary>
    public Exception? CloseError;
    /// <summary>옛 배포가 private에 남긴 예약 (플레이어 → 토큰 해시)</summary>
    public readonly Dictionary<string, HashSet<string>> LegacyReserved = new();
    /// <summary>토큰 해시 → (글, lock)</summary>
    public readonly Dictionary<string, (string json, string writeLock)> Index = new();
    public readonly Dictionary<string, string> Acks = new();
    public readonly Dictionary<string, (string json, string writeLock)> Archives = new();
    public readonly Dictionary<string, string> Closing = new();
    public string? GateLock;
    public string? JobStateJson;
    /// <summary>protected 쓰기 기록: (플레이어, 키, 읽은 lock으로 지켜졌나)</summary>
    public readonly List<(string player, string key, bool guarded)> Writes = new();
    /// <summary>결제 더하기 직전에 끼어드는 다른 호출 (true를 돌려주면 그 쓰기는 충돌로 거절)</summary>
    public Func<FakeStore, bool>? BeforeAdd;
    /// <summary>머리 닫기 직전에 끼어드는 다른 호출</summary>
    public Action<FakeStore>? BeforeCloseHead;
    /// <summary>이 토큰 해시의 결제 고치기를 늘 충돌로 (환불 표시 실패 흉내)</summary>
    public Func<string, bool>? UpdateConflict;
    /// <summary>작업 문지기를 만든 직후 다른 호출이 끼어든다</summary>
    public Action<FakeStore>? AfterCreateGate;
    /// <summary>작업 상태 쓰기 직전 (몇 번째 쓰기인지)</summary>
    public Action<FakeStore, int>? BeforeJobSave;
    private int _jobSaves;
    /// <summary>색인 한 칸을 쓰기 직전 · 직후 (다른 호출 끼워 넣기 · 시간 초과 흉내)</summary>
    public Action<FakeStore, string>? BeforeIndexSet;
    public Action<FakeStore, string>? AfterIndexSet;
    public Exception? LoadError;
    /// <summary>읽기가 끝나지 않는다 (시간 한도 시험)</summary>
    public bool Hang;

    private string NewLock() => "lock-" + ++_locks;

    private static string Ser(object value) => JsonConvert.SerializeObject(value);

    private static T Deser<T>(string json) => JsonConvert.DeserializeObject<T>(json)!;

    private static string HeadJson(string marker, bool closed = false) => Ser(new { v = 1, n = marker, s = closed ? "closed" : "open" });

    private Dictionary<string, (string json, string writeLock)> P(string player) =>
        Protected.TryGetValue(player, out var d) ? d : Protected[player] = new Dictionary<string, (string, string)>();

    /// <summary>protected 키 수 (닫힘 표시 포함)</summary>
    public int KeyCount(string player = Make.Player) => P(player).Count + (Closed.Contains(player) ? 1 : 0);

    /// <summary>새 키를 쓸 칸이 있나 (없으면 Cloud Save처럼 실패)</summary>
    private void Room(string player, bool newKey)
    {
        if (newKey && KeyCount(player) >= KeyLimit) throw new InvalidOperationException("key_limit");
    }

    public List<string> ReservedOf(string player = Make.Player) => P(player).Keys.Where(k => k.StartsWith("r_")).Select(k => k[2..]).ToList();

    private async Task Gate(CancellationToken ct)
    {
        if (Hang) await Task.Delay(Timeout.Infinite, ct);
        if (LoadError != null) throw LoadError;
    }

    public List<PurchaseRecord> Purchases(string player = Make.Player) =>
        P(player).Where(kv => kv.Key.StartsWith("p_")).Select(kv => Deser<PurchaseRecord>(kv.Value.json)).OrderBy(p => p.Utc).ToList();

    public bool HeadClosed(string player = Make.Player) => P(player).TryGetValue("iap_head", out var h) && h.json.Contains("\"s\":\"closed\"");

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

    public async Task<(string? writeLock, bool closed)> LoadHeadAsync(IExecutionContext context, string playerId, CancellationToken ct)
    {
        OnOp?.Invoke(context);
        await Gate(ct);
        bool marked = Closed.Contains(playerId);
        return P(playerId).TryGetValue("iap_head", out var head) ? (head.writeLock, marked || head.json.Contains("\"s\":\"closed\"")) : (null, marked);
    }

    public Task<int> CountProtectedKeysAsync(IExecutionContext context, string playerId, CancellationToken ct)
    {
        OnOp?.Invoke(context);
        return Task.FromResult(KeyCount(playerId));
    }

    public Task CreateHeadAsync(IExecutionContext context, string playerId, CancellationToken ct)
    {
        OnOp?.Invoke(context);
        Room(playerId, !P(playerId).ContainsKey("iap_head"));
        ResetHead(playerId);
        Writes.Add((playerId, "iap_head", false));
        return Task.CompletedTask;
    }

    public Task<bool> TryCloseHeadAsync(IExecutionContext context, string playerId, string headLock, DateTime utcNow, CancellationToken ct)
    {
        OnOp?.Invoke(context);
        BeforeCloseHead?.Invoke(this);
        if (!P(playerId).TryGetValue("iap_head", out var head) || head.writeLock != headLock) return Task.FromResult(false);
        Room(playerId, !Closed.Contains(playerId));
        P(playerId)["iap_head"] = (HeadJson("closed", closed: true), NewLock()); // [머리 + 표시] 한 batch
        Closed.Add(playerId);
        Writes.Add((playerId, "iap_head", true));
        return Task.FromResult(true);
    }

    public async Task<List<PurchaseRecord>> LoadPurchasesAsync(IExecutionContext context, string playerId, CancellationToken ct)
    {
        OnOp?.Invoke(context);
        await Gate(ct);
        return Purchases(playerId);
    }

    public async Task<(PurchaseRecord? record, string? writeLock)> LoadPurchaseAsync(IExecutionContext context, string playerId, string tokenHash, CancellationToken ct)
    {
        OnOp?.Invoke(context);
        await Gate(ct);
        return P(playerId).TryGetValue("p_" + tokenHash, out var v) ? (Deser<PurchaseRecord>(v.json), v.writeLock) : (null, null);
    }

    public Task<bool> TryAddPurchaseAsync(IExecutionContext context, string playerId, string headLock, PurchaseRecord record, CancellationToken ct)
    {
        OnOp?.Invoke(context);
        if (BeforeAdd != null && BeforeAdd(this)) return Task.FromResult(false);
        if (!P(playerId).TryGetValue("iap_head", out var head) || head.writeLock != headLock) return Task.FromResult(false);
        Room(playerId, !P(playerId).ContainsKey("p_" + record.TokenHash));
        P(playerId)["iap_head"] = (HeadJson(record.TokenHash), NewLock());
        P(playerId)["p_" + record.TokenHash] = (Ser(record), NewLock());
        Writes.Add((playerId, "p_" + record.TokenHash, true));
        return Task.FromResult(true);
    }

    public Task<bool> TryReserveAsync(IExecutionContext context, string playerId, string headLock, string tokenHash, CancellationToken ct)
    {
        OnOp?.Invoke(context);
        if (!P(playerId).TryGetValue("iap_head", out var head) || head.writeLock != headLock) return Task.FromResult(false);
        Room(playerId, !P(playerId).ContainsKey("r_" + tokenHash));
        P(playerId)["iap_head"] = (HeadJson("r:" + tokenHash), NewLock());
        P(playerId)["r_" + tokenHash] = ("true", NewLock());
        Writes.Add((playerId, "r_" + tokenHash, true));
        return Task.FromResult(true);
    }

    public Task<List<string>> ListReservedAsync(IExecutionContext context, string playerId, CancellationToken ct)
    {
        OnOp?.Invoke(context);
        return Task.FromResult(ReservedOf(playerId));
    }

    public Task DeleteReservedAsync(IExecutionContext context, string playerId, IReadOnlyCollection<string> tokenHashes, CancellationToken ct)
    {
        foreach (string h in tokenHashes) P(playerId).Remove("r_" + h);
        return Task.CompletedTask;
    }

    public Task<bool> TryUpdatePurchaseAsync(IExecutionContext context, string playerId, PurchaseRecord record, string writeLock, CancellationToken ct)
    {
        OnOp?.Invoke(context);
        string key = "p_" + record.TokenHash;
        if (UpdateConflict?.Invoke(record.TokenHash) == true) return Task.FromResult(false);
        if (!P(playerId).TryGetValue(key, out var v) || v.writeLock != writeLock) return Task.FromResult(false);
        P(playerId)[key] = (Ser(record), NewLock());
        Writes.Add((playerId, key, true));
        return Task.FromResult(true);
    }

    public Task DeletePurchasesAsync(IExecutionContext context, string playerId, IReadOnlyCollection<string> tokenHashes, CancellationToken ct)
    {
        foreach (string h in tokenHashes) P(playerId).Remove("p_" + h);
        return Task.CompletedTask;
    }

    private HashSet<string> L(string player) => LegacyReserved.TryGetValue(player, out var r) ? r : LegacyReserved[player] = new HashSet<string>();

    public Task<List<string>> ListLegacyReservedAsync(IExecutionContext context, string playerId, CancellationToken ct)
    {
        OnOp?.Invoke(context);
        return Task.FromResult(L(playerId).ToList());
    }

    public Task DeleteLegacyReservedAsync(IExecutionContext context, string playerId, IReadOnlyCollection<string> tokenHashes, CancellationToken ct)
    {
        foreach (string h in tokenHashes) L(playerId).Remove(h);
        return Task.CompletedTask;
    }

    public async Task<bool> IsClosedAsync(IExecutionContext context, string playerId, CancellationToken ct)
    {
        OnOp?.Invoke(context);
        await Gate(ct);
        return Closed.Contains(playerId) || LegacyClosed.Contains(playerId);
    }

    public Task CloseAsync(IExecutionContext context, string playerId, DateTime utcNow, CancellationToken ct)
    {
        OnOp?.Invoke(context);
        if (CloseError != null) throw CloseError;
        Room(playerId, !Closed.Contains(playerId));
        Closed.Add(playerId);
        return Task.CompletedTask;
    }

    public IndexEntry? IndexOf(string tokenHash) => Index.TryGetValue(tokenHash, out var v) ? Deser<IndexEntry>(v.json) : null;

    /// <summary>다른 호출의 색인 쓰기 (lock이 바뀐다)</summary>
    public void PutIndex(string tokenHash, IndexEntry entry) => Index[tokenHash] = (Ser(entry), NewLock());

    public Task<Dictionary<string, IndexEntry>> FindIndexAsync(IExecutionContext context, IReadOnlyCollection<string> tokenHashes, CancellationToken ct)
    {
        OnOp?.Invoke(context);
        return Task.FromResult(tokenHashes.Distinct().Where(Index.ContainsKey).ToDictionary(h => h, h => IndexOf(h)!));
    }

    public Task<(IndexEntry? entry, string? writeLock)> LoadIndexAsync(IExecutionContext context, string tokenHash, CancellationToken ct)
    {
        OnOp?.Invoke(context);
        return Task.FromResult(Index.TryGetValue(tokenHash, out var v) ? (Deser<IndexEntry>(v.json), v.writeLock) : ((IndexEntry?)null, (string?)null));
    }

    public Task<bool> TrySetIndexAsync(IExecutionContext context, string tokenHash, IndexEntry entry, string? writeLock, CancellationToken ct)
    {
        OnOp?.Invoke(context);
        BeforeIndexSet?.Invoke(this, tokenHash);
        if (writeLock != null && (!Index.TryGetValue(tokenHash, out var v) || v.writeLock != writeLock)) return Task.FromResult(false);
        PutIndex(tokenHash, entry);
        AfterIndexSet?.Invoke(this, tokenHash);
        return Task.FromResult(true);
    }

    public Task SetAckAsync(IExecutionContext context, string tokenHash, PendingAck ack, CancellationToken ct)
    {
        OnOp?.Invoke(context);
        Acks[tokenHash] = Ser(ack);
        return Task.CompletedTask;
    }

    public PendingAck? AckOf(string tokenHash) => Acks.TryGetValue(tokenHash, out string? json) ? Deser<PendingAck>(json) : null;

    public Task<Dictionary<string, PendingAck>> FindAcksAsync(IExecutionContext context, IReadOnlyCollection<string> tokenHashes, CancellationToken ct)
    {
        OnOp?.Invoke(context);
        return Task.FromResult(tokenHashes.Distinct().Where(Acks.ContainsKey).ToDictionary(h => h, h => Deser<PendingAck>(Acks[h])));
    }

    public Task<Dictionary<string, PendingAck>> ListAcksAsync(IExecutionContext context, CancellationToken ct) =>
        Task.FromResult(Acks.ToDictionary(kv => kv.Key, kv => Deser<PendingAck>(kv.Value)));

    public Task DeleteAckAsync(IExecutionContext context, string tokenHash, CancellationToken ct)
    {
        OnOp?.Invoke(context);
        Acks.Remove(tokenHash);
        return Task.CompletedTask;
    }

    public JobState? Job => JobStateJson == null ? null : Deser<JobState>(JobStateJson);

    /// <summary>다른 호출이 작업 상태를 바꿨다 (문지기 lock이 바뀐다)</summary>
    public void PutJobState(JobState state)
    {
        JobStateJson = Ser(state);
        GateLock = NewLock();
    }

    public Task<string?> LoadJobGateAsync(IExecutionContext context, CancellationToken ct) => Task.FromResult(GateLock);

    public Task CreateJobGateAsync(IExecutionContext context, CancellationToken ct)
    {
        GateLock = NewLock(); // 문지기 내용은 늘 같다 — 상태는 건드리지 않는다
        AfterCreateGate?.Invoke(this);
        return Task.CompletedTask;
    }

    public Task<JobState?> LoadJobStateAsync(IExecutionContext context, CancellationToken ct) => Task.FromResult(Job);

    public Task<string?> TrySaveJobAsync(IExecutionContext context, string gateLock, JobState state, CancellationToken ct)
    {
        BeforeJobSave?.Invoke(this, ++_jobSaves);
        if (gateLock != GateLock) return Task.FromResult<string?>(null);
        PutJobState(state);
        return Task.FromResult<string?>(GateLock);
    }

    public ArchivedPlayer? ArchiveOf(string id) => Archives.TryGetValue(id, out var a) ? Deser<ArchivedPlayer>(a.json) : null;

    public Task<(ArchivedPlayer? archive, string? writeLock)> LoadArchiveAsync(IExecutionContext context, string archiveId, CancellationToken ct) =>
        Task.FromResult(Archives.TryGetValue(archiveId, out var a) ? (Deser<ArchivedPlayer>(a.json), a.writeLock) : ((ArchivedPlayer?)null, (string?)null));

    public Task<bool> TrySetArchiveAsync(IExecutionContext context, ArchivedPlayer archive, string? writeLock, CancellationToken ct)
    {
        if (writeLock != null && (!Archives.TryGetValue(archive.Id, out var a) || a.writeLock != writeLock)) return Task.FromResult(false);
        Archives[archive.Id] = (Ser(archive), NewLock());
        return Task.FromResult(true);
    }

    public Task<List<ArchivedPlayer>> LoadArchiveShardAsync(IExecutionContext context, int shard, CancellationToken ct) =>
        Task.FromResult(Archives.Where(kv => kv.Key.StartsWith(shard.ToString("x2"))).Select(kv => Deser<ArchivedPlayer>(kv.Value.json)).ToList());

    public Task DeleteArchiveAsync(IExecutionContext context, string archiveId, CancellationToken ct)
    {
        Archives.Remove(archiveId);
        return Task.CompletedTask;
    }

    public Task SetClosingAsync(IExecutionContext context, string archiveId, ClosingPlayer item, CancellationToken ct)
    {
        Closing[archiveId] = Ser(item);
        return Task.CompletedTask;
    }

    public Task<Dictionary<string, ClosingPlayer>> ListClosingAsync(IExecutionContext context, CancellationToken ct) =>
        Task.FromResult(Closing.ToDictionary(kv => kv.Key, kv => Deser<ClosingPlayer>(kv.Value)));

    public Task DeleteClosingAsync(IExecutionContext context, string archiveId, CancellationToken ct)
    {
        Closing.Remove(archiveId);
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
