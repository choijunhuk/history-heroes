using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;
using Unity.Services.CloudSave.Model;

namespace HistoryHeroes.Server;

/// <summary>
/// 서버 장부 = Cloud Save **protected** 플레이어 데이터 키 "purchases" (게임은 읽을 수만 있고 쓸 수 없다 — 서버 토큰으로만 쓴다).
/// 값: { "v": 1, "purchases": [PurchaseRecord…] }. 쓰기는 읽은 판의 write lock을 붙인다 (서로 다른 호출이 동시에 더해도 하나는 충돌로 거절).
/// 계정 삭제(게임의 DeleteAllAsync)는 기본 데이터만 지워서 이 기록은 남는다 — 결제 기록 분리 보관 (LiveOps 3-3, 개인정보 처리방침 3장).
/// </summary>
public sealed class CloudSavePurchaseStore : IPurchaseStore
{
    public const string Key = "purchases";

    private readonly IGameApiClient _api;

    public CloudSavePurchaseStore(IGameApiClient api) => _api = api;

    private sealed class Stored
    {
        [JsonProperty("v")] public int Version { get; set; } = 1;
        [JsonProperty("purchases")] public List<PurchaseRecord> Purchases { get; set; } = new();
    }

    public async Task<(List<PurchaseRecord> purchases, string? writeLock)> LoadAsync(IExecutionContext context)
    {
        ApiResponse<GetItemsResponse> response = await _api.CloudSaveData.GetProtectedItemsAsync(
            context, context.ServiceToken, context.ProjectId, context.PlayerId!, new List<string> { Key });
        Item? item = response.Data?.Results?.Find(i => i.Key == Key);
        if (item == null) return (new List<PurchaseRecord>(), null);
        string json = item.Value as string ?? JsonConvert.SerializeObject(item.Value);
        Stored? stored = JsonConvert.DeserializeObject<Stored>(json);
        return (stored?.Purchases ?? new List<PurchaseRecord>(), item.WriteLock);
    }

    public async Task<bool> TrySaveAsync(IExecutionContext context, List<PurchaseRecord> purchases, string? writeLock)
    {
        try
        {
            await _api.CloudSaveData.SetProtectedItemAsync(context, context.ServiceToken, context.ProjectId, context.PlayerId!,
                new SetItemBody(Key, JObject.FromObject(new Stored { Purchases = purchases }), writeLock!));
            return true;
        }
        catch (ApiException e) when (e.Response?.StatusCode == HttpStatusCode.Conflict)
        {
            return false;
        }
    }
}
