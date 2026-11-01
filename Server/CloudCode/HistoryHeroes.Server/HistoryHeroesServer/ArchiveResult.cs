using Newtonsoft.Json;

namespace HistoryHeroes.Server;

/// <summary>ArchiveAndDeletePurchases의 답: ok면 장부를 보관본으로 옮기고 지웠다 (없었으면 할 일 없음)</summary>
public sealed class ArchiveResult
{
    [JsonProperty("status")] public string Status { get; set; } = PurchaseList.Ok;
    [JsonProperty("archived")] public int Archived { get; set; }
}
