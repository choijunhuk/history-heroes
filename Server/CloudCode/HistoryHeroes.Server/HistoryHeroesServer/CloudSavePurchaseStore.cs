using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;
using Unity.Services.CloudSave.Model;

namespace HistoryHeroes.Server;

/// <summary>
/// 서버 저장소 = Cloud Save (모두 서버 토큰으로만 쓴다). Cloud Save에는 "없을 때만 만들기"가 없어서, 진짜 내용은 lock으로 고치거나 겹칠 수 없는 새 키에만 쓴다.
/// - 결제 장부 (protected 플레이어 데이터, 게임은 자기 것을 읽기만):
///   iap_head = 머리. 내용은 처음엔 늘 {"v":1,"n":""}, 결제를 더할 때마다 n이 그 토큰 해시로 바뀐다 — lock을 바꾸는 게 목적 (판정을 한 줄로 세운다)
///   p_{토큰 해시} = 결제 한 건. 머리와 한 batch로 만든다 (batch는 한 접근 등급 안에서 전부 되거나 전부 안 된다 — Cloud Save 문서 "item-batch … atomic")
/// - 닫힘 표시 (private 플레이어 데이터 iap_closed): 계정 삭제로 보관하고 닫은 플레이어. 서버만 읽고 쓴다
/// - 게임 전체 (private custom, 게임은 못 읽는다): 토큰 색인 iap_index_{해시 앞 2자}/{해시} · 확정 대기 iap_ack/{해시} · 작업 상태 iap_jobs/state ·
///   지운 계정 보관본 iap_archive_{ID 앞 2자}/{ID}
/// </summary>
public sealed class CloudSavePurchaseStore : IPurchaseStore
{
    public const string HeadKey = "iap_head";
    public const string PurchasePrefix = "p_";
    private const string ClosedKey = "iap_closed";
    private const string AckId = "iap_ack";
    private const string JobsId = "iap_jobs";
    private const string JobKey = "state";
    /// <summary>Cloud Save는 한 번에 키 20개까지 읽는다</summary>
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

    private static JObject Head(string marker) => new() { ["v"] = 1, ["n"] = marker };

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

    private async Task IgnoreMissing(Func<Task> delete)
    {
        try
        {
            await delete();
        }
        catch (ApiException e) when (IsStatus(e, HttpStatusCode.NotFound))
        {
        }
    }

    // ------------------------------------------------------------------ 플레이어 결제 장부

    public async Task<string?> LoadHeadLockAsync(IExecutionContext context, string playerId, CancellationToken ct)
    {
        ApiResponse<GetItemsResponse> response = await _api.CloudSaveData.GetProtectedItemsAsync(
            context, context.ServiceToken, context.ProjectId, playerId, new List<string> { HeadKey }, cancellationToken: ct);
        return response.Data?.Results?.Find(i => i.Key == HeadKey)?.WriteLock;
    }

    public async Task CreateHeadAsync(IExecutionContext context, string playerId, CancellationToken ct)
    {
        await _api.CloudSaveData.SetProtectedItemAsync(context, context.ServiceToken, context.ProjectId, playerId, new SetItemBody(HeadKey, Head("")), ct);
    }

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

    public async Task<bool> TryAddPurchaseAsync(IExecutionContext context, string playerId, string headLock, PurchaseRecord record, CancellationToken ct)
    {
        try
        {
            await _api.CloudSaveData.SetProtectedItemBatchAsync(context, context.ServiceToken, context.ProjectId, playerId, new SetItemBatchBody(new List<SetItemBody>
            {
                new(HeadKey, Head(record.TokenHash), headLock),
                new(PurchaseKey(record.TokenHash), JObject.FromObject(record)), // 새 키: 머리 lock이 같은 판정 둘을 막으니 겹치지 않는다
            }), ct);
            return true;
        }
        catch (ApiException e) when (IsStatus(e, HttpStatusCode.Conflict))
        {
            return false;
        }
    }

    public async Task<bool> TryUpdatePurchaseAsync(IExecutionContext context, string playerId, PurchaseRecord record, string writeLock, CancellationToken ct)
    {
        try
        {
            await _api.CloudSaveData.SetProtectedItemAsync(context, context.ServiceToken, context.ProjectId, playerId,
                new SetItemBody(PurchaseKey(record.TokenHash), JObject.FromObject(record), writeLock), ct);
            return true;
        }
        catch (ApiException e) when (IsStatus(e, HttpStatusCode.Conflict))
        {
            return false;
        }
    }

    public Task DeletePlayerPurchasesAsync(IExecutionContext context, string playerId, CancellationToken ct) =>
        IgnoreMissing(() => _api.CloudSaveData.DeleteProtectedItemsAsync(context, context.ServiceToken, context.ProjectId, playerId, ct));

    // ------------------------------------------------------------------ 닫힘 표시

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

    // ------------------------------------------------------------------ 토큰 색인

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

    public async Task SetIndexAsync(IExecutionContext context, string tokenHash, IndexEntry entry, CancellationToken ct)
    {
        await _api.CloudSaveData.SetPrivateCustomItemAsync(context, context.ServiceToken, context.ProjectId, IndexId(tokenHash),
            new SetItemBody(tokenHash, JObject.FromObject(entry)), ct);
    }

    // ------------------------------------------------------------------ 확정 대기

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

    // ------------------------------------------------------------------ 작업 상태

    public async Task<(JobState? state, string? writeLock)> LoadJobAsync(IExecutionContext context, CancellationToken ct)
    {
        ApiResponse<GetItemsResponse> response = await _api.CloudSaveData.GetPrivateCustomItemsAsync(
            context, context.ServiceToken, context.ProjectId, JobsId, new List<string> { JobKey }, cancellationToken: ct);
        Item? item = response.Data?.Results?.Find(i => i.Key == JobKey);
        return item == null ? (null, null) : (Read<JobState>(item) ?? new JobState(), item.WriteLock);
    }

    public async Task CreateJobAsync(IExecutionContext context, CancellationToken ct)
    {
        await _api.CloudSaveData.SetPrivateCustomItemAsync(context, context.ServiceToken, context.ProjectId, JobsId,
            new SetItemBody(JobKey, JObject.FromObject(new JobState())), ct);
    }

    public async Task<string?> TrySaveJobAsync(IExecutionContext context, JobState state, string writeLock, CancellationToken ct)
    {
        try
        {
            ApiResponse<SetItemResponse> response = await _api.CloudSaveData.SetPrivateCustomItemAsync(context, context.ServiceToken, context.ProjectId, JobsId,
                new SetItemBody(JobKey, JObject.FromObject(state), writeLock), ct);
            return response.Data?.WriteLock ?? string.Empty;
        }
        catch (ApiException e) when (IsStatus(e, HttpStatusCode.Conflict))
        {
            return null;
        }
    }

    // ------------------------------------------------------------------ 지운 계정 보관본

    public async Task SetArchiveAsync(IExecutionContext context, ArchivedPlayer archive, CancellationToken ct)
    {
        await _api.CloudSaveData.SetPrivateCustomItemAsync(context, context.ServiceToken, context.ProjectId, ArchiveShardId(archive.Id),
            new SetItemBody(archive.Id, JObject.FromObject(archive)), ct);
    }

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
}
