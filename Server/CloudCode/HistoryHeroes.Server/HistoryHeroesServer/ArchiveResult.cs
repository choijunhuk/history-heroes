using Newtonsoft.Json;

namespace HistoryHeroes.Server;

/// <summary>ArchiveAndDeletePurchases의 답: ok면 장부를 보관본으로 옮기고 계정을 닫았다 (그 뒤 이 플레이어의 결제 함수는 모두 거절)</summary>
public sealed class ArchiveResult
{
    [JsonProperty("status")] public string Status { get; set; } = PurchaseList.Ok;
    [JsonProperty("archived")] public int Archived { get; set; }
    /// <summary>이미 보관하고 닫은 계정이다 (두 번 보관하지 않는다 — 남은 장부 지우기만 다시)</summary>
    [JsonProperty("alreadyArchived")] public bool AlreadyArchived { get; set; }
}
