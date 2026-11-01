using Newtonsoft.Json;

namespace HistoryHeroes.Server;

/// <summary>voidedpurchases.list의 한 칸 중 쓰는 것 (https://developers.google.com/android-publisher/api-ref/rest/v3/purchases.voidedpurchases)</summary>
public sealed class VoidedPurchase
{
    [JsonProperty("purchaseToken")] public string? PurchaseToken { get; set; }
    [JsonProperty("orderId")] public string? OrderId { get; set; }
    /// <summary>환불 · 취소된 시각 (밀리초, 구글은 글자로 준다)</summary>
    [JsonProperty("voidedTimeMillis")] public long VoidedTimeMillis { get; set; }
}
