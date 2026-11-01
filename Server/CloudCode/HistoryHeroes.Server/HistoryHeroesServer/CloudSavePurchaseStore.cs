using System.Globalization;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;
using Unity.Services.CloudSave.Model;

namespace HistoryHeroes.Server;

/// <summary>
/// 서버 저장소 = Cloud Save (모두 서버 토큰으로만 쓴다).
/// - 플레이어 장부: **protected** 플레이어 데이터 키 "purchases" (게임은 자기 것을 읽을 수만 있다). 값 { "v": 1, "purchases": [PurchaseRecord…] }
/// - 토큰 해시 → 플레이어 색인: **private** custom 데이터 iap_index_{해시 앞 2자} (256칸으로 나눠 칸마다 키 2000개 한도 안에)
/// - 작업 상태: private custom iap_jobs/state
/// - 지운 계정 보관본: private custom iap_archive_{yyyyMM}/d{dd} (달마다 따로 → 기한이 지나면 달째로 지운다)
/// private custom 데이터는 게임(클라이언트)이 읽지 못한다. 쓰기는 읽은 판의 write lock을 붙인다 (충돌 = false).
/// 계정 삭제(게임의 DeleteAllAsync)는 기본 데이터만 지운다 → 장부는 ArchiveAndDeletePurchases가 보관본으로 옮긴 뒤 지운다.
/// </summary>
public sealed class CloudSavePurchaseStore : IPurchaseStore
{
    public const string Key = "purchases";
    private const string JobsId = "iap_jobs";
    private const string JobKey = "state";
    /// <summary>Cloud Save는 한 번에 키 20개까지 읽는다</summary>
    private const int KeysPerRead = 20;

    private readonly IGameApiClient _api;

    public CloudSavePurchaseStore(IGameApiClient api) => _api = api;

    private sealed class StoredLedger
    {
        [JsonProperty("v")] public int Version { get; set; } = 1;
        [JsonProperty("purchases")] public List<PurchaseRecord> Purchases { get; set; } = new();
    }

    private sealed class StoredArchive
    {
        [JsonProperty("players")] public List<ArchivedPlayer> Players { get; set; } = new();
    }

    internal static string IndexId(string tokenHash) => "iap_index_" + tokenHash[..2];

    internal static string ArchiveMonthId(string month) => "iap_archive_" + month;

    private static T? Read<T>(Item item) where T : class =>
        JsonConvert.DeserializeObject<T>(item.Value as string ?? JsonConvert.SerializeObject(item.Value));

    private static bool IsStatus(ApiException e, HttpStatusCode status) => e.Response?.StatusCode == status;

    // ------------------------------------------------------------------ 플레이어 장부

    public async Task<(List<PurchaseRecord> purchases, string? writeLock)> LoadAsync(IExecutionContext context, string playerId, CancellationToken ct)
    {
        ApiResponse<GetItemsResponse> response = await _api.CloudSaveData.GetProtectedItemsAsync(
            context, context.ServiceToken, context.ProjectId, playerId, new List<string> { Key }, cancellationToken: ct);
        Item? item = response.Data?.Results?.Find(i => i.Key == Key);
        if (item == null) return (new List<PurchaseRecord>(), null);
        return (Read<StoredLedger>(item)?.Purchases ?? new List<PurchaseRecord>(), item.WriteLock);
    }

    public async Task<bool> TrySaveAsync(IExecutionContext context, string playerId, List<PurchaseRecord> purchases, string? writeLock, CancellationToken ct)
    {
        try
        {
            await _api.CloudSaveData.SetProtectedItemAsync(context, context.ServiceToken, context.ProjectId, playerId,
                new SetItemBody(Key, JObject.FromObject(new StoredLedger { Purchases = purchases }), writeLock!), ct);
            return true;
        }
        catch (ApiException e) when (IsStatus(e, HttpStatusCode.Conflict))
        {
            return false;
        }
    }

    public async Task DeleteAsync(IExecutionContext context, string playerId, CancellationToken ct)
    {
        try
        {
            await _api.CloudSaveData.DeleteProtectedItemAsync(context, context.ServiceToken, context.ProjectId, playerId, Key, cancellationToken: ct);
        }
        catch (ApiException e) when (IsStatus(e, HttpStatusCode.NotFound))
        {
        }
    }

    // ------------------------------------------------------------------ 토큰 해시 → 플레이어 색인

    public async Task SetIndexAsync(IExecutionContext context, string tokenHash, string playerId, CancellationToken ct)
    {
        await _api.CloudSaveData.SetPrivateCustomItemAsync(context, context.ServiceToken, context.ProjectId, IndexId(tokenHash),
            new SetItemBody(tokenHash, playerId), ct);
    }

    public async Task<Dictionary<string, string>> FindIndexAsync(IExecutionContext context, IReadOnlyCollection<string> tokenHashes, CancellationToken ct)
    {
        var found = new Dictionary<string, string>();
        foreach (IGrouping<string, string> shard in tokenHashes.Distinct().GroupBy(IndexId))
        {
            foreach (string[] keys in shard.Chunk(KeysPerRead))
            {
                ApiResponse<GetItemsResponse> response = await _api.CloudSaveData.GetPrivateCustomItemsAsync(
                    context, context.ServiceToken, context.ProjectId, shard.Key, keys.ToList(), cancellationToken: ct);
                foreach (Item item in response.Data?.Results ?? new List<Item>())
                    if (item.Value?.ToString() is { Length: > 0 } player) found[item.Key] = player;
            }
        }

        return found;
    }

    public async Task DeleteIndexAsync(IExecutionContext context, IReadOnlyCollection<string> tokenHashes, CancellationToken ct)
    {
        foreach (string[] batch in tokenHashes.Distinct().Chunk(10))
        {
            await Task.WhenAll(batch.Select(async hash =>
            {
                try
                {
                    await _api.CloudSaveData.DeletePrivateCustomItemAsync(context, context.ServiceToken, context.ProjectId, IndexId(hash), hash, cancellationToken: ct);
                }
                catch (ApiException e) when (IsStatus(e, HttpStatusCode.NotFound))
                {
                }
            }));
        }
    }

    // ------------------------------------------------------------------ 작업 상태

    public async Task<(JobState state, string? writeLock)> LoadJobAsync(IExecutionContext context, CancellationToken ct)
    {
        ApiResponse<GetItemsResponse> response = await _api.CloudSaveData.GetPrivateCustomItemsAsync(
            context, context.ServiceToken, context.ProjectId, JobsId, new List<string> { JobKey }, cancellationToken: ct);
        Item? item = response.Data?.Results?.Find(i => i.Key == JobKey);
        return item == null ? (new JobState(), null) : (Read<JobState>(item) ?? new JobState(), item.WriteLock);
    }

    public async Task<string?> TrySaveJobAsync(IExecutionContext context, JobState state, string? writeLock, CancellationToken ct)
    {
        try
        {
            ApiResponse<SetItemResponse> response = await _api.CloudSaveData.SetPrivateCustomItemAsync(context, context.ServiceToken, context.ProjectId, JobsId,
                new SetItemBody(JobKey, JObject.FromObject(state), writeLock!), ct);
            return response.Data?.WriteLock ?? string.Empty;
        }
        catch (ApiException e) when (IsStatus(e, HttpStatusCode.Conflict))
        {
            return null;
        }
    }

    // ------------------------------------------------------------------ 지운 계정 보관본

    public async Task AppendArchiveAsync(IExecutionContext context, DateTime utcNow, ArchivedPlayer archive, CancellationToken ct)
    {
        string customId = ArchiveMonthId(utcNow.ToString("yyyyMM", CultureInfo.InvariantCulture));
        string key = "d" + utcNow.ToString("dd", CultureInfo.InvariantCulture);
        for (int attempt = 0; attempt < PurchaseVerifier.MaxAttempts; attempt++)
        {
            ApiResponse<GetItemsResponse> response = await _api.CloudSaveData.GetPrivateCustomItemsAsync(
                context, context.ServiceToken, context.ProjectId, customId, new List<string> { key }, cancellationToken: ct);
            Item? item = response.Data?.Results?.Find(i => i.Key == key);
            StoredArchive stored = (item == null ? null : Read<StoredArchive>(item)) ?? new StoredArchive();
            stored.Players.RemoveAll(p => p.Id == archive.Id); // 다시 불러도 한 번만 (마지막 것으로)
            stored.Players.Add(archive);
            try
            {
                // ponytail: 한 달 보관본이 한 custom 칸(5 MB)에 들어간다 — 결제 약 2만 건/달. 넘치면 날짜 칸을 시간 단위로 더 나눈다
                await _api.CloudSaveData.SetPrivateCustomItemAsync(context, context.ServiceToken, context.ProjectId, customId,
                    new SetItemBody(key, JObject.FromObject(stored), item?.WriteLock!), ct);
                return;
            }
            catch (ApiException e) when (IsStatus(e, HttpStatusCode.Conflict))
            {
            }
        }

        throw new InvalidOperationException("archive_conflict");
    }

    public async Task DeleteArchiveMonthAsync(IExecutionContext context, string month, CancellationToken ct)
    {
        try
        {
            await _api.CloudSaveData.DeletePrivateCustomItemsAsync(context, context.ServiceToken, context.ProjectId, ArchiveMonthId(month), ct);
        }
        catch (ApiException e) when (IsStatus(e, HttpStatusCode.NotFound))
        {
        }
    }
}
