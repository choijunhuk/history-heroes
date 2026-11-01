using Newtonsoft.Json;

namespace HistoryHeroes.Server;

/// <summary>
/// GetPurchases의 답: 이 플레이어의 서버 장부 전부 (환불된 것은 voided 표시 — 게임이 회수한다) + 결제 검증 준비 여부.
/// status가 ok가 아니면(retry) 장부를 믿지 말고 나중에 다시 부른다.
/// </summary>
public sealed class PurchaseList
{
    public const string Ok = "ok";

    [JsonProperty("status")] public string Status { get; set; } = Ok;
    [JsonProperty("configured")] public bool Configured { get; set; }
    [JsonProperty("purchases")] public List<PurchaseRecord> Purchases { get; set; } = new();
}
