using Newtonsoft.Json;

namespace HistoryHeroes.Server;

/// <summary>GetPurchases의 답: 이 플레이어의 서버 장부 전부 + 결제 검증 준비 여부 (게임은 준비가 안 됐으면 팔지 않는다)</summary>
public sealed class PurchaseList
{
    [JsonProperty("configured")] public bool Configured { get; set; }
    [JsonProperty("purchases")] public List<PurchaseRecord> Purchases { get; set; } = new();
}
