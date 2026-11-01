using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;
using Unity.Services.CloudSave.Model;

namespace HistoryHeroes.Server;

/// <summary>
/// 서버 저장소 = Cloud Save (모두 서버 토큰으로만 쓴다). Cloud Save에는 "없을 때만 만들기"가 없어서, 진짜 내용은 lock으로 고치거나 lock으로 지킨 batch 안에서,
/// 또는 겹칠 수 없는 새 키에만 쓴다. batch는 한 접근 등급 안에서 전부 되거나 전부 안 된다
/// (Cloud Save Data API: "The batch set operation is considered atomic and if any of the set key operation fails, the entire operation is failed", 충돌 409).
/// - 결제 장부 (protected 플레이어 데이터, 게임은 자기 것을 읽기만): iap_head(머리) · p_{토큰 해시}(결제 한 건) · r_{토큰 해시}(예약 — 색인에 이 플레이어를 적을 수 있는 토큰)
/// - 플레이어 서버 전용 (private 플레이어 데이터): iap_closed(닫힘 표시)
/// - 게임 전체 (private custom, 게임은 못 읽는다): 토큰 색인 iap_index_{해시 앞 2자}/{해시} · 확정 대기 iap_ack/{해시} · 작업 iap_jobs/gate · state ·
///   지운 계정 보관본 iap_archive_{ID 앞 2자}/{ID} · 계정 삭제 마무리 iap_closing/{보관본 ID}
/// </summary>
public sealed class CloudSavePurchaseStore : IPurchaseStore
{
    public const string HeadKey = "iap_head";
    public const string PurchasePrefix = "p_";
    private const string ReservedPrefix = "r_";
    private const string ClosedKey = "iap_closed";
    private const string AckId = "iap_ack";
    private const string JobsId = "iap_jobs";
    private const string GateKey = "gate";
    private const string StateKey = "state";
    private const string ClosingId = "iap_closing";
    /// <summary>Cloud Save는 한 번에 키 20개까지 읽고, batch도 20칸까지</summary>
    private const int KeysPerRead = 20;
    /// <summary>쪽 넘기기 한도 (한 플레이어 · 한 칸의 키 2000개 한도 / 쪽당 20개 = 100쪽)</summary>
    private const int MaxPages = 100;

    private readonly IGameApiClient _api;

    public CloudSavePurchaseStore(IGameApiClient api) => _api = api;

    internal static string PurchaseKey(string tokenHash) => PurchasePrefix + tokenHash;

    internal static string IndexId(string tokenHash) => "iap_index_" + tokenHash[..2];

    internal static string ArchiveShardId(int shard) => "iap_archive_" + shard.ToString("x2");

    private static string ArchiveShardId(string archiveId) => "iap_archive_" + archiveId[..2];

    private static T? Read<T>(Item item) where T : class =>
        JsonConvert.DeserializeObject<T>(item.Value as string ?? JsonConvert.SerializeObject(item.Value));

    private static bool IsStatus(ApiException e, HttpStatusCode status) => e.Response?.StatusCode == status;

    /// <summary>머리 내용: 처음엔 늘 {"v":1,"n":"","s":"open"} — 결제를 더할 때 n이 바뀌고(lock을 바꾸려고), 닫을 때 s가 closed</summary>
    private static JObject Head(string marker, bool closed = false) => new() { ["v"] = 1, ["n"] = marker, ["s"] = closed ? "closed" : "open" };

    /// <summary>키를 주지 않고 읽어 쪽을 넘기며 모두 모은다</summary>
    private static async Task<List<Item>> AllPagesAsync(Func<string?, Task<ApiResponse<GetItemsResponse>>> page)
    {
        var all = new List<Item>();
        string? after = null;
        for (int i = 0; i < MaxPages; i++)
        {
            ApiResponse<GetItemsResponse> response = await page(after);
            List<Item> items = response.Data?.Results ?? new List<Item>();
            all.AddRange(items);
            if (items.Count == 0 || string.IsNullOrEmpty(response.Data?.Links?.Next)) break;
            after = items[^1].Key;
        }

        return all;
    }

    private static async Task IgnoreMissing(Func<Task> delete)
    {
        try
        {
            await delete();
        }
        catch (ApiException e) when (IsStatus(e, HttpStatusCode.NotFound))
        {
        }
    }

    private static async Task<bool> NotConflict(Func<Task> write)
    {
        try
        {
            await write();
            return true;
        }
        catch (ApiException e) when (IsStatus(e, HttpStatusCode.Conflict))
        {
            return false;
        }
    }

    // ------------------------------------------------------------------ 플레이어 결제 장부

    public async Task<(string? writeLock, bool closed)> LoadHeadAsync(IExecutionContext context, string playerId, CancellationToken ct)
    {
        ApiResponse<GetItemsResponse> response = await _api.CloudSaveData.GetProtectedItemsAsync(
            context, context.ServiceToken, context.ProjectId, playerId, new List<string> { HeadKey }, cancellationToken: ct);
        Item? item = response.Data?.Results?.Find(i => i.Key == HeadKey);
        if (item == null) return (null, false);
        var head = JObject.FromObject(item.Value ?? new JObject());
        return (item.WriteLock, (string?)head["s"] == "closed");
    }

    public async Task CreateHeadAsync(IExecutionContext context, string playerId, CancellationToken ct)
    {
        await _api.CloudSaveData.SetProtectedItemAsync(context, context.ServiceToken, context.ProjectId, playerId, new SetItemBody(HeadKey, Head("")), ct);
    }

    public Task<bool> TryCloseHeadAsync(IExecutionContext context, string playerId, string headLock, CancellationToken ct) =>
        NotConflict(() => _api.CloudSaveData.SetProtectedItemAsync(context, context.ServiceToken, context.ProjectId, playerId,
            new SetItemBody(HeadKey, Head("closed", closed: true), headLock), ct));

    public async Task<List<PurchaseRecord>> LoadPurchasesAsync(IExecutionContext context, string playerId, CancellationToken ct)
    {
        List<Item> items = await AllPagesAsync(after => _api.CloudSaveData.GetProtectedItemsAsync(
            context, context.ServiceToken, context.ProjectId, playerId, null!, after!, ct));
        var purchases = new List<PurchaseRecord>();
        foreach (Item item in items)
            if (item.Key.StartsWith(PurchasePrefix, StringComparison.Ordinal) && Read<PurchaseRecord>(item) is { } p) purchases.Add(p);
        purchases.Sort((a, b) => string.CompareOrdinal(a.Utc, b.Utc));
        return purchases;
    }

    public async Task<(PurchaseRecord? record, string? writeLock)> LoadPurchaseAsync(IExecutionContext context, string playerId, string tokenHash, CancellationToken ct)
    {
        string key = PurchaseKey(tokenHash);
        ApiResponse<GetItemsResponse> response = await _api.CloudSaveData.GetProtectedItemsAsync(
            context, context.ServiceToken, context.ProjectId, playerId, new List<string> { key }, cancellationToken: ct);
        Item? item = response.Data?.Results?.Find(i => i.Key == key);
        return item == null ? (null, null) : (Read<PurchaseRecord>(item), item.WriteLock);
    }

    public Task<bool> TryAddPurchaseAsync(IExecutionContext context, string playerId, string headLock, PurchaseRecord record, CancellationToken ct) =>
        NotConflict(() => _api.CloudSaveData.SetProtectedItemBatchAsync(context, context.ServiceToken, context.ProjectId, playerId, new SetItemBatchBody(new List<SetItemBody>
        {
            new(HeadKey, Head(record.TokenHash), headLock),
            new(PurchaseKey(record.TokenHash), JObject.FromObject(record)), // 새 키: 머리 lock이 같은 판정 둘을 막으니 겹치지 않는다
        }), ct));

    public Task<bool> TryReserveAsync(IExecutionContext context, string playerId, string headLock, string tokenHash, CancellationToken ct) =>
        NotConflict(() => _api.CloudSaveData.SetProtectedItemBatchAsync(context, context.ServiceToken, context.ProjectId, playerId, new SetItemBatchBody(new List<SetItemBody>
        {
            new(HeadKey, Head("r:" + tokenHash), headLock),
            new(ReservedPrefix + tokenHash, true),
        }), ct));

    public async Task<List<string>> ListReservedAsync(IExecutionContext context, string playerId, CancellationToken ct)
    {
        List<Item> items = await AllPagesAsync(after => _api.CloudSaveData.GetProtectedItemsAsync(
            context, context.ServiceToken, context.ProjectId, playerId, null!, after!, ct));
        return items.Where(i => i.Key.StartsWith(ReservedPrefix, StringComparison.Ordinal)).Select(i => i.Key[ReservedPrefix.Length..]).ToList();
    }

    public async Task DeleteReservedAsync(IExecutionContext context, string playerId, IReadOnlyCollection<string> tokenHashes, CancellationToken ct)
    {
        foreach (string[] batch in tokenHashes.Distinct().Chunk(10))
            await Task.WhenAll(batch.Select(h => IgnoreMissing(() =>
                _api.CloudSaveData.DeleteProtectedItemAsync(context, context.ServiceToken, context.ProjectId, playerId, ReservedPrefix + h, cancellationToken: ct))));
    }

    public Task<bool> TryUpdatePurchaseAsync(IExecutionContext context, string playerId, PurchaseRecord record, string writeLock, CancellationToken ct) =>
        NotConflict(() => _api.CloudSaveData.SetProtectedItemAsync(context, context.ServiceToken, context.ProjectId, playerId,
            new SetItemBody(PurchaseKey(record.TokenHash), JObject.FromObject(record), writeLock), ct));

    public async Task DeletePurchasesAsync(IExecutionContext context, string playerId, IReadOnlyCollection<string> tokenHashes, CancellationToken ct)
    {
        foreach (string[] batch in tokenHashes.Distinct().Chunk(10))
            await Task.WhenAll(batch.Select(h => IgnoreMissing(() =>
                _api.CloudSaveData.DeleteProtectedItemAsync(context, context.ServiceToken, context.ProjectId, playerId, PurchaseKey(h), cancellationToken: ct))));
    }

    // ------------------------------------------------------------------ 플레이어 서버 전용

    public async Task<bool> IsClosedAsync(IExecutionContext context, string playerId, CancellationToken ct)
    {
        ApiResponse<GetItemsResponse> response = await _api.CloudSaveData.GetPrivateItemsAsync(
            context, context.ServiceToken, context.ProjectId, playerId, new List<string> { ClosedKey }, cancellationToken: ct);
        return response.Data?.Results?.Exists(i => i.Key == ClosedKey) == true;
    }

    public async Task CloseAsync(IExecutionContext context, string playerId, DateTime utcNow, CancellationToken ct)
    {
        await _api.CloudSaveData.SetPrivateItemAsync(context, context.ServiceToken, context.ProjectId, playerId,
            new SetItemBody(ClosedKey, new JObject { ["utc"] = utcNow.ToString("o") }), ct);
    }

    // ------------------------------------------------------------------ 토큰 색인 · 확정 대기

    public async Task<Dictionary<string, IndexEntry>> FindIndexAsync(IExecutionContext context, IReadOnlyCollection<string> tokenHashes, CancellationToken ct)
    {
        var found = new Dictionary<string, IndexEntry>();
        foreach (IGrouping<string, string> shard in tokenHashes.Distinct().GroupBy(IndexId))
        {
            foreach (string[] keys in shard.Chunk(KeysPerRead))
            {
                ApiResponse<GetItemsResponse> response = await _api.CloudSaveData.GetPrivateCustomItemsAsync(
                    context, context.ServiceToken, context.ProjectId, shard.Key, keys.ToList(), cancellationToken: ct);
                foreach (Item item in response.Data?.Results ?? new List<Item>())
                    if (Read<IndexEntry>(item) is { } entry) found[item.Key] = entry;
            }
        }

        return found;
    }

    public async Task<(IndexEntry? entry, string? writeLock)> LoadIndexAsync(IExecutionContext context, string tokenHash, CancellationToken ct)
    {
        ApiResponse<GetItemsResponse> response = await _api.CloudSaveData.GetPrivateCustomItemsAsync(
            context, context.ServiceToken, context.ProjectId, IndexId(tokenHash), new List<string> { tokenHash }, cancellationToken: ct);
        Item? item = response.Data?.Results?.Find(i => i.Key == tokenHash);
        return item == null ? (null, null) : (Read<IndexEntry>(item), item.WriteLock);
    }

    public Task<bool> TrySetIndexAsync(IExecutionContext context, string tokenHash, IndexEntry entry, string? writeLock, CancellationToken ct) =>
        NotConflict(() => _api.CloudSaveData.SetPrivateCustomItemAsync(context, context.ServiceToken, context.ProjectId, IndexId(tokenHash),
            new SetItemBody(tokenHash, JObject.FromObject(entry), writeLock!), ct));

    public async Task SetAckAsync(IExecutionContext context, string tokenHash, PendingAck ack, CancellationToken ct)
    {
        await _api.CloudSaveData.SetPrivateCustomItemAsync(context, context.ServiceToken, context.ProjectId, AckId,
            new SetItemBody(tokenHash, JObject.FromObject(ack)), ct);
    }

    public async Task<Dictionary<string, PendingAck>> FindAcksAsync(IExecutionContext context, IReadOnlyCollection<string> tokenHashes, CancellationToken ct)
    {
        var found = new Dictionary<string, PendingAck>();
        foreach (string[] keys in tokenHashes.Distinct().Chunk(KeysPerRead))
        {
            ApiResponse<GetItemsResponse> response = await _api.CloudSaveData.GetPrivateCustomItemsAsync(
                context, context.ServiceToken, context.ProjectId, AckId, keys.ToList(), cancellationToken: ct);
            foreach (Item item in response.Data?.Results ?? new List<Item>())
                if (Read<PendingAck>(item) is { } ack) found[item.Key] = ack;
        }

        return found;
    }

    public async Task<Dictionary<string, PendingAck>> ListAcksAsync(IExecutionContext context, CancellationToken ct)
    {
        List<Item> items = await AllPagesAsync(after => _api.CloudSaveData.GetPrivateCustomItemsAsync(
            context, context.ServiceToken, context.ProjectId, AckId, null!, after!, ct));
        var all = new Dictionary<string, PendingAck>();
        foreach (Item item in items)
            if (Read<PendingAck>(item) is { } ack) all[item.Key] = ack;
        return all;
    }

    public Task DeleteAckAsync(IExecutionContext context, string tokenHash, CancellationToken ct) =>
        IgnoreMissing(() => _api.CloudSaveData.DeletePrivateCustomItemAsync(context, context.ServiceToken, context.ProjectId, AckId, tokenHash, cancellationToken: ct));

    // ------------------------------------------------------------------ 작업 문지기 · 상태

    public async Task<string?> LoadJobGateAsync(IExecutionContext context, CancellationToken ct)
    {
        ApiResponse<GetItemsResponse> response = await _api.CloudSaveData.GetPrivateCustomItemsAsync(
            context, context.ServiceToken, context.ProjectId, JobsId, new List<string> { GateKey }, cancellationToken: ct);
        return response.Data?.Results?.Find(i => i.Key == GateKey)?.WriteLock;
    }

    public async Task CreateJobGateAsync(IExecutionContext context, CancellationToken ct)
    {
        await _api.CloudSaveData.SetPrivateCustomItemAsync(context, context.ServiceToken, context.ProjectId, JobsId,
            new SetItemBody(GateKey, new JObject { ["v"] = 1 }), ct);
    }

    public async Task<JobState?> LoadJobStateAsync(IExecutionContext context, CancellationToken ct)
    {
        ApiResponse<GetItemsResponse> response = await _api.CloudSaveData.GetPrivateCustomItemsAsync(
            context, context.ServiceToken, context.ProjectId, JobsId, new List<string> { StateKey }, cancellationToken: ct);
        Item? item = response.Data?.Results?.Find(i => i.Key == StateKey);
        return item == null ? null : Read<JobState>(item);
    }

    public async Task<string?> TrySaveJobAsync(IExecutionContext context, string gateLock, JobState state, CancellationToken ct)
    {
        try
        {
            ApiResponse<SetItemBatchResponse> response = await _api.CloudSaveData.SetPrivateCustomItemBatchAsync(context, context.ServiceToken, context.ProjectId, JobsId,
                new SetItemBatchBody(new List<SetItemBody>
                {
                    new(GateKey, new JObject { ["v"] = 1 }, gateLock),
                    new(StateKey, JObject.FromObject(state)), // 문지기 lock이 지키는 batch 안에서만 쓴다
                }), ct);
            return response.Data?.Results?.Find(r => r.Key == GateKey)?.WriteLock ?? string.Empty;
        }
        catch (ApiException e) when (IsStatus(e, HttpStatusCode.Conflict))
        {
            return null;
        }
    }

    // ------------------------------------------------------------------ 지운 계정 보관본

    public async Task<(ArchivedPlayer? archive, string? writeLock)> LoadArchiveAsync(IExecutionContext context, string archiveId, CancellationToken ct)
    {
        ApiResponse<GetItemsResponse> response = await _api.CloudSaveData.GetPrivateCustomItemsAsync(
            context, context.ServiceToken, context.ProjectId, ArchiveShardId(archiveId), new List<string> { archiveId }, cancellationToken: ct);
        Item? item = response.Data?.Results?.Find(i => i.Key == archiveId);
        return item == null ? (null, null) : (Read<ArchivedPlayer>(item), item.WriteLock);
    }

    public Task<bool> TrySetArchiveAsync(IExecutionContext context, ArchivedPlayer archive, string? writeLock, CancellationToken ct) =>
        NotConflict(() => _api.CloudSaveData.SetPrivateCustomItemAsync(context, context.ServiceToken, context.ProjectId, ArchiveShardId(archive.Id),
            new SetItemBody(archive.Id, JObject.FromObject(archive), writeLock!), ct));

    public async Task<List<ArchivedPlayer>> LoadArchiveShardAsync(IExecutionContext context, int shard, CancellationToken ct)
    {
        List<Item> items = await AllPagesAsync(after => _api.CloudSaveData.GetPrivateCustomItemsAsync(
            context, context.ServiceToken, context.ProjectId, ArchiveShardId(shard), null!, after!, ct));
        var all = new List<ArchivedPlayer>();
        foreach (Item item in items)
            if (Read<ArchivedPlayer>(item) is { } archive) all.Add(archive);
        return all;
    }

    public Task DeleteArchiveAsync(IExecutionContext context, string archiveId, CancellationToken ct) =>
        IgnoreMissing(() => _api.CloudSaveData.DeletePrivateCustomItemAsync(context, context.ServiceToken, context.ProjectId, ArchiveShardId(archiveId), archiveId, cancellationToken: ct));

    // ------------------------------------------------------------------ 계정 삭제 마무리

    public async Task SetClosingAsync(IExecutionContext context, string archiveId, ClosingPlayer item, CancellationToken ct)
    {
        await _api.CloudSaveData.SetPrivateCustomItemAsync(context, context.ServiceToken, context.ProjectId, ClosingId,
            new SetItemBody(archiveId, JObject.FromObject(item)), ct);
    }

    public async Task<Dictionary<string, ClosingPlayer>> ListClosingAsync(IExecutionContext context, CancellationToken ct)
    {
        List<Item> items = await AllPagesAsync(after => _api.CloudSaveData.GetPrivateCustomItemsAsync(
            context, context.ServiceToken, context.ProjectId, ClosingId, null!, after!, ct));
        var all = new Dictionary<string, ClosingPlayer>();
        foreach (Item item in items)
            if (Read<ClosingPlayer>(item) is { } c) all[item.Key] = c;
        return all;
    }

    public Task DeleteClosingAsync(IExecutionContext context, string archiveId, CancellationToken ct) =>
        IgnoreMissing(() => _api.CloudSaveData.DeletePrivateCustomItemAsync(context, context.ServiceToken, context.ProjectId, ClosingId, archiveId, cancellationToken: ct));
}
