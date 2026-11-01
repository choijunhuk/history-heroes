using Newtonsoft.Json;

namespace HistoryHeroes.Server;

/// <summary>
/// CloseForSwitch · CancelSwitch의 답. status: ok · has_purchases(결제 · 예약이 있어 바꾸지 않음 — 다시 열었다) · closed(계정 삭제로 닫혀 다시 열 수 없음) · retry
/// </summary>
public sealed class SwitchResult
{
    public const string HasPurchases = "has_purchases";

    [JsonProperty("status")] public string Status { get; set; } = PurchaseList.Ok;

    public static SwitchResult Of(string status) => new() { Status = status };
}
