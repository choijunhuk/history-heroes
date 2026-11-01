using Newtonsoft.Json;

namespace HistoryHeroes.Server;

/// <summary>
/// 서버 장부의 결제 한 건 (Cloud Save protected 플레이어 키 p_{토큰 해시} 하나 = 게임이 받는 지급 내역). 만든 뒤에는 확정 · 환불 표시만 바뀐다.
/// JSON 이름은 게임의 JsonUtility 칸 이름(Economy/PurchaseServer.ServerPurchase)과 글자까지 같아야 한다.
/// 구매 토큰은 적지 않고 SHA-256만 적는다 (환불 내역 API와는 해시 · 주문 번호로 맞춘다).
/// </summary>
public sealed class PurchaseRecord
{
    [JsonProperty("tokenHash")] public string TokenHash { get; set; } = "";
    /// <summary>구글 주문 번호 (GPA.…). 테스트 결제는 없을 수 있다</summary>
    [JsonProperty("orderId")] public string? OrderId { get; set; }
    [JsonProperty("productId")] public string ProductId { get; set; } = "";
    /// <summary>서버가 지급을 정한 시각 (UTC, ISO 8601)</summary>
    [JsonProperty("utc")] public string Utc { get; set; } = "";
    [JsonProperty("paidGems")] public int PaidGems { get; set; }
    [JsonProperty("freeGems")] public int FreeGems { get; set; }
    [JsonProperty("tickets")] public int Tickets { get; set; }
    [JsonProperty("gold")] public int Gold { get; set; }
    /// <summary>이 결제에서 첫 구매 2배를 받았다</summary>
    [JsonProperty("firstBonus")] public bool FirstBonus { get; set; }
    /// <summary>라이선스 테스터 결제 (purchaseType 0, 돈이 나가지 않음). 지급은 같다 — 매출 집계에서 뺀다</summary>
    [JsonProperty("test")] public bool Test { get; set; }
    /// <summary>환불 · 취소 · 차지백 (구글 voidedpurchases). 다시 지급하지 않고, 게임은 이 결제로 준 것을 회수한다 (LiveOps 3-4)</summary>
    [JsonProperty("voided")] public bool Voided { get; set; }
    [JsonProperty("voidedUtc", NullValueHandling = NullValueHandling.Ignore)] public string? VoidedUtc { get; set; }
    /// <summary>서버가 구글에 결제 확정(acknowledge)을 마쳤다 (또는 게임이 이미 소비했다). 안 하면 구글이 3일 뒤 환불한다.
    /// 확정을 다시 하려고 남기는 토큰 원문은 여기가 아니라 서버 전용 칸(PendingAck)에 둔다 — 이 장부는 플레이어가 읽을 수 있다</summary>
    [JsonProperty("acknowledged")] public bool Acknowledged { get; set; }

    public PurchaseRecord Copy() => (PurchaseRecord)MemberwiseClone();
}
